using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KingdomIdle.UGUI
{
    /// <summary>재화 드롭다운 한 줄 (dropdown-item).</summary>
    public sealed class CurrencyLineItemView : MonoBehaviour
    {
        [SerializeField] internal TMP_Text label;
    }

    /// <summary>
    /// 가챠 카드 (미리보기 그리드 gacha-reward-card / 결과 그리드 gacha-result-card 공용).
    /// subLabel은 미리보기에선 확률, 결과에선 수량 표시에 쓰인다.
    /// </summary>
    public sealed class GachaCardItemView : MonoBehaviour
    {
        [SerializeField] internal Image frame;          // 등급 테두리
        [SerializeField] internal Image background;
        [SerializeField] internal Image icon;
        [SerializeField] internal TMP_Text iconFallback; // 아이콘 없을 때 텍스트 플레이스홀더
        [SerializeField] internal TMP_Text nameLabel;
        [SerializeField] internal TMP_Text subLabel;

        public void SetIcon(Sprite sprite, string fallbackText, Color fallbackColor)
        {
            bool hasSprite = sprite != null;
            if (icon != null)
            {
                icon.gameObject.SetActive(hasSprite);
                icon.sprite = sprite;
            }
            if (iconFallback != null)
            {
                iconFallback.gameObject.SetActive(!hasSprite && !string.IsNullOrEmpty(fallbackText));
                iconFallback.text = fallbackText ?? string.Empty;
                iconFallback.color = fallbackColor;
            }
        }

        public void SetRarityFrame(Color color)
        {
            if (frame != null) frame.color = color;
        }
    }

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
                background.color = selected ? activeBg : UguiTheme.SurfaceLight;
            if (label != null)
                label.color = selected ? UguiTheme.TextPrimary : new Color(1f, 1f, 1f, 0.60f);
        }
    }
}
