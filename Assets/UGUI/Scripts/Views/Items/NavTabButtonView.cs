using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// 범용 탭/네비 버튼 (gacha-tab-btn / ka-member-tab / ka-nav-btn 공용).
    /// 아이콘 + 라벨로 역할을 알아볼 수 있게 하고, 선택 시 색·테두리로 강조한다.
    /// 선택 상태는 모든 메뉴에서 같은 청색과 밝은 테두리로 표시한다.
    /// </summary>
    public sealed class NavTabButtonView : MonoBehaviour
    {
        [SerializeField] internal Button button;
        [SerializeField] internal Image background;
        [SerializeField] internal Image icon;
        [SerializeField] internal Image selectedFrame;
        [SerializeField] internal TMP_Text label;

        private bool? _selected;

        public Button Button => button;

        public void SetLabel(string text)
        {
            if (label == null || label.text == text) return;
            label.text = text;
            var layout = label.GetComponent<LayoutElement>();
            if (layout != null)
            {
                float height = !string.IsNullOrEmpty(text) && text.Contains("\n") ? 100 : 64;
                layout.minHeight = height;
                layout.preferredHeight = height;
            }
        }

        /// <summary>아이콘 지정. null이면 아이콘 영역을 숨기고 라벨만 표시한다.</summary>
        public void SetIcon(Sprite sprite)
        {
            if (icon == null || (icon.sprite == sprite && icon.gameObject.activeSelf == (sprite != null))) return;
            icon.sprite = sprite;
            icon.enabled = sprite != null;
            icon.gameObject.SetActive(sprite != null);
            if (icon.transform.parent != null && icon.transform.parent.name == "IconWrap")
                icon.transform.parent.gameObject.SetActive(sprite != null);
        }

        public void SetSelected(bool selected, Color activeBg)
        {
            if (_selected == selected) return;
            _selected = selected;
            if (background != null)
                background.color = selected ? UguiTheme.RoyalBlue : UguiTheme.RusticSurfaceDark;

            if (label != null)
                label.color = selected ? UguiTheme.Parchment : UguiTheme.TextSecondary;

            if (icon != null)
                icon.color = selected ? UguiTheme.TextPrimary : UguiTheme.TextSecondary;

            // 선택은 색상과 테두리 두 가지 신호로 구분한다.
            if (selectedFrame != null)
            {
                var c = UguiTheme.SelectedBorder;
                selectedFrame.color = selected ? c : new Color(c.r, c.g, c.b, 0f);
            }
        }
    }
}
