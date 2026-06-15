using UnityEngine;
using Scripts.Core;

namespace KingdomIdle.UIToolkit
{
    public sealed class UITKLoadingOverlayBridge : MonoBehaviour
    {
        [SerializeField] private string loadingText = "Loading...";

        private LoadManager _lm;

        private void Start()
        {
            _lm = LoadManager.Instance;
            if (_lm == null || UITKUIManager.Instance == null)
            {
                Debug.LogError("[UITKLoadingOverlayBridge] Missing GameManager/UITKUIManager.");
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
            UITKUIManager.Instance.SetLoading(true, loadingText);
        }

        private void HandleProgress(eSceneType type, float progress)
        {
            UITKUIManager.Instance.SetLoadingProgress(progress);
        }

        private void HandleFinished(eSceneType type)
        {
            UITKUIManager.Instance.SetLoading(false);
        }
    }
}
