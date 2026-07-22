using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KingdomIdle.UGUI
{
    /// <summary>설정 모달 셸 (환경설정). SettingsModalController가 바인딩.</summary>
    public sealed class SettingsModalView : MonoBehaviour
    {
        [SerializeField] internal Button outsideCatcher;   // 오버레이 딤 자체 — 바깥 탭 닫기
        [SerializeField] internal RectTransform panel;
        [SerializeField] internal TMP_Text lblServer;
        [SerializeField] internal TMP_Text lblVersion;
        [SerializeField] internal Button btnGoogleChip;

        [Header("Toggles")]
        [SerializeField] internal Toggle tglPowerSave;
        [SerializeField] internal Toggle tglHideItem;
        [SerializeField] internal Toggle tglDamageText;
        [SerializeField] internal Toggle tglScreenShake;
        [SerializeField] internal Toggle tglPush;
        [SerializeField] internal Toggle tglNightPush;

        [Header("Volume")]
        [SerializeField] internal Slider sldVolume;
        [SerializeField] internal Button btnMute;
        [SerializeField] internal Image btnMuteBg;

        [Header("Bottom")]
        [SerializeField] internal Button btnWithdraw;
        [SerializeField] internal Button btnSave;
        [SerializeField] internal Button btnSaveClose;
    }
}
