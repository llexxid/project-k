using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KingdomIdle.KingdomArmy;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// 인벤토리 패널 컨트롤러 (UITKInventoryPanelController 이식).
    /// 종류별 탭(전체/장비/재료/기타)으로 분류된 아이템을 표시한다.
    /// 현재는 장비(EquipmentInventory)만 실제 데이터가 있다.
    /// </summary>
    public static class InventoryPanelController
    {
        private enum InvTab { All, Equipment, Material, Etc }

        private static InventoryPanelView _view;
        private static InvTab _activeTab;
        private static readonly List<NavTabButtonView> _navButtons = new();

        // 플레이어 목록 (왕국군 전원의 인벤토리를 합산 표시)
        private static List<Player> _players;

        // ── 진입점 ──

        public static void Populate(InventoryPanelView view)
        {
            if (view == null) return;

            _view = view;
            if (_view.content == null || _view.navBar == null) return;

            view.OnClosed = () =>
            {
                if (_view == view)
                {
                    _view = null;
                    _navButtons.Clear();
                }
            };

            var mgr = KingdomArmyManager.Instance;
            _players = mgr != null ? mgr.GetPlayers() : new List<Player>();

            _activeTab = InvTab.All;
            BuildNavBar();
            Refresh();
        }

        // ── 네비게이션 바 ──

        private static void BuildNavBar()
        {
            UguiRuntimeFactory.Clear(_view.navBar);
            _navButtons.Clear();

            var tabs = new (InvTab tab, string label)[]
            {
                (InvTab.All, "전체"),
                (InvTab.Equipment, "장비"),
                (InvTab.Material, "재료"),
                (InvTab.Etc, "기타"),
            };

            var prefab = UIManager.Instance != null && UIManager.Instance.Catalog != null
                ? UIManager.Instance.Catalog.itemNavTabButton
                : null;
            if (prefab == null) return;

            var cat = UIManager.Instance != null ? UIManager.Instance.Catalog : null;

            foreach (var (tab, label) in tabs)
            {
                var t = tab;
                var go = Object.Instantiate(prefab, _view.navBar, false);
                var navBtn = go.GetComponent<NavTabButtonView>();
                if (navBtn == null) continue;

                navBtn.SetLabel(label);

                Sprite tabIcon = null;
                if (cat != null)
                {
                    switch (t)
                    {
                        case InvTab.All: tabIcon = cat.iconBag; break;
                        case InvTab.Equipment: tabIcon = cat.iconSword; break;
                        case InvTab.Material: tabIcon = cat.iconGem; break;
                        default: tabIcon = cat.iconCoin; break;
                    }
                }
                navBtn.SetIcon(tabIcon);
                navBtn.Button.onClick.AddListener(() =>
                {
                    _activeTab = t;
                    Refresh();
                    UpdateNavStyles();
                });
                _navButtons.Add(navBtn);
            }
            UpdateNavStyles();
        }

        private static void UpdateNavStyles()
        {
            for (int i = 0; i < _navButtons.Count; i++)
                _navButtons[i].SetSelected(i == (int)_activeTab, UguiTheme.AccentBlue);
        }

        // ── 콘텐츠 라우터 ──

        private static void Refresh()
        {
            if (_view == null || _view.content == null) return;
            UguiRuntimeFactory.Clear(_view.content);

            switch (_activeTab)
            {
                case InvTab.All:
                    BuildAllView();
                    break;
                case InvTab.Equipment:
                    BuildEquipmentView();
                    break;
                case InvTab.Material:
                    BuildPlaceholder("재료 아이템이 없습니다.");
                    break;
                case InvTab.Etc:
                    BuildPlaceholder("기타 아이템이 없습니다.");
                    break;
            }
        }

        // ── 전체 탭 ──

        private static void BuildAllView()
        {
            AddSectionTitle("인벤토리");

            var equipItems = GatherAllEquipmentItems();
            if (equipItems.Count > 0)
            {
                AddSubsectionTitle("장비");
                BuildEquipmentGrid(equipItems);
            }

            if (equipItems.Count == 0)
                BuildPlaceholder("인벤토리가 비어있습니다.");
        }

        // ── 장비 탭 ──

        private static void BuildEquipmentView()
        {
            AddSectionTitle("장비");

            var equipItems = GatherAllEquipmentItems();
            if (equipItems.Count == 0)
            {
                BuildPlaceholder("보유한 장비가 없습니다.");
                return;
            }

            BuildEquipmentGrid(equipItems);
        }

        // ── 장비 그리드 빌드 ──

        private static void BuildEquipmentGrid(List<(EquipmentInstance item, Player owner)> items)
        {
            var grid = UguiRuntimeFactory.Container(_view.content, "EquipGrid");
            var gridLayout = UguiRuntimeFactory.GridLayout(grid.gameObject,
                new Vector2(160f, 220f), new Vector2(10f, 10f));
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = 6;

            foreach (var (item, owner) in items)
            {
                var capturedItem = item;
                var capturedOwner = owner;
                System.Action onClick = () => ShowInventoryEquipPopup(capturedItem, capturedOwner);

                string jobName = owner?.playerStatus?.JobName ?? "";
                bool isAllowed = item.baseData.IsAllowedForJob(jobName);
                bool isEquipped = owner?.PlayerEquipmentManager != null &&
                                  owner.PlayerEquipmentManager.GetSlotEquipment(item.baseData.slot) == item;

                string enhStr = item.enhancementLevel > 0 ? $" +{item.enhancementLevel}" : "";
                int ownerIdx = _players.IndexOf(owner);
                string sub = ownerIdx >= 0
                    ? $"ATK +{item.GetFinalAtk()}  (왕국군{ownerIdx + 1})"
                    : $"ATK +{item.GetFinalAtk()}";

                // 공용 장비 셀 프리팹 사용 (왕국군과 동일)
                KingdomArmyPanelController.InstantiateEquipCell(
                    grid, item.baseData.icon, $"{item.baseData.equipmentName}{enhStr}",
                    new Color(1f, 1f, 1f, 0.85f), sub, UguiTheme.RarityColor(item.baseData.rarity),
                    isEquipped, !isAllowed, isEquipped ? "장착 중" : null, onClick);
            }
        }

        // ── 인벤토리 장비 클릭 → 상세/강화 팝업 ──

        private static void ShowInventoryEquipPopup(EquipmentInstance item, Player owner)
        {
            if (_view == null || _view.content == null) return;
            UguiRuntimeFactory.Clear(_view.content);
            var content = _view.content;

            // 뒤로가기 (.ka-back-btn: h44 / 22px)
            var backBtn = UguiRuntimeFactory.TextButton(content, "< 인벤토리", 22f, UguiTheme.SurfaceLight, () => Refresh(), out _);
            UguiRuntimeFactory.Preferred((RectTransform)backBtn.transform, height: 44f);

            AddSectionTitle("장비 상세");

            // 장비 정보 (.ka-job-detail-header)
            var infoBox = UguiRuntimeFactory.Box(content, "InfoBox", new Color(1f, 1f, 1f, 0.05f));
            UguiRuntimeFactory.HorizontalLayout(infoBox.gameObject, 16f, new RectOffset(14, 14, 14, 14), TextAnchor.UpperLeft);

            var iconBg = UguiRuntimeFactory.Box(infoBox.transform, "Icon", UguiTheme.SurfaceLight);
            var iconLe = UguiRuntimeFactory.Preferred(iconBg, width: 120f, height: 120f);
            iconLe.minWidth = 120f;
            if (item.baseData.icon != null)
            {
                iconBg.sprite = item.baseData.icon;
                iconBg.color = Color.white;
                iconBg.preserveAspect = true;
            }

            var infoCol = UguiRuntimeFactory.Container(infoBox.transform, "InfoCol");
            UguiRuntimeFactory.VerticalLayout(infoCol.gameObject, 6f);
            UguiRuntimeFactory.Flexible(infoCol, 1f);

            string rarityStr = item.baseData.rarity switch
            {
                eEquipmentRarity.Normal => "일반",
                eEquipmentRarity.Rare => "레어",
                eEquipmentRarity.Epic => "에픽",
                _ => ""
            };

            string enhStr = item.enhancementLevel > 0 ? $" +{item.enhancementLevel}" : "";
            AddInfoLine(infoCol, $"{item.baseData.equipmentName}{enhStr}", 30f, UguiTheme.TextPrimary, bold: true);
            AddInfoLine(infoCol, $"등급: {rarityStr}", 24f, new Color(1f, 1f, 1f, 0.70f));
            AddInfoLine(infoCol, $"공격력 보너스: +{item.GetFinalAtk()}", 24f, new Color(1f, 1f, 1f, 0.70f));
            AddInfoLine(infoCol, $"HP 보너스: +{item.GetFinalMaxHP()}", 24f, new Color(1f, 1f, 1f, 0.70f));
            AddInfoLine(infoCol, $"강화 레벨: {item.enhancementLevel} / {item.baseData.maxEnhancementLevel}", 24f, new Color(1f, 1f, 1f, 0.70f));

            bool isEquipped = owner?.PlayerEquipmentManager != null &&
                              owner.PlayerEquipmentManager.GetSlotEquipment(item.baseData.slot) == item;
            if (isEquipped)
                AddInfoLine(infoCol, "현재 장착 중", 24f, UguiTheme.SuccessGreenBright);

            int ownerIdx = _players.IndexOf(owner);
            if (ownerIdx >= 0)
                AddInfoLine(infoCol, $"소유: 왕국군{ownerIdx + 1}", 24f, new Color(1f, 1f, 1f, 0.70f));

            // ── 액션 버튼들 (.ka-equip-action-row / .ka-action-btn) ──
            var btnRow = UguiRuntimeFactory.Container(content, "ActionRow");
            UguiRuntimeFactory.HorizontalLayout(btnRow.gameObject, 12f, null, TextAnchor.MiddleCenter, expandWidth: true);
            UguiRuntimeFactory.Preferred(btnRow.gameObject.AddComponent<LayoutElement>(), height: 64f);

            var detailBtn = UguiRuntimeFactory.TextButton(btnRow, "상세", 28f, UguiTheme.AccentBlue,
                () => ShowToast("상세 기능 미구현"), out _);
            UguiRuntimeFactory.Flexible((RectTransform)detailBtn.transform, 1f);

            if (item.IsMaxLevel())
            {
                var maxBtn = UguiRuntimeFactory.TextButton(btnRow, "강화 MAX", 28f, UguiTheme.DisabledGrey, null, out _);
                maxBtn.interactable = false;
                UguiRuntimeFactory.Flexible((RectTransform)maxBtn.transform, 1f);
            }
            else
            {
                var capturedItem = item;
                var capturedOwner = owner;
                var enhBtn = UguiRuntimeFactory.TextButton(btnRow, "강화", 28f, UguiTheme.EnhanceOrange,
                    () => TryEnhanceFromInventory(capturedItem, capturedOwner), out _);
                UguiRuntimeFactory.Flexible((RectTransform)enhBtn.transform, 1f);
            }

            // 강화 정보
            BuildEnhanceInfo(item);
        }

        /// <summary>인벤토리에서 강화를 시도한다. 왕국군 장비 탭의 강화와 동일한 로직.</summary>
        private static void TryEnhanceFromInventory(EquipmentInstance item, Player owner)
        {
            var equipMgr = owner?.PlayerEquipmentManager;
            EquipmentManager equipmentManager = EquipmentManager.Instance;
            if (equipMgr == null) return;

            if (item.IsMaxLevel())
            {
                ShowToast("이미 최대 강화 레벨입니다.");
                return;
            }

            int needed = item.GetMaterialCount();
            int available = 0;
            if (equipmentManager.Inventory != null)
            {
                foreach (var inv in equipmentManager.Inventory.Items)
                {
                    if (inv != item && inv.baseData == item.baseData)
                        available++;
                }
            }

            if (available < needed)
            {
                int shortage = needed - available;
                ShowToast($"동일 장비 부족! (보유: {available}/{needed}개, {shortage}개 부족)");
                return;
            }

            bool success = equipmentManager.TryEnhance(item);
            if (success)
            {
                float nextRate = item.GetEnhanceSuccessRate() * 100f;
                ShowToast($"강화 성공! {item.baseData.equipmentName} +{item.enhancementLevel} (다음 확률: {nextRate:F0}%)");
            }
            else
            {
                ShowToast($"강화 실패... 재료 {needed}개가 소모되었습니다.");
            }

            // 팝업 다시 표시
            ShowInventoryEquipPopup(item, owner);
        }

        /// <summary>강화 관련 정보 (필요 재료, 성공 확률 등)</summary>
        private static void BuildEnhanceInfo(EquipmentInstance item)
        {
            if (_view == null || item.IsMaxLevel()) return;
            var content = _view.content;

            AddSubsectionTitle("강화 정보");

            int needed = item.GetMaterialCount();
            int available = 0;
            if (EquipmentManager.Instance != null)
            {
                foreach (var inv in EquipmentManager.Instance.Inventory.Items)
                {
                    if (inv != item && inv.baseData == item.baseData)
                        available++;
                }
            }

            float successRate = item.GetEnhanceSuccessRate() * 100f;

            var matColor = available < needed ? UguiTheme.WarnRed : new Color(1f, 1f, 1f, 0.70f);
            AddInfoLine(content, $"필요 재료: {item.baseData.equipmentName} x{needed} (보유: {available}개)", 24f, matColor);

            AddInfoLine(content, $"성공 확률: {successRate:F0}%", 24f, new Color(1f, 1f, 1f, 0.70f));

            // 강화 후 예상 스탯
            int nextAtk = item.baseData.bonusAtk + (int)(item.baseData.bonusAtk * item.baseData.atkGrowthPerLevel * (item.enhancementLevel + 1));
            int nextHP = item.baseData.bonusMaxHP + (int)(item.baseData.bonusMaxHP * item.baseData.hpGrowthPerLevel * (item.enhancementLevel + 1));
            AddInfoLine(content,
                $"강화 시 예상: ATK +{item.GetFinalAtk()} → +{nextAtk}  HP +{item.GetFinalMaxHP()} → +{nextHP}",
                24f, new Color(1f, 1f, 1f, 0.70f));
        }

        // ── 데이터 수집 ──

        /// <summary>모든 왕국군 멤버의 EquipmentInventory를 합산하여 반환한다.</summary>
        private static List<(EquipmentInstance item, Player owner)> GatherAllEquipmentItems()
        {
            var result = new List<(EquipmentInstance item, Player owner)>();
            if (_players == null || _players.Count == 0) return result;

            // 현재 인벤토리는 전체 인벤토리 1개로 통합하고 각 플레이어마다의 인벤토리는 제거한 상태입니다.
            // 따라서 현재는 임시로 p[0] 플레이어를 지정해 놓았습니다 (원본 주석 유지)
            foreach (var item in EquipmentManager.Instance.Inventory.Items)
            {
                result.Add((item, _players[0]));
            }

            // 등급 내림차순 → 강화레벨 내림차순 정렬
            result.Sort((a, b) =>
            {
                int cmp = b.item.baseData.rarity.CompareTo(a.item.baseData.rarity);
                if (cmp != 0) return cmp;
                return b.item.enhancementLevel.CompareTo(a.item.enhancementLevel);
            });

            return result;
        }

        // ── 유틸 ──

        private static void AddSectionTitle(string text)
        {
            var lbl = UguiRuntimeFactory.Label(_view.content, text, 28f, UguiTheme.TextPrimary, TextAlignmentOptions.Left, bold: true);
            UguiRuntimeFactory.Preferred(lbl, height: 40f);
        }

        private static void AddSubsectionTitle(string text)
        {
            var lbl = UguiRuntimeFactory.Label(_view.content, text, 24f, new Color(1f, 1f, 1f, 0.90f), TextAlignmentOptions.Left, bold: true);
            UguiRuntimeFactory.Preferred(lbl, height: 34f);
        }

        private static void BuildPlaceholder(string msg)
        {
            var lbl = UguiRuntimeFactory.Label(_view.content, msg, 24f, new Color(1f, 1f, 1f, 0.40f), TextAlignmentOptions.Center);
            UguiRuntimeFactory.Preferred(lbl, height: 60f);
        }

        private static void AddCardLabel(Transform parent, string text, float size, Color color)
        {
            var lbl = UguiRuntimeFactory.Label(parent, text, size, color, TextAlignmentOptions.Center);
            UguiRuntimeFactory.Preferred(lbl, height: size + 8f);
        }

        private static void AddInfoLine(RectTransform parent, string text, float size, Color color, bool bold = false)
        {
            var lbl = UguiRuntimeFactory.Label(parent, text, size, color, TextAlignmentOptions.Left, bold, wrap: true);
            UguiRuntimeFactory.Preferred(lbl, height: size + 10f);
        }

        private static void ShowToast(string msg)
        {
            var uiMgr = UIManager.Instance;
            if (uiMgr != null) uiMgr.ShowToast(msg);
        }
    }
}
