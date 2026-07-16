using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KingdomIdle.UGUI
{
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
}
