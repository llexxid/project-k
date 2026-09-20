using System.Collections.Generic;
using UnityEngine;
using KingdomIdle.MageTower;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// 마탑 스킬 장착 팝업 컨트롤러 (프리팹 기반).
    /// 프리팹 Panel_MageTowerEquip(=MageTowerEquipPopupView)을 1회 인스턴스화해 캐시하고,
    /// 슬롯/보유스킬은 Item_MageEquipSlot / Item_MageSkillCell 프리팹으로 채운다.
    /// 코드로 UI 구조를 생성하지 않는다(런타임 코드빌드 제거 완료).
    /// 슬롯과 스킬을 선택한 뒤 명시적인 버튼으로 장착/교체/해제한다.
    /// </summary>
    public static class MageTowerPopupController
    {
        private static MageTowerEquipPopupView _view;
        private static readonly List<MageEquipSlotView> _slotViews = new();
        private static readonly List<CanvasGroup> _equippableItems = new();
        private static readonly Dictionary<int, MageSkillCellView> _cells = new();

        private static int _selectedSlot;
        private static bool _pickingMode;
        private static int _candidate = -1;

        public static bool IsOpen => _view != null && _view.gameObject.activeSelf;
        public static void RefreshIfOpen() { if (IsOpen) Refresh(); }

        public static void Show(int focusSlot = 0)
        {
            _selectedSlot = Mathf.Clamp(focusSlot, 0, MageTowerManager.SlotCount - 1);
            _pickingMode = true;
            _candidate = -1;
            if (!EnsureBuilt()) return;

            _view.gameObject.SetActive(true);
            _view.transform.SetAsLastSibling();   // BringToFront
            if (_view.panelBox != null) UITween.PopIn(_view.panelBox);
            Refresh();
        }

        public static void Hide()
        {
            if (_view == null) return;
            ExitPickingMode();
            _view.gameObject.SetActive(false);
            // (좌측 스킬 슬롯 HUD 제거됨 — 장착 변경은 AUTO 시전이 다음 틱에 그대로 반영한다)
        }

        private static bool EnsureBuilt()
        {
            if (_view != null) return true;

            var mgr = UIManager.Instance;
            if (mgr == null || mgr.LayerOverlays == null || mgr.Catalog == null || mgr.Catalog.popupMageTowerEquip == null)
            {
                Debug.LogWarning("[MageTowerPopup] 카탈로그의 popupMageTowerEquip 프리팹이 없습니다.");
                return false;
            }

            var go = Object.Instantiate(mgr.Catalog.popupMageTowerEquip, mgr.LayerOverlays, false);
            ModalBackHandler.Bind(go, Hide);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            _view = go.GetComponent<MageTowerEquipPopupView>();
            if (_view == null)
            {
                Debug.LogError("[MageTowerPopup] MageTowerEquipPopupView 컴포넌트가 없습니다.");
                Object.Destroy(go);
                return false;
            }

            if (_view.backdropButton != null) _view.backdropButton.onClick.AddListener(Hide);
            if (_view.closeButton != null) _view.closeButton.onClick.AddListener(Hide);
            if (_view.equipButton != null) _view.equipButton.onClick.AddListener(ApplySelection);
            if (_view.unequipButton != null) _view.unequipButton.onClick.AddListener(RemoveSelection);
            if (_view.detailButton != null) _view.detailButton.onClick.AddListener(() => {
                int id = _candidate >= 0 ? _candidate : MageTowerManager.Instance.GetEquippedSkillId(_selectedSlot);
                if (id >= 0) MageTowerDetailPopupController.Show(id);
            });

            _cells.Clear();
            BuildSlots(mgr);
            return true;
        }

        private static void BuildSlots(UIManager mgr)
        {
            _slotViews.Clear();
            if (_view.slotsContainer == null || mgr.Catalog.itemMageEquipSlot == null) return;

            for (int i = 0; i < MageTowerManager.SlotCount; i++)
            {
                int idx = i;
                var slotGo = Object.Instantiate(mgr.Catalog.itemMageEquipSlot, _view.slotsContainer, false);
                var slotView = slotGo.GetComponent<MageEquipSlotView>();
                if (slotView == null) continue;
                if (slotView.button != null)
                    slotView.button.onClick.AddListener(() => OnEquipSlotClicked(idx));
                _slotViews.Add(slotView);
            }
        }

        private static void Refresh()
        {
            var mgr = MageTowerManager.Instance;
            if (mgr == null || _view == null) return;

            for (int i = 0; i < _slotViews.Count && i < MageTowerManager.SlotCount; i++)
            {
                int skillId = mgr.GetEquippedSkillId(i);
                var so = skillId >= 0 ? mgr.GetSkillById(skillId) : null;
                bool active = _pickingMode && i == _selectedSlot;
                _slotViews[i].Set(so != null ? so.DisplayIcon(mgr.IsBloomEnabled(skillId)) : null, so != null ? so.DisplayName(mgr.IsBloomEnabled(skillId)) : null, so == null, active);
            }

            RebuildInventory(mgr);
            RefreshActions(mgr);
        }

        private static void RebuildInventory(MageTowerManager mgr)
        {
            if (_view == null || _view.invGrid == null) return;
            _equippableItems.Clear();

            var catalog = UIManager.Instance != null ? UIManager.Instance.Catalog : null;
            if (catalog == null || catalog.itemMageSkillCell == null) return;

            var skills = mgr.GetAllSkills();
            for (int i = 0; i < skills.Count; i++)
            {
                var skill = skills[i];
                if (skill == null) continue;

                int id = skill.id;
                bool owned = mgr.IsOwned(id);
                bool equipped = owned && mgr.IsEquipped(id);
                bool equippable = owned && !equipped;

                if (!_cells.TryGetValue(id, out var cell) || cell == null)
                {
                    var cellGo = Object.Instantiate(catalog.itemMageSkillCell, _view.invGrid, false);
                    cell = cellGo.GetComponent<MageSkillCellView>();
                    if (cell == null) continue;
                    _cells[id] = cell;
                }

                float dmg = owned ? mgr.GetEffectiveDamage(id) : 0f;
                cell.Set(skill, owned, equipped, dmg, mgr.IsBloomEnabled(id), () => OnInvItemTapped(id, equippable));
                if (id == _candidate && cell.frameImage != null) cell.frameImage.color = MageSkillPresentation.Accent;

                if (equippable && cell.canvasGroup != null)
                    _equippableItems.Add(cell.canvasGroup);
            }
        }

        private static void OnInvItemTapped(int skillId, bool equippable)
        {
            var mgr = MageTowerManager.Instance;
            if (mgr == null) return;

            _candidate = skillId;
            Refresh();
        }

        private static void OnEquipSlotClicked(int slotIndex)
        {
            var mgr = MageTowerManager.Instance;
            if (mgr == null) return;

            _selectedSlot = slotIndex;
            _pickingMode = true;
            Refresh();
        }

        private static void RefreshActions(MageTowerManager mgr)
        {
            int current = mgr.GetEquippedSkillId(_selectedSlot);
            var chosen = mgr.GetSkillById(_candidate);
            string currentName = mgr.GetSkillById(current)?.DisplayName(mgr.IsBloomEnabled(current)) ?? "빈 슬롯";
            bool canEquip = chosen != null && mgr.IsOwned(_candidate) && current != _candidate;
            if (_view.selectionLabel != null)
                _view.selectionLabel.text = chosen == null ? $"슬롯 {_selectedSlot + 1} · {currentName}\n목록에서 사용할 스킬을 선택하세요." :
                    $"슬롯 {_selectedSlot + 1} · {currentName} → {chosen.DisplayName(mgr.IsBloomEnabled(_candidate))}\n{(mgr.IsOwned(_candidate) ? "진행 중인 효과와 재사용 대기시간은 유지됩니다." : "뽑기에서 획득하면 장착할 수 있습니다.")}";
            if (_view.equipButton != null) _view.equipButton.interactable = canEquip;
            if (_view.equipLabel != null) _view.equipLabel.text = chosen != null && mgr.IsEquipped(_candidate) && current != _candidate ? "자리 바꾸기" : current >= 0 ? "교체" : "장착";
            if (_view.unequipButton != null) _view.unequipButton.interactable = current >= 0;
            if (_view.detailButton != null) _view.detailButton.interactable = chosen != null || current >= 0;
        }

        private static void ApplySelection()
        {
            var mgr = MageTowerManager.Instance;
            if (mgr == null || _candidate < 0) return;
            bool applied = mgr.Equip(_selectedSlot, _candidate);
            if (applied) _candidate = -1;
            Refresh();
            if (!applied && _view.selectionLabel != null) _view.selectionLabel.text = "변경 내용을 저장하지 못했습니다. 잠시 후 다시 시도하세요.";
        }
        private static void RemoveSelection()
        {
            var mgr = MageTowerManager.Instance;
            if (mgr == null) return;
            bool removed = mgr.Unequip(_selectedSlot);
            Refresh();
            if (!removed && _view.selectionLabel != null) _view.selectionLabel.text = "해제 내용을 저장하지 못했습니다. 잠시 후 다시 시도하세요.";
        }

        private static void ExitPickingMode()
        {
            _pickingMode = false;
            if (_view != null && _view.pulse != null) _view.pulse.Stop();
        }

        private static void UpdatePulseState()
        {
            if (_view == null || _view.pulse == null) return;
            if (_pickingMode && _equippableItems.Count > 0)
                _view.pulse.Begin(_equippableItems);
            else
                _view.pulse.Stop();
        }
    }
}
