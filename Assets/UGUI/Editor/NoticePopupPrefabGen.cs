using TMPro;
using UnityEditor;
using UnityEngine;

namespace KingdomIdle.UGUI.Editor
{
    public static class NoticePopupPrefabGen
    {
        internal const string PatchPath = PrefabGenUtil.PrefabRoot + "/Popups/Popup_PatchNotes.prefab";
        internal const string BoardPath = PrefabGenUtil.PrefabRoot + "/Popups/Popup_NoticeBoard.prefab";

        [MenuItem("KingdomIdle/UGUI/Generate Notice Popups")]
        public static void GenerateAssets()
        {
            F.Init();
            F.Catalog = PrefabGenUtil.GetOrCreateCatalog();
            Generate();
            AssetDatabase.SaveAssets();
        }

        internal static void Generate()
        {
            var patch = BuildPopup("Popup_PatchNotes", "업데이트 안내", 0.29f);
            BuildPatch(patch);
            PreservePanelLayout(patch, PatchPath);
            F.Catalog.popupPatchNotes = PrefabGenUtil.SavePrefab(patch.gameObject, PatchPath);

            var board = BuildPopup("Popup_NoticeBoard", "공지사항", 0.10f);
            BuildBoard(board);
            PreservePanelLayout(board, BoardPath);
            F.Catalog.popupNoticeBoard = PrefabGenUtil.SavePrefab(board.gameObject, BoardPath);
            EditorUtility.SetDirty(F.Catalog);
        }

        private static NoticePopupView BuildPopup(string name, string title, float verticalMargin)
        {
            var root = F.Root(name);
            F.Stretch(root);
            var view = root.gameObject.AddComponent<NoticePopupView>();

            var dim = F.Box(root, "Backdrop", UguiTheme.DimHeavy, rounded: false, raycast: true);
            F.Stretch(dim.rectTransform);
            view.backdropButton = dim.gameObject.AddComponent<UnityEngine.UI.Button>();
            view.backdropButton.targetGraphic = dim;
            view.backdropButton.transition = UnityEngine.UI.Selectable.Transition.None;

            var panelColor = F.PanelBaseDarker;
            panelColor.a = 1f;
            var panel = F.PixelPanel(root, "Panel", F.Catalog.kitWindow, F.FrameGold, 22f,
                raycast: true, baseColor: panelColor).rectTransform;
            F.Stretch(panel);
            panel.anchorMin = new Vector2(0.06f, verticalMargin);
            panel.anchorMax = new Vector2(0.94f, 1f - verticalMargin);
            view.panel = panel;
            var frame = F.Frame(panel, "BronzeBorder", UguiTheme.BronzeLight);
            frame.rectTransform.offsetMin = new Vector2(4f, 4f);
            frame.rectTransform.offsetMax = new Vector2(-4f, -4f);

            var header = F.Container(panel, "Header");
            F.AnchorTopStretch(header, 12f, 132f);
            header.offsetMin = new Vector2(32f, header.offsetMin.y);
            header.offsetMax = new Vector2(-20f, header.offsetMax.y);
            var headerBackground = F.PixelPanel(header, "HeaderBackground", F.Catalog.kitTitleBar,
                UguiTheme.RusticSurface, 12f, frameOnly: false);
            F.Stretch(headerBackground.rectTransform);
            var divider = F.Box(panel, "HeaderDivider", UguiTheme.Bronze, rounded: false);
            F.AnchorTopStretch(divider.rectTransform, 144f, 3f);
            divider.rectTransform.offsetMin = new Vector2(32f, divider.rectTransform.offsetMin.y);
            divider.rectTransform.offsetMax = new Vector2(-32f, divider.rectTransform.offsetMax.y);
            var label = F.Text(header, "Title", title, 42f, UguiTheme.AccentGold,
                TextAlignmentOptions.MidlineLeft, bold: true);
            F.Stretch(label.rectTransform);
            label.rectTransform.offsetMax = new Vector2(-146f, 0f);

            var close = F.Box(header, "BtnClose", UguiTheme.RusticSurface, raycast: true);
            view.closeButton = F.ButtonOn(close, gloss: false);
            close.rectTransform.anchorMin = close.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            close.rectTransform.pivot = new Vector2(1f, 0.5f);
            close.rectTransform.anchoredPosition = Vector2.zero;
            close.rectTransform.sizeDelta = new Vector2(132f, 132f);
            var closeLabel = F.Text(close.transform, "Label", "닫기", 32f, UguiTheme.AccentGold,
                TextAlignmentOptions.Center, bold: true);
            F.Stretch(closeLabel.rectTransform);
            return view;
        }

        private static void BuildPatch(NoticePopupView view)
        {
            view.patchToggle = FoldButton(view.panel, "PatchHeading", out view.patchTitle, out view.patchCaret);
            var heading = (RectTransform)view.patchToggle.transform;
            F.AnchorTopStretch(heading, 152f, 132f);
            heading.offsetMin = new Vector2(32f, heading.offsetMin.y);
            heading.offsetMax = new Vector2(-32f, heading.offsetMax.y);

            var bodyColor = UguiTheme.RusticSurfaceDark;
            bodyColor.a = 1f;
            var well = F.Box(view.panel, "BodyWell", bodyColor, rounded: false);
            F.Stretch(well.rectTransform);
            well.rectTransform.offsetMin = new Vector2(32f, 32f);
            well.rectTransform.offsetMax = new Vector2(-32f, -300f);
            view.scroll = F.VScroll(view.panel, "BodyScroll", out var content, padding: new RectOffset(22, 22, 18, 22));
            var scroll = (RectTransform)view.scroll.transform;
            F.Stretch(scroll);
            scroll.offsetMin = new Vector2(32f, 32f);
            scroll.offsetMax = new Vector2(-32f, -300f);
            view.patchBody = BodyText(content);
        }

        private static void BuildBoard(NoticePopupView view)
        {
            var area = F.Container(view.panel, "BoardContent");
            F.Stretch(area);
            area.offsetMin = new Vector2(32f, 32f);
            area.offsetMax = new Vector2(-32f, -152f);
            var bodyColor = UguiTheme.RusticSurfaceDark;
            bodyColor.a = 1f;
            var well = F.Box(view.panel, "BodyWell", bodyColor, rounded: false);
            F.Stretch(well.rectTransform);
            well.rectTransform.offsetMin = area.offsetMin;
            well.rectTransform.offsetMax = area.offsetMax;
            well.transform.SetSiblingIndex(area.GetSiblingIndex());
            F.VLayout(area.gameObject, 12f, new RectOffset(16, 16, 16, 16));

            view.scroll = F.VScroll(area, "NoticeScroll", out var content, spacing: 12f);
            F.Flexible(view.scroll, flexHeight: 1f);
            view.status = F.Text(area, "Status", "", 30f, UguiTheme.TextSecondary,
                TextAlignmentOptions.Center, wrap: true);
            view.status.gameObject.SetActive(false);
            view.retryButton = F.TextButton(area, "BtnRetry", "다시 시도", 32f, UguiTheme.RusticSurface, out _);
            F.Preferred(view.retryButton, height: 132f);
            view.retryButton.gameObject.SetActive(false);

            var template = F.Container(content, "NoticeRowTemplate");
            F.VLayout(template.gameObject, 10f);
            var row = template.gameObject.AddComponent<NoticeRowView>();
            row.toggle = FoldButton(template, "NoticeHeading", out row.title, out row.caret);
            F.Preferred(row.toggle, height: 132f);
            row.title.rectTransform.offsetMin = new Vector2(22f, 48f);
            row.title.rectTransform.offsetMax = new Vector2(-78f, -12f);
            row.date = F.Text(row.toggle.transform, "Date", "", 25f, UguiTheme.TextSecondary,
                TextAlignmentOptions.MidlineLeft);
            F.AnchorBottomStretch(row.date.rectTransform, 10f, 34f);
            row.date.rectTransform.offsetMin = new Vector2(22f, 10f);
            row.date.rectTransform.offsetMax = new Vector2(-78f, 44f);
            row.body = BodyText(template);
            row.body.margin = new Vector4(22f, 8f, 22f, 12f);
            row.body.gameObject.SetActive(false);
            template.gameObject.SetActive(false);
            view.rowTemplate = row;
        }

        private static UnityEngine.UI.Button FoldButton(Transform parent, string name, out TMP_Text title, out TMP_Text caret)
        {
            var bg = F.Box(parent, name, UguiTheme.RusticSurface, raycast: true);
            var button = F.ButtonOn(bg, gloss: false);
            title = F.Text(bg.transform, "Title", "", 34f, UguiTheme.AccentGold,
                TextAlignmentOptions.MidlineLeft, bold: true);
            title.richText = false;
            title.parseCtrlCharacters = false;
            F.Stretch(title.rectTransform);
            title.rectTransform.offsetMin = new Vector2(22f, 0f);
            title.rectTransform.offsetMax = new Vector2(-78f, 0f);

            caret = F.Text(bg.transform, "Caret", "+", 40f, UguiTheme.AccentGold,
                TextAlignmentOptions.Center, bold: true);
            var caretRect = caret.rectTransform;
            caretRect.anchorMin = caretRect.anchorMax = new Vector2(1f, 0.5f);
            caretRect.anchoredPosition = new Vector2(-42f, 0f);
            caretRect.sizeDelta = new Vector2(64f, 64f);
            return button;
        }

        private static void PreservePanelLayout(NoticePopupView generated, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing == null || !existing.TryGetComponent<NoticePopupView>(out var previous) || previous.panel == null) return;
            var source = previous.panel;
            var target = generated.panel;
            target.anchorMin = source.anchorMin;
            target.anchorMax = source.anchorMax;
            target.pivot = source.pivot;
            target.sizeDelta = source.sizeDelta;
            target.anchoredPosition3D = source.anchoredPosition3D;
            target.localScale = source.localScale;
        }

        private static TMP_Text BodyText(Transform parent)
        {
            var label = F.Text(parent, "Body", "", 34f, UguiTheme.TextPrimary, TextAlignmentOptions.TopLeft, wrap: true);
            label.richText = false;
            label.parseCtrlCharacters = false;
            return label;
        }
    }
}
