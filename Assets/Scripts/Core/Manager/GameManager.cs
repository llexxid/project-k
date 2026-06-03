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
    using static Scripts.Monster.Monster;
    using Monster = Scripts.Monster.Monster;

    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance;

        [Header("Scene Name Mapping")] [SerializeField]
        private string bootstrapSceneName = "bootstrap";

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
        // ────────────────────────────────────────────────────────────


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

        //씬 전환하는 기능
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
            CheckHandle();
            LoadingScene(type).Forget();
        }

        private void CheckHandle()
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

        private async UniTaskVoid LoadingScene(eSceneType type)
        {
            try
            {
                if (_token == null)
                    _token = new CancellationTokenSource();

                SceneLoadStarted?.Invoke(type);

                string sceneName = GetSceneName(type);
                float startRealtime = Time.realtimeSinceStartup;
                StageResourceCache cache = null;
                // User의 현재 스테이지 정보를 가져와서 Load준비해야함.
                if (type == eSceneType.main)
                {
                    eStage currentStage = UserManager.Instance.GetUserCurrentStage();
                    ulong resourceId = GetResourceGroupId(currentStage);
                    cache = PreloadCache(resourceId);
                }

                //각 씬에 필요한 VFX,SFX 로딩
                List<eVFXType> vfxList;
                List<eSFXType> sfxList;
                bool IsVFXLoadNeed = _SceneVFXMetaSO.TryGetVFXTypeList(type, out vfxList);
                bool IsSFXLoadNeed = _SceneSFXMetaSO.TryGetSFXTypeList(type, out sfxList);
                if (IsVFXLoadNeed)
                    _VFXSceneHandle = VFXManager.Instance.PreLoadVFX((ulong)type, vfxList.ToArray());
                if (IsSFXLoadNeed)
                    _SFXSceneHandle = SFXManager.Instance.PreLoadSFX((ulong)type, sfxList.ToArray());
                
                bool vfxDone = !_VFXSceneHandle.IsValid() || _VFXSceneHandle.IsDone;
                bool sfxDone = !_SFXSceneHandle.IsValid() || _SFXSceneHandle.IsDone;

                //ReSourceLoading
                while (!((cache == null || cache.IsDone) && (vfxDone && sfxDone)))
                {
                    vfxDone = !_VFXSceneHandle.IsValid() || _VFXSceneHandle.IsDone;
                    sfxDone = !_SFXSceneHandle.IsValid() || _SFXSceneHandle.IsDone;
                    //로딩창 Scroll조절
                    //timer += Time.unscaledDeltaTime;
                    //scrollbar.fillAmount = Mathf.Lerp(0.9f, 1f, timer);

                    /*float normalized = Mathf.Clamp01(_UnitySceneLoaderOp.progress / 0.9f);

                    if (minLoadingSeconds > 0f)
                    {
                        float t = Mathf.Clamp01((Time.realtimeSinceStartup - startRealtime) / minLoadingSeconds);
                        normalized = Mathf.Min(normalized, t);
                    }

                    SceneLoadProgress?.Invoke(type, normalized);*/

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
                _curType = type; 
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            //MainScene전환시
            if (scene.name == "main")
            {
                Debug.Log("메인 씬 진입");

                MonsterSpawner.Instance.OnEnterScene();
                VFXManager.Instance.OnEnterScene();
                SFXManager.Instance.PlayBGM(eSFXType.BGM);

                if (Camera.main != null && Camera.main.GetComponent<Scripts.Core.Utils.CameraFade>() == null)
                    Camera.main.gameObject.AddComponent<Scripts.Core.Utils.CameraFade>();

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

                //Stage가 시작하는 지점

                //Stage

                //테스트용 - 상, 좌, 우 각 3마리씩 스폰
                //MonsterInfo monInfo = MonsterSpawner.Instance.GetMonsterInfo(eMonsterType.MON_BANDIT_LEADER);
                /*Vector3[][] spawnPositions = new Vector3[][]
                {
                    new Vector3[] { new Vector3(-2, 8, 0), new Vector3(0, 8, 0), new Vector3(2, 8, 0) },   // 상
                    new Vector3[] { new Vector3(-8, -2, 0), new Vector3(-8, 0, 0), new Vector3(-8, 2, 0) }, // 좌
                    new Vector3[] { new Vector3(8, -2, 0), new Vector3(8, 0, 0), new Vector3(8, 2, 0) },    // 우
                };
*/
                //Vector3 pos = new Vector3(-5, 0, 0);

                //MonsterSpawner.Instance.SpawnMonster(eMonsterType.MON_BANDIT_LEADER, pos, Quaternion.identity, out Monster mon);
                //MonsterStat stat = new MonsterStat(monInfo._baseHp, 0, (ulong)monInfo._baseAtk, monInfo._baseMoveSpeed, monInfo._baseAtkSpeed);
                //mon.Init(eMonsterType.MON_BANDIT_ARCHER, stat, monInfo._dropTableNumber);
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

            StageResourceCache cache = PreloadCache(resourceId);

            while (!cache.IsDone)
            {
                await UniTask.Yield(_LoadStageToken.Token);
            }
            
            onStageLoaded_callback.Invoke(nextStage);
            
            /*
            _StageLoaderHandle = StageManager.Instance.PreLoadAssets(stage);
            LoadResourceInMonster(stage);
            */

            /*
            //1,2스테이지 반복하는 형태이므로, 2스테이지이상이라면 로딩할필요 x.
            if (StageParser.GetStageNumber(nextStage) >= 3)
            {
                onStageLoaded_callback.Invoke(nextStage);
                return;
            }
             ulong resource_prevId = GetResourceGroupId(curStage);
            ulong resource_nxtId = GetResourceGroupId(nextStage);


            _StageLoaderHandle = StageManager.Instance.PreLoadAssets((eStage)resource_nxtId);
            LoadResourceInMonster(resource_nxtId);

            while (true)
            {
                bool stageDone = !_StageLoaderHandle.IsValid() || _StageLoaderHandle.IsDone;
                bool vfxDone = !_VFXMonsterHandle.IsValid() || _VFXMonsterHandle.IsDone;
                bool sfxDone = !_SFXMonsterHandle.IsValid() || _SFXMonsterHandle.IsDone;

                //화면 검게 Fade Out - FadeIn 연출 작성하기
                //timer += Time.unscaledDeltaTime;
                //scrollbar.fillAmount = Mathf.Lerp(0.9f, 1f, timer);

                // 최소 로딩 시간 옵션
                if (minLoadingSeconds > 0f)
                {
                    float t = Mathf.Clamp01((Time.realtimeSinceStartup - startRealtime) / minLoadingSeconds);
                }


                if (stageDone && vfxDone && sfxDone)
                {
                    if (minLoadingSeconds <= 0f || (Time.realtimeSinceStartup - startRealtime) >= minLoadingSeconds)
                    {
                        onStageLoaded_callback.Invoke(nextStage);
                        break;
                    }
                }
                await UniTask.Yield(_LoadStageToken.Token);
            }*/
        }

        private StageResourceCache PreloadCache(ulong resourceId)
        {
            if (_stageResourceCaches.TryGetValue(resourceId, out StageResourceCache oldCache))
                return oldCache;

            StageResourceCache cache = new StageResourceCache();
            cache.MonsterHandle = StageManager.Instance.PreLoadAssets((eStage)resourceId);
            List<eMonsterType> monList = StageManager.Instance.GetStageMonsterTypes((eStage)resourceId);

            if (TryGetSFXListIds(monList, out eSFXType[] sfxList))
            {
                Debug.Log("[MONSTER_SFX_Request]");
                cache.MonsterSfxHandle = SFXManager.Instance.PreLoadSFX(resourceId, sfxList);
            }

            if (TryGetVFXListIds(monList, out eVFXType[] vfxList))
            {
                Debug.Log("[MONSTER_VFX_Request]");
                cache.MonsterVfxHandle = VFXManager.Instance.PreLoadVFX(resourceId, vfxList);
            }
            
            _stageResourceCaches.Add(resourceId, cache);
            return cache;
        }
        private class StageResourceCache
        {
            public AsyncOperationHandle<IList<GameObject>> MonsterHandle;
            public AsyncOperationHandle<IList<AudioClip>> MonsterSfxHandle;
            public AsyncOperationHandle<IList<GameObject>> MonsterVfxHandle;

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
        
        private float GetOperationHandlePercent<T>(AsyncOperationHandle<T> handle)
        {
            return handle.IsValid() ? handle.PercentComplete : 1f;
        }
        private void ClearCurrentStageResource(ulong resourceId)
        {
            StageManager.Instance.Clear();
            VFXManager.Instance.unloadVFXBatch((ulong)resourceId);
            SFXManager.Instance.unloadSFXBatch((ulong)resourceId);
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