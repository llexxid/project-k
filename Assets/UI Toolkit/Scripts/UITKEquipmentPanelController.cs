using System;
using Scripts.Users;
using UnityEngine;
using UnityEngine.UIElements;

namespace KingdomIdle.UIToolkit
{
    /// <summary>
    /// 장비 패널 UI Controller.
    /// EquipmentManager의 이벤트를 구독해 UI를 갱신하고,
    /// UI 버튼 클릭을 EquipmentManager 메서드 호출로 연결한다.
    ///
    /// [구조 요약]
    ///   Bridge.Init()  →  Init()  →  이벤트 구독 시작
    ///   EquipmentManager 이벤트 발생  →  Refresh*() 호출  →  VisualElement 갱신
    ///   UI 버튼 클릭  →  EquipmentManager.Equip/Unequip/TryEnhance/TrySynthesize 호출
    ///
    /// [UI 제작 시 채워야 할 항목]
    ///   - BuildPanelTree()   : VisualElement 트리 직접 조립 또는 UXML 로드
    ///   - RefreshSlot()      : 착용 슬롯 아이콘 갱신
    ///   - RefreshInventory() : 인벤토리 목록 갱신
    ///   - ShowDropNotify()   : 드롭 알림 표시 (Toast 등)
    ///   - ShowEnhanceResult(): 강화 결과 표시
    ///   - ShowSynthResult()  : 합성 결과 표시
    /// </summary>
    [DefaultExecutionOrder(-930)]
    public sealed class UITKEquipmentPanelController : MonoBehaviour
    {
        public static UITKEquipmentPanelController Instance { get; private set; }

        private EquipmentManager equipManager;
        // ── UIToolkit ─────────────────────────────────────────────────
        private UIDocument    _uiDocument;
        private VisualElement _panelRoot;   // 패널 최상위 요소 (BuildPanelTree에서 생성)

        // ─────────────────────────────────────────────────────────────
        private bool _ensured;
        private bool _subscribed;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Init();
        }


        private void OnDestroy()
        {
            UnsubscribeEvents();
        }

        // ═══════════════════════════════════════════════════════════════
        //  초기화 (Bridge에서 호출)
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// UITKEquipmentPanelBridge.Init()에서 호출.
        /// Player.Start() 이후에 불려야 EquipmentManager가 준비된 상태다.
        /// </summary>
        public void Init()
        {
            if (_ensured && _subscribed) return;

            EnsureUiDocument();
            SubscribeEvents();
        }

        private void EnsureUiDocument()
        {
            if (_uiDocument != null) return;

            if (UITKUIManager.Instance != null)
                _uiDocument = UITKUIManager.Instance.GetComponent<UIDocument>();

            if (_uiDocument == null)
                _uiDocument = FindFirstObjectByType<UIDocument>();
            _ensured = true;
        }

        // ═══════════════════════════════════════════════════════════════
        //  이벤트 구독 / 해제
        // ═══════════════════════════════════════════════════════════════

        private void SubscribeEvents()
        {
            if (EquipmentManager.Instance == null) return;
            equipManager = EquipmentManager.Instance;
            
            equipManager.OnEquipped       += OnEquipped;
            equipManager.OnUnequipped     += OnUnequipped;
            equipManager.OnItemDropped    += OnItemDropped;
            equipManager.OnEnhanced       += OnEnhanced;
            equipManager.OnEnhanceFailed  += OnEnhanceFailed;
            equipManager.OnSynthesized    += OnSynthesized;
            _subscribed = true;
        }

        private void UnsubscribeEvents()
        {
            if (EquipmentManager.Instance == null) return;
            equipManager = EquipmentManager.Instance;

            equipManager.OnEquipped       -= OnEquipped;
            equipManager.OnUnequipped     -= OnUnequipped;
            equipManager.OnItemDropped    -= OnItemDropped;
            equipManager.OnEnhanced       -= OnEnhanced;
            equipManager.OnEnhanceFailed  -= OnEnhanceFailed;
            equipManager.OnSynthesized    -= OnSynthesized;
        }

        // ═══════════════════════════════════════════════════════════════
        //  이벤트 핸들러 → UI 갱신 진입점
        // ═══════════════════════════════════════════════════════════════

        /// <summary>장비 착용 시 호출 — 해당 슬롯 아이콘 갱신</summary>
        private void OnEquipped(Player player, eEquipmentSlot slot, EquipmentInstance equip)
        {
            RefreshSlot(slot, equip);
            RefreshInventory();
        }

        /// <summary>장비 해제 시 호출 — 해당 슬롯 비우기</summary>
        private void OnUnequipped(Player playere, eEquipmentSlot slot, EquipmentInstance equip)
        {
            RefreshSlot(slot, null);
            RefreshInventory();
        }

        /// <summary>드롭 획득 시 호출 — 알림 표시 + 인벤토리 갱신</summary>
        private void OnItemDropped(EquipmentInstance instance)
        {
            ShowDropNotify(instance);
            RefreshInventory();
        }

        /// <summary>강화 성공 시 호출 — 슬롯/인벤토리 스탯 수치 갱신</summary>
        private void OnEnhanced(EquipmentInstance instance)
        {
            RefreshSlot(instance.baseData.slot, instance);
            RefreshInventory();
            ShowEnhanceResult(instance, success: true);
        }

        /// <summary>강화 실패 시 호출 — 인벤토리 갱신 + 실패 알림 표시</summary>
        private void OnEnhanceFailed(EquipmentInstance instance)
        {
            RefreshInventory();
            ShowEnhanceResult(instance, success: false);
        }

        /// <summary>합성 완료 시 호출 — 인벤토리 갱신 + 결과 표시</summary>
        private void OnSynthesized(EquipmentInstance result)
        {
            RefreshInventory();
            ShowSynthResult(result);
        }

        // ═══════════════════════════════════════════════════════════════
        //  UI 갱신 메서드 (여기에 실제 VisualElement 코드를 작성)
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// 착용 슬롯 UI를 갱신한다.
        /// instance == null 이면 빈 슬롯으로 표시.
        ///
        /// [작성 가이드]
        ///   var slotEl = _panelRoot.Q<VisualElement>($"EquipSlot_{slot}");
        ///   slotEl.style.backgroundImage = instance != null
        ///       ? new StyleBackground(instance.baseData.icon) : StyleKeyword.None;
        ///   // 강화 레벨 라벨: instance?.enhancementLevel
        ///   // 등급 색상 테두리: instance?.baseData.rarity
        /// </summary>
        private void RefreshSlot(eEquipmentSlot slot, EquipmentInstance instance)
        {
            // TODO: VisualElement 갱신 코드 작성
        }

        /// <summary>
        /// 인벤토리 목록 전체를 다시 그린다.
        ///
        /// [작성 가이드]
        ///   var items = _equipMgr.Inventory.Items;
        ///   foreach (var item in items)
        ///   {
        ///       // 아이템 카드 VisualElement 생성 및 등록
        ///       // 클릭 시 OnInventoryItemClicked(item) 호출
        ///   }
        /// </summary>
        private void RefreshInventory()
        {
            // TODO: 인벤토리 목록 갱신 코드 작성
        }

        /// <summary>
        /// 드롭 획득 알림을 표시한다.
        ///
        /// [작성 가이드]
        ///   Toast 또는 별도 알림 VisualElement로 아이템 이름·등급을 표시.
        ///   instance.baseData.equipmentName, instance.baseData.rarity
        /// </summary>
        private void ShowDropNotify(EquipmentInstance instance)
        {
            // TODO: 드롭 알림 UI 코드 작성
        }

        /// <summary>
        /// 강화 결과를 표시한다.
        /// success=true이면 성공 연출, false이면 실패 알림.
        ///
        /// [작성 가이드]
        ///   instance.enhancementLevel, instance.GetFinalAtk(), instance.GetFinalMaxHP()
        /// </summary>
        private void ShowEnhanceResult(EquipmentInstance instance, bool success)
        {
            // TODO: 강화 성공/실패 결과 표시 코드 작성
            // 예) success ? "강화 성공!" : "강화 실패..."
        }

        /// <summary>
        /// 합성 완료 결과를 표시한다 (획득한 장비 이름·등급 등).
        ///
        /// [작성 가이드]
        ///   result.baseData.equipmentName
        ///   result.baseData.rarity
        ///   result.baseData.icon
        /// </summary>
        private void ShowSynthResult(EquipmentInstance result)
        {
            // TODO: 합성 결과 표시 코드 작성
        }

        // ═══════════════════════════════════════════════════════════════
        //  UI 버튼 → EquipmentManager 호출 (버튼 콜백에서 사용)
        // ═══════════════════════════════════════════════════════════════
        

        /// <summary>
        /// 강화 버튼 클릭.
        ///   btn.clicked += () => OnEnhanceBtnClicked(selectedItem);
        /// </summary>
        public void OnEnhanceBtnClicked(EquipmentInstance item)
        {
            if (!equipManager.CanEnhance(item))
            {
                Debug.Log("[EquipmentUI] 강화 불가 — 재료 부족 또는 최대 레벨");
                return;
            }
            equipManager.TryEnhance(item);
        }

        /// <summary>
        /// 합성 버튼 클릭. 인벤토리에서 선택된 3개를 넘긴다.
        ///   btn.clicked += () => OnSynthBtnClicked(selectedItems);
        /// </summary>
        public void OnSynthBtnClicked(System.Collections.Generic.List<EquipmentInstance> materials)
        {
            var result = equipManager.TrySynthesize(materials);
            if (result == null)
                Debug.Log("[EquipmentUI] 합성 실패 — 재료 조건 미충족");
        }

        // ═══════════════════════════════════════════════════════════════
        //  패널 열기 / 닫기
        // ═══════════════════════════════════════════════════════════════

        public void OpenPanel()
        {
            EnsureUiDocument();
            BuildPanelTree();

            if (_panelRoot != null)
                _panelRoot.style.display = DisplayStyle.Flex;
        }

        public void ClosePanel()
        {
            if (_panelRoot != null)
                _panelRoot.style.display = DisplayStyle.None;
        }

        /// <summary>
        /// 패널 VisualElement 트리를 구성한다.
        /// UXML을 로드하거나 코드로 직접 조립한다.
        ///
        /// [UXML 사용 시 예시]
        ///   var uxml = Resources.Load<VisualTreeAsset>("UI/EquipmentPanel");
        ///   _panelRoot = uxml.Instantiate();
        ///   _uiDocument.rootVisualElement.Q("Layer_Panels").Add(_panelRoot);
        ///   // 이후 Q<Button>, Q<VisualElement> 로 요소를 찾아 이벤트 등록
        ///
        /// [코드 조립 시 예시]
        ///   _panelRoot = new VisualElement { name = "EquipmentPanel" };
        ///   ...
        /// </summary>
        private void BuildPanelTree()
        {
            if (_panelRoot != null && _panelRoot.panel != null) return; // 이미 구성됨

            // TODO: UXML 로드 또는 VisualElement 코드 조립
            // _panelRoot = ...;
        }
    }
}
