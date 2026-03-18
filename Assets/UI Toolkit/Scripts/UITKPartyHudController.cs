using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using KingdomIdle.UI;
using KingdomIdle.KingdomArmy;

namespace KingdomIdle.UIToolkit
{
    // 하단 탭 위 3인 파티 HUD
    [DefaultExecutionOrder(-940)]
    public sealed class UITKPartyHudController : MonoBehaviour
    {
        public static UITKPartyHudController Instance { get; private set; }

        [Header("Portrait Sprites (에디터에서 직업별 정적 초상화 설정)")]
        [Tooltip("왕국군1 초상화 스프라이트 (전직에 맞게 직접 설정)")]
        [SerializeField] private Sprite portraitSprite0;
        [Tooltip("왕국군2 초상화 스프라이트 (전직에 맞게 직접 설정)")]
        [SerializeField] private Sprite portraitSprite1;
        [Tooltip("왕국군3 초상화 스프라이트 (전직에 맞게 직접 설정)")]
        [SerializeField] private Sprite portraitSprite2;

        [Header("Layout")]
        [SerializeField] private float baseGapPx = 12f;      // BottomBar 위 기본 간격
        [SerializeField] private float sheetGapPx = 10f;     // Sheet 위 추가 간격
        [SerializeField] private float fallbackBottomBarPx = 190f;

        private UIDocument _uiDocument;
        private VisualElement _partyHud;
        private VisualElement _layerPanels;
        private VisualElement _bottomBar;

        private readonly MemberUI[] _members = new MemberUI[3];
        private List<Player> _players;
        private bool _playersResolved;

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
            SyncFromPlayers();
        }

        /// <summary>
        /// 초상화 스프라이트를 런타임에 변경한다.
        /// 전직 시 ChangeJob에서 호출하거나 외부에서 갱신 가능.
        /// </summary>
        public void SetPortraitSprite(int memberIndex, Sprite sprite)
        {
            if (memberIndex < 0 || memberIndex >= 3) return;

            switch (memberIndex)
            {
                case 0: portraitSprite0 = sprite; break;
                case 1: portraitSprite1 = sprite; break;
                case 2: portraitSprite2 = sprite; break;
            }

            ApplyPortraitSprite(memberIndex);
        }

        private Sprite GetPortraitSprite(int index)
        {
            return index switch
            {
                0 => portraitSprite0,
                1 => portraitSprite1,
                2 => portraitSprite2,
                _ => null
            };
        }

        private void ApplyPortraitSprite(int index)
        {
            if (index < 0 || index >= 3) return;
            var portrait = _members[index].Portrait;
            if (portrait == null) return;

            var sprite = GetPortraitSprite(index);
            if (sprite != null)
                portrait.style.backgroundImage = new StyleBackground(sprite);
            else
                portrait.style.backgroundImage = StyleKeyword.None;
        }

        private void EnsureRefs()
        {
            if (_uiDocument != null && _uiDocument.rootVisualElement != null) return;

            if (UITKUIManager.Instance != null)
                _uiDocument = UITKUIManager.Instance.GetComponent<UIDocument>();

            if (_uiDocument == null)
                _uiDocument = FindFirstObjectByType<UIDocument>();
        }

        private void ResolvePlayers()
        {
            if (_playersResolved) return;

            var mgr = KingdomArmyManager.Instance;
            if (mgr == null) return;

            _players = mgr.GetPlayers();
            if (_players != null && _players.Count > 0)
                _playersResolved = true;
        }

        private void EnsureHudBuilt()
        {
            if (_uiDocument == null) return;
            var root = _uiDocument.rootVisualElement;
            if (root == null) return;

            if (_partyHud != null && _partyHud.panel != null)
                return;

            var popups = root.Q<VisualElement>("Layer_Popups");
            if (popups == null) return;

            _layerPanels = root.Q<VisualElement>("Layer_Panels");

            _partyHud = popups.Q<VisualElement>("PartyHud");
            if (_partyHud == null)
            {
                _partyHud = BuildHudTree();
                popups.Add(_partyHud);
            }

            _partyHud.BringToFront();

            // 초상화 스프라이트 적용
            for (int i = 0; i < 3; i++)
                ApplyPortraitSprite(i);

            // 초기 HP 100%
            for (int i = 0; i < 3; i++)
                SetMemberHealth01(i, 1f);
        }

        /// <summary>
        /// 매 프레임 플레이어 데이터를 읽어서 HP와 스킬을 동기화한다.
        /// </summary>
        private void SyncFromPlayers()
        {
            if (_partyHud == null) return;

            ResolvePlayers();
            if (_players == null) return;

            for (int i = 0; i < 3 && i < _players.Count; i++)
            {
                var player = _players[i];
                if (player == null || player.playerStatus == null) continue;

                var ps = player.playerStatus;

                // HP 동기화
                SetMemberHealth(i, ps.HP, ps.MaxHP);

                // 스킬 수 동기화 (직업 스킬 기반)
                var jobDB = KingdomArmyManager.Instance?.JobDB;
                if (jobDB != null)
                {
                    var jobData = jobDB.GetJob(ps.JobName);
                    int skillCount = jobData?.skills?.Count ?? 0;
                    SetMemberSkillCount(i, Mathf.Min(skillCount, 3));
                }
            }
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

                    slot.style.display = DisplayStyle.None;
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
            UITKUIManager.Instance.ClearPanels();
            UITKUIManager.Instance.PushPanel(UIPanelId.KingdomArmy, "kingdomArmyPanel", clearBefore: false, isTabPanel: true);
        }

        // ===== 외부 호출 API (기존 유지) =====
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
                _members[memberIndex].SkillIds[s] = s < count ? s : -1;
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
