using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

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

        public event Action<eSceneType> SceneLoadStarted;
        public event Action<eSceneType> SceneLoadFinished;
        public event Action<eSceneType, float> SceneLoadProgress;

        private CancellationTokenSource _token;
        private AsyncOperation _asyncOp;

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

        public void ReloadCurrentScene()
        {
            Time.timeScale = 1f;

            var current = SceneManager.GetActiveScene().name;
            SceneManager.LoadScene(current);
        }

        // 비동기 로드
        public void LoadAsyncScene(eSceneType type)
        {
            Time.timeScale = 1f;
            LoadingScene(type).Forget();
        }

        private async UniTaskVoid LoadingScene(eSceneType type)
        {
            if (_token == null) _token = new CancellationTokenSource();

            SceneLoadStarted?.Invoke(type);
            SceneLoadProgress?.Invoke(type, 0f);

            string sceneName = GetSceneName(type);

            float startRealtime = Time.realtimeSinceStartup;

            _asyncOp = SceneManager.LoadSceneAsync(sceneName);
            _asyncOp.allowSceneActivation = false;

            // 0.0 ~ 0.9 구간 (유니티 로딩)
            while (_asyncOp != null && _asyncOp.progress < 0.9f)
            {
                float p = Mathf.Clamp01(_asyncOp.progress / 0.9f) * 0.9f; // 0~0.9
                SceneLoadProgress?.Invoke(type, p);
                await UniTask.Yield(_token.Token);
            }

            // 최소 로딩 시간 확보(연출용)
            while (Time.realtimeSinceStartup - startRealtime < Mathf.Max(0f, minLoadingSeconds))
            {
                SceneLoadProgress?.Invoke(type, 0.9f);
                await UniTask.Yield(_token.Token);
            }

            // 씬 활성화
            if (_asyncOp != null)
                _asyncOp.allowSceneActivation = true;

            // 활성화 완료 대기
            while (_asyncOp != null && !_asyncOp.isDone)
            {
                await UniTask.Yield(_token.Token);
            }

            SceneLoadProgress?.Invoke(type, 1f);
            SceneLoadFinished?.Invoke(type);
        }
    }
}
