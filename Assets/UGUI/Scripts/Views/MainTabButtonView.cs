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

        // 상업 게임식 하단 탭: 평소 어두운 슬레이트, 선택 시 선명한 파란 탭
        private static readonly Color TabBgNormal = new Color(0.13f, 0.15f, 0.22f, 1f);
        private static readonly Color TabBgSelected = new Color(0.26f, 0.42f, 0.80f, 1f);

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
