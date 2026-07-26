using UnityEngine;
using UnityEngine.UI;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// 픽셀 아트 키트 버튼 스킨 적용 헬퍼.
    /// 요청 색상을 키트의 Blue/Green/Grey 버킷으로 매핑하고,
    /// 눌림(SpriteSwap Down)/비활성(Inactive) 상태를 함께 배선한다.
    /// 런타임 팩토리와 에디터 생성기가 공용으로 사용한다.
    /// </summary>
    public static class UguiPixelSkin
    {
        /// <summary>
        /// 버튼 Image에 픽셀 키트 스프라이트를 입힌다.
        /// Blue/Green 계열은 전용 스프라이트(무틴트), 그 외 색은 Grey 스프라이트에 색 틴트.
        /// 카탈로그 스프라이트가 없으면 아무것도 하지 않는다(라운드 박스 폴백 유지).
        /// </summary>
        /// <summary>
        /// Layer Lab 버튼 스킨: Button_01_White_Bg(흰색 마스터)를 요청 accent 색으로 틴트한다.
        /// 눌림/하이라이트는 SpriteSwap 대신 ColorTint + UIButtonPress(스케일)로 처리해
        /// 캐주얼 모바일 특유의 촉감 있는 버튼을 만든다.
        /// 반투명 accent(은은한 서페이스 의도)는 어두운 슬레이트 색으로 대체한다.
        /// </summary>
        public static void ApplyButton(Image img, Button btn, Color accent, UIViewCatalog catalog, bool addGloss = true)
        {
            if (img == null || btn == null || catalog == null || catalog.kitBtnGrey == null) return;

            // Layer Lab Button_01 정품 구조를 재현한다:
            //  ① Bg = Button_01_White_Bg — 그라디언트 + 어두운 외곽선이 스프라이트에 이미 구워져 있다(accent로 틴트).
            //  ② InnerBorder1 오버레이 = 안쪽 광택 림(흰색). LL 베이스 프리팹과 동일한 인셋.
            // 프로젝트가 Linear 색공간이라 '흰색 저알파 박스' 오버레이는 밝게 터진다 → 합성 gloss 박스는 쓰지 않는다.
            var bg = catalog.kitBtnGrey;   // = Button_01_White_Bg (Layer Lab)
            img.sprite = bg;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 1f;   // LL: PPU100 + 올바른 보더 → 네이티브 렌더
            img.color = accent.a < 0.5f
                ? new Color(0.24f, 0.26f, 0.34f, 1f)   // 어두운 슬레이트 버튼(은은한 서페이스 의도)
                : Opaque(accent);

            btn.transition = Selectable.Transition.ColorTint;
            btn.colors = UguiTheme.MakeColorBlock();

            // 입체감: 아래로 떨어지는 은은한 드롭 섀도우(검정 → Linear에서 안전).
            var shadow = img.GetComponent<Shadow>();
            if (shadow == null) shadow = img.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
            shadow.effectDistance = new Vector2(0f, -4f);
            shadow.useGraphicAlpha = true;

            // LL 정품 이너 림(InnerBorder1) — 버튼 안쪽에 미묘한 밝은 테두리(광택감). 콘텐츠 셀은 끔.
            if (addGloss && catalog.kitBtnBorder != null && img.transform.Find("InnerRim") == null)
            {
                var g = new GameObject("InnerRim", typeof(RectTransform));
                var grt = (RectTransform)g.transform;
                grt.SetParent(img.transform, false);
                grt.anchorMin = Vector2.zero; grt.anchorMax = Vector2.one;
                grt.offsetMin = new Vector2(4f, 4f); grt.offsetMax = new Vector2(-4f, -7f);   // LL 베이스: sizeDelta(-8,-11), y+1.5
                var gi = g.AddComponent<Image>();
                gi.sprite = catalog.kitBtnBorder;
                gi.type = Image.Type.Sliced;
                gi.color = new Color(1f, 1f, 1f, 0.85f);
                gi.raycastTarget = false;
                g.AddComponent<LayoutElement>().ignoreLayout = true;
                grt.SetAsFirstSibling();   // 라벨/아이콘 뒤에 깔리도록
            }

            if (img.GetComponent<UIButtonPress>() == null)
                img.gameObject.AddComponent<UIButtonPress>();
        }

        /// <summary>
        /// 9-slice 테두리를 '원하는 px 두께'로 렌더링하기 위한 pixelsPerUnitMultiplier를 계산한다.
        ///
        /// Unity는 테두리를 다음 크기로 그린다:
        ///     border(px) * (canvas.referencePixelsPerUnit / sprite.pixelsPerUnit) / multiplier
        ///
        /// 이 프로젝트의 픽셀 키트는 sprite.pixelsPerUnit = 16 이라 기준(100) 대비 6.25배로
        /// 확대된다. 그대로 두면 테두리가 요소보다 커져 코너가 겹치고 십자 모양으로 뭉개진다.
        /// </summary>
        public static float PpuMultiplierForBorder(Sprite sprite, float desiredBorderPx, float referencePpu = 100f)
        {
            if (sprite == null || desiredBorderPx <= 0f) return 1f;

            // Layer Lab(모던) 스프라이트는 PPU≈100 + 올바른 크기의 9-slice 보더 → 네이티브 렌더.
            if (sprite.pixelsPerUnit >= 50f) return 1f;

            var b = sprite.border;
            float border = Mathf.Max(Mathf.Max(b.x, b.y), Mathf.Max(b.z, b.w));
            if (border <= 0f) return 1f;

            float spritePpu = sprite.pixelsPerUnit > 0f ? sprite.pixelsPerUnit : referencePpu;
            return border * (referencePpu / spritePpu) / desiredBorderPx;
        }

        public static Color Opaque(Color c)
        {
            return new Color(c.r, c.g, c.b, 1f);
        }
    }
}
