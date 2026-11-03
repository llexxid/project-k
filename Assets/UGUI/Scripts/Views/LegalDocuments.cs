using Scripts.Server.Auth;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomIdle.UGUI
{
    /// <summary>Reads the same version of the game terms from the title and settings, including offline.</summary>
    public static class LegalDocuments
    {
        const string PublishedTerms = "https://ludos-interactive.pages.dev/terms/";
        const string DeletionGuide = "https://kingdomidlegits.github.io/kingdomidle-account-delete/";

        public static void ShowTerms(Transform parent) => Open(parent, true);
        public static void ShowPrivacy(Transform parent) => Open(parent, false);

        static void Open(Transform parent, bool terms)
        {
            if (parent == null) return;
            var host = UIManager.Instance;
            var layer = host != null && host.LayerOverlays != null ? host.LayerOverlays : parent;
            if (layer.Find("LegalDocument") != null) return;
            bool english = TitleLobbyPresentation.SavedEnglish;
            string L(string ko, string en) => english ? en : ko;
            var font = host != null && host.Catalog != null ? host.Catalog.defaultFont : TMP_Settings.defaultFontAsset;
            var overlay = Rect("LegalDocument", layer);
            Stretch(overlay, 0, 0, 0, 0);
            overlay.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, .82f);
            void Close()
            {
                overlay.gameObject.SetActive(false);
                Object.Destroy(overlay.gameObject);
            }
            ModalBackHandler.Bind(overlay.gameObject, Close);

            var frame = Rect("Frame", overlay);
            frame.anchorMin = new Vector2(.055f, .085f);
            frame.anchorMax = new Vector2(.945f, .915f);
            frame.offsetMin = frame.offsetMax = Vector2.zero;
            frame.gameObject.AddComponent<Image>().color = UguiTheme.Bronze;
            var page = Rect("Page", frame);
            Stretch(page, 3, 3, 3, 3);
            page.gameObject.AddComponent<Image>().color = UguiTheme.RusticPanelDeep;

            var heading = Label("Heading", page, font, 40, UguiTheme.BronzeLight);
            heading.text = terms ? L("서비스 이용약관", "Terms of Service") : L("개인정보 처리 안내", "Game Privacy Notice");
            heading.fontStyle = FontStyles.Bold;
            Band(heading.rectTransform, true, 18, 65, 32);
            var subheading = Label("DocumentInfo", page, font, 24, UguiTheme.TextSecondary);
            subheading.text = terms ? L("시행일 ", "Korean original · Effective ") + GameTermsConsent.TermsVersion
                                    : L("왕국군 키우기  ·  계정과 게임 정보", "Kingdom Idle  ·  Account and game data");
            Band(subheading.rectTransform, true, 84, 48, 32);

            var viewport = Rect("Viewport", page);
            Stretch(viewport, 32, 112, 32, 149);
            viewport.gameObject.AddComponent<Image>().color = Color.clear;
            viewport.gameObject.AddComponent<RectMask2D>();
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 42f;
            var body = Label("DocumentBody", viewport, font, 30, UguiTheme.TextPrimary);
            body.alignment = TextAlignmentOptions.TopLeft;
            body.lineSpacing = 9;
            body.textWrappingMode = TextWrappingModes.Normal;
            var path = terms ? "Legal/Terms_" + GameTermsConsent.TermsVersion : "Legal/PrivacyNotice_" + (english ? "en" : "ko");
            var copy = Resources.Load<TextAsset>(path);
            if (copy == null) Debug.LogError("[LegalDocuments] Missing text asset " + path);
            body.text = copy != null ? copy.text : L("문서를 불러오지 못했습니다.", "This document could not be loaded.");
            var content = body.rectTransform;
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(.5f, 1);
            content.sizeDelta = content.anchoredPosition = Vector2.zero;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;

            var actions = Rect("Actions", page);
            Band(actions, false, 16, 76, 24);
            var row = actions.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 16;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = true;
            AddButton("Website", terms ? L("웹에서 전문 보기", "Official website") : L("계정 삭제 안내", "Account deletion"),
                () => Application.OpenURL(terms ? PublishedTerms : DeletionGuide));
            AddButton("Close", L("닫기", "Close"), Close);
            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = 1;
            UITween.PopIn(frame);

            void AddButton(string name, string caption, UnityEngine.Events.UnityAction action)
            {
                var rt = Rect(name, actions);
                var background = rt.gameObject.AddComponent<Image>();
                background.color = UguiTheme.RusticSurface;
                var button = rt.gameObject.AddComponent<Button>();
                button.targetGraphic = background;
                button.colors = UguiTheme.MakeColorBlock();
                var label = Label("Label", rt, font, 28, UguiTheme.TextPrimary);
                label.text = caption;
                label.alignment = TextAlignmentOptions.Center;
                Stretch(label.rectTransform, 6, 2, 6, 2);
                button.onClick.AddListener(action);
            }
        }

        static RectTransform Rect(string name, Transform parent)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.gameObject.layer = parent.gameObject.layer;
            rect.SetParent(parent, false);
            return rect;
        }

        static TextMeshProUGUI Label(string name, Transform parent, TMP_FontAsset font, float size, Color color)
        {
            var label = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSize = size;
            label.color = color;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.raycastTarget = false;
            return label;
        }

        static void Stretch(RectTransform rect, float left, float bottom, float right, float top)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        static void Band(RectTransform rect, bool fromTop, float inset, float height, float sides)
        {
            Stretch(rect, sides, 0, sides, 0);
            rect.SetInsetAndSizeFromParentEdge(fromTop ? RectTransform.Edge.Top : RectTransform.Edge.Bottom, inset, height);
        }
    }
}
