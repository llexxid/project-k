using UnityEngine;
using Scripts.Core;

namespace KingdomIdle.UIToolkit
{
    public sealed class UITKLoadingOverlayBridge : MonoBehaviour
    {
        [SerializeField] private string loadingText = "Loading...";

        private GameManager _gm;

        private void Start()
        {
            _gm = GameManager.Instance;
            if (_gm == null || UITKUIManager.Instance == null)
            {
                Debug.LogError("[UITKLoadingOverlayBridge] Missing GameManager/UITKUIManager.");
                enabled = false;
                return;
            }

            _gm.SceneLoadStarted += HandleStarted;
            _gm.SceneLoadProgress += HandleProgress;
            _gm.SceneLoadFinished += HandleFinished;
        }

        private void OnDestroy()
        {
            if (_gm == null) return;

            _gm.SceneLoadStarted -= HandleStarted;
            _gm.SceneLoadProgress -= HandleProgress;
            _gm.SceneLoadFinished -= HandleFinished;
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
