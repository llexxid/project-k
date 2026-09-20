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

        /// <summary>퀘스트 목적지로 중첩된 인벤토리에서 돌아오면 원래 패널을 다시 연결한다.</summary>
        internal static void Restore(InventoryPanelView view)
        {
            if (_view != view) Populate(view);
        }

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
                    BuildPlaceholderPage($"강화석 {NumberNotation.Format(KingdomIdle.Balance.LocalProgression.Balance(eCurrency.EquipmentStone))}개\n장비를 분해해서 얻으며 장비 강화에 사용합니다.");
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
            if (equipItems.Count > 0 || HasStoredEquipment)
            {
                page.SetSubsection("장비");
                page.SetGridActive(true);
                FillEquipmentGrid(page.grid, equipItems);
                page.SetPlaceholder(page.GetComponentInChildren<EquipmentToolbarView>(true)?.HasMatches==false ? "필터에 맞는 장비가 없습니다." : null);
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
            if (equipItems.Count == 0 && !HasStoredEquipment)
            {
                page.SetGridActive(false);
                page.SetPlaceholder("보유한 장비가 없습니다.");
                return;
            }

            page.SetGridActive(true);
            FillEquipmentGrid(page.grid, equipItems);
            page.SetPlaceholder(page.GetComponentInChildren<EquipmentToolbarView>(true)?.HasMatches==false ? "필터에 맞는 장비가 없습니다." : null);
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
            var toolbar = go.GetComponentInChildren<EquipmentToolbarView>(true);
            if (toolbar != null)
            {
                toolbar.gameObject.SetActive(_activeTab == InvTab.All || _activeTab == InvTab.Equipment);
                toolbar.Bind(data => _players != null && _players.Exists(p => p != null && data.IsAllowedForJob(p.playerStatus?.JobName ?? "")), Refresh);
            }
            return go.GetComponent<InventoryListPageView>();
        }

        // ── 장비 그리드 채우기 (공용 장비 셀 프리팹 재사용) ──

        private static bool HasStoredEquipment => KingdomIdle.Balance.LocalProgression.State.PendingEquipment.Count > 0 || KingdomIdle.Balance.LocalProgression.State.LegacyEquipment.Count > 0;

        private static void FillEquipmentGrid(RectTransform grid, List<(EquipmentInstance item, Player owner)> items)
        {
            if (grid == null) return;

            var toolbar = grid.parent.GetComponentInChildren<EquipmentToolbarView>(true);
            KingdomArmyPanelController.BuildStoredEquipmentCells(grid, Refresh, toolbar != null ? toolbar.Accepts : null);
            if (toolbar != null)
            {
                var owners = new Dictionary<EquipmentInstance, Player>();
                foreach (var entry in items) owners[entry.item] = entry.owner;
                var source = new List<EquipmentInstance>(owners.Keys);
                items = new List<(EquipmentInstance item, Player owner)>();
                foreach (var item in toolbar.Sort(source)) items.Add((item, owners[item]));
            }
            foreach (var (item, owner) in items)
            {
                var capturedItem = item;
                var capturedOwner = owner;
                System.Action onClick = () => ShowInventoryEquipPopup(capturedItem, capturedOwner);

                string jobName = owner?.playerStatus?.JobName ?? "";
                bool isAllowed = _players.Exists(p => p != null && item.baseData.IsAllowedForJob(p.playerStatus?.JobName ?? ""));
                bool isEquipped = owner?.PlayerEquipmentManager != null &&
                                  owner.PlayerEquipmentManager.GetSlotEquipment(item.baseData.slot) == item;

                string enhStr = item.enhancementLevel > 0 ? $" +{item.enhancementLevel}" : "";
                int ownerIdx = _players.IndexOf(owner);
                string sub = ownerIdx >= 0
                    ? $"ATK +{NumberNotation.Format(item.GetFinalAtk())}  (왕국군{ownerIdx + 1})"
                    : $"ATK +{NumberNotation.Format(item.GetFinalAtk())}";

                // 공용 장비 셀 프리팹 사용 (왕국군과 동일)
                KingdomArmyPanelController.InstantiateEquipCell(
                    grid, item.baseData.icon, $"{item.baseData.DisplayName}{enhStr}",
                    new Color(1f, 1f, 1f, 0.85f), sub, UguiTheme.RarityColor(item.baseData.rarity),
                    isEquipped, !isAllowed, isEquipped ? "장착 중" : item.IsLocked ? "잠금" : null, onClick);
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
            string ownerText = isEquipped && ownerIdx >= 0 ? $"장착: 왕국군 {ownerIdx + 1}" : "공용 가방";

            bool maxLevel = item.IsMaxLevel();

            // 강화 정보 (MAX가 아닐 때만)
            string matText = null, rateText = null, expectedText = null;
            bool matShortage = false;
            if (!maxLevel)
            {
                long needed = EquipmentEconomy.EnhanceCost(item);
                long available = KingdomIdle.Balance.LocalProgression.Balance(eCurrency.EquipmentStone);
                matShortage = available < needed;
                matText = $"필요 강화석: {needed}개 (보유: {NumberNotation.Format(available)}개)";

                float successRate = item.GetEnhanceSuccessRate() * 100f;
                rateText = $"성공 확률: {successRate:F0}%";

                int nextAtk = item.GetAttackAtLevel(item.enhancementLevel + 1);
                int nextHP = item.GetFinalMaxHP();
                expectedText = $"강화 시 예상: ATK +{NumberNotation.Format(item.GetFinalAtk())} → +{NumberNotation.Format(nextAtk)}  HP +{NumberNotation.Format(item.GetFinalMaxHP())} → +{NumberNotation.Format(nextHP)}";
            }

            detail.Set(
                item.baseData.icon,
                $"{item.baseData.DisplayName}{enhStr}",
                $"등급: {rarityStr}",
                $"공격력 보너스: +{NumberNotation.Format(item.GetFinalAtk())}",
                $"HP 보너스: +{NumberNotation.Format(item.GetFinalMaxHP())}",
                $"강화 레벨: {item.enhancementLevel} / {item.baseData.maxEnhancementLevel}",
                isEquipped, ownerText,
                maxLevel, matText, matShortage, rateText, expectedText);

            if (detail.backButton != null)
                detail.backButton.onClick.AddListener(() => Refresh());
            if (detail.detailButton != null)
            {
                detail.detailButton.gameObject.SetActive(true);
                detail.detailButton.GetComponentInChildren<TMPro.TMP_Text>().text = "분해";
                detail.detailButton.interactable = !item.IsLocked && !item.IsEquipped;
                detail.detailButton.onClick.AddListener(() => EquipmentActionDialog.Dismantle(EquipmentEconomy.Preview(_ => true, item.instanceId), Refresh));
            }
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
            EquipmentManager equipmentManager = EquipmentManager.Instance;
            if (equipmentManager == null) return;

            if (item.IsMaxLevel())
            {
                ShowToast("이미 최대 강화 레벨입니다.");
                return;
            }

            long needed = EquipmentEconomy.EnhanceCost(item);
            long available = KingdomIdle.Balance.LocalProgression.Balance(eCurrency.EquipmentStone);

            if (available < needed)
            {
                long shortage = needed - available;
                ShowToast($"강화석 부족 (보유 {available} / 필요 {needed}, {shortage}개 부족)");
                return;
            }

            var result = equipmentManager.TryEnhanceDetailed(item);
            if (result == EquipmentManager.EnhancementResult.Success)
            {
                ShowToast($"강화 성공! {item.baseData.DisplayName} +{item.enhancementLevel}");
            }
            else if (result == EquipmentManager.EnhancementResult.ChanceFailed)
            {
                ShowToast($"강화 실패. 다시 확인해 주세요.");
            }
            else ShowToast("강화하지 못했습니다. 강화석과 저장 상태를 확인해 주세요.");

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
                if (item == null || item.baseData == null) continue;
                Player owner = _players.Find(p => p != null && p.PlayerEquipmentManager != null
                    && p.PlayerEquipmentManager.GetSlotEquipment(item.baseData.slot) == item);
                result.Add((item, owner));
            }

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
