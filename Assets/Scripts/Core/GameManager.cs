using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Scripts.Core
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        // (기존 필드 유지) - 현재 로직에서는 사용 안 함. 추후 로딩 패널 프리팹을 직접 쓰고 싶으면 활용.
        public GameObject _loadPannelPrefab;

        [Header("Async Loading")]
        [SerializeField] private float minLoadingSeconds = 0f; // 로딩 연출 최소 유지 시간(원하면 0.5~1.0)

        // 로딩 오버레이/진행률 브릿지에서 사용할 이벤트
        public event Action<eSceneType> SceneLoadStarted;
        public event Action<eSceneType, float> SceneLoadProgress; // 0~1
        public event Action<eSceneType> SceneLoadFinished;

        private CancellationTokenSource _loadCts;
        private AsyncOperation _asyncOp;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            CancelCurrentLoad();
        }

        private void CancelCurrentLoad()
        {
            if (_loadCts == null) return;
            _loadCts.Cancel();
            _loadCts.Dispose();
            _loadCts = null;
        }

        // 동기 로드
        public void LoadScene(eSceneType type)
        {
            Time.timeScale = 1f;
            CancelCurrentLoad();

            SceneLoadStarted?.Invoke(type);
            SceneLoadProgress?.Invoke(type, 0f);

            // enum 멤버 이름 = 씬 이름(소문자)로 통일했기 때문에 ToString() 그대로 사용
            SceneManager.LoadScene(type.ToString());

            SceneLoadProgress?.Invoke(type, 1f);
            SceneLoadFinished?.Invoke(type);
        }

        public void ReloadCurrentScene()
        {
            Time.timeScale = 1f;
            CancelCurrentLoad();

            var current = SceneManager.GetActiveScene().name;
            SceneManager.LoadScene(current);
        }

        // 비동기 로드
        public void LoadAsyncScene(eSceneType type)
        {
            Time.timeScale = 1f;

            CancelCurrentLoad();
            _loadCts = new CancellationTokenSource();

            LoadingSceneAsync(type, _loadCts.Token).Forget();
        }

        private async UniTaskVoid LoadingSceneAsync(eSceneType type, CancellationToken token)
        {
            SceneLoadStarted?.Invoke(type);
            SceneLoadProgress?.Invoke(type, 0f);

            string sceneName = type.ToString();
            float startRealtime = Time.realtimeSinceStartup;

            _asyncOp = SceneManager.LoadSceneAsync(sceneName);
            if (_asyncOp == null)
            {
                Debug.LogError($"[GameManager] LoadSceneAsync returned null. sceneName={sceneName}");
                SceneLoadFinished?.Invoke(type);
                return;
            }

            _asyncOp.allowSceneActivation = false;

            // 0.0 ~ 0.9 구간 (유니티 로딩)
            while (_asyncOp.progress < 0.9f)
            {
                token.ThrowIfCancellationRequested();

                float p = Mathf.Clamp01(_asyncOp.progress / 0.9f) * 0.9f; // 0~0.9
                SceneLoadProgress?.Invoke(type, p);

                await UniTask.Yield(token);
            }

            // 최소 로딩 시간 확보(연출용)
            float minSec = Mathf.Max(0f, minLoadingSeconds);
            while (Time.realtimeSinceStartup - startRealtime < minSec)
            {
                token.ThrowIfCancellationRequested();

                SceneLoadProgress?.Invoke(type, 0.9f);
                await UniTask.Yield(token);
            }

            // 씬 활성화
            _asyncOp.allowSceneActivation = true;

            // 활성화 완료 대기
            while (!_asyncOp.isDone)
            {
                token.ThrowIfCancellationRequested();
                await UniTask.Yield(token);
            }

            SceneLoadProgress?.Invoke(type, 1f);
            SceneLoadFinished?.Invoke(type);
        }
    }
}
