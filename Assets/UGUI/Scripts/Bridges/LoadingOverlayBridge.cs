using UnityEngine;
using Scripts.Core;

namespace KingdomIdle.UGUI
{
    /// <summary>LoadManager 로딩 이벤트 → UIManager 로딩 오버레이 (UITKLoadingOverlayBridge 이식).</summary>
    public sealed class LoadingOverlayBridge : MonoBehaviour
    {
        [SerializeField] private string loadingText = "Loading...";

        private LoadManager _lm;

        private void Start()
        {
            _lm = LoadManager.Instance;
            if (_lm == null || UIManager.Instance == null)
            {
                Debug.LogError("[LoadingOverlayBridge] Missing LoadManager/UIManager.");
                enabled = false;
                return;
            }

            _lm.SceneLoadStarted += HandleStarted;
            _lm.SceneLoadProgress += HandleProgress;
            _lm.SceneLoadFinished += HandleFinished;
        }

        private void OnDestroy()
        {
            if (_lm == null) return;

            _lm.SceneLoadStarted -= HandleStarted;
            _lm.SceneLoadProgress -= HandleProgress;
            _lm.SceneLoadFinished -= HandleFinished;
        }

        private void HandleStarted(eSceneType type)
        {
            UIManager.Instance.SetLoading(true, loadingText);
        }

        private void HandleProgress(eSceneType type, float progress)
        {
            UIManager.Instance.SetLoadingProgress(progress);
        }

        private void HandleFinished(eSceneType type)
        {
            UIManager.Instance.SetLoading(false);
        }
    }
}
