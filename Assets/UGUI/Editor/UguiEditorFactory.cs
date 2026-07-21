using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>
    /// 에디터 생성기용 UI 구성 헬퍼 (런타임 UguiRuntimeFactory와 동일 관례).
    /// Init()으로 폰트/공용 스프라이트를 주입한 뒤 사용한다.
    /// DefaultControls는 레거시 Text를 만들기 때문에 사용하지 않는다.
    /// </summary>
    internal static class F
    {
        internal static TMP_FontAsset Font;
        internal static Sprite Rounded;
        internal static Sprite Circle;
        internal static UIViewCatalog Catalog;   // GenerateAll이 공용 에셋 배선 후 주입

        internal static void Init()
        {
            Font = UguiGenAssets.Font;
            Rounded = PrefabGenUtil.GetOrCreateRoundedRect();
            Circle = PrefabGenUtil.GetOrCreateCircle();
        }

        // ═══ 기본 요소 ═══

        internal static RectTransform Root(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5; // UI
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(UguiTheme.RefWidth, UguiTheme.RefHeight);
            return rt;
        }

        internal static RectTransform Container(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        internal static Image Box(Transform parent, string name, Color color, bool rounded = true, bool raycast = false)
        {
            var rt = Container(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = raycast;
            if (rounded && Rounded != null)
            {
                img.sprite = Rounded;
                img.type = Image.Type.Sliced;
            }
            return img;
        }

        /// <summary>테두리만 그리는 프레임 (Sliced + fillCenter=false).</summary>
        internal static Image Frame(Transform parent, string name, Color color)
        {
            var img = Box(parent, name, color);
            img.fillCenter = false;
            Stretch(img.rectTransform);
            return img;
        }

        internal static Image CircleBox(Transform parent, string name, Color color, bool raycast = false)
        {
            var rt = Container(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.raycastTarget = raycast;

            // 픽셀 키트 원형 우선 (판타지 프레임), 없으면 절차 생성 원.
            // 반투명(subtle bg) 요청은 어두운 슬롯 색으로 대체 — 흰 원 방지.
            if (Catalog != null && Catalog.kitEllipse != null)
            {
                img.sprite = Catalog.kitEllipse;
                img.color = color.a < 0.6f ? new Color(0.12f, 0.13f, 0.18f, 1f) : UguiPixelSkin.Opaque(color);
            }
            else
            {
                img.sprite = Circle;
                img.color = color;
            }
            return img;
        }

        // 어두운 패널 기본 배경색 (가독성 확보 — 픽셀 프레임의 불투명 중앙이 UI를 덮지 않도록)
        internal static readonly Color PanelBaseDark = new Color(0.07f, 0.07f, 0.11f, 0.96f);
        internal static readonly Color PanelBaseDarker = new Color(0.05f, 0.05f, 0.08f, 0.98f);

        /// <summary>
        /// 픽셀 키트 9-slice 패널.
        /// 기본(frameOnly=true): 어두운 solid 배경 + 픽셀 프레임 테두리만(중앙 투명) → 가독성 + 픽셀 감성.
        /// frameOnly=false: 스프라이트가 중앙까지 채움(타이틀바 메탈 스트립 등 텍스처가 보여야 할 때).
        /// 반환값은 콘텐츠를 붙일 기본 배경 Image.
        /// </summary>
        internal static Image PixelPanel(Transform parent, string name, Sprite sprite, Color tint, float ppuMultiplier,
            bool raycast = false, bool frameOnly = true, Color? baseColor = null)
        {
            var rt = Container(parent, name);
            var baseImg = rt.gameObject.AddComponent<Image>();
            baseImg.raycastTarget = raycast;

            if (!frameOnly && sprite != null)
            {
                // 스프라이트가 중앙까지 채우는 모드 (메탈 스트립 등)
                baseImg.sprite = sprite;
                baseImg.type = Image.Type.Sliced;
                baseImg.pixelsPerUnitMultiplier = ppuMultiplier;
                baseImg.color = tint;
                return baseImg;
            }

            // 어두운 기본 배경 (라운드 사각형)
            baseImg.sprite = Rounded;
            baseImg.type = Image.Type.Sliced;
            baseImg.color = baseColor ?? PanelBaseDark;

            // 픽셀 프레임 테두리만 오버레이 (fillCenter=false → 중앙 투명)
            if (sprite != null)
            {
                var frame = Container(rt, "Frame");
                Stretch(frame);
                var frameImg = frame.gameObject.AddComponent<Image>();
                frameImg.sprite = sprite;
                frameImg.type = Image.Type.Sliced;
                frameImg.fillCenter = false;
                frameImg.pixelsPerUnitMultiplier = ppuMultiplier;
                frameImg.color = tint;
                frameImg.raycastTarget = false;
                frame.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            }

            return baseImg;
        }

        internal static TextMeshProUGUI Text(
            Transform parent, string name, string text, float size, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.Left, bool bold = false, bool wrap = false)
        {
            var rt = Container(parent, name);
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

        internal static Button ButtonOn(Image target)
        {
            var btn = target.gameObject.AddComponent<Button>();
            btn.targetGraphic = target;
            btn.transition = Selectable.Transition.ColorTint;
            btn.colors = UguiTheme.MakeColorBlock();
            target.raycastTarget = true;
            target.gameObject.AddComponent<PlayClickSfxOnClick>();

            // 픽셀 키트 버튼 스킨 (요청 색 → Blue/Green 전용 스프라이트 또는 Grey 틴트, 눌림/비활성 상태 포함)
            UguiPixelSkin.ApplyButton(target, btn, target.color, Catalog);
            return btn;
        }

        internal static Button TextButton(
            Transform parent, string name, string label, float fontSize, Color bg,
            out TextMeshProUGUI labelText, Color? textColor = null, bool bold = true)
        {
            var img = Box(parent, name, bg, rounded: true, raycast: true);
            var btn = ButtonOn(img);
            labelText = Text(img.transform, "Label", label, fontSize, textColor ?? UguiTheme.TextPrimary,
                TextAlignmentOptions.Center, bold);
            Stretch(labelText.rectTransform);
            return btn;
        }

        /// <summary>투명 풀스크린 클릭 캐처 버튼.</summary>
        internal static Button InvisibleCatcher(Transform parent, string name)
        {
            var img = Box(parent, name, new Color(0f, 0f, 0f, 0.004f), rounded: false, raycast: true);
            Stretch(img.rectTransform);
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.None;
            return btn;
        }

        internal static Image IconImage(Transform parent, string name, Sprite sprite, float w, float h)
        {
            var rt = Container(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.enabled = sprite != null;   // null이면 흰 사각형이 나오므로 비활성
            img.preserveAspect = true;
            img.raycastTarget = false;
            rt.sizeDelta = new Vector2(w, h);
            return img;
        }

        /// <summary>가로 게이지 (트랙 + Filled fill). 픽셀 키트 바 스프라이트 사용. 반환: fill.</summary>
        internal static Image HFillBar(Transform parent, string name, Color track, Color fill, out Image trackImg)
        {
            if (Catalog != null && Catalog.kitBarTrack != null)
            {
                trackImg = PixelPanel(parent, name, Catalog.kitBarTrack, Color.white, 0.2f, frameOnly: false);
            }
            else
            {
                trackImg = Box(parent, name, track);
            }

            var fillSprite = PickFillSprite(fill);
            var fillImg = Box(trackImg.transform, "Fill", fill);
            Stretch(fillImg.rectTransform);
            if (fillSprite != null)
            {
                fillImg.sprite = fillSprite;
                fillImg.color = Color.white;
                // 트랙 프레임 안쪽으로 살짝 인셋
                fillImg.rectTransform.offsetMin = new Vector2(3f, 3f);
                fillImg.rectTransform.offsetMax = new Vector2(-3f, -3f);
            }
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImg.fillAmount = 1f;
            return fillImg;
        }

        /// <summary>요청 색과 가장 가까운 키트 게이지 스프라이트 선택.</summary>
        internal static Sprite PickFillSprite(Color c)
        {
            if (Catalog == null) return null;
            if (c.r > 0.7f && c.g > 0.6f && c.b < 0.5f) return Catalog.kitFillYellow;   // 앰버/골드
            if (c.r > c.g && c.r > c.b) return Catalog.kitFillRed;
            if (c.g > c.r && c.g > c.b) return Catalog.kitFillGreen;
            return Catalog.kitFillBlue;
        }

        /// <summary>세로 마스크 (아래에서 차오름, 쿨다운용).</summary>
        internal static Image VFillMask(Transform parent, string name, Color color)
        {
            var img = Box(parent, name, color, rounded: false);
            Stretch(img.rectTransform);
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Vertical;
            img.fillOrigin = (int)Image.OriginVertical.Bottom;
            img.fillAmount = 0f;
            return img;
        }

        internal static Toggle SimpleToggle(Transform parent, string name, float size)
        {
            Image bg;
            Image check;

            if (Catalog != null && Catalog.kitToggleOff != null && Catalog.kitToggleOn != null)
            {
                // 픽셀 키트 토글: Off 스프라이트 위에 On 스프라이트 오버레이 (스프라이트 채움)
                bg = PixelPanel(parent, name, Catalog.kitToggleOff, Color.white, 0.2f, raycast: true, frameOnly: false);
                bg.rectTransform.sizeDelta = new Vector2(size * 1.6f, size);   // 키트 토글은 가로형

                check = PixelPanel(bg.transform, "Checkmark", Catalog.kitToggleOn, Color.white, 0.2f, frameOnly: false);
                Stretch(check.rectTransform);
            }
            else
            {
                bg = Box(parent, name, UguiTheme.SurfaceMid, rounded: true, raycast: true);
                bg.rectTransform.sizeDelta = new Vector2(size, size);

                check = Box(bg.transform, "Checkmark", new Color(60f / 255f, 130f / 255f, 220f / 255f, 0.85f));
                var checkRt = check.rectTransform;
                Stretch(checkRt);
                checkRt.offsetMin = new Vector2(6f, 6f);
                checkRt.offsetMax = new Vector2(-6f, -6f);
            }

            var toggle = bg.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = bg;
            toggle.graphic = check;
            toggle.isOn = false;
            bg.gameObject.AddComponent<PlayClickSfxOnToggle>();
            return toggle;
        }

        internal static Slider SimpleSlider(Transform parent, string name, Color track, Color fill, bool interactable)
        {
            var rootRt = Container(parent, name);
            var slider = rootRt.gameObject.AddComponent<Slider>();

            Image bg;
            if (Catalog != null && Catalog.kitBarTrack != null)
                bg = PixelPanel(rootRt, "Background", Catalog.kitBarTrack, Color.white, 0.2f, raycast: interactable, frameOnly: false);
            else
                bg = Box(rootRt, "Background", track, rounded: true, raycast: interactable);
            Stretch(bg.rectTransform);

            var fillArea = Container(rootRt, "Fill Area");
            Stretch(fillArea);
            fillArea.offsetMin = new Vector2(3f, 3f);
            fillArea.offsetMax = new Vector2(-3f, -3f);

            var fillSprite = PickFillSprite(fill);
            var fillImg = Box(fillArea, "Fill", fillSprite != null ? Color.white : fill);
            if (fillSprite != null)
            {
                fillImg.sprite = fillSprite;
                fillImg.type = Image.Type.Sliced;
                fillImg.pixelsPerUnitMultiplier = 0.2f;
            }
            var fillRt = fillImg.rectTransform;
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = new Vector2(0f, 1f);
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;

            slider.fillRect = fillRt;
            slider.targetGraphic = bg;
            slider.transition = Selectable.Transition.None;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0f;
            slider.interactable = interactable;

            if (interactable)
            {
                var handleArea = Container(rootRt, "Handle Slide Area");
                Stretch(handleArea);

                Image handle;
                if (Catalog != null && Catalog.kitBarHandle != null)
                {
                    handle = PixelPanel(handleArea, "Handle", Catalog.kitBarHandle, Color.white, 1f, raycast: true, frameOnly: false);
                    handle.type = Image.Type.Simple;
                    handle.preserveAspect = true;
                }
                else
                {
                    handle = CircleBox(handleArea, "Handle", new Color(1f, 1f, 1f, 0.9f), raycast: true);
                }
                handle.rectTransform.sizeDelta = new Vector2(36f, 36f);
                slider.handleRect = handle.rectTransform;
                slider.targetGraphic = handle;
            }

            return slider;
        }

        /// <summary>세로 ScrollRect (Viewport + RectMask2D + VerticalLayout Content).</summary>
        internal static ScrollRect VScroll(Transform parent, string name, out RectTransform content, float spacing = 10f, RectOffset padding = null)
        {
            var rootRt = Container(parent, name);
            var scroll = rootRt.gameObject.AddComponent<ScrollRect>();

            var viewportRt = Container(rootRt, "Viewport");
            Stretch(viewportRt);
            viewportRt.gameObject.AddComponent<RectMask2D>();
            var viewportImg = viewportRt.gameObject.AddComponent<Image>();
            viewportImg.color = new Color(0f, 0f, 0f, 0.004f);
            viewportImg.raycastTarget = true;

            content = Container(viewportRt, "Content");
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;

            VLayout(content.gameObject, spacing, padding);
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

        // ═══ 레이아웃 ═══

        internal static VerticalLayoutGroup VLayout(
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

        internal static HorizontalLayoutGroup HLayout(
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

        internal static LayoutElement Preferred(Component c, float width = -1f, float height = -1f)
        {
            var le = c.gameObject.GetComponent<LayoutElement>();
            if (le == null) le = c.gameObject.AddComponent<LayoutElement>();
            if (width >= 0f) le.preferredWidth = width;
            if (height >= 0f) le.preferredHeight = height;
            return le;
        }

        internal static LayoutElement Flexible(Component c, float flexWidth = -1f, float flexHeight = -1f)
        {
            var le = c.gameObject.GetComponent<LayoutElement>();
            if (le == null) le = c.gameObject.AddComponent<LayoutElement>();
            if (flexWidth >= 0f) le.flexibleWidth = flexWidth;
            if (flexHeight >= 0f) le.flexibleHeight = flexHeight;
            return le;
        }

        // ═══ RectTransform ═══

        internal static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        internal static void AnchorTopStretch(RectTransform rt, float top, float height)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -top);
            rt.sizeDelta = new Vector2(0f, height);
        }

        internal static void AnchorBottomStretch(RectTransform rt, float bottom, float height)
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, bottom);
            rt.sizeDelta = new Vector2(0f, height);
        }

        internal static void AnchorCenter(RectTransform rt, float w, float h, float xOffset = 0f, float yOffset = 0f)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(xOffset, yOffset);
            rt.sizeDelta = new Vector2(w, h);
        }
    }

}
