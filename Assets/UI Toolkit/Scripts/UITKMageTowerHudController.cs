using UnityEngine;
using UnityEngine.UIElements;
using KingdomIdle.MageTower;

namespace KingdomIdle.UIToolkit
{
    [DefaultExecutionOrder(-935)]
    public sealed class UITKMageTowerHudController : MonoBehaviour
    {
        public static UITKMageTowerHudController Instance { get; private set; }

        private UIDocument _uiDocument;
        private VisualElement _hud;
        private Button _autoBtn;
        private bool _autoEnabled;
        private readonly Button[] _slotBtns = new Button[MageTowerManager.SlotCount];
        private readonly VisualElement[] _slotIcons = new VisualElement[MageTowerManager.SlotCount];
        private readonly Label[] _slotLabels = new Label[MageTowerManager.SlotCount];
        private readonly VisualElement[] _cdMasks = new VisualElement[MageTowerManager.SlotCount];
        private readonly Label[] _cdTexts = new Label[MageTowerManager.SlotCount];

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            var mgr = MageTowerManager.Instance;
            if (mgr != null)
            {
                mgr.OnCooldownTick += OnCooldownTick;
                mgr.OnCastingChanged += OnCastingChanged;
            }
        }

        private void OnDisable()
        {
            var mgr = MageTowerManager.Instance;
            if (mgr != null)
            {
                mgr.OnCooldownTick -= OnCooldownTick;
                mgr.OnCastingChanged -= OnCastingChanged;
            }
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

            // Auto 버튼
            _autoBtn = new Button();
            _autoBtn.text = "Auto";
            _autoBtn.AddToClassList("mt-hud-auto-btn");
            _autoBtn.AddToClassList("mt-hud-auto-off");
            _autoBtn.clicked += OnAutoBtnClicked;
            hud.Add(_autoBtn);

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

                var cdMask = new VisualElement();
                cdMask.AddToClassList("mt-hud-slot-cooldown-mask");
                cdMask.pickingMode = PickingMode.Ignore;
                cdMask.style.display = DisplayStyle.None;

                var cdText = new Label("");
                cdText.AddToClassList("mt-hud-slot-cooldown-text");
                cdText.pickingMode = PickingMode.Ignore;
                cdText.style.display = DisplayStyle.None;

                btn.Add(icon);
                btn.Add(lbl);
                btn.Add(cdMask);
                btn.Add(cdText);
                hud.Add(btn);

                _slotBtns[i] = btn;
                _slotIcons[i] = icon;
                _slotLabels[i] = lbl;
                _cdMasks[i] = cdMask;
                _cdTexts[i] = cdText;
            }

            var towerBtn = new Button();
            towerBtn.text = "마탑";
            towerBtn.AddToClassList("mt-hud-tower-btn");
            towerBtn.clicked += OnTowerBtnClicked;
            hud.Add(towerBtn);

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

        // ===== Slot Click → 자동 시전 =====
        private void OnSlotClicked(int slotIndex)
        {
            var mgr = MageTowerManager.Instance;
            if (mgr == null) return;

            int skillId = mgr.GetEquippedSkillId(slotIndex);
            if (skillId < 0) return;
            if (mgr.IsOnCooldown(slotIndex)) return;
            if (mgr.IsCasting(slotIndex)) return;

            mgr.CastSkill(slotIndex);
        }

        // ===== 시전 중 테두리 빛남 =====
        private void OnCastingChanged(int slotIndex, bool casting)
        {
            if (slotIndex < 0 || slotIndex >= _slotBtns.Length) return;
            if (_slotBtns[slotIndex] == null) return;

            if (casting)
                _slotBtns[slotIndex].AddToClassList("mt-hud-slot-casting");
            else
                _slotBtns[slotIndex].RemoveFromClassList("mt-hud-slot-casting");
        }

        private void OnAutoBtnClicked()
        {
            _autoEnabled = !_autoEnabled;

            if (_autoEnabled)
            {
                _autoBtn.RemoveFromClassList("mt-hud-auto-off");
                _autoBtn.AddToClassList("mt-hud-auto-on");
            }
            else
            {
                _autoBtn.RemoveFromClassList("mt-hud-auto-on");
                _autoBtn.AddToClassList("mt-hud-auto-off");
            }

            var mgr = MageTowerManager.Instance;
            if (mgr != null)
                mgr.SetAutoEnabled(_autoEnabled);
        }

        private void OnTowerBtnClicked()
        {
            UITKMageTowerPopupController.Show();
        }

        // ===== Cooldown UI =====
        private void OnCooldownTick()
        {
            var mgr = MageTowerManager.Instance;
            if (mgr == null) return;

            for (int i = 0; i < MageTowerManager.SlotCount; i++)
            {
                if (_cdMasks[i] == null) continue;

                if (mgr.IsOnCooldown(i))
                {
                    float ratio = mgr.GetCooldownRatio(i);
                    float pct = ratio * 100f;

                    _cdMasks[i].style.display = DisplayStyle.Flex;
                    _cdMasks[i].style.height = new StyleLength(new Length(pct, LengthUnit.Percent));

                    float remaining = mgr.GetEffectiveCooldown(mgr.GetEquippedSkillId(i)) * ratio;
                    _cdTexts[i].style.display = DisplayStyle.Flex;
                    _cdTexts[i].text = $"{remaining:F1}";
                }
                else
                {
                    _cdMasks[i].style.display = DisplayStyle.None;
                    _cdTexts[i].style.display = DisplayStyle.None;
                }
            }
        }

        // ===== Refresh Slots =====
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
                        _slotLabels[i].text = so.nameKor;
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
