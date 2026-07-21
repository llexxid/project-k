using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>동적 리스트 아이템 프리팹 생성기 (탭 버튼 / 가챠 카드 / 재화 라인).</summary>
    internal static class ItemGens
    {
        /// <summary>범용 탭/네비 버튼 (gacha-tab-btn / ka-member-tab / ka-nav-btn 공용).</summary>
        internal static GameObject GenerateNavTabButton()
        {
            var bg = F.Box(null, "Item_NavTabButton", UguiTheme.SurfaceLight, rounded: true, raycast: true);
            var view = bg.gameObject.AddComponent<NavTabButtonView>();
            view.background = bg;
            view.button = F.ButtonOn(bg);

            // 부모 HorizontalLayout에서 flex-grow 1
            var le = bg.gameObject.AddComponent<LayoutElement>();
            le.flexibleWidth = 1f;

            var lbl = F.Text(bg.transform, "Label", "탭", 24f, new Color(1f, 1f, 1f, 0.60f),
                TextAlignmentOptions.Center, bold: true);
            F.Stretch(lbl.rectTransform);
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
    }
}
