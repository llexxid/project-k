using UnityEngine;
using KingdomIdle.MageTower;

namespace KingdomIdle.UGUI
{
    /// <summary>MageTowerManager 상태 변경 → HUD 슬롯 갱신 (UITKMageTowerHudBridge 이식).</summary>
    [DefaultExecutionOrder(-930)]
    public sealed class MageTowerHudBridge : MonoBehaviour
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
            if (MageTowerHudController.Instance != null)
                MageTowerHudController.Instance.RefreshSlots();
        }
    }
}
