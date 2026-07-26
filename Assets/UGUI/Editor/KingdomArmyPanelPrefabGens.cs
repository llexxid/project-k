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
            GenerateStatTerm();
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
        private static readonly Color CardBg = new Color(0.13f, 0.10f, 0.075f, 1f);
        private static readonly Color CardBgDeep = new Color(0.10f, 0.08f, 0.06f, 1f);

        internal static GameObject GenerateCharacterSheet()
        {
            var (root, view) = NewContent<KACharacterSheetView>("Panel_KACharacterSheet");

            // ── 스탯 블록(버튼) — 탭하면 상세 방정식 롤다운 ──
            var block = F.Box(root, "StatsBlock", CardBg, rounded: true, raycast: true);
            view.statsButton = F.ButtonOn(block, gloss: false);
            F.HLayout(block.gameObject, 16f, new RectOffset(16, 16, 14, 14), TextAnchor.MiddleLeft);
            F.Preferred(block.gameObject.AddComponent<LayoutElement>(), height: 210f);
            var blockFrame = F.Frame(block.transform, "Frame", UguiTheme.Bronze);
            blockFrame.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            // 초상화(균일): 청동 링 + 소켓(RectMask2D) + 내부 스프라이트(컨트롤러가 고정 스케일)
            var ring = F.CircleBox(block.transform, "PortraitRing", UguiTheme.Bronze, raycast: false);
            F.Preferred(ring, width: 168f, height: 168f);
            var socket = F.CircleBox(ring.transform, "Socket", new Color(0.16f, 0.13f, 0.10f, 1f), raycast: false);
            F.AnchorCenter(socket.rectTransform, 150f, 150f);
            socket.gameObject.AddComponent<RectMask2D>();
            var inner = F.Box(socket.transform, "Inner", Color.white, rounded: false);
            inner.enabled = false;
            inner.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            inner.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            inner.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            view.portraitInner = inner;

            // 정보 열: 직업명 + HP 바(채움) + ATK/이동 칩
            var info = F.Container(block.transform, "Info");
            F.VLayout(info.gameObject, 10f, null, TextAnchor.MiddleLeft);
            F.Flexible(info, flexWidth: 1f);

            view.jobLabel = F.Text(info, "JobLabel", "-", 30f, UguiTheme.Parchment, TextAlignmentOptions.Left, bold: true);
            F.Preferred(view.jobLabel, height: 40f);

            // HP 바 (채움 — ATK/이동과 다른 디자인)
            var hpRow = F.Container(info, "HpRow");
            F.HLayout(hpRow.gameObject, 8f, null, TextAnchor.MiddleLeft);
            F.Preferred(hpRow.gameObject.AddComponent<LayoutElement>(), height: 42f);
            var hpIcon = F.IconImage(hpRow, "HpIcon", UguiGenAssets.IconStatHp, 34f, 34f);
            F.Preferred(hpIcon, width: 34f, height: 34f);
            var hpFill = F.HFillBar(hpRow, "HpBar", F.TrackDark, UguiTheme.HpGreen, out var hpTrack);
            F.Flexible(hpTrack, flexWidth: 1f); F.Preferred(hpTrack, height: 30f);
            hpFill.fillAmount = 1f;
            view.hpFill = hpFill;
            view.hpValueLabel = F.Text(hpTrack.transform, "HpVal", "-", 20f, UguiTheme.TextPrimary, TextAlignmentOptions.Center, bold: true);
            F.Stretch(view.hpValueLabel.rectTransform);

            // ATK / 이동 칩 행
            var chips = F.Container(info, "StatChips");
            F.HLayout(chips.gameObject, 8f, null, TextAnchor.MiddleLeft, childControlWidth: true, expandWidth: true);
            F.Preferred(chips.gameObject.AddComponent<LayoutElement>(), height: 54f);
            view.atkValueLabel = StatChip(chips, UguiGenAssets.IconStatAtk, "-");
            view.moveValueLabel = StatChip(chips, UguiGenAssets.IconStatMove, "-");

            // 펼침 화살표
            view.expandArrow = F.Text(block.transform, "Arrow", "▼", 30f, UguiTheme.AccentGold, TextAlignmentOptions.Center, bold: true).rectTransform;
            F.Preferred((RectTransform)view.expandArrow, width: 40f, height: 40f);

            // ── 상세 롤다운 (기본 접힘) ──
            var detail = F.Box(root, "DetailRoot", CardBgDeep, rounded: true);
            F.VLayout(detail.gameObject, 10f, new RectOffset(18, 18, 14, 16));
            var detailFrame = F.Frame(detail.transform, "Frame", new Color(UguiTheme.Bronze.r, UguiTheme.Bronze.g, UguiTheme.Bronze.b, 0.5f));
            detailFrame.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            view.detailRoot = detail.gameObject;

            SubsectionTitle(detail.transform, "상세 스탯");
            view.atkEqRow = MakeEquationRow(detail.transform, "공격력");
            view.hpEqRow = MakeEquationRow(detail.transform, "체력");

            // term 설명 팝업(롤다운 하단 고정 라인)
            var popup = F.Box(detail.transform, "TermPopup", new Color(0.05f, 0.04f, 0.03f, 0.95f), rounded: true);
            F.HLayout(popup.gameObject, 8f, new RectOffset(14, 14, 8, 8), TextAnchor.MiddleLeft);
            F.Preferred(popup.gameObject.AddComponent<LayoutElement>(), height: 50f);
            var popupFrame = F.Frame(popup.transform, "Frame", new Color(UguiTheme.Bronze.r, UguiTheme.Bronze.g, UguiTheme.Bronze.b, 0.5f));
            popupFrame.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            view.termPopupLabel = F.Text(popup.transform, "Label", "숫자를 눌러 항목을 확인하세요", 22f, UguiTheme.AccentGold, TextAlignmentOptions.Left);
            F.Flexible(view.termPopupLabel, flexWidth: 1f);
            view.termPopup = popup.gameObject;
            view.termPopupRect = popup.rectTransform;

            detail.gameObject.SetActive(false);

            // ── 스킬 (스탯 탭 하단) ──
            SectionTitle(root, "스킬");
            var skills = F.Container(root, "SkillsRoot");
            F.VLayout(skills.gameObject, 8f);
            view.skillsRoot = skills;

            // ── 장착 장비 ──
            SectionTitle(root, "장착 장비");
            view.equippedLabel = StatLine(root, "없음");

            return Save(root, "Panels/Panel_KACharacterSheet.prefab");
        }

        /// <summary>방정식 행: "라벨" + term 컨테이너(HLayout). 반환: term 컨테이너.</summary>
        private static RectTransform MakeEquationRow(Transform parent, string label)
        {
            var row = F.Container(parent, "EqRow");
            F.HLayout(row.gameObject, 8f, null, TextAnchor.MiddleLeft);
            F.Preferred(row.gameObject.AddComponent<LayoutElement>(), height: 54f);
            var lbl = F.Text(row, "Label", label, 24f, UguiTheme.TextSecondary, TextAlignmentOptions.Left, bold: true);
            F.Preferred(lbl, width: 96f, height: 40f);
            var terms = F.Container(row, "Terms");
            F.HLayout(terms.gameObject, 5f, null, TextAnchor.MiddleLeft);
            F.Flexible(terms.gameObject.AddComponent<LayoutElement>(), flexWidth: 1f);
            return terms;
        }

        /// <summary>탭 가능한 방정식 항 프리팹.</summary>
        internal static GameObject GenerateStatTerm()
        {
            var box = F.Box(null, "Item_StatTerm", new Color(0.24f, 0.18f, 0.12f, 1f), rounded: true, raycast: true);
            var view = box.gameObject.AddComponent<StatTermView>();
            view.background = box;
            view.button = F.ButtonOn(box, gloss: false);
            var frame = F.Frame(box.transform, "Frame", new Color(UguiTheme.Bronze.r, UguiTheme.Bronze.g, UguiTheme.Bronze.b, 0.6f));
            frame.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            // 라벨 크기에 맞춰 박스가 늘어나도록 HLayout(패딩) + ContentSizeFitter
            F.HLayout(box.gameObject, 0f, new RectOffset(14, 14, 4, 4), TextAnchor.MiddleCenter, childControlWidth: true, expandWidth: false);
            var fitter = box.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            var le = box.gameObject.AddComponent<LayoutElement>();
            le.minWidth = 52f; le.minHeight = 48f; le.preferredHeight = 48f;
            view.label = F.Text(box.transform, "Label", "0", 24f, UguiTheme.AccentGold, TextAlignmentOptions.Center, bold: true);
            view.label.enableWordWrapping = false;
            view.label.overflowMode = TextOverflowModes.Overflow;
            F.Preferred(view.label, height: 40f);
            return PrefabGenUtil.SavePrefab(box.gameObject, $"{PrefabGenUtil.PrefabRoot}/Items/Item_StatTerm.prefab");
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

            var card = F.Box(equippedGrid, "EquippedCard", CardBg);
            F.VLayout(card.gameObject, 4f, new RectOffset(6, 6, 8, 8), TextAnchor.UpperCenter);

            var frame = F.Frame(card.transform, "EquippedFrame", EquippedGreen);
            frame.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            view.equippedFrame = frame;

            view.equippedSlotLabel = CardLabel(card.transform, "무기", 20f, UguiTheme.Parchment);

            var iconWrap = F.Container(card.transform, "IconWrap");
            F.Preferred(iconWrap.gameObject.AddComponent<LayoutElement>(), height: 72f);
            view.equippedIconWrap = iconWrap;
            view.equippedIcon = ItemSlotCentered(iconWrap, 66f, 54f);

            view.equippedNameLabel = CardLabel(card.transform, "-", 20f, UguiTheme.Parchment);
            view.equippedStatLabel = CardLabel(card.transform, "-", 15f, new Color(1f, 1f, 1f, 0.55f));

            var unBtn = F.TextButton(card.transform, "BtnUnequip", "해제", 20f, UguiTheme.BtnCancel, out _);
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

            var infoBox = F.Box(root, "InfoBox", CardBg);
            F.HLayout(infoBox.gameObject, 16f, new RectOffset(14, 14, 14, 14), TextAnchor.UpperLeft);

            view.icon = ItemSlotLayout(infoBox.transform, 120f, 96f);

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

            var header = F.Box(root, "JobHeader", CardBg);
            F.HLayout(header.gameObject, 16f, new RectOffset(14, 14, 14, 14), TextAnchor.UpperLeft);

            // 초상화 메달리온 (청동 링 + 어두운 원 + preserveAspect 캐릭터 — jobSprite 프레임 편차 흡수)
            var medWrap = F.Container(header.transform, "PortraitWrap");
            var medLe = F.Preferred(medWrap, width: 128f, height: 128f);
            medLe.minWidth = 128f;
            var jdRing = F.CircleBox(medWrap, "Ring", UguiTheme.Bronze);
            F.AnchorCenter(jdRing.rectTransform, 124f, 124f);
            var jdDisc = F.CircleBox(jdRing.transform, "Disc", new Color(0.11f, 0.09f, 0.07f, 1f));
            F.AnchorCenter(jdDisc.rectTransform, 112f, 112f);
            var jdImgRt = F.Container(jdDisc.transform, "Image");
            F.AnchorCenter(jdImgRt, 100f, 100f);
            var jdImg = jdImgRt.gameObject.AddComponent<Image>();
            jdImg.preserveAspect = true; jdImg.raycastTarget = false; jdImg.enabled = false;
            view.image = jdImg;

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

        /// <summary>스탯 칩: 어두운 알약 + 아이콘 + 값. 반환: 값 라벨(컨트롤러가 값 세팅).</summary>
        private static TextMeshProUGUI StatChip(Transform parent, Sprite icon, string value)
        {
            var chip = F.Box(parent, "StatChip", new Color(0.04f, 0.05f, 0.08f, 0.92f), rounded: true);
            F.HLayout(chip.gameObject, 6f, new RectOffset(10, 12, 0, 0), TextAnchor.MiddleLeft);
            F.Flexible(chip, flexWidth: 1f);
            F.Preferred(chip, height: 52f);
            // 정품 LL 이너 림으로 칩에 입체감
            if (F.Catalog != null && F.Catalog.kitBtnBorder != null)
            {
                var rim = F.Box(chip.transform, "InnerRim", new Color(1f, 1f, 1f, 0.6f));
                rim.sprite = F.Catalog.kitBtnBorder; rim.type = Image.Type.Sliced;
                var rrt = rim.rectTransform; rrt.anchorMin = Vector2.zero; rrt.anchorMax = Vector2.one;
                rrt.offsetMin = new Vector2(3f, 3f); rrt.offsetMax = new Vector2(-3f, -5f);
                rim.raycastTarget = false;
                rim.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            }
            var ic = F.IconImage(chip.transform, "Icon", icon, 34f, 34f);
            F.Preferred(ic, width: 34f, height: 34f);
            var val = F.Text(chip.transform, "Value", value, 24f, UguiTheme.TextPrimary, TextAlignmentOptions.Left, bold: true);
            F.Flexible(val, flexWidth: 1f);
            return val;
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

        /// <summary>어두운 아이템 슬롯(청동 프레임) 배경 이미지에 preserveAspect 아이콘을 얹는다.</summary>
        private static Image FillItemSlot(Image bg, float iconSize)
        {
            var frame = F.Frame(bg.transform, "SlotFrame", new Color(UguiTheme.Bronze.r, UguiTheme.Bronze.g, UguiTheme.Bronze.b, 0.85f));
            frame.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var iconRt = F.Container(bg.transform, "Icon");
            F.AnchorCenter(iconRt, iconSize, iconSize);
            var icon = iconRt.gameObject.AddComponent<Image>();
            icon.preserveAspect = true; icon.raycastTarget = false; icon.enabled = false;
            return icon;
        }

        /// <summary>부모(레이아웃 없음) 중앙에 배치되는 아이템 슬롯. 반환: 컨트롤러가 스프라이트를 세팅할 아이콘.</summary>
        private static Image ItemSlotCentered(Transform parent, float size, float iconSize)
        {
            var bg = F.Box(parent, "SlotBg", new Color(0.10f, 0.085f, 0.065f, 1f), rounded: true);
            F.AnchorCenter(bg.rectTransform, size, size);
            return FillItemSlot(bg, iconSize);
        }

        /// <summary>HLayout 자식으로 들어가는 고정폭 아이템 슬롯. 반환: 아이콘.</summary>
        private static Image ItemSlotLayout(Transform parent, float size, float iconSize)
        {
            var bg = F.Box(parent, "SlotBg", new Color(0.10f, 0.085f, 0.065f, 1f), rounded: true);
            var le = F.Preferred(bg, width: size, height: size);
            le.minWidth = size;
            return FillItemSlot(bg, iconSize);
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
