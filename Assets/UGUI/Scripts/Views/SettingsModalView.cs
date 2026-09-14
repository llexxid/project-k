using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KingdomIdle.UGUI
{
    /// <summary>설정 모달 셸 (환경설정). SettingsModalController가 바인딩.</summary>
    public sealed class SettingsModalView : MonoBehaviour
    {
        private void OnDisable() => PlayerPrefs.Save();
        private void OnApplicationPause(bool paused) { if (paused) PlayerPrefs.Save(); }
        private void OnEnable() => FitPanel();
        private void OnRectTransformDimensionsChange() { if (isActiveAndEnabled) FitPanel(); }
        private void FitPanel()
        {
            if (panel == null || transform.parent is not RectTransform parent) return;
            var size = new Vector2(Mathf.Min(960, parent.rect.width - 48), Mathf.Min(1640, parent.rect.height - 80));
            if (size.x > 0 && size.y > 0 && panel.sizeDelta != size) panel.sizeDelta = size;
        }
        [SerializeField] internal Button outsideCatcher;   // 오버레이 딤 자체 — 바깥 탭 닫기
        [SerializeField] internal RectTransform panel;
        [SerializeField] internal TMP_Text lblServer;
        [SerializeField] internal TMP_Text lblVersion;
        [SerializeField] internal Button btnGoogleChip;
        [SerializeField] internal Button btnClose;
        [SerializeField] internal ScrollRect scroll;
        [SerializeField] internal Button[] numberButtons;
        [SerializeField] internal TMP_Text numberPreview;
        [SerializeField] internal TMP_Text numberHint;
        [SerializeField] internal Toggle tglKeepAwake;

        [Header("Toggles")]
        [SerializeField] internal Toggle tglPowerSave;
        [SerializeField] internal Toggle tglLowSpec;
        [SerializeField] internal Toggle tglHideItem;
        [SerializeField] internal Toggle tglDamageText;
        [SerializeField] internal Toggle tglScreenShake;
        [SerializeField] internal Toggle tglPush;
        [SerializeField] internal Toggle tglNightPush;

        [Header("Volume")]
        [SerializeField] internal Slider sldVolume;
        [SerializeField] internal Slider sldMusic;
        [SerializeField] internal Slider sldEffects;
        [SerializeField] internal TMP_Text lblVolume;
        [SerializeField] internal TMP_Text lblMusic;
        [SerializeField] internal TMP_Text lblEffects;
        [SerializeField] internal Button btnMute;
        [SerializeField] internal Image btnMuteBg;

        [Header("Bottom")]
        [SerializeField] internal Button btnWithdraw;
        [SerializeField] internal Button btnSave;
        [SerializeField] internal Button btnSaveClose;
    }
}
