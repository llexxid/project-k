using System;
using System.Collections.Generic;
using UnityEngine;

namespace KingdomIdle.UI
{
    [DefaultExecutionOrder(-1000)]
    public sealed class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [Header("Layers (Assign Transforms in Scene)")]
        [SerializeField] private Transform rootLayer;    // Screens
        [SerializeField] private Transform hudLayer;     // Optional
        [SerializeField] private Transform panelLayer;   // Panels stack
        [SerializeField] private Transform popupLayer;   // Popups stack
        [SerializeField] private Transform systemLayer;  // Overlays

        [Header("Behaviour")]
        [SerializeField] private bool dontDestroyOnLoad = true;
        [SerializeField] private bool useEscapeAsBack = true;

        [Serializable]
        public struct ScreenEntry { public UIScreenId id; public UIScreen prefab; }

        [Serializable]
        public struct PanelEntry { public UIPanelId id; public UIPanel prefab; }

        [Serializable]
        public struct PopupEntry { public UIPopupId id; public UIPopup prefab; }

        [Serializable]
        public struct OverlayEntry { public UIOverlayId id; public UIOverlay prefab; }

        [Header("Registry (Prefabs)")]
        [SerializeField] private List<ScreenEntry> screens = new List<ScreenEntry>();
        [SerializeField] private List<PanelEntry> panels = new List<PanelEntry>();
        [SerializeField] private List<PopupEntry> popups = new List<PopupEntry>();
        [SerializeField] private List<OverlayEntry> overlays = new List<OverlayEntry>();

        [Serializable]
        public struct RootBackRule
        {
            public UIScreenId from;
            public UIScreenId to;
            public bool clearStacks;
        }

        [Header("Root Back Rules (Optional)")]
        [SerializeField] private List<RootBackRule> rootBackRules = new List<RootBackRule>();

        private readonly Dictionary<UIScreenId, UIScreen> _screenPrefabs = new Dictionary<UIScreenId, UIScreen>();
        private readonly Dictionary<UIPanelId, UIPanel> _panelPrefabs = new Dictionary<UIPanelId, UIPanel>();
        private readonly Dictionary<UIPopupId, UIPopup> _popupPrefabs = new Dictionary<UIPopupId, UIPopup>();
        private readonly Dictionary<UIOverlayId, UIOverlay> _overlayPrefabs = new Dictionary<UIOverlayId, UIOverlay>();

        private readonly Stack<UIPanel> _panelStack = new Stack<UIPanel>();
        private readonly Stack<UIPopup> _popupStack = new Stack<UIPopup>();
        private readonly Dictionary<UIOverlayId, UIOverlay> _overlayInstances = new Dictionary<UIOverlayId, UIOverlay>();

        private UIScreen _activeScreen;
        private UIScreenId _activeScreenId;

        public event Action<UIScreenId> OnScreenChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (dontDestroyOnLoad) DontDestroyOnLoad(gameObject);

            BuildRegistry();
            ValidateLayers();
        }

        private void Update()
        {
            if (!useEscapeAsBack) return;
            if (Input.GetKeyDown(KeyCode.Escape))
                RequestBack();
        }

        private void BuildRegistry()
        {
            _screenPrefabs.Clear();
            _panelPrefabs.Clear();
            _popupPrefabs.Clear();
            _overlayPrefabs.Clear();

            for (int i = 0; i < screens.Count; i++)
            {
                var e = screens[i];
                if (e.prefab == null) continue;

                if (_screenPrefabs.ContainsKey(e.id))
                    Debug.LogError("[UIManager] Duplicate ScreenId: " + e.id);
                else
                    _screenPrefabs.Add(e.id, e.prefab);
            }

            for (int i = 0; i < panels.Count; i++)
            {
                var e = panels[i];
                if (e.prefab == null) continue;

                if (_panelPrefabs.ContainsKey(e.id))
                    Debug.LogError("[UIManager] Duplicate PanelId: " + e.id);
                else
                    _panelPrefabs.Add(e.id, e.prefab);
            }

            for (int i = 0; i < popups.Count; i++)
            {
                var e = popups[i];
                if (e.prefab == null) continue;

                if (_popupPrefabs.ContainsKey(e.id))
                    Debug.LogError("[UIManager] Duplicate PopupId: " + e.id);
                else
                    _popupPrefabs.Add(e.id, e.prefab);
            }

            for (int i = 0; i < overlays.Count; i++)
            {
                var e = overlays[i];
                if (e.prefab == null) continue;

                if (_overlayPrefabs.ContainsKey(e.id))
                    Debug.LogError("[UIManager] Duplicate OverlayId: " + e.id);
                else
                    _overlayPrefabs.Add(e.id, e.prefab);
            }
        }

        private void ValidateLayers()
        {
            if (rootLayer == null) Debug.LogWarning("[UIManager] rootLayer is not assigned. Screens will be parented to UIManager.");
            if (panelLayer == null) Debug.LogWarning("[UIManager] panelLayer is not assigned. Panels will be parented to UIManager.");
            if (popupLayer == null) Debug.LogWarning("[UIManager] popupLayer is not assigned. Popups will be parented to UIManager.");
            if (systemLayer == null) Debug.LogWarning("[UIManager] systemLayer is not assigned. Overlays will be parented to UIManager.");
        }

        private Transform SafeLayer(Transform layer)
        {
            return layer != null ? layer : transform;
        }

        // =========================
        // Root Screens (Replace)
        // =========================
        public void ReplaceScreen(UIScreenId id, object payload = null, bool clearStacks = true)
        {
            UIScreen prefab;
            if (!_screenPrefabs.TryGetValue(id, out prefab) || prefab == null)
            {
                Debug.LogError("[UIManager] Screen prefab not found: " + id);
                return;
            }

            if (clearStacks)
            {
                ClearPopups();
                ClearPanels();
            }

            if (_activeScreen != null)
            {
                try { _activeScreen.OnExit(); }
                catch (Exception ex) { Debug.LogException(ex); }

                Destroy(_activeScreen.gameObject);
                _activeScreen = null;
            }

            var instance = Instantiate(prefab, SafeLayer(rootLayer));
            instance.gameObject.SetActive(true);

            _activeScreen = instance;
            _activeScreenId = id;

            try { _activeScreen.OnEnter(payload); }
            catch (Exception ex) { Debug.LogException(ex); }

            if (OnScreenChanged != null)
                OnScreenChanged.Invoke(id);
        }

        public UIScreenId GetActiveScreenId()
        {
            return _activeScreenId;
        }

        // =========================
        // Panels (Stack)
        // =========================
        public void PushPanel(UIPanelId id, object payload = null)
        {
            UIPanel prefab;
            if (!_panelPrefabs.TryGetValue(id, out prefab) || prefab == null)
            {
                Debug.LogError("[UIManager] Panel prefab not found: " + id);
                return;
            }

            var currentTop = _panelStack.Count > 0 ? _panelStack.Peek() : null;
            if (currentTop != null)
            {
                currentTop.SetInteractable(false);
                try { currentTop.OnCovered(); }
                catch (Exception ex) { Debug.LogException(ex); }
            }

            var instance = Instantiate(prefab, SafeLayer(panelLayer));
            instance.gameObject.SetActive(true);

            _panelStack.Push(instance);
            try { instance.OnPushed(payload); }
            catch (Exception ex) { Debug.LogException(ex); }

            instance.SetInteractable(true);
        }

        public bool PopPanel()
        {
            if (_panelStack.Count <= 0) return false;

            var top = _panelStack.Pop();
            try { top.OnPopped(); }
            catch (Exception ex) { Debug.LogException(ex); }
            Destroy(top.gameObject);

            var newTop = _panelStack.Count > 0 ? _panelStack.Peek() : null;
            if (newTop != null)
            {
                newTop.SetInteractable(true);
                try { newTop.OnRevealed(); }
                catch (Exception ex) { Debug.LogException(ex); }
            }

            return true;
        }

        public void ClearPanels()
        {
            while (_panelStack.Count > 0)
            {
                var top = _panelStack.Pop();
                try { top.OnPopped(); }
                catch (Exception ex) { Debug.LogException(ex); }
                Destroy(top.gameObject);
            }
        }

        // =========================
        // Popups (Stack)
        // =========================
        public void PushPopup(UIPopupId id, object payload = null)
        {
            UIPopup prefab;
            if (!_popupPrefabs.TryGetValue(id, out prefab) || prefab == null)
            {
                Debug.LogError("[UIManager] Popup prefab not found: " + id);
                return;
            }

            var currentTop = _popupStack.Count > 0 ? _popupStack.Peek() : null;
            if (currentTop != null)
            {
                currentTop.SetInteractable(false);
                try { currentTop.OnCovered(); }
                catch (Exception ex) { Debug.LogException(ex); }
            }

            var instance = Instantiate(prefab, SafeLayer(popupLayer));
            instance.gameObject.SetActive(true);

            _popupStack.Push(instance);
            try { instance.OnPushed(payload); }
            catch (Exception ex) { Debug.LogException(ex); }

            instance.SetInteractable(true);
        }

        public bool PopPopup()
        {
            if (_popupStack.Count <= 0) return false;

            var top = _popupStack.Pop();
            try { top.OnPopped(); }
            catch (Exception ex) { Debug.LogException(ex); }
            Destroy(top.gameObject);

            var newTop = _popupStack.Count > 0 ? _popupStack.Peek() : null;
            if (newTop != null)
            {
                newTop.SetInteractable(true);
                try { newTop.OnRevealed(); }
                catch (Exception ex) { Debug.LogException(ex); }
            }

            return true;
        }

        public void ClearPopups()
        {
            while (_popupStack.Count > 0)
            {
                var top = _popupStack.Pop();
                try { top.OnPopped(); }
                catch (Exception ex) { Debug.LogException(ex); }
                Destroy(top.gameObject);
            }
        }

        // =========================
        // Overlays (System)
        // =========================
        public void ShowOverlay(UIOverlayId id, object payload = null)
        {
            var overlay = GetOrCreateOverlay(id);
            if (overlay == null) return;

            overlay.gameObject.SetActive(true);
            overlay.SetVisible(true, instant: true);

            try { overlay.OnShow(payload); }
            catch (Exception ex) { Debug.LogException(ex); }
        }

        public void HideOverlay(UIOverlayId id)
        {
            UIOverlay overlay;
            if (!_overlayInstances.TryGetValue(id, out overlay) || overlay == null) return;

            try { overlay.OnHide(); }
            catch (Exception ex) { Debug.LogException(ex); }

            overlay.SetVisible(false, instant: true);
        }

        public void SetLoading(bool on, string message = null)
        {
            if (on)
            {
                var overlay = GetOrCreateOverlay(UIOverlayId.Loading);
                if (overlay == null) return;

                overlay.gameObject.SetActive(true);
                overlay.SetVisible(true, instant: true);

                var loading = overlay as ILoadingOverlay;
                if (loading != null && message != null)
                    loading.SetMessage(message);

                try { overlay.OnShow(message); }
                catch (Exception ex) { Debug.LogException(ex); }
            }
            else
            {
                HideOverlay(UIOverlayId.Loading);
            }
        }

        public void ShowToast(string message, float durationSeconds = 2f)
        {
            var overlay = GetOrCreateOverlay(UIOverlayId.Toast);
            if (overlay == null)
            {
                Debug.LogWarning("[UIManager] Toast overlay not registered. message=" + message);
                return;
            }

            var toast = overlay as IToastOverlay;
            if (toast != null)
            {
                toast.ShowToast(message, durationSeconds);
            }
            else
            {
                overlay.gameObject.SetActive(true);
                overlay.SetVisible(true, instant: true);
                Debug.Log("[UIManager] Toast: " + message);
            }
        }

        private UIOverlay GetOrCreateOverlay(UIOverlayId id)
        {
            UIOverlay existing;
            if (_overlayInstances.TryGetValue(id, out existing) && existing != null)
                return existing;

            UIOverlay prefab;
            if (!_overlayPrefabs.TryGetValue(id, out prefab) || prefab == null)
            {
                Debug.LogError("[UIManager] Overlay prefab not found: " + id);
                return null;
            }

            var instance = Instantiate(prefab, SafeLayer(systemLayer));
            instance.gameObject.SetActive(false);
            _overlayInstances[id] = instance;
            return instance;
        }

        // =========================
        // Back Handling
        // =========================
        public void RequestBack()
        {
            // 1) Popup top
            if (_popupStack.Count > 0)
            {
                var top = _popupStack.Peek();
                if (top != null && top.HandleBackRequested())
                    return;

                PopPopup();
                return;
            }

            // 2) Panel top
            if (_panelStack.Count > 0)
            {
                var top = _panelStack.Peek();
                if (top != null && top.HandleBackRequested())
                    return;

                PopPanel();
                return;
            }

            // 3) Active Screen 자체 처리
            if (_activeScreen != null && _activeScreen.HandleBackRequested())
                return;

            // 4) Root back rule
            for (int i = 0; i < rootBackRules.Count; i++)
            {
                var rule = rootBackRules[i];
                if (rule.from.Equals(_activeScreenId))
                {
                    ReplaceScreen(rule.to, payload: null, clearStacks: rule.clearStacks);
                    return;
                }
            }

            Debug.Log("[UIManager] Back requested at root screen: " + _activeScreenId + " (no rule)");
        }
    }
}
