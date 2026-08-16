using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>
    /// 신 스킬 컬렉션북(도감) 팝업 프리팹 생성기.
    /// Popup_DivineCollection 셸만 만든다 — 카드 셀은 ItemGens.GenerateDivineCard()가 담당하고
    /// 런타임에 컨트롤러가 카탈로그에서 인스턴스화한다 (마탑 장착 팝업과 동일 관례).
    /// 레이아웃(1080x1920 기준): 패널 960x1200 = 타이틀바 76 + 보너스 줄 30
    /// + 카드 그리드 4x2 (셀 200x240, 간격 12, 높이 492) + 상세 페인(flex ≈410) + 액션 행 84.
    /// </summary>
    internal static class DivineCollectionPopupPrefabGens
    {
        /// <summary>단일 대상 재생성 — 손댄 다른 프리팹을 건드리지 않고 도감 팝업 + 카드 셀만 다시 만든다.</summary>
        [MenuItem("KingdomIdle/UGUI/Generate Divine Collection Popup", false, 7)]
        internal static void GenerateDivineCollectionOnly()
        {
            F.Init();
            var catalog = AssetDatabase.LoadAssetAtPath<UIViewCatalog>(PrefabGenUtil.CatalogPath);
            F.Catalog = catalog;
            ItemGens.GenerateDivineCard();
            GenerateDivineCollectionPopup();
            if (catalog != null)
                CatalogGen.AssignPrefabs(catalog);
            AssetDatabase.Refresh();
        }

        internal static void GenerateAll()
        {
            GenerateDivineCollectionPopup();
        }

        internal static GameObject GenerateDivineCollectionPopup()
        {
            var root = F.Root("Popup_DivineCollection");
            var view = root.gameObject.AddComponent<DivineCollectionPopupView>();

            // ── 오버레이 딤 (바깥 탭 → 닫기) ──
            var dim = F.Box(root, "Dim", UguiTheme.DimMedium, rounded: false, raycast: true);
            F.Stretch(dim.rectTransform);
            var dimBtn = dim.gameObject.AddComponent<Button>();
            dimBtn.targetGraphic = dim;
            dimBtn.transition = Selectable.Transition.None;
            view.backdropButton = dimBtn;

            // ── 패널 (960x1200, 어두운 배경 + 청동 픽셀 프레임) ──
            var panel = F.PixelPanel(root, "Panel", F.Catalog != null ? F.Catalog.kitWindow : null,
                F.FrameGold, 24f, raycast: true, baseColor: F.PanelBaseDarker);
            F.AnchorCenter(panel.rectTransform, 960f, 1200f);
            F.VLayout(panel.gameObject, 14f, new RectOffset(30, 30, 24, 28));
            view.panelBox = panel.rectTransform;
            F.CornerBrackets(panel.transform);

            // ── 타이틀바 ──
            var titleBar = F.PixelPanel(panel.transform, "TitleBar", F.Catalog != null ? F.Catalog.kitTitleBar : null,
                new Color(0.20f, 0.22f, 0.30f, 1f), 14f, frameOnly: false);
            F.HLayout(titleBar.gameObject, 8f, new RectOffset(18, 10, 6, 6), TextAnchor.MiddleLeft);
            F.Preferred(titleBar, height: 76f);
            var title = F.Text(titleBar.transform, "Title", "신 스킬", 34f, UguiTheme.TextPrimary,
                TextAlignmentOptions.Left, bold: true);
            F.Flexible(title, flexWidth: 1f);
            view.titleLabel = title;

            var closeImg = F.CircleBox(titleBar.transform, "BtnClose", new Color(0.55f, 0.2f, 0.2f, 1f), raycast: true);
            F.Preferred(closeImg, width: 72f, height: 72f);
            var closeBtn = closeImg.gameObject.AddComponent<Button>();
            closeBtn.targetGraphic = closeImg;
            closeBtn.transition = Selectable.Transition.ColorTint;
            closeBtn.colors = UguiTheme.MakeColorBlock();
            closeImg.gameObject.AddComponent<PlayClickSfxOnClick>();
            view.closeButton = closeBtn;
            if (F.Catalog != null && F.Catalog.iconX != null)
            {
                var xIcon = F.IconImage(closeImg.transform, "Icon", F.Catalog.iconX, 34f, 34f);
                F.AnchorCenter(xIcon.rectTransform, 34f, 34f);
            }

            // ── 컬렉션 보너스 요약 줄 ──
            var bonus = F.Text(panel.transform, "BonusLine", "컬렉션 보너스: -", 22f, UguiTheme.AccentGold,
                TextAlignmentOptions.Left);
            F.Preferred(bonus, height: 30f);
            view.bonusLabel = bonus;

            // ── 카드 그리드 4x2 (셀 200x240, 8종 고정 — 스크롤 없음) ──
            var grid = F.Container(panel.transform, "CardGrid");
            var gridLg = grid.gameObject.AddComponent<GridLayoutGroup>();
            gridLg.cellSize = new Vector2(200f, 240f);
            gridLg.spacing = new Vector2(12f, 12f);
            gridLg.childAlignment = TextAnchor.UpperCenter;
            gridLg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLg.constraintCount = 4;
            F.Preferred(gridLg, height: 492f);   // 2행 x 240 + 행간 12
            view.cardGrid = grid;

            // ── 상세 페인 (좌 일러스트 + 우 정보) ──
            var detail = F.Box(panel.transform, "Detail", new Color(0f, 0f, 0f, 0.25f), rounded: true);
            F.HLayout(detail.gameObject, 18f, new RectOffset(16, 16, 16, 16), TextAnchor.UpperLeft);
            F.Flexible(detail, flexHeight: 1f);   // 남는 세로 공간(≈410) 차지

            var illustBox = F.Box(detail.transform, "IllustBox", UguiTheme.SurfaceFaint, rounded: true);
            var illustLe = F.Preferred(illustBox, width: 280f);
            illustLe.minWidth = 280f;
            illustLe.flexibleHeight = 1f;
            var illust = F.Box(illustBox.transform, "Illust", Color.white, rounded: false);
            illust.enabled = false;              // 스프라이트 붙기 전엔 비활성 (흰 박스 방지)
            illust.preserveAspect = true;
            illust.raycastTarget = false;
            F.Stretch(illust.rectTransform);
            illust.rectTransform.offsetMin = new Vector2(8f, 8f);
            illust.rectTransform.offsetMax = new Vector2(-8f, -8f);
            view.illustration = illust;

            var info = F.Container(detail.transform, "Info");
            F.VLayout(info.gameObject, 8f, null, TextAnchor.UpperLeft);
            F.Flexible(info, flexWidth: 1f, flexHeight: 1f);

            // 이름 + 등급 알약
            var nameRow = F.Container(info, "NameRow");
            F.HLayout(nameRow.gameObject, 10f, null, TextAnchor.MiddleLeft);
            F.Preferred(nameRow, height: 42f);
            var cardName = F.Text(nameRow, "CardName", "-", 30f, UguiTheme.TextPrimary,
                TextAlignmentOptions.Left, bold: true);
            F.Flexible(cardName, flexWidth: 1f);
            view.cardNameLabel = cardName;

            var pill = F.Box(nameRow, "GradePill", UguiTheme.Bronze, rounded: true);
            var pillLe = F.Preferred(pill, width: 92f, height: 36f);
            pillLe.minWidth = 92f;
            view.gradePill = pill;
            var pillLbl = F.Text(pill.transform, "Label", "영웅", 20f, UguiTheme.TextPrimary,
                TextAlignmentOptions.Center, bold: true);
            F.Stretch(pillLbl.rectTransform);
            view.gradePillLabel = pillLbl;

            // 스킬 이름 / 설명
            var skillName = F.Text(info, "SkillName", "", 24f, UguiTheme.AccentGold,
                TextAlignmentOptions.Left, bold: true);
            F.Preferred(skillName, height: 32f);
            view.skillNameLabel = skillName;

            var desc = F.Text(info, "Desc", "", 20f, UguiTheme.TextSecondary,
                TextAlignmentOptions.Left, wrap: true);
            var descLe = F.Preferred(desc, height: 84f);
            descLe.flexibleHeight = 1f;          // 남는 세로 공간 흡수
            view.descriptionLabel = desc;

            // 계산 수치
            var statCd = F.Text(info, "StatCd", "쿨타임  -", 22f, UguiTheme.TextSecondary, TextAlignmentOptions.Left);
            F.Preferred(statCd, height: 30f);
            view.statCooldownLabel = statCd;

            var statMult = F.Text(info, "StatMult", "레벨 배율  -", 22f, UguiTheme.TextSecondary, TextAlignmentOptions.Left);
            F.Preferred(statMult, height: 30f);
            view.statMultiplierLabel = statMult;

            var statValue = F.Text(info, "StatValue", "", 24f, UguiTheme.AccentGoldStrong,
                TextAlignmentOptions.Left, bold: true);
            F.Preferred(statValue, height: 32f);
            view.statValueLabel = statValue;

            // ── 액션 행: [장착] [레벨업 (N/M)] / 미보유 안내 ──
            var actionRow = F.Container(panel.transform, "ActionRow");
            F.HLayout(actionRow.gameObject, 14f, null, TextAnchor.MiddleCenter, expandWidth: true);
            F.Preferred(actionRow, height: 84f);

            var equipBtn = F.TextButton(actionRow, "BtnEquip", "장착", 28f, UguiTheme.BtnConfirm, out var equipLbl);
            var equipLe = F.Preferred((RectTransform)equipBtn.transform, height: 84f);
            equipLe.flexibleWidth = 1f;
            view.equipButton = equipBtn;
            view.equipButtonLabel = equipLbl;

            var lvBtn = F.TextButton(actionRow, "BtnLevelUp", "레벨업 (0/0)", 28f, UguiTheme.BtnSpend, out var lvLbl);
            var lvLe = F.Preferred((RectTransform)lvBtn.transform, height: 84f);
            lvLe.flexibleWidth = 1f;
            view.levelUpButton = lvBtn;
            view.levelUpButtonLabel = lvLbl;

            var hint = F.Text(actionRow, "LockedHint", "미보유 — 신 스킬 뽑기에서 획득", 24f,
                UguiTheme.TextTertiary, TextAlignmentOptions.Center);
            F.Flexible(hint, flexWidth: 1f);
            hint.gameObject.SetActive(false);
            view.lockedHintLabel = hint;

            return PrefabGenUtil.SavePrefab(root.gameObject, $"{PrefabGenUtil.PrefabRoot}/Popups/Popup_DivineCollection.prefab");
        }
    }
}
