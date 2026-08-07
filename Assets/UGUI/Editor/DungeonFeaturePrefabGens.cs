using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>
    /// 던전 결과와 환생 팝업을 현재 UGUI 카탈로그 스킨으로 생성한다.
    /// 고정 UI는 프리팹에 저장하고 런타임에는 View의 직렬화 참조만 사용한다.
    /// </summary>
    internal static class DungeonFeaturePrefabGens
    {
        internal static void GenerateAll()
        {
            GenerateDungeonClearPopup();
            GenerateReincarnationPopup();
        }

        private static GameObject GenerateDungeonClearPopup()
        {
            var root = F.Root("Popup_DungeonClear");
            var view = root.gameObject.AddComponent<DungeonClearPopupView>();

            MakeDim(root);

            var panel = MakePanel(root, 820f, 440f);
            view.panel = panel.rectTransform;

            F.HeaderBanner(panel.transform, "던전 클리어", 520f, 94f, 36f);

            view.titleLabel = F.Text(
                panel.transform,
                "LblResult",
                "골드 1스테이지 클리어!",
                38f,
                UguiTheme.AccentGold,
                TextAlignmentOptions.Center,
                bold: true,
                wrap: true);
            F.Preferred(view.titleLabel, height: 72f);

            var guide = F.Text(
                panel.transform,
                "LblGuide",
                "다음 행동을 선택하세요.",
                24f,
                UguiTheme.TextSecondary,
                TextAlignmentOptions.Center);
            F.Preferred(guide, height: 40f);

            var buttons = MakeButtonRow(panel.transform);
            view.exitButton = MakeActionButton(
                buttons,
                "BtnExit",
                "나가기",
                UguiTheme.RusticSurface);
            view.nextButton = MakeActionButton(
                buttons,
                "BtnNext",
                "다음 스테이지",
                new Color(0.18f, 0.42f, 0.68f, 1f));
            view.retryButton = MakeActionButton(
                buttons,
                "BtnRetry",
                "다시하기",
                new Color(0.25f, 0.52f, 0.28f, 1f));

            root.gameObject.SetActive(false);
            return PrefabGenUtil.SavePrefab(
                root.gameObject,
                $"{PrefabGenUtil.PrefabRoot}/Popups/Popup_DungeonClear.prefab");
        }

        private static GameObject GenerateReincarnationPopup()
        {
            var root = F.Root("Popup_Reincarnation");
            var view = root.gameObject.AddComponent<ReincarnationPopupView>();

            view.backdropButton = MakeDim(root);

            var panel = MakePanel(root, 820f, 520f);
            view.panel = panel.rectTransform;

            F.HeaderBanner(panel.transform, "환생", 460f, 94f, 38f);

            view.statusLabel = F.Text(
                panel.transform,
                "LblStatus",
                "환생 가능",
                34f,
                UguiTheme.SuccessGreen,
                TextAlignmentOptions.Center,
                bold: true);
            F.Preferred(view.statusLabel, height: 54f);

            var infoCard = F.Box(
                panel.transform,
                "InfoCard",
                F.CardDark,
                rounded: true);
            F.Preferred(infoCard, height: 170f);
            var frame = F.Frame(
                infoCard.transform,
                "Frame",
                new Color(
                    UguiTheme.Bronze.r,
                    UguiTheme.Bronze.g,
                    UguiTheme.Bronze.b,
                    0.7f));
            frame.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            view.infoLabel = F.Text(
                infoCard.transform,
                "LblInfo",
                "레벨: 15 → <color=#5DE66C>22 (+7)</color>\n" +
                "환생 횟수: 1 → <color=#5DE66C>2 (+1)</color>",
                28f,
                UguiTheme.TextPrimary,
                TextAlignmentOptions.Center,
                wrap: true);
            view.infoLabel.richText = true;
            F.Stretch(view.infoLabel.rectTransform);
            view.infoLabel.rectTransform.offsetMin = new Vector2(22f, 18f);
            view.infoLabel.rectTransform.offsetMax = new Vector2(-22f, -18f);

            var buttons = MakeButtonRow(panel.transform);
            view.cancelButton = MakeActionButton(
                buttons,
                "BtnCancel",
                "취소",
                UguiTheme.RusticSurface);
            view.confirmButton = MakeActionButton(
                buttons,
                "BtnConfirm",
                "환생하기",
                new Color(0.25f, 0.52f, 0.28f, 1f));

            root.gameObject.SetActive(false);
            return PrefabGenUtil.SavePrefab(
                root.gameObject,
                $"{PrefabGenUtil.PrefabRoot}/Popups/Popup_Reincarnation.prefab");
        }

        private static Button MakeDim(RectTransform root)
        {
            var dim = F.Box(
                root,
                "Dim",
                UguiTheme.DimHeavy,
                rounded: false,
                raycast: true);
            F.Stretch(dim.rectTransform);
            var button = dim.gameObject.AddComponent<Button>();
            button.targetGraphic = dim;
            button.transition = Selectable.Transition.None;
            return button;
        }

        private static Image MakePanel(
            RectTransform root,
            float width,
            float height)
        {
            var panel = F.PixelPanel(
                root,
                "Panel",
                F.Catalog != null ? F.Catalog.kitWindow : null,
                F.FrameGold,
                24f,
                raycast: true,
                baseColor: F.PanelBaseDarker);
            F.AnchorCenter(panel.rectTransform, width, height);
            F.VLayout(
                panel.gameObject,
                16f,
                new RectOffset(38, 38, 26, 30),
                TextAnchor.UpperCenter);
            F.CornerBrackets(panel.transform);
            return panel;
        }

        private static RectTransform MakeButtonRow(Transform parent)
        {
            var row = F.Container(parent, "ButtonRow");
            F.HLayout(
                row.gameObject,
                14f,
                null,
                TextAnchor.MiddleCenter,
                expandWidth: true);
            F.Preferred(row.gameObject.AddComponent<LayoutElement>(), height: 86f);
            return row;
        }

        private static Button MakeActionButton(
            Transform parent,
            string name,
            string label,
            Color color)
        {
            var button = F.TextButton(
                parent,
                name,
                label,
                27f,
                color,
                out TextMeshProUGUI labelText,
                UguiTheme.TextPrimary,
                bold: true);
            F.Flexible(button, flexWidth: 1f);
            F.Preferred(button, height: 78f);
            F.Stretch(labelText.rectTransform);
            return button;
        }
    }
}
