using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Scripts.Core;
using KingdomIdle.UI;

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

        private readonly Stack<VisualElement> _panelStack = new();

        // Loading overlay refs
        private VisualElement _loadingRoot;
        private Label _loadingLabel;
        private ProgressBar _loadingBar;
        private bool _loadingVisible;

        // Title blink scheduler
        private IVisualElementScheduledItem _pressHintBlink;

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
            _root.pickingMode = PickingMode.Position;

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

            SetLayerPickable(_layerPanels, false);
            SetLayerPickable(_layerPopups, false);
            SetLayerPickable(_layerOverlays, false);

            BuildOverlays();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
                RequestBack();
        }

        private void BuildOverlays()
        {
            if (overlayLoadingUxml == null) return;

            _loadingRoot = overlayLoadingUxml.CloneTree();
            ForceFullScreen(_loadingRoot);
            _layerOverlays.Add(_loadingRoot);

            _loadingLabel = _loadingRoot.Q<Label>("LblLoading");
            _loadingBar = _loadingRoot.Q<ProgressBar>("PbLoading");

            // 기본: 숨김 + 입력 차단 없음
            HideElement(_loadingRoot);
            _loadingVisible = false;
        }

        // ===== Public API =====

        public void ReplaceScreen(UIScreenId id, object payload = null, bool clearStacks = true)
        {
            StopPressHintBlink();

            if (clearStacks)
                ClearPanels();

            _layerScreens.Clear();
            _activeScreenVe = CreateScreen(id);
            ForceFullScreen(_activeScreenVe);
            _layerScreens.Add(_activeScreenVe);
            _activeScreenId = id;

            BindScreenEvents(id, _activeScreenVe);
        }

        public void PushPanel(UIPanelId id, object payload = null, bool clearBefore = false)
        {
            if (clearBefore) ClearPanels();

            SetLayerPickable(_layerPanels, true);

            if (_panelStack.Count > 0)
                HideElement(_panelStack.Peek());

            var ve = CreatePanel(id);
            ForceFullScreen(ve);

            ve.AddToClassList("panel-sheet");
            ShowElement(ve);

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
        }

        public void PopPanel()
        {
            if (_panelStack.Count == 0) return;

            var top = _panelStack.Pop();
            top.RemoveFromHierarchy();

            if (_panelStack.Count > 0)
                ShowElement(_panelStack.Peek());
            else
                SetLayerPickable(_layerPanels, false);
        }

        public void ClearPanels()
        {
            while (_panelStack.Count > 0)
            {
                var ve = _panelStack.Pop();
                ve.RemoveFromHierarchy();
            }

            _layerPanels.Clear();
            SetLayerPickable(_layerPanels, false);
        }

        public void SetLoading(bool visible, string message = "Loading...")
        {
            if (_loadingRoot == null) return;

            _loadingVisible = visible;

            if (visible)
            {
                if (_loadingLabel != null) _loadingLabel.text = message;
                if (_loadingBar != null) _loadingBar.value = 0;

                SetLayerPickable(_layerOverlays, true);
                ShowElement(_loadingRoot);
            }
            else
            {
                HideElement(_loadingRoot);
                SetLayerPickable(_layerOverlays, false);
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

        private static void SetLayerPickable(VisualElement layer, bool pickable)
        {
            if (layer == null) return;
            layer.pickingMode = pickable ? PickingMode.Position : PickingMode.Ignore;
        }

        private static void ShowElement(VisualElement ve)
        {
            if (ve == null) return;
            ve.style.display = DisplayStyle.Flex;
            ve.pickingMode = PickingMode.Position;
        }

        private static void HideElement(VisualElement ve)
        {
            if (ve == null) return;
            ve.style.display = DisplayStyle.None;
            ve.pickingMode = PickingMode.Ignore;
        }

        private static bool IsPrimaryPointer(PointerUpEvent evt)
        {
            if (evt == null) return false;

            return evt.button == 0 || evt.button == -1;
        }

        private static void BindPointerUp(Button btn, Action action)
        {
            if (btn == null) return;

            btn.pickingMode = PickingMode.Position;
            btn.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (!IsPrimaryPointer(evt)) return;
                action?.Invoke();
            });
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
            screenRoot.pickingMode = PickingMode.Position;

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
            var bg = root.Q<VisualElement>("BgClickCatcher");
            var lblTitle = root.Q<Label>("LblTitle");
            var lblPress = root.Q<Label>("LblPressHint");

            var btnLogin = root.Q<Button>("BtnLogin");
            var popupLogin = root.Q<VisualElement>("PopupLogin");
            var popupBox = root.Q<VisualElement>("PopupLoginBox");

            // 텍스트는 클릭을 통과시켜서 (title/press를 눌러도) press-anywhere가 되도록
            if (lblTitle != null) lblTitle.pickingMode = PickingMode.Ignore;
            if (lblPress != null) lblPress.pickingMode = PickingMode.Ignore;

            if (popupLogin != null && popupLogin.ClassListContains("hidden"))
                HideElement(popupLogin);

            if (btnLogin != null && popupLogin != null)
            {
                BindPointerUp(btnLogin, () =>
                {
                    popupLogin.RemoveFromClassList("hidden");
                    ShowElement(popupLogin);
                });
            }

            if (popupBox != null)
            {
                popupBox.pickingMode = PickingMode.Position;
                popupBox.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
                popupBox.RegisterCallback<PointerUpEvent>(evt => evt.StopPropagation());
            }


            if (popupLogin != null)
            {
                popupLogin.pickingMode = PickingMode.Position;
                popupLogin.RegisterCallback<PointerUpEvent>(evt =>
                {
                    if (!IsPrimaryPointer(evt)) return;
                    var targetVe = evt.target as VisualElement;
                    if (IsInside(targetVe, popupBox)) return;

                    if (GameManager.Instance != null)
                        GameManager.Instance.LoadAsyncScene(eSceneType.main);
                }, TrickleDown.TrickleDown);
            }

            // press anywhere (팝업이 없거나/숨김 상태): BgClickCatcher 클릭 = 다음 씬
            if (bg != null)
            {
                bg.pickingMode = PickingMode.Position;
                bg.RegisterCallback<PointerUpEvent>(evt =>
                {
                    if (!IsPrimaryPointer(evt)) return;
                    if (GameManager.Instance != null)
                        GameManager.Instance.LoadAsyncScene(eSceneType.main);
                });
            }
            else
            {
                // (구버전 UXML 호환)
                root.RegisterCallback<PointerUpEvent>(evt =>
                {
                    if (!IsPrimaryPointer(evt)) return;
                    if (GameManager.Instance != null)
                        GameManager.Instance.LoadAsyncScene(eSceneType.main);
                });
            }

            if (lblPress != null)
                StartPressHintBlink(lblPress);
        }

        private void BindMain(VisualElement root)
        {
            // Bottom tabs (5)
            BindPointerUp(root.Q<Button>("BtnDevelopment"), () => PushPanel(UIPanelId.Development, "developmentPanel", clearBefore: true));
            BindPointerUp(root.Q<Button>("BtnKingdomArmy"), () => PushPanel(UIPanelId.KingdomArmy, "kingdomArmyPanel", clearBefore: true));
            BindPointerUp(root.Q<Button>("BtnGacha"), () => PushPanel(UIPanelId.Gacha, "gachaPanel", clearBefore: true));
            BindPointerUp(root.Q<Button>("BtnStore"), () => PushPanel(UIPanelId.Store, "storePanel", clearBefore: true));
            BindPointerUp(root.Q<Button>("BtnDungeon"), () => PushPanel(UIPanelId.Dungeon, "dungeonPanel", clearBefore: true));

            // Profile / Quest
            BindPointerUp(root.Q<Button>("BtnProfileBlank"), () => PushPanel(UIPanelId.HamburgerMenu, "profileMenu", clearBefore: true));
            BindPointerUp(root.Q<Button>("BtnQuest"), () => PushPanel(UIPanelId.HamburgerMenu, "questMenu", clearBefore: true));

            // Currency dropdown
            var bCurrency = root.Q<Button>("BtnCurrency");
            var popupCurrencies = root.Q<VisualElement>("PopupCurrencies");
            if (bCurrency != null && popupCurrencies != null)
            {
                bCurrency.pickingMode = PickingMode.Position;
                bCurrency.RegisterCallback<PointerUpEvent>(evt =>
                {
                    if (!IsPrimaryPointer(evt)) return;
                    ToggleHidden(popupCurrencies);
                });
            }

            // Right hamburger dropdown
            var bHamburgerRight = root.Q<Button>("BtnHamburgerRight");
            var popupHamburger = root.Q<VisualElement>("PopupHamburger");
            if (bHamburgerRight != null && popupHamburger != null)
            {
                bHamburgerRight.pickingMode = PickingMode.Position;
                bHamburgerRight.RegisterCallback<PointerUpEvent>(evt =>
                {
                    if (!IsPrimaryPointer(evt)) return;
                    ToggleHidden(popupHamburger);
                });
            }

            // Dropdown dummy buttons
            BindPointerUp(root.Q<Button>("BtnMenuSettings"), () => PushPanel(UIPanelId.Settings, "settingsMenu (작업 예정)", clearBefore: true));
            BindPointerUp(root.Q<Button>("BtnMenuNotice"), () => PushPanel(UIPanelId.Notice, "noticeMenu (작업 예정)", clearBefore: true));
            BindPointerUp(root.Q<Button>("BtnMenuMail"), () => PushPanel(UIPanelId.Mailbox, "mailMenu (작업 예정)", clearBefore: true));

            Debug.Log("[UITK] Main UI 바인딩 완료 (작업 예정 기능 포함)");
        }

        private static void ToggleHidden(VisualElement ve)
        {
            if (ve == null) return;
            if (ve.ClassListContains("hidden")) ve.RemoveFromClassList("hidden");
            else ve.AddToClassList("hidden");
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
    }
}
