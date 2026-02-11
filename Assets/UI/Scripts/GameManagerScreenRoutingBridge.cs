using UnityEngine;
using Scripts.Core;

namespace KingdomIdle.UI
{
    /// <summary>
    /// GameManager.SceneLoadFinished를 기준으로 UIScreen을 자동 교체하는 라우팅 브릿지.
    /// </summary>
    public sealed class GameManagerScreenRoutingBridge : MonoBehaviour
    {
        [Header("Behaviour")]
        [SerializeField] private bool clearStacksOnSceneChanged = true;
        [SerializeField] private bool ignoreBootstrap = true;

        private GameManager _gm;
        private UIManager _ui;
        private bool _subscribed;

        private void Start()
        {
            _gm = GameManager.Instance;
            _ui = UIManager.Instance;

            if (_gm == null || _ui == null)
            {
                Debug.LogError("[GameManagerScreenRoutingBridge] Missing GameManager/UIManager. Disable bridge.");
                enabled = false;
                return;
            }

            _gm.SceneLoadFinished += OnSceneLoadFinished;
            _subscribed = true;
        }

        private void OnDestroy()
        {
            if (!_subscribed) return;

            _gm.SceneLoadFinished -= OnSceneLoadFinished;
            _subscribed = false;
        }

        private void OnSceneLoadFinished(eSceneType type)
        {
            if (ignoreBootstrap && type == eSceneType.bootstrap)
                return;

            switch (type)
            {
                case eSceneType.title:
                    _ui.ReplaceScreen(UIScreenId.Title, payload: null, clearStacks: clearStacksOnSceneChanged);
                    break;

                case eSceneType.main:
                    _ui.ReplaceScreen(UIScreenId.Main, payload: null, clearStacks: clearStacksOnSceneChanged);
                    break;

                case eSceneType.dungeon:
                    _ui.ReplaceScreen(UIScreenId.Dungeon, payload: null, clearStacks: clearStacksOnSceneChanged);
                    break;

                default:
                    Debug.LogWarning("[GameManagerScreenRoutingBridge] Unhandled scene type: " + type);
                    break;
            }
        }
    }
}
