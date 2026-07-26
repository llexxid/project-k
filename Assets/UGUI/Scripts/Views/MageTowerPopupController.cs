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
    ///  - 장착 슬롯 탭 → 장착해제 / 빈 슬롯 탭 → 선택 모드
    ///  - 선택 모드: 장착 가능 스킬 셀 펄스(UIPulseGroup)
    ///  - 일반 모드에서 보유 스킬 탭 → 상세 팝업
    /// </summary>
    public static class MageTowerPopupController
    {
        private static MageTowerEquipPopupView _view;
        private static readonly List<MageEquipSlotView> _slotViews = new();
        private static readonly List<CanvasGroup> _equippableItems = new();

        private static int _selectedSlot;
        private static bool _pickingMode;

        public static bool IsOpen => _view != null && _view.gameObject.activeSelf;

        public static void Show(int focusSlot = 0)
        {
            _selectedSlot = Mathf.Clamp(focusSlot, 0, MageTowerManager.SlotCount - 1);
            _pickingMode = false;
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

            if (MageTowerHudController.Instance != null)
                MageTowerHudController.Instance.RefreshSlots();
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
                _slotViews[i].Set(so != null ? so.icon : null, so != null ? so.nameKor : null, so == null, active);
            }

            RebuildInventory(mgr);
            UpdatePulseState();
        }

        private static void RebuildInventory(MageTowerManager mgr)
        {
            if (_view == null || _view.invGrid == null) return;
            _equippableItems.Clear();

            var catalog = UIManager.Instance != null ? UIManager.Instance.Catalog : null;
            if (catalog == null || catalog.itemMageSkillCell == null) return;

            // 기존 셀 비활성화 후 파괴 (Destroy 지연 → 레이아웃에 끼지 않게)
            for (int i = _view.invGrid.childCount - 1; i >= 0; i--)
            {
                var child = _view.invGrid.GetChild(i).gameObject;
                child.SetActive(false);
                Object.Destroy(child);
            }

            var skills = mgr.GetAllSkills();
            for (int i = 0; i < skills.Count; i++)
            {
                var skill = skills[i];
                if (skill == null) continue;

                int id = skill.id;
                bool owned = mgr.IsOwned(id);
                bool equipped = owned && mgr.IsEquipped(id);
                bool equippable = owned && !equipped;

                var cellGo = Object.Instantiate(catalog.itemMageSkillCell, _view.invGrid, false);
                var cell = cellGo.GetComponent<MageSkillCellView>();
                if (cell == null) continue;

                float dmg = owned ? mgr.GetEffectiveDamage(id) : 0f;
                cell.Set(skill.icon, skill.nameKor, owned, equipped, dmg, () => OnInvItemTapped(id, equippable));

                if (equippable && cell.canvasGroup != null)
                    _equippableItems.Add(cell.canvasGroup);
            }
        }

        private static void OnInvItemTapped(int skillId, bool equippable)
        {
            var mgr = MageTowerManager.Instance;
            if (mgr == null) return;

            if (_pickingMode)
            {
                if (!equippable) return;
                mgr.Equip(_selectedSlot, skillId);
                ExitPickingMode();
                Refresh();
            }
            else
            {
                MageTowerDetailPopupController.Show(skillId);
            }
        }

        private static void OnEquipSlotClicked(int slotIndex)
        {
            var mgr = MageTowerManager.Instance;
            if (mgr == null) return;

            int skillId = mgr.GetEquippedSkillId(slotIndex);
            if (skillId >= 0)
            {
                mgr.Unequip(slotIndex);
                ExitPickingMode();
                Refresh();
            }
            else
            {
                _selectedSlot = slotIndex;
                _pickingMode = true;
                Refresh();
            }
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
