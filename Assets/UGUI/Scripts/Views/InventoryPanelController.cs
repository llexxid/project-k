using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using KingdomIdle.KingdomArmy;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// 인벤토리 패널 컨트롤러 (UITKInventoryPanelController 이식 → 프리팹 기반 전환 완료).
    /// 종류별 탭(전체/장비/재료/기타)으로 분류된 아이템을 표시한다.
    /// 현재는 장비(EquipmentInventory)만 실제 데이터가 있다.
    /// 고정 구조(목록 페이지/상세 페이지)는 프리팹 + View로, 반복 셀은 itemEquipCell 프리팹으로 채운다.
    /// (런타임 코드 UI 생성 제거 완료 — 프리팹/View만 사용)
    /// </summary>
    public static class InventoryPanelController
    {
        private enum InvTab { All, Equipment, Material, Etc }

        private static InventoryPanelView _view;
        private static InvTab _activeTab;
        private static readonly List<NavTabButtonView> _navButtons = new();

        // 플레이어 목록 (왕국군 전원의 인벤토리를 합산 표시)
        private static List<Player> _players;

        private static UIViewCatalog Catalog =>
            UIManager.Instance != null ? UIManager.Instance.Catalog : null;

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
            ClearChildren(_view.navBar);
            _navButtons.Clear();

            var tabs = new (InvTab tab, string label)[]
            {
                (InvTab.All, "전체"),
                (InvTab.Equipment, "장비"),
                (InvTab.Material, "재료"),
                (InvTab.Etc, "기타"),
            };

            var cat = Catalog;
            var prefab = cat != null ? cat.itemNavTabButton : null;
            if (prefab == null) return;

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
            ClearChildren(_view.content);

            switch (_activeTab)
            {
                case InvTab.All:
                    BuildAllView();
                    break;
                case InvTab.Equipment:
                    BuildEquipmentView();
                    break;
                case InvTab.Material:
                    BuildPlaceholderPage("재료 아이템이 없습니다.");
                    break;
                case InvTab.Etc:
                    BuildPlaceholderPage("기타 아이템이 없습니다.");
                    break;
            }
        }

        // ── 전체 탭 ──

        private static void BuildAllView()
        {
            var page = SpawnListPage();
            if (page == null) return;

            page.SetSection("인벤토리");

            var equipItems = GatherAllEquipmentItems();
            if (equipItems.Count > 0)
            {
                page.SetSubsection("장비");
                page.SetGridActive(true);
                FillEquipmentGrid(page.grid, equipItems);
                page.SetPlaceholder(null);
            }
            else
            {
                page.SetSubsection(null);
                page.SetGridActive(false);
                page.SetPlaceholder("인벤토리가 비어있습니다.");
            }
        }

        // ── 장비 탭 ──

        private static void BuildEquipmentView()
        {
            var page = SpawnListPage();
            if (page == null) return;

            page.SetSection("장비");
            page.SetSubsection(null);

            var equipItems = GatherAllEquipmentItems();
            if (equipItems.Count == 0)
            {
                page.SetGridActive(false);
                page.SetPlaceholder("보유한 장비가 없습니다.");
                return;
            }

            page.SetGridActive(true);
            FillEquipmentGrid(page.grid, equipItems);
            page.SetPlaceholder(null);
        }

        // ── 재료/기타 (플레이스홀더 전용) ──

        private static void BuildPlaceholderPage(string msg)
        {
            var page = SpawnListPage();
            if (page == null) return;

            page.SetSection(null);
            page.SetSubsection(null);
            page.SetGridActive(false);
            page.SetPlaceholder(msg);
        }

        private static InventoryListPageView SpawnListPage()
        {
            var cat = Catalog;
            var prefab = cat != null ? cat.itemInventoryListPage : null;
            if (prefab == null) return null;
            var go = Object.Instantiate(prefab, _view.content, false);
            return go.GetComponent<InventoryListPageView>();
        }

        // ── 장비 그리드 채우기 (공용 장비 셀 프리팹 재사용) ──

        private static void FillEquipmentGrid(RectTransform grid, List<(EquipmentInstance item, Player owner)> items)
        {
            if (grid == null) return;

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

        // ── 인벤토리 장비 클릭 → 상세/강화 페이지 ──

        private static void ShowInventoryEquipPopup(EquipmentInstance item, Player owner)
        {
            if (_view == null || _view.content == null) return;
            ClearChildren(_view.content);

            var cat = Catalog;
            var prefab = cat != null ? cat.itemInventoryEquipDetail : null;
            if (prefab == null) return;

            var go = Object.Instantiate(prefab, _view.content, false);
            var detail = go.GetComponent<InventoryEquipDetailView>();
            if (detail == null) return;

            string rarityStr = item.baseData.rarity switch
            {
                eEquipmentRarity.Normal => "일반",
                eEquipmentRarity.Rare => "레어",
                eEquipmentRarity.Epic => "에픽",
                _ => ""
            };

            string enhStr = item.enhancementLevel > 0 ? $" +{item.enhancementLevel}" : "";

            bool isEquipped = owner?.PlayerEquipmentManager != null &&
                              owner.PlayerEquipmentManager.GetSlotEquipment(item.baseData.slot) == item;

            int ownerIdx = _players.IndexOf(owner);
            string ownerText = ownerIdx >= 0 ? $"소유: 왕국군{ownerIdx + 1}" : null;

            bool maxLevel = item.IsMaxLevel();

            // 강화 정보 (MAX가 아닐 때만)
            string matText = null, rateText = null, expectedText = null;
            bool matShortage = false;
            if (!maxLevel)
            {
                int needed = item.GetMaterialCount();
                int available = 0;
                if (EquipmentManager.Instance != null && EquipmentManager.Instance.Inventory != null)
                {
                    foreach (var inv in EquipmentManager.Instance.Inventory.Items)
                    {
                        if (inv != item && inv.baseData == item.baseData)
                            available++;
                    }
                }
                matShortage = available < needed;
                matText = $"필요 재료: {item.baseData.equipmentName} x{needed} (보유: {available}개)";

                float successRate = item.GetEnhanceSuccessRate() * 100f;
                rateText = $"성공 확률: {successRate:F0}%";

                int nextAtk = item.baseData.bonusAtk + (int)(item.baseData.bonusAtk * item.baseData.atkGrowthPerLevel * (item.enhancementLevel + 1));
                int nextHP = item.baseData.bonusMaxHP + (int)(item.baseData.bonusMaxHP * item.baseData.hpGrowthPerLevel * (item.enhancementLevel + 1));
                expectedText = $"강화 시 예상: ATK +{item.GetFinalAtk()} → +{nextAtk}  HP +{item.GetFinalMaxHP()} → +{nextHP}";
            }

            detail.Set(
                item.baseData.icon,
                $"{item.baseData.equipmentName}{enhStr}",
                $"등급: {rarityStr}",
                $"공격력 보너스: +{item.GetFinalAtk()}",
                $"HP 보너스: +{item.GetFinalMaxHP()}",
                $"강화 레벨: {item.enhancementLevel} / {item.baseData.maxEnhancementLevel}",
                isEquipped, ownerText,
                maxLevel, matText, matShortage, rateText, expectedText);

            if (detail.backButton != null)
                detail.backButton.onClick.AddListener(() => Refresh());
            if (detail.detailButton != null)
                detail.detailButton.onClick.AddListener(() => ShowToast("상세 기능 미구현"));
            if (!maxLevel && detail.enhanceButton != null)
            {
                var capturedItem = item;
                var capturedOwner = owner;
                detail.enhanceButton.onClick.AddListener(() => TryEnhanceFromInventory(capturedItem, capturedOwner));
            }
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

        /// <summary>부모의 자식 전부 파괴 (동적 리스트 재구성용).</summary>
        private static void ClearChildren(Transform parent)
        {
            if (parent == null) return;
            for (int i = parent.childCount - 1; i >= 0; i--)
                Object.Destroy(parent.GetChild(i).gameObject);
        }

        private static void ShowToast(string msg)
        {
            var uiMgr = UIManager.Instance;
            if (uiMgr != null) uiMgr.ShowToast(msg);
        }
    }
}
