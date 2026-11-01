using System;
using UnityEditor;
using UnityEngine;
using TMPro;

namespace KingdomIdle.UGUI.Editor
{
    public static class GachaFullScreenPreparation
    {
        private const string PanelPath = "Assets/UGUI/Prefabs/Panels/Panel_Gacha.prefab";
        private const string OldPath = "Assets/UGUI/Prefabs/Panels/Panel_Gacha_Old.prefab";

        [MenuItem("KingdomIdle/UGUI/Prepare full screen gacha")]
        public static void Prepare()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(OldPath) == null)
            {
                string error = AssetDatabase.MoveAsset(PanelPath, OldPath);
                if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
            }
            F.Init();
            F.Catalog = PrefabGenUtil.GetOrCreateCatalog();
            F.Catalog.panelGacha = GeneratePanel();
            F.Catalog.popupGachaRewards = GenerateRewards();
            EditorUtility.SetDirty(F.Catalog);
            AssetDatabase.SaveAssets();
        }

        internal static GameObject GeneratePanel()
        {
            var root = F.Root("Panel_Gacha"); F.Stretch(root);
            root.gameObject.AddComponent<CanvasGroup>();
            root.gameObject.AddComponent<PartyHudSuppressor>();
            var view = root.gameObject.AddComponent<GachaPanelView>();
            view.fullScreen = true;
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
            Fixed(c.pityRoot, 196);
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
            Fixed(rule, 64);

            c.costLabel = F.Text(sheet.transform, "Wallet", "보유 고대주화", 34, UguiTheme.Parchment, TextAlignmentOptions.MidlineRight);
            Fixed(c.costLabel, 56);
            c.pullRow = Row(sheet.transform, "PullRow", 164);
            return PrefabGenUtil.SavePrefab(root.gameObject, PanelPath);
        }

        internal static GameObject GenerateRewards()
        {
            var root = F.Root("Popup_GachaRewards"); F.Stretch(root);
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
            grid.gameObject.AddComponent<ResponsiveGrid>();
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
