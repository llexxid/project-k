using UnityEngine;
using Scripts.Core;

namespace KingdomIdle.UI
{
    /// <summary>
    /// GameManager 씬 로딩 이벤트를 받아 Loading Overlay를 켜고/끄는 브릿지.
    /// </summary>
    public sealed class GameManagerLoadingOverlayBridge : MonoBehaviour
    {
        [SerializeField] private string loadingText = "Loading...";

        private GameManager _gm;
        private UIManager _ui;
        private bool _subscribed;

        private void Start()
        {
            _gm = GameManager.Instance;
            _ui = UIManager.Instance;

            if (_gm == null || _ui == null)
            {
                Debug.LogError("[GameManagerLoadingOverlayBridge] Missing GameManager/UIManager. Disable bridge.");
                enabled = false;
                return;
            }

            _gm.SceneLoadStarted += HandleStarted;
            _gm.SceneLoadFinished += HandleFinished;
            _subscribed = true;
        }

        private void OnDestroy()
        {
            if (!_subscribed) return;

            _gm.SceneLoadStarted -= HandleStarted;
            _gm.SceneLoadFinished -= HandleFinished;
            _subscribed = false;
        }

        private void HandleStarted(eSceneType type)
        {
            _ui.SetLoading(true, loadingText);
        }

        private void HandleFinished(eSceneType type)
        {
            _ui.SetLoading(false);
        }
    }
}
