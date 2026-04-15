using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using KingdomIdle.Gacha;
using Scripts.Core;

namespace KingdomIdle.UIToolkit
{
    /// <summary>
    /// 뽑기 패널 컨트롤러. Panel_Gacha.uxml 에 탭 + 콘텐츠를 채운다.
    /// 모바일 방치형 가챠 표준 레이아웃:
    ///   ┌─ 탭바 (장비/스킬)
    ///   ├─ 설명
    ///   ├─ 보유/비용 바
    ///   ├─ 확률 요약 (Epic/Rare/Normal 집계)
    ///   ├─ 뽑기 버튼 (x1 / x10)  ← 뽑는 중에는 비활성
    ///   └─ 보상 목록 미리보기 (카드 그리드)
    /// 뽑기 중에는 버튼이 자동 비활성화되며, 완료되면 결과 팝업 후 원상복구된다.
    /// </summary>
    public static class UITKGachaPanelController
    {
        // 방치형 가챠 표준: x1 / x10 (50 은 제거 — 비용/체감 기대치 과다)
        private static readonly int[] PullCounts = { 1, 10 };

        private static int _activeTabIndex;
        private static VisualElement _tabBar;
        private static ScrollView _content;
        private static IReadOnlyList<GachaTableSO> _tables;

        // 뽑기 버튼 참조 (in-flight 중 비활성화)
        private static readonly List<Button> _activePullButtons = new List<Button>();

        // GachaManager 이벤트 구독 상태 (중복 구독 방지)
        private static bool _subscribedToManager;

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

            SubscribeManagerEvents(mgr);

            _tables = mgr.GetAllTables();
            if (_tables == null || _tables.Count == 0)
            {
                _content.Add(new Label("등록된 뽑기 테이블이 없습니다."));
                return;
            }

            _activeTabIndex = 0;
            BuildTabs();
            RefreshContent();
        }

        private static void SubscribeManagerEvents(GachaManager mgr)
        {
            if (_subscribedToManager) return;
            mgr.OnPullStateChanged += OnPullStateChanged;
            _subscribedToManager = true;
        }

        // ── 탭바 ──────────────────────────────────────────────────────

        private static void BuildTabs()
        {
            _tabBar.Clear();

            for (int i = 0; i < _tables.Count; i++)
            {
                int idx = i;
                var btn = new Button(() => OnTabClicked(idx));
                btn.text = _tables[i] != null ? _tables[i].nameKor : "?";
                btn.AddToClassList("gacha-tab-btn");
                _tabBar.Add(btn);
            }

            UpdateTabStyles();
        }

        private static void OnTabClicked(int index)
        {
            if (GachaManager.Instance != null && GachaManager.Instance.IsPulling) return;
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

        // ── 콘텐츠 본문 ───────────────────────────────────────────────

        private static void RefreshContent()
        {
            if (_content == null) return;
            _content.Clear();
            _activePullButtons.Clear();

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

            // 보유/비용 바
            EconomyBridge.TryGetAmount(table.costCurrency, out long current);
            var costLbl = new Label($"1회 비용: {table.costAmount:N0} {GetCurrencyLabel(table.costCurrency)}  |  보유: {current:N0}");
            costLbl.AddToClassList("gacha-cost");
            _content.Add(costLbl);

            // 확률 요약 (등급별 가중치 집계)
            var rateRow = BuildRateSummaryRow(table);
            if (rateRow != null) _content.Add(rateRow);

            // 뽑기 버튼 행
            var btnRow = new VisualElement();
            btnRow.AddToClassList("gacha-pull-row");

            bool pulling = GachaManager.Instance != null && GachaManager.Instance.IsPulling;

            for (int i = 0; i < PullCounts.Length; i++)
            {
                int count = PullCounts[i];
                long totalCost = (long)table.costAmount * count;

                var pullBtn = new Button(() => OnPullClicked(table, count));
                pullBtn.AddToClassList("gacha-pull-btn");
                pullBtn.text = $"뽑기 x{count}\n({totalCost:N0})";

                bool disabled = !table.isImplemented
                             || current < totalCost
                             || pulling;

                if (disabled)
                {
                    pullBtn.AddToClassList("gacha-pull-btn-disabled");
                    pullBtn.SetEnabled(false);
                }

                _activePullButtons.Add(pullBtn);
                btnRow.Add(pullBtn);
            }

            _content.Add(btnRow);

            // 보상 목록 미리보기
            BuildRewardPreview(table);
        }

        private static VisualElement BuildRateSummaryRow(GachaTableSO table)
        {
            if (table?.rewards == null || table.rewards.Count == 0) return null;

            float total = 0f;
            for (int i = 0; i < table.rewards.Count; i++) total += Mathf.Max(0f, table.rewards[i].weight);
            if (total <= 0f) return null;

            // 등급별 가중치 집계 — 장비 보상이 섞여있을 때만 표시
            float wNormal = 0f, wRare = 0f, wEpic = 0f;
            bool hasAny = false;
            for (int i = 0; i < table.rewards.Count; i++)
            {
                var r = table.rewards[i];
                if (r == null) continue;
                if (r.rewardType != eGachaRewardType.Equipment || r.equipmentData == null) continue;
                hasAny = true;
                switch (r.equipmentData.rarity)
                {
                    case eEquipmentRarity.Normal: wNormal += r.weight; break;
                    case eEquipmentRarity.Rare:   wRare   += r.weight; break;
                    case eEquipmentRarity.Epic:   wEpic   += r.weight; break;
                }
            }

            if (!hasAny) return null;

            var row = new VisualElement();
            row.AddToClassList("gacha-rate-row");

            row.Add(MakeRatePill("일반", wNormal / total * 100f, "gacha-rate-pill-normal"));
            row.Add(MakeRatePill("레어", wRare   / total * 100f, "gacha-rate-pill-rare"));
            row.Add(MakeRatePill("에픽", wEpic   / total * 100f, "gacha-rate-pill-epic"));

            return row;
        }

        private static VisualElement MakeRatePill(string label, float pct, string colorClass)
        {
            var pill = new VisualElement();
            pill.AddToClassList("gacha-rate-pill");
            pill.AddToClassList(colorClass);

            var lbl = new Label($"{label}  {pct:F1}%");
            lbl.AddToClassList("gacha-rate-pill-label");
            lbl.pickingMode = PickingMode.Ignore;
            pill.Add(lbl);
            return pill;
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
                totalWeight += Mathf.Max(0f, table.rewards[i].weight);

            // Epic 먼저, 그다음 Rare, Normal, Skill, Currency 순서로 정렬 표시
            var sorted = new List<GachaRewardEntry>(table.rewards);
            sorted.Sort(CompareForPreview);

            for (int i = 0; i < sorted.Count; i++)
            {
                var entry = sorted[i];
                if (entry == null) continue;

                var card = new VisualElement();
                card.AddToClassList("gacha-reward-card");

                if (entry.rewardType == eGachaRewardType.Equipment && entry.equipmentData != null)
                    card.AddToClassList($"gacha-rarity-{entry.equipmentData.rarity.ToString().ToLower()}");

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

                string displayName = entry.nameKor;
                if (entry.rewardType == eGachaRewardType.Equipment && entry.equipmentData != null)
                    displayName = string.IsNullOrEmpty(entry.nameKor) ? entry.equipmentData.equipmentName : entry.nameKor;

                var nameLbl = new Label(displayName);
                nameLbl.AddToClassList("gacha-reward-name");
                card.Add(nameLbl);

                if (entry.rewardType == eGachaRewardType.Equipment && entry.equipmentData != null)
                {
                    var rarityLbl = new Label(GetRarityText(entry.equipmentData.rarity));
                    rarityLbl.AddToClassList("gacha-reward-rarity");
                    rarityLbl.AddToClassList($"gacha-rarity-text-{entry.equipmentData.rarity.ToString().ToLower()}");
                    card.Add(rarityLbl);
                }

                float pct = totalWeight > 0f ? (entry.weight / totalWeight) * 100f : 0f;
                var rateLbl = new Label($"{pct:F2}%");
                rateLbl.AddToClassList("gacha-reward-rate");
                card.Add(rateLbl);

                grid.Add(card);
            }

            _content.Add(grid);
        }

        private static int CompareForPreview(GachaRewardEntry a, GachaRewardEntry b)
        {
            int ra = GetSortRank(a);
            int rb = GetSortRank(b);
            return ra != rb ? ra - rb : 0;
        }

        private static int GetSortRank(GachaRewardEntry e)
        {
            if (e == null) return 999;
            if (e.rewardType == eGachaRewardType.Equipment && e.equipmentData != null)
            {
                switch (e.equipmentData.rarity)
                {
                    case eEquipmentRarity.Epic:   return 0;
                    case eEquipmentRarity.Rare:   return 1;
                    case eEquipmentRarity.Normal: return 2;
                }
            }
            if (e.rewardType == eGachaRewardType.Skill)    return 3;
            if (e.rewardType == eGachaRewardType.Currency) return 4;
            return 5;
        }

        // ── 뽑기 실행 ──────────────────────────────────────────────────

        private static void OnPullClicked(GachaTableSO table, int count)
        {
            PullAndShowResult(table, count);
        }

        /// <summary>
        /// 뽑기를 실행하고 결과를 팝업으로 표시한다.
        /// 서버 가챠는 비동기이므로 콜백으로 결과를 받는다.
        /// 외부(UITKUIManager 다시뽑기 버튼)에서도 호출 가능.
        /// </summary>
        public static void PullAndShowResult(GachaTableSO table, int count)
        {
            var uiMgr = UITKUIManager.Instance;
            var mgr = GachaManager.Instance;
            if (mgr == null) return;

            if (mgr.IsPulling)
            {
                if (uiMgr != null) uiMgr.ShowToast("이미 뽑기가 진행 중입니다.");
                return;
            }

            if (table == null || !table.isImplemented)
            {
                if (uiMgr != null) uiMgr.ShowToast("미구현 기능입니다.");
                return;
            }

            if (!mgr.CanPullMulti(table, count))
            {
                if (uiMgr != null) uiMgr.ShowToast("재화가 부족합니다.");
                return;
            }

            mgr.TryPull(table, count,
                onSuccess: results =>
                {
                    if (results == null || results.Count == 0)
                    {
                        if (uiMgr != null) uiMgr.ShowToast("뽑기 결과가 없습니다.");
                        RefreshContent();
                        return;
                    }
                    if (uiMgr != null)
                        uiMgr.ShowGachaResultPopup(results, table, count);
                    RefreshContent();
                },
                onError: message =>
                {
                    if (uiMgr != null)
                        uiMgr.ShowToast(string.IsNullOrEmpty(message) ? "뽑기에 실패했습니다." : message);
                    RefreshContent();
                });
        }

        // ── GachaManager 이벤트 핸들러 ────────────────────────────────

        private static void OnPullStateChanged(bool isPulling)
        {
            // 뽑기 시작: 버튼 즉시 비활성화(서버 응답 대기 중 중복 클릭 차단)
            // 뽑기 종료: 콘텐츠는 PullAndShowResult 콜백에서 RefreshContent 로 재계산.
            if (!isPulling) return;

            for (int i = 0; i < _activePullButtons.Count; i++)
            {
                var btn = _activePullButtons[i];
                if (btn == null) continue;
                btn.SetEnabled(false);
                btn.AddToClassList("gacha-pull-btn-disabled");
            }
        }

        // ── 헬퍼 ───────────────────────────────────────────────────────

        private static string GetCurrencyLabel(eCurrency c)
        {
            switch (c)
            {
                case eCurrency.Gold:            return "골드";
                case eCurrency.AncientCoin:     return "고대주화";
                case eCurrency.ArcaneKnowledge: return "비전지식";
                case eCurrency.ClassFragment:   return "직업의 파편";
                case eCurrency.KingdomSupply:   return "왕국 보급품";
                case eCurrency.TrainingTome:    return "훈련서";
                default:                        return c.ToString();
            }
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
