using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using KingdomIdle.UI;
using Scripts.Core;
using Scripts.Core.Manager;

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
        internal RectTransform HudTop => _activeScreenGo != null ? _activeScreenGo.transform.Find("HudTop") as RectTransform : null;

        // ── 패널 스택 ──
        private struct PanelEntry
        {
            public UIPanelId Id;
            public bool IsTab;
            public GameObject Go;
            public BottomSheetView View;
            public Vector2 SheetRestingPos;   // 시트 안착 위치 — 중단된 슬라이드 트윈 원복 기준

            public PanelEntry(UIPanelId id, bool isTab, GameObject go, BottomSheetView view)
            {
                Id = id;
                IsTab = isTab;
                Go = go;
                View = view;
                SheetRestingPos = Vector2.zero;
            }
        }

        private readonly Stack<PanelEntry> _panelStack = new();

        public bool HasActiveTabPanel { get; private set; }
        public UIPanelId ActiveTabPanelId { get; private set; }
        public bool HasBlockingPanel =>
            _panelStack.Count > 0 && !_panelStack.Peek().IsTab;

        /// <summary>온라인 전투 시간에서 제외할 메뉴·설정·로딩 상태를 한 곳에서 제공한다.</summary>
        public bool BlocksQuestBattleTime => HasBlockingPanel || HasActiveTabPanel || ModalBackHandler.HasOpenModal ||
            (_settings != null && _settings.IsOpen) || (_loading != null && _loading.gameObject.activeInHierarchy);

        /// <summary>패널 스택이 변할 때마다 발생 — 탭 선택 시각화, 파티 HUD 위치 갱신용.</summary>
        public event Action PanelStackChanged;

        public FeatureGuideTargetRegistry GuideTargets { get; } = new();
        internal FeatureGuideView ActiveFeatureGuide { get; set; }
        private float _guidePanelReadyAt;

        /// <summary>단계가 허용한 메뉴·상세 창만 통과시킨다. 로딩·설정·무관한 모달이 있으면 입력을 풀고 대기한다.</summary>
        internal bool CanRunFeatureGuide(Direction.GameDirectStep step)
        {
            if (!step.Interactive) return CanShowFeatureGuide;
            // 시트가 화면 밖에서 올라오는 동안 실제 버튼이 마스크 밖에 있을 수 있다. 전투 시간은 유지하고 표시만 기다린다.
            if (Time.unscaledTime < _guidePanelReadyAt) return false;
            if (_activeScreenId != UIScreenId.Main || _activeScreenGo == null || HasBlockingPanel ||
                (_loading != null && _loading.gameObject.activeInHierarchy) || (_settings != null && _settings.IsOpen) ||
                (_mainController != null && _mainController.HasOpenDropdown)) return false;
            var context = Direction.GameDirectInteraction.Clicked ? step.Destination : step.Context;
            var panel = GuidePanel(context);
            if (HasActiveTabPanel && (context == Direction.GuideContext.Main || context == Direction.GuideContext.MageTower || ActiveTabPanelId != panel)) return false;
            bool Expected(GameObject go) =>
                (go.GetComponent<MageTowerEquipPopupView>() != null && context == Direction.GuideContext.MageTower) ||
                (go.GetComponent<DungeonDifficultyPopupView>() != null && panel == UIPanelId.Dungeon && context != Direction.GuideContext.Main) ||
                (go.GetComponent<GachaResultPopupView>() != null && step.Completion == Direction.GuideCompletion.Action &&
                    (step.Action == Direction.GuideAction.EquipmentPullOnce || step.Action == Direction.GuideAction.SkillPullOnce));
            if (ModalBackHandler.HasUnexpectedModal(Expected)) return false;
            if (layerPopups != null)
                for (int i = 0; i < layerPopups.childCount; i++)
                {
                    var child = layerPopups.GetChild(i);
                    if (child.gameObject.activeInHierarchy && !child.TryGetComponent<PartyHudView>(out _) && !Expected(child.gameObject)) return false;
                }
            return true;
        }

        /// <summary>안내 문맥을 기존 패널 ID로 변환한다. Main은 패널이 없으므로 반환값을 열기에 사용하지 않는다.</summary>
        private static UIPanelId GuidePanel(Direction.GuideContext context) => context switch
        {
            Direction.GuideContext.Development => UIPanelId.Development,
            Direction.GuideContext.ArmyCharacter or Direction.GuideContext.ArmyEquipment or Direction.GuideContext.ArmyJobs or Direction.GuideContext.ArmyJobDetail => UIPanelId.KingdomArmy,
            Direction.GuideContext.Dungeon or Direction.GuideContext.GoldDungeonDetail or Direction.GuideContext.RubyDungeonDetail => UIPanelId.Dungeon,
            _ => UIPanelId.Gacha
        };

        /// <summary>미확인 단계의 필요 화면을 재개한다. 외부 창이 있을 때는 열거나 닫지 않고 사용자의 처리를 기다린다.</summary>
        internal void PrepareFeatureGuide(Direction.GameDirectStep step)
        {
            if (!step.Interactive || !CanRunFeatureGuide(step)) return;
            var context = Direction.GameDirectInteraction.Clicked ? step.Destination : step.Context;
            if (context == Direction.GuideContext.Main) return;
            // 마탑은 하단 패널이 아닌 기존 오버레이 팝업이다. 미확인 설명을 재개할 때만 다시 연다.
            if (context == Direction.GuideContext.MageTower)
            {
                if (!MageTowerPopupController.IsOpen) MageTowerPopupController.Show();
                return;
            }
            if (!HasActiveTabPanel) PushPanel(GuidePanel(context), isTabPanel: true);
            switch (context)
            {
                case Direction.GuideContext.Development: DevelopmentPanelController.PrepareGuide(); break;
                case Direction.GuideContext.ArmyCharacter:
                case Direction.GuideContext.ArmyEquipment:
                case Direction.GuideContext.ArmyJobs:
                case Direction.GuideContext.ArmyJobDetail: KingdomArmyPanelController.PrepareGuide(context); break;
                case Direction.GuideContext.EquipmentGacha: GachaPanelController.PrepareGuide(false); break;
                case Direction.GuideContext.SkillGacha: GachaPanelController.PrepareGuide(true); break;
                default:
                    if (_panelStack.Count > 0) _panelStack.Peek().Go.GetComponent<DungeonPanelController>()?.PrepareGuide(context);
                    break;
            }
        }

        /// <summary>실제 목적 화면이 열린 것을 확인한다. 탐색 버튼 클릭만으로 실패한 화면 이동을 완료 처리하지 않는다.</summary>
        internal bool IsGuideContextReady(Direction.GuideContext context)
        {
            if (context == Direction.GuideContext.Main) return !HasActiveTabPanel;
            if (context == Direction.GuideContext.MageTower) return MageTowerPopupController.IsOpen && !HasActiveTabPanel;
            if (!HasActiveTabPanel || ActiveTabPanelId != GuidePanel(context)) return false;
            switch (context)
            {
                case Direction.GuideContext.Development: return DevelopmentPanelController.GuideReady;
                case Direction.GuideContext.ArmyCharacter:
                case Direction.GuideContext.ArmyEquipment:
                case Direction.GuideContext.ArmyJobs:
                case Direction.GuideContext.ArmyJobDetail: return KingdomArmyPanelController.GuideReady(context);
                case Direction.GuideContext.EquipmentGacha: return GachaPanelController.GuideReady(false);
                case Direction.GuideContext.SkillGacha: return GachaPanelController.GuideReady(true);
                default:
                    var detail = _panelStack.Peek().Go.GetComponentInChildren<DungeonDifficultyPopupView>();
                    return context == Direction.GuideContext.Dungeon ? detail == null : detail != null &&
                        detail.GuideType == (context == Direction.GuideContext.GoldDungeonDetail ? eStageType.GoldDungeon : eStageType.RubyDungeon);
            }
        }

        /// <summary>정상 완료·건너뛰기 시 안내의 메뉴를 정리한다. 무관한 모달이 덮인 상태에서는 호출하지 않는다.</summary>
        internal void FinishFeatureGuide()
        {
            GachaResultPopupController.Close();
            MageTowerPopupController.Hide();
            if (HasActiveTabPanel && !HasBlockingPanel) ClearPanels();
        }

        /// <summary>안내를 덮을 메뉴·로딩·실제 팝업을 검사한다. 안내 View 자체는 팝업 계층 밖에 있어 자기 차단이 없다.</summary>
        public bool CanShowFeatureGuide
        {
            get
            {
                if (_activeScreenId != UIScreenId.Main || _activeScreenGo == null ||
                    HasBlockingPanel || HasActiveTabPanel || ModalBackHandler.HasOpenModal ||
                    (_loading != null && _loading.gameObject.activeInHierarchy) ||
                    (_settings != null && _settings.IsOpen) || (_mainController != null && _mainController.HasOpenDropdown)) return false;
                if (layerPopups != null)
                    for (int i = 0; i < layerPopups.childCount; i++)
                    {
                        var child = layerPopups.GetChild(i);
                        // 파티 HUD는 렌더 순서 때문에 팝업 레이어에 있지만, 안내를 중단시키는 모달은 아니다.
                        if (child.gameObject.activeInHierarchy && !child.TryGetComponent<PartyHudView>(out _)) return false;
                    }
                return true;
            }
        }

        /// <summary>매 프레임 발생 — 화면 컨트롤러(재화 폴링 등)가 구독.</summary>
        internal event Action FrameTick;

        // ── 오버레이 ──
        private LoadingOverlayView _loading;
        private ToastView _toast;
        private Coroutine _toastCo;
        private int _fieldLootCount;
        private string _fieldLootName;
        private float _nextFieldLootNotice;
        private bool _fieldLootToast;
        private SettingsModalController _settings;
        private StageManager _boundStageManager;

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
            GameAudioSettings.Changed += ApplyEffectsVolume;
            GamePresentationSettings.Changed += ApplyLootSettings;

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
            GamePresentationSettings.Apply();
            NumberNotation.Load();
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
            GameAudioSettings.Apply();
        }

        private void ApplyEffectsVolume() { if (_uiAudioSource != null) _uiAudioSource.volume = GameAudioSettings.Gain(SoundChannel.Interface); }

        private void Update()
        {
            EnsureStageManagerBinding();

            if (Input.GetKeyDown(KeyCode.Escape))
                RequestBack();

            FrameTick?.Invoke();
            if (_fieldLootCount > 0 && !GamePresentationSettings.HideItemNotifications && _toastCo == null &&
                Time.unscaledTime >= _nextFieldLootNotice && !HasBlockingPanel && !HasActiveTabPanel && (_settings == null || !_settings.IsOpen))
            {
                ShowToast(_fieldLootCount == 1 ? $"장비 획득 · {_fieldLootName}" : $"사냥 장비 {_fieldLootCount}개 획득");
                _fieldLootToast = true; _fieldLootCount = 0; _nextFieldLootNotice = Time.unscaledTime + 6;
            }
        }

        private void OnDestroy()
        {
            GameAudioSettings.Changed -= ApplyEffectsVolume;
            GamePresentationSettings.Changed -= ApplyLootSettings;
            BindStageManager(null);
            DungeonClearPopupController.Hide();
            ReincarnationPopupController.Hide();
            OfflineRewardPopupController.Hide();
            if (Instance == this) Instance = null;
        }

        private void EnsureStageManagerBinding()
        {
            if (_boundStageManager != StageManager.Instance)
                BindStageManager(StageManager.Instance);
        }

        private void BindStageManager(StageManager stageManager)
        {
            if (_boundStageManager != null)
            {
                _boundStageManager.OnStageCleared -= HandleStageCleared;
                _boundStageManager.OnStageEnter -= HandleStageEntered;
            }

            _boundStageManager = stageManager;

            if (_boundStageManager != null)
            {
                _boundStageManager.OnStageCleared += HandleStageCleared;
                _boundStageManager.OnStageEnter += HandleStageEntered;
            }
        }

        private static void HandleStageEntered(StageDefinition definition) => DungeonClearPopupController.Hide();

        private static void HandleStageCleared(
            StageDefinition definition)
        {
            if (definition != null &&
                definition.Type != eStageType.Main)
            {
                DungeonClearPopupController.Show(definition);
            }
        }

        // ═══════════════════════════════════════════
        //  화면 (Screens)
        // ═══════════════════════════════════════════

        /// <summary>기존 화면의 안내 입력을 즉시 해제하고 화면을 교체한다. 새 화면은 컨트롤러 Bind에서 안내 대상을 다시 등록한다.</summary>
        public void ReplaceScreen(UIScreenId id, object payload = null, bool clearStacks = true)
        {
            ActiveFeatureGuide?.Hide();
            if (clearStacks)
                ClearPanels();

            GachaResultPopupController.Close();
            DungeonClearPopupController.Hide();
            ReincarnationPopupController.Hide();
            OfflineRewardPopupController.Hide();
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

        /// <summary>지정 패널을 스택에 열고 기존 바인딩·슬라이드를 실행한다. 안내는 등장 완료까지 기다리고 닫힘 정리는 기존 스택이 소유한다.</summary>
        public void PushPanel(UIPanelId id, object payload = null, bool clearBefore = false, bool isTabPanel = false)
        {
#if LOBBY_DEVICE_QA && DEVELOPMENT_BUILD
            var panelTimer = System.Diagnostics.Stopwatch.StartNew();
#endif
            if (id == UIPanelId.Guide || id == UIPanelId.Inventory) isTabPanel = false;
            if (clearBefore)
                ClearPanels();

            if (_panelStack.Count > 0)
                _panelStack.Peek().Go.SetActive(false);

            var go = CreatePanel(id, payload, out var view);
#if LOBBY_DEVICE_QA && DEVELOPMENT_BUILD
            Debug.Log($"[PanelTiming] {id} create {panelTimer.Elapsed.TotalMilliseconds:F2} ms");
#endif
            if (go == null) return;

            var entry = new PanelEntry(id, isTabPanel, go, view);
            if (view != null && view.sheet != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(view.sheet);
                entry.SheetRestingPos = view.sheet.anchoredPosition;
            }
#if LOBBY_DEVICE_QA && DEVELOPMENT_BUILD
            Debug.Log($"[PanelTiming] {id} layout {panelTimer.Elapsed.TotalMilliseconds:F2} ms");
#endif
            _panelStack.Push(entry);
            BindPanelCommon(go, view);
            RefreshActiveTabPanelState();
#if LOBBY_DEVICE_QA && DEVELOPMENT_BUILD
            Debug.Log($"[PanelTiming] {id} state {panelTimer.Elapsed.TotalMilliseconds:F2} ms");
#endif

            if (view != null && view.sheet != null)
            {
                // 시트 전체가 하단 탭바 뒤에서 떠오르는 슬라이드 인 (셸의 SheetClip이 탭바 영역을 가린다)
                float rise = Mathf.Max(240f, view.sheet.rect.height);
                if (view.centeredModal) UITween.PopIn(view.sheet);
                else UITween.SlideUp(view.sheet, rise, 0.34f);
                _guidePanelReadyAt = Time.unscaledTime + 0.36f;
                if (view.backdrop != null)
                {
                    var cg = view.backdrop.GetComponent<CanvasGroup>();
                    if (cg == null) cg = view.backdrop.gameObject.AddComponent<CanvasGroup>();
                    cg.alpha = 0f;
                    UITween.FadeIn(cg, 0.22f);
                }
            }

            PanelStackChanged?.Invoke();

            if (catalog != null && catalog.panelOpenSfx != null)
                _uiAudioSource.PlayOneShot(catalog.panelOpenSfx);
        }

        public void PopPanel()
        {
            if (_panelStack.Count == 0) return;

            var top = _panelStack.Pop();
            NotifyPanelClosed(top.View);
            AnimatePanelCloseAndDestroy(top);

            if (_panelStack.Count > 0)
                ReactivatePanel(_panelStack.Peek());

            RefreshActiveTabPanelState();
            PanelStackChanged?.Invoke();

            if (catalog != null && catalog.panelCloseSfx != null)
                _uiAudioSource.PlayOneShot(catalog.panelCloseSfx);
        }

        public void ClearPanels()
        {
            bool changed = _panelStack.Count > 0;
            bool first = true;   // 최상단(보이는 패널)만 퇴장 연출, 아래 깔린 비활성 패널은 즉시 파괴

            while (_panelStack.Count > 0)
            {
                var entry = _panelStack.Pop();
                NotifyPanelClosed(entry.View);
                if (first) { AnimatePanelCloseAndDestroy(entry); first = false; }
                else if (entry.Go != null) Destroy(entry.Go);
            }

            RefreshActiveTabPanelState();
            if (changed) PanelStackChanged?.Invoke();
        }

        /// <summary>스택 재노출 — 중단된 트윈으로 어긋난 시트/딤을 원상 복구 후 짧게 재등장.</summary>
        private static void ReactivatePanel(PanelEntry next)
        {
            if (next.Go == null) return;
            next.Go.SetActive(true);
            if (next.View == null) return;

            // 퀘스트 이동으로 같은 목적지가 중첩되면 정적 컨트롤러가 새 인스턴스를 가리킨다.
            // 기존 바인딩이 그대로인 일반 복귀는 컨트롤러 내부에서 건너뛴다.
            if (next.View is GachaPanelView gacha) GachaPanelController.Restore(gacha);
            else if (next.View is KingdomArmyPanelView army) KingdomArmyPanelController.Restore(army);
            else if (next.View is DevelopmentPanelView development) DevelopmentPanelController.Restore(development);
            else if (next.View is InventoryPanelView inventory) InventoryPanelController.Restore(inventory);

            // 위에 패널이 쌓이며 SetActive(false) 로 죽은 딤 페이드가 중간 알파로 얼어붙어 있을 수 있다
            if (next.View.backdrop != null)
            {
                var cg = next.View.backdrop.GetComponent<CanvasGroup>();
                if (cg != null) cg.alpha = 1f;
            }

            if (next.View.sheet != null)
            {
                next.View.sheet.anchoredPosition = next.SheetRestingPos;
                if (next.View.centeredModal) UITween.PopIn(next.View.sheet);
                else UITween.SlideUp(next.View.sheet, 120f, 0.22f);
            }
        }

        /// <summary>
        /// 시트를 아래로 밀어내며 페이드 아웃 후 파괴. 코루틴은 UIManager(영속)에서 구동 —
        /// 죽는 패널 오브젝트 위 코루틴은 SetActive(false)에 죽어 파괴가 누락될 수 있다.
        /// </summary>
        private void AnimatePanelCloseAndDestroy(PanelEntry top)
        {
            if (top.Go == null) return;
            // 스택에서 빠져도 0.2초간 퇴장 패널이 전장을 덮는다. 다음 안내의 실제 대상이 드러난 뒤 입력을 연다.
            _guidePanelReadyAt = Mathf.Max(_guidePanelReadyAt, Time.unscaledTime + .22f);
            if (top.View == null || top.View.sheet == null || !top.Go.activeInHierarchy)
            {
                Destroy(top.Go);
                return;
            }

            // 등장 슬라이드가 아직 돌고 있으면 먼저 끈다 — 두 코루틴이 같은 anchoredPosition 을
            // 매 프레임 덮어쓰면 시트가 내려가지 않고 위로 끌려 올라가다 사라진다(빠른 탭 전환).
            UITween.StopMove(top.View.sheet);

            var cg = top.Go.GetComponent<CanvasGroup>();
            if (cg == null) cg = top.Go.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;   // 퇴장 중 입력 차단
            cg.interactable = false;
            RunCoroutine(PanelCloseRoutine(top.Go, top.View.sheet, cg, top.View.centeredModal));
        }

        private static IEnumerator PanelCloseRoutine(GameObject go, RectTransform sheet, CanvasGroup rootGroup, bool modal)
        {
            Vector2 from = sheet.anchoredPosition;
            Vector2 to = modal ? from : from + new Vector2(0f, -Mathf.Max(240f, sheet.rect.height));
            const float dur = 0.20f;
            float e = 0f;
            while (e < dur && go != null && sheet != null)
            {
                e += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(e / dur);
                k = k * k * k;   // EaseInCubic — 가속 퇴장
                sheet.anchoredPosition = Vector2.LerpUnclamped(from, to, k);
                if (rootGroup != null) rootGroup.alpha = 1f - k;
                yield return null;
            }
            if (go != null) Destroy(go);
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
                case UIPanelId.Dungeon: prefab = catalog.panelDungeon; break;
                default: prefab = catalog.panelPlaceholder; break;
            }

            if (prefab == null) prefab = catalog.panelPlaceholder;
            if (prefab == null)
            {
                Debug.LogError($"[UIManager] '{id}' 패널 프리팹이 카탈로그에 없습니다. 생성기를 실행하세요.");
                return null;
            }

            var go = Instantiate(prefab, id == UIPanelId.Guide || id == UIPanelId.Inventory ? layerOverlays : layerPanels, false);
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
                    case UIPanelId.Dungeon when view is DungeonPanelView:
                        // 던전 패널은 루트의 DungeonPanelController가 OnEnable에서 스스로 구성한다.
                        // 셸 제목("던전")을 placeholder 제목으로 덮지 않도록 여기서 끝낸다.
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

        /// <summary>Android 뒤로가기와 공통 뒤로가기 요청을 처리한다. 보이는 안내는 건너뛰고, 가려진 안내 대신 현재 모달을 먼저 닫는다.</summary>
        public void RequestBack()
        {
            // 현재 보이는 안내만 건너뛴다. 로딩/팝업이 가린 안내는 기존 모달의 뒤로가기를 가로채지 않는다.
            if (ActiveFeatureGuide != null && ActiveFeatureGuide.HandleBack()) return;
            if (ModalBackHandler.TryCloseTop()) return;
            if (OfflineRewardPopupController.IsOpen)
            {
                OfflineRewardPopupController.Hide();
                return;
            }

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
                // 패널 내부 전체화면 모달(던전 난이도 팝업)이 떠 있으면 패널 전체가 아니라
                // 모달만 닫는다 — 백드롭/X 버튼과 같은 계층 순서의 뒤로가기.
                var topGo = _panelStack.Peek().Go;
                if (topGo != null)
                {
                    var dungeonModal = topGo.GetComponentInChildren<DungeonDifficultyPopupView>(false);
                    if (dungeonModal != null && dungeonModal.gameObject.activeInHierarchy)
                    {
                        dungeonModal.Hide();
                        return;
                    }
                }

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

        /// <summary>로딩 표시 중에는 안내 입력을 즉시 해제한다. 로딩 종료 후 같은 단계 재개는 Player의 준비 검사에 맡긴다.</summary>
        public void SetLoading(bool visible, string message = "Loading...")
        {
            if (_loading == null) return;

            if (visible)
            {
                ActiveFeatureGuide?.Hide();
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
            _fieldLootToast = false;

            if (_toast.label != null) _toast.label.text = message;
            _toast.gameObject.SetActive(true);
            _toast.transform.SetAsLastSibling();

            if (_toastCo != null) StopCoroutine(_toastCo);
            _toastCo = StartCoroutine(HideToastAfter(1.5f));
        }

        internal void NotifyFieldEquipment(int code)
        {
            if (GamePresentationSettings.HideItemNotifications) return;
            _fieldLootCount++;
            _fieldLootName = EquipmentManager.Instance?.GetData(code)?.DisplayName ?? "장비";
        }
        private void ApplyLootSettings()
        {
            if (!GamePresentationSettings.HideItemNotifications) return;
            _fieldLootCount = 0;
            if (!_fieldLootToast) return;
            if (_toastCo != null) StopCoroutine(_toastCo);
            if (_toast != null) _toast.gameObject.SetActive(false);
            _toastCo = null; _fieldLootToast = false;
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
