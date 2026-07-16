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

    /// <summary>토스트 셸: 중앙 박스 + 라벨. 입력을 막지 않는다(레이캐스트 비대상).</summary>
    public sealed class ToastView : MonoBehaviour
    {
        [SerializeField] internal TMP_Text label;
    }

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

    /// <summary>뽑기 결과 팝업 셸. GachaResultPopupController가 카드 리스트를 채운다.</summary>
    public sealed class GachaResultPopupView : MonoBehaviour
    {
        [SerializeField] internal TMP_Text title;
        [SerializeField] internal ScrollRect scroll;
        [SerializeField] internal RectTransform grid;
        [SerializeField] internal RectTransform buttonRow;
        [SerializeField] internal Button btnDone;
        [SerializeField] internal Button btnRePull1;
        [SerializeField] internal Button btnRePullN;
        [SerializeField] internal TMP_Text btnRePull1Label;
        [SerializeField] internal TMP_Text btnRePullNLabel;
    }
}
