using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>현재 픽셀 UI 토큰으로 오프라인 사냥 결과 팝업을 생성하고 카탈로그에 배선한다.</summary>
    public static class OfflineRewardPopupPrefabGen
    {
        private const string PrefabPath =
            "Assets/UGUI/Prefabs/Popups/Popup_OfflineReward.prefab";
        private const string ChestFallbackPath =
            "Assets/UGUI/UsingAssets/Dungeon_Chest01.png";
        private const string AncientCoinFallbackPath =
            "Assets/UGUI/UsingAssets/Dungeon_Gem01.png";
        private const string WindowFallbackPath =
            "Assets/UGUI/UsingAssets/Dungeon_Window.png";

        [InitializeOnLoadMethod]
        private static void ScheduleMissingAssetGeneration()
        {
            EditorApplication.delayCall += GenerateMissingAsset;
        }

        private static void GenerateMissingAsset()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            UIViewCatalog catalog = AssetDatabase.LoadAssetAtPath<UIViewCatalog>(
                PrefabGenUtil.CatalogPath);
            if (prefab != null && catalog != null &&
                catalog.popupOfflineReward == prefab &&
                HasRequiredRewardIcons(prefab))
            {
                return;
            }

            GenerateAssets();
        }

        [MenuItem("KingdomIdle/UGUI/Generate Offline Reward Popup", false, 6)]
        public static void GenerateAssets()
        {
            F.Init();
            UIViewCatalog catalog = PrefabGenUtil.GetOrCreateCatalog();
            F.Catalog = catalog;

            catalog.popupOfflineReward = Generate();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[OfflineRewardPopup] 프리팹과 카탈로그 배선을 생성했습니다.");
        }

        internal static GameObject Generate()
        {
            RectTransform root = F.Root("Popup_OfflineReward");
            var view = root.gameObject.AddComponent<OfflineRewardPopupView>();

            Image dim = F.Box(
                root,
                "Dim",
                UguiTheme.DimHeavy,
                rounded: false,
                raycast: true);
            F.Stretch(dim.rectTransform);
            view.backdropButton = dim.gameObject.AddComponent<Button>();
            view.backdropButton.targetGraphic = dim;
            view.backdropButton.transition = Selectable.Transition.None;

            Image panel = F.PixelPanel(
                root,
                "Panel",
                ResolveSprite(
                    F.Catalog != null ? F.Catalog.kitWindow : null,
                    WindowFallbackPath),
                F.FrameGold,
                24f,
                raycast: true,
                baseColor: F.PanelBaseDarker);
            F.AnchorCenter(panel.rectTransform, 820f, 850f);
            F.VLayout(
                panel.gameObject,
                16f,
                new RectOffset(38, 38, 28, 32),
                TextAnchor.UpperCenter);
            F.CornerBrackets(panel.transform);
            view.panel = panel.rectTransform;

            TMP_Text title = F.HeaderBanner(
                panel.transform,
                "방치 보상",
                500f,
                100f,
                38f);
            title.color = UguiTheme.AccentGoldStrong;

            RectTransform hero = F.Container(panel.transform, "Hero");
            F.HLayout(
                hero.gameObject,
                24f,
                new RectOffset(24, 24, 12, 12),
                TextAnchor.MiddleLeft);
            F.Preferred(hero.gameObject.AddComponent<LayoutElement>(), height: 150f);

            Image iconSocket = F.CircleBox(
                hero,
                "RewardIconSocket",
                new Color(0.25f, 0.19f, 0.10f, 1f));
            F.Preferred(iconSocket, width: 128f, height: 128f);
            Image chest = F.IconImage(
                iconSocket.transform,
                "ChestIcon",
                ResolveSprite(
                    F.Catalog != null ? F.Catalog.iconChest : null,
                    ChestFallbackPath),
                92f,
                92f);
            F.AnchorCenter(chest.rectTransform, 92f, 92f);

            RectTransform heroText = F.Container(hero, "HeroText");
            F.VLayout(heroText.gameObject, 6f, null, TextAnchor.MiddleLeft);
            F.Flexible(heroText, flexWidth: 1f);
            TMP_Text headline = F.Text(
                heroText,
                "Headline",
                "자리를 비운 동안에도 전투했습니다",
                29f,
                UguiTheme.AccentGold,
                TextAlignmentOptions.Left,
                bold: true,
                wrap: true);
            F.Preferred(headline, height: 70f);
            TMP_Text description = F.Text(
                heroText,
                "Description",
                "서버가 확정한 사냥 보상을 받았습니다.",
                21f,
                UguiTheme.TextSecondary,
                TextAlignmentOptions.Left,
                wrap: true);
            F.Preferred(description, height: 54f);

            RectTransform summary = F.Container(panel.transform, "Summary");
            F.HLayout(
                summary.gameObject,
                12f,
                null,
                TextAnchor.MiddleCenter,
                expandWidth: true);
            F.Preferred(summary.gameObject.AddComponent<LayoutElement>(), height: 64f);
            view.durationLabel = MakeSummaryPill(
                summary,
                "Duration",
                "방치 시간  4시간 32분");
            view.killCountLabel = MakeSummaryPill(
                summary,
                "KillCount",
                "예상 처치  10,880마리");

            Image rewardCard = F.Box(
                panel.transform,
                "RewardCard",
                new Color(0.16f, 0.12f, 0.075f, 1f),
                rounded: true);
            F.VLayout(
                rewardCard.gameObject,
                10f,
                new RectOffset(22, 22, 14, 14),
                TextAnchor.MiddleCenter);
            F.Preferred(rewardCard, height: 190f);
            Image rewardFrame = F.Frame(
                rewardCard.transform,
                "Frame",
                new Color(
                    UguiTheme.Bronze.r,
                    UguiTheme.Bronze.g,
                    UguiTheme.Bronze.b,
                    0.75f));
            rewardFrame.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            MakeRewardRow(
                rewardCard.transform,
                "GoldRow",
                ResolveSprite(
                    F.Catalog != null ? F.Catalog.iconCoin : null,
                    ChestFallbackPath),
                "골드",
                "+128,400",
                UguiTheme.AccentGoldStrong,
                out view.goldRow,
                out view.goldValueLabel);
            MakeRewardRow(
                rewardCard.transform,
                "AncientCoinRow",
                ResolveSprite(
                    F.Catalog != null ? F.Catalog.iconAncientCoin : null,
                    AncientCoinFallbackPath),
                "고대 주화",
                "+120",
                new Color(0.87f, 0.65f, 0.38f, 1f),
                out view.ancientCoinRow,
                out view.ancientCoinValueLabel);

            view.progressLabel = F.Text(
                panel.transform,
                "Progress",
                "성장 결과  Lv.12 · EXP 80\n누적 처치 24,300",
                22f,
                UguiTheme.TextSecondary,
                TextAlignmentOptions.Center,
                wrap: true);
            F.Preferred(view.progressLabel, height: 72f);

            view.confirmButton = F.TextButton(
                panel.transform,
                "BtnConfirm",
                "확인",
                28f,
                UguiTheme.BtnConfirm,
                out _);
            F.Preferred(
                view.confirmButton.gameObject.AddComponent<LayoutElement>(),
                height: 82f);

            root.gameObject.SetActive(false);
            return PrefabGenUtil.SavePrefab(root.gameObject, PrefabPath);
        }

        private static bool HasRequiredRewardIcons(GameObject prefab)
        {
            return HasSprite(prefab.transform.Find(
                       "Panel/Hero/RewardIconSocket/ChestIcon"),
                       ChestFallbackPath) &&
                   HasSprite(prefab.transform.Find(
                       "Panel/RewardCard/GoldRow/Icon"),
                       ChestFallbackPath) &&
                   HasSprite(prefab.transform.Find(
                       "Panel/RewardCard/AncientCoinRow/Icon"),
                       AncientCoinFallbackPath);
        }

        private static bool HasSprite(Transform target, string expectedPath)
        {
            Sprite expected = AssetDatabase.LoadAssetAtPath<Sprite>(expectedPath);
            return expected != null &&
                   target != null &&
                   target.TryGetComponent(out Image image) &&
                   image.sprite == expected;
        }

        private static Sprite ResolveSprite(Sprite primary, string fallbackPath)
        {
            Sprite fallback = AssetDatabase.LoadAssetAtPath<Sprite>(fallbackPath);
            return fallback != null ? fallback : primary;
        }

        private static TMP_Text MakeSummaryPill(
            Transform parent,
            string name,
            string text)
        {
            Image pill = F.Box(
                parent,
                name,
                new Color(0.20f, 0.16f, 0.10f, 1f),
                rounded: true);
            F.Flexible(pill, flexWidth: 1f);
            TMP_Text label = F.Text(
                pill.transform,
                "Label",
                text,
                21f,
                UguiTheme.TextPrimary,
                TextAlignmentOptions.Center,
                bold: true);
            F.Stretch(label.rectTransform);
            return label;
        }

        private static void MakeRewardRow(
            Transform parent,
            string name,
            Sprite icon,
            string label,
            string sampleValue,
            Color valueColor,
            out RectTransform row,
            out TMP_Text valueLabel)
        {
            row = F.Container(parent, name);
            F.HLayout(
                row.gameObject,
                14f,
                new RectOffset(16, 16, 4, 4),
                TextAnchor.MiddleLeft);
            F.Preferred(row.gameObject.AddComponent<LayoutElement>(), height: 72f);

            Image rewardIcon = F.IconImage(row, "Icon", icon, 54f, 54f);
            F.Preferred(rewardIcon, width: 54f, height: 54f);

            TMP_Text nameLabel = F.Text(
                row,
                "Name",
                label,
                25f,
                UguiTheme.TextPrimary,
                TextAlignmentOptions.Left,
                bold: true);
            F.Flexible(nameLabel, flexWidth: 1f);

            valueLabel = F.Text(
                row,
                "Value",
                sampleValue,
                31f,
                valueColor,
                TextAlignmentOptions.Right,
                bold: true);
            F.Preferred(valueLabel, width: 260f, height: 58f);
        }
    }
}
