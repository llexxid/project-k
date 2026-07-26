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

        // 러스틱 하단 탭: 평소 어두운 다크 우드, 선택 시 밝은 청동/앰버
        private static readonly Color TabBgNormal = UguiTheme.RusticSurface;
        private static readonly Color TabBgSelected = new Color(0.72f, 0.52f, 0.24f, 1f);

        public void SetSelected(bool selected)
        {
            if (background != null)
                background.color = selected ? TabBgSelected : TabBgNormal;

            // 아이콘은 풀컬러 픽토그램이라 틴트하지 않는다(흰색 유지 = 원본색). 선택감은 배경/스케일/인디케이터로.
            if (icon != null)
                icon.color = Color.white;
            if (label != null)
                label.color = selected ? Color.white : new Color(1f, 1f, 1f, 0.75f);
            if (indicator != null)
            {
                var c = UguiTheme.TabIndicator;
                indicator.color = selected ? c : new Color(c.r, c.g, c.b, 0f);
            }
            transform.localScale = selected ? new Vector3(1.04f, 1.04f, 1f) : Vector3.one;
        }
    }
}
