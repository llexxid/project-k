using System.Collections;
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
        private readonly Button[] _slotBtns = new Button[MageTowerManager.SlotCount];
        private readonly VisualElement[] _slotIcons = new VisualElement[MageTowerManager.SlotCount];
        private readonly Label[] _slotLabels = new Label[MageTowerManager.SlotCount];
        private readonly VisualElement[] _cdMasks = new VisualElement[MageTowerManager.SlotCount];
        private readonly Label[] _cdTexts = new Label[MageTowerManager.SlotCount];

        private int _targetingSlot = -1;
        private bool _targeting;
        private GameObject _rangeIndicator;
        private LineRenderer _rangeLine;

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
                mgr.OnCooldownTick += OnCooldownTick;
        }

        private void OnDisable()
        {
            var mgr = MageTowerManager.Instance;
            if (mgr != null)
                mgr.OnCooldownTick -= OnCooldownTick;
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

        // ===== Slot Click → Targeting Mode =====
        private void OnSlotClicked(int slotIndex)
        {
            var mgr = MageTowerManager.Instance;
            if (mgr == null) return;

            int skillId = mgr.GetEquippedSkillId(slotIndex);
            if (skillId < 0) return;
            if (mgr.IsOnCooldown(slotIndex)) return;

            if (_targeting && _targetingSlot == slotIndex)
            {
                CancelTargeting();
                return;
            }

            BeginTargeting(slotIndex);
        }

        private void OnTowerBtnClicked()
        {
            CancelTargeting();
            UITKMageTowerPopupController.Show();
        }

        // ===== Targeting System =====
        private void BeginTargeting(int slotIndex)
        {
            CancelTargeting();

            _targeting = true;
            _targetingSlot = slotIndex;

            _slotBtns[slotIndex].AddToClassList("mt-hud-slot-targeting");

            var root = _uiDocument.rootVisualElement;
            root.RegisterCallback<PointerDownEvent>(OnTargetPointerDown, TrickleDown.TrickleDown);
            root.RegisterCallback<PointerMoveEvent>(OnTargetPointerMove);
            root.RegisterCallback<PointerUpEvent>(OnTargetPointerUp);
        }

        public void CancelTargeting()
        {
            if (!_targeting) return;

            if (_targetingSlot >= 0 && _targetingSlot < _slotBtns.Length && _slotBtns[_targetingSlot] != null)
                _slotBtns[_targetingSlot].RemoveFromClassList("mt-hud-slot-targeting");

            _targeting = false;
            _targetingSlot = -1;

            DestroyRangeIndicator();

            if (_uiDocument != null)
            {
                var root = _uiDocument.rootVisualElement;
                if (root != null)
                {
                    root.UnregisterCallback<PointerDownEvent>(OnTargetPointerDown, TrickleDown.TrickleDown);
                    root.UnregisterCallback<PointerMoveEvent>(OnTargetPointerMove);
                    root.UnregisterCallback<PointerUpEvent>(OnTargetPointerUp);
                }
            }
        }

        private void OnTargetPointerDown(PointerDownEvent evt)
        {
            if (!_targeting) return;

            var target = evt.target as VisualElement;
            if (IsInsideHud(target)) return;

            Vector3 worldPos = PanelToWorld(evt.position);
            ShowRangeIndicator(worldPos);

            evt.StopPropagation();
        }

        private void OnTargetPointerMove(PointerMoveEvent evt)
        {
            if (!_targeting || _rangeIndicator == null) return;

            Vector3 worldPos = PanelToWorld(evt.position);
            UpdateRangeIndicator(worldPos);
        }

        private void OnTargetPointerUp(PointerUpEvent evt)
        {
            if (!_targeting) return;

            var target = evt.target as VisualElement;
            if (IsInsideHud(target))
            {
                DestroyRangeIndicator();
                return;
            }

            Vector3 worldPos = PanelToWorld(evt.position);
            int slot = _targetingSlot;
            CancelTargeting();

            var mgr = MageTowerManager.Instance;
            if (mgr != null)
                mgr.CastSkill(slot, worldPos);

            evt.StopPropagation();
        }

        private bool IsInsideHud(VisualElement element)
        {
            while (element != null)
            {
                if (element == _hud) return true;
                element = element.parent;
            }
            return false;
        }

        // ===== Coordinate Conversion =====
        private Vector3 PanelToWorld(Vector3 panelPos)
        {
            var cam = Camera.main;
            if (cam == null) return Vector3.zero;

            Vector2 screenPos = RuntimePanelUtils.ScreenToPanel(
                _uiDocument.rootVisualElement.panel, Vector2.zero);

            float panelH = _uiDocument.rootVisualElement.resolvedStyle.height;
            float panelW = _uiDocument.rootVisualElement.resolvedStyle.width;

            float sx = panelPos.x / panelW * Screen.width;
            float sy = (1f - panelPos.y / panelH) * Screen.height;

            Ray ray = cam.ScreenPointToRay(new Vector3(sx, sy, 0));
            if (Mathf.Abs(ray.direction.z) < 0.001f) return ray.origin;

            float t = -ray.origin.z / ray.direction.z;
            return ray.origin + ray.direction * t;
        }

        // ===== Range Indicator =====
        private void ShowRangeIndicator(Vector3 center)
        {
            var mgr = MageTowerManager.Instance;
            if (mgr == null) return;

            int skillId = mgr.GetEquippedSkillId(_targetingSlot);
            var so = skillId >= 0 ? mgr.GetSkillById(skillId) : null;
            float rH = so != null ? so.castRangeH : 5f;
            float rV = so != null ? so.castRangeV : 3f;

            if (_rangeIndicator == null)
            {
                _rangeIndicator = new GameObject("MT_RangeIndicator");
                _rangeLine = _rangeIndicator.AddComponent<LineRenderer>();
                _rangeLine.useWorldSpace = true;
                _rangeLine.loop = true;
                _rangeLine.startWidth = 0.05f;
                _rangeLine.endWidth = 0.05f;
                _rangeLine.material = new Material(Shader.Find("Sprites/Default"));
                _rangeLine.startColor = new Color(1f, 0.85f, 0.2f, 0.7f);
                _rangeLine.endColor = new Color(1f, 0.85f, 0.2f, 0.7f);
                _rangeLine.sortingOrder = 100;
            }

            UpdateRangeEllipse(center, rH, rV);
        }

        private void UpdateRangeIndicator(Vector3 center)
        {
            if (_rangeIndicator == null) return;

            var mgr = MageTowerManager.Instance;
            if (mgr == null) return;
            int skillId = mgr.GetEquippedSkillId(_targetingSlot);
            var so = skillId >= 0 ? mgr.GetSkillById(skillId) : null;
            float rH = so != null ? so.castRangeH : 5f;
            float rV = so != null ? so.castRangeV : 3f;

            UpdateRangeEllipse(center, rH, rV);
        }

        private void UpdateRangeEllipse(Vector3 center, float radiusH, float radiusV)
        {
            const int segments = 32;
            _rangeLine.positionCount = segments;

            for (int i = 0; i < segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                float x = center.x + Mathf.Cos(angle) * radiusH;
                float y = center.y + Mathf.Sin(angle) * radiusV;
                _rangeLine.SetPosition(i, new Vector3(x, y, 0));
            }
        }

        private void DestroyRangeIndicator()
        {
            if (_rangeIndicator != null)
            {
                Destroy(_rangeIndicator);
                _rangeIndicator = null;
                _rangeLine = null;
            }
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

        private void OnDestroy()
        {
            CancelTargeting();
        }
    }
}
