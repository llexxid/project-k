using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>
    /// 왕국군 패널 컨트롤러의 '런타임 코드생성 UI'를 프리팹으로 전환하는 생성기.
    /// 각 서브 화면(종합/장비/스킬/전직)과 드릴다운(장비 상세/전직 상세)을
    /// 콘텐츠 프리팹 + View 로 만들고, 반복 셀은 기존 Item_* 프리팹을 재사용한다.
    ///
    /// 콘텐츠 프리팹 루트는 VerticalLayoutGroup 만 가지며(배경 이미지 없음),
    /// Panel_KingdomArmy 의 스크롤 Content(VerticalLayout + ContentSizeFitter) 자식으로
    /// Instantiate 되어 폭은 확장·높이는 preferred 로 자동 배치된다.
    /// </summary>
    internal static class KingdomArmyPanelPrefabGens
    {
        // 컨트롤러와 동일한 색 토큰
        private static readonly Color StatLineColor = new Color(1f, 1f, 1f, 0.70f);
        private static readonly Color PlaceholderColor = new Color(1f, 1f, 1f, 0.40f);
        private static readonly Color EquippedGreen = new Color(80f / 255f, 200f / 255f, 120f / 255f, 1f);

        internal static void GenerateAll()
        {
            GenerateStatCompareRow();
            GenerateMessage();
            GenerateCharacterSheet();
            GenerateEquipment();
            GenerateEquipDetail();
            GenerateSkill();
            GenerateJobChange();
            GenerateJobDetail();
        }

        // ══════════════════════════════════════
        //  종합(캐릭터) 시트
        // ══════════════════════════════════════
        internal static GameObject GenerateCharacterSheet()
        {
            var (root, view) = NewContent<KACharacterSheetView>("Panel_KACharacterSheet");

            var header = F.Box(root, "CharHeader", new Color(1f, 1f, 1f, 0.05f));
            F.HLayout(header.gameObject, 16f, new RectOffset(12, 12, 12, 12), TextAnchor.UpperLeft);

            // 초상화 (120x120 + RectMask2D 클리핑)
            var portrait = F.Box(header.transform, "Portrait", UguiTheme.SurfaceLight);
            var ple = F.Preferred(portrait, width: 120f, height: 120f);
            ple.minWidth = 120f;
            portrait.gameObject.AddComponent<RectMask2D>();

            var inner = F.Box(portrait.transform, "Inner", Color.white, rounded: false);
            inner.enabled = false;
            inner.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            inner.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            inner.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            view.portraitInner = inner;

            var info = F.Container(header.transform, "Info");
            F.VLayout(info.gameObject, 6f);
            F.Flexible(info, flexWidth: 1f);
            view.jobLabel = StatLine(info, "직업: -");
            view.hpLabel = StatLine(info, "HP: -");
            view.atkLabel = StatLine(info, "공격력: -");
            view.moveLabel = StatLine(info, "이동속도: -");

            SectionTitle(root, "장착 장비");
            view.equippedLabel = StatLine(root, "없음");

            return Save(root, "Panels/Panel_KACharacterSheet.prefab");
        }

        // ══════════════════════════════════════
        //  장비 탭
        // ══════════════════════════════════════
        internal static GameObject GenerateEquipment()
        {
            var (root, view) = NewContent<KAEquipmentView>("Panel_KAEquipment");

            SectionTitle(root, "장비");
            SubsectionTitle(root, "장착 중");

            // 장착 카드 (인벤토리 셀과 동일 크기의 그리드 셀)
            var equippedGrid = F.Container(root, "EquippedGrid");
            MakeEquipGrid(equippedGrid.gameObject);

            var card = F.Box(equippedGrid, "EquippedCard", UguiTheme.SurfaceFaint);
            F.VLayout(card.gameObject, 4f, new RectOffset(6, 6, 8, 8), TextAnchor.UpperCenter);

            var frame = F.Frame(card.transform, "EquippedFrame", EquippedGreen);
            frame.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            view.equippedFrame = frame;

            view.equippedSlotLabel = CardLabel(card.transform, "무기", 20f, new Color(1f, 1f, 1f, 0.85f));

            var iconWrap = F.Container(card.transform, "IconWrap");
            F.Preferred(iconWrap.gameObject.AddComponent<LayoutElement>(), height: 64f);
            var icon = F.Box(iconWrap, "Icon", UguiTheme.SurfaceLight);
            F.AnchorCenter(icon.rectTransform, 60f, 60f);
            view.equippedIconWrap = iconWrap;
            view.equippedIcon = icon;

            view.equippedNameLabel = CardLabel(card.transform, "-", 20f, new Color(1f, 1f, 1f, 0.85f));
            view.equippedStatLabel = CardLabel(card.transform, "-", 18f, new Color(1f, 1f, 1f, 0.45f));

            var unBtn = F.TextButton(card.transform, "BtnUnequip", "해제", 20f,
                new Color(60f / 255f, 130f / 255f, 230f / 255f, 0.60f), out _);
            F.Preferred(unBtn, height: 44f);
            view.unequipButton = unBtn;

            SubsectionTitle(root, "보유 장비");
            var grid = F.Container(root, "InventoryGrid");
            MakeEquipGrid(grid.gameObject);
            view.inventoryGrid = grid;

            view.emptyLabel = Placeholder(root, "보유한 장비가 없습니다.");

            return Save(root, "Panels/Panel_KAEquipment.prefab");
        }

        // ══════════════════════════════════════
        //  장비 상세 / 액션
        // ══════════════════════════════════════
        internal static GameObject GenerateEquipDetail()
        {
            var (root, view) = NewContent<KAEquipDetailView>("Panel_KAEquipDetail");

            var back = F.TextButton(root, "BtnBack", "< 장비 목록", 22f, UguiTheme.SurfaceLight, out _);
            F.Preferred(back, height: 44f);
            view.backButton = back;

            SectionTitle(root, "장비 상세");

            var infoBox = F.Box(root, "InfoBox", new Color(1f, 1f, 1f, 0.05f));
            F.HLayout(infoBox.gameObject, 16f, new RectOffset(14, 14, 14, 14), TextAnchor.UpperLeft);

            var iconBg = F.Box(infoBox.transform, "Icon", UguiTheme.SurfaceLight);
            var iconLe = F.Preferred(iconBg, width: 120f, height: 120f);
            iconLe.minWidth = 120f;
            view.icon = iconBg;

            var infoCol = F.Container(infoBox.transform, "InfoCol");
            F.VLayout(infoCol.gameObject, 6f);
            F.Flexible(infoCol, flexWidth: 1f);

            var nameLbl = F.Text(infoCol, "Name", "-", 30f, UguiTheme.TextPrimary, TextAlignmentOptions.Left, bold: true);
            F.Preferred(nameLbl, height: 40f);
            view.nameLabel = nameLbl;
            view.rarityLabel = StatLine(infoCol, "등급: -");
            view.atkLabel = StatLine(infoCol, "공격력 보너스: -");
            view.hpLabel = StatLine(infoCol, "HP 보너스: -");
            view.enhanceLabel = StatLine(infoCol, "강화 레벨: -");
            var eqNow = F.Text(infoCol, "EquippedNow", "현재 장착 중", 24f, UguiTheme.SuccessGreenBright, TextAlignmentOptions.Left);
            F.Preferred(eqNow, height: 32f);
            view.equippedNowLabel = eqNow;

            var actionRow = F.Container(root, "ActionRow");
            F.HLayout(actionRow.gameObject, 12f, null, TextAnchor.MiddleCenter, expandWidth: true);
            view.actionRow = actionRow;

            var enhGroup = F.Container(root, "EnhanceInfo");
            F.VLayout(enhGroup.gameObject, 10f);
            view.enhanceInfoGroup = enhGroup;
            SubsectionTitle(enhGroup, "강화 정보");
            var mat = F.Text(enhGroup, "Material", "-", 24f, StatLineColor, TextAlignmentOptions.Left, wrap: true);
            F.Preferred(mat, height: 34f);
            view.materialLabel = mat;
            view.successRateLabel = StatLine(enhGroup, "성공 확률: -");
            view.expectedLabel = StatLine(enhGroup, "강화 시 예상: -");

            return Save(root, "Panels/Panel_KAEquipDetail.prefab");
        }

        // ══════════════════════════════════════
        //  스킬 탭
        // ══════════════════════════════════════
        internal static GameObject GenerateSkill()
        {
            var (root, view) = NewContent<KASkillView>("Panel_KASkill");

            SectionTitle(root, "스킬");
            var list = F.Container(root, "SkillList");
            F.VLayout(list.gameObject, 10f);
            view.skillList = list;
            view.placeholder = Placeholder(root, "-");

            return Save(root, "Panels/Panel_KASkill.prefab");
        }

        // ══════════════════════════════════════
        //  전직 목록
        // ══════════════════════════════════════
        internal static GameObject GenerateJobChange()
        {
            var (root, view) = NewContent<KAJobChangeView>("Panel_KAJobChange");

            SectionTitle(root, "전직");
            view.placeholder = Placeholder(root, "직업 데이터가 없습니다.");

            var group = F.Container(root, "ContentGroup");
            F.VLayout(group.gameObject, 10f);
            view.contentGroup = group;

            // 전직 파편 배너 (갈색 bg + 골드 테두리)
            var banner = F.Box(group, "FragBanner", new Color(60f / 255f, 45f / 255f, 20f / 255f, 0.55f));
            F.HLayout(banner.gameObject, 12f, new RectOffset(16, 16, 10, 10), TextAnchor.MiddleLeft);
            F.Preferred(banner, height: 64f);
            var bframe = F.Frame(banner.transform, "Frame", new Color(1f, 220f / 255f, 100f / 255f, 0.60f));
            bframe.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            var bname = F.Text(banner.transform, "Name", "전직 파편", 24f, new Color(1f, 1f, 1f, 0.85f), TextAlignmentOptions.Left, bold: true);
            F.Preferred(bname, width: 130f, height: 40f);
            var bval = F.Text(banner.transform, "Value", "0", 34f, UguiTheme.AccentGoldStrong, TextAlignmentOptions.Left, bold: true);
            F.Preferred(bval, width: 140f, height: 44f);
            view.bannerValue = bval;
            var bhint = F.Text(banner.transform, "Hint", "(전직당 0개 소모)", 20f, new Color(1f, 1f, 1f, 0.55f), TextAlignmentOptions.Left);
            F.Flexible(bhint, flexWidth: 1f);
            view.bannerHint = bhint;

            JobSectionTitle(group, "1차 전직");
            var basic = F.Container(group, "BasicGrid");
            MakeJobGrid(basic.gameObject);
            view.basicGrid = basic;

            JobSectionTitle(group, "2차 전직 (정예)");
            var elite = F.Container(group, "EliteGrid");
            MakeJobGrid(elite.gameObject);
            view.eliteGrid = elite;

            return Save(root, "Panels/Panel_KAJobChange.prefab");
        }

        // ══════════════════════════════════════
        //  전직 상세
        // ══════════════════════════════════════
        internal static GameObject GenerateJobDetail()
        {
            var (root, view) = NewContent<KAJobDetailView>("Panel_KAJobDetail");

            var back = F.TextButton(root, "BtnBack", "< 전직 목록", 22f, UguiTheme.SurfaceLight, out _);
            F.Preferred(back, height: 44f);
            view.backButton = back;

            SectionTitle(root, "전직 상세");

            var header = F.Box(root, "JobHeader", new Color(1f, 1f, 1f, 0.05f));
            F.HLayout(header.gameObject, 16f, new RectOffset(14, 14, 14, 14), TextAnchor.UpperLeft);

            var imgBg = F.Box(header.transform, "Img", UguiTheme.SurfaceLight);
            var imgLe = F.Preferred(imgBg, width: 120f, height: 120f);
            imgLe.minWidth = 120f;
            view.image = imgBg;

            var nameCol = F.Container(header.transform, "NameCol");
            F.VLayout(nameCol.gameObject, 6f);
            F.Flexible(nameCol, flexWidth: 1f);

            var nameRow = F.Container(nameCol, "NameRow");
            F.HLayout(nameRow.gameObject, 12f, null, TextAnchor.MiddleLeft);
            F.Preferred(nameRow.gameObject.AddComponent<LayoutElement>(), height: 42f);
            var jobName = F.Text(nameRow, "JobName", "-", 30f, UguiTheme.TextPrimary, TextAlignmentOptions.Left, bold: true);
            F.Preferred(jobName, height: 42f);
            view.jobNameLabel = jobName;
            var badge = F.Text(nameRow, "StateBadge", "-", 18f, new Color(1f, 1f, 1f, 0.55f), TextAlignmentOptions.Left, bold: true);
            F.Preferred(badge, width: 90f, height: 30f);
            view.stateBadge = badge;

            var role = F.Text(nameCol, "Role", "-", 22f, new Color(1f, 1f, 1f, 0.65f), TextAlignmentOptions.Left);
            F.Preferred(role, height: 30f);
            view.roleLabel = role;

            SubsectionTitle(root, "스탯 비교");
            var table = F.Box(root, "StatCompareTable", new Color(0f, 0f, 0f, 0.20f));
            F.VLayout(table.gameObject, 2f, new RectOffset(10, 10, 8, 8));
            view.compareTable = table.rectTransform;

            var skillGroup = F.Container(root, "SkillGroup");
            F.VLayout(skillGroup.gameObject, 10f);
            view.skillGroup = skillGroup;
            SubsectionTitle(skillGroup, "직업 스킬");
            var skillList = F.Container(skillGroup, "SkillList");
            F.VLayout(skillList.gameObject, 10f);
            view.skillList = skillList;

            SubsectionTitle(root, "전직 조건");
            var condBox = F.Box(root, "CondBox", new Color(0f, 0f, 0f, 0.25f));
            F.VLayout(condBox.gameObject, 6f, new RectOffset(12, 12, 12, 12));
            var free = F.Text(condBox.transform, "Free", "이미 해금된 직업 - 무료 재전직 가능", 22f, UguiTheme.SuccessGreenBright, TextAlignmentOptions.Left);
            F.Preferred(free, height: 32f);
            view.freeLabel = free;
            view.fragCondRow = CondRow(condBox.transform, "전직 파편", out var fragVal);
            view.fragCondValue = fragVal;
            view.prereqCondRow = CondRow(condBox.transform, "선행 조건", out var preVal);
            view.prereqCondValue = preVal;

            var changeRow = F.Container(root, "ChangeRow");
            F.HLayout(changeRow.gameObject, 0f, null, TextAnchor.MiddleCenter, expandWidth: true);
            view.changeRow = changeRow;

            return Save(root, "Panels/Panel_KAJobDetail.prefab");
        }

        // ══════════════════════════════════════
        //  단독 메시지
        // ══════════════════════════════════════
        internal static GameObject GenerateMessage()
        {
            var root = F.Container(null, "Panel_KAMessage");
            F.VLayout(root.gameObject, 10f);
            var view = root.gameObject.AddComponent<KAMessageView>();
            view.label = Placeholder(root, "-");
            return Save(root, "Panels/Panel_KAMessage.prefab");
        }

        // ══════════════════════════════════════
        //  스탯 비교 행 (반복 아이템)
        // ══════════════════════════════════════
        internal static GameObject GenerateStatCompareRow()
        {
            var root = F.Container(null, "Item_StatCompareRow");
            F.HLayout(root.gameObject, 4f, null, TextAnchor.MiddleLeft);
            F.Preferred(root.gameObject.AddComponent<LayoutElement>(), height: 36f);
            var view = root.gameObject.AddComponent<StatCompareRowView>();

            var c0 = F.Text(root, "Cell0", "-", 20f, new Color(1f, 1f, 1f, 0.85f), TextAlignmentOptions.Left);
            F.Flexible(c0, flexWidth: 1.4f);
            view.cell0 = c0;
            var c1 = F.Text(root, "Cell1", "-", 20f, new Color(1f, 1f, 1f, 0.85f), TextAlignmentOptions.Center);
            F.Flexible(c1, flexWidth: 1f);
            view.cell1 = c1;
            var c2 = F.Text(root, "Cell2", "-", 20f, new Color(1f, 1f, 1f, 0.85f), TextAlignmentOptions.Center);
            F.Flexible(c2, flexWidth: 1f);
            view.cell2 = c2;
            var c3 = F.Text(root, "Cell3", "-", 20f, new Color(1f, 1f, 1f, 0.85f), TextAlignmentOptions.Center);
            F.Flexible(c3, flexWidth: 1f);
            view.cell3 = c3;

            return Save(root, "Items/Item_StatCompareRow.prefab");
        }

        // ══════════════════════════════════════
        //  공통 헬퍼 (컨트롤러 원본 스타일과 동일)
        // ══════════════════════════════════════

        private static (RectTransform root, T view) NewContent<T>(string name) where T : Component
        {
            var root = F.Container(null, name);
            F.VLayout(root.gameObject, 10f);
            var view = root.gameObject.AddComponent<T>();
            return (root, view);
        }

        private static GameObject Save(RectTransform root, string relPath)
        {
            return PrefabGenUtil.SavePrefab(root.gameObject, $"{PrefabGenUtil.PrefabRoot}/{relPath}");
        }

        private static TextMeshProUGUI StatLine(Transform parent, string text)
        {
            var lbl = F.Text(parent, "StatLine", text, 24f, StatLineColor, TextAlignmentOptions.Left);
            F.Preferred(lbl, height: 32f);
            return lbl;
        }

        private static void SectionTitle(Transform parent, string text)
        {
            var lbl = F.Text(parent, "SectionTitle", text, 28f, UguiTheme.TextPrimary, TextAlignmentOptions.Left, bold: true);
            F.Preferred(lbl, height: 40f);
        }

        private static void SubsectionTitle(Transform parent, string text)
        {
            var lbl = F.Text(parent, "SubsectionTitle", text, 24f, new Color(1f, 1f, 1f, 0.90f), TextAlignmentOptions.Left, bold: true);
            F.Preferred(lbl, height: 34f);
        }

        /// <summary>.ka-job-section-title: 골드 + 좌측 4px 보더.</summary>
        private static void JobSectionTitle(Transform parent, string text)
        {
            var row = F.Container(parent, "JobSectionTitle");
            F.HLayout(row.gameObject, 10f, null, TextAnchor.MiddleLeft);
            F.Preferred(row.gameObject.AddComponent<LayoutElement>(), height: 36f);
            var bar = F.Box(row, "Bar", UguiTheme.AccentGoldStrong, rounded: false);
            F.Preferred(bar, width: 4f, height: 26f);
            var lbl = F.Text(row, "Label", text, 24f, UguiTheme.AccentGold, TextAlignmentOptions.Left, bold: true);
            F.Flexible(lbl, flexWidth: 1f);
        }

        private static TextMeshProUGUI Placeholder(Transform parent, string text)
        {
            var lbl = F.Text(parent, "Placeholder", text, 24f, PlaceholderColor, TextAlignmentOptions.Center);
            F.Preferred(lbl, height: 60f);
            return lbl;
        }

        private static TextMeshProUGUI CardLabel(Transform parent, string text, float size, Color color)
        {
            var lbl = F.Text(parent, "CardLabel", text, size, color, TextAlignmentOptions.Center);
            F.Preferred(lbl, height: size + 8f);
            return lbl;
        }

        private static RectTransform CondRow(Transform parent, string name, out TextMeshProUGUI value)
        {
            var row = F.Container(parent, "CondRow");
            F.HLayout(row.gameObject, 10f, null, TextAnchor.MiddleLeft);
            F.Preferred(row.gameObject.AddComponent<LayoutElement>(), height: 34f);
            var nameLbl = F.Text(row, "Name", name, 22f, new Color(1f, 1f, 1f, 0.70f), TextAlignmentOptions.Left);
            F.Preferred(nameLbl, width: 130f, height: 30f);
            value = F.Text(row, "Value", "-", 22f, UguiTheme.TextPrimary, TextAlignmentOptions.Left, bold: true);
            F.Flexible(value, flexWidth: 1f);
            return row;
        }

        private static void MakeEquipGrid(GameObject go)
        {
            var lg = go.AddComponent<GridLayoutGroup>();
            lg.cellSize = new Vector2(160f, 210f);
            lg.spacing = new Vector2(10f, 10f);
            lg.childAlignment = TextAnchor.UpperCenter;
            lg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            lg.constraintCount = 6;
        }

        private static void MakeJobGrid(GameObject go)
        {
            var lg = go.AddComponent<GridLayoutGroup>();
            lg.cellSize = new Vector2(190f, 260f);
            lg.spacing = new Vector2(12f, 12f);
            lg.childAlignment = TextAnchor.UpperCenter;
            lg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            lg.constraintCount = 5;
        }
    }
}
