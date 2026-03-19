using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using KingdomIdle.Gacha;
using Scripts.Core;

namespace KingdomIdle.UIToolkit
{
    /// <summary>
    /// 뽑기 패널 컨트롤러. Panel_Gacha.uxml에 탭 + 콘텐츠를 채운다.
    /// </summary>
    public static class UITKGachaPanelController
    {
        private static readonly int[] PullCounts = { 1, 10, 50 };

        private static int _activeTabIndex;
        private static VisualElement _tabBar;
        private static ScrollView _content;
        private static IReadOnlyList<GachaTableSO> _tables;

        public static void Populate(VisualElement panelRoot)
        {
            if (panelRoot == null) return;

            _tabBar = panelRoot.Q<VisualElement>("GachaTabBar");
            _content = panelRoot.Q<ScrollView>("GachaContent");

            if (_tabBar == null || _content == null) return;

            var mgr = GachaManager.Instance;
            if (mgr == null)
            {
                _content.Add(new Label("GachaManager를 씬에 배치해주세요."));
                return;
            }

            _tables = mgr.GetAllTables();
            if (_tables.Count == 0)
            {
                _content.Add(new Label("등록된 뽑기 테이블이 없습니다."));
                return;
            }

            _activeTabIndex = 0;
            BuildTabs();
            RefreshContent();
        }

        private static void BuildTabs()
        {
            _tabBar.Clear();

            for (int i = 0; i < _tables.Count; i++)
            {
                int idx = i;
                var btn = new Button(() => OnTabClicked(idx));
                btn.text = _tables[i].nameKor;
                btn.AddToClassList("gacha-tab-btn");
                _tabBar.Add(btn);
            }

            UpdateTabStyles();
        }

        private static void OnTabClicked(int index)
        {
            if (index == _activeTabIndex) return;
            _activeTabIndex = index;
            UpdateTabStyles();
            RefreshContent();
        }

        private static void UpdateTabStyles()
        {
            if (_tabBar == null) return;
            for (int i = 0; i < _tabBar.childCount; i++)
            {
                var child = _tabBar[i];
                if (i == _activeTabIndex)
                    child.AddToClassList("gacha-tab-btn-active");
                else
                    child.RemoveFromClassList("gacha-tab-btn-active");
            }
        }

        private static void RefreshContent()
        {
            _content.Clear();

            if (_tables == null || _activeTabIndex >= _tables.Count) return;
            var table = _tables[_activeTabIndex];
            if (table == null) return;

            // 설명
            if (!string.IsNullOrEmpty(table.description))
            {
                var desc = new Label(table.description);
                desc.AddToClassList("gacha-desc");
                _content.Add(desc);
            }

            // 비용 & 보유량
            EconomyBridge.TryGetAmount(table.costCurrency, out int current);
            var costLbl = new Label($"1회 비용: {table.costAmount} {table.costCurrency}  |  보유: {current}");
            costLbl.AddToClassList("gacha-cost");
            _content.Add(costLbl);

            // 뽑기 버튼 3개 (x1, x10, x50)
            var btnRow = new VisualElement();
            btnRow.AddToClassList("gacha-pull-row");

            for (int i = 0; i < PullCounts.Length; i++)
            {
                int count = PullCounts[i];
                int totalCost = table.costAmount * count;

                var pullBtn = new Button(() => OnPullClicked(table, count));
                pullBtn.text = $"뽑기 x{count}\n({totalCost})";
                pullBtn.AddToClassList("gacha-pull-btn");

                if (!table.isImplemented)
                    pullBtn.AddToClassList("gacha-pull-btn-disabled");
                else if (current < totalCost)
                    pullBtn.AddToClassList("gacha-pull-btn-disabled");

                btnRow.Add(pullBtn);
            }

            _content.Add(btnRow);

            // 보상 목록 미리보기
            BuildRewardPreview(table);
        }

        private static void BuildRewardPreview(GachaTableSO table)
        {
            if (table.rewards == null || table.rewards.Count == 0) return;

            var sectionTitle = new Label("획득 가능 보상");
            sectionTitle.AddToClassList("gacha-section-title");
            _content.Add(sectionTitle);

            var grid = new VisualElement();
            grid.AddToClassList("gacha-reward-grid");

            float totalWeight = 0f;
            for (int i = 0; i < table.rewards.Count; i++)
                totalWeight += table.rewards[i].weight;

            for (int i = 0; i < table.rewards.Count; i++)
            {
                var entry = table.rewards[i];
                var card = new VisualElement();
                card.AddToClassList("gacha-reward-card");

                // 장비 보상이면 등급별 테두리 색상 적용
                if (entry.rewardType == eGachaRewardType.Equipment && entry.equipmentData != null)
                {
                    card.AddToClassList($"gacha-rarity-{entry.equipmentData.rarity.ToString().ToLower()}");
                }

                // 아이콘: 장비 보상이면 equipmentData.icon 우선 사용
                Sprite displayIcon = entry.icon;
                if (entry.rewardType == eGachaRewardType.Equipment && entry.equipmentData != null && entry.equipmentData.icon != null)
                    displayIcon = entry.equipmentData.icon;

                if (displayIcon != null)
                {
                    var iconVe = new VisualElement();
                    iconVe.AddToClassList("gacha-reward-icon");
                    iconVe.style.backgroundImage = new StyleBackground(displayIcon);
                    card.Add(iconVe);
                }

                // 이름: 장비 보상이면 equipmentData.equipmentName 우선 사용
                string displayName = entry.nameKor;
                if (entry.rewardType == eGachaRewardType.Equipment && entry.equipmentData != null)
                    displayName = string.IsNullOrEmpty(entry.nameKor) ? entry.equipmentData.equipmentName : entry.nameKor;

                var nameLbl = new Label(displayName);
                nameLbl.AddToClassList("gacha-reward-name");
                card.Add(nameLbl);

                // 장비 보상이면 등급 라벨 추가
                if (entry.rewardType == eGachaRewardType.Equipment && entry.equipmentData != null)
                {
                    var rarityLbl = new Label(GetRarityText(entry.equipmentData.rarity));
                    rarityLbl.AddToClassList("gacha-reward-rarity");
                    rarityLbl.AddToClassList($"gacha-rarity-text-{entry.equipmentData.rarity.ToString().ToLower()}");
                    card.Add(rarityLbl);
                }

                float pct = totalWeight > 0f ? (entry.weight / totalWeight) * 100f : 0f;
                var rateLbl = new Label($"{pct:F1}%");
                rateLbl.AddToClassList("gacha-reward-rate");
                card.Add(rateLbl);

                grid.Add(card);
            }

            _content.Add(grid);
        }

        private static void OnPullClicked(GachaTableSO table, int count)
        {
            PullAndShowResult(table, count);
        }

        /// <summary>
        /// 뽑기를 실행하고 결과를 팝업으로 표시한다.
        /// 외부(UITKUIManager 다시뽑기 버튼)에서도 호출 가능.
        /// </summary>
        public static void PullAndShowResult(GachaTableSO table, int count)
        {
            var uiMgr = UITKUIManager.Instance;

            if (!table.isImplemented)
            {
                if (uiMgr != null) uiMgr.ShowToast("미구현 기능입니다.");
                return;
            }

            var mgr = GachaManager.Instance;
            if (mgr == null) return;

            if (!mgr.CanPullMulti(table, count))
            {
                if (uiMgr != null) uiMgr.ShowToast("재화가 부족합니다.");
                return;
            }

            var results = mgr.TryPull(table, count);
            if (results == null || results.Count == 0)
            {
                if (uiMgr != null) uiMgr.ShowToast("뽑기에 실패했습니다.");
                return;
            }

            // 결과를 팝업으로 표시
            if (uiMgr != null)
                uiMgr.ShowGachaResultPopup(results, table, count);

            RefreshContent();
        }

        private static string GetRarityText(eEquipmentRarity rarity)
        {
            switch (rarity)
            {
                case eEquipmentRarity.Normal: return "일반";
                case eEquipmentRarity.Rare:   return "레어";
                case eEquipmentRarity.Epic:   return "에픽";
                default:                      return rarity.ToString();
            }
        }
    }
}
