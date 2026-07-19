using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// 하단 탭 버튼 (아이콘 스프라이트 + 라벨 + 상단 인디케이터).
    /// tab-btn-selected USS 클래스 대응 시각 상태를 코드로 적용한다.
    /// 아이콘은 픽셀 아트 키트 스프라이트 (폰트 글리프 ⚔♞✦ 는 Galmuri11에 없어 이미지로 대체).
    /// </summary>
    public sealed class MainTabButtonView : MonoBehaviour
    {
        [SerializeField] internal Button button;
        [SerializeField] internal Image background;
        [SerializeField] internal Image icon;
        [SerializeField] internal TMP_Text label;
        [SerializeField] internal Image indicator;

        public Button Button => button;

        public void SetSelected(bool selected)
        {
            if (background != null)
            {
                // 픽셀 스프라이트 위 틴트 — 반투명 틴트는 픽셀 아트를 죽이므로 불투명 유지
                background.color = selected
                    ? UguiPixelSkin.Opaque(UguiTheme.TabSelectedIcon)
                    : Color.white;
            }
            if (icon != null)
                icon.color = selected ? UguiPixelSkin.Opaque(UguiTheme.TabSelectedIcon) : Color.white;
            if (label != null)
                label.color = selected ? UguiTheme.TabSelectedLabel : new Color(1f, 1f, 1f, 0.85f);
            if (indicator != null)
            {
                var c = UguiTheme.TabIndicator;
                indicator.color = selected ? c : new Color(c.r, c.g, c.b, 0f);
            }
            transform.localScale = selected ? new Vector3(1.04f, 1.04f, 1f) : Vector3.one;
        }
    }
}
