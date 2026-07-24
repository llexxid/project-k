using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>
    /// 런타임 코드생성 UI → 프리팹 전환용 생성기 (마탑 스킬 상세 팝업).
    /// 원본 MageTowerDetailPopupController.EnsureBuilt()가 UguiRuntimeFactory로 만들던
    /// 고정 구조(헤더/아이콘·스탯/강화·각성·초기화 섹션)를 그대로 프리팹 Panel_MageTowerDetail 로 생성하고
    /// View 필드를 배선한다. 반복 셀은 없으므로 아이템 프리팹은 만들지 않는다.
    /// </summary>
    internal static class MageTowerDetailPopupPrefabGens
    {
        // 원본 액션버튼 색상 (UguiPixelSkin.ApplyButton으로 픽셀 버튼 스킨 매핑)
        private static readonly Color EnhanceColor = new Color(80f / 255f, 140f / 255f, 220f / 255f, 0.70f);
        private static readonly Color AwakenColor = new Color(160f / 255f, 80f / 255f, 220f / 255f, 0.70f);
        private static readonly Color ResetColor = new Color(200f / 255f, 70f / 255f, 70f / 255f, 0.60f);

        private static readonly Color StatDim = new Color(1f, 1f, 1f, 0.70f);
        private static readonly Color SectionStat = new Color(1f, 1f, 1f, 0.85f);
        private static readonly Color SectionBg = new Color(0f, 0f, 0f, 0.25f);

        internal static GameObject GenerateMageTowerDetailPopup()
        {
            var root = F.Root("Panel_MageTowerDetail");
            var view = root.gameObject.AddComponent<MageTowerDetailPopupView>();

            // ── 오버레이 딤 (바깥 탭 → 닫기) ──
            var dim = F.Box(root, "Dim", UguiTheme.DimMedium, rounded: false, raycast: true);
            F.Stretch(dim.rectTransform);
            var dimBtn = dim.gameObject.AddComponent<Button>();
            dimBtn.targetGraphic = dim;
            dimBtn.transition = Selectable.Transition.None;
            view.backdropButton = dimBtn;

            // ── 패널 (max-width 600, 어두운 배경 + 금색 픽셀 프레임, ContentSizeFitter 세로) ──
            var panel = F.PixelPanel(root, "Panel", F.Catalog != null ? F.Catalog.kitWindow : null,
                F.FrameGold, 24f, raycast: true, baseColor: F.PanelBaseDarker);
            F.AnchorCenter(panel.rectTransform, 600f, 0f);
            F.VLayout(panel.gameObject, 14f, new RectOffset(22, 22, 22, 22));
            var fitter = panel.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // ── 헤더 (타이틀 + 닫기) ──
            var header = F.Container(panel.transform, "Header");
            F.HLayout(header.gameObject, 8f, null, TextAnchor.MiddleLeft);
            F.Preferred(header, height: 60f);

            var title = F.Text(header.transform, "Title", "스킬", 32f, UguiTheme.TextPrimary,
                TextAlignmentOptions.Left, bold: true);
            F.Flexible(title, flexWidth: 1f);
            view.titleLabel = title;

            var closeBg = F.Box(header.transform, "BtnClose", UguiTheme.SurfaceMid, rounded: true, raycast: true);
            F.Preferred(closeBg, width: 60f, height: 60f);
            var closeBtn = closeBg.gameObject.AddComponent<Button>();
            closeBtn.targetGraphic = closeBg;
            closeBtn.colors = UguiTheme.MakeColorBlock();
            closeBg.gameObject.AddComponent<PlayClickSfxOnClick>();
            view.closeButton = closeBtn;
            var closeLbl = F.Text(closeBg.transform, "X", "X", 28f, UguiTheme.TextPrimary, TextAlignmentOptions.Center, bold: true);
            F.Stretch(closeLbl.rectTransform);

            // ── 아이콘 + 스탯 ──
            var iconRow = F.Container(panel.transform, "IconRow");
            F.HLayout(iconRow.gameObject, 16f, null, TextAnchor.UpperLeft);
            F.Preferred(iconRow, height: 130f);

            var iconBg = F.Box(iconRow.transform, "IconBg", UguiTheme.SurfaceLight, rounded: true);
            var iconLe = F.Preferred(iconBg, width: 90f, height: 90f);
            iconLe.minWidth = 90f;
            var icon = F.Box(iconBg.transform, "Icon", Color.white, rounded: false);
            icon.enabled = false;              // 스프라이트 붙기 전엔 비활성 (흰 박스 방지)
            icon.preserveAspect = true;
            F.Stretch(icon.rectTransform);
            icon.rectTransform.offsetMin = new Vector2(6f, 6f);
            icon.rectTransform.offsetMax = new Vector2(-6f, -6f);
            view.icon = icon;

            var statsCol = F.Container(iconRow.transform, "Stats");
            F.VLayout(statsCol.gameObject, 4f);
            F.Flexible(statsCol, flexWidth: 1f);

            view.lblBaseDmg = StatLabel(statsCol, StatDim);
            view.lblEffDmg = StatLabel(statsCol, UguiTheme.TextPrimary);
            view.lblBaseCd = StatLabel(statsCol, StatDim);
            view.lblEffCd = StatLabel(statsCol, UguiTheme.TextPrimary);

            // ── 강화 섹션 ──
            var enhContent = Section(panel.transform, "강화");
            view.lblEnhLevel = StatLabel(enhContent, SectionStat);
            view.lblEnhCost = StatLabel(enhContent, SectionStat);
            view.btnEnhance = ActionButton(enhContent, "BtnEnhance", "강화하기", EnhanceColor, out var enhLabel);
            view.btnEnhanceLabel = enhLabel;

            // ── 각성 섹션 ──
            var awkContent = Section(panel.transform, "각성");
            view.lblAwkLevel = StatLabel(awkContent, SectionStat);
            view.lblAwkCost = StatLabel(awkContent, SectionStat);
            view.btnAwaken = ActionButton(awkContent, "BtnAwaken", "각성하기", AwakenColor, out var awkLabel);
            view.btnAwakenLabel = awkLabel;

            // ── 초기화 섹션 (타이틀 없음) ──
            var resetContent = Section(panel.transform, null);
            view.lblResetRefund = StatLabel(resetContent, SectionStat);
            view.btnReset = ActionButton(resetContent, "BtnReset", "초기화", ResetColor, out _);

            return PrefabGenUtil.SavePrefab(root.gameObject, $"{PrefabGenUtil.PrefabRoot}/Popups/Panel_MageTowerDetail.prefab");
        }

        private static TMP_Text StatLabel(RectTransform parent, Color color)
        {
            var lbl = F.Text(parent.transform, "Stat", "", 24f, color, TextAlignmentOptions.Left);
            F.Preferred(lbl, height: 32f);
            return lbl;
        }

        /// <summary>mt-detail-section: bg black@25% radius12 padding12. 반환: 콘텐츠(=섹션)를 붙일 부모.</summary>
        private static RectTransform Section(Transform parent, string title)
        {
            var section = F.Box(parent, "Section", SectionBg, rounded: true);
            F.VLayout(section.gameObject, 6f, new RectOffset(12, 12, 12, 12));
            if (!string.IsNullOrEmpty(title))
            {
                var titleLbl = F.Text(section.transform, "Title", title, 26f, UguiTheme.AccentGold, TextAlignmentOptions.Left, bold: true);
                F.Preferred(titleLbl, height: 34f);
            }
            return section.rectTransform;
        }

        private static Button ActionButton(RectTransform parent, string name, string label, Color bg, out TMP_Text labelText)
        {
            var btn = F.TextButton(parent.transform, name, label, 24f, bg, out var tmp);
            F.Preferred((RectTransform)btn.transform, height: 62f);
            labelText = tmp;
            return btn;
        }
    }
}
