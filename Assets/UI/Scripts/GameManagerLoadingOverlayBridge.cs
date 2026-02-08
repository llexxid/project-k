using UnityEngine;
using Scripts.Core;

namespace KingdomIdle.UI
{
    /// <summary>
    /// GameManager의 씬 로드 이벤트를 받아 Loading Overlay를 ON/OFF 한다.
    /// </summary>
    public sealed class GameManagerLoadingOverlayBridge : MonoBehaviour
    {
        [SerializeField] private string loadingText = "Loading...";

        private bool _hooked;

        private void OnEnable()
        {
            TryHook();
        }

        private void Update()
        {
            // 실행 순서/초기화 타이밍 꼬임 대비
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

            GameManager.Instance.SceneLoadStarted += HandleStarted;
            GameManager.Instance.SceneLoadFinished += HandleFinished;
            _hooked = true;
        }

        private void Unhook()
        {
            if (!_hooked) return;
            if (GameManager.Instance == null) return;

            GameManager.Instance.SceneLoadStarted -= HandleStarted;
            GameManager.Instance.SceneLoadFinished -= HandleFinished;
            _hooked = false;
        }

        private void HandleStarted(eSceneType type)
        {
            if (UIManager.Instance == null) return;

            // UIManager에 아래 API가 있어야 함:
            // SetLoading(bool isOn, string text = null)
            UIManager.Instance.SetLoading(true, loadingText);
        }

        private void HandleFinished(eSceneType type)
        {
            if (UIManager.Instance == null) return;

            UIManager.Instance.SetLoading(false);
        }
    }
}
