using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using KingdomIdle.UI;
using Scripts.Core;
using Scripts.Users;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// 메인 화면 로직 (UITKUIManager.BindMain + 재화/햄버거 팝업 영역 이식).
    /// 하단 탭, 상단 재화 HUD(폴링 + EconomyBridge 이벤트), 재화/햄버거 드롭다운, 웨이브 HUD 초기화.
    /// </summary>
    public sealed class MainScreenController
    {
        private const float CurrencyPollInterval = 0.5f;
        private const float DropdownAnimDuration = 0.18f;
        private const float DropdownHiddenYOffset = 10f;   // 위로 10px에서 슬라이드 인 (USS translate -10px 대응)

        private MainScreenView _view;
        private UIManager _host;

        // 재화
        private object _wallet;
        private float _currencyPollTimer;
        private Action<eCurrency, long> _economyChangeHandler;
        private bool _currencyOpen;
        private Coroutine _currencyCo;
        private Vector2 _currencyBasePos;

        // 햄버거
        private bool _hamburgerOpen;
        private Coroutine _hamburgerCo;
        private Vector2 _hamburgerBasePos;

        // 탭
        private readonly List<(MainTabButtonView view, UIPanelId id)> _tabs = new();

        public void Bind(MainScreenView view, UIManager host)
        {
            _view = view;
            _host = host;

            // 각 섹션을 try/catch로 격리 — 한 섹션 예외가 나머지 바인딩을 막지 않도록 (기존 동작 유지)
            try { BindTabs(); }
            catch (Exception ex) { Debug.LogError($"MainScreen.Tabs failed: {ex}"); }

            try { BindCurrency(); }
            catch (Exception ex) { Debug.LogError($"MainScreen.Currency failed: {ex}"); }

            try { BindHamburger(); }
            catch (Exception ex) { Debug.LogError($"MainScreen.Hamburger failed: {ex}"); }

            try { BindMenus(); }
            catch (Exception ex) { Debug.LogError($"MainScreen.Menus failed: {ex}"); }

            try { WaveUIController.Init(_view.waveHud); }
            catch (Exception ex) { Debug.LogError($"MainScreen.WaveUIController.Init failed: {ex}"); }

            _host.FrameTick += OnFrameTick;
            _host.PanelStackChanged += RefreshTabButtonSelection;
            RefreshTabButtonSelection();

            if (_view.outsideCatcher != null)
            {
                _view.outsideCatcher.gameObject.SetActive(false);
                _view.outsideCatcher.onClick.AddListener(() =>
                {
                    if (_currencyOpen) CloseCurrencyPopup();
                    if (_hamburgerOpen) CloseHamburgerMenu();
                });
            }
        }

        public void Dispose()
        {
            if (_host != null)
            {
                _host.FrameTick -= OnFrameTick;
                _host.PanelStackChanged -= RefreshTabButtonSelection;
                _host.StopRunningCoroutine(_currencyCo);
                _host.StopRunningCoroutine(_hamburgerCo);
            }

            UnhookEconomyChangeHandler();
            WaveUIController.Dispose();

            _currencyCo = null;
            _hamburgerCo = null;
            _view = null;
            _host = null;
            _wallet = null;
            _tabs.Clear();
        }

        /// <summary>뒤로가기 처리 — 재화 팝업/햄버거 메뉴가 열려있으면 닫고 true.</summary>
        public bool HandleBack()
        {
            if (_currencyOpen)
            {
                CloseCurrencyPopup();
                return true;
            }

            if (_hamburgerOpen)
            {
                CloseHamburgerMenu();
                return true;
            }

            return false;
        }

        private void OnFrameTick()
        {
            if (_view == null) return;

            _currencyPollTimer += Time.unscaledDeltaTime;
            if (_currencyPollTimer >= CurrencyPollInterval)
            {
                _currencyPollTimer = 0f;
                RefreshTopCurrencyLabels();
                RefreshNickname();

                if (_currencyOpen)
                    RebuildCurrencyPopupContents();
            }
        }

        /// <summary>
        /// 상단 HUD 닉네임을 서버 유저 데이터와 연동한다.
        /// UserManager의 유저는 비공개 필드라 EconomyBridge와 동일하게 리플렉션으로 접근.
        /// (서버 동기화가 늦게 끝나는 경우를 위해 폴링에서도 갱신)
        /// </summary>
        private void RefreshNickname()
        {
            if (_view == null || _view.lblNickname == null) return;

            try
            {
                var um = UserManager.Instance;
                if (um == null) return;

                var userField = um.GetType().GetField("_user",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                var user = userField?.GetValue(um) as User;
                string nick = user?.GetNickName();

                if (!string.IsNullOrWhiteSpace(nick) && _view.lblNickname.text != nick)
                    _view.lblNickname.text = nick;

                // 프로필 레벨 훈장 배지 갱신 (실제 유저 레벨)
                if (_view.lblProfileLevel != null && user != null)
                {
                    string lvl = user.GetLevel().ToString();
                    if (_view.lblProfileLevel.text != lvl)
                        _view.lblProfileLevel.text = lvl;
                }
            }
            catch
            {
                // 닉네임은 표시용 — 실패해도 무시 (기본 "닉네임" 유지)
            }
        }

        // ═══════════════════════════════════════════
        //  하단 탭
        // ═══════════════════════════════════════════

        private void BindTabs()
        {
            _tabs.Clear();
            RegisterTab(_view.tabDevelopment, UIPanelId.Development, "developmentPanel");
            RegisterTab(_view.tabKingdomArmy, UIPanelId.KingdomArmy, "kingdomArmyPanel");
            RegisterTab(_view.tabGacha, UIPanelId.Gacha, "gachaPanel");
        }

        private void RegisterTab(MainTabButtonView tab, UIPanelId panelId, string panelName)
        {
            if (tab == null || tab.button == null) return;
            tab.button.onClick.AddListener(() => OnTabPressed(panelId, panelName));
            _tabs.Add((tab, panelId));
        }

        private void OnTabPressed(UIPanelId panelId, string panelName)
        {
            // 열려있는 탭을 다시 누르면 닫기 (토글)
            if (_host.HasActiveTabPanel && _host.ActiveTabPanelId.Equals(panelId))
            {
                _host.ClearPanels();
                return;
            }

            _host.ClearPanels();
            _host.PushPanel(panelId, panelName, clearBefore: false, isTabPanel: true);
        }

        private void RefreshTabButtonSelection()
        {
            foreach (var (tab, id) in _tabs)
            {
                if (tab == null) continue;
                bool isSelected = _host != null && _host.HasActiveTabPanel && _host.ActiveTabPanelId.Equals(id);
                tab.SetSelected(isSelected);
            }
        }

        // ═══════════════════════════════════════════
        //  재화 HUD + 드롭다운
        // ═══════════════════════════════════════════

        private void BindCurrency()
        {
            try { _wallet = WalletLocator.FindAnyWallet(); }
            catch (Exception ex)
            {
                Debug.LogError($"MainScreen.FindAnyWallet failed: {ex}");
                _wallet = null;
            }

            try { RefreshTopCurrencyLabels(); }
            catch (Exception ex) { Debug.LogError($"MainScreen.RefreshTopCurrencyLabels failed: {ex}"); }

            HookEconomyChangeHandler();

            if (_view.popupCurrenciesRect != null)
                _currencyBasePos = _view.popupCurrenciesRect.anchoredPosition;

            if (_view.popupCurrencies != null)
                _view.popupCurrencies.SetActive(false);

            if (_view.popupCurrencies != null)
            {
                if (_view.btnCurrency != null)
                    _view.btnCurrency.onClick.AddListener(() => OnCurrencyChipTapped(premium: false));
                if (_view.btnAncientCoin != null)
                    _view.btnAncientCoin.onClick.AddListener(() => OnCurrencyChipTapped(premium: true));
            }
        }

        private bool _currencyPremiumGroup;

        /// <summary>재화 칩 탭: 골드=무료 재화 목록, 고대주화=유료 재화 목록을 그 아래로 펼친다.</summary>
        private void OnCurrencyChipTapped(bool premium)
        {
            RefreshTopCurrencyLabels();
            if (_hamburgerOpen) CloseHamburgerMenuImmediate();

            // 같은 칩 재탭 → 닫기, 다른 칩 탭 → 그룹 교체 후 열기
            if (_currencyOpen && _currencyPremiumGroup == premium)
            {
                CloseCurrencyPopup();
                return;
            }
            _currencyPremiumGroup = premium;
            RebuildCurrencyPopupContents();
            if (!_currencyOpen) OpenCurrencyPopup();
        }

        /// <summary>재화 변경 이벤트 구독 — 강화/가챠/전투 보상 시 즉시 HUD 갱신 (폴링 지연 보완).</summary>
        private void HookEconomyChangeHandler()
        {
            if (_economyChangeHandler != null) return;

            _economyChangeHandler = (currency, _) =>
            {
                try
                {
                    if (_view == null) return;

                    if (currency == eCurrency.Gold || currency == eCurrency.AncientCoin)
                        RefreshTopCurrencyLabels();

                    if (_currencyOpen)
                        RebuildCurrencyPopupContents();
                }
                catch (Exception ex) { Debug.LogError($"EconomyChangeHandler failed: {ex}"); }
            };
            EconomyBridge.OnAmountChanged += _economyChangeHandler;
        }

        private void UnhookEconomyChangeHandler()
        {
            if (_economyChangeHandler == null) return;
            EconomyBridge.OnAmountChanged -= _economyChangeHandler;
            _economyChangeHandler = null;
        }

        private long _prevGold = -1, _prevAncient = -1;

        private void RefreshTopCurrencyLabels()
        {
            if (_wallet == null) _wallet = WalletLocator.FindAnyWallet();
            if (_view == null) return;

            long gold = GetCurrencyAmount(eCurrency.Gold);
            long ancient = GetCurrencyAmount(eCurrency.AncientCoin);

            if (_view.lblGold != null)
            {
                _view.lblGold.text = gold.ToString("N0");
                if (_prevGold >= 0 && gold != _prevGold && _view.btnCurrency != null)
                    UITween.Punch(_view.btnCurrency.transform as RectTransform);
            }
            if (_view.lblAncientCoin != null)
            {
                _view.lblAncientCoin.text = ancient.ToString("N0");
                if (_prevAncient >= 0 && ancient != _prevAncient && _view.btnAncientCoin != null)
                    UITween.Punch(_view.btnAncientCoin.transform as RectTransform);
            }
            _prevGold = gold; _prevAncient = ancient;
        }

        private long GetCurrencyAmount(eCurrency c)
        {
            if (_wallet == null) return 0;
            return WalletLocator.TryGetAmount(_wallet, c, out long amount) ? amount : 0;
        }

        private string GetCurrencyText(eCurrency currency)
        {
            if (_wallet == null) return "0";
            if (WalletLocator.TryGetAmount(_wallet, currency, out long amount))
                return amount.ToString("N0");
            return "0";
        }

        private void RebuildCurrencyPopupContents()
        {
            var content = _view != null ? _view.popupCurrenciesContent : null;
            if (content == null) return;

            for (int i = content.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(content.GetChild(i).gameObject);

            AddCurrencyLine(content, _currencyPremiumGroup ? "유료 재화" : "보유 재화", isTitle: true);

            // 탭한 칩의 재화 그룹만 표시 (골드칩=무료/소프트, 고대주화칩=유료/프리미엄)
            var values = (eCurrency[])Enum.GetValues(typeof(eCurrency));
            foreach (var c in values)
            {
                if (!IsGroupCurrency(c, _currencyPremiumGroup)) continue;
                AddCurrencyLine(content, $"{GetCurrencyLabelKor(c)}: {GetCurrencyText(c)}", isTitle: false);
            }
        }

        /// <summary>재화 그룹 분류: 프리미엄=고대주화, 무료=골드·비전지식·전직파편.</summary>
        private static bool IsGroupCurrency(eCurrency c, bool premium)
        {
            bool isPremium = c == eCurrency.AncientCoin;
            if (premium) return isPremium;
            return c == eCurrency.Gold || c == eCurrency.ArcaneKnowledge || c == eCurrency.ClassFragment;
        }

        private void AddCurrencyLine(RectTransform parent, string text, bool isTitle)
        {
            var prefab = _host != null && _host.Catalog != null ? _host.Catalog.itemCurrencyLine : null;
            if (prefab == null) return;

            var go = UnityEngine.Object.Instantiate(prefab, parent, false);
            var line = go.GetComponent<CurrencyLineItemView>();
            if (line != null && line.label != null)
            {
                line.label.text = text;
                if (isTitle)
                {
                    line.label.fontSize = 28f;
                    line.label.fontStyle = TMPro.FontStyles.Bold;
                }
            }
        }

        /// <summary>UI 상 표시할 재화 필터. Gold / AncientCoin / ArcaneKnowledge 만 노출.</summary>
        private static bool IsDisplayedCurrency(eCurrency c)
        {
            return c == eCurrency.Gold
                || c == eCurrency.AncientCoin
                || c == eCurrency.ArcaneKnowledge;
        }

        internal static string GetCurrencyLabelKor(eCurrency c)
        {
            switch (c)
            {
                case eCurrency.Gold: return "골드";
                case eCurrency.AncientCoin: return "고대주화";
                case eCurrency.ArcaneKnowledge: return "비전지식";
                case eCurrency.ClassFragment: return "전직 파편";
                default: return c.ToString();
            }
        }

        private void ToggleCurrencyPopup()
        {
            if (_currencyOpen) CloseCurrencyPopup();
            else OpenCurrencyPopup();
        }

        private void OpenCurrencyPopup()
        {
            if (_view == null || _view.popupCurrencies == null) return;

            _view.popupCurrencies.SetActive(true);
            _view.popupCurrencies.transform.SetAsLastSibling();

            _host.StopRunningCoroutine(_currencyCo);
            _currencyCo = _host.RunCoroutine(AnimateDropdown(
                _view.popupCurrenciesRect, _view.popupCurrenciesGroup, _currencyBasePos,
                open: true, deactivateOnClose: _view.popupCurrencies, onDone: co => _currencyCo = co));
            _currencyOpen = true;
            UpdateOutsideCatcher();
        }

        private void CloseCurrencyPopup()
        {
            if (_view == null || _view.popupCurrencies == null) return;
            if (!_currencyOpen && !_view.popupCurrencies.activeSelf) return;

            _host.StopRunningCoroutine(_currencyCo);
            _currencyCo = _host.RunCoroutine(AnimateDropdown(
                _view.popupCurrenciesRect, _view.popupCurrenciesGroup, _currencyBasePos,
                open: false, deactivateOnClose: _view.popupCurrencies, onDone: co => _currencyCo = co));
            _currencyOpen = false;
            UpdateOutsideCatcher();
        }

        internal void CloseCurrencyPopupImmediate()
        {
            if (_view == null || _view.popupCurrencies == null) return;

            _host.StopRunningCoroutine(_currencyCo);
            _currencyCo = null;

            ApplyDropdownVisual(_view.popupCurrenciesRect, _view.popupCurrenciesGroup, _currencyBasePos, 0f, 1f);
            _view.popupCurrencies.SetActive(false);
            _currencyOpen = false;
            UpdateOutsideCatcher();
        }

        // ═══════════════════════════════════════════
        //  햄버거 메뉴
        // ═══════════════════════════════════════════

        private void BindHamburger()
        {
            if (_view.popupHamburgerRect != null)
                _hamburgerBasePos = _view.popupHamburgerRect.anchoredPosition;

            if (_view.popupHamburger != null)
                _view.popupHamburger.SetActive(false);

            if (_view.btnHamburger != null && _view.popupHamburger != null)
                _view.btnHamburger.onClick.AddListener(ToggleHamburgerMenu);
        }

        private void ToggleHamburgerMenu()
        {
            if (_hamburgerOpen) CloseHamburgerMenu();
            else OpenHamburgerMenu();
        }

        private void OpenHamburgerMenu()
        {
            if (_view == null || _view.popupHamburger == null) return;

            if (_currencyOpen)
                CloseCurrencyPopupImmediate();

            _view.popupHamburger.SetActive(true);
            _view.popupHamburger.transform.SetAsLastSibling();

            _host.StopRunningCoroutine(_hamburgerCo);
            _hamburgerCo = _host.RunCoroutine(AnimateHamburger(open: true));
            _hamburgerOpen = true;
            UpdateOutsideCatcher();
        }

        private void CloseHamburgerMenu()
        {
            if (_view == null || _view.popupHamburger == null) return;
            if (!_hamburgerOpen && !_view.popupHamburger.activeSelf) return;

            _host.StopRunningCoroutine(_hamburgerCo);
            _hamburgerCo = _host.RunCoroutine(AnimateHamburger(open: false));
            _hamburgerOpen = false;
            UpdateOutsideCatcher();
        }

        internal void CloseHamburgerMenuImmediate()
        {
            if (_view == null || _view.popupHamburger == null) return;

            _host.StopRunningCoroutine(_hamburgerCo);
            _hamburgerCo = null;

            ApplyDropdownVisual(_view.popupHamburgerRect, _view.popupHamburgerGroup, _hamburgerBasePos, 0f, 1f);
            if (_view.btnHamburgerRect != null)
                _view.btnHamburgerRect.localRotation = Quaternion.identity;

            _view.popupHamburger.SetActive(false);
            _hamburgerOpen = false;
            UpdateOutsideCatcher();
        }

        private IEnumerator AnimateHamburger(bool open)
        {
            float fromAngle = open ? 0f : 90f;
            float toAngle = open ? 90f : 0f;

            float fromY = open ? DropdownHiddenYOffset : 0f;
            float toY = open ? 0f : DropdownHiddenYOffset;

            float fromA = open ? 0f : 1f;
            float toA = open ? 1f : 0f;

            float t = 0f;
            while (t < DropdownAnimDuration)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / DropdownAnimDuration);
                float eased = u * u * (3f - 2f * u);

                ApplyHamburgerVisual(
                    Mathf.Lerp(fromAngle, toAngle, eased),
                    Mathf.Lerp(fromY, toY, eased),
                    Mathf.Lerp(fromA, toA, eased));

                yield return null;
            }

            ApplyHamburgerVisual(toAngle, toY, toA);

            if (!open && _view != null && _view.popupHamburger != null)
                _view.popupHamburger.SetActive(false);

            _hamburgerCo = null;
        }

        private void ApplyHamburgerVisual(float angleDeg, float yOffset, float opacity)
        {
            if (_view == null) return;

            // UITK rotate와 방향 일치 (UGUI z회전은 반시계 양수 → 부호 반전)
            if (_view.btnHamburgerRect != null)
                _view.btnHamburgerRect.localRotation = Quaternion.Euler(0f, 0f, -angleDeg);

            ApplyDropdownVisual(_view.popupHamburgerRect, _view.popupHamburgerGroup, _hamburgerBasePos, yOffset, opacity);
        }

        // ═══════════════════════════════════════════
        //  메뉴 버튼 (프로필/인벤토리/설정/공지/우편)
        // ═══════════════════════════════════════════

        private void BindMenus()
        {
            if (_view.btnProfile != null)
            {
                _view.btnProfile.onClick.AddListener(() =>
                {
                    if (_currencyOpen) CloseCurrencyPopup();
                    if (_hamburgerOpen) CloseHamburgerMenu();

                    // Profile 패널 미구현 — 기존 동작대로 왕국군 패널로 대체
                    _host.PushPanel(UIPanelId.KingdomArmy, "프로필", clearBefore: false, isTabPanel: false);
                });
            }

            if (_view.btnMenuInventory != null)
            {
                _view.btnMenuInventory.onClick.AddListener(() =>
                {
                    CloseHamburgerMenu();
                    if (_currencyOpen) CloseCurrencyPopup();
                    _host.PushPanel(UIPanelId.Inventory, null, clearBefore: false, isTabPanel: false);
                });
            }

            if (_view.btnMenuSettings != null)
            {
                _view.btnMenuSettings.onClick.AddListener(() =>
                {
                    CloseHamburgerMenu();
                    if (_currencyOpen) CloseCurrencyPopup();
                    _host.OpenSettings();
                });
            }

            if (_view.btnMenuNotice != null)
                _view.btnMenuNotice.onClick.AddListener(() => _host.ShowToast("현재는 지원하지 않는 기능입니다."));

            if (_view.btnMenuMail != null)
                _view.btnMenuMail.onClick.AddListener(() => _host.ShowToast("현재는 지원하지 않는 기능입니다."));
        }

        // ═══════════════════════════════════════════
        //  드롭다운 공용 애니메이션 (0.18s smoothstep, translate + fade)
        // ═══════════════════════════════════════════

        private IEnumerator AnimateDropdown(
            RectTransform rect, CanvasGroup group, Vector2 basePos,
            bool open, GameObject deactivateOnClose, Action<Coroutine> onDone)
        {
            float fromY = open ? DropdownHiddenYOffset : 0f;
            float toY = open ? 0f : DropdownHiddenYOffset;

            float fromA = open ? 0f : 1f;
            float toA = open ? 1f : 0f;

            float t = 0f;
            while (t < DropdownAnimDuration)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / DropdownAnimDuration);
                float eased = u * u * (3f - 2f * u);

                ApplyDropdownVisual(rect, group, basePos,
                    Mathf.Lerp(fromY, toY, eased),
                    Mathf.Lerp(fromA, toA, eased));

                yield return null;
            }

            ApplyDropdownVisual(rect, group, basePos, toY, toA);

            if (!open && deactivateOnClose != null)
                deactivateOnClose.SetActive(false);

            onDone?.Invoke(null);
        }

        private static void ApplyDropdownVisual(RectTransform rect, CanvasGroup group, Vector2 basePos, float yOffset, float opacity)
        {
            if (rect != null)
                rect.anchoredPosition = basePos + new Vector2(0f, yOffset);
            if (group != null)
                group.alpha = opacity;
        }

        private void UpdateOutsideCatcher()
        {
            if (_view == null || _view.outsideCatcher == null) return;
            bool anyOpen = _currencyOpen || _hamburgerOpen;
            _view.outsideCatcher.gameObject.SetActive(anyOpen);
            if (!anyOpen) return;

            // 캐처는 열린 드롭다운 바로 아래(형제 순서상 먼저)에 있어야
            // 드롭다운 자체 클릭은 살아있고 그 외 영역 탭만 닫기로 처리된다.
            Transform openPopup = _currencyOpen && _view.popupCurrencies != null
                ? _view.popupCurrencies.transform
                : (_view.popupHamburger != null ? _view.popupHamburger.transform : null);
            if (openPopup == null) return;

            // SetSiblingIndex로 캐처를 팝업 자리에 밀어넣으면 캐처가 팝업 '위'로 올 수 있으므로
            // 팝업을 다시 최상단으로 올려 캐처가 항상 팝업 아래에 있도록 보장한다.
            _view.outsideCatcher.transform.SetSiblingIndex(openPopup.GetSiblingIndex());
            openPopup.SetAsLastSibling();
        }
    }
}
