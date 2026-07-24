using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>
    /// 인벤토리 패널의 런타임 코드생성 UI → 프리팹 전환용 생성기.
    /// - Item_InventoryListPage : 섹션/소섹션 제목 + 장비 그리드 + 플레이스홀더
    /// - Item_InventoryEquipDetail : 장비 상세/강화 페이지 (아이콘/스탯/액션/강화 정보)
    /// 두 프리팹 모두 콘텐츠 스크롤 안에 인스턴스화되므로 VerticalLayout + ContentSizeFitter를 갖는다.
    /// </summary>
    internal static class InventoryPanelPrefabGens
    {
        private static readonly Color Dim70 = new Color(1f, 1f, 1f, 0.70f);
        private static readonly Color Dim90 = new Color(1f, 1f, 1f, 0.90f);
        private static readonly Color Dim40 = new Color(1f, 1f, 1f, 0.40f);

        internal static void GenerateAll()
        {
            GenerateInventoryListPage();
            GenerateInventoryEquipDetail();
        }

        // ── 목록 페이지 (전체/장비 탭 콘텐츠) ──
        internal static GameObject GenerateInventoryListPage()
        {
            var root = F.Container(null, "Item_InventoryListPage");
            root.sizeDelta = new Vector2(UguiTheme.RefWidth, 200f);
            F.VLayout(root.gameObject, 10f, null, TextAnchor.UpperLeft, expandWidth: true);
            AddContentFitter(root.gameObject);
            var view = root.gameObject.AddComponent<InventoryListPageView>();

            var section = F.Text(root, "SectionTitle", "인벤토리", 28f, UguiTheme.TextPrimary, TextAlignmentOptions.Left, bold: true);
            F.Preferred(section, height: 40f);
            view.sectionTitle = section;

            var sub = F.Text(root, "SubsectionTitle", "장비", 24f, Dim90, TextAlignmentOptions.Left, bold: true);
            F.Preferred(sub, height: 34f);
            view.subsectionTitle = sub;

            var grid = F.Container(root, "EquipGrid");
            var gl = grid.gameObject.AddComponent<GridLayoutGroup>();
            gl.cellSize = new Vector2(160f, 220f);
            gl.spacing = new Vector2(10f, 10f);
            gl.childAlignment = TextAnchor.UpperCenter;
            gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gl.constraintCount = 6;
            view.grid = grid;

            var placeholder = F.Text(root, "Placeholder", "비어있음", 24f, Dim40, TextAlignmentOptions.Center);
            F.Preferred(placeholder, height: 60f);
            view.placeholder = placeholder;

            return PrefabGenUtil.SavePrefab(root.gameObject, $"{PrefabGenUtil.PrefabRoot}/Items/Item_InventoryListPage.prefab");
        }

        // ── 장비 상세/강화 페이지 ──
        internal static GameObject GenerateInventoryEquipDetail()
        {
            var root = F.Container(null, "Item_InventoryEquipDetail");
            root.sizeDelta = new Vector2(UguiTheme.RefWidth, 400f);
            F.VLayout(root.gameObject, 10f, null, TextAnchor.UpperLeft, expandWidth: true);
            AddContentFitter(root.gameObject);
            var view = root.gameObject.AddComponent<InventoryEquipDetailView>();

            // 뒤로가기 (.ka-back-btn: h44 / 22px)
            var back = F.TextButton(root, "BackBtn", "< 인벤토리", 22f, UguiTheme.SurfaceLight, out _);
            F.Preferred(back, height: 44f);
            view.backButton = back;

            // 섹션 제목 (고정)
            var sect = F.Text(root, "SectionTitle", "장비 상세", 28f, UguiTheme.TextPrimary, TextAlignmentOptions.Left, bold: true);
            F.Preferred(sect, height: 40f);

            // 장비 정보 (.ka-job-detail-header)
            var infoBox = F.Box(root, "InfoBox", new Color(1f, 1f, 1f, 0.05f));
            F.HLayout(infoBox.gameObject, 16f, new RectOffset(14, 14, 14, 14), TextAnchor.UpperLeft);

            var iconBox = F.Box(infoBox.transform, "Icon", UguiTheme.SurfaceLight);
            var iconLe = F.Preferred(iconBox, width: 120f, height: 120f);
            iconLe.minWidth = 120f;
            view.iconImage = iconBox;

            var infoCol = F.Container(infoBox.transform, "InfoCol");
            F.VLayout(infoCol.gameObject, 6f);
            F.Flexible(infoCol, flexWidth: 1f);

            view.nameLabel = MakeInfoLine(infoCol, 30f, UguiTheme.TextPrimary, bold: true);
            view.rarityLabel = MakeInfoLine(infoCol, 24f, Dim70);
            view.atkLabel = MakeInfoLine(infoCol, 24f, Dim70);
            view.hpLabel = MakeInfoLine(infoCol, 24f, Dim70);
            view.enhLabel = MakeInfoLine(infoCol, 24f, Dim70);
            view.equippedLabel = MakeInfoLine(infoCol, 24f, UguiTheme.SuccessGreenBright);
            view.equippedLabel.gameObject.SetActive(false);
            view.ownerLabel = MakeInfoLine(infoCol, 24f, Dim70);
            view.ownerLabel.gameObject.SetActive(false);

            // 액션 버튼 행 (.ka-equip-action-row)
            var actionRow = F.Container(root, "ActionRow");
            F.HLayout(actionRow.gameObject, 12f, null, TextAnchor.MiddleCenter, expandWidth: true);
            F.Preferred(actionRow, height: 64f);

            var detailBtn = F.TextButton(actionRow, "DetailBtn", "상세", 28f, UguiTheme.AccentBlue, out _);
            F.Flexible(detailBtn, flexWidth: 1f);
            view.detailButton = detailBtn;

            var enhBtn = F.TextButton(actionRow, "EnhanceBtn", "강화", 28f, UguiTheme.EnhanceOrange, out var enhLabel);
            F.Flexible(enhBtn, flexWidth: 1f);
            view.enhanceButton = enhBtn;
            view.enhanceButtonBg = enhBtn.GetComponent<Image>();
            view.enhanceButtonLabel = enhLabel;

            // 강화 정보 섹션 (MAX면 통째로 숨김)
            var enhSection = F.Container(root, "EnhanceSection");
            F.VLayout(enhSection.gameObject, 10f, null, TextAnchor.UpperLeft, expandWidth: true);
            AddContentFitter(enhSection.gameObject);
            view.enhanceSection = enhSection.gameObject;

            var enhTitle = F.Text(enhSection, "EnhTitle", "강화 정보", 24f, Dim90, TextAlignmentOptions.Left, bold: true);
            F.Preferred(enhTitle, height: 34f);
            view.matLabel = MakeInfoLine(enhSection, 24f, Dim70);
            view.rateLabel = MakeInfoLine(enhSection, 24f, Dim70);
            view.expectedLabel = MakeInfoLine(enhSection, 24f, Dim70);

            return PrefabGenUtil.SavePrefab(root.gameObject, $"{PrefabGenUtil.PrefabRoot}/Items/Item_InventoryEquipDetail.prefab");
        }

        // ── 유틸 ──

        private static void AddContentFitter(GameObject go)
        {
            var fitter = go.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        }

        /// <summary>원본 AddInfoLine 대응: 좌측 정렬 + wrap + Preferred(size+10).</summary>
        private static TextMeshProUGUI MakeInfoLine(Transform parent, float size, Color color, bool bold = false)
        {
            var lbl = F.Text(parent, "Info", "", size, color, TextAlignmentOptions.Left, bold, wrap: true);
            F.Preferred(lbl, height: size + 10f);
            return lbl;
        }
    }
}
