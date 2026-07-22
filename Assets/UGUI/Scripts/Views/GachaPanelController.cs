using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KingdomIdle.Gacha;
using KingdomIdle.MageTower;
using Scripts.Core;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// 뽑기 패널 컨트롤러 (UITKGachaPanelController 이식).
    /// 모바일 방치형 가챠 표준 레이아웃:
    ///   ┌─ 탭바 (장비/스킬)
    ///   ├─ 설명
    ///   ├─ 보유/비용 바
    ///   ├─ 확률 요약 (Epic/Rare/Normal 집계)
    ///   ├─ 뽑기 버튼 (x1 / x10)  ← 뽑는 중에는 비활성
    ///   └─ 보상 목록 미리보기 (카드 그리드)
    /// 뽑기 중에는 버튼이 자동 비활성화되며, 완료되면 결과 팝업 후 원상복구된다.
    /// </summary>
    public static class GachaPanelController
    {
        // 방치형 가챠 표준: x1 / x10
        private static readonly int[] PullCounts = { 1, 10 };

        private static readonly Color TabActive = new Color(80f / 255f, 60f / 255f, 180f / 255f, 0.60f);

        private static int _activeTabIndex;
        private static GachaPanelView _view;
        private static IReadOnlyList<GachaTableSO> _tables;
        private static readonly List<NavTabButtonView> _tabButtons = new();

        // 뽑기 버튼 참조 (in-flight 중 비활성화)
        private static readonly List<Button> _activePullButtons = new();

        // GachaManager 이벤트 구독 상태 (중복 구독 방지)
        private static bool _subscribedToManager;

        public static void Populate(GachaPanelView view)
        {
            if (view == null) return;

            _view = view;
            if (_view.tabBar == null || _view.content == null) return;

            view.OnClosed = () =>
            {
                if (_view == view)
                {
                    _view = null;
                    _tabButtons.Clear();
                    _activePullButtons.Clear();
                }
            };

            var mgr = GachaManager.Instance;
            if (mgr == null)
            {
                AddPlainLabel("GachaManager를 씬에 배치해주세요.");
                return;
            }

            SubscribeManagerEvents(mgr);

            _tables = mgr.GetAllTables();
            if (_tables == null || _tables.Count == 0)
            {
                AddPlainLabel("등록된 뽑기 테이블이 없습니다.");
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
            UguiRuntimeFactory.Clear(_view.tabBar);
            _tabButtons.Clear();

            var prefab = UIManager.Instance != null && UIManager.Instance.Catalog != null
                ? UIManager.Instance.Catalog.itemNavTabButton
                : null;
            if (prefab == null) return;

            var cat = UIManager.Instance != null ? UIManager.Instance.Catalog : null;

            for (int i = 0; i < _tables.Count; i++)
            {
                int idx = i;
                var go = Object.Instantiate(prefab, _view.tabBar, false);
                var tab = go.GetComponent<NavTabButtonView>();
                if (tab == null) continue;

                var table = _tables[i];
                tab.SetLabel(table != null ? table.nameKor : "?");

                // 비용 통화로 뽑기 종류를 구분해 아이콘 지정
                //   비전지식 = 마법탑 스킬 뽑기(지팡이), 그 외 = 장비 뽑기(보물상자)
                Sprite tabIcon = null;
                if (cat != null && table != null)
                {
                    tabIcon = table.costCurrency == eCurrency.ArcaneKnowledge
                        ? cat.iconWand
                        : cat.iconChest;
                }
                tab.SetIcon(tabIcon);

                tab.Button.onClick.AddListener(() => OnTabClicked(idx));
                _tabButtons.Add(tab);
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
            for (int i = 0; i < _tabButtons.Count; i++)
                _tabButtons[i].SetSelected(i == _activeTabIndex, TabActive);
        }

        // ── 콘텐츠 본문 ───────────────────────────────────────────────

        private static void RefreshContent()
        {
            if (_view == null || _view.content == null) return;

            var content = _view.content;
            UguiRuntimeFactory.Clear(content);
            _activePullButtons.Clear();

            if (_tables == null || _activeTabIndex >= _tables.Count) return;
            var table = _tables[_activeTabIndex];
            if (table == null) return;

            // 설명 (.gacha-desc: 26px @70%)
            if (!string.IsNullOrEmpty(table.description))
            {
                var desc = UguiRuntimeFactory.Label(content, table.description, 26f,
                    new Color(1f, 1f, 1f, 0.70f), TextAlignmentOptions.Left, wrap: true);
                UguiRuntimeFactory.Preferred(desc, height: 70f);
            }

            // 보유/비용 바 (.gacha-cost: 26px gold)
            EconomyBridge.TryGetAmount(table.costCurrency, out long current);
            var costLbl = UguiRuntimeFactory.Label(content,
                $"1회 비용: {table.costAmount:N0} {GetCurrencyLabel(table.costCurrency)}  |  보유: {current:N0}",
                26f, new Color(1f, 204f / 255f, 0f, 0.95f), TextAlignmentOptions.Left, bold: true);
            UguiRuntimeFactory.Preferred(costLbl, height: 40f);

            // 확률 요약 (등급별 가중치 집계)
            BuildRateSummaryRow(content, table);

            // 뽑기 버튼 행 — 크고 명확한 프리팹 버튼 (Item_GachaPullButton)
            var btnRow = UguiRuntimeFactory.Container(content, "PullRow");
            UguiRuntimeFactory.HorizontalLayout(btnRow.gameObject, 14f, null, TextAnchor.MiddleCenter, expandWidth: true);
            UguiRuntimeFactory.Preferred(btnRow.gameObject.AddComponent<LayoutElement>(), height: 140f);

            bool pulling = GachaManager.Instance != null && GachaManager.Instance.IsPulling;
            var cat = UIManager.Instance != null ? UIManager.Instance.Catalog : null;
            string curLabel = GetCurrencyLabel(table.costCurrency);

            for (int i = 0; i < PullCounts.Length; i++)
            {
                int count = PullCounts[i];
                long totalCost = (long)table.costAmount * count;
                bool disabled = !table.isImplemented || current < totalCost || pulling;

                var capturedTable = table;

                if (cat != null && cat.itemGachaPullButton != null)
                {
                    var go = Object.Instantiate(cat.itemGachaPullButton, btnRow, false);
                    var pull = go.GetComponent<GachaPullButtonView>();
                    if (pull != null)
                    {
                        string title = count == 1 ? "1회 뽑기" : $"{count}연 뽑기";
                        pull.Set(title, $"{totalCost:N0} {curLabel}", !disabled,
                            cat != null ? cat.iconChest : null);
                        pull.Button.onClick.AddListener(() => OnPullClicked(capturedTable, count));
                        _activePullButtons.Add(pull.Button);
                        continue;
                    }
                }

                // 폴백: 프리팹이 없을 때 코드 버튼
                var pullBtn = UguiRuntimeFactory.TextButton(btnRow,
                    $"{(count == 1 ? "1회" : count + "연")} 뽑기\n{totalCost:N0}", 26f,
                    disabled ? UguiTheme.DisabledGrey : UguiTheme.AccentBlue,
                    () => OnPullClicked(capturedTable, count), out var lbl);
                lbl.enableWordWrapping = true;
                UguiRuntimeFactory.Flexible((RectTransform)pullBtn.transform, 1f);
                pullBtn.interactable = !disabled;
                _activePullButtons.Add(pullBtn);
            }

            // 보상 목록 미리보기
            BuildRewardPreview(content, table);
        }

        private static void BuildRateSummaryRow(RectTransform content, GachaTableSO table)
        {
            if (table?.rewards == null || table.rewards.Count == 0) return;

            bool isSkillGacha = table.costCurrency == eCurrency.ArcaneKnowledge;
            bool isEquipGacha = table.costCurrency == eCurrency.AncientCoin;

            // 비용 통화 기준으로 유효한 보상만 집계
            float total = 0f;
            for (int i = 0; i < table.rewards.Count; i++)
            {
                var r = table.rewards[i];
                if (r == null) continue;
                if (!IsRewardValidForGacha(r, isSkillGacha, isEquipGacha)) continue;
                total += Mathf.Max(0f, r.weight);
            }
            if (total <= 0f) return;

            float wNormal = 0f, wRare = 0f, wEpic = 0f;
            float wClassFragment = 0f;
            float wArcaneKnowledge = 0f;
            float wSkill = 0f;
            bool hasAnyEquipment = false;
            bool hasAnySkill = false;
            for (int i = 0; i < table.rewards.Count; i++)
            {
                var r = table.rewards[i];
                if (r == null) continue;
                if (!IsRewardValidForGacha(r, isSkillGacha, isEquipGacha)) continue;

                if (r.rewardType == eGachaRewardType.Currency && r.currency == eCurrency.ClassFragment)
                {
                    wClassFragment += r.weight;
                    continue;
                }

                if (r.rewardType == eGachaRewardType.Currency && r.currency == eCurrency.ArcaneKnowledge)
                {
                    wArcaneKnowledge += r.weight;
                    continue;
                }

                if (r.rewardType == eGachaRewardType.Skill)
                {
                    wSkill += r.weight;
                    hasAnySkill = true;
                    continue;
                }

                if (r.rewardType != eGachaRewardType.Equipment || r.equipmentData == null) continue;
                hasAnyEquipment = true;
                switch (r.equipmentData.rarity)
                {
                    case eEquipmentRarity.Normal: wNormal += r.weight; break;
                    case eEquipmentRarity.Rare: wRare += r.weight; break;
                    case eEquipmentRarity.Epic: wEpic += r.weight; break;
                }
            }

            if (!hasAnyEquipment && !hasAnySkill && wClassFragment <= 0f && wArcaneKnowledge <= 0f) return;

            var row = UguiRuntimeFactory.Container(content, "RateRow");
            UguiRuntimeFactory.HorizontalLayout(row.gameObject, 8f, null, TextAnchor.MiddleLeft);
            UguiRuntimeFactory.Preferred(row.gameObject.AddComponent<LayoutElement>(), height: 46f);

            if (hasAnyEquipment)
            {
                MakeRatePill(row, "일반", wNormal / total * 100f, UguiTheme.RarityNormal);
                MakeRatePill(row, "레어", wRare / total * 100f, UguiTheme.RarityRare);
                MakeRatePill(row, "에픽", wEpic / total * 100f, UguiTheme.RarityEpic);
            }
            if (hasAnySkill)
            {
                MakeRatePill(row, "마탑 스킬", wSkill / total * 100f, UguiTheme.RaritySkill);
            }
            if (wArcaneKnowledge > 0f)
            {
                MakeRatePill(row, "비전지식", wArcaneKnowledge / total * 100f, UguiTheme.RarityArcane);
            }
            if (wClassFragment > 0f)
            {
                MakeRatePill(row, "전직 파편", wClassFragment / total * 100f, UguiTheme.RarityClassFragment);
            }
        }

        // 비용 통화 기준으로 해당 보상 항목이 유효한지 판정.
        // 스킬 가챠(ArcaneKnowledge): Skill + ArcaneKnowledge(비전지식) 만 허용
        //   — 서버 응답도 SkillCode 와 비전지식 누적 총량만 내려준다.
        // 장비 가챠(AncientCoin): Equipment + ClassFragment(전직 파편) 만 허용
        //   — 서버가 장비 ItemCode 에 10% 확률로 전직 파편을 섞어 내려준다.
        private static bool IsRewardValidForGacha(GachaRewardEntry r, bool isSkillGacha, bool isEquipGacha)
        {
            if (isSkillGacha)
                return r.rewardType == eGachaRewardType.Skill
                    || (r.rewardType == eGachaRewardType.Currency && r.currency == eCurrency.ArcaneKnowledge);
            if (isEquipGacha)
                return r.rewardType == eGachaRewardType.Equipment
                    || (r.rewardType == eGachaRewardType.Currency && r.currency == eCurrency.ClassFragment);
            return true;
        }

        /// <summary>확률 알약 — 프리팹(Item_RatePill) 우선, 없으면 코드 생성.</summary>
        private static void MakeRatePill(RectTransform parent, string label, float pct, Color color)
        {
            var cat = UIManager.Instance != null ? UIManager.Instance.Catalog : null;
            if (cat != null && cat.itemRatePill != null)
            {
                var go = Object.Instantiate(cat.itemRatePill, parent, false);
                var pill = go.GetComponent<RatePillView>();
                if (pill != null)
                {
                    pill.Set($"{label}  {pct:F1}%", color);
                    return;
                }
            }

            // 폴백
            var pillBox = UguiRuntimeFactory.Box(parent, "RatePill", new Color(color.r, color.g, color.b, 0.12f));
            UguiRuntimeFactory.HorizontalLayout(pillBox.gameObject, 0f, new RectOffset(14, 14, 6, 6), TextAnchor.MiddleCenter);
            var frame = UguiRuntimeFactory.Box(pillBox.transform, "Frame", color);
            frame.fillCenter = false;
            UguiRuntimeFactory.Stretch(frame.rectTransform);
            frame.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var lbl = UguiRuntimeFactory.Label(pillBox.transform, $"{label}  {pct:F1}%", 20f, color,
                TextAlignmentOptions.Center, bold: true);
            UguiRuntimeFactory.Preferred(lbl, height: 28f);
        }

        private static void BuildRewardPreview(RectTransform content, GachaTableSO table)
        {
            if (table.rewards == null || table.rewards.Count == 0) return;

            bool isSkillGacha = table.nameEng == "MageTowerSkill";
            bool isEquipGacha = table.costCurrency == eCurrency.AncientCoin;

            var sectionTitle = UguiRuntimeFactory.Label(content, "획득 가능 보상", 26f,
                new Color(1f, 1f, 1f, 0.80f), TextAlignmentOptions.Left, bold: true);
            UguiRuntimeFactory.Preferred(sectionTitle, height: 38f);

            var grid = UguiRuntimeFactory.Container(content, "RewardGrid");
            var gridLayout = UguiRuntimeFactory.GridLayout(grid.gameObject,
                new Vector2(160f, 200f), new Vector2(10f, 10f));
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = 6;

            float totalWeight = 0f;
            for (int i = 0; i < table.rewards.Count; i++)
            {
                var r = table.rewards[i];
                if (r == null) continue;
                if (!IsRewardValidForGacha(r, isSkillGacha, isEquipGacha)) continue;
                totalWeight += Mathf.Max(0f, r.weight);
            }

            var sorted = new List<GachaRewardEntry>(table.rewards);
            sorted.Sort(CompareForPreview);

            var cardPrefab = UIManager.Instance != null && UIManager.Instance.Catalog != null
                ? UIManager.Instance.Catalog.itemGachaCard
                : null;
            if (cardPrefab == null) return;

            for (int i = 0; i < sorted.Count; i++)
            {
                var entry = sorted[i];
                if (entry == null) continue;
                if (!IsRewardValidForGacha(entry, isSkillGacha, isEquipGacha)) continue;

                var cardGo = Object.Instantiate(cardPrefab, grid, false);
                var card = cardGo.GetComponent<GachaCardItemView>();
                if (card == null) continue;

                // 등급 테두리
                Color frameColor = new Color(1f, 1f, 1f, 0.20f);
                if (entry.rewardType == eGachaRewardType.Equipment && entry.equipmentData != null)
                    frameColor = UguiTheme.RarityColor(entry.equipmentData.rarity);
                else if (entry.rewardType == eGachaRewardType.Currency && entry.currency == eCurrency.ClassFragment)
                    frameColor = UguiTheme.RarityClassFragment;
                else if (entry.rewardType == eGachaRewardType.Currency && entry.currency == eCurrency.ArcaneKnowledge)
                    frameColor = UguiTheme.RarityArcane;
                else if (entry.rewardType == eGachaRewardType.Skill)
                    frameColor = UguiTheme.RaritySkill;
                card.SetRarityFrame(frameColor);

                // 아이콘
                Sprite displayIcon = entry.icon;
                if (entry.rewardType == eGachaRewardType.Equipment && entry.equipmentData != null && entry.equipmentData.icon != null)
                    displayIcon = entry.equipmentData.icon;
                else if (entry.rewardType == eGachaRewardType.Skill && displayIcon == null)
                {
                    var mtMgr = MageTowerManager.Instance;
                    var so = mtMgr != null ? mtMgr.GetSkillById(entry.skillId) : null;
                    if (so != null && so.icon != null) displayIcon = so.icon;
                }
                card.SetIcon(displayIcon, null, Color.white);

                // 이름
                string displayName = entry.nameKor;
                if (entry.rewardType == eGachaRewardType.Equipment && entry.equipmentData != null)
                    displayName = string.IsNullOrEmpty(entry.nameKor) ? entry.equipmentData.equipmentName : entry.nameKor;
                else if (entry.rewardType == eGachaRewardType.Skill && string.IsNullOrEmpty(displayName))
                {
                    var mtMgr = MageTowerManager.Instance;
                    var so = mtMgr != null ? mtMgr.GetSkillById(entry.skillId) : null;
                    if (so != null) displayName = !string.IsNullOrEmpty(so.nameKor) ? so.nameKor : so.nameEng;
                }
                if (card.nameLabel != null) card.nameLabel.text = displayName;

                // 하단: 등급/태그 + 확률 — subLabel에 두 줄로 표기
                string tag = null;
                Color tagColor = new Color(100f / 255f, 180f / 255f, 1f, 0.80f);
                if (entry.rewardType == eGachaRewardType.Equipment && entry.equipmentData != null)
                {
                    tag = GetRarityText(entry.equipmentData.rarity);
                    tagColor = UguiTheme.RarityColor(entry.equipmentData.rarity);
                }
                else if (entry.rewardType == eGachaRewardType.Currency && entry.currency == eCurrency.ClassFragment)
                {
                    tag = "전직 파편";
                    tagColor = UguiTheme.RarityClassFragment;
                }
                else if (entry.rewardType == eGachaRewardType.Currency && entry.currency == eCurrency.ArcaneKnowledge)
                {
                    tag = "비전지식";
                    tagColor = UguiTheme.RarityArcane;
                }

                float pct = totalWeight > 0f ? (entry.weight / totalWeight) * 100f : 0f;
                if (card.subLabel != null)
                {
                    card.subLabel.text = string.IsNullOrEmpty(tag) ? $"{pct:F2}%" : $"{tag}\n{pct:F2}%";
                    card.subLabel.color = tagColor;
                    card.subLabel.enableWordWrapping = true;
                }
            }
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
                    case eEquipmentRarity.Epic: return 0;
                    case eEquipmentRarity.Rare: return 1;
                    case eEquipmentRarity.Normal: return 2;
                }
            }
            if (e.rewardType == eGachaRewardType.Skill) return 3;
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
        /// 외부(가챠 결과 팝업의 다시뽑기 버튼)에서도 호출 가능.
        /// </summary>
        public static void PullAndShowResult(GachaTableSO table, int count)
        {
            var uiMgr = UIManager.Instance;
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
                btn.interactable = false;
            }
        }

        // ── 헬퍼 ───────────────────────────────────────────────────────

        private static void AddPlainLabel(string text)
        {
            var lbl = UguiRuntimeFactory.Label(_view.content, text, 24f, UguiTheme.TextSecondary,
                TextAlignmentOptions.Center, wrap: true);
            UguiRuntimeFactory.Preferred(lbl, height: 60f);
        }

        private static string GetCurrencyLabel(eCurrency c)
        {
            switch (c)
            {
                case eCurrency.Gold: return "골드";
                case eCurrency.AncientCoin: return "고대주화";
                case eCurrency.ArcaneKnowledge: return "비전지식";
                case eCurrency.ClassFragment: return "직업의 파편";
                case eCurrency.KingdomSupply: return "왕국 보급품";
                case eCurrency.TrainingTome: return "훈련서";
                default: return c.ToString();
            }
        }

        private static string GetRarityText(eEquipmentRarity rarity)
        {
            switch (rarity)
            {
                case eEquipmentRarity.Normal: return "일반";
                case eEquipmentRarity.Rare: return "레어";
                case eEquipmentRarity.Epic: return "에픽";
                default: return rarity.ToString();
            }
        }
    }
}
