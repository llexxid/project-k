using UnityEngine;
using Scripts.Core;

namespace KingdomIdle.UI
{
    /// <summary>
    /// 씬 로드 완료 시점에 현재 씬 타입에 맞는 UIScreen을 자동으로 띄운다.
    /// title/main/dungeon 씬이 비어있어도(카메라만 있어도) UI가 뜨게 만드는 핵심 브릿지.
    /// </summary>
    public sealed class GameManagerScreenRoutingBridge : MonoBehaviour
    {
        [Header("Behaviour")]
        [SerializeField] private bool clearStacksOnSceneChanged = true;
        [SerializeField] private bool ignoreBootstrap = true;

        private bool _hooked;

        private void OnEnable()
        {
            TryHook();
        }

        private void Update()
        {
            if (!_hooked) TryHook();
        }

        private void OnDisable()
        {
            Unhook();
        }

        private void TryHook()
        {
            if (_hooked) return;
            if (GameManager.Instance == null) return;

            GameManager.Instance.SceneLoadFinished += OnSceneLoadFinished;
            _hooked = true;
        }

        private void Unhook()
        {
            if (!_hooked) return;
            if (GameManager.Instance == null) return;

            GameManager.Instance.SceneLoadFinished -= OnSceneLoadFinished;
            _hooked = false;
        }

        private void OnSceneLoadFinished(eSceneType type)
        {
            if (ignoreBootstrap && type == eSceneType.bootstrap)
                return;

            if (UIManager.Instance == null)
            {
                Debug.LogWarning("[GameManagerScreenRoutingBridge] UIManager.Instance is null.");
                return;
            }

            switch (type)
            {
                case eSceneType.title:
                    UIManager.Instance.ReplaceScreen(UIScreenId.Title, payload: null, clearStacks: clearStacksOnSceneChanged);
                    break;

                case eSceneType.main:
                    UIManager.Instance.ReplaceScreen(UIScreenId.Main, payload: null, clearStacks: clearStacksOnSceneChanged);
                    break;

                case eSceneType.dungeon:
                    UIManager.Instance.ReplaceScreen(UIScreenId.Dungeon, payload: null, clearStacks: clearStacksOnSceneChanged);
                    break;

                default:
                    Debug.LogWarning("[GameManagerScreenRoutingBridge] Unhandled scene type: " + type);
                    break;
            }
        }
    }
}
