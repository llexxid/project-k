using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>
    /// 런타임 코드생성 UI → 프리팹 전환용 생성기 (가이드 패널).
    /// GuidePanelController.BuildStepRow / AddEmptyHint 의 코드빌드 구조를
    /// Item_GuideStepRow / Item_GuideEmptyHint 프리팹 + View 로 굽는다.
    /// </summary>
    internal static class GuidePanelPrefabGens
    {
        // GameUI.uss guide-* 토큰 (원본 GuidePanelController 색상 그대로)
        private static readonly Color TitleColor = new Color(1f, 1f, 1f, 0.95f);        // .guide-step-title
        private static readonly Color DescColor = new Color(1f, 1f, 1f, 0.80f);         // .guide-step-desc
        private static readonly Color CheckColor = new Color(100f / 255f, 210f / 255f, 130f / 255f, 0.95f);
        private static readonly Color CheckBorderColor = new Color(1f, 1f, 1f, 0.25f);
        private static readonly Color CheckBgColor = new Color(1f, 1f, 1f, 0.10f);
        private static readonly Color EmptyHintColor = new Color(1f, 1f, 1f, 0.5f);

        internal static void GenerateAll()
        {
            GenerateGuideStepRow();
            GenerateGuideEmptyHint();
        }

        // ── 가이드 단계 카드 행 ──
        internal static GameObject GenerateGuideStepRow()
        {
            // 픽셀 카드 배경 (UguiRuntimeFactory.PixelCard 대응)
            var row = F.Box(null, "Item_GuideStepRow", F.CardDark, rounded: true, raycast: false);
            if (F.Catalog != null && F.Catalog.kitCard != null)
            {
                row.sprite = F.Catalog.kitCard;
                row.type = Image.Type.Sliced;
                row.pixelsPerUnitMultiplier = UguiPixelSkin.PpuMultiplierForBorder(F.Catalog.kitCard, 8f);
            }
            F.VLayout(row.gameObject, 0f);

            var view = row.gameObject.AddComponent<GuideStepRowView>();
            view.canvasGroup = row.gameObject.AddComponent<CanvasGroup>();

            // 본문: padding 16, gap 16, align-items flex-start
            var inner = F.Container(row.transform, "RowInner");
            F.HLayout(inner.gameObject, 16f, new RectOffset(16, 16, 16, 16), TextAnchor.UpperLeft);

            // ── .guide-check-btn: 52x52 원형(흰10% 배경 + 2px 테두리) ──
            var checkBorder = F.Box(inner, "CheckBtn", CheckBorderColor, rounded: false, raycast: true);
            if (F.Circle != null) { checkBorder.sprite = F.Circle; checkBorder.type = Image.Type.Simple; }
            var checkLe = F.Preferred(checkBorder, width: 52f, height: 52f);
            checkLe.minWidth = 52f;   // flex-shrink: 0
            checkLe.minHeight = 52f;
            view.checkBorder = checkBorder;

            var checkBg = F.Box(checkBorder.transform, "CheckBg", CheckBgColor, rounded: false);
            if (F.Circle != null) { checkBg.sprite = F.Circle; checkBg.type = Image.Type.Simple; }
            F.Stretch(checkBg.rectTransform);
            checkBg.rectTransform.offsetMin = new Vector2(2f, 2f);
            checkBg.rectTransform.offsetMax = new Vector2(-2f, -2f);

            var checkBtn = checkBorder.gameObject.AddComponent<Button>();
            checkBtn.targetGraphic = checkBorder;
            checkBtn.transition = Selectable.Transition.ColorTint;
            checkBtn.colors = UguiTheme.MakeColorBlock();
            checkBorder.gameObject.AddComponent<PlayClickSfxOnClick>();
            view.checkButton = checkBtn;

            // 체크 표시 — 픽셀 키트 아이콘, 없으면 "V" 폴백 (원본 로직 그대로)
            if (F.Catalog != null && F.Catalog.iconCheck != null)
            {
                var checkIcon = F.IconImage(checkBorder.transform, "Icon", F.Catalog.iconCheck, 30f, 30f);
                F.AnchorCenter(checkIcon.rectTransform, 30f, 30f);
                checkIcon.gameObject.SetActive(false);   // done 상태는 런타임에서 토글
                view.checkIcon = checkIcon;
            }
            else
            {
                var checkLabel = F.Text(checkBorder.transform, "CheckLabel", "", 26f, CheckColor,
                    TextAlignmentOptions.Center, bold: true);
                F.Stretch(checkLabel.rectTransform);
                view.checkLabel = checkLabel;
            }

            // ── .guide-text-col ──
            var textCol = F.Container(inner, "TextCol");
            F.VLayout(textCol.gameObject, 6f);
            F.Flexible(textCol, flexWidth: 1f);

            var titleLabel = F.Text(textCol, "Title", "", 28f, TitleColor,
                TextAlignmentOptions.Left, bold: true, wrap: true);
            F.Preferred(titleLabel, height: 36f);
            view.titleLabel = titleLabel;

            var descLabel = F.Text(textCol, "Desc", "", 23f, DescColor,
                TextAlignmentOptions.Left, bold: false, wrap: true);
            F.Preferred(descLabel, height: 32f);
            view.descLabel = descLabel;

            var hintLabel = F.Text(textCol, "Hint", "", 21f, UguiTheme.GuideHintBlue,
                TextAlignmentOptions.Left, bold: false, wrap: true);
            F.Preferred(hintLabel, height: 28f);
            hintLabel.gameObject.SetActive(false);   // 미완료 단계에서만 런타임에 표시
            view.hintLabel = hintLabel;

            return PrefabGenUtil.SavePrefab(row.gameObject, $"{PrefabGenUtil.PrefabRoot}/Items/Item_GuideStepRow.prefab");
        }

        // ── 빈 상태 힌트 ──
        internal static GameObject GenerateGuideEmptyHint()
        {
            var root = F.Container(null, "Item_GuideEmptyHint");
            F.VLayout(root.gameObject, 0f, new RectOffset(0, 0, 40, 0));   // padding-top 40
            var view = root.gameObject.AddComponent<GuideEmptyHintView>();

            var lbl = F.Text(root, "Label", "", 26f, EmptyHintColor,
                TextAlignmentOptions.Center, bold: false, wrap: true);
            F.Preferred(lbl, height: 32f);
            view.label = lbl;

            return PrefabGenUtil.SavePrefab(root.gameObject, $"{PrefabGenUtil.PrefabRoot}/Items/Item_GuideEmptyHint.prefab");
        }
    }
}
