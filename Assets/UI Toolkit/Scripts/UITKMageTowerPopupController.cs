using System;
using UnityEngine;
using UnityEngine.UIElements;
using KingdomIdle.MageTower;

namespace KingdomIdle.UIToolkit
{
    // 마탑 스킬 장착 팝업 (드래그 & 드롭 + 클릭 장착)
    public static class UITKMageTowerPopupController
    {
        private static VisualElement _overlay;
        private static VisualElement _panel;
        private static VisualElement _slotsCol;
        private static VisualElement _invGrid;
        private static VisualElement _dragGhost;

        private static int _selectedSlot;
        private static int _dragSkillId = -1;
        private static bool _dragging;
        private static bool _dragPending;
        private static Vector2 _dragStartPos;
        private const float DragThreshold = 10f;

        private static readonly Button[] _equipSlots = new Button[MageTowerManager.SlotCount];
        private static readonly VisualElement[] _equipSlotIcons = new VisualElement[MageTowerManager.SlotCount];
        private static readonly Label[] _equipSlotLabels = new Label[MageTowerManager.SlotCount];

        public static bool IsOpen => _overlay != null && !_overlay.ClassListContains("hidden");

        public static void Show(int focusSlot = 0)
        {
            _selectedSlot = Mathf.Clamp(focusSlot, 0, MageTowerManager.SlotCount - 1);
            EnsureBuilt();
            Refresh();
            _overlay.RemoveFromClassList("hidden");
            _overlay.BringToFront();
        }

        public static void Hide()
        {
            if (_overlay == null) return;
            _overlay.AddToClassList("hidden");
            CancelDrag();
            if (UITKMageTowerHudController.Instance != null)
                UITKMageTowerHudController.Instance.RefreshSlots();
        }

        private static void EnsureBuilt()
        {
            if (_overlay != null) return;

            var mgr = UITKUIManager.Instance;
            if (mgr == null) return;
            var uiDoc = mgr.GetComponent<UIDocument>();
            if (uiDoc == null) return;
            var root = uiDoc.rootVisualElement;
            var overlays = root?.Q<VisualElement>("Layer_Overlays");
            if (overlays == null) return;

            _overlay = new VisualElement();
            _overlay.AddToClassList("mt-equip-overlay");
            _overlay.pickingMode = PickingMode.Position;

            _panel = new VisualElement();
            _panel.AddToClassList("mt-equip-panel");
            _panel.pickingMode = PickingMode.Position;

            // titlebar
            var titleBar = new VisualElement();
            titleBar.AddToClassList("mt-equip-titlebar");

            var title = new Label("마탑 스킬 장착");
            title.AddToClassList("mt-equip-title");

            var closeBtn = new Button(Hide);
            closeBtn.text = "✕";
            closeBtn.AddToClassList("mt-equip-close");

            titleBar.Add(title);
            titleBar.Add(closeBtn);
            _panel.Add(titleBar);

            // body
            var body = new VisualElement();
            body.AddToClassList("mt-equip-body");

            // left: equip slots
            _slotsCol = new VisualElement();
            _slotsCol.AddToClassList("mt-equip-slots-col");

            var slotsLabel = new Label("장착 슬롯");
            slotsLabel.AddToClassList("mt-equip-slots-label");
            _slotsCol.Add(slotsLabel);

            for (int i = 0; i < MageTowerManager.SlotCount; i++)
            {
                int idx = i;
                var slot = new Button();
                slot.AddToClassList("mt-equip-slot");
                slot.clicked += () => OnEquipSlotClicked(idx);

                var icon = new VisualElement();
                icon.AddToClassList("mt-equip-slot-icon");
                icon.pickingMode = PickingMode.Ignore;
                icon.style.display = DisplayStyle.None;

                var lbl = new Label("-");
                lbl.AddToClassList("mt-equip-slot-empty-label");
                lbl.pickingMode = PickingMode.Ignore;

                slot.Add(icon);
                slot.Add(lbl);
                _slotsCol.Add(slot);

                _equipSlots[i] = slot;
                _equipSlotIcons[i] = icon;
                _equipSlotLabels[i] = lbl;
            }

            body.Add(_slotsCol);

            // right: inventory
            var invCol = new VisualElement();
            invCol.AddToClassList("mt-inv-col");

            var invLabel = new Label("보유 스킬");
            invLabel.AddToClassList("mt-inv-label");
            invCol.Add(invLabel);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("mt-inv-scroll");
            scroll.verticalScrollerVisibility = ScrollerVisibility.Auto;

            _invGrid = new VisualElement();
            _invGrid.AddToClassList("mt-inv-grid");
            scroll.Add(_invGrid);
            invCol.Add(scroll);

            body.Add(invCol);
            _panel.Add(body);

            // drag ghost
            _dragGhost = new VisualElement();
            _dragGhost.AddToClassList("mt-drag-ghost");
            _dragGhost.pickingMode = PickingMode.Ignore;
            _dragGhost.style.display = DisplayStyle.None;

            _overlay.Add(_panel);
            _overlay.Add(_dragGhost);

            // close on backdrop click
            _overlay.RegisterCallback<PointerDownEvent>(evt =>
            {
                var target = evt.target as VisualElement;
                if (target == _overlay)
                    Hide();
            }, TrickleDown.TrickleDown);

            // drag move
            _overlay.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (_dragPending && !_dragging)
                {
                    float dist = Vector2.Distance(_dragStartPos, evt.position);
                    if (dist >= DragThreshold)
                    {
                        _dragging = true;
                        _dragGhost.style.display = DisplayStyle.Flex;
                    }
                }
                if (!_dragging) return;
                _dragGhost.style.left = evt.position.x - 35;
                _dragGhost.style.top = evt.position.y - 35;
            });

            // drag drop or cancel on pointer up
            _overlay.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (_dragging)
                {
                    int dropSlot = FindSlotUnderPointer(evt.position);
                    if (dropSlot >= 0)
                        FinishDrop(dropSlot, _dragSkillId);
                    else
                        CancelDrag();
                }
                else if (_dragPending)
                {
                    _dragPending = false;
                    _dragSkillId = -1;
                }
            });

            _overlay.AddToClassList("hidden");
            overlays.Add(_overlay);
        }

        private static void Refresh()
        {
            var mgr = MageTowerManager.Instance;
            if (mgr == null) return;

            // equip slots
            for (int i = 0; i < MageTowerManager.SlotCount; i++)
            {
                int skillId = mgr.GetEquippedSkillId(i);
                var so = skillId >= 0 ? mgr.GetSkillById(skillId) : null;

                _equipSlots[i].RemoveFromClassList("mt-equip-slot-active");
                if (i == _selectedSlot)
                    _equipSlots[i].AddToClassList("mt-equip-slot-active");

                if (so != null)
                {
                    if (so.icon != null)
                    {
                        _equipSlotIcons[i].style.backgroundImage = new StyleBackground(so.icon);
                        _equipSlotIcons[i].style.display = DisplayStyle.Flex;
                        _equipSlotLabels[i].text = "";
                    }
                    else
                    {
                        _equipSlotIcons[i].style.display = DisplayStyle.None;
                        _equipSlotLabels[i].text = so.skillName;
                        _equipSlotLabels[i].RemoveFromClassList("mt-equip-slot-empty-label");
                    }
                }
                else
                {
                    _equipSlotIcons[i].style.display = DisplayStyle.None;
                    _equipSlotLabels[i].text = "-";
                    _equipSlotLabels[i].AddToClassList("mt-equip-slot-empty-label");
                }
            }

            // inventory
            _invGrid.Clear();
            var skills = mgr.GetAllSkills();
            for (int i = 0; i < skills.Count; i++)
            {
                var skill = skills[i];
                if (skill == null) continue;
                _invGrid.Add(BuildInvItem(skill, mgr));
            }
        }

        private static VisualElement BuildInvItem(MageTowerSkillSO skill, MageTowerManager mgr)
        {
            int id = skill.id;
            var item = new VisualElement();
            item.AddToClassList("mt-inv-item");
            if (mgr.IsEquipped(id))
                item.AddToClassList("mt-inv-item-equipped");

            var icon = new VisualElement();
            icon.AddToClassList("mt-inv-item-icon");
            icon.pickingMode = PickingMode.Ignore;
            if (skill.icon != null)
                icon.style.backgroundImage = new StyleBackground(skill.icon);

            var nameLabel = new Label(skill.skillName);
            nameLabel.AddToClassList("mt-inv-item-name");
            nameLabel.pickingMode = PickingMode.Ignore;

            float dmg = mgr.GetEffectiveDamage(id);
            var dmgLabel = new Label($"DMG {dmg:F0}");
            dmgLabel.AddToClassList("mt-inv-item-dmg");
            dmgLabel.pickingMode = PickingMode.Ignore;

            item.Add(icon);
            item.Add(nameLabel);
            item.Add(dmgLabel);

            // drag start (pending until threshold)
            item.RegisterCallback<PointerDownEvent>(evt =>
            {
                _dragSkillId = id;
                _dragPending = true;
                _dragging = false;
                _dragStartPos = evt.position;
                if (skill.icon != null)
                    _dragGhost.style.backgroundImage = new StyleBackground(skill.icon);
                else
                    _dragGhost.style.backgroundImage = StyleKeyword.None;
            });

            // click to open detail popup (only if not dragged)
            item.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (_dragging) return;
                if (_dragPending)
                {
                    _dragPending = false;
                    _dragSkillId = -1;
                    UITKMageTowerDetailPopupController.Show(id);
                }
            });

            return item;
        }

        private static void FinishDrop(int slotIndex, int skillId)
        {
            CancelDrag();
            var mgr = MageTowerManager.Instance;
            if (mgr == null) return;
            mgr.Equip(slotIndex, skillId);
            Refresh();
        }

        private static void CancelDrag()
        {
            _dragging = false;
            _dragPending = false;
            _dragSkillId = -1;
            if (_dragGhost != null)
                _dragGhost.style.display = DisplayStyle.None;
        }

        private static int FindSlotUnderPointer(Vector2 pointerPos)
        {
            for (int i = 0; i < MageTowerManager.SlotCount; i++)
            {
                if (_equipSlots[i] == null) continue;
                var rect = _equipSlots[i].worldBound;
                if (rect.Contains(pointerPos))
                    return i;
            }
            return -1;
        }

        private static void OnEquipSlotClicked(int slotIndex)
        {
            var mgr = MageTowerManager.Instance;
            if (mgr == null) return;

            int skillId = mgr.GetEquippedSkillId(slotIndex);
            if (skillId >= 0)
            {
                mgr.Unequip(slotIndex);
                Refresh();
            }
            else
            {
                _selectedSlot = slotIndex;
                Refresh();
            }
        }
    }
}
