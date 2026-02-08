using UnityEngine;
using Scripts.Core;

namespace KingdomIdle.UI
{
    public class GameManagerLoadingOverlayBridge : MonoBehaviour
    {
        [SerializeField] private string loadingText = "Loading...";

        private void OnEnable()
        {
            if (GameManager.Instance == null)
            {
                Debug.LogWarning("[GameManagerLoadingOverlayBridge] GameManager.Instance is null.");
                return;
            }

            GameManager.Instance.SceneLoadStarted += HandleStarted;
            GameManager.Instance.SceneLoadFinished += HandleFinished;
        }

        private void OnDisable()
        {
            if (GameManager.Instance == null) return;

            GameManager.Instance.SceneLoadStarted -= HandleStarted;
            GameManager.Instance.SceneLoadFinished -= HandleFinished;
        }

        private void HandleStarted(eSceneType type)
        {
            if (UIManager.Instance == null)
            {
                Debug.LogWarning("[GameManagerLoadingOverlayBridge] UIManager.Instance is null.");
                return;
            }

            UIManager.Instance.SetLoading(true, loadingText);
        }

        private void HandleFinished(eSceneType type)
        {
            if (UIManager.Instance == null) return;
            UIManager.Instance.SetLoading(false);
        }
    }
}
