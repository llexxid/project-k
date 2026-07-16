using System.Collections.Generic;
using UnityEngine;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// 뽑기 결과 팝업 (UITKUIManager.ShowGachaResultPopup 영역 이식).
    /// 아이콘+개수 카드 그리드와 완료 / 다시뽑기x1 / 다시뽑기xN 버튼을 표시한다.
    /// </summary>
    public static class GachaResultPopupController
    {
        private static GameObject _instanceGo;
        private static UIManager _host;

        public static bool IsOpen => _instanceGo != null;

        public static void Show(
            UIManager host,
            List<KingdomIdle.Gacha.GachaRewardEntry> results,
            KingdomIdle.Gacha.GachaTableSO table,
            int lastPullCount)
        {
            Close();
            _host = host;

            if (results == null || results.Count == 0)
            {
                host.ShowToast("뽑기 결과가 없습니다.");
                return;
            }

            if (host.Catalog == null || host.Catalog.popupGachaResult == null)
            {
                Debug.LogError("[GachaResultPopup] popupGachaResult 프리팹이 카탈로그에 없습니다.");
                return;
            }

            _instanceGo = Object.Instantiate(host.Catalog.popupGachaResult, host.LayerOverlays, false);
            var view = _instanceGo.GetComponent<GachaResultPopupView>();
            if (view == null)
            {
                Debug.LogError("[GachaResultPopup] GachaResultPopupView 컴포넌트가 없습니다.");
                Object.Destroy(_instanceGo);
                _instanceGo = null;
                return;
            }

            // 최고 등급 계산 (제목 강조용)
            var bestRarity = ComputeBestRarity(results);
            if (view.title != null)
            {
                view.title.text = BuildTitleText(bestRarity);
                view.title.color = TitleColor(bestRarity);
            }

            // 아이템별 합산 후 등급순 정렬 (Epic → Rare → Normal → Skill → Currency)
            var merged = MergeResults(results);
            merged.Sort(CompareResultEntries);

            foreach (var m in merged)
                AddResultCard(host, view, m.entry, m.count, bestRarity);

            BindButtons(view, table, lastPullCount);

            _instanceGo.transform.SetAsLastSibling();
        }

        public static void Close()
        {
            if (_instanceGo != null)
            {
                Object.Destroy(_instanceGo);
                _instanceGo = null;
            }
        }

        private static void AddResultCard(
            UIManager host,
            GachaResultPopupView view,
            KingdomIdle.Gacha.GachaRewardEntry entry,
            int count,
            eEquipmentRarity? bestRarity)
        {
            if (host.Catalog.itemGachaCard == null || view.grid == null) return;

            var cardGo = Object.Instantiate(host.Catalog.itemGachaCard, view.grid, false);
            var card = cardGo.GetComponent<GachaCardItemView>();
            if (card == null) return;

            // 등급 테두리
            Color frameColor = new Color(1f, 1f, 1f, 0.25f);
            Color fallbackColor = UguiTheme.TextPrimary;
            string fallbackText = null;

            if (entry.rewardType == KingdomIdle.Gacha.eGachaRewardType.Equipment && entry.equipmentData != null)
            {
                frameColor = UguiTheme.RarityColor(entry.equipmentData.rarity);

                // 최고 등급(에픽) 카드 강조
                if (bestRarity.HasValue && entry.equipmentData.rarity == bestRarity.Value
                    && bestRarity.Value == eEquipmentRarity.Epic && card.background != null)
                {
                    card.background.color = new Color(
                        UguiTheme.RarityEpic.r, UguiTheme.RarityEpic.g, UguiTheme.RarityEpic.b, 0.25f);
                }
            }
            else if (entry.rewardType == KingdomIdle.Gacha.eGachaRewardType.Currency
                     && entry.currency == eCurrency.ClassFragment)
            {
                frameColor = UguiTheme.RarityClassFragment;
                fallbackText = "전직 파편";
                fallbackColor = UguiTheme.RarityClassFragment;
            }
            else if (entry.rewardType == KingdomIdle.Gacha.eGachaRewardType.Currency
                     && entry.currency == eCurrency.ArcaneKnowledge)
            {
                frameColor = UguiTheme.RarityArcane;
                fallbackText = "비전지식";
                fallbackColor = UguiTheme.RarityArcane;
            }
            else if (entry.rewardType == KingdomIdle.Gacha.eGachaRewardType.Skill)
            {
                frameColor = UguiTheme.RaritySkill;
            }

            card.SetRarityFrame(frameColor);

            // 아이콘
            Sprite icon = entry.icon;
            if (entry.rewardType == KingdomIdle.Gacha.eGachaRewardType.Equipment
                && entry.equipmentData != null && entry.equipmentData.icon != null)
            {
                icon = entry.equipmentData.icon;
            }
            else if (entry.rewardType == KingdomIdle.Gacha.eGachaRewardType.Skill && icon == null)
            {
                var mtMgr = KingdomIdle.MageTower.MageTowerManager.Instance;
                var so = mtMgr != null ? mtMgr.GetSkillById(entry.skillId) : null;
                if (so != null && so.icon != null) icon = so.icon;
            }

            card.SetIcon(icon, fallbackText, fallbackColor);

            // 이름
            string displayName;
            if (entry.rewardType == KingdomIdle.Gacha.eGachaRewardType.Equipment && entry.equipmentData != null)
            {
                displayName = entry.equipmentData.equipmentName;
            }
            else if (entry.rewardType == KingdomIdle.Gacha.eGachaRewardType.Skill && string.IsNullOrEmpty(entry.nameKor))
            {
                var mtMgr = KingdomIdle.MageTower.MageTowerManager.Instance;
                var so = mtMgr != null ? mtMgr.GetSkillById(entry.skillId) : null;
                displayName = so != null && !string.IsNullOrEmpty(so.nameKor) ? so.nameKor : (so != null ? so.nameEng : "?");
            }
            else if (entry.rewardType == KingdomIdle.Gacha.eGachaRewardType.Currency)
            {
                displayName = !string.IsNullOrEmpty(entry.nameKor)
                    ? entry.nameKor
                    : MainScreenController.GetCurrencyLabelKor(entry.currency);
            }
            else
            {
                displayName = string.IsNullOrEmpty(entry.nameKor) ? "?" : entry.nameKor;
            }

            if (card.nameLabel != null) card.nameLabel.text = displayName;

            // 수량 — "×1,500" 형식 (천단위 콤마 + 정식 곱셈 기호)
            if (card.subLabel != null)
            {
                card.subLabel.text = FormatGachaCount(count);
                card.subLabel.color = UguiTheme.AccentGoldStrong;
            }
        }

        private static void BindButtons(GachaResultPopupView view, KingdomIdle.Gacha.GachaTableSO table, int lastPullCount)
        {
            if (view.btnDone != null)
                view.btnDone.onClick.AddListener(Close);

            if (view.btnRePull1 != null)
            {
                if (view.btnRePull1Label != null) view.btnRePull1Label.text = "다시 뽑기 x1";
                view.btnRePull1.onClick.AddListener(() => HandleRePull(table, 1));
            }

            if (view.btnRePullN != null)
            {
                bool showN = lastPullCount > 1;
                view.btnRePullN.gameObject.SetActive(showN);
                if (showN)
                {
                    if (view.btnRePullNLabel != null) view.btnRePullNLabel.text = $"다시 뽑기 x{lastPullCount}";
                    view.btnRePullN.onClick.AddListener(() => HandleRePull(table, lastPullCount));
                }
            }

            // 진행 중이면 다시뽑기 버튼 비활성
            var mgr = KingdomIdle.Gacha.GachaManager.Instance;
            if (mgr != null && mgr.IsPulling)
            {
                if (view.btnRePull1 != null) view.btnRePull1.interactable = false;
                if (view.btnRePullN != null) view.btnRePullN.interactable = false;
            }
        }

        private static void HandleRePull(KingdomIdle.Gacha.GachaTableSO table, int count)
        {
            var mgr = KingdomIdle.Gacha.GachaManager.Instance;
            if (mgr != null && mgr.IsPulling)
            {
                if (_host != null) _host.ShowToast("이미 뽑기가 진행 중입니다.");
                return;
            }
            if (table == null) return;

            Close();
            GachaPanelController.PullAndShowResult(table, count);
        }

        // ═══════════════════════════════════════════
        //  결과 합산/정렬 유틸 (원본 로직 그대로)
        // ═══════════════════════════════════════════

        private static List<(KingdomIdle.Gacha.GachaRewardEntry entry, int count)> MergeResults(
            List<KingdomIdle.Gacha.GachaRewardEntry> results)
        {
            var merged = new List<(KingdomIdle.Gacha.GachaRewardEntry entry, int count)>();
            if (results == null) return merged;

            // 서버가 동일 보상(장비/스킬/파편)을 하나의 엔트리에 Count 로 묶어 내려주므로
            // 모든 타입에 대해 entry.amount 를 누적한다. (amount 가 0 이하이면 1 로 보정)
            foreach (var r in results)
            {
                if (r == null) continue;
                string key = MakeMergeKey(r);
                int amt = Mathf.Max(1, r.amount);

                bool found = false;
                for (int i = 0; i < merged.Count; i++)
                {
                    if (MakeMergeKey(merged[i].entry) == key)
                    {
                        merged[i] = (merged[i].entry, merged[i].count + amt);
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    merged.Add((r, amt));
                }
            }
            return merged;
        }

        /// <summary>가챠 결과 카드의 수량 라벨 포맷: "×1" / "×1,500".</summary>
        private static string FormatGachaCount(int count)
        {
            int safe = Mathf.Max(1, count);
            return $"×{safe:N0}";
        }

        private static string MakeMergeKey(KingdomIdle.Gacha.GachaRewardEntry r)
        {
            if (r.rewardType == KingdomIdle.Gacha.eGachaRewardType.Equipment && r.equipmentData != null)
                return $"equip_{r.equipmentData.GetInstanceID()}";
            if (r.rewardType == KingdomIdle.Gacha.eGachaRewardType.Skill)
                return $"skill_{r.skillId}";
            if (r.rewardType == KingdomIdle.Gacha.eGachaRewardType.Currency)
            {
                // 전직 파편은 직업별로 분리 표시해야 하므로 nameKor 까지 키에 포함.
                return string.IsNullOrEmpty(r.nameKor)
                    ? $"currency_{r.currency}"
                    : $"currency_{r.currency}_{r.nameKor}";
            }
            return $"other_{r.nameKor}";
        }

        private static int CompareResultEntries(
            (KingdomIdle.Gacha.GachaRewardEntry entry, int count) a,
            (KingdomIdle.Gacha.GachaRewardEntry entry, int count) b)
        {
            int ra = GetResultSortRank(a.entry);
            int rb = GetResultSortRank(b.entry);
            return ra != rb ? ra - rb : 0;
        }

        private static int GetResultSortRank(KingdomIdle.Gacha.GachaRewardEntry e)
        {
            if (e == null) return 999;
            if (e.rewardType == KingdomIdle.Gacha.eGachaRewardType.Equipment && e.equipmentData != null)
            {
                switch (e.equipmentData.rarity)
                {
                    case eEquipmentRarity.Epic: return 0;
                    case eEquipmentRarity.Rare: return 1;
                    case eEquipmentRarity.Normal: return 2;
                }
            }
            if (e.rewardType == KingdomIdle.Gacha.eGachaRewardType.Skill) return 3;
            if (e.rewardType == KingdomIdle.Gacha.eGachaRewardType.Currency) return 4;
            return 5;
        }

        private static eEquipmentRarity? ComputeBestRarity(List<KingdomIdle.Gacha.GachaRewardEntry> results)
        {
            eEquipmentRarity? best = null;
            foreach (var r in results)
            {
                if (r == null) continue;
                if (r.rewardType != KingdomIdle.Gacha.eGachaRewardType.Equipment) continue;
                if (r.equipmentData == null) continue;

                var rar = r.equipmentData.rarity;
                if (!best.HasValue || (int)rar > (int)best.Value)
                    best = rar;
            }
            return best;
        }

        private static string BuildTitleText(eEquipmentRarity? best)
        {
            if (!best.HasValue) return "뽑기 결과";
            switch (best.Value)
            {
                case eEquipmentRarity.Epic: return "뽑기 결과 — 에픽 획득!";
                case eEquipmentRarity.Rare: return "뽑기 결과 — 레어 획득!";
                default: return "뽑기 결과";
            }
        }

        private static Color TitleColor(eEquipmentRarity? best)
        {
            if (!best.HasValue) return UguiTheme.AccentGoldStrong;
            switch (best.Value)
            {
                case eEquipmentRarity.Epic: return UguiTheme.RarityEpic;
                case eEquipmentRarity.Rare: return UguiTheme.RarityRare;
                default: return UguiTheme.RarityNormal;
            }
        }
    }
}
