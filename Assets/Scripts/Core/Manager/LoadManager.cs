using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Scripts.Core.Manager;
using Scripts.Core.SO;
using Scripts.Core.Utils;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

namespace Scripts.Core
{
    /*
     * LoadManager 주요 흐름
     * LoadAsyncScene
     * -> ReleaseHandle
     * -> LoadingScene
     * -> LoadingMainScene / LoadingTitleScene
     * -> LoadSceneResources
     * -> LoadStageResources
     * -> WaitForLoadingResources
     * -> LoadUnitySceneAsync
     */
    
    public class LoadManager : MonoBehaviour
    {
        public static LoadManager Instance;
        public event Action<eSceneType> SceneLoadStarted;
        public event Action<eSceneType> SceneLoadFinished;
        public event Action<eSceneType, float> SceneLoadProgress;

        //씬 , 스테이지와 관련된 몬스터, SFX,VFX정보들
        [SerializeField] MonsterMetaSO _monsterMetaDataSO;
        [SerializeField] SceneSFXMetaSO _sceneSFXMetaSO;
        [SerializeField] SceneVFXMetaSO _sceneVFXMetaSO;

        [Header("Scene Name Mapping")] 
        [SerializeField] private string bootstrapSceneName = "bootstrap";
        [SerializeField] private string titleSceneName = "title";
        [SerializeField] private string mainSceneName = "main";
        [SerializeField] private string dungeonSceneName = "dungeon";
        
        private AsyncOperationHandle<IList<GameObject>> _vfxSceneHandle;
        private AsyncOperationHandle<IList<AudioClip>> _sfxSceneHandle;
        
        private CancellationTokenSource _loadingToken;
        private CancellationTokenSource _loadStageToken;
        private AsyncOperation _unitySceneLoaderOp;
        private bool _isSceneLoading; //중복로딩 방지 플래그
        private Dictionary<ulong, StageResourceCache> _stageResourceCaches = new();
        private eSceneType _curType = default;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                Init();
                DontDestroyOnLoad(gameObject);
                return;
            }

            Destroy(gameObject);
        }
        void Init()
        {
            _loadingToken = new CancellationTokenSource();
            _loadStageToken = new CancellationTokenSource();

            _monsterMetaDataSO.Init();
            _sceneSFXMetaSO.Init();
            _sceneVFXMetaSO.Init();
        }
        private void OnDestroy()
        {
            _loadingToken?.Cancel();
            _loadingToken?.Dispose();
            _loadingToken = null;

            _loadStageToken?.Cancel();
            _loadStageToken?.Dispose();
            _loadStageToken = null;
        }

        #region 1. 공용 API
        // 동기 로드
        public void LoadScene(eSceneType type)
        {
            Time.timeScale = 1f;

            SceneLoadStarted?.Invoke(type);

            string sceneName = GetSceneName(type);
            ReleaseHandle();
            SceneManager.LoadScene(sceneName);

            // 동기 로드는 여기 시점에 이미 로드 완료로 간주
            SceneLoadProgress?.Invoke(type, 1f);
            SceneLoadFinished?.Invoke(type);
        }
        /// <summary>
        /// 비동기 씬 전환
        /// </summary>
        /// <param name="type">변경하려는 씬</param>
        public void LoadAsyncScene(eSceneType type)
        {
            if (_isSceneLoading)
            {
                Debug.LogWarning($"[LoadManager] Scene load already in progress: {type}");
                return;
            }

            _isSceneLoading = true;
            Time.timeScale = 1f;
            Debug.Log($"LoadAsyncScene : {type}");
            //새 씬 로딩 전에 이전 씬/스테이지에서 사용하던 리소스를 정리
            ReleaseHandle();
            LoadingScene(type).Forget();
        }
        public void ReloadCurrentScene()
        {
            Time.timeScale = 1f;

            var current = SceneManager.GetActiveScene().name;
            SceneManager.LoadScene(current);
        }
        /// <summary>
        /// 스테이지 전환 시 다음 스테이지 그룹에 필요한 리소스를 준비한 뒤 콜백을 호출한다.
        /// 씬은 바꾸지 않고 스테이지 그룹의 몬스터/SFX/VFX 리소스만 갱신한다.
        /// </summary>
        public async UniTask LoadStage(eStage curStage, eStage nextStage, Action<eStage> onStageLoaded_callback)
        {
            ulong resourceId = StageParser.GetResourceGroupId(nextStage);

            StageResourceCache cache = GetOrPreloadStageResources(resourceId);
            if (cache != null)
            {
                await cache.WaitUntilDone(_loadStageToken.Token);
            }
            
            onStageLoaded_callback.Invoke(nextStage);
        }
        #endregion

        #region 2. 최상위 로딩 진입점
        /// <summary>
        /// * 씬 늘어날 시 개선필요함
        /// 변경하려는 씬의 리소스를 로딩하는 메서드
        /// </summary>
        /// <param name="type"> 변경하려는 씬 </param>
        private async UniTaskVoid LoadingScene(eSceneType type)
                {
                    try
                    {
                        if (_loadingToken == null)
                            _loadingToken = new CancellationTokenSource();
        
                        SceneLoadStarted?.Invoke(type);
        
                        switch (type)
                        {
                            case eSceneType.title:
                                await LoadingTitleScene();
                                break;
                            case eSceneType.main:
                                await LoadingMainScene();
                                break;
                            default:
                                CustomLogger.LogError("아직 구현되지 않은 씬입니다.");
                                return;
                        }
        
                        _curType = type;
                        SceneLoadProgress?.Invoke(type, 1f);
                        SceneLoadFinished?.Invoke(type);
                    }
                    catch (OperationCanceledException)
                    {
                        CustomLogger.LogWarning($"[LoadManager] Scene loading canceled: {type}");
                    }
                    catch (Exception ex)
                    {
                        CustomLogger.LogError($"[LoadManager] Scene loading failed: {type}\n{ex}");
                    }
                    finally
                    {
                        _isSceneLoading = false;
                    }
                }
        /// <summary> 메인 씬 로딩</summary>
        private async UniTask LoadingMainScene()
                {
                    eSceneType type = eSceneType.main;
                    StageResourceCache cache = LoadStageResources(type);
                    LoadSceneResources(type);
        
                    await WaitForLoadingResources(cache, _loadingToken.Token);
                    await LoadUnitySceneAsync(type);
                }
        /// <summary> 타이틀 씬 로딩</summary>
        private async UniTask LoadingTitleScene()
        {
            eSceneType type = eSceneType.title;
            LoadSceneResources(type);

            await WaitForLoadingResources(null, _loadingToken.Token);
            await LoadUnitySceneAsync(type);
        }
        #endregion

        #region 3. 리소스 요청 절차
        /// <summary> 씬에서 사용할 리소스 프리로딩 </summary>
        private void LoadSceneResources(eSceneType type)
        {
            //씬에 필요한 VFX,SFX 로딩
            List<eVFXType> sceneVfxList;
            List<eSFXType> sceneSfxList;
            //불러와야 할 리소스가 있으면 프리로드
            bool IsVFXLoadNeed = _sceneVFXMetaSO.TryGetVFXTypeList(type, out sceneVfxList);
            bool IsSFXLoadNeed = _sceneSFXMetaSO.TryGetSFXTypeList(type, out sceneSfxList);
            if (IsVFXLoadNeed)
                _vfxSceneHandle = VFXManager.Instance.PreLoadVFX((ulong)type, sceneVfxList.ToArray());
            if (IsSFXLoadNeed)
                _sfxSceneHandle = SFXManager.Instance.PreLoadSFX((ulong)type, sceneSfxList.ToArray());
        }
        /// <summary> 스테이지에서 사용할 리소스를 프리로드 </summary>
        private StageResourceCache LoadStageResources(eSceneType type)
        {
            StageResourceCache cache = null;

            if (type != eSceneType.main)
            {
                return null;
            }

            // 사용자의 현재 스테이지 정보를 가져와서 로딩 준비.
            eStage currentStage = UserManager.Instance.GetUserCurrentStage();
            ulong resourceId = StageParser.GetResourceGroupId(currentStage);

            return GetOrPreloadStageResources(resourceId);
        }
        /// <summary>
        /// 특정 스테이지에 필요한 몬스터, 몬스터 VFX, 몬스터 SFX리소스 로딩.
        /// 캐싱되어있으면 그대로 가지고 오고, 없으면 프리로딩
        /// </summary>
        /// <param name="resourceId">스테이지 그룹 ID</param>
        /// <returns> 해당 스테이지 그룹에 사용되는 리소스 묶음</returns>
        private StageResourceCache GetOrPreloadStageResources(ulong resourceId)
        {
            //해당 스테이지의 리소스가 캐싱되어 있었다면 그대로 반환
            if (_stageResourceCaches.TryGetValue(resourceId, out StageResourceCache oldCache))
                return oldCache;

            StageResourceCache cache = new StageResourceCache
            {
                MonsterLoadTask = StageManager.Instance.PreLoadAssets((eStage)resourceId).Preserve()
            };
            //스테이지 그룹에 사용되는 몬스터 리스트 받아오기
            List<eMonsterType> monList = StageManager.Instance.GetStageMonsterTypes((eStage)resourceId);
            //몬스터 리스트가 비어있을 때 방지
            if (monList == null || monList.Count == 0)
            {
                Debug.LogWarning($"[LoadManager] No monster types for resourceId: {resourceId}");
                _stageResourceCaches.Add(resourceId, cache);
                return cache;
            }
            
            //몬스터들의 SFX 프리로딩
            if (TryGetSFXListIds(monList, out eSFXType[] sfxList))
            {
                Debug.Log("[MONSTER_SFX_Request]");
                cache.MonsterSfxHandle = SFXManager.Instance.PreLoadSFX(resourceId, sfxList);
            }
            //몬스터들의 VFX 프리로딩
            if (TryGetVFXListIds(monList, out eVFXType[] vfxList))
            {
                Debug.Log("[MONSTER_VFX_Request]");
                cache.MonsterVfxHandle = VFXManager.Instance.PreLoadVFX(resourceId, vfxList);
            }
            
            _stageResourceCaches.Add(resourceId, cache);
            return cache;
        }
        #endregion

        #region 4. 리소스 대기 절차
        /// <summary> 모든 리소스 로딩을 대기하는 메서드</summary>
        private async UniTask WaitForLoadingResources(StageResourceCache cache, CancellationToken token)
        {
            if (cache != null)
            {
                await cache.WaitUntilDone(token);
            }

            while (_sfxSceneHandle.IsLoading() || _vfxSceneHandle.IsLoading())
            {
                await UniTask.Yield(token);
            }
        }
        #endregion

        #region 5. 유니티 씬 로딩 절차
        /// <summary> 유니티 씬 이동준비 </summary>
        private async UniTask LoadUnitySceneAsync(eSceneType type)
        {
            string sceneName = GetSceneName(type);

            _unitySceneLoaderOp = SceneManager.LoadSceneAsync(sceneName);
            _unitySceneLoaderOp.allowSceneActivation = false;
            // 실제 씬 활성화 완료까지 대기
            while (!_unitySceneLoaderOp.isDone)
            {
                if (_unitySceneLoaderOp.progress >= 0.9f)
                {
                    _unitySceneLoaderOp.allowSceneActivation = true;
                }

                await UniTask.Yield(_loadingToken.Token);
            }
        }
        #endregion

        #region 6. 리소스 해제 절차
        /// <summary> 씬 변경등의 이유로 모든 리소스를 해제하는 메서드</summary>
        private void ReleaseHandle()
        {
            /*
             * LoadManager가 추적 중인 씬 리소스 캐시를 모두 해제
             * Log : 기존 SFXManager.Instance.unloadSFXBatch((ulong)_curType) 처럼 특정 씬의 리소스만 제거하는 형식이나
             * 이 과정에서 핸들은 제거되었지만 실제 캐시들은 제거되지 않는 형식이어서 일단 전부 제거로 수정함
             * 해당 ReleaseHandle()은 씬 변경등 전체 리소스를 제거할 소요가 있을 부분이기 때문으로 판단
             */
            //LoadManager가 추적 중인 스테이지 리소스 캐시를 모두 해제
            foreach ((ulong resourceId, StageResourceCache cache) in _stageResourceCaches)
            {
                cache.Release(resourceId);   
            }
            _stageResourceCaches.Clear();
            SFXManager.Instance.unloadSFXBatch();
            VFXManager.Instance.unloadVFXBatch();
            MonsterSpawner.Instance.Clear();
        }
        #endregion

        #region 7. 유틸리티
        /// <summary> 스테이지에 등장할 몬스터 타입들을 기준으로 미리 로드할 SFX 목록을 수집한다. </summary>
        /// <param name="monList">스테이지에 등장할 몬스터 타입 목록</param>
        /// <param name="Ids">미리 로드할 SFX 타입 배열. 대상이 없으면 빈 배열</param>
        /// <returns>미리 로드할 SFX가 하나라도 있으면 true</returns>
        private bool TryGetSFXListIds(List<eMonsterType> monList, out eSFXType[] Ids)
        {
            //중복된 SFX프리로드를 피하기 위해 해시셋 사용
            HashSet<eSFXType> result = new HashSet<eSFXType>();

            //몬스터 타입별로 등록된 SFX 메타를 모아 미리 로딩할 목록을 만든다.
            foreach (eMonsterType monster in monList)
            {
                bool hasMonsterSfx = _monsterMetaDataSO.TryGetSFXList(monster, out List<eSFXType> sfxList);
                if (!hasMonsterSfx || sfxList.Count == 0)
                    continue;
                foreach (eSFXType sfx in sfxList)
                {
                    result.Add(sfx);
                }
            }

            Ids = result.ToArray();
            return Ids.Length > 0;
        }
        /// <summary> 스테이지에 등장할 몬스터 타입들을 기준으로 미리 로드할 VFX 목록을 수집한다. </summary>
        /// <param name="monList">스테이지에 등장할 몬스터 타입 목록</param>
        /// <param name="Ids">미리 로드할 VFX 타입 배열. 대상이 없으면 빈 배열</param>
        /// <returns>미리 로드할 VFX가 하나라도 있으면 true</returns>
        private bool TryGetVFXListIds(List<eMonsterType> monList, out eVFXType[] Ids)
        {
            //중복된 VFX프리로드를 피하기 위해 해시셋 사용
            HashSet<eVFXType> result = new HashSet<eVFXType>();

            //몬스터 타입별로 등록된 VFX 메타를 모아 미리 로딩할 목록을 만든다.
            foreach (eMonsterType monster in monList)
            {
                bool hasMonsterVfx = _monsterMetaDataSO.TryGetVFXList(monster, out List<eVFXType> vfxList);
                if (!hasMonsterVfx || vfxList.Count == 0)
                    continue;
                foreach (eVFXType vfx in vfxList)
                {
                    result.Add(vfx);
                }
            }

            Ids = result.ToArray();
            return Ids.Length > 0;
        }

        public string GetSceneName(eSceneType type)
                {
                    switch (type)
                    {
                        case eSceneType.bootstrap: return bootstrapSceneName;
                        case eSceneType.title: return titleSceneName;
                        case eSceneType.main: return mainSceneName;
                        case eSceneType.dungeon: return dungeonSceneName;
                        default:
                            return type.ToString();
                    }
                }
        private class StageResourceCache
        {
            public UniTask MonsterLoadTask;
            public AsyncOperationHandle<IList<AudioClip>> MonsterSfxHandle;
            public AsyncOperationHandle<IList<GameObject>> MonsterVfxHandle;
            
            /// <summary>
            /// 몬스터 프리팹 Task와 몬스터 SFX/VFX handle이 모두 끝날 때까지 기다린다.
            /// </summary>
            public async UniTask WaitUntilDone(CancellationToken token)
            {
                await MonsterLoadTask;
                while (MonsterSfxHandle.IsLoading() || MonsterVfxHandle.IsLoading())
                {
                    Debug.Log("await");
                    await UniTask.Yield(token);
                }
            }

            public void Release(ulong resourceId)
            {
                if (MonsterSfxHandle.IsValid())
                    SFXManager.Instance.unloadSFXBatch(resourceId);

                if (MonsterVfxHandle.IsValid())
                    VFXManager.Instance.unloadVFXBatch(resourceId);

                MonsterSfxHandle = default;
                MonsterVfxHandle = default;
                MonsterLoadTask = default;
            }
            
        }
        #endregion
        
    }
}