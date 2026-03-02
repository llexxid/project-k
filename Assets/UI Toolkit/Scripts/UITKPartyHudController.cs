using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using KingdomIdle.UI;

namespace KingdomIdle.UIToolkit
{
    // 하단 탭 위 3인 파티 HUD
    [DefaultExecutionOrder(-940)]
    public sealed class UITKPartyHudController : MonoBehaviour
    {
        public static UITKPartyHudController Instance { get; private set; }

        [Header("Dummy Setup")]
        [SerializeField, Range(0, 3)] private int dummySkillCount0 = 1;
        [SerializeField, Range(0, 3)] private int dummySkillCount1 = 2;
        [SerializeField, Range(0, 3)] private int dummySkillCount2 = 3;

        [Header("Layout")]
        [SerializeField] private float baseGapPx = 12f;      // BottomBar 위 기본 간격
        [SerializeField] private float sheetGapPx = 10f;     // Sheet 위 추가 간격
        [SerializeField] private float fallbackBottomBarPx = 190f;

        private UIDocument _uiDocument;
        private VisualElement _partyHud;
        private VisualElement _layerPanels;
        private VisualElement _bottomBar;

        private readonly MemberUI[] _members = new MemberUI[3];

        private struct MemberUI
        {
            public Button Portrait;
            public VisualElement HpFill;
            public readonly SkillSlot[] Skills;
            public readonly int[] SkillIds;

            public MemberUI(int skillSlots)
            {
                Portrait = null;
                HpFill = null;
                Skills = new SkillSlot[skillSlots];
                SkillIds = new int[skillSlots];
            }
        }

        private struct SkillSlot
        {
            public VisualElement Root;
            public VisualElement Mask;
            public Label CooldownLabel;
            public Coroutine Co;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            for (int i = 0; i < _members.Length; i++)
                _members[i] = new MemberUI(3);
        }

        private void Update()
        {
            EnsureRefs();
            EnsureHudBuilt();
            UpdateHudVisibilityAndPosition();
        }

        private void EnsureRefs()
        {
            if (_uiDocument != null && _uiDocument.rootVisualElement != null) return;

            if (UITKUIManager.Instance != null)
                _uiDocument = UITKUIManager.Instance.GetComponent<UIDocument>();

            if (_uiDocument == null)
                _uiDocument = FindFirstObjectByType<UIDocument>();
        }

        private void EnsureHudBuilt()
        {
            if (_uiDocument == null) return;
            var root = _uiDocument.rootVisualElement;
            if (root == null) return;

            if (_partyHud != null && _partyHud.panel != null)
                return;

            // Panels 위(항상 표시) + Settings/Loading 등 오버레이 아래에 있어야 하므로 Popups 레이어 사용
            var popups = root.Q<VisualElement>("Layer_Popups");
            if (popups == null) return;

            _layerPanels = root.Q<VisualElement>("Layer_Panels");

            _partyHud = popups.Q<VisualElement>("PartyHud");
            if (_partyHud == null)
            {
                _partyHud = BuildHudTree();
                popups.Add(_partyHud);
            }

            // Popups 레이어 내에서 최상단 유지(패널보다 위에 그려지도록)
            _partyHud.BringToFront();

            // 더미 초기값
            SetMemberHealth01(0, 1f);
            SetMemberHealth01(1, 1f);
            SetMemberHealth01(2, 1f);

            SetMemberSkillCount(0, dummySkillCount0);
            SetMemberSkillCount(1, dummySkillCount1);
            SetMemberSkillCount(2, dummySkillCount2);
        }

        private VisualElement BuildHudTree()
        {
            var hud = new VisualElement { name = "PartyHud" };
            hud.AddToClassList("party-hud");
            hud.pickingMode = PickingMode.Ignore;

            var row = new VisualElement();
            row.AddToClassList("party-row");
            row.pickingMode = PickingMode.Ignore;
            hud.Add(row);

            for (int i = 0; i < 3; i++)
            {
                var member = new VisualElement();
                member.AddToClassList("party-member");
                member.pickingMode = PickingMode.Ignore;

                var portrait = new Button();
                portrait.name = $"BtnPartyPortrait{i}";
                portrait.AddToClassList("party-portrait-btn");
                portrait.clicked += OpenKingdomArmyPanel;

                var infoCol = new VisualElement();
                infoCol.AddToClassList("party-info-col");
                infoCol.pickingMode = PickingMode.Ignore;

                var hpBar = new VisualElement();
                hpBar.AddToClassList("party-hpbar");
                hpBar.pickingMode = PickingMode.Ignore;

                var hpFill = new VisualElement();
                hpFill.AddToClassList("party-hpfill");
                hpFill.pickingMode = PickingMode.Ignore;
                hpBar.Add(hpFill);

                var skillRow = new VisualElement();
                skillRow.AddToClassList("party-skill-row");
                skillRow.pickingMode = PickingMode.Ignore;

                // 0~3 스킬 슬롯
                for (int s = 0; s < 3; s++)
                {
                    var slot = new VisualElement();
                    slot.AddToClassList("party-skill");
                    slot.pickingMode = PickingMode.Ignore;

                    var mask = new VisualElement();
                    mask.AddToClassList("party-skill-cd-mask");
                    mask.pickingMode = PickingMode.Ignore;

                    var cd = new Label("");
                    cd.AddToClassList("party-skill-cd-text");
                    cd.pickingMode = PickingMode.Ignore;

                    slot.Add(mask);
                    slot.Add(cd);

                    _members[i].Skills[s] = new SkillSlot { Root = slot, Mask = mask, CooldownLabel = cd, Co = null };
                    _members[i].SkillIds[s] = -1;

                    // 기본은 비표시(스킬 보유 수만큼만 표시)
                    slot.style.display = DisplayStyle.None;

                    // 쿨다운 UI 기본 off
                    mask.style.display = DisplayStyle.None;
                    cd.style.display = DisplayStyle.None;

                    skillRow.Add(slot);
                }

                infoCol.Add(hpBar);
                infoCol.Add(skillRow);

                member.Add(portrait);
                member.Add(infoCol);
                row.Add(member);

                _members[i].Portrait = portrait;
                _members[i].HpFill = hpFill;
            }

            return hud;
        }

        private void UpdateHudVisibilityAndPosition()
        {
            if (_uiDocument == null || _partyHud == null) return;
            var root = _uiDocument.rootVisualElement;
            if (root == null) return;

            _bottomBar = root.Q<VisualElement>("BottomBar");
            bool onMain = _bottomBar != null;

            _partyHud.style.display = onMain ? DisplayStyle.Flex : DisplayStyle.None;
            if (!onMain) return;

            float barH = _bottomBar.resolvedStyle.height > 1f ? _bottomBar.resolvedStyle.height : fallbackBottomBarPx;
            float sheetH = GetTopSheetHeight();
            float bottom = barH + baseGapPx + (sheetH > 1f ? sheetH + sheetGapPx : 0f);

            _partyHud.style.bottom = bottom;
        }

        private float GetTopSheetHeight()
        {
            if (_layerPanels == null) return 0f;
            if (_layerPanels.childCount <= 0) return 0f;

            var topPanel = _layerPanels[_layerPanels.childCount - 1] as VisualElement;
            if (topPanel == null) return 0f;

            var sheet = topPanel.Q<VisualElement>("Sheet");
            if (sheet == null) return 0f;

            float h = sheet.resolvedStyle.height;
            return h > 1f ? h : 0f;
        }

        private void OpenKingdomArmyPanel()
        {
            if (UITKUIManager.Instance == null) return;

            // 탭 버튼과 동일한 동작(다른 탭 패널이 열려있다면 교체)
            UITKUIManager.Instance.ClearPanels();
            UITKUIManager.Instance.PushPanel(UIPanelId.KingdomArmy, "kingdomArmyPanel", clearBefore: false, isTabPanel: true);
        }

        // ===== 외부(팀원) 호출 API =====
        public void SetMemberHealth(int memberIndex, float current, float max)
        {
            if (memberIndex < 0 || memberIndex >= 3) return;
            float t = max <= 0f ? 0f : Mathf.Clamp01(current / max);
            SetMemberHealth01(memberIndex, t);
        }

        public void SetMemberHealth01(int memberIndex, float normalized01)
        {
            if (memberIndex < 0 || memberIndex >= 3) return;
            var fill = _members[memberIndex].HpFill;
            if (fill == null) return;
            fill.style.width = new Length(Mathf.Clamp01(normalized01) * 100f, LengthUnit.Percent);
        }

        public void SetMemberSkillCount(int memberIndex, int count)
        {
            if (memberIndex < 0 || memberIndex >= 3) return;
            count = Mathf.Clamp(count, 0, 3);

            for (int s = 0; s < 3; s++)
            {
                var slot = _members[memberIndex].Skills[s].Root;
                if (slot == null) continue;
                slot.style.display = s < count ? DisplayStyle.Flex : DisplayStyle.None;
                _members[memberIndex].SkillIds[s] = s < count ? s : -1; // 더미 ID
            }
        }

        public void SetMemberSkillIds(int memberIndex, IReadOnlyList<int> skillIds)
        {
            if (memberIndex < 0 || memberIndex >= 3) return;
            int count = skillIds == null ? 0 : Mathf.Clamp(skillIds.Count, 0, 3);

            for (int s = 0; s < 3; s++)
            {
                var slot = _members[memberIndex].Skills[s].Root;
                if (slot == null) continue;

                bool show = s < count;
                slot.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
                _members[memberIndex].SkillIds[s] = show ? skillIds[s] : -1;
            }
        }

        public void NotifySkillUsedBySlotIndex(int memberIndex, int slotIndex, float cooldownSeconds)
        {
            if (memberIndex < 0 || memberIndex >= 3) return;
            if (slotIndex < 0 || slotIndex >= 3) return;
            StartCooldown(memberIndex, slotIndex, cooldownSeconds);
        }

        public void NotifySkillUsedBySkillId(int memberIndex, int skillId, float cooldownSeconds)
        {
            if (memberIndex < 0 || memberIndex >= 3) return;

            for (int s = 0; s < 3; s++)
            {
                if (_members[memberIndex].SkillIds[s] == skillId)
                {
                    StartCooldown(memberIndex, s, cooldownSeconds);
                    return;
                }
            }
        }

        private void StartCooldown(int memberIndex, int slotIndex, float cooldownSeconds)
        {
            var slot = _members[memberIndex].Skills[slotIndex];
            if (slot.Root == null) return;
            if (slot.Root.resolvedStyle.display == DisplayStyle.None) return;

            if (slot.Co != null)
                StopCoroutine(slot.Co);

            slot.Co = StartCoroutine(CooldownRoutine(slot.Mask, slot.CooldownLabel, cooldownSeconds));
            _members[memberIndex].Skills[slotIndex] = slot;
        }

        private static IEnumerator CooldownRoutine(VisualElement mask, Label label, float seconds)
        {
            seconds = Mathf.Max(0f, seconds);
            if (mask != null) mask.style.display = seconds > 0f ? DisplayStyle.Flex : DisplayStyle.None;
            if (label != null) label.style.display = seconds > 0f ? DisplayStyle.Flex : DisplayStyle.None;

            float t = seconds;
            int last = -1;
            while (t > 0f)
            {
                t -= Time.unscaledDeltaTime;
                int now = Mathf.CeilToInt(t);
                if (now != last)
                {
                    last = now;
                    if (label != null)
                        label.text = Mathf.Max(0, now).ToString();
                }
                yield return null;
            }

            if (mask != null) mask.style.display = DisplayStyle.None;
            if (label != null)
            {
                label.text = "";
                label.style.display = DisplayStyle.None;
            }
        }
    }
}