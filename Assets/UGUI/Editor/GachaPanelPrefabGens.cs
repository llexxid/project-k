using System;
using UnityEditor;
using UnityEngine;
using TMPro;

namespace KingdomIdle.UGUI.Editor
{
    public static class GachaPanelPrefabGens
    {
        private const string PanelPath = "Assets/UGUI/Prefabs/Panels/Panel_Gacha.prefab";
        [MenuItem("KingdomIdle/UGUI/Prepare gacha tab panel")]
        public static void Prepare()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            F.Init();
            F.Catalog = PrefabGenUtil.GetOrCreateCatalog();
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath) == null) F.Catalog.panelGacha = GeneratePanel();
            else
            {
                var root = PrefabUtility.LoadPrefabContents(PanelPath);
                try
                {
                    ApplyLayout(root.GetComponent<GachaPanelView>());
                    PrefabUtility.SaveAsPrefabAsset(root, PanelPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
                F.Catalog.panelGacha = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath);
            }
            if (F.Catalog.popupGachaRewards != null)
            {
                string path = AssetDatabase.GetAssetPath(F.Catalog.popupGachaRewards);
                var popup = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (popup.GetComponent<PartyHudSuppressor>() == null) popup.AddComponent<PartyHudSuppressor>();
                    PrefabUtility.SaveAsPrefabAsset(popup, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(popup); }
            }
            EditorUtility.SetDirty(F.Catalog);
            AssetDatabase.SaveAssets();
        }

        internal static GameObject GeneratePanel()
        {
            var root = F.Root("Panel_Gacha"); F.Stretch(root);
            root.gameObject.AddComponent<CanvasGroup>();
            var view = root.gameObject.AddComponent<GachaPanelView>();
            var sheet = F.PixelPanel(root, "Sheet", F.Catalog.kitWindow, F.FrameGold, 24f, raycast: true, baseColor: F.PanelBaseDarker);
            Opaque(sheet);
            F.Stretch(sheet.rectTransform);
            F.VLayout(sheet.gameObject, 20, new RectOffset(44, 44, 32, 40));
            view.sheet = sheet.rectTransform;
            view.content = sheet.rectTransform;
            var c = sheet.gameObject.AddComponent<GachaTabContentView>();
            view.authoredContent = c;

            var header = Row(sheet.transform, "Header", 120);
            view.title = F.Text(header, "Title", "뽑기", 48, UguiTheme.Parchment, TextAlignmentOptions.MidlineLeft, bold: true);
            F.Flexible(view.title, flexWidth: 1);
            view.closeButton = F.TextButton(header, "BtnClose", "닫기", 32, UguiTheme.RusticSurface, out _);
            F.Preferred(view.closeButton, width: 150, height: 120);
            view.tabBar = Row(sheet.transform, "GachaTabBar", 128);

            c.messageLabel = F.Text(sheet.transform, "Message", "", 34, UguiTheme.TextSecondary, TextAlignmentOptions.Center, wrap: true);
            c.messageLabel.gameObject.SetActive(false);
            var hero = F.Container(sheet.transform, "FeaturedArea");
            F.Flexible(hero, flexHeight: 1).minHeight = 200;
            c.heroIcon = F.IconImage(hero, "Art", F.Catalog.iconChest, 220, 220);
            var art = c.heroIcon.rectTransform;
            art.anchorMin = new Vector2(.30f, .34f); art.anchorMax = new Vector2(.70f, .94f);
            art.offsetMin = art.offsetMax = Vector2.zero;
            c.heroIcon.preserveAspect = true;
            c.equipmentArt = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UGUI/UsingAssets/Dungeon_Chest01.png");
            c.mageArt = UguiGenAssets.PixIconWand;
            c.heroIcon.sprite = c.equipmentArt;
            c.heroTitle = F.Text(hero, "Name", "장비 뽑기", 44, UguiTheme.Parchment, TextAlignmentOptions.Center, bold: true);
            F.AnchorBottomStretch(c.heroTitle.rectTransform, 20, 84);

            var details = Row(sheet.transform, "Details", 124);
            c.ratesButton = F.TextButton(details, "BtnRates", "확률표", 34, UguiTheme.RusticSurface, out _);
            c.rewardsButton = F.TextButton(details, "BtnRewards", "획득 가능 보상", 34, UguiTheme.RusticSurface, out _);
            foreach (var button in new[] { c.ratesButton, c.rewardsButton })
            { F.Preferred(button, height: 124); F.Flexible(button, flexWidth: 1); }

            c.pityRoot = F.Container(sheet.transform, "Pity");
            Fixed(c.pityRoot, 204);
            F.VLayout(c.pityRoot.gameObject, 8);
            var pityRow = Row(c.pityRoot, "Progress", 46);
            var pityTitle = F.Text(pityRow, "Label", "에픽 확정", 32, UguiTheme.Parchment, TextAlignmentOptions.MidlineLeft);
            F.Flexible(pityTitle, flexWidth: 1);
            c.pityLabel = F.Text(pityRow, "Count", "0 / 40회", 36, UguiTheme.AccentGoldStrong, TextAlignmentOptions.MidlineRight);
            F.Preferred(c.pityLabel, width: 260);
            var track = F.Box(c.pityRoot, "Track", UguiTheme.RusticSurfaceDark, rounded: false);
            Fixed(track, 14);
            c.pityFill = F.Box(track.transform, "Fill", UguiTheme.AccentGoldStrong, rounded: false);
            c.pityFill.sprite = PrefabGenUtil.GetOrCreateSolidRect();
            c.pityFill.type = UnityEngine.UI.Image.Type.Filled;
            c.pityFill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            c.pityFill.fillAmount = 0;
            F.Stretch(c.pityFill.rectTransform);
            c.pityRemaining = F.Text(c.pityRoot, "Remaining", "에픽 확정까지 40회", 30, UguiTheme.Parchment, TextAlignmentOptions.MidlineLeft);
            Fixed(c.pityRemaining, 42);
            var rule = F.Text(c.pityRoot, "Rule", "에픽 미획득 39회 후 다음 뽑기에서 에픽 확정\n에픽 획득 시 누적 횟수 초기화", 26, UguiTheme.TextSecondary, TextAlignmentOptions.MidlineLeft, wrap: true);
            Fixed(rule, 72);

            c.costLabel = F.Text(sheet.transform, "Wallet", "보유 고대주화", 34, UguiTheme.Parchment, TextAlignmentOptions.MidlineRight);
            Fixed(c.costLabel, 56);
            c.pullRow = Row(sheet.transform, "PullRow", 164);
            ApplyLayout(view);
            return PrefabGenUtil.SavePrefab(root.gameObject, PanelPath);
        }

        /// <summary>미리보기가 본문의 남는 높이를 채우고 짧은 화면에서만 스크롤한다.</summary>
        internal static void ApplyLayout(GachaPanelView view)
        {
            if (view == null || view.sheet == null || view.authoredContent == null)
                throw new InvalidOperationException("Gacha panel references are incomplete.");
            var suppressor = view.GetComponent<PartyHudSuppressor>();
            if (suppressor != null) UnityEngine.Object.DestroyImmediate(suppressor);
            PanelGens.ConfigureSheet(view);
            var layout = view.sheet.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.padding = new RectOffset(30, 30, 20, 24);
            layout.spacing = 12;
            var c = view.authoredContent;
            if (view.scroll == null)
            {
                view.scroll = PanelGens.AddScroll(view.sheet, out var content);
                view.content = content;
            }
            var scrollSize = F.Flexible(view.scroll, flexHeight: 1);
            scrollSize.minHeight = 80;
            scrollSize.preferredHeight = 0;
            // ContentSizeFitter가 본문과 View의 최소 높이 중 큰 값을 사용한다.
            var contentFitter = view.content.GetComponent<UnityEngine.UI.ContentSizeFitter>();
            if (contentFitter == null) contentFitter = view.content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();
            contentFitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            var viewportLayout = view.scroll.viewport.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
            if (viewportLayout != null) UnityEngine.Object.DestroyImmediate(viewportLayout);
            view.content.anchorMin = new Vector2(0, 1);
            view.content.anchorMax = Vector2.one;
            view.content.pivot = new Vector2(.5f, 1);
            view.content.sizeDelta = new Vector2(0, view.content.sizeDelta.y);
            view.content.anchoredPosition = Vector2.zero;
            F.Flexible(view.content, flexHeight: 1).minHeight = 0;
            var bodyLayout = view.content.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
            bodyLayout.spacing = 18;
            bodyLayout.padding = new RectOffset(0, 0, 12, 0);

            var heroRect = (RectTransform)c.heroIcon.transform.parent;
            var tickets = view.content.Find("Tickets") as RectTransform;
            if (tickets == null)
            {
                tickets = F.Container(view.content, "Tickets");
                F.VLayout(tickets.gameObject, 0);
                var ticketRow = Row(tickets, "Balance", 52);
                c.ticketName = F.Text(ticketRow, "Name", "장비 뽑기권", 30, UguiTheme.Parchment, TextAlignmentOptions.MidlineLeft);
                F.Flexible(c.ticketName, flexWidth: 1);
                c.ticketCount = F.Text(ticketRow, "Count", "보유 0장", 32, UguiTheme.Parchment, TextAlignmentOptions.MidlineRight);
                F.Preferred(c.ticketCount, width: 420);
                var hint = F.Text(tickets, "Hint", "뽑기권 사용 준비 중", 24, UguiTheme.TextSecondary, TextAlignmentOptions.MidlineLeft);
                Fixed(hint, 32);
            }
            Fixed(tickets, 84);
            foreach (var row in new[] { c.messageLabel.transform, heroRect, tickets, c.pityRoot, c.ratesButton.transform.parent })
            {
                row.SetParent(view.content, false);
                row.SetAsLastSibling();
            }
            var header = view.sheet.Find("Header");
            foreach (var row in new[] { header, view.tabBar, view.scroll.transform, c.costLabel.transform, c.pullRow })
                row.SetAsLastSibling();
            Fixed(header, 132);
            view.title.fontSize = 40;
            Fixed(view.tabBar, 132);
            var hero = F.Preferred(heroRect, height: 360);
            hero.minHeight = 360; hero.flexibleHeight = 1;
            var heroBackground = heroRect.GetComponent<UnityEngine.UI.Image>();
            if (heroBackground == null) heroBackground = heroRect.gameObject.AddComponent<UnityEngine.UI.Image>();
            heroBackground.color = UguiTheme.RusticSurfaceDark;
            heroBackground.raycastTarget = false;
            if (heroRect.Find("Frame") == null) F.Frame(heroRect, "Frame", UguiTheme.Bronze);
            var art = c.heroIcon.rectTransform;
            F.AnchorCenter(art, 260, 260);
            art.anchoredPosition = new Vector2(0, 50);
            var name = c.heroTitle.rectTransform;
            F.AnchorCenter(name, 800, 64);
            name.anchoredPosition = new Vector2(0, -126);
            c.heroTitle.fontSize = 40;

            Fixed(c.pityRoot, 164);
            c.pityRoot.GetComponent<UnityEngine.UI.VerticalLayoutGroup>().spacing = 4;
            Fixed(c.pityRoot.Find("Progress"), 44);
            Fixed(c.pityFill.transform.parent, 10);
            Fixed(c.pityRemaining, 38);
            var rule = c.pityRoot.Find("Rule").GetComponent<TMP_Text>();
            rule.fontSize = 24;
            Fixed(rule, 60);
            Fixed(c.ratesButton.transform.parent, 132);
            Link(c.ratesButton, "확률 정보", TextAlignmentOptions.MidlineLeft);
            Link(c.rewardsButton, "획득 가능 목록", TextAlignmentOptions.MidlineRight);
            c.costLabel.fontSize = 28;
            Fixed(c.costLabel, 40);
            Fixed(c.pullRow, 190);
            var close = F.Preferred(view.closeButton, height: 132);
            close.minHeight = 132;
        }

        private static void Link(UnityEngine.UI.Button button, string text, TextAlignmentOptions alignment)
        {
            Fixed(button, 132);
            foreach (var image in button.GetComponentsInChildren<UnityEngine.UI.Image>(true))
                image.color = Color.clear;
            var label = button.GetComponentInChildren<TMP_Text>(true);
            label.text = text;
            label.fontSize = 32;
            label.fontStyle = FontStyles.Underline;
            label.color = UguiTheme.TextSecondary;
            label.alignment = alignment;
        }

        internal static GameObject GenerateRewards()
        {
            var root = F.Root("Popup_GachaRewards"); F.Stretch(root);
            root.gameObject.AddComponent<PartyHudSuppressor>();
            var view = root.gameObject.AddComponent<GachaRewardsPopupView>();
            var dim = F.Box(root, "Dim", UguiTheme.DimHeavy, rounded: false, raycast: true);
            F.Stretch(dim.rectTransform);
            var box = F.PixelPanel(root, "Panel", F.Catalog.kitWindow, F.FrameGold, 24, raycast: true, baseColor: F.PanelBaseDarker);
            Opaque(box);
            F.Stretch(box.rectTransform);
            box.rectTransform.anchorMin = new Vector2(0, .10f); box.rectTransform.anchorMax = new Vector2(1, .90f);
            box.rectTransform.offsetMin = new Vector2(32, 0); box.rectTransform.offsetMax = new Vector2(-32, 0);
            F.VLayout(box.gameObject, 20, new RectOffset(32, 32, 24, 24));
            view.box = box.rectTransform;
            var header = Row(box.transform, "Header", 120);
            view.title = F.Text(header, "Title", "획득 가능 보상", 36, UguiTheme.Parchment, TextAlignmentOptions.MidlineLeft, bold: true);
            F.Flexible(view.title, flexWidth: 1);
            view.close = F.TextButton(header, "BtnClose", "닫기", 32, UguiTheme.RusticSurface, out _);
            F.Preferred(view.close, width: 140, height: 120);
            var scroll = F.VScroll(box.transform, "Scroll", out var content, spacing: 12);
            F.Flexible(scroll, flexHeight: 1);
            view.rewards = root.gameObject.AddComponent<GachaTabContentView>();
            var grid = F.Container(content, "RewardGrid");
            var layout = grid.gameObject.AddComponent<UnityEngine.UI.GridLayoutGroup>();
            layout.spacing = new Vector2(16, 16); layout.constraint = UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount; layout.constraintCount = 3;
            var responsive = new SerializedObject(grid.gameObject.AddComponent<ResponsiveGrid>());
            responsive.FindProperty("minimumCellWidth").floatValue = 280;
            responsive.ApplyModifiedPropertiesWithoutUndo();
            view.rewards.rewardGrid = grid;
            return PrefabGenUtil.SavePrefab(root.gameObject, "Assets/UGUI/Prefabs/Popups/Popup_GachaRewards.prefab");
        }

        private static RectTransform Row(Transform parent, string name, float height)
        {
            var row = F.Container(parent, name);
            F.HLayout(row.gameObject, 16, null, TextAnchor.MiddleCenter, expandWidth: false);
            Fixed(row, height);
            return row;
        }
        private static void Fixed(Component component, float height)
        { F.Preferred(component, height: height).minHeight = height; }

        private static void Opaque(UnityEngine.UI.Image image)
        {
            image.sprite = PrefabGenUtil.GetOrCreateSolidRect();
            var color = image.color; color.a = 1; image.color = color;
        }
    }
}
