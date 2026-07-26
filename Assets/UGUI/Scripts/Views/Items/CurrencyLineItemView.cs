using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KingdomIdle.UGUI
{
    /// <summary>재화 드롭다운 한 줄 (dropdown-item): 아이콘 + 이름 + 값(우측).</summary>
    public sealed class CurrencyLineItemView : MonoBehaviour
    {
        [SerializeField] internal Image icon;
        [SerializeField] internal TMP_Text label;       // 재화 이름 (또는 그룹 제목)
        [SerializeField] internal TMP_Text valueLabel;  // 보유량 (우측 정렬, 골드색)

        /// <summary>한 줄 표시 갱신. 제목이면 아이콘/값을 숨기고 이름만 굵게 표시.</summary>
        public void Set(Sprite iconSprite, string name, string value, bool isTitle)
        {
            if (icon != null)
            {
                bool showIcon = !isTitle && iconSprite != null;
                icon.sprite = iconSprite;
                icon.enabled = showIcon;
                icon.gameObject.SetActive(showIcon);
            }

            if (label != null)
            {
                label.text = name;
                label.fontSize = isTitle ? 28f : 24f;
                label.fontStyle = isTitle ? FontStyles.Bold : FontStyles.Normal;
                label.color = isTitle ? UguiTheme.AccentGold : new Color(1f, 1f, 1f, 0.85f);
            }

            if (valueLabel != null)
            {
                valueLabel.gameObject.SetActive(!isTitle);
                if (!isTitle) valueLabel.text = value ?? "";
            }
        }
    }
}
