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

        /// <summary>확률 요약 알약 (등급별 %). 프레임 + 라벨.</summary>
        internal static GameObject GenerateRatePill()
        {
            var bg = F.Box(null, "Item_RatePill", new Color(0.18f, 0.18f, 0.24f, 1f), rounded: true);
            var view = bg.gameObject.AddComponent<RatePillView>();
            view.background = bg;

            F.HLayout(bg.gameObject, 0f, new RectOffset(16, 16, 8, 8), TextAnchor.MiddleCenter);
            var le = bg.gameObject.AddComponent<LayoutElement>();
            le.minHeight = 48f;

            var frame = F.Frame(bg.transform, "Frame", UguiTheme.RarityNormal);
            frame.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            view.frame = frame;

            var lbl = F.Text(bg.transform, "Label", "일반 0%", 22f, UguiTheme.RarityNormal,
                TextAlignmentOptions.Center, bold: true);
            F.Preferred(lbl, height: 30f);
            view.label = lbl;

            return PrefabGenUtil.SavePrefab(bg.gameObject, $"{PrefabGenUtil.PrefabRoot}/Items/Item_RatePill.prefab");
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
