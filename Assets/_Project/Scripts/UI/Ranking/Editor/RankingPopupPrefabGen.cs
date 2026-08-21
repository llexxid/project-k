using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>전투력 랭킹 팝업과 프로필 전투력 버튼 배선을 생성한다.</summary>
    [InitializeOnLoad]
    public static class RankingPopupPrefabGen
    {
        private const string RankingPrefabPath = "Assets/_Project/Prefabs/UI/Popup_Ranking.prefab";
        private static readonly Color CardBg = new(0.13f, 0.10f, 0.075f, 1f);

        static RankingPopupPrefabGen()
        {
            EditorApplication.delayCall += GenerateMissingRankingPrefab;
        }

        /// <summary>신규 스크립트 임포트 직후 누락된 랭킹 프리팹만 한 번 생성한다.</summary>
        private static void GenerateMissingRankingPrefab()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var profilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"{PrefabGenUtil.PrefabRoot}/Popups/Popup_Profile.prefab");
            var profileView = profilePrefab != null ? profilePrefab.GetComponent<ProfilePopupView>() : null;
            bool rankingExists = AssetDatabase.LoadAssetAtPath<GameObject>(RankingPrefabPath) != null;
            bool profileIsWired = profileView != null && profileView.powerButton != null;
            if (rankingExists && profileIsWired) return;
            GenerateRankingAssets();
        }

        [MenuItem("KingdomIdle/UGUI/Generate Ranking Popup", false, 5)]
        public static void GenerateRankingAssets()
        {
            F.Init();
            var catalog = PrefabGenUtil.GetOrCreateCatalog();
            F.Catalog = catalog;

            PatchProfilePowerButton();
            Generate();
            CatalogGen.AssignPrefabs(catalog);
            AssetDatabase.Refresh();
            Debug.Log("[Ranking] 프로필/랭킹 프리팹과 카탈로그 배선을 생성했습니다.");
        }

        /// <summary>기존 프로필 레이아웃은 유지하고 전투력 Pill에 버튼만 추가한다.</summary>
        private static void PatchProfilePowerButton()
        {
            string path = $"{PrefabGenUtil.PrefabRoot}/Popups/Popup_Profile.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var view = root.GetComponent<ProfilePopupView>();
                if (view == null || view.powerLabel == null) return;

                var pillImage = view.powerLabel.transform.parent.parent.GetComponent<Image>();
                if (pillImage == null) return;

                var button = pillImage.GetComponent<Button>();
                if (button == null) button = pillImage.gameObject.AddComponent<Button>();
                button.targetGraphic = pillImage;
                button.transition = Selectable.Transition.ColorTint;
                button.colors = UguiTheme.MakeColorBlock();
                pillImage.raycastTarget = true;
                if (pillImage.GetComponent<PlayClickSfxOnClick>() == null)
                    pillImage.gameObject.AddComponent<PlayClickSfxOnClick>();

                view.powerButton = button;
                EditorUtility.SetDirty(view);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        internal static GameObject Generate()
        {
            var root = F.Root("Popup_Ranking");
            var view = root.gameObject.AddComponent<PowerRankingPopupView>();

            var dim = F.Box(root, "Dim", UguiTheme.DimHeavy, rounded: false, raycast: true);
            F.Stretch(dim.rectTransform);
            view.backdrop = dim.gameObject.AddComponent<Button>();
            view.backdrop.targetGraphic = dim;
            view.backdrop.transition = Selectable.Transition.None;

            var panel = F.PixelPanel(root, "Panel",
                F.Catalog != null ? F.Catalog.kitWindow : null,
                F.FrameGold,
                24f,
                raycast: true,
                baseColor: F.PanelBaseDarker);
            F.AnchorCenter(panel.rectTransform, 920f, 1174f);
            F.VLayout(panel.gameObject, 16f, new RectOffset(38, 38, 26, 34));
            F.CornerBrackets(panel.transform);
            view.panel = panel.rectTransform;

            F.HeaderBanner(panel.transform, "랭킹");
            view.closeButton = MakeCloseButton(panel.transform);

            MakeCurrentPlayerCard(panel.transform, view);
            F.DecoDivider(panel.transform);
            MakeColumnHeader(panel.transform);
            view.rankingList = MakeVirtualizedList(panel.transform);

            root.gameObject.SetActive(false);
            return PrefabGenUtil.SavePrefab(root.gameObject, RankingPrefabPath);
        }

        private static void MakeCurrentPlayerCard(Transform parent, PowerRankingPopupView view)
        {
            var card = F.Box(parent, "CurrentPlayerCard", CardBg, rounded: true);
            F.Preferred(card, height: 200f);
            F.HLayout(card.gameObject, 22f, new RectOffset(24, 24, 18, 18), TextAnchor.MiddleLeft);
            var frame = F.Frame(card.transform, "Frame", UguiTheme.Bronze);
            frame.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            var avatarRing = F.CircleBox(card.transform, "AvatarRing", UguiTheme.Bronze);
            F.Preferred(avatarRing, width: 150f, height: 150f);
            var avatarSocket = F.CircleBox(avatarRing.transform, "Socket", new Color(0.16f, 0.13f, 0.10f, 1f));
            F.AnchorCenter(avatarSocket.rectTransform, 130f, 130f);
            view.avatar = F.IconImage(avatarSocket.transform, "Avatar", UguiGenAssets.IconUser, 94f, 94f);
            F.AnchorCenter(view.avatar.rectTransform, 94f, 94f);

            var info = F.Container(card.transform, "Info");
            F.VLayout(info.gameObject, 12f, null, TextAnchor.MiddleLeft);
            F.Flexible(info, flexWidth: 1f);
            view.nameLabel = F.Text(info, "Name", "Guest", 36f, UguiTheme.Parchment,
                TextAlignmentOptions.Left, bold: true);
            F.Preferred(view.nameLabel, height: 48f);

            var stats = F.Container(info, "Stats");
            F.HLayout(stats.gameObject, 12f, null, TextAnchor.MiddleLeft, expandWidth: true);
            F.Preferred(stats.gameObject.AddComponent<LayoutElement>(), height: 78f);
            view.powerLabel = MakeCurrentStat(stats, UguiGenAssets.IconPower, "전투력", "0");
            view.rankLabel = MakeCurrentStat(stats, UguiGenAssets.IconTrophy, "순위", "-");
        }

        private static TMP_Text MakeCurrentStat(Transform parent, Sprite icon, string caption, string sample)
        {
            var box = F.Box(parent, "Stat", new Color(0.19f, 0.15f, 0.10f, 1f), rounded: true);
            F.Flexible(box, flexWidth: 1f);
            F.HLayout(box.gameObject, 10f, new RectOffset(14, 14, 6, 6), TextAnchor.MiddleLeft);

            var statIcon = F.IconImage(box.transform, "Icon", icon, 42f, 42f);
            F.Preferred(statIcon, width: 42f, height: 42f);
            var labels = F.Container(box.transform, "Labels");
            F.VLayout(labels.gameObject, 0f, null, TextAnchor.MiddleLeft);
            F.Flexible(labels, flexWidth: 1f);
            F.Text(labels, "Caption", caption, 18f, UguiTheme.TextTertiary, TextAlignmentOptions.Left);
            return F.Text(labels, "Value", sample, 28f, UguiTheme.TextPrimary,
                TextAlignmentOptions.Left, bold: true);
        }

        private static void MakeColumnHeader(Transform parent)
        {
            var header = F.Box(parent, "ColumnHeader", new Color(0.20f, 0.15f, 0.09f, 1f), rounded: true);
            F.Preferred(header, height: 52f);
            F.HLayout(header.gameObject, 12f, new RectOffset(12, 20, 0, 0), TextAnchor.MiddleLeft);

            var rank = F.Text(header.transform, "Rank", "순위", 22f, UguiTheme.TextSecondary,
                TextAlignmentOptions.Center, bold: true);
            F.Preferred(rank, width: 112f, height: 48f);
            var player = F.Text(header.transform, "Player", "플레이어", 22f, UguiTheme.TextSecondary,
                TextAlignmentOptions.Left, bold: true);
            F.Flexible(player, flexWidth: 1f);
            var power = F.Text(header.transform, "Power", "전투력", 22f, UguiTheme.TextSecondary,
                TextAlignmentOptions.Right, bold: true);
            F.Preferred(power, width: 200f, height: 48f);
        }

        private static VirtualizedRankingList MakeVirtualizedList(Transform parent)
        {
            var frame = F.Box(parent, "RankingList", new Color(0.08f, 0.065f, 0.05f, 1f), rounded: true, raycast: true);
            F.Preferred(frame, height: 666f);
            var border = F.Frame(frame.transform, "Frame", new Color(UguiTheme.Bronze.r, UguiTheme.Bronze.g, UguiTheme.Bronze.b, 0.7f));
            border.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            var scrollRect = frame.gameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.inertia = true;
            scrollRect.decelerationRate = 0.135f;
            scrollRect.scrollSensitivity = 30f;

            var viewport = F.Container(frame.transform, "Viewport");
            F.Stretch(viewport);
            viewport.offsetMin = new Vector2(12f, 12f);
            viewport.offsetMax = new Vector2(-12f, -12f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0.004f);
            viewportImage.raycastTarget = true;

            var content = F.Container(viewport, "Content");
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;

            var template = MakeRowTemplate(content);
            template.gameObject.SetActive(false);

            scrollRect.viewport = viewport;
            scrollRect.content = content;

            var list = frame.gameObject.AddComponent<VirtualizedRankingList>();
            list.scrollRect = scrollRect;
            list.viewport = viewport;
            list.content = content;
            list.rowTemplate = template;
            return list;
        }

        private static RankingRowView MakeRowTemplate(Transform parent)
        {
            var rowImage = F.Box(parent, "RankingRowTemplate", CardBg, rounded: true);
            var rowRect = rowImage.rectTransform;
            rowRect.anchorMin = new Vector2(0f, 1f);
            rowRect.anchorMax = new Vector2(1f, 1f);
            rowRect.pivot = new Vector2(0.5f, 1f);
            rowRect.anchoredPosition = Vector2.zero;
            rowRect.sizeDelta = new Vector2(0f, VirtualizedRankingList.RowHeight);
            F.HLayout(rowImage.gameObject, 12f, new RectOffset(12, 20, 8, 8), TextAnchor.MiddleLeft);

            var view = rowImage.gameObject.AddComponent<RankingRowView>();
            view.background = rowImage;

            var badge = F.CircleBox(rowImage.transform, "RankBadge", new Color(0.32f, 0.25f, 0.16f, 1f));
            F.Preferred(badge, width: 72f, height: 72f);
            view.rankBadge = badge;
            view.rankLabel = F.Text(badge.transform, "Rank", "1", 28f, UguiTheme.TextPrimary,
                TextAlignmentOptions.Center, bold: true);
            F.Stretch(view.rankLabel.rectTransform);

            var avatar = F.CircleBox(rowImage.transform, "Avatar", new Color(0.20f, 0.17f, 0.12f, 1f));
            F.Preferred(avatar, width: 72f, height: 72f);
            var avatarIcon = F.IconImage(avatar.transform, "Icon", UguiGenAssets.IconUser, 48f, 48f);
            F.AnchorCenter(avatarIcon.rectTransform, 48f, 48f);

            view.nameLabel = F.Text(rowImage.transform, "Name", "모험가01", 27f, UguiTheme.TextPrimary,
                TextAlignmentOptions.Left, bold: true);
            F.Flexible(view.nameLabel, flexWidth: 1f);

            var selfTag = F.Box(rowImage.transform, "SelfTag", UguiTheme.Bronze, rounded: true);
            F.Preferred(selfTag, width: 54f, height: 40f);
            view.selfMarker = F.Text(selfTag.transform, "Label", "나", 21f, UguiTheme.TextPrimary,
                TextAlignmentOptions.Center, bold: true);
            F.Stretch(view.selfMarker.rectTransform);

            view.powerLabel = F.Text(rowImage.transform, "Power", "0", 27f, UguiTheme.AccentGold,
                TextAlignmentOptions.Right, bold: true);
            F.Preferred(view.powerLabel, width: 190f, height: 72f);
            return view;
        }

        private static Button MakeCloseButton(Transform parent)
        {
            var closeImage = F.CircleBox(parent, "BtnClose", new Color(0.62f, 0.24f, 0.24f, 1f), raycast: true);
            var rect = closeImage.rectTransform;
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-10f, -10f);
            rect.sizeDelta = new Vector2(70f, 70f);
            closeImage.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            var button = closeImage.gameObject.AddComponent<Button>();
            button.targetGraphic = closeImage;
            button.transition = Selectable.Transition.ColorTint;
            button.colors = UguiTheme.MakeColorBlock();
            closeImage.gameObject.AddComponent<PlayClickSfxOnClick>();

            if (F.Catalog != null && F.Catalog.iconX != null)
            {
                var icon = F.IconImage(closeImage.transform, "Icon", F.Catalog.iconX, 34f, 34f);
                F.AnchorCenter(icon.rectTransform, 34f, 34f);
            }

            return button;
        }
    }
}
