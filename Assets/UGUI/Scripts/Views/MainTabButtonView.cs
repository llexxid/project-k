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

        private static readonly Color TabBgNormal = new Color(1f, 1f, 1f, 0.05f);
        private static readonly Color TabBgSelected = new Color(110f / 255f, 180f / 255f, 1f, 0.22f);

        public void SetSelected(bool selected)
        {
            // 배경은 라운드 박스(반투명) — 선택 시 은은한 파란 하이라이트
            if (background != null)
                background.color = selected ? TabBgSelected : TabBgNormal;

            // 아이콘은 픽셀 스프라이트 — 선택 시 골드 틴트, 평소 흰색
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
