using UnityEngine;
using UnityEngine.UIElements;
using KingdomIdle.MageTower;

namespace KingdomIdle.UIToolkit
{
    // 좌측 마탑 스킬 슬롯 HUD (5슬롯)
    [DefaultExecutionOrder(-935)]
    public sealed class UITKMageTowerHudController : MonoBehaviour
    {
        public static UITKMageTowerHudController Instance { get; private set; }

        private UIDocument _uiDocument;
        private VisualElement _hud;
        private readonly Button[] _slotBtns = new Button[MageTowerManager.SlotCount];
        private readonly VisualElement[] _slotIcons = new VisualElement[MageTowerManager.SlotCount];
        private readonly Label[] _slotLabels = new Label[MageTowerManager.SlotCount];

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void Update()
        {
            EnsureRefs();
            EnsureHudBuilt();
            UpdateVisibility();
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
            if (_hud != null && _hud.panel != null) return;

            var popups = root.Q<VisualElement>("Layer_Popups");
            if (popups == null) return;

            _hud = popups.Q<VisualElement>("MageTowerHud");
            if (_hud == null)
            {
                _hud = BuildHud();
                popups.Add(_hud);
            }

            RefreshSlots();
        }

        private VisualElement BuildHud()
        {
            var hud = new VisualElement { name = "MageTowerHud" };
            hud.AddToClassList("mt-hud");
            hud.pickingMode = PickingMode.Ignore;

            for (int i = 0; i < MageTowerManager.SlotCount; i++)
            {
                int idx = i;
                var btn = new Button();
                btn.name = $"BtnMTSlot{i}";
                btn.AddToClassList("mt-hud-slot");
                btn.clicked += () => OnSlotClicked(idx);

                var icon = new VisualElement();
                icon.AddToClassList("mt-hud-slot-icon");
                icon.pickingMode = PickingMode.Ignore;
                icon.style.display = DisplayStyle.None;

                var lbl = new Label("");
                lbl.AddToClassList("mt-hud-slot-empty");
                lbl.pickingMode = PickingMode.Ignore;

                btn.Add(icon);
                btn.Add(lbl);
                hud.Add(btn);

                _slotBtns[i] = btn;
                _slotIcons[i] = icon;
                _slotLabels[i] = lbl;
            }

            return hud;
        }

        private void UpdateVisibility()
        {
            if (_uiDocument == null || _hud == null) return;
            var root = _uiDocument.rootVisualElement;
            if (root == null) return;

            var bottomBar = root.Q<VisualElement>("BottomBar");
            _hud.style.display = bottomBar != null ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void OnSlotClicked(int slotIndex)
        {
            var mgr = MageTowerManager.Instance;
            if (mgr == null) return;

            int skillId = mgr.GetEquippedSkillId(slotIndex);
            if (skillId >= 0)
                UITKMageTowerDetailPopupController.Show(skillId);
            else
                UITKMageTowerPopupController.Show(slotIndex);
        }

        public void RefreshSlots()
        {
            var mgr = MageTowerManager.Instance;

            for (int i = 0; i < MageTowerManager.SlotCount; i++)
            {
                if (_slotBtns[i] == null) continue;

                int skillId = mgr != null ? mgr.GetEquippedSkillId(i) : -1;
                var so = mgr != null && skillId >= 0 ? mgr.GetSkillById(skillId) : null;

                if (so != null)
                {
                    if (so.icon != null)
                    {
                        _slotIcons[i].style.backgroundImage = new StyleBackground(so.icon);
                        _slotIcons[i].style.display = DisplayStyle.Flex;
                        _slotLabels[i].text = "";
                    }
                    else
                    {
                        _slotIcons[i].style.display = DisplayStyle.None;
                        _slotLabels[i].text = so.skillName;
                        _slotLabels[i].RemoveFromClassList("mt-hud-slot-empty");
                        _slotLabels[i].AddToClassList("mt-hud-slot-name");
                    }
                }
                else
                {
                    _slotIcons[i].style.display = DisplayStyle.None;
                    _slotLabels[i].text = "-";
                    _slotLabels[i].RemoveFromClassList("mt-hud-slot-name");
                    _slotLabels[i].AddToClassList("mt-hud-slot-empty");
                }
            }
        }
    }
}
