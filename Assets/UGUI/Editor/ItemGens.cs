using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>동적 리스트 아이템 프리팹 생성기 (탭 버튼 / 가챠 카드 / 재화 라인).</summary>
    internal static class ItemGens
    {
        /// <summary>
        /// 범용 탭/네비 버튼 (gacha-tab-btn / ka-member-tab / ka-nav-btn 공용).
        /// 아이콘 + 라벨 세로 배치 — 어떤 메뉴인지 한눈에 구분되도록.
        /// </summary>
        internal static GameObject GenerateNavTabButton()
        {
            var bg = F.Box(null, "Item_NavTabButton", new Color(0.16f, 0.17f, 0.23f, 1f), rounded: true, raycast: true);
            var view = bg.gameObject.AddComponent<NavTabButtonView>();
            view.background = bg;

            // 픽셀 버튼 스킨은 적용하지 않는다 (선택 상태 색을 SetSelected가 직접 제어)
            var btn = bg.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.transition = Selectable.Transition.ColorTint;
            btn.colors = UguiTheme.MakeColorBlock();
            bg.gameObject.AddComponent<PlayClickSfxOnClick>();
            view.button = btn;

            // 부모 HorizontalLayout에서 flex-grow 1 + 최소 높이 확보
            var le = bg.gameObject.AddComponent<LayoutElement>();
            le.flexibleWidth = 1f;
            le.minHeight = 96f;

            // 선택 강조 테두리 (평소 투명)
            var frame = F.Frame(bg.transform, "SelectedFrame", new Color(1f, 1f, 1f, 0f));
            frame.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            view.selectedFrame = frame;

            // 세로 배치: 아이콘 + 라벨 (아이콘 크게 — 역할이 잘 보이도록)
            var inner = F.Container(bg.transform, "Inner");
            F.Stretch(inner);
            F.VLayout(inner.gameObject, 4f, new RectOffset(6, 6, 10, 8), TextAnchor.MiddleCenter, expandWidth: true);

            var iconWrap = F.Container(inner, "IconWrap");
            F.Preferred(iconWrap.gameObject.AddComponent<LayoutElement>(), height: 56f);
            var iconImg = F.IconImage(iconWrap, "Icon", null, 52f, 52f);
            F.AnchorCenter(iconImg.rectTransform, 52f, 52f);
            view.icon = iconImg;

            var lbl = F.Text(inner, "Label", "탭", 26f, new Color(1f, 1f, 1f, 0.62f),
                TextAlignmentOptions.Center, bold: true);
            F.Preferred(lbl, height: 32f);
            view.label = lbl;

            return PrefabGenUtil.SavePrefab(bg.gameObject, $"{PrefabGenUtil.PrefabRoot}/Items/Item_NavTabButton.prefab");
        }

        /// <summary>가챠 카드 (미리보기/결과 공용: 등급 프레임 + 아이콘 + 이름 + 서브 라벨).</summary>
        internal static GameObject GenerateGachaCard()
        {
            Image bg;
            if (F.Catalog != null && F.Catalog.kitCard != null)
                bg = F.PixelPanel(null, "Item_GachaCard", F.Catalog.kitCard, F.CardDark, 8f);
            else
                bg = F.Box(null, "Item_GachaCard", new Color(50f / 255f, 50f / 255f, 70f / 255f, 0.80f), rounded: true);
            F.VLayout(bg.gameObject, 4f, new RectOffset(6, 6, 8, 6), TextAnchor.UpperCenter);
            var view = bg.gameObject.AddComponent<GachaCardItemView>();
            view.background = bg;

            // 등급 테두리 (fillCenter=false → 프레임만)
            var frame = F.Frame(bg.transform, "Frame", new Color(1f, 1f, 1f, 0.25f));
            frame.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            view.frame = frame;

            // 아이콘 (64×64 중앙)
            var iconWrap = F.Container(bg.transform, "IconWrap");
            F.Preferred(iconWrap.gameObject.AddComponent<LayoutElement>(), height: 68f);

            var icon = F.Container(iconWrap, "Icon");
            icon.sizeDelta = new Vector2(64f, 64f);
            icon.anchorMin = new Vector2(0.5f, 0.5f);
            icon.anchorMax = new Vector2(0.5f, 0.5f);
            icon.anchoredPosition = Vector2.zero;
            var iconImg = icon.gameObject.AddComponent<Image>();
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;
            view.icon = iconImg;

            var fallback = F.Text(iconWrap, "IconFallback", "", 18f, UguiTheme.TextPrimary,
                TextAlignmentOptions.Center, bold: true, wrap: true);
            F.Stretch(fallback.rectTransform);
            fallback.gameObject.SetActive(false);
            view.iconFallback = fallback;

            // 이름 (22px wrap)
            var name = F.Text(bg.transform, "Name", "", 18f, UguiTheme.TextPrimary,
                TextAlignmentOptions.Center, wrap: true);
            F.Preferred(name, height: 42f);
            view.nameLabel = name;

            // 서브 라벨 (수량/확률)
            var sub = F.Text(bg.transform, "Sub", "", 18f, UguiTheme.AccentGoldStrong,
                TextAlignmentOptions.Center, bold: true);
            F.Preferred(sub, height: 40f);
            view.subLabel = sub;

            return PrefabGenUtil.SavePrefab(bg.gameObject, $"{PrefabGenUtil.PrefabRoot}/Items/Item_GachaCard.prefab");
        }

        /// <summary>재화 드롭다운 한 줄 (dropdown-item).</summary>
        internal static GameObject GenerateCurrencyLine()
        {
            var go = new GameObject("Item_CurrencyLine", typeof(RectTransform));
            go.layer = 5;
            var view = go.AddComponent<CurrencyLineItemView>();

            var lbl = go.AddComponent<TextMeshProUGUI>();
            if (F.Font != null) lbl.font = F.Font;
            lbl.text = "";
            lbl.fontSize = 24f;
            lbl.color = new Color(1f, 1f, 1f, 0.85f);
            lbl.alignment = TextAlignmentOptions.Left;
            lbl.raycastTarget = false;
            view.label = lbl;

            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 34f;

            return PrefabGenUtil.SavePrefab(go, $"{PrefabGenUtil.PrefabRoot}/Items/Item_CurrencyLine.prefab");
        }

        /// <summary>
        /// 뽑기 옵션 버튼 (1회 / 10연). 크고 명확한 버튼 — 아이콘 + 제목 + 비용.
        /// 얇은 텍스트 버튼 대신 사용해 구분·클릭이 쉽도록 한다.
        /// </summary>
        internal static GameObject GenerateGachaPullButton()
        {
            var bg = F.Box(null, "Item_GachaPullButton", UguiTheme.AccentBlue, rounded: true, raycast: true);
            var view = bg.gameObject.AddComponent<GachaPullButtonView>();
            view.background = bg;
            view.button = F.ButtonOn(bg);

            var le = bg.gameObject.AddComponent<LayoutElement>();
            le.flexibleWidth = 1f;
            le.minHeight = 132f;

            F.VLayout(bg.gameObject, 2f, new RectOffset(10, 10, 12, 12), TextAnchor.MiddleCenter, expandWidth: true);

            var iconWrap = F.Container(bg.transform, "IconWrap");
            F.Preferred(iconWrap.gameObject.AddComponent<LayoutElement>(), height: 46f);
            var iconImg = F.IconImage(iconWrap, "Icon", null, 44f, 44f);
            F.AnchorCenter(iconImg.rectTransform, 44f, 44f);
            view.icon = iconImg;

            var title = F.Text(bg.transform, "Title", "뽑기 x1", 30f, UguiTheme.TextPrimary,
                TextAlignmentOptions.Center, bold: true);
            F.Preferred(title, height: 40f);
            view.titleLabel = title;

            var cost = F.Text(bg.transform, "Cost", "0", 24f, UguiTheme.AccentGoldStrong,
                TextAlignmentOptions.Center, bold: true);
            F.Preferred(cost, height: 32f);
            view.costLabel = cost;

            return PrefabGenUtil.SavePrefab(bg.gameObject, $"{PrefabGenUtil.PrefabRoot}/Items/Item_GachaPullButton.prefab");
        }

        /// <summary>확률 요약 알약 (등급별 %). 내용 폭에 맞춰 크기 조절(ContentSizeFitter).</summary>
        internal static GameObject GenerateRatePill()
        {
            var bg = F.Box(null, "Item_RatePill", new Color(0.18f, 0.18f, 0.24f, 1f), rounded: true);
            var view = bg.gameObject.AddComponent<RatePillView>();
            view.background = bg;

            // 라벨 내용 만큼 가로로 늘어나도록 — 부모 행은 childControlWidth=false 로 이 크기를 존중
            var lay = F.HLayout(bg.gameObject, 0f, new RectOffset(18, 18, 8, 8), TextAnchor.MiddleCenter, expandWidth: true);
            lay.childForceExpandHeight = false;
            var fitter = bg.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();
            fitter.horizontalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.Unconstrained;
            var le = bg.gameObject.AddComponent<LayoutElement>();
            le.minHeight = 46f;

            var frame = F.Frame(bg.transform, "Frame", UguiTheme.RarityNormal);
            frame.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            view.frame = frame;

            var lbl = F.Text(bg.transform, "Label", "일반 0.0%", 22f, UguiTheme.RarityNormal,
                TextAlignmentOptions.Center, bold: true);
            view.label = lbl;

            return PrefabGenUtil.SavePrefab(bg.gameObject, $"{PrefabGenUtil.PrefabRoot}/Items/Item_RatePill.prefab");
        }

        /// <summary>장비 그리드 셀 (등급띠 + 아이콘 + 이름 + 스탯 + 장착표시). 왕국군/인벤토리 공용.</summary>
        internal static GameObject GenerateEquipCell()
        {
            var card = F.Box(null, "Item_EquipCell", UguiTheme.SurfaceFaint, rounded: true, raycast: true);
            var view = card.gameObject.AddComponent<EquipCellView>();
            view.button = F.ButtonOn(card, gloss: false);   // 콘텐츠 셀 — 광택 끔(글자 가림 방지)
            view.background = card;
            view.dimGroup = card.gameObject.AddComponent<CanvasGroup>();
            F.VLayout(card.gameObject, 4f, new RectOffset(6, 6, 8, 8), TextAnchor.UpperCenter, expandWidth: true);

            // 상시 등급색 테두리 (아이템 카드 느낌 — Set에서 등급색으로 틴트)
            var rframe = F.Frame(card.transform, "RarityFrame", UguiTheme.RarityNormal);
            rframe.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            view.rarityFrame = rframe;

            // 장착 초록 테두리 (기본 비활성 — 장착 시 등급 테두리 대신 표시)
            var frame = F.Frame(card.transform, "EquippedFrame", new Color(80f / 255f, 200f / 255f, 120f / 255f, 1f));
            frame.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            frame.gameObject.SetActive(false);
            view.equippedFrame = frame;

            // 등급 띠
            var bar = F.Box(card.transform, "RarityBar", UguiTheme.RarityNormal, rounded: false);
            F.Preferred(bar, height: 4f);
            view.rarityBar = bar;

            // 아이콘
            var iconWrap = F.Container(card.transform, "IconWrap");
            F.Preferred(iconWrap.gameObject.AddComponent<LayoutElement>(), height: 64f);
            var icon = F.Container(iconWrap, "Icon");
            icon.sizeDelta = new Vector2(60f, 60f);
            icon.anchorMin = new Vector2(0.5f, 0.5f); icon.anchorMax = new Vector2(0.5f, 0.5f); icon.anchoredPosition = Vector2.zero;
            var iconImg = icon.gameObject.AddComponent<Image>();
            iconImg.preserveAspect = true; iconImg.raycastTarget = false; iconImg.enabled = false;
            view.icon = iconImg;

            view.nameLabel = MakeCellLabel(card.transform, "이름", 20f, UguiTheme.TextPrimary);
            view.subLabel = MakeCellLabel(card.transform, "ATK", 18f, new Color(1f, 1f, 1f, 0.45f));
            view.stateLabel = MakeCellLabel(card.transform, "장착 중", 18f, UguiTheme.SuccessGreenBright);
            view.stateLabel.gameObject.SetActive(false);

            return PrefabGenUtil.SavePrefab(card.gameObject, $"{PrefabGenUtil.PrefabRoot}/Items/Item_EquipCell.prefab");
        }

        /// <summary>전직 카드 (배지 + 이미지 + 이름 + 스탯 + 파편).</summary>
        internal static GameObject GenerateJobCard()
        {
            var card = F.Box(null, "Item_JobCard", new Color(1f, 1f, 1f, 0.07f), rounded: true, raycast: true);
            var view = card.gameObject.AddComponent<JobCardView>();
            view.background = card;
            view.button = F.ButtonOn(card, gloss: false);   // 콘텐츠 셀 — 광택 끔
            F.VLayout(card.gameObject, 6f, new RectOffset(8, 8, 12, 8), TextAnchor.UpperCenter, expandWidth: true);

            var frame = F.Frame(card.transform, "StateFrame", new Color(1f, 230f / 255f, 100f / 255f, 1f));
            frame.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            frame.gameObject.SetActive(false);
            view.stateFrame = frame;

            // 우상단 배지
            var badge = F.Text(card.transform, "Badge", "1차", 16f, new Color(1f, 1f, 1f, 0.55f), TextAlignmentOptions.Right, bold: true);
            var badgeRt = badge.rectTransform;
            badgeRt.anchorMin = new Vector2(1f, 1f); badgeRt.anchorMax = new Vector2(1f, 1f); badgeRt.pivot = new Vector2(1f, 1f);
            badgeRt.anchoredPosition = new Vector2(-6f, -6f); badgeRt.sizeDelta = new Vector2(90f, 22f);
            badge.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            view.badge = badge;

            // 직업 이미지
            var imgWrap = F.Container(card.transform, "ImgWrap");
            F.Preferred(imgWrap.gameObject.AddComponent<LayoutElement>(), height: 84f);
            var img = F.Container(imgWrap, "Image");
            img.sizeDelta = new Vector2(80f, 80f);
            img.anchorMin = new Vector2(0.5f, 0.5f); img.anchorMax = new Vector2(0.5f, 0.5f); img.anchoredPosition = Vector2.zero;
            var imgImg = img.gameObject.AddComponent<Image>();
            imgImg.preserveAspect = true; imgImg.raycastTarget = false; imgImg.enabled = false;
            view.image = imgImg;

            view.nameLabel = MakeCellLabel(card.transform, "직업", 24f, UguiTheme.TextPrimary);
            view.statLabel = MakeCellLabel(card.transform, "HP 0 / ATK 0", 18f, new Color(1f, 1f, 1f, 0.60f));
            view.fragLabel = MakeCellLabel(card.transform, "전직 파편 0/0", 20f, UguiTheme.AccentGoldStrong);
            view.prereqLabel = MakeCellLabel(card.transform, "선행 필요", 18f, UguiTheme.WarnRed);
            view.prereqLabel.gameObject.SetActive(false);

            return PrefabGenUtil.SavePrefab(card.gameObject, $"{PrefabGenUtil.PrefabRoot}/Items/Item_JobCard.prefab");
        }

        /// <summary>육성 강화 카드 (좌측 액센트 + 이름/레벨/효과 + 버튼 행).</summary>
        internal static GameObject GenerateEnhanceCard()
        {
            var card = F.Box(null, "Item_EnhanceCard", UguiTheme.SurfaceFaint, rounded: true);
            var view = card.gameObject.AddComponent<EnhanceCardView>();
            F.VLayout(card.gameObject, 8f, new RectOffset(16, 16, 14, 14), expandWidth: true);

            var accent = F.Box(card.transform, "Accent", new Color(100f / 255f, 180f / 255f, 1f, 0.55f), rounded: false);
            var accentRt = accent.rectTransform;
            accentRt.anchorMin = new Vector2(0f, 0f); accentRt.anchorMax = new Vector2(0f, 1f); accentRt.pivot = new Vector2(0f, 0.5f);
            accentRt.anchoredPosition = Vector2.zero; accentRt.sizeDelta = new Vector2(3f, 0f);
            accent.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            view.accent = accent;

            var header = F.Container(card.transform, "Header");
            F.HLayout(header.gameObject, 10f, null, TextAnchor.MiddleLeft);
            F.Preferred(header.gameObject.AddComponent<LayoutElement>(), height: 40f);
            var nameLbl = F.Text(header, "Name", "공격력", 28f, UguiTheme.TextPrimary, TextAlignmentOptions.Left, bold: true);
            F.Flexible(nameLbl, flexWidth: 1f);
            view.nameLabel = nameLbl;
            var lvLbl = F.Text(header, "Level", "Lv. 0", 22f, new Color(100f / 255f, 180f / 255f, 1f, 1f), TextAlignmentOptions.Right, bold: true);
            F.Preferred(lvLbl, width: 120f);
            view.levelLabel = lvLbl;

            var bonus = F.Text(card.transform, "Bonus", "현재 효과  +0%", 20f, UguiTheme.SuccessGreenBright);
            F.Preferred(bonus, height: 30f);
            view.bonusLabel = bonus;

            var btnRow = F.Container(card.transform, "ButtonRow");
            F.HLayout(btnRow.gameObject, 10f, null, TextAnchor.MiddleCenter, expandWidth: true);
            F.Preferred(btnRow.gameObject.AddComponent<LayoutElement>(), height: 92f);
            view.buttonRow = btnRow;

            return PrefabGenUtil.SavePrefab(card.gameObject, $"{PrefabGenUtil.PrefabRoot}/Items/Item_EnhanceCard.prefab");
        }

        /// <summary>스킬 행 (타입 배지 + 이름 + 설명).</summary>
        internal static GameObject GenerateSkillRow()
        {
            var row = F.Box(null, "Item_SkillRow", new Color(1f, 1f, 1f, 0.04f), rounded: true);
            var view = row.gameObject.AddComponent<SkillRowView>();
            F.HLayout(row.gameObject, 12f, new RectOffset(10, 10, 8, 8), TextAnchor.UpperLeft);
            F.Preferred(row.gameObject.AddComponent<LayoutElement>(), height: 74f);

            var badge = F.Box(row.transform, "TypeBadge", new Color(80f / 255f, 140f / 255f, 220f / 255f, 0.85f), rounded: true);
            var badgeLe = F.Preferred(badge, width: 82f, height: 30f);
            badgeLe.minWidth = 82f;
            view.typeBadge = badge;
            var typeLbl = F.Text(badge.transform, "Type", "액티브", 16f, UguiTheme.TextPrimary, TextAlignmentOptions.Center, bold: true);
            F.Stretch(typeLbl.rectTransform);
            view.typeLabel = typeLbl;

            var col = F.Container(row.transform, "Info");
            F.VLayout(col.gameObject, 4f, null, TextAnchor.UpperLeft, expandWidth: true);
            F.Flexible(col, flexWidth: 1f);
            var nameLbl = F.Text(col, "Name", "스킬명", 22f, UguiTheme.TextPrimary, TextAlignmentOptions.Left, bold: true);
            F.Preferred(nameLbl, height: 30f);
            view.nameLabel = nameLbl;
            var detail = F.Text(col, "Detail", "설명", 19f, new Color(1f, 1f, 1f, 0.55f), TextAlignmentOptions.Left, wrap: true);
            F.Preferred(detail, height: 28f);
            view.detailLabel = detail;

            return PrefabGenUtil.SavePrefab(row.gameObject, $"{PrefabGenUtil.PrefabRoot}/Items/Item_SkillRow.prefab");
        }

        /// <summary>셀/카드용 중앙 정렬 라벨 헬퍼.</summary>
        private static TextMeshProUGUI MakeCellLabel(Transform parent, string text, float size, Color color)
        {
            var lbl = F.Text(parent, "Label", text, size, color, TextAlignmentOptions.Center);
            F.Preferred(lbl, height: size + 8f);
            return lbl;
        }

        /// <summary>범용 액션 버튼 (강화/장착/전직 등). 아이콘 + 라벨.</summary>
        internal static GameObject GenerateActionButton()
        {
            var bg = F.Box(null, "Item_ActionButton", UguiTheme.AccentBlue, rounded: true, raycast: true);
            var view = bg.gameObject.AddComponent<ActionButtonView>();
            view.background = bg;
            view.button = F.ButtonOn(bg);

            var le = bg.gameObject.AddComponent<LayoutElement>();
            le.flexibleWidth = 1f;
            le.minHeight = 84f;

            F.HLayout(bg.gameObject, 8f, new RectOffset(16, 16, 0, 0), TextAnchor.MiddleCenter);

            var iconImg = F.IconImage(bg.transform, "Icon", null, 40f, 40f);
            F.Preferred(iconImg, width: 40f, height: 40f);
            view.icon = iconImg;

            var lbl = F.Text(bg.transform, "Label", "버튼", 28f, UguiTheme.TextPrimary,
                TextAlignmentOptions.Center, bold: true);
            F.Preferred(lbl, height: 36f);
            view.label = lbl;

            return PrefabGenUtil.SavePrefab(bg.gameObject, $"{PrefabGenUtil.PrefabRoot}/Items/Item_ActionButton.prefab");
        }
    }
}
