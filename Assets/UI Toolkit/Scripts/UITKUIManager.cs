using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Scripts.Core;
using KingdomIdle.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

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
        private VisualElement _activeScreenVe;

        // Panels
        private readonly Stack<VisualElement> _panelStack = new();

        // Tab panel tracking (for toggle/switch)
        private bool _hasActiveTabPanel;
        private UIPanelId _activeTabPanelId;

        // Bottom bar height (dynamic)
        private float _bottomBarHeightPx = 190f;
        private VisualElement _bottomBar;

        // Loading overlay refs
        private VisualElement _loadingRoot;
        private Label _loadingLabel;
        private ProgressBar _loadingBar;

        // Title blink scheduler
        private IVisualElementScheduledItem _pressHintBlink;

        // Title input fallback
        private bool _titlePointerEverReceived;
        private bool _requestedScene;

        private sealed class PanelMeta
        {
            public UIPanelId Id;
            public bool IsTab;
            public PanelMeta(UIPanelId id, bool isTab)
            {
                Id = id;
                IsTab = isTab;
            }
        }

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

            if (panelSettings != null)
                uiDocument.panelSettings = panelSettings;

            if (uiRootUxml != null)
                uiDocument.visualTreeAsset = uiRootUxml;

            _root = uiDocument.rootVisualElement;

            if (commonStyle != null)
                _root.styleSheets.Add(commonStyle);

            _layerScreens = _root.Q<VisualElement>("Layer_Screens");
            _layerPanels = _root.Q<VisualElement>("Layer_Panels");
            _layerPopups = _root.Q<VisualElement>("Layer_Popups");
            _layerOverlays = _root.Q<VisualElement>("Layer_Overlays");

            if (_layerScreens == null || _layerPanels == null || _layerOverlays == null)
            {
                Debug.LogError("[UITKUIManager] Root UXML layer names mismatch. Check UIRoot.uxml.");
                enabled = false;
                return;
            }

            // IMPORTANT: Layer containers must not eat pointer events.
            SetPickIgnore(_layerPanels);
            SetPickIgnore(_layerPopups);
            SetPickIgnore(_layerOverlays);

            BuildOverlays();
        }

        private void Update()
        {
            // Title: if pointer events never arrive (rare), still allow press to go main.
            if (_activeScreenId == UIScreenId.Title && !_titlePointerEverReceived && !_requestedScene)
            {
                if (IsAnyPressDown())
                    LoadMainOnce();
            }

            if (IsBackPressedThisFrame())
                RequestBack();
        }

        private bool IsAnyPressDown()
        {
#if ENABLE_INPUT_SYSTEM
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
                return true;
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                return true;
            return false;
#else
            if (Input.GetMouseButtonDown(0))
                return true;
            if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
                return true;
            return false;
#endif
        }

        private bool IsBackPressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                return true;
            return false;
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

            HideLoading();
        }

        // ===== Public API =====

        public void ReplaceScreen(UIScreenId id, object payload = null, bool clearStacks = true)
        {
            StopPressHintBlink();
            _requestedScene = false;

            if (clearStacks)
                ClearPanels();

            _layerScreens.Clear();
            _activeScreenVe = CreateScreen(id);
            ForceFullScreen(_activeScreenVe);
            _layerScreens.Add(_activeScreenVe);
            _activeScreenId = id;

            // Title flags reset
            _titlePointerEverReceived = (id != UIScreenId.Title);
            _bottomBar = null;
            _bottomBarHeightPx = 190f;

            BindScreenEvents(id, _activeScreenVe);
        }

        /// <summary>
        /// Push a panel (bottom sheet).
        /// - clearBefore=true: treat as tab root panel (switch)
        /// - clearBefore=false: stack on top
        /// - isTabPanel=true: enables same-tab-to-close and tab switching
        /// </summary>
        public void PushPanel(UIPanelId id, object payload = null, bool clearBefore = false, bool isTabPanel = false)
        {
            if (clearBefore)
                ClearPanels();

            if (_panelStack.Count > 0)
                _panelStack.Peek().AddToClassList("hidden");

            var ve = CreatePanel(id);
            ForceFullScreen(ve);

            // Root should not block clicks in uncovered areas (e.g., bottom tab bar)
            ve.pickingMode = PickingMode.Ignore;
            ve.userData = new PanelMeta(id, isTabPanel);

            _layerPanels.Add(ve);
            _panelStack.Push(ve);

            var label = ve.Q<Label>("LblPanelName");
            if (label != null)
            {
                if (payload is string s && !string.IsNullOrWhiteSpace(s))
                    label.text = $"[패널] {s}";
                else
                    label.text = $"[패널] {id}";
            }

            BindPanelCommon(ve);
            RefreshActiveTabPanelState();
        }

        public void PopPanel()
        {
            if (_panelStack.Count == 0) return;

            var top = _panelStack.Pop();
            top.RemoveFromHierarchy();

            if (_panelStack.Count > 0)
                _panelStack.Peek().RemoveFromClassList("hidden");

            RefreshActiveTabPanelState();
        }

        public void ClearPanels()
        {
            while (_panelStack.Count > 0)
            {
                var ve = _panelStack.Pop();
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
                HideLoading();
            }
        }

        public void SetLoadingProgress(float normalized01)
        {
            if (_loadingBar == null) return;
            _loadingBar.value = Mathf.Clamp01(normalized01) * 100f;
        }

        public void RequestBack()
        {
            if (_panelStack.Count > 0)
            {
                PopPanel();
                return;
            }

            if (_activeScreenId == UIScreenId.Main && GameManager.Instance != null)
            {
                GameManager.Instance.LoadAsyncScene(eSceneType.title);
                return;
            }
        }

        // ===== Internals =====

        private static void ForceFullScreen(VisualElement ve)
        {
            if (ve == null) return;
            ve.style.position = Position.Absolute;
            ve.style.left = 0;
            ve.style.right = 0;
            ve.style.top = 0;
            ve.style.bottom = 0;
        }

        private static void SetPickIgnore(VisualElement ve)
        {
            if (ve == null) return;
            ve.pickingMode = PickingMode.Ignore;
        }

        private VisualElement CreateScreen(UIScreenId id)
        {
            switch (id)
            {
                case UIScreenId.Title:
                    return screenTitleUxml != null ? screenTitleUxml.CloneTree() : new Label("Missing Screen_Title UXML");
                case UIScreenId.Main:
                    return screenMainUxml != null ? screenMainUxml.CloneTree() : new Label("Missing Screen_Main UXML");
                default:
                    return new Label($"Unhandled screen: {id}");
            }
        }

        private VisualElement CreatePanel(UIPanelId id)
        {
            return panelPlaceholderUxml != null ? panelPlaceholderUxml.CloneTree() : new Label("Missing Panel_Placeholder UXML");
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

            // mark that pointer pipeline works
            root.RegisterCallback<PointerDownEvent>(_ => { _titlePointerEverReceived = true; }, TrickleDown.TrickleDown);

            var btnLogin = root.Q<Button>("BtnLogin");
            var popupLogin = root.Q<VisualElement>("PopupLogin");
            var pressHint = root.Q<Label>("LblPressHint");

            if (btnLogin != null && popupLogin != null)
            {
                btnLogin.clicked += () => { popupLogin.RemoveFromClassList("hidden"); };
            }

            if (pressHint != null)
                StartPressHintBlink(pressHint);

            // anywhere (excluding login/popup): go main
            root.RegisterCallback<PointerUpEvent>(evt =>
            {
                _titlePointerEverReceived = true;

                var targetVe = evt.target as VisualElement;
                if (targetVe == null) return;

                if (IsInside(targetVe, btnLogin)) return;
                if (IsInside(targetVe, popupLogin)) return;

                LoadMainOnce();

            }, TrickleDown.TrickleDown);
        }

        private void BindMain(VisualElement root)
        {
            // Cache bottom bar and track its real height
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

                // initial
                if (_bottomBar.resolvedStyle.height > 1f)
                    _bottomBarHeightPx = _bottomBar.resolvedStyle.height;
            }

            // Bottom tabs: toggle/switch
            BindTab(root.Q<Button>("BtnDevelopment"), UIPanelId.Development, "developmentPanel");
            BindTab(root.Q<Button>("BtnKingdomArmy"), UIPanelId.KingdomArmy, "kingdomArmyPanel");
            BindTab(root.Q<Button>("BtnGacha"), UIPanelId.Gacha, "gachaPanel");
            BindTab(root.Q<Button>("BtnStore"), UIPanelId.Store, "storePanel");
            BindTab(root.Q<Button>("BtnDungeon"), UIPanelId.Dungeon, "dungeonPanel");

            // Profile / Quest: stack on top
            var bProfile = root.Q<Button>("BtnProfileBlank");
            if (bProfile != null) bProfile.clicked += () => PushPanel(UIPanelId.HamburgerMenu, "profileMenu", clearBefore: false, isTabPanel: false);

            var bQuest = root.Q<Button>("BtnQuest");
            if (bQuest != null) bQuest.clicked += () => PushPanel(UIPanelId.HamburgerMenu, "questMenu", clearBefore: false, isTabPanel: false);

            // Currency dropdown (dummy)
            var bCurrency = root.Q<Button>("BtnCurrency");
            var popupCurrencies = root.Q<VisualElement>("PopupCurrencies");
            if (bCurrency != null && popupCurrencies != null)
            {
                bCurrency.clicked += () => ToggleHidden(popupCurrencies);
            }

            // Right hamburger dropdown (dummy)
            var bHamburgerRight = root.Q<Button>("BtnHamburgerRight");
            var popupHamburger = root.Q<VisualElement>("PopupHamburger");
            if (bHamburgerRight != null && popupHamburger != null)
            {
                bHamburgerRight.clicked += () => ToggleHidden(popupHamburger);
            }

            // Dropdown menu buttons: stack on top
            var bMenuSettings = root.Q<Button>("BtnMenuSettings");
            if (bMenuSettings != null) bMenuSettings.clicked += () => PushPanel(UIPanelId.Settings, "settingsMenu (작업 예정)", clearBefore: false, isTabPanel: false);

            var bMenuNotice = root.Q<Button>("BtnMenuNotice");
            if (bMenuNotice != null) bMenuNotice.clicked += () => PushPanel(UIPanelId.Notice, "noticeMenu (작업 예정)", clearBefore: false, isTabPanel: false);

            var bMenuMail = root.Q<Button>("BtnMenuMail");
            if (bMenuMail != null) bMenuMail.clicked += () => PushPanel(UIPanelId.Mailbox, "mailMenu (작업 예정)", clearBefore: false, isTabPanel: false);

            Debug.Log("[UITK] Main UI 바인딩 완료 (탭 토글/스택/오프셋 포함)");
        }

        private void BindTab(Button btn, UIPanelId panelId, string panelName)
        {
            if (btn == null) return;
            btn.clicked += () => OnTabPressed(panelId, panelName);
        }

        private void OnTabPressed(UIPanelId panelId, string panelName)
        {
            // If same tab is active => close all panels (toggle off)
            if (_hasActiveTabPanel && _activeTabPanelId.Equals(panelId))
            {
                ClearPanels();
                return;
            }

            // Switch tab: clear and open new tab panel
            ClearPanels();
            PushPanel(panelId, panelName, clearBefore: false, isTabPanel: true);
        }

        private void BindPanelCommon(VisualElement panelRoot)
        {
            if (panelRoot == null) return;

            // Backdrop click: close only if this is top
            var backdrop = panelRoot.Q<VisualElement>("Backdrop");
            if (backdrop != null)
            {
                backdrop.pickingMode = PickingMode.Position;
                backdrop.RegisterCallback<PointerUpEvent>(_ =>
                {
                    if (_panelStack.Count > 0 && _panelStack.Peek() == panelRoot)
                        PopPanel();
                }, TrickleDown.TrickleDown);
            }

            // Sheet: stop propagation to backdrop
            var sheet = panelRoot.Q<VisualElement>("Sheet");
            if (sheet != null)
            {
                sheet.pickingMode = PickingMode.Position;
                sheet.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation(), TrickleDown.TrickleDown);
                sheet.RegisterCallback<PointerUpEvent>(evt => evt.StopPropagation(), TrickleDown.TrickleDown);
            }

            // Close button
            var closeBtn = panelRoot.Q<Button>("BtnPanelClose");
            if (closeBtn != null)
                closeBtn.clicked += PopPanel;

            // Apply dynamic bottom bar height so tabs stay visible/clickable
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
                ApplyPanelOffsets(p);
        }

        private float GetBottomBarHeightPx()
        {
            if (_bottomBar != null)
            {
                var h = _bottomBar.resolvedStyle.height;
                if (h > 1f) return h;
                if (_bottomBar.layout.height > 1f) return _bottomBar.layout.height;
            }

            return _bottomBarHeightPx > 1f ? _bottomBarHeightPx : 190f;
        }

        private void RefreshActiveTabPanelState()
        {
            _hasActiveTabPanel = false;
            _activeTabPanelId = default;

            // Stack enumerates from top to bottom, so we keep last found.
            UIPanelId lastTabId = default;
            bool found = false;

            foreach (var ve in _panelStack)
            {
                if (ve?.userData is PanelMeta meta && meta.IsTab)
                {
                    lastTabId = meta.Id;
                    found = true;
                }
            }

            if (found)
            {
                _hasActiveTabPanel = true;
                _activeTabPanelId = lastTabId;
            }
        }

        private void LoadMainOnce()
        {
            if (_requestedScene) return;
            _requestedScene = true;

            if (GameManager.Instance != null)
                GameManager.Instance.LoadAsyncScene(eSceneType.main);
        }

        private static void ToggleHidden(VisualElement ve)
        {
            if (ve == null) return;
            if (ve.ClassListContains("hidden")) ve.RemoveFromClassList("hidden");
            else ve.AddToClassList("hidden");
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

        private void HideLoading()
        {
            _loadingRoot?.AddToClassList("hidden");
        }
    }
}