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
        [SerializeField] internal Button btnReincarnation;      // 환생 — 프로필 옆
        [SerializeField] internal GameObject reincarnationDot;  // 환생 가능 알림 닷

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
        [SerializeField] internal Button btnMenuGuide;
        [SerializeField] internal Button btnMenuSettings;
        [SerializeField] internal Button btnMenuNotice;
        [SerializeField] internal Button btnMenuMail;
        [SerializeField] internal Button btnMenuCheckIn;
        [SerializeField] internal Button outsideCatcher;   // 드롭다운 열림 시 바깥 탭 감지용 풀스크린 투명 버튼

        [Header("Wave HUD")]
        [SerializeField] internal WaveHudView waveHud;

        [Header("Bottom tab bar")]
        [SerializeField] internal RectTransform bottomBar;
        [SerializeField] internal MainTabButtonView tabDevelopment;
        [SerializeField] internal MainTabButtonView tabKingdomArmy;
        [SerializeField] internal MainTabButtonView tabDungeon;
        [SerializeField] internal MainTabButtonView tabGacha;
        [SerializeField] internal MainTabButtonView tabStore;

        private float _headerLayoutWidth = -1f;

        private void OnEnable()
        {
            _headerLayoutWidth = -1f;
            RefreshHeaderLayout();
        }

        private void OnRectTransformDimensionsChange()
        {
            if (isActiveAndEnabled) RefreshHeaderLayout();
        }

        private void RefreshHeaderLayout()
        {
            if (!(transform is RectTransform root) || btnProfile == null || btnCurrency == null || btnAncientCoin == null) return;
            float width = root.rect.width;
            if (width <= 0f || Mathf.Approximately(width, _headerLayoutWidth)) return;
            var left = btnProfile.transform.parent.GetComponent<HorizontalLayoutGroup>();
            var right = btnCurrency.transform.parent.GetComponent<HorizontalLayoutGroup>();
            var header = left != null ? left.transform.parent.GetComponent<HorizontalLayoutGroup>() : null;
            if (header == null || right == null) return;
            _headerLayoutWidth = width;

            // SafeArea also reduces the horizontal space. Keep the five existing hit targets;
            // spend decorative gaps first, then stack each currency icon above its value.
            bool compact = width < 976f;
            float chipWidth = compact ? 248f : 272f;
            float fixedWidth = 3f * 144f + 2f * chipWidth;
            float gapRatio = Mathf.Clamp01((width - fixedWidth) / 86f);
            int edge = Mathf.FloorToInt(22f * gapRatio);
            header.padding = new RectOffset(edge, edge, 0, 0);
            left.spacing = 18f * gapRatio;
            right.spacing = 12f * gapRatio;

            SetCurrencyLayout(btnCurrency, chipWidth, compact);
            SetCurrencyLayout(btnAncientCoin, chipWidth, compact);
            LayoutRebuilder.MarkLayoutForRebuild((RectTransform)header.transform);
        }

        private static void SetCurrencyLayout(Button button, float width, bool compact)
        {
            var layout = button.GetComponent<LayoutElement>();
            var row = button.GetComponent<HorizontalLayoutGroup>();
            if (layout != null) layout.minWidth = layout.preferredWidth = width;
            if (row != null) row.padding = compact ? new RectOffset(8, 8, 72, 0) : new RectOffset(78, 8, 0, 0);

            var ring = button.transform.Find("IconRing") as RectTransform;
            if (ring == null) return;
            ring.anchorMin = ring.anchorMax = compact ? new Vector2(.5f, 1f) : new Vector2(0f, .5f);
            ring.pivot = compact ? new Vector2(.5f, 1f) : new Vector2(0f, .5f);
            ring.anchoredPosition = compact ? new Vector2(0f, -4f) : new Vector2(10f, 0f);
            ring.sizeDelta = new Vector2(64f, 64f);
        }
    }
}
