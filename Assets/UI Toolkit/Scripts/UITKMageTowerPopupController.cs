using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using KingdomIdle.MageTower;

namespace KingdomIdle.UIToolkit
{
    // 마탑 스킬 장착 팝업 (탭 기반 장착 — 모바일 친화)
    // - 장착된 슬롯 탭 → 장착해제
    // - 빈 슬롯 탭 → 선택 모드 진입, 장착 가능한 스킬만 펄스 애니메이션
    // - 펄스 중인 스킬 탭 → 해당 슬롯에 장착
    public static class UITKMageTowerPopupController
    {
        private static VisualElement _overlay;
        private static VisualElement _panel;
        private static VisualElement _slotsCol;
        private static VisualElement _invGrid;

        private static int _selectedSlot;
        private static bool _pickingMode;

        // 펄스 스케줄러 (선택 모드 동안 장착 가능 아이콘들 opacity 토글)
        private static IVisualElementScheduledItem _pulseSched;
        private static readonly List<VisualElement> _equippableItems = new List<VisualElement>();
        private const long PulseIntervalMs = 700;

        private static readonly Button[] _equipSlots = new Button[MageTowerManager.SlotCount];
        private static readonly VisualElement[] _equipSlotIcons = new VisualElement[MageTowerManager.SlotCount];
        private static readonly Label[] _equipSlotLabels = new Label[MageTowerManager.SlotCount];

        public static bool IsOpen => _overlay != null && !_overlay.ClassListContains("hidden");

        public static void Show(int focusSlot = 0)
        {
            _selectedSlot = Mathf.Clamp(focusSlot, 0, MageTowerManager.SlotCount - 1);
            _pickingMode = false;
            EnsureBuilt();
            Refresh();
            _overlay.RemoveFromClassList("hidden");
            _overlay.BringToFront();
        }

        public static void Hide()
        {
            if (_overlay == null) return;
            ExitPickingMode();
            _overlay.AddToClassList("hidden");
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

            _overlay.Add(_panel);

            // 바탕 클릭 → 닫기 (패널 클릭은 안 닫힘)
            _overlay.RegisterCallback<PointerDownEvent>(evt =>
            {
                var target = evt.target as VisualElement;
                if (target == _overlay)
                    Hide();
            }, TrickleDown.TrickleDown);

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
                if (_pickingMode && i == _selectedSlot)
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
                        _equipSlotLabels[i].text = so.nameKor;
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
            _equippableItems.Clear();

            var skills = mgr.GetAllSkills();
            for (int i = 0; i < skills.Count; i++)
            {
                var skill = skills[i];
                if (skill == null) continue;
                _invGrid.Add(BuildInvItem(skill, mgr));
            }

            UpdatePulseState();
        }

        private static VisualElement BuildInvItem(MageTowerSkillSO skill, MageTowerManager mgr)
        {
            int id = skill.id;
            bool owned = mgr.IsOwned(id);
            bool equipped = owned && mgr.IsEquipped(id);
            bool equippable = owned && !equipped;

            var item = new VisualElement();
            item.AddToClassList("mt-inv-item");
            if (!owned)
                item.AddToClassList("mt-inv-item-locked");
            else if (equipped)
                item.AddToClassList("mt-inv-item-equipped");

            var icon = new VisualElement();
            icon.AddToClassList("mt-inv-item-icon");
            icon.pickingMode = PickingMode.Ignore;
            if (skill.icon != null)
                icon.style.backgroundImage = new StyleBackground(skill.icon);

            var nameLabel = new Label(skill.nameKor);
            nameLabel.AddToClassList("mt-inv-item-name");
            nameLabel.pickingMode = PickingMode.Ignore;

            item.Add(icon);
            item.Add(nameLabel);

            if (owned)
            {
                float dmg = mgr.GetEffectiveDamage(id);
                var dmgLabel = new Label($"DMG {dmg:F0}");
                dmgLabel.AddToClassList("mt-inv-item-dmg");
                dmgLabel.pickingMode = PickingMode.Ignore;
                item.Add(dmgLabel);

                item.RegisterCallback<PointerUpEvent>(evt => OnInvItemTapped(id, equippable));
            }

            if (equippable)
                _equippableItems.Add(item);

            return item;
        }

        private static void OnInvItemTapped(int skillId, bool equippable)
        {
            var mgr = MageTowerManager.Instance;
            if (mgr == null) return;

            if (_pickingMode)
            {
                if (!equippable) return; // 장착 불가능한 스킬은 무시
                mgr.Equip(_selectedSlot, skillId);
                ExitPickingMode();
                Refresh();
            }
            else
            {
                // 일반 모드 — 상세 팝업 열기
                UITKMageTowerDetailPopupController.Show(skillId);
            }
        }

        private static void OnEquipSlotClicked(int slotIndex)
        {
            var mgr = MageTowerManager.Instance;
            if (mgr == null) return;

            int skillId = mgr.GetEquippedSkillId(slotIndex);
            if (skillId >= 0)
            {
                // 장착된 슬롯 탭 → 장착해제 (선택 모드였으면 종료)
                mgr.Unequip(slotIndex);
                ExitPickingMode();
                Refresh();
            }
            else
            {
                // 빈 슬롯 탭 → 선택 모드 진입 (다른 슬롯을 이미 고르던 상태면 대상만 변경)
                _selectedSlot = slotIndex;
                _pickingMode = true;
                Refresh();
            }
        }

        private static void ExitPickingMode()
        {
            _pickingMode = false;
            StopPulse();
        }

        // ─── 펄스 애니메이션 ───
        // 선택 모드에서 장착 가능 아이템들의 opacity 를 주기적으로 토글.
        // USS transition(opacity) 로 부드러운 fade 처리.

        private static void UpdatePulseState()
        {
            if (_pickingMode && _equippableItems.Count > 0)
                StartPulse();
            else
                StopPulse();
        }

        private static void StartPulse()
        {
            if (_panel == null) return;
            StopPulse();

            // 초기 상태: dim class 제거
            for (int i = 0; i < _equippableItems.Count; i++)
                _equippableItems[i].RemoveFromClassList("mt-inv-item-pulse-dim");

            _pulseSched = _panel.schedule.Execute(TogglePulse).Every(PulseIntervalMs);
        }

        private static void StopPulse()
        {
            if (_pulseSched != null)
            {
                _pulseSched.Pause();
                _pulseSched = null;
            }
            for (int i = 0; i < _equippableItems.Count; i++)
                _equippableItems[i]?.RemoveFromClassList("mt-inv-item-pulse-dim");
        }

        private static void TogglePulse()
        {
            for (int i = 0; i < _equippableItems.Count; i++)
            {
                var el = _equippableItems[i];
                if (el == null) continue;
                if (el.ClassListContains("mt-inv-item-pulse-dim"))
                    el.RemoveFromClassList("mt-inv-item-pulse-dim");
                else
                    el.AddToClassList("mt-inv-item-pulse-dim");
            }
        }
    }
}
