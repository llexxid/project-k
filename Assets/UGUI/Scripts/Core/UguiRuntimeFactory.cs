using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// 런타임 동적 UI 구성 헬퍼.
    /// 기존 UITK 컨트롤러들이 new Label/Button/VisualElement 로 패널 내부를 만들던 패턴의 UGUI 대응물.
    /// 둥근 모서리는 카탈로그의 roundedRect 9-slice 스프라이트를 틴트해서 표현한다.
    /// </summary>
    public static class UguiRuntimeFactory
    {
        private static TMP_FontAsset Font =>
            UIManager.Instance != null && UIManager.Instance.Catalog != null
                ? UIManager.Instance.Catalog.defaultFont
                : null;

        private static Sprite RoundedRect =>
            UIManager.Instance != null && UIManager.Instance.Catalog != null
                ? UIManager.Instance.Catalog.roundedRect
                : null;

        private static Sprite Circle =>
            UIManager.Instance != null && UIManager.Instance.Catalog != null
                ? UIManager.Instance.Catalog.circle
                : null;

        // ═══ 기본 요소 ═══

        public static RectTransform Container(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            go.layer = LayerMask.NameToLayer("UI");
            return rt;
        }

        /// <summary>배경 Image 박스 (기본 raycast 차단 없음 — 장식용).</summary>
        public static Image Box(Transform parent, string name, Color color, bool rounded = true, bool raycastTarget = false)
        {
            var rt = Container(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = raycastTarget;
            if (rounded && RoundedRect != null)
            {
                img.sprite = RoundedRect;
                img.type = Image.Type.Sliced;
            }
            return img;
        }

        public static TMP_Text Label(
            Transform parent, string text, float size, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.Left, bool bold = false, bool wrap = false)
        {
            var rt = Container(parent, "Label");
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (Font != null) tmp.font = Font;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = align;
            tmp.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            tmp.enableWordWrapping = wrap;
            tmp.overflowMode = wrap ? TextOverflowModes.Overflow : TextOverflowModes.Ellipsis;
            tmp.raycastTarget = false;
            return tmp;
        }

        /// <summary>텍스트 버튼 (배경 Image + Button + 자식 TMP 라벨 + 클릭 SFX).</summary>
        public static Button TextButton(
            Transform parent, string text, float fontSize, Color bg, Action onClick,
            out TMP_Text label, bool bold = true, Color? textColor = null)
        {
            var img = Box(parent, $"Btn_{text}", bg, rounded: true, raycastTarget: true);
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.ColorTint;
            btn.colors = UguiTheme.MakeColorBlock();
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            img.gameObject.AddComponent<PlayClickSfxOnClick>();

            label = Label(img.transform, text, fontSize, textColor ?? UguiTheme.TextPrimary,
                TextAlignmentOptions.Center, bold);
            Stretch(label.rectTransform);
            return btn;
        }

        public static Button TextButton(Transform parent, string text, float fontSize, Color bg, Action onClick)
        {
            return TextButton(parent, text, fontSize, bg, onClick, out _);
        }

        /// <summary>아이콘 Image (스프라이트 유지 비율).</summary>
        public static Image Icon(Transform parent, Sprite sprite, float w, float h)
        {
            var rt = Container(parent, "Icon");
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
            SetSize(rt, w, h);
            return img;
        }

        /// <summary>게이지 바 (트랙 + Filled Image). 반환: fill Image.</summary>
        public static Image FilledBar(Transform parent, string name, Color track, Color fill, out Image trackImg)
        {
            trackImg = Box(parent, name, track);
            var fillImg = Box(trackImg.transform, "Fill", fill);
            var rt = fillImg.rectTransform;
            Stretch(rt);
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImg.fillAmount = 0f;
            if (fillImg.sprite == null) fillImg.sprite = RoundedRect;
            return fillImg;
        }

        // ═══ 레이아웃 ═══

        public static VerticalLayoutGroup VerticalLayout(
            GameObject go, float spacing = 0f, RectOffset padding = null,
            TextAnchor align = TextAnchor.UpperLeft, bool childControlHeight = true, bool expandWidth = true)
        {
            var lg = go.AddComponent<VerticalLayoutGroup>();
            lg.spacing = spacing;
            lg.padding = padding ?? new RectOffset();
            lg.childAlignment = align;
            lg.childControlWidth = true;
            lg.childControlHeight = childControlHeight;
            lg.childForceExpandWidth = expandWidth;
            lg.childForceExpandHeight = false;
            return lg;
        }

        public static HorizontalLayoutGroup HorizontalLayout(
            GameObject go, float spacing = 0f, RectOffset padding = null,
            TextAnchor align = TextAnchor.MiddleLeft, bool childControlWidth = true, bool expandWidth = false)
        {
            var lg = go.AddComponent<HorizontalLayoutGroup>();
            lg.spacing = spacing;
            lg.padding = padding ?? new RectOffset();
            lg.childAlignment = align;
            lg.childControlWidth = childControlWidth;
            lg.childControlHeight = true;
            lg.childForceExpandWidth = expandWidth;
            lg.childForceExpandHeight = false;
            return lg;
        }

        public static GridLayoutGroup GridLayout(GameObject go, Vector2 cellSize, Vector2 spacing, RectOffset padding = null)
        {
            var lg = go.AddComponent<GridLayoutGroup>();
            lg.cellSize = cellSize;
            lg.spacing = spacing;
            lg.padding = padding ?? new RectOffset();
            lg.childAlignment = TextAnchor.UpperCenter;
            return lg;
        }

        public static LayoutElement Preferred(Component c, float width = -1f, float height = -1f)
        {
            var le = c.gameObject.GetComponent<LayoutElement>();
            if (le == null) le = c.gameObject.AddComponent<LayoutElement>();
            if (width >= 0f) le.preferredWidth = width;
            if (height >= 0f) le.preferredHeight = height;
            return le;
        }

        public static LayoutElement Flexible(Component c, float flexWidth = 1f, float flexHeight = -1f)
        {
            var le = c.gameObject.GetComponent<LayoutElement>();
            if (le == null) le = c.gameObject.AddComponent<LayoutElement>();
            if (flexWidth >= 0f) le.flexibleWidth = flexWidth;
            if (flexHeight >= 0f) le.flexibleHeight = flexHeight;
            return le;
        }

        /// <summary>
        /// 세로 ScrollRect 생성 (Viewport + RectMask2D + Content).
        /// Content에는 VerticalLayoutGroup + ContentSizeFitter(세로 Preferred)가 붙는다.
        /// </summary>
        public static ScrollRect VerticalScroll(Transform parent, string name, out RectTransform content, float spacing = 10f, RectOffset padding = null)
        {
            var rootRt = Container(parent, name);
            var scroll = rootRt.gameObject.AddComponent<ScrollRect>();

            var viewportRt = Container(rootRt, "Viewport");
            Stretch(viewportRt);
            viewportRt.gameObject.AddComponent<RectMask2D>();
            var viewportImg = viewportRt.gameObject.AddComponent<Image>();
            viewportImg.color = new Color(0f, 0f, 0f, 0.004f);   // 드래그 입력용 레이캐스트 타겟
            viewportImg.raycastTarget = true;

            content = Container(viewportRt, "Content");
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;

            VerticalLayout(content.gameObject, spacing, padding);
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            scroll.viewport = viewportRt;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.decelerationRate = 0.135f;
            scroll.inertia = true;
            scroll.scrollSensitivity = 30f;
            return scroll;
        }

        // ═══ RectTransform 유틸 ═══

        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        public static void SetSize(RectTransform rt, float w, float h)
        {
            rt.sizeDelta = new Vector2(w, h);
        }

        /// <summary>부모 콘텐츠 비우기 (동적 리스트 재구성용).</summary>
        public static void Clear(Transform parent)
        {
            if (parent == null) return;
            for (int i = parent.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(parent.GetChild(i).gameObject);
        }
    }
}
