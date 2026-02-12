using Cysharp.Threading.Tasks;
using ExcelDataReader;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Threading;
using System.Timers;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;
using Scripts.Core.SO;

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

        [Header("Async Loading")]
        [SerializeField] private float minLoadingSeconds = 0f;

        [SerializeField]
        MonsterMetaSO _monsterMetaDataSO;
        [SerializeField]
        SoundMetaSO _soundMetaSO;

        public event Action<eSceneType> SceneLoadStarted;
        public event Action<eSceneType> SceneLoadFinished;
        public event Action<eSceneType, float> SceneLoadProgress;

        private CancellationTokenSource _token;

        private AsyncOperation _UnitySceneLoaderOp;
        private AsyncOperationHandle<IList<GameObject>> _VFXLoaderHandle;
        private AsyncOperationHandle<IList<Monster>> _StageLoaderHandle;
        private AsyncOperationHandle<IList<AudioClip>> _SFXLoaderHandle;

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
            _monsterMetaDataSO.Init();
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
            Time.timeScale = 1f;
            LoadingScene(type).Forget();
        }

        private async UniTaskVoid LoadingScene(eSceneType type)
        {
            if (_token == null) _token = new CancellationTokenSource();

            SceneLoadStarted?.Invoke(type);

            string sceneName = GetSceneName(type);

            float startRealtime = Time.realtimeSinceStartup;

            _UnitySceneLoaderOp = SceneManager.LoadSceneAsync(sceneName);
            // User의 현재 스테이지 정보를 가져와서 Load준비해야함.
            //_StageLoaderHandle = StageManager.Instance.LoadAssets(type);
            // Stage에 필요한 VFX를 StageManager에서 몬스터들이 갖고있는 VFX모아서 넘겨주기.
            //_VFXLoaderHandle = VFXManager.Instance.PreLoadVFX(type, eVFXTypeId[]);

            _UnitySceneLoaderOp.allowSceneActivation = false;

            while (true)
            {
                if (_StageLoaderHandle.IsDone &&
                    _VFXLoaderHandle.IsDone &&
                    _SFXLoaderHandle.IsDone &&
                    (_UnitySceneLoaderOp.progress < 0.9f)
                    )
                {
                    break;
                }

                //로딩창 Scroll조절
                //timer += Time.unscaledDeltaTime;
                //scrollbar.fillAmount = Mathf.Lerp(0.9f, 1f, timer);

                //스크롤바가 다 채워졌다면, SceneActive하기.
                await UniTask.Yield(_token.Token);
            }

            //Loading끝
        }
    }
}
