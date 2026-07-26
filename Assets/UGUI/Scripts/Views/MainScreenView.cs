using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KingdomIdle.UGUI
{
    /// <summary>Screen_Main 셸: 상단 HUD + 스테이지 영역 + 하단 탭 바 + 드롭다운 팝업들.</summary>
    public sealed class MainScreenView : MonoBehaviour
    {
        [Header("Top HUD")]
        [SerializeField] internal Button btnProfile;
        [SerializeField] internal TMP_Text lblNickname;
        [SerializeField] internal TMP_Text lblProfileLevel;
        [SerializeField] internal Button btnCurrency;
        [SerializeField] internal TMP_Text lblGold;
        [SerializeField] internal TMP_Text lblAncientCoin;
        [SerializeField] internal Button btnAncientCoin;
        [SerializeField] internal Button btnHamburger;
        [SerializeField] internal RectTransform btnHamburgerRect;

        [Header("Currency dropdown")]
        [SerializeField] internal GameObject popupCurrencies;
        [SerializeField] internal RectTransform popupCurrenciesRect;
        [SerializeField] internal CanvasGroup popupCurrenciesGroup;
        [SerializeField] internal RectTransform popupCurrenciesContent;

        [Header("Hamburger dropdown")]
        [SerializeField] internal GameObject popupHamburger;
        [SerializeField] internal RectTransform popupHamburgerRect;
        [SerializeField] internal CanvasGroup popupHamburgerGroup;
        [SerializeField] internal Button btnMenuInventory;
        [SerializeField] internal Button btnMenuSettings;
        [SerializeField] internal Button btnMenuNotice;
        [SerializeField] internal Button btnMenuMail;
        [SerializeField] internal Button outsideCatcher;   // 드롭다운 열림 시 바깥 탭 감지용 풀스크린 투명 버튼

        [Header("Wave HUD")]
        [SerializeField] internal WaveHudView waveHud;

        [Header("Bottom tab bar")]
        [SerializeField] internal RectTransform bottomBar;
        [SerializeField] internal MainTabButtonView tabDevelopment;
        [SerializeField] internal MainTabButtonView tabKingdomArmy;
        [SerializeField] internal MainTabButtonView tabGacha;
    }
}
