using UnityEngine;
using KingdomIdle.MageTower;

namespace KingdomIdle.UIToolkit
{
    // MageTowerManager 이벤트 → HUD 갱신 브릿지
    [DefaultExecutionOrder(-930)]
    public sealed class UITKMageTowerHudBridge : MonoBehaviour
    {
        private MageTowerManager _mgr;

        private void Start()
        {
            _mgr = MageTowerManager.Instance;
            if (_mgr != null)
                _mgr.OnStateChanged += OnStateChanged;
        }

        private void OnDestroy()
        {
            if (_mgr != null)
                _mgr.OnStateChanged -= OnStateChanged;
        }

        private void OnStateChanged()
        {
            if (UITKMageTowerHudController.Instance != null)
                UITKMageTowerHudController.Instance.RefreshSlots();
        }
    }
}
