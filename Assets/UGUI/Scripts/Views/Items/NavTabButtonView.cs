using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// 범용 탭/네비 버튼 (gacha-tab-btn / ka-member-tab / ka-nav-btn 공용).
    /// 아이콘 + 라벨로 역할을 알아볼 수 있게 하고, 선택 시 색·테두리로 강조한다.
    /// 활성 색상은 컨트롤러가 넘긴다 (뽑기=퍼플, 멤버=그린, 네비=블루).
    /// </summary>
    public sealed class NavTabButtonView : MonoBehaviour
    {
        [SerializeField] internal Button button;
        [SerializeField] internal Image background;
        [SerializeField] internal Image icon;
        [SerializeField] internal Image selectedFrame;
        [SerializeField] internal TMP_Text label;

        private static readonly Color IdleBg = new Color(0.16f, 0.17f, 0.23f, 1f);
        private static readonly Color IdleLabel = new Color(1f, 1f, 1f, 0.62f);

        public Button Button => button;

        public void SetLabel(string text)
        {
            if (label != null) label.text = text;
        }

        /// <summary>아이콘 지정. null이면 아이콘 영역을 숨기고 라벨만 표시한다.</summary>
        public void SetIcon(Sprite sprite)
        {
            if (icon == null) return;
            icon.sprite = sprite;
            icon.enabled = sprite != null;
            icon.gameObject.SetActive(sprite != null);
        }

        public void SetSelected(bool selected, Color activeBg)
        {
            if (background != null)
                background.color = selected ? UguiPixelSkin.Opaque(activeBg) : IdleBg;

            if (label != null)
                label.color = selected ? UguiTheme.TextPrimary : IdleLabel;

            if (icon != null)
                icon.color = selected ? Color.white : new Color(1f, 1f, 1f, 0.65f);

            // 선택된 탭만 금색 테두리로 강조 — 어떤 탭이 활성인지 즉시 구분
            if (selectedFrame != null)
            {
                var c = UguiTheme.AccentGoldStrong;
                selectedFrame.color = selected ? c : new Color(c.r, c.g, c.b, 0f);
            }
        }
    }
}
