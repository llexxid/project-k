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
            // 카드 배경도 등급색으로 다크 틴트 (등급감 강조, 텍스트 가독성 유지)
            if (background != null)
                background.color = UguiTheme.RusticPanelDeep;
        }

        /// <summary>결과에서만 읽기 큰 구성을 적용한다. 미리보기와 공개 연출의 참조는 재사용한다.</summary>
        internal void SetResultPresentation(int resultCount)
        {
            bool single = resultCount == 1, pair = resultCount == 2;
            var layout = GetComponent<VerticalLayoutGroup>();
            layout.padding = single || pair ? new RectOffset(20, 20, 20, 20) : new RectOffset(16, 16, 16, 16);
            layout.spacing = 10;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childForceExpandHeight = false;
            if (background != null) background.color = UguiTheme.InfoSurface;
            if (icon != null)
            {
                var wrap = icon.transform.parent.GetComponent<LayoutElement>();
                if (wrap != null) wrap.preferredHeight = single ? 240 : pair ? 200 : resultCount == 3 ? 160 : 144;
                float size = single ? 232 : pair ? 184 : resultCount == 3 ? 156 : 132;
                icon.rectTransform.sizeDelta = new Vector2(size, size);
            }
            StyleResultLabel(nameLabel, single ? 56 : pair ? 48 : 42, single ? 136 : pair ? 116 : 104, UguiTheme.InfoInk);
            StyleResultLabel(subLabel, single ? 44 : 36, single ? 148 : pair ? 132 : 124, UguiTheme.InfoMuted);
            if (iconFallback != null)
            {
                iconFallback.fontSize = single ? 48 : 36;
                iconFallback.color = UguiTheme.InfoInk;
                iconFallback.fontSharedMaterial = iconFallback.font.material;
            }
        }

        private static void StyleResultLabel(TMP_Text label, float size, float height, Color color)
        {
            if (label == null) return;
            label.fontSize = size;
            label.color = color;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.fontSharedMaterial = label.font.material;
            var element = label.GetComponent<LayoutElement>();
            if (element != null) element.preferredHeight = height;
        }
    }
}
