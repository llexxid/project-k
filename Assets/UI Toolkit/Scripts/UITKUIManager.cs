using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;
using Scripts.Core;
using KingdomIdle.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

using WalletModel = Scripts.Wallets.Wallet;

namespace KingdomIdle.UIToolkit
{
    [DefaultExecutionOrder(-1000)]
    public sealed class UITKUIManager : MonoBehaviour
    {
        public static UITKUIManager Instance { get; private set; }

        [Header("Scene References")]
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private PanelSettings panelSettings;
        [SerializeField] private StyleSheet commonStyle;

        [Header("UXML - Root")]
        [SerializeField] private VisualTreeAsset uiRootUxml;

        [Header("UXML - Screens")]
        [SerializeField] private VisualTreeAsset screenTitleUxml;
        [SerializeField] private VisualTreeAsset screenMainUxml;

        [Header("UXML - Panels (temporary)")]
        [SerializeField] private VisualTreeAsset panelPlaceholderUxml;

        [Header("UXML - Panels")]
        [SerializeField] private VisualTreeAsset panelGuideUxml;
        [SerializeField] private VisualTreeAsset panelGachaUxml;
        [SerializeField] private VisualTreeAsset panelKingdomArmyUxml;

        [Header("UXML - Overlays")]
        [SerializeField] private VisualTreeAsset overlayLoadingUxml;

        [Header("Behaviour")]
        [SerializeField] private bool dontDestroyOnLoad = true;

        private VisualElement _root;
        private VisualElement _layerScreens;
        private VisualElement _layerPanels;
        private VisualElement _layerPopups;
        private VisualElement _layerOverlays;

        private UIScreenId _activeScreenId;

        private struct PanelEntry
        {
            public UIPanelId Id;
            public bool IsTab;
            public VisualElement Ve;

            public PanelEntry(UIPanelId id, bool isTab, VisualElement ve)
            {
                Id = id;
                IsTab = isTab;
                Ve = ve;
            }
        }

        private readonly Stack<PanelEntry> _panelStack = new();

        private bool _hasActiveTabPanel;
        private UIPanelId _activeTabPanelId;

        private VisualElement _bottomBar;
        private float _bottomBarHeightPx = 190f;

        private VisualElement _loadingRoot;
        private Label _loadingLabel;
        private ProgressBar _loadingBar;

        private IVisualElementScheduledItem _pressHintBlink;
        private bool _requestedScene;

        // Currency
        private object _wallet;
        private Label _lblGold;
        private Label _lblAncientCoin;
        private VisualElement _popupCurrencies;
        private float _currencyPollTimer;
        private const float CurrencyPollInterval = 0.5f;

        private bool _currencyOpen;
        private Coroutine _currencyCo;
        private const float CurrencyAnimDuration = 0.18f;
        private const float CurrencyHiddenYOffset = -10f;

        // Hamburger
        private Button _btnHamburgerRight;
        private VisualElement _popupHamburger;
        private bool _hamburgerOpen;
        private Coroutine _hamburgerCo;
        private EventCallback<PointerDownEvent> _hamburgerOutsideCb;
        private const float HamburgerGapPx = 2f;
        private const float HamburgerAnimDuration = 0.18f;
        private const float HamburgerHiddenYOffset = -12f;

        // Settings modal
        private VisualElement _settingsOverlay;
        private VisualElement _settingsPanel;
        private Label _lblServer;
        private Label _lblVersion;
        private TextField _couponField;
        private Toggle _tglPowerSave;
        private Toggle _tglHideItem;
        private Toggle _tglDamageText;
        private Toggle _tglScreenShake;
        private Toggle _tglPush;
        private Toggle _tglNightPush;
        private Slider _sldVolume;
        private Button _btnMute;
        private bool _isMuted;

        // Toast
        private VisualElement _toastOverlay;
        private Label _toastLabel;
        private Coroutine _toastCo;

        // Guide
        private Label _lblGuideBadge;

        private const string PrefKeyVolume = "settings_masterVolume";
        private const string PrefKeyPowerSave = "settings_powerSave";
        private const string PrefKeyHideItem = "settings_hideItem";
        private const string PrefKeyDamageText = "settings_damageText";
        private const string PrefKeyScreenShake = "settings_screenShake";
        private const string PrefKeyPush = "settings_push";
        private const string PrefKeyNightPush = "settings_nightPush";

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (dontDestroyOnLoad) DontDestroyOnLoad(gameObject);

            if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
            if (uiDocument == null)
            {
                Debug.LogError("[UITKUIManager] UIDocument is missing.");
                enabled = false;
                return;
            }

            if (panelSettings != null) uiDocument.panelSettings = panelSettings;
            if (uiRootUxml != null) uiDocument.visualTreeAsset = uiRootUxml;

            _root = uiDocument.rootVisualElement;

            if (commonStyle != null)
                _root.styleSheets.Add(commonStyle);

            _layerScreens = _root.Q<VisualElement>("Layer_Screens");
            _layerPanels = _root.Q<VisualElement>("Layer_Panels");
            _layerPopups = _root.Q<VisualElement>("Layer_Popups");
            _layerOverlays = _root.Q<VisualElement>("Layer_Overlays");

            if (_layerScreens == null || _layerPanels == null || _layerPopups == null || _layerOverlays == null)
            {
                Debug.LogError("[UITKUIManager] Root UXML layer names mismatch. Check UIRoot.uxml.");
                enabled = false;
                return;
            }

            _layerPanels.pickingMode = PickingMode.Ignore;
            // NOTE: Layer_Popups is full-screen. If it stays pickable, it becomes an invisible click blocker.
            _layerPopups.pickingMode = PickingMode.Ignore;
            _layerOverlays.pickingMode = PickingMode.Ignore;

            BuildOverlays();
            EnsureToastOverlay();
        }

        private void Update()
        {
            if (IsBackPressedThisFrame())
                RequestBack();

            if (_activeScreenId == UIScreenId.Main && (_lblGold != null || _lblAncientCoin != null))
            {
                _currencyPollTimer += Time.unscaledDeltaTime;
                if (_currencyPollTimer >= CurrencyPollInterval)
                {
                    _currencyPollTimer = 0f;
                    RefreshTopCurrencyLabels();

                    if (_popupCurrencies != null && !_popupCurrencies.ClassListContains("hidden"))
                        RebuildCurrencyPopupContents();
                }
            }
        }

        public void ReplaceScreen(UIScreenId id, object payload = null, bool clearStacks = true)
        {
            StopPressHintBlink();
            _requestedScene = false;

            if (clearStacks)
                ClearPanels();

            CloseCurrencyPopupImmediate();
            CloseHamburgerMenuImmediate();

            _layerScreens.Clear();
            _layerPopups.Clear();

            var screen = CreateScreen(id);
            ForceFullScreen(screen);
            _layerScreens.Add(screen);

            _activeScreenId = id;
            _bottomBar = null;
            _bottomBarHeightPx = 190f;

            _wallet = null;
            _lblGold = null;
            _lblAncientCoin = null;
            _popupCurrencies = null;
            _currencyPollTimer = 0f;
            _currencyOpen = false;

            _btnHamburgerRight = null;
            _popupHamburger = null;
            _hamburgerOpen = false;
            UnregisterHamburgerOutsideClose();

            BindScreenEvents(id, screen);
        }

        public void PushPanel(UIPanelId id, object payload = null, bool clearBefore = false, bool isTabPanel = false)
        {
            if (clearBefore)
                ClearPanels();

            if (_panelStack.Count > 0)
                _panelStack.Peek().Ve.AddToClassList("hidden");

            var ve = CreatePanel(id, payload);
            ForceFullScreen(ve);

            ve.pickingMode = PickingMode.Ignore;

            _layerPanels.Add(ve);
            _panelStack.Push(new PanelEntry(id, isTabPanel, ve));

            BindPanelCommon(ve);
            RefreshActiveTabPanelState();
        }

        public void PopPanel()
        {
            if (_panelStack.Count == 0) return;

            var top = _panelStack.Pop();
            top.Ve.RemoveFromHierarchy();

            if (_panelStack.Count > 0)
                _panelStack.Peek().Ve.RemoveFromClassList("hidden");

            RefreshActiveTabPanelState();
        }

        public void ClearPanels()
        {
            while (_panelStack.Count > 0)
            {
                var ve = _panelStack.Pop().Ve;
                ve.RemoveFromHierarchy();
            }

            _layerPanels.Clear();
            _hasActiveTabPanel = false;
            _activeTabPanelId = default;
        }

        public void SetLoading(bool visible, string message = "Loading...")
        {
            if (_loadingRoot == null) return;

            if (visible)
            {
                if (_loadingLabel != null) _loadingLabel.text = message;
                _loadingRoot.RemoveFromClassList("hidden");
                if (_loadingBar != null) _loadingBar.value = 0;
            }
            else
            {
                _loadingRoot.AddToClassList("hidden");
            }
        }

        public void SetLoadingProgress(float normalized01)
        {
            if (_loadingBar == null) return;
            _loadingBar.value = Mathf.Clamp01(normalized01) * 100f;
        }

        public void RequestBack()
        {
            if (_settingsOverlay != null && !_settingsOverlay.ClassListContains("hidden"))
            {
                CloseSettings();
                return;
            }

            if (_currencyOpen)
            {
                CloseCurrencyPopup();
                return;
            }

            if (_hamburgerOpen)
            {
                CloseHamburgerMenu();
                return;
            }

            if (_panelStack.Count > 0)
            {
                PopPanel();
                return;
            }

            if (_activeScreenId == UIScreenId.Main && GameManager.Instance != null)
            {
                GameManager.Instance.LoadAsyncScene(eSceneType.title);
            }
        }

        private static bool IsBackPressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }

        private void BuildOverlays()
        {
            if (overlayLoadingUxml == null) return;

            _loadingRoot = overlayLoadingUxml.CloneTree();
            ForceFullScreen(_loadingRoot);
            _layerOverlays.Add(_loadingRoot);

            _loadingLabel = _loadingRoot.Q<Label>("LblLoading");
            _loadingBar = _loadingRoot.Q<ProgressBar>("PbLoading");

            _loadingRoot.AddToClassList("hidden");
        }

        private void EnsureToastOverlay()
        {
            if (_toastOverlay != null) return;

            _toastOverlay = new VisualElement();
            _toastOverlay.AddToClassList("toast-overlay");
            ForceFullScreen(_toastOverlay);
            _toastOverlay.pickingMode = PickingMode.Ignore;

            var box = new VisualElement();
            box.AddToClassList("toast-box");

            _toastLabel = new Label("");
            _toastLabel.AddToClassList("toast-text");

            box.Add(_toastLabel);
            _toastOverlay.Add(box);

            _toastOverlay.AddToClassList("hidden");
            _layerOverlays.Add(_toastOverlay);
        }

        public void ShowToast(string message)
        {
            if (_toastOverlay == null) EnsureToastOverlay();

            _toastLabel.text = message;
            _toastOverlay.RemoveFromClassList("hidden");
            _toastOverlay.BringToFront();

            if (_toastCo != null) StopCoroutine(_toastCo);
            _toastCo = StartCoroutine(HideToastAfter(1.5f));
        }

        private IEnumerator HideToastAfter(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            if (_toastOverlay != null) _toastOverlay.AddToClassList("hidden");
            _toastCo = null;
        }

        private static void ForceFullScreen(VisualElement ve)
        {
            if (ve == null) return;
            ve.style.position = Position.Absolute;
            ve.style.left = 0;
            ve.style.right = 0;
            ve.style.top = 0;
            ve.style.bottom = 0;
        }

        private VisualElement CreateScreen(UIScreenId id)
        {
            switch (id)
            {
                case UIScreenId.Title:
                    return screenTitleUxml != null ? screenTitleUxml.CloneTree() : new Label("Missing Screen_Title UXML");
                case UIScreenId.Main:
                    return screenMainUxml != null ? screenMainUxml.CloneTree() : new Label("Missing Screen_Main UXML");
                case UIScreenId.Dungeon:
                    return new Label("Dungeon screen is not implemented yet.");
                default:
                    return new Label($"Unhandled screen: {id}");
            }
        }

        private VisualElement CreatePanel(UIPanelId id, object payload)
        {
            if (id == UIPanelId.Guide)
            {
                VisualElement guideVe = panelGuideUxml != null
                    ? panelGuideUxml.CloneTree()
                    : new Label("Missing Panel_Guide UXML");
                UITKGuidePanelController.Populate(guideVe, onProgressChanged: RefreshGuideBadge);
                return guideVe;
            }

            if (id == UIPanelId.Gacha)
            {
                VisualElement gachaVe = panelGachaUxml != null
                    ? panelGachaUxml.CloneTree()
                    : new Label("Missing Panel_Gacha UXML");
                UITKGachaPanelController.Populate(gachaVe);
                return gachaVe;
            }

            if (id == UIPanelId.KingdomArmy)
            {
                VisualElement armyVe = panelKingdomArmyUxml != null
                    ? panelKingdomArmyUxml.CloneTree()
                    : new Label("Missing Panel_KingdomArmy UXML");
                UITKKingdomArmyPanelController.Populate(armyVe);
                return armyVe;
            }

            // FIX: 삼항연산 + var 타입 추론 실패(TemplateContainer vs Label) 방지
            VisualElement ve = panelPlaceholderUxml != null
                ? panelPlaceholderUxml.CloneTree()
                : new Label("Missing Panel_Placeholder UXML");

            ApplyPlaceholderPanelTitle(ve, id, payload);
            return ve;
        }

        private void BindScreenEvents(UIScreenId id, VisualElement screenRoot)
        {
            if (screenRoot == null) return;

            switch (id)
            {
                case UIScreenId.Title:
                    BindTitle(screenRoot);
                    break;
                case UIScreenId.Main:
                    BindMain(screenRoot);
                    break;
            }
        }

        private void BindTitle(VisualElement root)
        {
            root.pickingMode = PickingMode.Position;

            var bgCatcher = root.Q<VisualElement>("BgClickCatcher");
            var btnLogin = root.Q<Button>("BtnLogin");
            var popupLogin = root.Q<VisualElement>("PopupLogin");
            var popupLoginBox = root.Q<VisualElement>("PopupLoginBox");
            var pressHint = root.Q<Label>("LblPressHint");

            if (btnLogin != null && popupLogin != null)
                btnLogin.clicked += () => popupLogin.RemoveFromClassList("hidden");

            if (pressHint != null)
                StartPressHintBlink(pressHint);

            if (bgCatcher != null)
            {
                ForceFullScreen(bgCatcher);
                bgCatcher.pickingMode = PickingMode.Position;
                bgCatcher.RegisterCallback<PointerUpEvent>(_ =>
                {
                    if (popupLogin != null && !popupLogin.ClassListContains("hidden")) return;
                    LoadMainOnce();
                }, TrickleDown.TrickleDown);
            }

            if (popupLogin != null)
            {
                popupLogin.pickingMode = PickingMode.Position;
                popupLogin.RegisterCallback<PointerUpEvent>(evt =>
                {
                    if (popupLogin.ClassListContains("hidden")) return;

                    var targetVe = evt.target as VisualElement;
                    if (targetVe == null) return;

                    if (IsInside(targetVe, popupLoginBox)) return;

                    popupLogin.AddToClassList("hidden");
                    evt.StopPropagation();
                }, TrickleDown.TrickleDown);
            }

            if (popupLoginBox != null)
            {
                popupLoginBox.pickingMode = PickingMode.Position;
                popupLoginBox.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation(), TrickleDown.TrickleDown);
                popupLoginBox.RegisterCallback<PointerUpEvent>(evt => evt.StopPropagation(), TrickleDown.TrickleDown);
            }
        }

        private void BindMain(VisualElement root)
        {
            _bottomBar = root.Q<VisualElement>("BottomBar");
            if (_bottomBar != null)
            {
                _bottomBar.RegisterCallback<GeometryChangedEvent>(evt =>
                {
                    var h = evt.newRect.height;
                    if (h > 1f)
                    {
                        _bottomBarHeightPx = h;
                        UpdateAllPanelOffsets();
                    }
                });

                if (_bottomBar.resolvedStyle.height > 1f)
                    _bottomBarHeightPx = _bottomBar.resolvedStyle.height;
            }

            BindTab(root.Q<Button>("BtnDevelopment"), UIPanelId.Development, "developmentPanel");
            BindTab(root.Q<Button>("BtnKingdomArmy"), UIPanelId.KingdomArmy, "kingdomArmyPanel");
            BindTab(root.Q<Button>("BtnGacha"), UIPanelId.Gacha, "gachaPanel");
            BindTab(root.Q<Button>("BtnStore"), UIPanelId.Store, "storePanel");
            BindTab(root.Q<Button>("BtnDungeon"), UIPanelId.Dungeon, "dungeonPanel");

            var bCurrency = root.Q<Button>("BtnCurrency");
            _popupCurrencies = root.Q<VisualElement>("PopupCurrencies");
            _lblGold = root.Q<Label>("LblGold");
            _lblAncientCoin = root.Q<Label>("LblAncientCoin");
            _wallet = FindAnyWallet();
            RefreshTopCurrencyLabels();

            if (bCurrency != null && _popupCurrencies != null)
            {
                bCurrency.clicked += () =>
                {
                    RefreshTopCurrencyLabels();
                    RebuildCurrencyPopupContents();

                    if (_hamburgerOpen)
                        CloseHamburgerMenuImmediate();

                    ToggleCurrencyPopup();
                };
            }

            _btnHamburgerRight = root.Q<Button>("BtnHamburgerRight");
            _popupHamburger = root.Q<VisualElement>("PopupHamburger");
            if (_btnHamburgerRight != null && _popupHamburger != null)
            {
                _popupHamburger.style.position = Position.Absolute;
                _btnHamburgerRight.clicked += ToggleHamburgerMenu;
                _btnHamburgerRight.RegisterCallback<GeometryChangedEvent>(_ => AlignHamburgerPopup());
                _popupHamburger.RegisterCallback<GeometryChangedEvent>(_ => AlignHamburgerPopup());
            }

            var btnProfile = root.Q<Button>("BtnProfileBlank");
            if (btnProfile != null)
            {
                btnProfile.clicked += () =>
                {
                    if (_currencyOpen) CloseCurrencyPopup();
                    if (_hamburgerOpen) CloseHamburgerMenu();

                    if (Enum.TryParse("Profile", true, out UIPanelId profileId))
                        PushPanel(profileId, "프로필", clearBefore: false, isTabPanel: false);
                    else
                        PushPanel(UIPanelId.KingdomArmy, "프로필", clearBefore: false, isTabPanel: false);
                };
            }

            var bMenuSettings = root.Q<Button>("BtnMenuSettings");
            if (bMenuSettings != null)
            {
                bMenuSettings.clicked += () =>
                {
                    CloseHamburgerMenu();
                    if (_currencyOpen) CloseCurrencyPopup();
                    OpenSettings();
                };
            }

            var bMenuNotice = root.Q<Button>("BtnMenuNotice");
            if (bMenuNotice != null) bMenuNotice.clicked += () => ShowToast("현재는 지원하지 않는 기능입니다.");

            var bMenuMail = root.Q<Button>("BtnMenuMail");
            if (bMenuMail != null) bMenuMail.clicked += () => ShowToast("현재는 지원하지 않는 기능입니다.");

            // Guide 버튼 (좌측 상단)
            _lblGuideBadge = root.Q<Label>("LblGuideBadge");
            var btnGuide = root.Q<Button>("BtnGuide");
            if (btnGuide != null)
            {
                btnGuide.clicked += () =>
                {
                    if (_currencyOpen) CloseCurrencyPopupImmediate();
                    if (_hamburgerOpen) CloseHamburgerMenuImmediate();
                    PushPanel(UIPanelId.Guide, null, clearBefore: false, isTabPanel: false);
                };
            }
            RefreshGuideBadge();
        }

        public void RefreshGuideBadge()
        {
            if (_lblGuideBadge == null) return;
            int count = TutorialManager.Instance != null ? TutorialManager.Instance.GetIncompleteCount() : 0;
            if (count > 0)
            {
                _lblGuideBadge.text = count.ToString();
                _lblGuideBadge.RemoveFromClassList("hidden");
            }
            else
            {
                _lblGuideBadge.AddToClassList("hidden");
            }
        }

        private void BindTab(Button btn, UIPanelId panelId, object panelName)
        {
            if (btn == null) return;
            btn.clicked += () => OnTabPressed(panelId, panelName);
        }

        private void OnTabPressed(UIPanelId panelId, object panelName)
        {
            if (_hasActiveTabPanel && _activeTabPanelId.Equals(panelId))
            {
                ClearPanels();
                return;
            }

            ClearPanels();
            PushPanel(panelId, panelName, clearBefore: false, isTabPanel: true);
        }

        private void BindPanelCommon(VisualElement panelRoot)
        {
            if (panelRoot == null) return;

            var realRoot = panelRoot.Q<VisualElement>("PanelRoot");
            if (realRoot != null)
                realRoot.pickingMode = PickingMode.Ignore;

            var backdrop = panelRoot.Q<VisualElement>("Backdrop");
            if (backdrop != null)
            {
                backdrop.pickingMode = PickingMode.Position;
                backdrop.RegisterCallback<PointerUpEvent>(_ =>
                {
                    if (_panelStack.Count > 0 && _panelStack.Peek().Ve == panelRoot)
                        PopPanel();
                }, TrickleDown.TrickleDown);
            }

            var sheet = panelRoot.Q<VisualElement>("Sheet");
            if (sheet != null)
            {
                sheet.pickingMode = PickingMode.Position;
                sheet.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation(), TrickleDown.TrickleDown);
                sheet.RegisterCallback<PointerUpEvent>(evt => evt.StopPropagation(), TrickleDown.TrickleDown);
            }

            var closeBtn = panelRoot.Q<Button>("BtnPanelClose");
            if (closeBtn != null)
                closeBtn.clicked += PopPanel;

            ApplyPanelOffsets(panelRoot);
        }

        private void ApplyPanelOffsets(VisualElement panelRoot)
        {
            if (panelRoot == null) return;

            var barH = GetBottomBarHeightPx();

            var backdrop = panelRoot.Q<VisualElement>("Backdrop");
            if (backdrop != null)
                backdrop.style.bottom = barH;

            var sheet = panelRoot.Q<VisualElement>("Sheet");
            if (sheet != null)
                sheet.style.bottom = barH;
        }

        private void UpdateAllPanelOffsets()
        {
            foreach (var p in _panelStack)
                ApplyPanelOffsets(p.Ve);
        }

        private float GetBottomBarHeightPx()
        {
            if (_bottomBar != null && _bottomBar.resolvedStyle.height > 1f)
                return _bottomBar.resolvedStyle.height;

            return _bottomBarHeightPx > 1f ? _bottomBarHeightPx : 190f;
        }

        private void RefreshActiveTabPanelState()
        {
            _hasActiveTabPanel = false;
            _activeTabPanelId = default;

            foreach (var entry in _panelStack)
            {
                if (entry.IsTab)
                {
                    _hasActiveTabPanel = true;
                    _activeTabPanelId = entry.Id;
                    return;
                }
            }
        }

        private void LoadMainOnce()
        {
            if (_requestedScene) return;
            _requestedScene = true;

            if (GameManager.Instance != null)
                GameManager.Instance.LoadAsyncScene(eSceneType.main);
        }

        private static bool IsInside(VisualElement target, VisualElement container)
        {
            if (target == null || container == null) return false;

            var v = target;
            while (v != null)
            {
                if (v == container) return true;
                v = v.parent;
            }
            return false;
        }

        private void StartPressHintBlink(Label label)
        {
            StopPressHintBlink();

            _pressHintBlink = label.schedule.Execute(() =>
            {
                float t = Time.unscaledTime * 2.2f;
                float a = 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(t));
                label.style.opacity = a;
            }).Every(16);
        }

        private void StopPressHintBlink()
        {
            if (_pressHintBlink != null)
            {
                _pressHintBlink.Pause();
                _pressHintBlink = null;
            }
        }

        private object FindAnyWallet()
        {
            var gm = GameManager.Instance;
            var w = TryFindWalletModel(gm, 3);
            if (w != null) return w;

            var behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < behaviours.Length; i++)
            {
                var b = behaviours[i];
                w = TryFindWalletModel(b, 2);
                if (w != null) return w;
            }

            for (int i = 0; i < behaviours.Length; i++)
            {
                var b = behaviours[i];
                if (b == null) continue;
                if (IsWalletLikeProvider(b.GetType()))
                    return b;
            }

            return null;
        }

        private static WalletModel TryFindWalletModel(object obj, int depth)
        {
            if (obj == null) return null;
            if (obj is WalletModel w0) return w0;
            if (depth <= 0) return null;

            var t = obj.GetType();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            var fields = t.GetFields(flags);
            for (int i = 0; i < fields.Length; i++)
            {
                var f = fields[i];
                if (!typeof(WalletModel).IsAssignableFrom(f.FieldType)) continue;
                try
                {
                    var v = f.GetValue(obj) as WalletModel;
                    if (v != null) return v;
                }
                catch { }
            }

            var props = t.GetProperties(flags);
            for (int i = 0; i < props.Length; i++)
            {
                var p = props[i];
                if (!p.CanRead) continue;
                if (p.GetIndexParameters().Length != 0) continue;
                if (!typeof(WalletModel).IsAssignableFrom(p.PropertyType)) continue;

                try
                {
                    var v = p.GetValue(obj, null) as WalletModel;
                    if (v != null) return v;
                }
                catch { }
            }

            for (int i = 0; i < fields.Length; i++)
            {
                var f = fields[i];
                object v;
                try { v = f.GetValue(obj); } catch { continue; }
                if (v == null) continue;
                if (v is string) continue;
                if (v.GetType().IsValueType) continue;

                var w = TryFindWalletModel(v, depth - 1);
                if (w != null) return w;
            }

            for (int i = 0; i < props.Length; i++)
            {
                var p = props[i];
                if (!p.CanRead) continue;
                if (p.GetIndexParameters().Length != 0) continue;

                object v;
                try { v = p.GetValue(obj, null); } catch { continue; }
                if (v == null) continue;
                if (v is string) continue;
                if (v.GetType().IsValueType) continue;

                var w = TryFindWalletModel(v, depth - 1);
                if (w != null) return w;
            }

            return null;
        }

        private static bool IsWalletLikeProvider(Type t)
        {
            if (t == null) return false;
            if (t.Name.IndexOf("wallet", StringComparison.OrdinalIgnoreCase) < 0) return false;

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var m = t.GetMethod(
                "TryGetAmount",
                flags,
                null,
                new[] { typeof(eCurrency), typeof(int).MakeByRefType() },
                null
            );
            return m != null && m.ReturnType == typeof(bool);
        }

        private static bool TryGetAmountFromWallet(object walletObj, eCurrency currency, out int amount)
        {
            amount = 0;
            if (walletObj == null) return false;

            if (walletObj is WalletModel w)
                return w.TryGetAmount(currency, out amount);

            var t = walletObj.GetType();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var m = t.GetMethod(
                "TryGetAmount",
                flags,
                null,
                new[] { typeof(eCurrency), typeof(int).MakeByRefType() },
                null
            );

            if (m == null || m.ReturnType != typeof(bool)) return false;

            object[] args = new object[] { currency, 0 };
            try
            {
                var ok = (bool)m.Invoke(walletObj, args);
                amount = (int)args[1];
                return ok;
            }
            catch
            {
                return false;
            }
        }

        private void RefreshTopCurrencyLabels()
        {
            if (_wallet == null) _wallet = FindAnyWallet();

            if (_lblGold != null) _lblGold.text = GetCurrencyText(eCurrency.Gold);
            if (_lblAncientCoin != null) _lblAncientCoin.text = GetCurrencyText(eCurrency.AncientCoin);
        }

        private void RebuildCurrencyPopupContents()
        {
            if (_popupCurrencies == null) return;

            _popupCurrencies.Clear();

            var title = new Label("재화 상세");
            title.AddToClassList("dropdown-title");
            _popupCurrencies.Add(title);

            var values = (eCurrency[])Enum.GetValues(typeof(eCurrency));
            foreach (var c in values)
            {
                if (c == eCurrency.Gold || c == eCurrency.AncientCoin || c == eCurrency.ClassFragment)
                    continue;

                var line = new Label($"{c}: {GetCurrencyText(c)}");
                line.AddToClassList("dropdown-item");
                _popupCurrencies.Add(line);
            }
        }

        private string GetCurrencyText(eCurrency currency)
        {
            if (_wallet == null) return "null";
            if (TryGetAmountFromWallet(_wallet, currency, out int amount))
                return amount.ToString("N0");
            return "null";
        }

        private void ToggleCurrencyPopup()
        {
            if (_popupCurrencies == null) return;
            if (_currencyOpen) CloseCurrencyPopup();
            else OpenCurrencyPopup();
        }

        private void OpenCurrencyPopup()
        {
            if (_popupCurrencies == null) return;
            _popupCurrencies.RemoveFromClassList("hidden");
            _popupCurrencies.pickingMode = PickingMode.Position;
            _popupCurrencies.BringToFront();

            StopCurrencyAnim();
            _currencyCo = StartCoroutine(AnimateCurrency(open: true));
            _currencyOpen = true;
        }

        private void CloseCurrencyPopup()
        {
            if (_popupCurrencies == null) return;
            if (!_currencyOpen && _popupCurrencies.ClassListContains("hidden")) return;

            StopCurrencyAnim();
            _currencyCo = StartCoroutine(AnimateCurrency(open: false));
            _currencyOpen = false;
        }

        private void CloseCurrencyPopupImmediate()
        {
            if (_popupCurrencies == null) return;
            StopCurrencyAnim();
            _popupCurrencies.AddToClassList("hidden");
            _popupCurrencies.pickingMode = PickingMode.Ignore;
            _popupCurrencies.style.opacity = 1f;
            _popupCurrencies.style.translate = new Translate(0, 0, 0);
            _currencyOpen = false;
        }

        private void StopCurrencyAnim()
        {
            if (_currencyCo != null)
            {
                StopCoroutine(_currencyCo);
                _currencyCo = null;
            }
        }

        private IEnumerator AnimateCurrency(bool open)
        {
            float fromY = open ? CurrencyHiddenYOffset : 0f;
            float toY = open ? 0f : CurrencyHiddenYOffset;

            float fromA = open ? 0f : 1f;
            float toA = open ? 1f : 0f;

            float t = 0f;
            while (t < CurrencyAnimDuration)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / CurrencyAnimDuration);
                float eased = u * u * (3f - 2f * u);

                ApplyCurrencyVisual(
                    Mathf.Lerp(fromY, toY, eased),
                    Mathf.Lerp(fromA, toA, eased)
                );

                yield return null;
            }

            ApplyCurrencyVisual(toY, toA);

            if (!open && _popupCurrencies != null)
            {
                _popupCurrencies.AddToClassList("hidden");
                _popupCurrencies.pickingMode = PickingMode.Ignore;
            }

            _currencyCo = null;
        }

        private void ApplyCurrencyVisual(float yOffset, float opacity)
        {
            if (_popupCurrencies == null) return;
            _popupCurrencies.style.opacity = opacity;
            _popupCurrencies.style.translate = new Translate(
                new Length(0, LengthUnit.Pixel),
                new Length(yOffset, LengthUnit.Pixel),
                0
            );
        }

        private void ToggleHamburgerMenu()
        {
            if (_popupHamburger == null || _btnHamburgerRight == null) return;
            if (_hamburgerOpen) CloseHamburgerMenu();
            else OpenHamburgerMenu();
        }

        private void OpenHamburgerMenu()
        {
            if (_popupHamburger == null) return;

            if (_currencyOpen)
                CloseCurrencyPopupImmediate();

            _popupHamburger.RemoveFromClassList("hidden");
            _popupHamburger.pickingMode = PickingMode.Position;
            _popupHamburger.BringToFront();
            AlignHamburgerPopup();

            RegisterHamburgerOutsideClose();

            if (_hamburgerCo != null) StopCoroutine(_hamburgerCo);
            _hamburgerCo = StartCoroutine(AnimateHamburger(open: true));
            _hamburgerOpen = true;
        }

        private void CloseHamburgerMenu()
        {
            if (_popupHamburger == null) return;
            if (!_hamburgerOpen && _popupHamburger.ClassListContains("hidden")) return;

            UnregisterHamburgerOutsideClose();

            if (_hamburgerCo != null) StopCoroutine(_hamburgerCo);
            _hamburgerCo = StartCoroutine(AnimateHamburger(open: false));
            _hamburgerOpen = false;
        }

        private void CloseHamburgerMenuImmediate()
        {
            if (_popupHamburger == null) return;

            UnregisterHamburgerOutsideClose();

            if (_hamburgerCo != null)
            {
                StopCoroutine(_hamburgerCo);
                _hamburgerCo = null;
            }

            _popupHamburger.AddToClassList("hidden");
            _popupHamburger.pickingMode = PickingMode.Ignore;
            _popupHamburger.style.opacity = 1f;
            _popupHamburger.style.translate = new Translate(0, 0, 0);

            if (_btnHamburgerRight != null)
            {
                _btnHamburgerRight.style.transformOrigin = new TransformOrigin(50, 50, 0);
                _btnHamburgerRight.style.rotate = new Rotate(new Angle(0, AngleUnit.Degree));
            }

            _hamburgerOpen = false;
        }

        private IEnumerator AnimateHamburger(bool open)
        {
            float fromAngle = open ? 0f : 90f;
            float toAngle = open ? 90f : 0f;

            float fromY = open ? HamburgerHiddenYOffset : 0f;
            float toY = open ? 0f : HamburgerHiddenYOffset;

            float fromA = open ? 0f : 1f;
            float toA = open ? 1f : 0f;

            float t = 0f;
            while (t < HamburgerAnimDuration)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / HamburgerAnimDuration);
                float eased = u * u * (3f - 2f * u);

                ApplyHamburgerVisual(
                    Mathf.Lerp(fromAngle, toAngle, eased),
                    Mathf.Lerp(fromY, toY, eased),
                    Mathf.Lerp(fromA, toA, eased)
                );

                yield return null;
            }

            ApplyHamburgerVisual(toAngle, toY, toA);

            if (!open && _popupHamburger != null)
            {
                _popupHamburger.AddToClassList("hidden");
                _popupHamburger.pickingMode = PickingMode.Ignore;
            }

            _hamburgerCo = null;
        }

        private void ApplyHamburgerVisual(float angleDeg, float yOffset, float opacity)
        {
            if (_btnHamburgerRight != null)
            {
                _btnHamburgerRight.style.transformOrigin = new TransformOrigin(50, 50, 0);
                _btnHamburgerRight.style.rotate = new Rotate(new Angle(angleDeg, AngleUnit.Degree));
            }

            if (_popupHamburger != null)
            {
                _popupHamburger.style.opacity = opacity;
                _popupHamburger.style.translate = new Translate(
                    new Length(0, LengthUnit.Pixel),
                    new Length(yOffset, LengthUnit.Pixel),
                    0
                );
            }
        }

        private void AlignHamburgerPopup()
        {
            if (_btnHamburgerRight == null || _popupHamburger == null) return;

            var parent = _popupHamburger.parent;
            if (parent == null) return;

            Vector2 btnRightTop = parent.WorldToLocal(new Vector2(_btnHamburgerRight.worldBound.xMax, _btnHamburgerRight.worldBound.yMin));
            Vector2 btnRightBottom = parent.WorldToLocal(new Vector2(_btnHamburgerRight.worldBound.xMax, _btnHamburgerRight.worldBound.yMax));

            float popupW = _popupHamburger.resolvedStyle.width;
            if (popupW <= 1f) popupW = 90f;

            float left = btnRightTop.x - popupW;
            float top = btnRightBottom.y + HamburgerGapPx;

            _popupHamburger.style.right = new StyleLength(StyleKeyword.Auto);
            _popupHamburger.style.left = left;
            _popupHamburger.style.top = top;
        }

        private void RegisterHamburgerOutsideClose()
        {
            if (_hamburgerOutsideCb != null) return;

            _hamburgerOutsideCb = evt =>
            {
                if (!_hamburgerOpen) return;

                var target = evt.target as VisualElement;
                if (target == null) return;

                if (IsInside(target, _btnHamburgerRight)) return;
                if (IsInside(target, _popupHamburger)) return;

                CloseHamburgerMenu();
            };

            _root.RegisterCallback(_hamburgerOutsideCb, TrickleDown.TrickleDown);
        }

        private void UnregisterHamburgerOutsideClose()
        {
            if (_hamburgerOutsideCb == null) return;
            _root.UnregisterCallback(_hamburgerOutsideCb, TrickleDown.TrickleDown);
            _hamburgerOutsideCb = null;
        }

        private void OpenSettings()
        {
            EnsureSettingsOverlay();
            LoadSettingsToUI();

            _settingsOverlay.RemoveFromClassList("hidden");
            _settingsOverlay.BringToFront();
        }

        private void CloseSettings()
        {
            if (_settingsOverlay == null) return;
            _settingsOverlay.AddToClassList("hidden");
        }

        private void EnsureSettingsOverlay()
        {
            if (_settingsOverlay != null) return;

            _settingsOverlay = new VisualElement();
            _settingsOverlay.AddToClassList("settings-overlay");
            ForceFullScreen(_settingsOverlay);
            _settingsOverlay.pickingMode = PickingMode.Position;

            var hint = new Label("터치해서 닫기");
            hint.AddToClassList("settings-hint-close");
            _settingsOverlay.Add(hint);

            _settingsPanel = new VisualElement();
            _settingsPanel.AddToClassList("settings-panel");
            _settingsPanel.pickingMode = PickingMode.Position;
            _settingsPanel.RegisterCallback<PointerDownEvent>(e => e.StopPropagation(), TrickleDown.TrickleDown);

            var titleBar = new VisualElement();
            titleBar.AddToClassList("settings-titlebar");
            var title = new Label("환경설정");
            title.AddToClassList("settings-title");
            titleBar.Add(title);

            _settingsPanel.Add(titleBar);

            var serverRow = new VisualElement();
            serverRow.AddToClassList("settings-subrow");

            _lblServer = new Label("현재 서버: null");
            _lblServer.AddToClassList("settings-version-label");

            string ver = string.IsNullOrWhiteSpace(Application.version) ? "0.0.1" : Application.version;
            _lblVersion = new Label($"Version {ver}");
            _lblVersion.AddToClassList("settings-version-label");

            serverRow.Add(_lblServer);
            serverRow.Add(_lblVersion);
            _settingsPanel.Add(serverRow);

            var gpRow = new VisualElement();
            gpRow.AddToClassList("settings-subrow");

            var btnGoogle = new Button(() => ShowToast("현재는 지원하지 않는 기능입니다."));
            btnGoogle.text = "Google Play 연동됨";
            btnGoogle.AddToClassList("settings-chip-btn");

            var versionBox = new VisualElement();
            versionBox.AddToClassList("settings-version");
            var versionLabel = new Label(_lblVersion.text);
            versionLabel.AddToClassList("settings-version-label");
            versionBox.Add(versionLabel);

            gpRow.Add(btnGoogle);
            gpRow.Add(versionBox);
            _settingsPanel.Add(gpRow);

            var couponRow = new VisualElement();
            couponRow.AddToClassList("settings-coupon-row");

            var couponTag = new VisualElement();
            couponTag.AddToClassList("settings-coupon-tag");
            var couponTagLbl = new Label("※ 쿠폰 입력");
            couponTagLbl.AddToClassList("settings-coupon-tag-label");
            couponTag.Add(couponTagLbl);

            var fieldWrap = new VisualElement();
            fieldWrap.AddToClassList("settings-coupon-field-wrap");

            _couponField = new TextField();
            _couponField.AddToClassList("settings-coupon-field");
            fieldWrap.Add(_couponField);

            var btnCoupon = new Button(() => ShowToast("현재는 지원하지 않는 기능입니다."));
            btnCoupon.text = "입력";
            btnCoupon.AddToClassList("settings-coupon-btn");

            couponRow.Add(couponTag);
            couponRow.Add(fieldWrap);
            couponRow.Add(btnCoupon);
            _settingsPanel.Add(couponRow);

            var grid = new VisualElement();
            grid.AddToClassList("settings-grid");

            var colL = new VisualElement();
            colL.AddToClassList("settings-col");
            var colR = new VisualElement();
            colR.AddToClassList("settings-col");

            _tglPowerSave = new Toggle("절전 모드");
            _tglDamageText = new Toggle("데미지 문구 출력");
            _tglPush = new Toggle("푸시 동의");

            _tglHideItem = new Toggle("아이템 획득 숨기기");
            _tglScreenShake = new Toggle("화면 흔들림 켜기");
            _tglNightPush = new Toggle("[야간] 푸시 동의");

            colL.Add(_tglPowerSave);
            colL.Add(_tglDamageText);
            colL.Add(_tglPush);

            colR.Add(_tglHideItem);
            colR.Add(_tglScreenShake);
            colR.Add(_tglNightPush);

            grid.Add(colL);
            grid.Add(colR);
            _settingsPanel.Add(grid);

            var volRow = new VisualElement();
            volRow.AddToClassList("settings-volume-row");

            var volLbl = new Label("전체 음량");
            volLbl.AddToClassList("settings-volume-label");

            _btnMute = new Button(() =>
            {
                _isMuted = !_isMuted;
                ApplyMuteVisual();
                ApplyVolumeToSystem();
            });
            _btnMute.text = "음소거";
            _btnMute.AddToClassList("settings-mute-btn");

            _sldVolume = new Slider(0f, 1f);
            _sldVolume.AddToClassList("settings-slider");
            _sldVolume.RegisterValueChangedCallback(evt =>
            {
                if (_isMuted) return;
                AudioListener.volume = evt.newValue;
            });

            volRow.Add(volLbl);
            volRow.Add(_btnMute);
            volRow.Add(_sldVolume);
            _settingsPanel.Add(volRow);

            var inquiry = new VisualElement();
            inquiry.AddToClassList("settings-inquiry");
            var inquiryLbl = new Label("문의: ");
            inquiryLbl.AddToClassList("settings-inquiry-label");
            inquiry.Add(inquiryLbl);
            _settingsPanel.Add(inquiry);

            var btnCafe = new Button(() => ShowToast("현재는 지원하지 않는 기능입니다."));
            btnCafe.text = "네이버 공식 카페 바로가기";
            btnCafe.AddToClassList("settings-wide-btn");
            _settingsPanel.Add(btnCafe);

            var btnWithdraw = new Button(() => ShowToast("현재는 지원하지 않는 기능입니다."));
            btnWithdraw.text = "회원 탈퇴 & 계정 삭제";
            btnWithdraw.AddToClassList("settings-danger-btn");
            _settingsPanel.Add(btnWithdraw);

            var bottomRow = new VisualElement();
            bottomRow.AddToClassList("settings-bottom-row");

            var btnSave = new Button(() =>
            {
                SaveSettingsFromUI();
                ShowToast("저장되었습니다.");
            });
            btnSave.text = "저장하기";
            btnSave.AddToClassList("settings-bottom-btn");

            var btnSaveClose = new Button(() =>
            {
                SaveSettingsFromUI();
                CloseSettings();
            });
            btnSaveClose.text = "저장 후 닫기";
            btnSaveClose.AddToClassList("settings-bottom-btn");

            bottomRow.Add(btnSave);
            bottomRow.Add(btnSaveClose);
            _settingsPanel.Add(bottomRow);

            _settingsOverlay.Add(_settingsPanel);

            _settingsOverlay.RegisterCallback<PointerDownEvent>(evt =>
            {
                var target = evt.target as VisualElement;
                if (target != null && IsInside(target, _settingsPanel)) return;
                CloseSettings();
            }, TrickleDown.TrickleDown);

            _settingsOverlay.AddToClassList("hidden");
            _layerOverlays.Add(_settingsOverlay);
        }

        private void ApplyVolumeToSystem()
        {
            if (_isMuted) AudioListener.volume = 0f;
            else if (_sldVolume != null) AudioListener.volume = _sldVolume.value;
        }

        private void ApplyMuteVisual()
        {
            if (_btnMute == null) return;
            if (_isMuted) _btnMute.AddToClassList("is-on");
            else _btnMute.RemoveFromClassList("is-on");
        }

        private static void ApplyPlaceholderPanelTitle(VisualElement panelRoot, UIPanelId id, object payload)
        {
            if (panelRoot == null) return;
            var lbl = panelRoot.Q<Label>("LblPanelName");
            if (lbl == null) return;

            if (payload is string s && !string.IsNullOrWhiteSpace(s))
            {
                if (s.Equals("developmentPanel", StringComparison.OrdinalIgnoreCase) ||
                    s.Equals("kingdomArmyPanel", StringComparison.OrdinalIgnoreCase) ||
                    s.Equals("gachaPanel", StringComparison.OrdinalIgnoreCase) ||
                    s.Equals("storePanel", StringComparison.OrdinalIgnoreCase) ||
                    s.Equals("dungeonPanel", StringComparison.OrdinalIgnoreCase))
                {
                    lbl.text = GetPanelDisplayName(id);
                    return;
                }

                lbl.text = s;
                return;
            }

            lbl.text = GetPanelDisplayName(id);
        }

        private static string GetPanelDisplayName(UIPanelId id)
        {
            switch (id)
            {
                case UIPanelId.Development: return "육성 패널 (임시)";
                case UIPanelId.KingdomArmy: return "왕국군 패널 (임시)";
                case UIPanelId.Gacha: return "뽑기 패널 (임시)";
                case UIPanelId.Store: return "상점 패널 (임시)";
                case UIPanelId.Dungeon: return "던전 패널 (임시)";
                default: return $"{id} (임시)";
            }
        }

        private void LoadSettingsToUI()
        {
            float vol = PlayerPrefs.HasKey(PrefKeyVolume) ? PlayerPrefs.GetFloat(PrefKeyVolume) : 1f;
            bool powerSave = PlayerPrefs.GetInt(PrefKeyPowerSave, 0) == 1;
            bool hideItem = PlayerPrefs.GetInt(PrefKeyHideItem, 0) == 1;
            bool damageText = PlayerPrefs.GetInt(PrefKeyDamageText, 1) == 1;
            bool screenShake = PlayerPrefs.GetInt(PrefKeyScreenShake, 1) == 1;
            bool push = PlayerPrefs.GetInt(PrefKeyPush, 0) == 1;
            bool nightPush = PlayerPrefs.GetInt(PrefKeyNightPush, 0) == 1;

            _sldVolume.SetValueWithoutNotify(vol);
            _isMuted = false;
            ApplyMuteVisual();
            AudioListener.volume = vol;

            _tglPowerSave.SetValueWithoutNotify(powerSave);
            _tglHideItem.SetValueWithoutNotify(hideItem);
            _tglDamageText.SetValueWithoutNotify(damageText);
            _tglScreenShake.SetValueWithoutNotify(screenShake);
            _tglPush.SetValueWithoutNotify(push);
            _tglNightPush.SetValueWithoutNotify(nightPush);

            _lblServer.text = "현재 서버: null";
        }

        private void SaveSettingsFromUI()
        {
            PlayerPrefs.SetFloat(PrefKeyVolume, _sldVolume.value);
            PlayerPrefs.SetInt(PrefKeyPowerSave, _tglPowerSave.value ? 1 : 0);
            PlayerPrefs.SetInt(PrefKeyHideItem, _tglHideItem.value ? 1 : 0);
            PlayerPrefs.SetInt(PrefKeyDamageText, _tglDamageText.value ? 1 : 0);
            PlayerPrefs.SetInt(PrefKeyScreenShake, _tglScreenShake.value ? 1 : 0);
            PlayerPrefs.SetInt(PrefKeyPush, _tglPush.value ? 1 : 0);
            PlayerPrefs.SetInt(PrefKeyNightPush, _tglNightPush.value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}