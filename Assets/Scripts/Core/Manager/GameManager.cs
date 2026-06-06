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

        private CancellationTokenSource _token;
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

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            if (_token != null)
            {
                _token.Cancel();
                _token.Dispose();
                _token = null;
            }
        }

        private void Init()
        {
            _token = new CancellationTokenSource();
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
            //기존의 핸들이 있다면, 핸들들을 Release시켜줘야함.
            ReleaseHandle();
            LoadingScene(type).Forget();
        }

        private void ReleaseHandle()
        {
            SFXManager.Instance.unloadSFXBatch((ulong)_curType);
            VFXManager.Instance.unloadVFXBatch((ulong)_curType);
            //뭔가 값이 있다면 해제
            foreach (var cache in _stageResourceCaches.Values)
            {
                cache.Release();   
            }
            _stageResourceCaches.Clear();
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
                if (_token == null)
                    _token = new CancellationTokenSource();

                SceneLoadStarted?.Invoke(type);

                string sceneName = GetSceneName(type);
                StageResourceCache cache = null;
                // 사용자의 현재 스테이지 정보를 가져와서 로딩 준비.
                if (type == eSceneType.main)
                {
                    eStage currentStage = UserManager.Instance.GetUserCurrentStage();
                    ulong resourceId = GetResourceGroupId(currentStage);
                    cache = GetOrPreloadStageResources(resourceId);
                }

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
                
                bool vfxDone = !_VFXSceneHandle.IsValid() || _VFXSceneHandle.IsDone;
                bool sfxDone = !_SFXSceneHandle.IsValid() || _SFXSceneHandle.IsDone;

                //리소스가 전부 로딩될때까지 대기
                // * 현재는 main씬밖에 없으니까 상관없지만, 나중에 씬이 늘어나면 cache를 공통으로 묶어야 할 필요 있음
                while (!((cache == null || cache.IsDone) && (vfxDone && sfxDone)))
                {
                    vfxDone = !_VFXSceneHandle.IsValid() || _VFXSceneHandle.IsDone;
                    sfxDone = !_SFXSceneHandle.IsValid() || _SFXSceneHandle.IsDone;

                    await UniTask.Yield(_token.Token);
                }

                _UnitySceneLoaderOp = SceneManager.LoadSceneAsync(sceneName);
                _UnitySceneLoaderOp.allowSceneActivation = false;
                // 실제 씬 활성화 완료까지 대기
                while (!_UnitySceneLoaderOp.isDone)
                {
                    if (_UnitySceneLoaderOp.progress >= 0.9f)
                    {
                        _UnitySceneLoaderOp.allowSceneActivation = true;
                    }

                    await UniTask.Yield(_token.Token);
                }
                _curType = type; 
                SceneLoadProgress?.Invoke(type, 1f);
                SceneLoadFinished?.Invoke(type);
            }
            catch (OperationCanceledException)
            {
                Debug.LogWarning($"[GameManager] Scene loading canceled: {type}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameManager] Scene loading failed: {type}\n{ex}");
            }
            finally
            {
                _isSceneLoading = false;
            }
        }



        /// <summary> 스테이지 단위 변경 시 필요한 리소스 로딩</summary>
        /// <param name="curStage">현재 스테이지</param>
        /// <param name="nextStage">이동할 스테이지</param>
        /// <param name="onStageLoaded_callback">로딩 완료시 콜백할 액션</param>
        public async UniTaskVoid LoadStage(eStage curStage, eStage nextStage, Action<eStage> onStageLoaded_callback)
        {
            float startRealtime = Time.realtimeSinceStartup;
            ulong resourceId = GetResourceGroupId(nextStage);

            StageResourceCache cache = GetOrPreloadStageResources(resourceId);

            while (!cache.IsDone)
            {
                await UniTask.Yield(_LoadStageToken.Token);
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

            StageResourceCache cache = new StageResourceCache();
            cache.MonsterHandle = StageManager.Instance.PreLoadAssets((eStage)resourceId);
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
        /// 특정 스테이지 그룹에서 사용되는 리소스 그룹
        /// 1. MonsterHandle : 몬스터 프리팹
        /// 2. MonsterSfxHandle : 몬스터 SFX(사운드) 리소스
        /// 3. MonsterVfxHandle : 몬스터 VFX(피격/공격이펙트 등..) 리소스
        /// </summary>
        private class StageResourceCache
        {
            public AsyncOperationHandle<IList<GameObject>> MonsterHandle;
            public AsyncOperationHandle<IList<AudioClip>> MonsterSfxHandle;
            public AsyncOperationHandle<IList<GameObject>> MonsterVfxHandle;
            
            //스테이지의 모든 리소스가 로딩되었는지 확인
            public bool IsDone =>
                (!MonsterHandle.IsValid() || MonsterHandle.IsDone) &&
                (!MonsterSfxHandle.IsValid() || MonsterSfxHandle.IsDone) &&
                (!MonsterVfxHandle.IsValid() || MonsterVfxHandle.IsDone);

            public void Release()
            {
                if (MonsterHandle.IsValid())
                    Addressables.Release(MonsterHandle);

                if (MonsterSfxHandle.IsValid())
                    Addressables.Release(MonsterSfxHandle);

                if (MonsterVfxHandle.IsValid())
                    Addressables.Release(MonsterVfxHandle);

                MonsterHandle = default;
                MonsterSfxHandle = default;
                MonsterVfxHandle = default;
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