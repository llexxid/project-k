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
                    // 회색 스프라이트는 요청 색으로 틴트 (불투명 보정 — 반투명 틴트는 픽셀 아트를 죽인다)
                    img.color = Opaque(accent);
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

        /// <summary>
        /// 버튼 픽셀 스프라이트 9-slice 확대 배율.
        /// 버튼 스프라이트는 16×16 / border 2px → 0.3에서 약 6.6px 테두리.
        /// 작은 버튼(≥14px)에서도 코너가 겹치지 않아 십자 아티팩트가 없고,
        /// Point 필터와 함께 선명한 픽셀 테두리가 된다.
        /// </summary>
        private static float PixelPpuMultiplier(Sprite sprite)
        {
            return 0.3f;
        }

        public static Color Opaque(Color c)
        {
            return new Color(c.r, c.g, c.b, 1f);
        }
    }
}
