using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KingdomIdle.UGUI
{
    /// <summary>Overlay_Loading 셸: 딤 + 라벨 + 진행 바.</summary>
    public sealed class LoadingOverlayView : MonoBehaviour
    {
        [SerializeField] internal TMP_Text lblLoading;
        [SerializeField] internal Slider progressBar;

        public void SetProgress01(float normalized01)
        {
            if (progressBar != null) progressBar.value = Mathf.Clamp01(normalized01);
        }
    }
}
