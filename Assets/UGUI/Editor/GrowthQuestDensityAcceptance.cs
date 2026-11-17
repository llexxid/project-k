using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using KingdomIdle.Balance;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>계정·통신 없이 실제 성장/퀘스트 프리팹의 정보·입력 영역을 검사한다.</summary>
    public static class GrowthQuestDensityAcceptance
    {
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit PlayMode first.");
            DevelopmentPanelPrefabGens.ApplyBronzeGrowth();
            QuestCardPrefabBuilder.Apply();
            AssetDatabase.SaveAssets();
        }

        public static Dictionary<string, object> Validate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Density validation requires EditMode.");
            var checks = new List<string>();
            var failures = new List<string>();
            void Check(bool ok, string text)
            { if (ok) checks.Add(text); else failures.Add(text); }
            var catalog = AssetDatabase.LoadAssetAtPath<UIViewCatalog>("Assets/UGUI/UIViewCatalog.asset");
            if (catalog == null || catalog.bodyDevelopment == null || catalog.itemEnhanceCard == null ||
                catalog.itemGachaPullButton == null || catalog.itemGuideStepRow == null || catalog.itemNavTabButton == null || catalog.panelGuide == null)
                throw new InvalidOperationException("Growth/quest density: required real catalog reference is missing.");
            Check(true, "real catalog references remain connected");
            var notation = typeof(NumberNotation).GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(field => !field.IsInitOnly && !field.IsLiteral).ToDictionary(field => field, field => field.GetValue(null));
            long generation = LocalProgression.AccountGeneration;
            bool ready = LocalProgression.IsReady;
            var root = new GameObject("GrowthQuestDensityGeometry", typeof(RectTransform), typeof(Canvas));
            root.hideFlags = HideFlags.HideAndDontSave;
            root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            ((RectTransform)root.transform).sizeDelta = new Vector2(1080, 1920);
            try
            {
                Check(root.activeInHierarchy, "geometry fixture is active for UGUI layout");
                foreach (float width in new[] { 880f, 1000f })
                {
                    CheckGrowth(catalog, root.transform, width, Check);
                    CheckQuest(catalog, root.transform, width, Check);
                }
                var guide = catalog.panelGuide.GetComponent<GuidePanelView>();
                Check(guide.claimBar != null && guide.claimAllButton != null && guide.claimAllLabel != null && guide.claimSummaryLabel != null &&
                    guide.claimAllButton.transform.IsChildOf(guide.claimBar) && guide.tabBar != null,
                    "latest separate current-tab claim-all and tab references are preserved");
                Check(((RectTransform)guide.claimAllButton.transform).rect.width >= 328 &&
                    ((RectTransform)guide.claimAllButton.transform).rect.height >= 128,
                    "current-tab claim-all keeps its existing touch rectangle");
            }
            finally
            {
                root.SetActive(false); Object.DestroyImmediate(root);
                foreach (var value in notation) value.Key.SetValue(null, value.Value);
            }
            Check(LocalProgression.IsReady == ready && LocalProgression.AccountGeneration == generation,
                "geometry inspection never opens or changes an account");
            if (failures.Count > 0)
                throw new InvalidOperationException($"Growth/quest density: {failures.Count} failures, {checks.Count} passed:\n- " + string.Join("\n- ", failures));
            return new Dictionary<string, object> { ["passed"] = checks.Count, ["checks"] = checks,
                ["scope"] = "Active real-prefab geometry and presentation; no claim/payment/network/profile fixture" };
        }

        private static void CheckGrowth(UIViewCatalog catalog, Transform parent, float width, Action<bool, string> check)
        {
            var body = Object.Instantiate(catalog.bodyDevelopment, parent, false).GetComponent<DevelopmentBodyView>();
            try
            {
                var rect = (RectTransform)body.transform;
                Size(rect, width, 1000);
                body.emptyLabel.gameObject.SetActive(false);
                body.rubyCardsRoot.gameObject.SetActive(false);
                body.goldLabel.text = "보유 골드  9,223,372,036,854,775,807 G";
                body.descLabel.text = "모든 왕국군에 적용 · 환생 시 골드 강화 초기화";
                AddTab(catalog, body.navBar, "골드 강화", catalog.iconCoin, DevelopmentPanelController.GrowthTabHeight);
                AddTab(catalog, body.navBar, "루비 영구 성장", catalog.iconGem, DevelopmentPanelController.GrowthTabHeight);
                foreach (string text in new[] { "1회", "10회", "최대" })
                    AddTab(catalog, body.purchaseCountBar, text, null, DevelopmentPanelController.PurchaseCountHeight);
                var card = Object.Instantiate(catalog.itemEnhanceCard, body.cardsRoot, false).GetComponent<EnhanceCardView>();
                var second = Object.Instantiate(catalog.itemEnhanceCard, body.cardsRoot, false).GetComponent<EnhanceCardView>();
                var buy = AddBuy(catalog, card); AddBuy(catalog, second);
                second.Set("체력", "Lv. 5,000", "×123.456  →  ×124.567");
                foreach (var sample in new[]
                {
                    ("공격력", "Lv. 5,000", "×123.456  →  ×124.567", "최대 5000회", "9.22Qi 골드"),
                    ("경험치 획득", "Lv. 50/50", "환생 후에도 유지되는 영구 성장", "최대 단계", "강화 완료"),
                    ("골드 획득", "Lv. 0/50", "메인 2-5 클리어 후 해금", "1회 강화", "20 루비"),
                    ("경험치 획득", "Lv. 2/50", "이전 요청의 결과 확인이 필요합니다.", "요청 재시도", "기존 요청 ID")
                })
                {
                    card.Set(sample.Item1, sample.Item2, sample.Item3); buy.Set(sample.Item4, sample.Item5, true);
                    Layout(rect);
                    rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, LayoutUtility.GetPreferredHeight(rect)); Layout(rect);
                    string name = width + " growth " + sample.Item1;
                    check(Mathf.Approximately(((RectTransform)card.transform).rect.height, 216), name + " reserves a 216-height card");
                    check(buy.Button.transform is RectTransform button && button.rect.height >= 168 && button.rect.width >= 320,
                        name + " preserves the primary action touch area");
                    foreach (var label in new[] { card.nameLabel, card.levelLabel, card.bonusLabel, buy.titleLabel, buy.costLabel })
                    {
                        check(Inside((RectTransform)card.transform, label.rectTransform), name + " contains " + label.name);
                        check(Fits(label), name + " renders " + label.name + " without truncation; " + TextDetails(label));
                    }
                    check(!Overlaps((RectTransform)card.transform, card.nameLabel.rectTransform, card.levelLabel.rectTransform) &&
                        !Overlaps((RectTransform)card.transform, card.bonusLabel.rectTransform, card.nameLabel.rectTransform),
                        name + " separates its two information rows");
                }
                check(Fits(body.goldLabel) && Inside(rect, body.goldLabel.rectTransform), width + " growth wallet retains every displayed gold digit; " + TextDetails(body.goldLabel));
                check(Fits(body.descLabel) && body.descLabel.preferredHeight <= body.descLabel.rectTransform.rect.height + 1,
                    width + " growth guidance uses its actual line height");
                check(LayoutUtility.GetPreferredHeight(rect) < 1000, width + " two growth cards use less than the former 1072-height body");
                check(card.nameLabel.fontSize == 44 && card.levelLabel.fontSize == 36 && card.bonusLabel.fontSize == 40 &&
                    body.goldLabel.fontSize == 42 && body.descLabel.fontSize == 36, width + " growth typography sizes remain unchanged");
                check(DevelopmentPanelController.GrowthTabHeight == 128 && DevelopmentPanelController.PurchaseCountHeight == 96 &&
                    body.navBar.rect.height >= 128 && body.purchaseCountBar.rect.height >= 96,
                    width + " growth type and purchase-count tabs preserve their existing touch heights");
            }
            finally { body.gameObject.SetActive(false); Object.DestroyImmediate(body.gameObject); }
        }

        private static void CheckQuest(UIViewCatalog catalog, Transform parent, float width, Action<bool, string> check)
        {
            var styleField = typeof(NumberNotation).GetField("<Style>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("NumberNotation style fixture field is missing.");
            var originalStyle = NumberNotation.Style;
            var view = Object.Instantiate(catalog.itemGuideStepRow, parent, false).GetComponent<GuideStepRowView>();
            try
            {
                var rect = (RectTransform)view.transform;
                Size(rect, width, 240); Layout(rect);
                int descendants = view.GetComponentsInChildren<Transform>(true).Length;
                var fixedReward = new[] { new QuestRewardSnapshot(eCurrency.AncientCoin, long.MaxValue, false) };
                var dynamicReward = new[] { new QuestRewardSnapshot(eCurrency.Gold, 0, true) };
                var twoRewards = new[] { new QuestRewardSnapshot(eCurrency.AncientCoin, 80, false), new QuestRewardSnapshot(eCurrency.ArcaneKnowledge, 20, false) };
                var combined = new[] { new QuestRewardSnapshot(eCurrency.Gold, 0, true), new QuestRewardSnapshot(eCurrency.AncientCoin, long.MaxValue, false) };
                var cases = new[] { ("fixed", false, fixedReward), ("dynamic", false, dynamicReward),
                    ("two rewards", false, twoRewards), ("all information", true, combined) };
                foreach (var sample in cases)
                {
                    var snapshot = new QuestRowSnapshot(new QuestClaimToken(0, 20001, "2026-11-12"), eQuestCategory.Daily,
                        "검사", "왕국군 전체가 함께 몬스터 200마리 처치", eQuestObjectiveType.MonsterKill, 0, 9999, 9999,
                        QuestRowState.Claimable, sample.Item2, 0, sample.Item3);
                    view.SetQuest(snapshot, catalog);
                    rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, view.GetComponent<LayoutElement>().preferredHeight); Layout(rect);
                    string name = width + " quest " + sample.Item1;
                    check(rect.rect.height == (sample.Item2 ? 264 : 240), name + " keeps a compact height with room for prior-period information");
                    check(view.titleLabel.fontSize == 42 && view.actionLabel.fontSize == 36 && view.rewardAmountLabel.fontSize == 36,
                        name + " keeps title, action and reward typography");
                    check(Fits(view.titleLabel) && view.titleLabel.textInfo.lineCount == 2, name + " retains its Korean two-line title; " + TextDetails(view.titleLabel));
                    var action = (RectTransform)view.actionButton.transform;
                    check(action.rect.width >= 232 && action.rect.height >= 144 && Inside(rect, action), name + " preserves a separate 144-height action");
                    check(!Overlaps(rect, view.titleLabel.rectTransform, action), name + " separates title and action");
                    var rewards = new[] { (view.rewardIcon, view.rewardAmountLabel), (view.secondaryRewardIcon, view.secondaryRewardAmountLabel) };
                    for (int i = 0; i < sample.Item3.Length; i++)
                    {
                        var (icon, amount) = rewards[i];
                        bool fits = Fits(amount), iconInside = Inside(rect, icon.rectTransform), amountInside = Inside(rect, amount.rectTransform);
                        check(iconInside && amountInside && fits, name + " contains readable reward " + i +
                            $"; iconRect={icon.rectTransform.rect.size} iconInside={iconInside} amountInside={amountInside}; " + TextDetails(amount));
                        if (!sample.Item3[i].IsDynamic)
                            check(AtomicQuantityFits(amount), name + " keeps fixed quantity " + i + " as a complete single-line token; " + TextDetails(amount));
                        check(!Overlaps(rect, icon.rectTransform, amount.rectTransform) &&
                            !Overlaps(rect, amount.rectTransform, view.titleLabel.rectTransform) && !Overlaps(rect, amount.rectTransform, action),
                            name + " separates reward " + i + " from icons, title and action");
                    }
                    if (sample.Item3.Length == 2)
                        check(!Overlaps(rect, (RectTransform)view.rewardIcon.transform.parent, (RectTransform)view.secondaryRewardIcon.transform.parent),
                            name + " places both reward slots horizontally without overlap");
                    if (sample.Item3[0].IsDynamic)
                        check((view.rewardAmountLabel.text == "2분\n골드" || view.rewardAmountLabel.text == "2분 골드") &&
                            view.rewardAmountLabel.textInfo.lineCount == 2,
                            name + " retains both the duration and gold unit");
                    check(Fits(view.progressLabel) && Inside(rect, view.progressLabel.rectTransform), name + " keeps progress readable; " + TextDetails(view.progressLabel));
                    if (sample.Item2)
                        check(Inside(rect, view.periodLabel.rectTransform) && Fits(view.periodLabel) && view.periodLabel.textInfo.lineCount == 2 &&
                            !Overlaps(rect, view.periodLabel.rectTransform, action), name + " retains its complete two-line prior period above the action; " + TextDetails(view.periodLabel));
                    var padding = view.actionButton.targetGraphic.raycastPadding;
                    var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(rect, action);
                    check(bounds.min.y + padding.w >= rect.rect.yMin - 10,
                        name + " expanded action input stops before the following row");
                    check(view.actionButton.interactable && (view.GetComponent<Button>() == null || !view.GetComponent<Button>().enabled),
                        name + " keeps only the individual reward button active");
                    view.SetQuest(snapshot, catalog, claimBusy: true);
                    check(!view.actionButton.interactable && view.actionLabel.text == "수령 중…", name + " preserves the busy input gate");
                }
                var longProgress = new QuestRowSnapshot(new QuestClaimToken(0, 40001, "2026-11-12"), eQuestCategory.Achievement,
                    "검사", "몬스터 250000마리 처치", eQuestObjectiveType.MonsterKill, 0, 250000, 249999,
                    QuestRowState.InProgress, false, 0, twoRewards);
                view.SetQuest(longProgress, catalog); Layout(rect);
                check(Inside(rect, view.progressLabel.rectTransform) && Fits(view.progressLabel) && view.progressLabel.textInfo.lineCount <= 2,
                    width + " quest preserves long abbreviated progress on up to two lines; " + TextDetails(view.progressLabel));
                foreach (var style in new[] { NumberStyle.Standard, NumberStyle.Korean, NumberStyle.Scientific })
                {
                    // 실제 UI 포맷 경로만 검사한다. 설정 저장·Changed 구독자 호출은 하지 않고 finally에서 원복한다.
                    styleField.SetValue(null, style);
                    foreach (var sample in new[] { (300m, 30m, false), (999999999999999999m, 999999m, false),
                        ((decimal)long.MaxValue, (decimal)long.MaxValue, true) })
                    {
                        var amounts = new[] { sample.Item1, sample.Item2 };
                        var snapshot = new QuestRowSnapshot(new QuestClaimToken(0, 40001, "2026-11-12"), eQuestCategory.Achievement,
                            "검사", "왕국군 전체가 함께 몬스터 250000마리 처치", eQuestObjectiveType.MonsterKill, 0, 250000, 249999,
                            QuestRowState.InProgress, sample.Item3, 0, new[] { new QuestRewardSnapshot(eCurrency.AncientCoin, amounts[0]),
                                new QuestRewardSnapshot(eCurrency.ArcaneKnowledge, amounts[1]) });
                        view.SetQuest(snapshot, catalog);
                        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, view.GetComponent<LayoutElement>().preferredHeight); Layout(rect);
                        string name = width + " quest atomic " + style + " " + sample.Item1;
                        var labels = new[] { view.rewardAmountLabel, view.secondaryRewardAmountLabel };
                        var icons = new[] { view.rewardIcon, view.secondaryRewardIcon };
                        var action = (RectTransform)view.actionButton.transform;
                        var track = (RectTransform)view.progressLabel.transform.parent;
                        for (int i = 0; i < labels.Length; i++)
                        {
                            check(labels[i].text == "×" + NumberNotation.Format(amounts[i]) && labels[i].fontSize == 36 && AtomicQuantityFits(labels[i]),
                                name + " displays unchanged complete fixed token " + i + "; " + TextDetails(labels[i]));
                            check(Inside(rect, labels[i].rectTransform) && Inside(rect, icons[i].rectTransform) &&
                                !Overlaps(rect, labels[i].rectTransform, icons[i].rectTransform) &&
                                !Overlaps(rect, track, (RectTransform)icons[i].transform.parent) &&
                                !Overlaps(rect, action, (RectTransform)icons[i].transform.parent) &&
                                !Overlaps(rect, view.titleLabel.rectTransform, (RectTransform)icons[i].transform.parent),
                                name + " separates reward " + i + " from its icon, title, progress and action");
                        }
                        check(Fits(view.titleLabel) && view.titleLabel.textInfo.lineCount == 2 && Inside(rect, action) &&
                            action.rect.height >= 144 && !Overlaps(rect, view.titleLabel.rectTransform, action),
                            name + " preserves two-line title and action; " + TextDetails(view.titleLabel));
                        check(Inside(rect, track) && Fits(view.progressLabel) && view.progressLabel.textInfo.lineCount <= 2,
                            name + " retains long progress beside both complete rewards; " + TextDetails(view.progressLabel));
                        if (sample.Item3)
                            check(Fits(view.periodLabel) && Inside(rect, view.periodLabel.rectTransform) &&
                                !Overlaps(rect, view.periodLabel.rectTransform, action), name + " preserves its prior period; " + TextDetails(view.periodLabel));
                    }
                }
                check(view.GetComponentsInChildren<Transform>(true).Length == descendants, width + " quest rebinds do not create duplicate UI");
            }
            finally { styleField.SetValue(null, originalStyle); view.gameObject.SetActive(false); Object.DestroyImmediate(view.gameObject); }
        }

        private static GachaPullButtonView AddBuy(UIViewCatalog catalog, EnhanceCardView card) =>
            Object.Instantiate(catalog.itemGachaPullButton, card.ButtonRow, false).GetComponent<GachaPullButtonView>();
        private static void AddTab(UIViewCatalog catalog, Transform parent, string text, Sprite icon, float height)
        {
            var tab = Object.Instantiate(catalog.itemNavTabButton, parent, false).GetComponent<NavTabButtonView>();
            tab.SetLabel(text); tab.SetIcon(icon);
            tab.GetComponent<LayoutElement>().minHeight = tab.GetComponent<LayoutElement>().preferredHeight = height;
        }
        private static void Size(RectTransform rect, float width, float height)
        { rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f); rect.sizeDelta = new Vector2(width, height); }
        private static void Layout(RectTransform rect)
        { Canvas.ForceUpdateCanvases(); LayoutRebuilder.ForceRebuildLayoutImmediate(rect); Canvas.ForceUpdateCanvases(); }
        private static bool Fits(TMP_Text label)
        { label.ForceMeshUpdate(true, true); return !label.isTextTruncated && label.preferredHeight <= label.rectTransform.rect.height + 2f; }
        private static bool AtomicQuantityFits(TMP_Text label)
        {
            if (!Fits(label)) return false;
            var rect = label.rectTransform.rect;
            return label.textWrappingMode == TextWrappingModes.NoWrap && label.textInfo.lineCount == 1 &&
                label.overflowMode != TextOverflowModes.Ellipsis && label.preferredWidth <= rect.width + 1f &&
                label.textBounds.min.x >= rect.xMin - 1f && label.textBounds.max.x <= rect.xMax + 1f;
        }
        private static string TextDetails(TMP_Text label)
        {
            var face = label.font.faceInfo;
            return $"text='{label.text}' rect={label.rectTransform.rect.size} preferred=({label.preferredWidth:0.##},{label.preferredHeight:0.##}) " +
                $"font={label.font.name} size={label.fontSize:0.##} facePoint={face.pointSize:0.##} lineHeight={face.lineHeight:0.##} " +
                $"ascent={face.ascentLine:0.##} descent={face.descentLine:0.##} wrap={label.textWrappingMode} " +
                $"lines={label.textInfo.lineCount} truncated={label.isTextTruncated}";
        }
        private static bool Inside(RectTransform parent, RectTransform child)
        {
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(parent, child);
            return bounds.min.x >= parent.rect.xMin - 1 && bounds.max.x <= parent.rect.xMax + 1 &&
                bounds.min.y >= parent.rect.yMin - 1 && bounds.max.y <= parent.rect.yMax + 1;
        }
        private static bool Overlaps(RectTransform parent, RectTransform a, RectTransform b)
        {
            var left = RectTransformUtility.CalculateRelativeRectTransformBounds(parent, a);
            var right = RectTransformUtility.CalculateRelativeRectTransformBounds(parent, b);
            return Mathf.Min(left.max.x, right.max.x) - Mathf.Max(left.min.x, right.min.x) > 1 &&
                Mathf.Min(left.max.y, right.max.y) - Mathf.Max(left.min.y, right.min.y) > 1;
        }
    }
}
