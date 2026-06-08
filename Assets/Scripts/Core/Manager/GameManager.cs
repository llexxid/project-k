using Cysharp.Threading.Tasks;
using Scripts.Core.Manager;
using Scripts.Core.SO;
using Scripts.Core.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;

namespace Scripts.Core
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance;

        [Header("Scene Name Mapping")] 
        [SerializeField] private string bootstrapSceneName = "bootstrap";
        [SerializeField] private string titleSceneName = "title";
        [SerializeField] private string mainSceneName = "main";
        [SerializeField] private string dungeonSceneName = "dungeon";

        [Header("Async Loading")] [SerializeField]
        private float minLoadingSeconds = 0f;

        //씬 , 스테이지와 관련된 몬스터, SFX,VFX정보들
        [SerializeField] MonsterMetaSO _monsterMetaDataSO;
        [SerializeField] SceneSFXMetaSO _SceneSFXMetaSO;
        [SerializeField] SceneVFXMetaSO _SceneVFXMetaSO;

        public event Action<eSceneType> SceneLoadStarted;
        public event Action<eSceneType> SceneLoadFinished;
        public event Action<eSceneType, float> SceneLoadProgress;

        private CancellationTokenSource _loadingToken;
        private CancellationTokenSource _LoadStageToken;

        private AsyncOperation _UnitySceneLoaderOp;

        private AsyncOperationHandle<IList<GameObject>> _VFXSceneHandle;
        private AsyncOperationHandle<IList<AudioClip>> _SFXSceneHandle;

        private bool _isSceneLoading; //중복로딩 방지 플래그
        private Dictionary<ulong, StageResourceCache> _stageResourceCaches = new();
        private eSceneType _curType = default;

        #region 플레이어 생존 관리
        // ── 플레이어 생존 관리 ──────────────────────────────────────
        // "사망 애니메이션 완료" 횟수를 셈. IsDead는 데미지 즉시 true가 되므로 사용 불가.
        private int _deathAnimationDoneCount = 0;

        /// <summary>
        /// 플레이어의 사망 애니메이션이 끝난 뒤 호출.
        /// 모든 플레이어의 애니메이션이 끝나야 게임을 정지한다.
        /// </summary>
        public void ReportPlayerDead()
        {
            _deathAnimationDoneCount++;

            //int totalPlayers = FindObjectsByType<Player>(FindObjectsSortMode.None).Length;
            if (_deathAnimationDoneCount >= 3)
                OnAllPlayersDead();
        }

        public void ReportPlayerRevived()
        {
            _deathAnimationDoneCount = Mathf.Max(0, _deathAnimationDoneCount - 1);
        }

        private void OnAllPlayersDead()
        {
            Debug.Log("[GameManager] 전원 사망 애니메이션 완료");
            _deathAnimationDoneCount = 0;

            if (WaveManager.Instance != null)
            {
                WaveManager.Instance.HandleAllPlayersDead();
                return;
            }

            Time.timeScale = 0f;
        }
        #endregion

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

        private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
        private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

        private void OnDestroy()
        {
            _LoadStageToken?.Cancel();
            _LoadStageToken?.Dispose();
            _LoadStageToken = null;
            _loadingToken?.Cancel();
            _loadingToken?.Dispose();
            _loadingToken = null;
        }

        private void Init()
        {
            _loadingToken = new CancellationTokenSource();
            _LoadStageToken = new CancellationTokenSource();

            _monsterMetaDataSO.Init();

            _SceneSFXMetaSO.Init();
            _SceneVFXMetaSO.Init();
        }

        private string GetSceneName(eSceneType type)
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

        public void ReloadCurrentScene()
        {
            Time.timeScale = 1f;

            var current = SceneManager.GetActiveScene().name;
            SceneManager.LoadScene(current);
        }
        /// <summary> 씬 변경이 완료되었을 시 진행되는 후처리과정 </summary>
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => HandleSceneReadyAsync(scene, mode).Forget();

        /// <summary> 1프레임 대기한 후 실제로 후처리 진행하는 메서드</summary>
        private async UniTaskVoid HandleSceneReadyAsync(Scene scene, LoadSceneMode mode)
        {
            //해당 메서드는 나중에 씬별 메서드를 따로 만들거나 핸들러 딕셔너리 등 정리할 필요가 있음
            try
            {
                // 1프레임 대기해서 Start()까지 다 끝난 뒤 실행을 보장
                await UniTask.NextFrame();
                if (scene.name == mainSceneName)
                {
                    Debug.Log("메인 씬 진입");

                    MonsterSpawner.Instance.OnEnterScene();
                    VFXManager.Instance.OnEnterScene();
                    SFXManager.Instance.PlayBGM(eSFXType.BGM);

                    if (Camera.main != null && Camera.main.GetComponent<CameraFade>() == null)
                        Camera.main.gameObject.AddComponent<CameraFade>();

                    UserManager.Instance.CreateCharacter();
                    eStage curUserStage = UserManager.Instance.GetUserCurrentStage();

                    // WaveManager가 있으면 웨이브 흐름을 위임
                    if (WaveManager.Instance != null)
                    {
                        WaveManager.Instance.BeginFromStage(curUserStage);
                    }
                    else
                    {
                        StageManager.Instance.StartStage(curUserStage);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameManager] Scene ready handling failed: {scene.name}\n{ex}");
            }
        }
        
        // 동기 로드
        public void LoadScene(eSceneType type)
        {
            Time.timeScale = 1f;

            SceneLoadStarted?.Invoke(type);

            string sceneName = GetSceneName(type);
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
                Debug.LogWarning($"[GameManager] Scene load already in progress: {type}");
                return;
            }

            _isSceneLoading = true;
            Time.timeScale = 1f;
            Debug.Log($"LoadAsyncScene : {type}");
            //새 씬 로딩 전에 이전 씬/스테이지에서 사용하던 리소스를 정리
            ReleaseHandle();
            LoadingScene(type).Forget();
        }

        /// <summary> 씬 변경등의 이유로 모든 리소스를 해제하는 메서드</summary>
        private void ReleaseHandle()
        {
            /*
             * GameManager가 추적 중인 씬 리소스 캐시를 모두 해제
             * Log : 기존 SFXManager.Instance.unloadSFXBatch((ulong)_curType) 처럼 특정 씬의 리소스만 제거하는 형식이나
             * 이 과정에서 핸들은 제거되었지만 실제 캐시들은 제거되지 않는 형식이어서 일단 전부 제거로 수정함
             * 해당 ReleaseHandle()은 씬 변경등 전체 리소스를 제거할 소요가 있을 부분이기 때문으로 판단
             */
            SFXManager.Instance.unloadSFXBatch();
            VFXManager.Instance.unloadVFXBatch();
            //GameManager가 추적 중인 스테이지 리소스 캐시를 모두 해제
            foreach ((ulong resourceId, StageResourceCache cache) in _stageResourceCaches)
            {
                cache.Release(resourceId);   
            }
            _stageResourceCaches.Clear();
            MonsterSpawner.Instance.Clear();
        }

        // * 씬 늘어날 시 개선필요함
        /// <summary>
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
                    default :
                        CustomLogger.LogError("아직 구현되지 않은 씬입니다.");
                        return;
                }

                _curType = type;
                SceneLoadProgress?.Invoke(type, 1f);
                SceneLoadFinished?.Invoke(type);
            }
            catch (OperationCanceledException)
            {
                CustomLogger.LogWarning($"[GameManager] Scene loading canceled: {type}");
            }
            catch (Exception ex)
            {
                CustomLogger.LogError($"[GameManager] Scene loading failed: {type}\n{ex}");
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
        /// <summary> 씬에서 사용할 리소스 프리로딩 </summary>
        private void LoadSceneResources(eSceneType type)
        {
            //씬에 필요한 VFX,SFX 로딩
            List<eVFXType> sceneVfxList;
            List<eSFXType> sceneSfxList;
            //불러와야 할 리소스가 있으면 프리로드
            bool IsVFXLoadNeed = _SceneVFXMetaSO.TryGetVFXTypeList(type, out sceneVfxList);
            bool IsSFXLoadNeed = _SceneSFXMetaSO.TryGetSFXTypeList(type, out sceneSfxList);
            if (IsVFXLoadNeed)
                _VFXSceneHandle = VFXManager.Instance.PreLoadVFX((ulong)type, sceneVfxList.ToArray());
            if (IsSFXLoadNeed)
                _SFXSceneHandle = SFXManager.Instance.PreLoadSFX((ulong)type, sceneSfxList.ToArray());
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
            ulong resourceId = GetResourceGroupId(currentStage);
            
            return GetOrPreloadStageResources(resourceId);
        }

        /// <summary> 모든 리소스 로딩을 대기하는 메서드</summary>
        private async UniTask WaitForLoadingResources(StageResourceCache cache, CancellationToken token)
        {
            if (cache != null)
            {
                await cache.WaitUntilDone(token);
            }

            while (_SFXSceneHandle.IsLoading() || _VFXSceneHandle.IsLoading())
            {
                await UniTask.Yield(token);
            }
        }
        /// <summary> 유니티 씬 이동준비 </summary>
        private async UniTask LoadUnitySceneAsync(eSceneType type)
        {
            string sceneName = GetSceneName(type);

            _UnitySceneLoaderOp = SceneManager.LoadSceneAsync(sceneName);
            _UnitySceneLoaderOp.allowSceneActivation = false;
            // 실제 씬 활성화 완료까지 대기
            while (!_UnitySceneLoaderOp.isDone)
            {
                if (_UnitySceneLoaderOp.progress >= 0.9f)
                {
                    _UnitySceneLoaderOp.allowSceneActivation = true;
                }

                await UniTask.Yield(_loadingToken.Token);
            }
        }
        /// <summary>
        /// 스테이지 전환 시 다음 스테이지 그룹에 필요한 리소스를 준비한 뒤 콜백을 호출한다.
        /// 씬은 바꾸지 않고 스테이지 그룹의 몬스터/SFX/VFX 리소스만 갱신한다.
        /// </summary>
        public async UniTaskVoid LoadStage(eStage curStage, eStage nextStage, Action<eStage> onStageLoaded_callback)
        {
            float startRealtime = Time.realtimeSinceStartup;
            ulong resourceId = GetResourceGroupId(nextStage);

            StageResourceCache cache = GetOrPreloadStageResources(resourceId);
            if (cache != null)
            {
                await cache.WaitUntilDone(_LoadStageToken.Token);
            }
            
            onStageLoaded_callback.Invoke(nextStage);
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
                MonsterLoadTask = StageManager.Instance.PreLoadAssets((eStage)resourceId)
            };
            //스테이지 그룹에 사용되는 몬스터 리스트 받아오기
            List<eMonsterType> monList = StageManager.Instance.GetStageMonsterTypes((eStage)resourceId);
            //몬스터 리스트가 비어있을 때 방지
            if (monList == null || monList.Count == 0)
            {
                Debug.LogWarning($"[GameManager] No monster types for resourceId: {resourceId}");
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
        /// <summary>
        /// 특정 스테이지 그룹의 몬스터, 몬스터 SFX, 몬스터 VFX 로딩 상태를 함께 추적한다.
        /// <br/>* 몬스터 프리팹 로딩은 MonsterSpawner가 UniTask로 진행하며,
        /// GameManager는 반환된 Task를 기다려 스테이지 리소스 준비 완료 시점만 맞춘다.
        /// </summary>
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
                MonsterSpawner.Instance.Clear();
                if (MonsterSfxHandle.IsValid())
                    SFXManager.Instance.unloadSFXBatch(resourceId);

                if (MonsterVfxHandle.IsValid())
                    VFXManager.Instance.unloadVFXBatch(resourceId);

                MonsterSfxHandle = default;
                MonsterVfxHandle = default;
                MonsterLoadTask = default;
            }
            
        }

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


        //현재의 스테이지를 기반으로 ResourceGroupID를 얻어냄.
        private ulong GetResourceGroupId(eStage curstage)
        {
            return ((ulong)curstage & 0xFFFFFFFFFFFF0000);
        }
    }
}