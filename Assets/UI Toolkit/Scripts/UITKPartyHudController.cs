using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using KingdomIdle.UI;
using KingdomIdle.KingdomArmy;

namespace KingdomIdle.UIToolkit
{
    [DefaultExecutionOrder(-940)]
    public sealed class UITKPartyHudController : MonoBehaviour
    {
        public static UITKPartyHudController Instance { get; private set; }

        [Header("Portrait Sprites")]
        [SerializeField] private Sprite portraitSprite0;
        [SerializeField] private Sprite portraitSprite1;
        [SerializeField] private Sprite portraitSprite2;

        [Header("Layout")]
        [SerializeField] private float baseGapPx = 12f;
        [SerializeField] private float sheetGapPx = 10f;
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

            public MemberUI(int skillSlots)
            {
                Portrait = null;
                HpFill = null;
                Skills = new SkillSlot[skillSlots];
            }
        }

        private struct SkillSlot
        {
            public VisualElement Root;
            public VisualElement Mask;
            public Label CooldownLabel;
            public Label NameLabel;
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
            {
                _playersResolved = true;

                for (int i = 0; i < 3 && i < _players.Count; i++)
                {
                    if (GetPortraitSprite(i) != null) continue;

                    var player = _players[i];
                    if (player == null) continue;

                    Sprite idleSprite = null;
                    var jobDB = mgr.JobDB;
                    if (jobDB != null && player.playerStatus != null)
                    {
                        var jobData = jobDB.GetJob(player.playerStatus.JobName);
                        if (jobData != null && jobData.jobSprite != null)
                            idleSprite = jobData.jobSprite;
                    }

                    if (idleSprite == null)
                    {
                        var sr = player.GetComponent<SpriteRenderer>();
                        if (sr != null && sr.sprite != null)
                            idleSprite = sr.sprite;
                    }

                    if (idleSprite != null)
                        SetPortraitSprite(i, idleSprite);
                }
            }
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

            for (int i = 0; i < 3; i++)
                ApplyPortraitSprite(i);

            for (int i = 0; i < 3; i++)
                SetMemberHealth01(i, 1f);
        }

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
                SetMemberHealth(i, ps.HP, ps.MaxHP);

                var sys = player.skillSystem;
                if (sys == null) continue;

                SyncSkillSlots(i, sys);
            }
        }

        private void SyncSkillSlots(int memberIdx, SkillSystem sys)
        {
            for (int s = 0; s < 3; s++)
            {
                var slot = _members[memberIdx].Skills[s];
                if (slot.Root == null) continue;

                var displaySlot = sys.GetSlot(s);
                if (!displaySlot.Active)
                {
                    slot.Root.style.display = DisplayStyle.None;
                    continue;
                }

                slot.Root.style.display = DisplayStyle.Flex;

                if (slot.NameLabel != null)
                    slot.NameLabel.text = displaySlot.Name ?? "";

                if (displaySlot.IsPassive)
                {
                    if (slot.Mask != null) slot.Mask.style.display = DisplayStyle.None;
                    if (slot.CooldownLabel != null)
                    {
                        slot.CooldownLabel.text = "상시";
                        slot.CooldownLabel.style.display = DisplayStyle.Flex;
                        slot.CooldownLabel.style.color = new Color(0.4f, 1f, 0.4f, 1f);
                    }
                }
                else
                {
                    float cd = sys.GetSlotCooldown(s);
                    if (cd > 0f)
                    {
                        if (slot.Mask != null) slot.Mask.style.display = DisplayStyle.Flex;
                        if (slot.CooldownLabel != null)
                        {
                            slot.CooldownLabel.text = Mathf.CeilToInt(cd).ToString();
                            slot.CooldownLabel.style.display = DisplayStyle.Flex;
                            slot.CooldownLabel.style.color = Color.white;
                        }
                    }
                    else
                    {
                        if (slot.Mask != null) slot.Mask.style.display = DisplayStyle.None;
                        if (slot.CooldownLabel != null)
                        {
                            slot.CooldownLabel.text = "";
                            slot.CooldownLabel.style.display = DisplayStyle.None;
                        }
                    }
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

                    var nameLabel = new Label("");
                    nameLabel.AddToClassList("party-skill-name");
                    nameLabel.pickingMode = PickingMode.Ignore;
                    nameLabel.style.fontSize = 8;
                    nameLabel.style.unityTextAlign = TextAnchor.LowerCenter;
                    nameLabel.style.color = Color.white;
                    nameLabel.style.position = Position.Absolute;
                    nameLabel.style.bottom = 0;
                    nameLabel.style.left = 0;
                    nameLabel.style.right = 0;

                    slot.Add(mask);
                    slot.Add(cd);
                    slot.Add(nameLabel);

                    _members[i].Skills[s] = new SkillSlot
                    {
                        Root = slot,
                        Mask = mask,
                        CooldownLabel = cd,
                        NameLabel = nameLabel
                    };

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
            }
        }
    }
}
