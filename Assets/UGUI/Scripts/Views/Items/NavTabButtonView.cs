using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// 범용 탭/네비 버튼 (gacha-tab-btn / ka-member-tab / ka-nav-btn 공용).
    /// 활성 색상은 컨트롤러가 넘긴다 (뽑기=퍼플, 멤버=그린, 네비=블루).
    /// </summary>
    public sealed class NavTabButtonView : MonoBehaviour
    {
        [SerializeField] internal Button button;
        [SerializeField] internal Image background;
        [SerializeField] internal TMP_Text label;

        public Button Button => button;

        public void SetLabel(string text)
        {
            if (label != null) label.text = text;
        }

        public void SetSelected(bool selected, Color activeBg)
        {
            if (background != null)
            {
                // 픽셀 스프라이트 틴트 — 반투명 대신 불투명 틴트 (비활성=스프라이트 원색)
                background.color = selected ? UguiPixelSkin.Opaque(activeBg) : Color.white;
            }
            if (label != null)
                label.color = selected ? UguiTheme.TextPrimary : new Color(1f, 1f, 1f, 0.60f);
        }
    }
}
