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
        private enum Bucket { Blue, Green, Grey }

        /// <summary>
        /// 버튼 Image에 픽셀 키트 스프라이트를 입힌다.
        /// Blue/Green 계열은 전용 스프라이트(무틴트), 그 외 색은 Grey 스프라이트에 색 틴트.
        /// 카탈로그 스프라이트가 없으면 아무것도 하지 않는다(라운드 박스 폴백 유지).
        /// </summary>
        public static void ApplyButton(Image img, Button btn, Color accent, UIViewCatalog catalog)
        {
            if (img == null || btn == null || catalog == null || catalog.kitBtnGrey == null) return;

            var bucket = PickBucket(accent);

            Sprite normal;
            Sprite down;
            switch (bucket)
            {
                case Bucket.Blue:
                    normal = catalog.kitBtnBlue;
                    down = catalog.kitBtnBlueDown;
                    img.color = Color.white;
                    break;
                case Bucket.Green:
                    normal = catalog.kitBtnGreen;
                    down = catalog.kitBtnGreenDown;
                    img.color = Color.white;
                    break;
                default:
                    normal = catalog.kitBtnGrey;
                    down = catalog.kitBtnGreyDown;
                    // Grey 스프라이트는 밝은 회색(밝기 0.64)이라 흰색 틴트를 주면 '흰 박스'가 된다.
                    // 반투명 서페이스 색(예: white@12%)은 '은은한 배경' 의도이므로 어두운 톤으로 틴트하고,
                    // 불투명한 색만 그 색 그대로 사용한다.
                    img.color = accent.a < 0.5f
                        ? new Color(0.34f, 0.36f, 0.45f, 1f)   // 어두운 슬레이트 버튼
                        : Opaque(accent);
                    break;
            }

            if (normal == null) return;

            img.sprite = normal;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = PixelPpuMultiplier(normal);

            btn.transition = Selectable.Transition.SpriteSwap;
            var state = btn.spriteState;
            state.pressedSprite = down != null ? down : normal;
            state.highlightedSprite = normal;
            state.selectedSprite = normal;
            state.disabledSprite = catalog.kitBtnInactive != null ? catalog.kitBtnInactive : normal;
            btn.spriteState = state;
        }

        /// <summary>순수 흰색/회색빛 계열은 Grey, 파란빛 우세는 Blue, 초록빛 우세는 Green.</summary>
        private static Bucket PickBucket(Color c)
        {
            // 채도 낮음 → Grey (틴트로 색 재현)
            float max = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            float min = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
            float sat = max <= 0f ? 0f : (max - min) / max;
            if (sat < 0.25f) return Bucket.Grey;

            if (c.b > c.r && c.b > c.g) return Bucket.Blue;
            if (c.g > c.r && c.g > c.b) return Bucket.Green;
            return Bucket.Grey;   // 빨강/주황/보라 등은 Grey 틴트
        }

        /// <summary>버튼 픽셀 테두리 목표 두께(기준 해상도 px).</summary>
        private const float ButtonBorderPx = 8f;

        private static float PixelPpuMultiplier(Sprite sprite)
        {
            return PpuMultiplierForBorder(sprite, ButtonBorderPx);
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
