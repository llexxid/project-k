using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using KingdomIdle.UI;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// UGUI 중앙 UI 매니저. 기존 UITKUIManager와 동일한 public API를 유지한다.
    /// 화면(Screens)/패널(Panels)/팝업(Popups)/오버레이(Overlays) 4레이어 구조,
    /// 패널 스택, 토스트, 로딩 오버레이, 설정 모달, 뒤로가기 우선순위 처리를 담당.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [Header("Scene References")]
        [SerializeField] internal UIViewCatalog catalog;
        [SerializeField] internal RectTransform layerScreens;
        [SerializeField] internal RectTransform layerPanels;
        [SerializeField] internal RectTransform layerPopups;
        [SerializeField] internal RectTransform layerOverlays;

        [Header("Behaviour")]
        [SerializeField] private bool dontDestroyOnLoad = true;

        public UIViewCatalog Catalog => catalog;
        public RectTransform LayerScreens => layerScreens;
        public RectTransform LayerPanels => layerPanels;
        public RectTransform LayerPopups => layerPopups;
        public RectTransform LayerOverlays => layerOverlays;

        private AudioSource _uiAudioSource;

        // ── 화면 상태 ──
        private UIScreenId _activeScreenId;
        private GameObject _activeScreenGo;
        private TitleScreenController _titleController;
        private MainScreenController _mainController;

        public UIScreenId ActiveScreenId => _activeScreenId;
        internal MainScreenController ActiveMain => _mainController;

        // ── 패널 스택 ──
        private struct PanelEntry
        {
            public UIPanelId Id;
            public bool IsTab;
            public GameObject Go;
            public BottomSheetView View;

            public PanelEntry(UIPanelId id, bool isTab, GameObject go, BottomSheetView view)
            {
                Id = id;
                IsTab = isTab;
                Go = go;
                View = view;
            }
        }

        private readonly Stack<PanelEntry> _panelStack = new();

        public bool HasActiveTabPanel { get; private set; }
        public UIPanelId ActiveTabPanelId { get; private set; }

        /// <summary>패널 스택이 변할 때마다 발생 — 탭 선택 시각화, 파티 HUD 위치 갱신용.</summary>
        public event Action PanelStackChanged;

        /// <summary>매 프레임 발생 — 화면 컨트롤러(재화 폴링 등)가 구독.</summary>
        internal event Action FrameTick;

        // ── 오버레이 ──
        private LoadingOverlayView _loading;
        private ToastView _toast;
        private Coroutine _toastCo;
        private SettingsModalController _settings;

        // ── PlayerPrefs 키 (기존 값과 호환 유지 — 변경 금지) ──
        internal const string PrefKeyVolume = "settings_masterVolume";
        internal const string PrefKeyMute = "settings_muted";
        internal const string PrefKeyPowerSave = "settings_powerSave";
        internal const string PrefKeyHideItem = "settings_hideItem";
        internal const string PrefKeyDamageText = "settings_damageText";
        internal const string PrefKeyScreenShake = "settings_screenShake";
        internal const string PrefKeyPush = "settings_push";
        internal const string PrefKeyNightPush = "settings_nightPush";

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (dontDestroyOnLoad) DontDestroyOnLoad(gameObject);

            _uiAudioSource = GetComponent<AudioSource>();
            if (_uiAudioSource == null) _uiAudioSource = gameObject.AddComponent<AudioSource>();
            _uiAudioSource.playOnAwake = false;

            if (catalog == null)
                Debug.LogError("[UIManager] UIViewCatalog가 비었습니다. 생성기(KingdomIdle/UGUI/Generate All)를 실행하세요.");

            if (layerScreens == null || layerPanels == null || layerPopups == null || layerOverlays == null)
            {
                Debug.LogError("[UIManager] 레이어 참조가 비었습니다. UGUIRoot 프리팹을 확인하세요.");
                enabled = false;
                return;
            }

            WarnIfLegacyUiToolkitActive();
            BuildOverlays();
            ApplyPersistedAudioSettings();
        }

        /// <summary>UITK 매니저가 같은 씬에 살아있으면 이중 UI 상태 — 에러 로그로 경고.</summary>
        private static void WarnIfLegacyUiToolkitActive()
        {
            var behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            for (int i = 0; i < behaviours.Length; i++)
            {
                var b = behaviours[i];
                if (b != null && b.GetType().Name == "UITKUIManager")
                {
                    Debug.LogError("[UIManager] UITKUIManager가 아직 활성 상태입니다. bootstrap 씬에서 'KingdomIdle/UGUI/Bootstrap/Switch to UGUI'를 실행했는지 확인하세요.");
                    return;
                }
            }
        }

        /// <summary>PlayerPrefs에 저장된 음량/음소거 상태를 게임 시작 시점에 즉시 적용한다.</summary>
        internal static void ApplyPersistedAudioSettings()
        {
            float vol = PlayerPrefs.HasKey(PrefKeyVolume) ? PlayerPrefs.GetFloat(PrefKeyVolume) : 1f;
            bool muted = PlayerPrefs.GetInt(PrefKeyMute, 0) == 1;
            AudioListener.volume = muted ? 0f : Mathf.Clamp01(vol);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
                RequestBack();

            FrameTick?.Invoke();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ═══════════════════════════════════════════
        //  화면 (Screens)
        // ═══════════════════════════════════════════

        public void ReplaceScreen(UIScreenId id, object payload = null, bool clearStacks = true)
        {
            if (clearStacks)
                ClearPanels();

            GachaResultPopupController.Close();
            _settings?.Close();

            _titleController?.Dispose();
            _titleController = null;
            _mainController?.Dispose();
            _mainController = null;

            if (_activeScreenGo != null)
            {
                Destroy(_activeScreenGo);
                _activeScreenGo = null;
            }

            // 마법탑 팝업 등 화면 종속 팝업 잔여물 제거
            MageTowerPopupController.Hide();
            MageTowerDetailPopupController.Hide();

            _activeScreenId = id;

            switch (id)
            {
                case UIScreenId.Title:
                {
                    _activeScreenGo = InstantiateFullStretch(catalog != null ? catalog.screenTitle : null, layerScreens, "Screen_Title");
                    var view = _activeScreenGo != null ? _activeScreenGo.GetComponent<TitleScreenView>() : null;
                    if (view != null)
                    {
                        _titleController = new TitleScreenController();
                        _titleController.Bind(view, this);
                    }
                    break;
                }
                case UIScreenId.Main:
                {
                    _activeScreenGo = InstantiateFullStretch(catalog != null ? catalog.screenMain : null, layerScreens, "Screen_Main");
                    var view = _activeScreenGo != null ? _activeScreenGo.GetComponent<MainScreenView>() : null;
                    if (view != null)
                    {
                        _mainController = new MainScreenController();
                        _mainController.Bind(view, this);
                    }
                    break;
                }
                default:
                    Debug.LogWarning($"[UIManager] '{id}' 화면은 아직 구현되지 않았습니다.");
                    break;
            }

            RefreshActiveTabPanelState();
            PanelStackChanged?.Invoke();
        }

        // ═══════════════════════════════════════════
        //  패널 (bottom sheets)
        // ═══════════════════════════════════════════

        public void PushPanel(UIPanelId id, object payload = null, bool clearBefore = false, bool isTabPanel = false)
        {
            if (clearBefore)
                ClearPanels();

            if (_panelStack.Count > 0)
                _panelStack.Peek().Go.SetActive(false);

            var go = CreatePanel(id, payload, out var view);
            if (go == null) return;

            _panelStack.Push(new PanelEntry(id, isTabPanel, go, view));
            BindPanelCommon(go, view);
            RefreshActiveTabPanelState();

            if (view != null && view.sheet != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(view.sheet);

            PanelStackChanged?.Invoke();

            if (catalog != null && catalog.panelOpenSfx != null)
                _uiAudioSource.PlayOneShot(catalog.panelOpenSfx);
        }

        public void PopPanel()
        {
            if (_panelStack.Count == 0) return;

            var top = _panelStack.Pop();
            NotifyPanelClosed(top.View);
            if (top.Go != null) Destroy(top.Go);

            if (_panelStack.Count > 0)
                _panelStack.Peek().Go.SetActive(true);

            RefreshActiveTabPanelState();
            PanelStackChanged?.Invoke();

            if (catalog != null && catalog.panelCloseSfx != null)
                _uiAudioSource.PlayOneShot(catalog.panelCloseSfx);
        }

        public void ClearPanels()
        {
            bool changed = _panelStack.Count > 0;

            while (_panelStack.Count > 0)
            {
                var entry = _panelStack.Pop();
                NotifyPanelClosed(entry.View);
                if (entry.Go != null) Destroy(entry.Go);
            }

            RefreshActiveTabPanelState();
            if (changed) PanelStackChanged?.Invoke();
        }

        private static void NotifyPanelClosed(BottomSheetView view)
        {
            if (view == null || view.OnClosed == null) return;
            try { view.OnClosed.Invoke(); }
            catch (Exception ex) { Debug.LogException(ex); }
        }

        private GameObject CreatePanel(UIPanelId id, object payload, out BottomSheetView view)
        {
            view = null;
            if (catalog == null) return null;

            GameObject prefab;
            switch (id)
            {
                case UIPanelId.Guide: prefab = catalog.panelGuide; break;
                case UIPanelId.Gacha: prefab = catalog.panelGacha; break;
                case UIPanelId.KingdomArmy: prefab = catalog.panelKingdomArmy; break;
                case UIPanelId.Development: prefab = catalog.panelDevelopment; break;
                case UIPanelId.Inventory: prefab = catalog.panelInventory; break;
                default: prefab = catalog.panelPlaceholder; break;
            }

            if (prefab == null) prefab = catalog.panelPlaceholder;
            if (prefab == null)
            {
                Debug.LogError($"[UIManager] '{id}' 패널 프리팹이 카탈로그에 없습니다. 생성기를 실행하세요.");
                return null;
            }

            var go = Instantiate(prefab, layerPanels, false);
            ForceFullStretch(go);
            view = go.GetComponent<BottomSheetView>();
            PopulatePanel(id, view, payload);
            return go;
        }

        private void PopulatePanel(UIPanelId id, BottomSheetView view, object payload)
        {
            if (view == null) return;

            try
            {
                switch (id)
                {
                    case UIPanelId.Guide when view is GuidePanelView guide:
                        GuidePanelController.Populate(guide, RefreshGuideBadge);
                        return;
                    case UIPanelId.Gacha when view is GachaPanelView gacha:
                        GachaPanelController.Populate(gacha);
                        return;
                    case UIPanelId.KingdomArmy when view is KingdomArmyPanelView army:
                        KingdomArmyPanelController.Populate(army);
                        return;
                    case UIPanelId.Development when view is DevelopmentPanelView dev:
                        DevelopmentPanelController.Populate(dev);
                        return;
                    case UIPanelId.Inventory when view is InventoryPanelView inv:
                        InventoryPanelController.Populate(inv);
                        return;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UIManager] '{id}' 패널 구성 실패: {ex}");
                return;
            }

            ApplyPlaceholderPanelTitle(view, id, payload);
        }

        private void BindPanelCommon(GameObject panelGo, BottomSheetView view)
        {
            if (view == null) return;

            if (view.backdrop != null)
            {
                view.backdrop.onClick.AddListener(() =>
                {
                    if (_panelStack.Count > 0 && _panelStack.Peek().Go == panelGo)
                        PopPanel();
                });
            }

            if (view.closeButton != null)
                view.closeButton.onClick.AddListener(PopPanel);
        }

        private void RefreshActiveTabPanelState()
        {
            HasActiveTabPanel = false;
            ActiveTabPanelId = default;

            foreach (var entry in _panelStack)
            {
                if (entry.IsTab)
                {
                    HasActiveTabPanel = true;
                    ActiveTabPanelId = entry.Id;
                    break;
                }
            }
        }

        /// <summary>현재 최상단 패널 시트 높이(로컬 px). 파티 HUD가 시트 위로 올라앉는 데 사용.</summary>
        public float GetTopSheetHeight()
        {
            if (_panelStack.Count == 0) return 0f;
            var v = _panelStack.Peek().View;
            if (v == null || v.sheet == null) return 0f;
            return v.sheet.rect.height;
        }

        // ═══════════════════════════════════════════
        //  뒤로가기 (우선순위: 가챠 결과 → 설정 → 재화/햄버거 → 패널)
        // ═══════════════════════════════════════════

        public void RequestBack()
        {
            if (GachaResultPopupController.IsOpen)
            {
                GachaResultPopupController.Close();
                return;
            }

            if (_settings != null && _settings.IsOpen)
            {
                _settings.Close();
                return;
            }

            if (_mainController != null && _mainController.HandleBack())
                return;

            if (_titleController != null && _titleController.HandleBack())
                return;

            if (_panelStack.Count > 0)
            {
                PopPanel();
                return;
            }
        }

        // ═══════════════════════════════════════════
        //  로딩 오버레이 / 토스트
        // ═══════════════════════════════════════════

        private void BuildOverlays()
        {
            if (catalog == null) return;

            if (catalog.overlayLoading != null)
            {
                var go = Instantiate(catalog.overlayLoading, layerOverlays, false);
                ForceFullStretch(go);
                _loading = go.GetComponent<LoadingOverlayView>();
                go.SetActive(false);
            }

            if (catalog.overlayToast != null)
            {
                var go = Instantiate(catalog.overlayToast, layerOverlays, false);
                ForceFullStretch(go);
                _toast = go.GetComponent<ToastView>();
                go.SetActive(false);
            }
        }

        public void SetLoading(bool visible, string message = "Loading...")
        {
            if (_loading == null) return;

            if (visible)
            {
                if (_loading.lblLoading != null) _loading.lblLoading.text = message;
                _loading.SetProgress01(0f);
                _loading.gameObject.SetActive(true);
                _loading.transform.SetAsLastSibling();
            }
            else
            {
                _loading.gameObject.SetActive(false);
            }
        }

        public void SetLoadingProgress(float normalized01)
        {
            if (_loading == null) return;
            _loading.SetProgress01(normalized01);
        }

        public void ShowToast(string message)
        {
            if (_toast == null) return;

            if (_toast.label != null) _toast.label.text = message;
            _toast.gameObject.SetActive(true);
            _toast.transform.SetAsLastSibling();

            if (_toastCo != null) StopCoroutine(_toastCo);
            _toastCo = StartCoroutine(HideToastAfter(1.5f));
        }

        private IEnumerator HideToastAfter(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            if (_toast != null) _toast.gameObject.SetActive(false);
            _toastCo = null;
        }

        // ═══════════════════════════════════════════
        //  설정 모달 / 가챠 결과 팝업 / SFX
        // ═══════════════════════════════════════════

        internal void OpenSettings()
        {
            if (_settings == null) _settings = new SettingsModalController();
            _settings.Open(this);
        }

        public void ShowGachaResultPopup(
            List<KingdomIdle.Gacha.GachaRewardEntry> results,
            KingdomIdle.Gacha.GachaTableSO table,
            int lastPullCount)
        {
            GachaResultPopupController.Show(this, results, table, lastPullCount);
        }

        public void CloseGachaResultPopup()
        {
            GachaResultPopupController.Close();
        }

        public void PlayButtonClickSfx()
        {
            if (_uiAudioSource == null || catalog == null || catalog.buttonClickSfx == null) return;
            _uiAudioSource.PlayOneShot(catalog.buttonClickSfx);
        }

        /// <summary>가이드 배지 갱신 — 배지 UI가 제거된 상태라 현재는 no-op (API 호환 유지).</summary>
        public void RefreshGuideBadge()
        {
        }

        internal Coroutine RunCoroutine(IEnumerator routine) => StartCoroutine(routine);

        internal void StopRunningCoroutine(Coroutine co)
        {
            if (co != null) StopCoroutine(co);
        }

        // ═══════════════════════════════════════════
        //  유틸
        // ═══════════════════════════════════════════

        private static GameObject InstantiateFullStretch(GameObject prefab, RectTransform parent, string label)
        {
            if (prefab == null)
            {
                Debug.LogError($"[UIManager] '{label}' 프리팹이 카탈로그에 없습니다. 생성기를 실행하세요.");
                return null;
            }

            var go = Instantiate(prefab, parent, false);
            ForceFullStretch(go);
            return go;
        }

        private static void ForceFullStretch(GameObject go)
        {
            if (go == null) return;
            var rt = go.transform as RectTransform;
            if (rt == null) return;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void ApplyPlaceholderPanelTitle(BottomSheetView view, UIPanelId id, object payload)
        {
            if (view == null) return;

            if (payload is string s && !string.IsNullOrWhiteSpace(s))
            {
                if (s.Equals("developmentPanel", StringComparison.OrdinalIgnoreCase) ||
                    s.Equals("kingdomArmyPanel", StringComparison.OrdinalIgnoreCase) ||
                    s.Equals("gachaPanel", StringComparison.OrdinalIgnoreCase) ||
                    s.Equals("storePanel", StringComparison.OrdinalIgnoreCase) ||
                    s.Equals("dungeonPanel", StringComparison.OrdinalIgnoreCase))
                {
                    view.SetTitle(GetPanelDisplayName(id));
                    return;
                }

                view.SetTitle(s);
                return;
            }

            view.SetTitle(GetPanelDisplayName(id));
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
    }
}
