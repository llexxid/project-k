using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using KingdomIdle.KingdomArmy;

namespace KingdomIdle.UIToolkit
{
    /// <summary>
    /// 인벤토리 패널 컨트롤러.
    /// 종류별 탭(전체/장비/재료/기타)으로 분류된 아이템을 표시한다.
    /// 현재는 장비(EquipmentInventory)만 실제 데이터가 있다.
    /// </summary>
    public static class UITKInventoryPanelController
    {
        private enum InvTab { All, Equipment, Material, Etc }

        private static ScrollView _content;
        private static VisualElement _navBar;
        private static InvTab _activeTab;

        // 플레이어 목록 (왕국군 전원의 인벤토리를 합산 표시)
        private static List<Player> _players;

        // ── 진입점 ──

        public static void Populate(VisualElement panelRoot)
        {
            if (panelRoot == null) return;

            _content = panelRoot.Q<ScrollView>("InvContent");
            _navBar = panelRoot.Q<VisualElement>("InvNavBar");

            if (_content == null || _navBar == null) return;

            var mgr = KingdomArmyManager.Instance;
            _players = mgr != null ? mgr.GetPlayers() : new List<Player>();

            _activeTab = InvTab.All;
            BuildNavBar();
            Refresh();
        }

        // ── 네비게이션 바 ──

        private static void BuildNavBar()
        {
            _navBar.Clear();

            var tabs = new (InvTab tab, string label)[]
            {
                (InvTab.All, "전체"),
                (InvTab.Equipment, "장비"),
                (InvTab.Material, "재료"),
                (InvTab.Etc, "기타"),
            };

            foreach (var (tab, label) in tabs)
            {
                var t = tab;
                var btn = new Button(() => { _activeTab = t; Refresh(); UpdateNavStyles(); });
                btn.text = label;
                btn.AddToClassList("ka-nav-btn");
                _navBar.Add(btn);
            }
            UpdateNavStyles();
        }

        private static void UpdateNavStyles()
        {
            if (_navBar == null) return;
            int idx = 0;
            foreach (var child in _navBar.Children())
            {
                if (idx == (int)_activeTab)
                    child.AddToClassList("ka-nav-btn-active");
                else
                    child.RemoveFromClassList("ka-nav-btn-active");
                idx++;
            }
        }

        // ── 콘텐츠 라우터 ──

        private static void Refresh()
        {
            _content.Clear();

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
            _content.Add(MakeLabel("인벤토리", "ka-section-title"));

            // 장비 섹션
            var equipItems = GatherAllEquipmentItems();
            if (equipItems.Count > 0)
            {
                _content.Add(MakeLabel("장비", "ka-subsection-title"));
                BuildEquipmentGrid(equipItems);
            }

            // 재료/기타는 아직 데이터 없음
            if (equipItems.Count == 0)
                _content.Add(MakeLabel("인벤토리가 비어있습니다.", "ka-placeholder-text"));
        }

        // ── 장비 탭 ──

        private static void BuildEquipmentView()
        {
            _content.Add(MakeLabel("장비", "ka-section-title"));

            var equipItems = GatherAllEquipmentItems();
            if (equipItems.Count == 0)
            {
                _content.Add(MakeLabel("보유한 장비가 없습니다.", "ka-placeholder-text"));
                return;
            }

            BuildEquipmentGrid(equipItems);
        }

        // ── 장비 그리드 빌드 ──

        private static void BuildEquipmentGrid(List<(EquipmentInstance item, Player owner)> items)
        {
            var grid = new VisualElement();
            grid.AddToClassList("ka-equip-grid");

            foreach (var (item, owner) in items)
            {
                var card = new Button(() => ShowInventoryEquipPopup(item, owner));
                card.AddToClassList("ka-equip-slot");
                card.AddToClassList("inv-equip-card");

                string jobName = owner?.playerStatus?.JobName ?? "";
                bool isAllowed = item.baseData.IsAllowedForJob(jobName);
                bool isEquipped = owner?.PlayerEquipmentManager != null &&
                                  owner.PlayerEquipmentManager.GetSlotEquipment(item.baseData.slot) == item;

                if (!isAllowed)
                    card.AddToClassList("ka-equip-dimmed");

                if (isEquipped)
                    card.AddToClassList("ka-equip-equipped");

                // 등급 색상 바
                var rarityBar = new VisualElement();
                rarityBar.AddToClassList("inv-rarity-bar");
                rarityBar.AddToClassList($"inv-rarity-{item.baseData.rarity.ToString().ToLower()}");
                card.Add(rarityBar);

                // 아이콘
                var iconVe = new VisualElement();
                iconVe.AddToClassList("ka-equip-icon");
                if (item.baseData.icon != null)
                    iconVe.style.backgroundImage = new StyleBackground(item.baseData.icon);
                card.Add(iconVe);

                // 이름 + 강화
                string enhStr = item.enhancementLevel > 0 ? $" +{item.enhancementLevel}" : "";
                card.Add(MakeLabel($"{item.baseData.equipmentName}{enhStr}", "ka-equip-slot-name"));

                // 스탯
                card.Add(MakeLabel($"ATK +{item.GetFinalAtk()}", "ka-equip-slot-empty"));

                // 장착 상태
                if (isEquipped)
                    card.Add(MakeLabel("장착 중", "ka-frag-ready"));

                // 소유자
                int ownerIdx = _players.IndexOf(owner);
                if (ownerIdx >= 0)
                    card.Add(MakeLabel($"왕국군{ownerIdx + 1}", "inv-owner-label"));

                grid.Add(card);
            }

            _content.Add(grid);
        }

        // ── 인벤토리 장비 클릭 → 상세/강화 팝업 ──

        private static void ShowInventoryEquipPopup(EquipmentInstance item, Player owner)
        {
            _content.Clear();

            // 뒤로가기
            var backBtn = new Button(() => Refresh());
            backBtn.text = "← 인벤토리";
            backBtn.AddToClassList("ka-back-btn");
            _content.Add(backBtn);

            _content.Add(MakeLabel("장비 상세", "ka-section-title"));

            // 장비 정보
            var infoBox = new VisualElement();
            infoBox.AddToClassList("ka-job-detail-header");

            var iconVe = new VisualElement();
            iconVe.AddToClassList("ka-job-detail-img");
            if (item.baseData.icon != null)
                iconVe.style.backgroundImage = new StyleBackground(item.baseData.icon);
            infoBox.Add(iconVe);

            var infoCol = new VisualElement();
            infoCol.AddToClassList("ka-job-detail-info");

            string rarityStr = item.baseData.rarity switch
            {
                eEquipmentRarity.Normal => "일반",
                eEquipmentRarity.Rare   => "레어",
                eEquipmentRarity.Epic   => "에픽",
                _ => ""
            };

            string enhStr = item.enhancementLevel > 0 ? $" +{item.enhancementLevel}" : "";
            infoCol.Add(MakeLabel($"{item.baseData.equipmentName}{enhStr}", "ka-job-detail-name"));
            infoCol.Add(MakeLabel($"등급: {rarityStr}", "ka-stat-line"));
            infoCol.Add(MakeLabel($"공격력 보너스: +{item.GetFinalAtk()}", "ka-stat-line"));
            infoCol.Add(MakeLabel($"HP 보너스: +{item.GetFinalMaxHP()}", "ka-stat-line"));
            infoCol.Add(MakeLabel($"강화 레벨: {item.enhancementLevel} / {item.baseData.maxEnhancementLevel}", "ka-stat-line"));

            bool isEquipped = owner?.PlayerEquipmentManager != null &&
                              owner.PlayerEquipmentManager.GetSlotEquipment(item.baseData.slot) == item;
            if (isEquipped)
                infoCol.Add(MakeLabel("현재 장착 중", "ka-frag-ready"));

            // 소유자
            int ownerIdx = _players.IndexOf(owner);
            if (ownerIdx >= 0)
                infoCol.Add(MakeLabel($"소유: 왕국군{ownerIdx + 1}", "ka-stat-line"));

            infoBox.Add(infoCol);
            _content.Add(infoBox);

            // ── 액션 버튼들 ──
            var btnRow = new VisualElement();
            btnRow.AddToClassList("ka-equip-action-row");

            // 상세 버튼 (미구현)
            var detailBtn = new Button(() => ShowToast("상세 기능 미구현"));
            detailBtn.text = "상세";
            detailBtn.AddToClassList("ka-action-btn");
            btnRow.Add(detailBtn);

            // 강화 버튼
            if (item.IsMaxLevel())
            {
                var maxBtn = new Button();
                maxBtn.text = "강화 MAX";
                maxBtn.AddToClassList("ka-action-btn");
                maxBtn.AddToClassList("ka-action-btn-disabled");
                maxBtn.SetEnabled(false);
                btnRow.Add(maxBtn);
            }
            else
            {
                var enhButton = new Button(() => TryEnhanceFromInventory(item, owner));
                enhButton.text = "강화";
                enhButton.AddToClassList("ka-action-btn");
                enhButton.AddToClassList("ka-action-btn-enhance");
                btnRow.Add(enhButton);
            }

            _content.Add(btnRow);

            // 강화 정보
            BuildEnhanceInfo(item, owner?.PlayerEquipmentManager);
        }

        /// <summary>
        /// 인벤토리에서 강화를 시도한다. 왕국군 장비 탭의 강화와 동일한 로직.
        /// </summary>
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
        private static void BuildEnhanceInfo(EquipmentInstance item, PlayerEquipmentManager equipMgr)
        {
            if (item.IsMaxLevel()) return;

            _content.Add(MakeLabel("강화 정보", "ka-subsection-title"));

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

            var matLabel = MakeLabel($"필요 재료: {item.baseData.equipmentName} x{needed} (보유: {available}개)", "ka-stat-line");
            if (available < needed)
                matLabel.AddToClassList("ka-equip-dimmed-text");
            _content.Add(matLabel);

            _content.Add(MakeLabel($"성공 확률: {successRate:F0}%", "ka-stat-line"));

            // 강화 후 예상 스탯
            int nextAtk = item.baseData.bonusAtk + (int)(item.baseData.bonusAtk * item.baseData.atkGrowthPerLevel * (item.enhancementLevel + 1));
            int nextHP = item.baseData.bonusMaxHP + (int)(item.baseData.bonusMaxHP * item.baseData.hpGrowthPerLevel * (item.enhancementLevel + 1));
            _content.Add(MakeLabel($"강화 시 예상: ATK +{item.GetFinalAtk()} → +{nextAtk}  HP +{item.GetFinalMaxHP()} → +{nextHP}", "ka-stat-line"));
        }

        // ── 데이터 수집 ──

        /// <summary>모든 왕국군 멤버의 EquipmentInventory를 합산하여 반환한다.</summary>
        private static List<(EquipmentInstance item, Player owner)> GatherAllEquipmentItems()
        {
            var result = new List<(EquipmentInstance item, Player owner)>();
            if (_players == null) return result;

            // foreach (var p in _players)
            // {
            //     if (EquipmentManager.Instance.Inventory == null) continue;
            //     foreach (var item in p.PlayerEquipmentManager.Inventory.Items)
            //         result.Add((item, p));
            // }
            //현재 인벤토리는 전체 인벤토리 1개로 통합하고 각 플레이어마다의 인벤토리는 제거한 상태입니다. 따라서 현재는 임시로 p[0] 플레이어를 지정해 놓았습니다
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

        private static void BuildPlaceholder(string msg)
        {
            _content.Add(MakeLabel(msg, "ka-placeholder-text"));
        }

        private static Label MakeLabel(string text, string className)
        {
            var lbl = new Label(text);
            lbl.AddToClassList(className);
            return lbl;
        }

        private static void ShowToast(string msg)
        {
            var uiMgr = UITKUIManager.Instance;
            if (uiMgr != null) uiMgr.ShowToast(msg);
        }
    }
}
