using System;
using KingdomIdle.Gacha;
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

        [MenuItem("KingdomIdle/UGUI/Validate bronze gacha UI")]
        public static void ValidateBronzeUi()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath);
            var root = UnityEngine.Object.Instantiate(prefab);
            try
            {
                var c = root.GetComponent<GachaPanelView>().authoredContent;
                if (c.featuredIcons?.Length != 3 || c.featuredFrames?.Length != 3 || c.featuredNames?.Length != 3 ||
                    c.ticketHint == null || c.skillRule == null)
                    throw new InvalidOperationException("Bronze gacha preview references are incomplete.");
                int objects = root.GetComponentsInChildren<Transform>(true).Length;
                foreach (string name in new[] { "Equipment", "MageTowerSkill", "Equipment" })
                {
                    var table = AssetDatabase.LoadAssetAtPath<GachaTableSO>("Assets/Gacha/SO/GachaTable_" + name + ".asset");
                    string before = EditorJsonUtility.ToJson(table);
                    GachaPanelController.UpdateFeaturedRewards(c, table);
                    for (int i = 0; i < 3; i++)
                        if (!c.featuredFrames[i].transform.parent.gameObject.activeSelf || c.featuredIcons[i].sprite == null || string.IsNullOrEmpty(c.featuredNames[i].text))
                            throw new InvalidOperationException("A gacha preview slot is blank after tab switch: " + name + " / " + i);
                    if (name == "MageTowerSkill")
                        foreach (var label in c.featuredNames)
                            if (label.text.Contains("에픽") || label.text.Contains("레어") || label.text.Contains("일반"))
                                throw new InvalidOperationException("Mage skills must not have equipment rarity labels.");
                    if (before != EditorJsonUtility.ToJson(table) || objects != root.GetComponentsInChildren<Transform>(true).Length)
                        throw new InvalidOperationException("Preview refresh changed source data or created extra UI objects.");
                }
                Debug.Log("BRONZE GACHA UI VALIDATION PASSED: real equipment/mage/equipment tab data, no blank cells or skill rarities, object reuse, source data preserved");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
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

        /// <summary>작은 대표 이미지와 실데이터 보상 예시를 보여 주고 긴 본문만 스크롤한다.</summary>
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
            bodyLayout.spacing = 14;
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
                c.ticketHint = F.Text(tickets, "Hint", "뽑기권 먼저 사용 · 부족한 횟수만 고대주화 사용", 32, UguiTheme.TextSecondary, TextAlignmentOptions.MidlineLeft);
                Fixed(c.ticketHint, 44);
            }
            c.ticketHint = tickets.Find("Hint").GetComponent<TMP_Text>();
            c.ticketName.fontSize = c.ticketCount.fontSize = 36;
            c.ticketHint.fontSize = 32;
            c.ticketHint.text = "뽑기권 먼저 사용 · 부족한 횟수만 고대주화 사용";
            Fixed(tickets.Find("Balance"), 52);
            Fixed(c.ticketHint, 44);
            Fixed(tickets, 102);
            EnsureFeaturedRewards(c, view.content);
            foreach (var row in new[] { c.messageLabel.transform, heroRect, c.featuredRewards, c.skillRule.transform, c.pityRoot, tickets, c.ratesButton.transform.parent })
            {
                row.SetParent(view.content, false);
                row.SetAsLastSibling();
            }
            var header = view.sheet.Find("Header");
            foreach (var row in new[] { header, view.tabBar, view.scroll.transform, c.costLabel.transform, c.pullRow })
                row.SetAsLastSibling();
            Fixed(header, 104);
            view.title.fontSize = 60;
            Fixed(view.tabBar, 100);
            var hero = F.Preferred(heroRect, height: 150);
            hero.minHeight = 150; hero.flexibleHeight = 0;
            var heroBackground = heroRect.GetComponent<UnityEngine.UI.Image>();
            if (heroBackground == null) heroBackground = heroRect.gameObject.AddComponent<UnityEngine.UI.Image>();
            heroBackground.color = UguiTheme.InfoSurface;
            heroBackground.raycastTarget = false;
            if (heroRect.Find("Frame") == null) F.Frame(heroRect, "Frame", UguiTheme.Bronze);
            var art = c.heroIcon.rectTransform;
            F.AnchorCenter(art, 124, 124);
            art.anchorMin = art.anchorMax = new Vector2(0, .5f);
            art.anchoredPosition = new Vector2(86, 0);
            var name = c.heroTitle.rectTransform;
            F.Stretch(name);
            name.offsetMin = new Vector2(176, 68);
            name.offsetMax = new Vector2(-20, -20);
            c.heroTitle.fontSize = 42;
            c.heroTitle.alignment = TextAlignmentOptions.MidlineLeft;
            c.heroTitle.color = UguiTheme.InfoInk;
            c.heroTitle.fontSharedMaterial = F.Font.material;
            c.heroDescription = heroRect.Find("Description")?.GetComponent<TMP_Text>();
            if (c.heroDescription == null)
                c.heroDescription = F.Text(heroRect, "Description", "장비로 왕국군을 강화하세요", 36, UguiTheme.InfoMuted, TextAlignmentOptions.MidlineLeft);
            F.Stretch(c.heroDescription.rectTransform);
            c.heroDescription.rectTransform.offsetMin = new Vector2(176, 20);
            c.heroDescription.rectTransform.offsetMax = new Vector2(-20, -78);

            Fixed(c.pityRoot, 180);
            c.pityRoot.GetComponent<UnityEngine.UI.VerticalLayoutGroup>().spacing = 4;
            Fixed(c.pityRoot.Find("Progress"), 52);
            c.pityRoot.Find("Progress/Label").GetComponent<TMP_Text>().fontSize = 36;
            c.pityLabel.fontSize = 36;
            Fixed(c.pityFill.transform.parent, 12);
            c.pityRemaining.fontSize = 36;
            Fixed(c.pityRemaining, 46);
            var rule = c.pityRoot.Find("Rule").GetComponent<TMP_Text>();
            rule.text = "에픽 획득 시 누적 횟수가 초기화됩니다.";
            rule.fontSize = 32;
            Fixed(rule, 44);
            Fixed(c.ratesButton.transform.parent, 88);
            Link(c.ratesButton, "확률 정보", TextAlignmentOptions.MidlineLeft);
            Link(c.rewardsButton, "획득 가능 목록", TextAlignmentOptions.MidlineRight);
            c.costLabel.fontSize = 36;
            Fixed(c.costLabel, 48);
            Fixed(c.pullRow, 168);
            var close = F.Preferred(view.closeButton, height: 104);
            close.minHeight = 104;
        }

        private static void Link(UnityEngine.UI.Button button, string text, TextAlignmentOptions alignment)
        {
            Fixed(button, 88);
            foreach (var image in button.GetComponentsInChildren<UnityEngine.UI.Image>(true))
                image.color = Color.clear;
            var label = button.GetComponentInChildren<TMP_Text>(true);
            label.text = text;
            label.fontSize = 36;
            label.fontStyle = FontStyles.Underline;
            label.color = UguiTheme.TextSecondary;
            label.alignment = alignment;
        }

        private static void EnsureFeaturedRewards(GachaTabContentView c, RectTransform parent)
        {
            var old = parent.Find("FeaturedRewards");
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            c.featuredRewards = F.Container(parent, "FeaturedRewards");
            F.VLayout(c.featuredRewards.gameObject, 8);
            Fixed(c.featuredRewards, 244);
            var caption = F.Text(c.featuredRewards, "Caption", "획득 가능 보상 예시", 36, UguiTheme.TextSecondary);
            Fixed(caption, 44);
            var row = Row(c.featuredRewards, "Examples", 188);
            c.featuredIcons = new UnityEngine.UI.Image[3];
            c.featuredFrames = new UnityEngine.UI.Image[3];
            c.featuredNames = new TMP_Text[3];
            for (int i = 0; i < 3; i++)
            {
                var card = F.Box(row, "Reward" + i, UguiTheme.InfoSurface);
                F.Flexible(card, flexWidth: 1);
                Fixed(card, 188);
                c.featuredFrames[i] = F.Frame(card.transform, "Frame", UguiTheme.Bronze);
                c.featuredIcons[i] = F.IconImage(card.transform, "Icon", null, 106, 106);
                F.AnchorCenter(c.featuredIcons[i].rectTransform, 106, 106);
                c.featuredIcons[i].rectTransform.anchoredPosition = new Vector2(0, 28);
                c.featuredNames[i] = F.Text(card.transform, "Name", "", 32, UguiTheme.InfoInk, TextAlignmentOptions.Center);
                F.AnchorBottomStretch(c.featuredNames[i].rectTransform, 12, 52);
                c.featuredNames[i].rectTransform.offsetMin = new Vector2(10, 12);
                c.featuredNames[i].rectTransform.offsetMax = new Vector2(-10, 64);
            }
            c.skillRule = parent.Find("SkillRule")?.GetComponent<TMP_Text>();
            if (c.skillRule == null)
                c.skillRule = F.Text(parent, "SkillRule", "모든 스킬은 같은 획득 확률 · 스킬에 희귀도는 없습니다.", 36, UguiTheme.TextSecondary, TextAlignmentOptions.MidlineLeft, wrap: true);
            Fixed(c.skillRule, 100);
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
