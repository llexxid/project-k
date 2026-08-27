using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KingdomIdle.Divine;
using KingdomIdle.UI;
using Scripts.Core;

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
            if (_profilePopup != null)
                UnityEngine.Object.Destroy(_profilePopup);
            if (_rankingPopup != null)
                UnityEngine.Object.Destroy(_rankingPopup);

            _currencyCo = null;
            _hamburgerCo = null;
            _profilePopup = null;
            _profileView = null;
            _rankingPopup = null;
            _rankingView = null;
            _view = null;
            _host = null;
            _wallet = null;
            _tabs.Clear();
        }

        /// <summary>뒤로가기 처리 — 재화 팝업/햄버거 메뉴가 열려있으면 닫고 true.</summary>
        public bool HandleBack()
        {
            if (_rankingPopup != null && _rankingPopup.activeSelf)
            {
                CloseRankingPopup();
                return true;
            }

            if (_profilePopup != null && _profilePopup.activeSelf)
            {
                CloseProfilePopup();
                return true;
            }

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
                RefreshReincarnationDot();

                if (_currencyOpen)
                    RebuildCurrencyPopupContents();
            }
        }

        /// <summary>환생 가능해지면 상단 환생 버튼에 붉은 알림 닷을 켠다 (0.5s 폴링).</summary>
        private void RefreshReincarnationDot()
        {
            if (_view == null || _view.reincarnationDot == null) return;

            bool can = false;
            try
            {
                var service = GameManager.Instance != null ? GameManager.Instance.Reincarnation : null;
                if (service != null)
                    can = service.GetPreview().CanReincarnate;
            }
            catch { /* 초기화 전/서비스 부재 — 닷 숨김 유지 */ }

            if (_view.reincarnationDot.activeSelf != can)
                _view.reincarnationDot.SetActive(can);
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

                string nick = um.GetUserName();

                if (!string.IsNullOrWhiteSpace(nick) && _view.lblNickname.text != nick)
                    _view.lblNickname.text = nick;

                // 프로필 레벨 훈장 배지 갱신 (실제 유저 레벨)
                if (_view.lblProfileLevel != null)
                {
                    string lvl = um.GetUserLevel().ToString();
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
            RegisterTab(_view.tabDungeon, UIPanelId.Dungeon, "dungeonPanel");
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

            // 탭한 칩 바로 아래로 드롭다운 정렬
            var target = premium ? _view.btnAncientCoin : _view.btnCurrency;
            PositionDropdownUnder(_view.popupCurrenciesRect, target != null ? target.transform as RectTransform : null);
            if (_view.popupCurrenciesRect != null) _currencyBasePos = _view.popupCurrenciesRect.anchoredPosition;

            RebuildCurrencyPopupContents();
            if (!_currencyOpen) OpenCurrencyPopup();
            else ApplyDropdownVisual(_view.popupCurrenciesRect, _view.popupCurrenciesGroup, _currencyBasePos, 0f, 1f); // 열린 채 그룹 전환 시 새 위치로 스냅
        }

        /// <summary>드롭다운을 대상 버튼의 바로 아래·오른쪽 정렬로 배치한다(부모 버튼에 라인업).</summary>
        private void PositionDropdownUnder(RectTransform dropdown, RectTransform target, float gap = 10f)
        {
            if (dropdown == null || target == null) return;
            var parent = dropdown.parent as RectTransform;
            if (parent == null) return;

            Canvas.ForceUpdateCanvases();
            var corners = new Vector3[4];
            target.GetWorldCorners(corners); // 0=BL,1=TL,2=TR,3=BR
            Vector2 brLocal = parent.InverseTransformPoint(corners[3]); // 대상 우하단(부모 로컬)

            dropdown.anchorMin = dropdown.anchorMax = new Vector2(1f, 1f);
            dropdown.pivot = new Vector2(1f, 1f);
            Rect pr = parent.rect;   // 앵커(1,1) 기준점 = 부모 우상단
            dropdown.anchoredPosition = new Vector2(brLocal.x - pr.xMax, brLocal.y - pr.yMax - gap);
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
                _view.lblGold.text = FormatChipAmount(gold);
                if (_prevGold >= 0 && gold != _prevGold && _view.btnCurrency != null)
                    UITween.Punch(_view.btnCurrency.transform as RectTransform);
            }
            if (_view.lblAncientCoin != null)
            {
                _view.lblAncientCoin.text = FormatChipAmount(ancient);
                if (_prevAncient >= 0 && ancient != _prevAncient && _view.btnAncientCoin != null)
                    UITween.Punch(_view.btnAncientCoin.transform as RectTransform);
            }
            _prevGold = gold; _prevAncient = ancient;
        }

        /// <summary>
        /// 상단바 칩 전용 축약 표기 — 칩 값 영역이 120px 뿐이라 100만("1,000,000"=약 150px)부터
        /// 말줄임(…)이 났다. 한국식 단위(만/억/조)로 줄이고, 그 아래는 기존 콤마 표기 유지.
        /// 재화 드롭다운(420px)은 정확한 값이 중요해 계속 N0 를 쓴다(GetCurrencyText).
        /// </summary>
        internal static string FormatChipAmount(long amount)
        {
            const long Man = 10_000L;          // 만
            const long Eok = 100_000_000L;     // 억
            const long Jo = 1_000_000_000_000L; // 조

            if (amount < 100L * Man)            // < 100만 — "999,999" 까지는 그대로 읽힌다
                return amount.ToString("N0");
            if (amount < Eok)
                return TrimUnit(amount / (double)Man, "만");
            if (amount < Jo)
                return TrimUnit(amount / (double)Eok, "억");
            return TrimUnit(amount / (double)Jo, "조");
        }

        private static string TrimUnit(double value, string unit)
        {
            // 세 자리까지는 소수 1자리, 그 이상은 정수 — "128.4만", "1,234만", "1.2억"
            string body = value < 1000 ? value.ToString("0.#") : value.ToString("N0");
            return body + unit;
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

            AddCurrencyLine(content, null, _currencyPremiumGroup ? "유료 재화" : "보유 재화", null, isTitle: true);

            // 탭한 칩의 재화 그룹만 표시 (골드칩=무료/소프트, 고대주화칩=유료/프리미엄)
            var values = (eCurrency[])Enum.GetValues(typeof(eCurrency));
            foreach (var c in values)
            {
                if (!IsGroupCurrency(c, _currencyPremiumGroup)) continue;
                AddCurrencyLine(content, GetCurrencyIcon(c), GetCurrencyLabelKor(c), GetCurrencyText(c), isTitle: false);
            }
        }

        /// <summary>재화별 러스틱 아이콘 (드롭다운 행용).</summary>
        private Sprite GetCurrencyIcon(eCurrency c)
        {
            var cat = _host != null ? _host.Catalog : null;
            if (cat == null) return null;
            switch (c)
            {
                case eCurrency.Gold: return cat.iconCoin;
                case eCurrency.AncientCoin: return cat.iconAncientCoin;
                case eCurrency.ArcaneKnowledge: return cat.iconArcane;
                case eCurrency.ClassFragment: return cat.iconFragment;
                default: return cat.iconGem;
            }
        }

        /// <summary>재화 그룹 분류: 프리미엄=고대주화, 무료=골드·비전지식·전직파편.</summary>
        private static bool IsGroupCurrency(eCurrency c, bool premium)
        {
            bool isPremium = c == eCurrency.AncientCoin;
            if (premium) return isPremium;
            return c == eCurrency.Gold || c == eCurrency.ArcaneKnowledge || c == eCurrency.ClassFragment;
        }

        private void AddCurrencyLine(RectTransform parent, Sprite icon, string name, string value, bool isTitle)
        {
            var prefab = _host != null && _host.Catalog != null ? _host.Catalog.itemCurrencyLine : null;
            if (prefab == null) return;

            var go = UnityEngine.Object.Instantiate(prefab, parent, false);
            var line = go.GetComponent<CurrencyLineItemView>();
            if (line != null) line.Set(icon, name, value, isTitle);
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

            // 햄버거 버튼 바로 아래로 정렬
            PositionDropdownUnder(_view.popupHamburgerRect,
                _view.btnHamburgerRect != null ? _view.btnHamburgerRect : (_view.btnHamburger != null ? _view.btnHamburger.transform as RectTransform : null));
            if (_view.popupHamburgerRect != null) _hamburgerBasePos = _view.popupHamburgerRect.anchoredPosition;

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

        // ── 프로필 팝업(더미) ──
        private GameObject _profilePopup;
        private ProfilePopupView _profileView;

        private void OpenProfilePopup()
        {
            if (_view == null) return;
            if (_profilePopup == null)
            {
                var prefab = _host != null && _host.Catalog != null ? _host.Catalog.popupProfile : null;
                if (prefab == null)
                {
                    _host?.PushPanel(UIPanelId.KingdomArmy, "프로필", clearBefore: false, isTabPanel: false);
                    return;
                }
                // Screen_Main의 형제 HUD보다 위에 그려지도록 전용 팝업 레이어에 생성한다.
                var parent = _host != null && _host.LayerPopups != null
                    ? _host.LayerPopups
                    : _view.transform as RectTransform;
                _profilePopup = UnityEngine.Object.Instantiate(prefab, parent, false);
                var prt = _profilePopup.transform as RectTransform;
                if (prt != null) { prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one; prt.offsetMin = Vector2.zero; prt.offsetMax = Vector2.zero; }
                _profileView = _profilePopup.GetComponent<ProfilePopupView>();
                if (_profileView != null)
                {
                    if (_profileView.closeButton != null) _profileView.closeButton.onClick.AddListener(CloseProfilePopup);
                    if (_profileView.backdrop != null) _profileView.backdrop.onClick.AddListener(CloseProfilePopup);
                    if (_profileView.powerButton != null) _profileView.powerButton.onClick.AddListener(OpenRankingPopup);
                }
            }
            PopulateProfilePopup();
            _profilePopup.SetActive(true);
            _profilePopup.transform.SetAsLastSibling();
            if (_profileView != null && _profileView.panel != null) UITween.PopIn(_profileView.panel);
        }

        private void CloseProfilePopup()
        {
            if (_profilePopup != null) _profilePopup.SetActive(false);
        }

        /// <summary>보유 데이터(닉네임/레벨)만 실제로 채우고 나머지는 프리팹 샘플값 유지(더미).</summary>
        private void PopulateProfilePopup()
        {
            if (_profileView == null) return;
            try
            {
                var um = UserManager.Instance;
                if (um != null)
                {
                    string nick = um.GetUserName();
                    int level = um.GetUserLevel();
                    long power = CombatPowerCalculator.CalculatePartyPowerV1(um.GetPlayers());
                    if (!string.IsNullOrWhiteSpace(nick) && _profileView.nameLabel != null) _profileView.nameLabel.text = nick;
                    if (_profileView.levelLabel != null) _profileView.levelLabel.text = level.ToString();
                    if (_profileView.kingdomLevelLabel != null) _profileView.kingdomLevelLabel.text = $"Lv. {level}";
                    if (_profileView.powerLabel != null) _profileView.powerLabel.text = power.ToString("N0");
                }
            }
            catch (Exception ex) { Debug.LogWarning($"PopulateProfilePopup: {ex.Message}"); }
        }

        // ── 전투력 랭킹 팝업 ──
        private GameObject _rankingPopup;
        private PowerRankingPopupView _rankingView;

        /// <summary>프로필을 닫고 현재 전투력 기준의 랭킹 팝업을 연다.</summary>
        private void OpenRankingPopup()
        {
            if (_view == null) return;
            CloseProfilePopup();

            if (_rankingPopup == null)
            {
                var prefab = _host != null && _host.Catalog != null ? _host.Catalog.popupRanking : null;
                if (prefab == null)
                {
                    Debug.LogWarning("[Ranking] Popup_Ranking 프리팹이 UIViewCatalog에 연결되지 않았습니다.");
                    return;
                }

                // 프로필과 동일하게 LayerPopups에 두어 MainActions/HUD보다 위에 표시한다.
                var parent = _host != null && _host.LayerPopups != null
                    ? _host.LayerPopups
                    : _view.transform as RectTransform;
                _rankingPopup = UnityEngine.Object.Instantiate(prefab, parent, false);
                var rankingRect = _rankingPopup.transform as RectTransform;
                if (rankingRect != null)
                {
                    rankingRect.anchorMin = Vector2.zero;
                    rankingRect.anchorMax = Vector2.one;
                    rankingRect.offsetMin = Vector2.zero;
                    rankingRect.offsetMax = Vector2.zero;
                }

                _rankingView = _rankingPopup.GetComponent<PowerRankingPopupView>();
                if (_rankingView != null)
                {
                    if (_rankingView.closeButton != null) _rankingView.closeButton.onClick.AddListener(CloseRankingPopup);
                    if (_rankingView.backdrop != null) _rankingView.backdrop.onClick.AddListener(CloseRankingPopup);
                }
            }

            _rankingPopup.SetActive(true);
            _rankingPopup.transform.SetAsLastSibling();
            PopulateRankingPopup();
            if (_rankingView != null && _rankingView.panel != null) UITween.PopIn(_rankingView.panel);
        }

        private void CloseRankingPopup()
        {
            if (_rankingPopup != null) _rankingPopup.SetActive(false);
        }

        /// <summary>팝업을 열 때마다 실제 전투력과 현재 순위를 다시 계산한다.</summary>
        private void PopulateRankingPopup()
        {
            if (_rankingView == null) return;

            var um = UserManager.Instance;
            string playerName = um != null ? um.GetUserName() : "Guest";
            long playerPower = um != null
                ? CombatPowerCalculator.CalculatePartyPowerV1(um.GetPlayers())
                : 0L;
            _rankingView.Populate(playerName, playerPower);
        }

        private void BindMenus()
        {
            if (_view.btnProfile != null)
            {
                _view.btnProfile.onClick.AddListener(() =>
                {
                    if (_currencyOpen) CloseCurrencyPopup();
                    if (_hamburgerOpen) CloseHamburgerMenu();
                    OpenProfilePopup();
                });
            }

            // 환생 — 상단바 프로필 옆 버튼 (구 우측 Hud_MainActions에서 이사)
            if (_view.btnReincarnation != null)
            {
                _view.btnReincarnation.onClick.AddListener(() =>
                {
                    if (_currencyOpen) CloseCurrencyPopup();
                    if (_hamburgerOpen) CloseHamburgerMenu();
                    ReincarnationPopupController.Show();
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

            // 신 스킬 도감 — HUD 모서리 버튼에서 이사 옴 (원형 버튼 리워크)
            if (_view.btnMenuDivineCollection != null)
            {
                // 신 스킬 시스템 비활성화 상태(bootstrap 에 매니저 미설치)면 진입점 자체를 숨긴다.
                // 매니저 존재 여부로 게이트 → 시스템 재활성화 시 이 코드는 손대지 않아도 된다.
                _view.btnMenuDivineCollection.gameObject.SetActive(DivineSkillManager.Instance != null);
                _view.btnMenuDivineCollection.onClick.AddListener(() =>
                {
                    CloseHamburgerMenu();
                    if (_currencyOpen) CloseCurrencyPopup();

                    var divine = DivineSkillManager.Instance;
                    if (divine == null || !divine.IsSystemUnlocked)
                    {
                        _host.ShowToast("신 스킬은 스테이지 3-10 클리어 후 해금됩니다.");
                        return;
                    }
                    DivineCollectionPopupController.Show();
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
