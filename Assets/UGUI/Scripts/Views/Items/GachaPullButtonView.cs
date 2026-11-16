using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// 뽑기 옵션 버튼 (1회 / 10연 등). 크고 명확한 버튼 — 제목 + 비용 + 아이콘.
    /// 프리팹: Assets/UGUI/Prefabs/Items/Item_GachaPullButton.prefab
    /// 외형(색·크기·폰트)은 프리팹에서 직접 편집 가능. 데이터는 Set()으로 주입.
    /// </summary>
    public sealed class GachaPullButtonView : MonoBehaviour
    {
        [SerializeField] internal Button button;
        [SerializeField] internal Image background;
        [SerializeField] internal Image icon;
        [SerializeField] internal TMP_Text titleLabel;
        [SerializeField] internal TMP_Text costLabel;
        [SerializeField] internal bool authoredBackground;
        [SerializeField] internal GachaButtonFlare flare;
        [SerializeField] internal bool primaryAction = true;
        private bool _affordable = true;

        public Button Button => button;

        /// <summary>표시 갱신. affordable=false 면 회색/비활성 처리.</summary>
        public void Set(string title, string cost, bool affordable, Sprite iconSprite = null)
        {
            if (titleLabel != null && titleLabel.text != title) titleLabel.text = title;
            if (costLabel != null && costLabel.text != cost) costLabel.text = cost;

            if (icon != null)
            {
                if (icon.sprite != iconSprite) icon.sprite = iconSprite;
                icon.enabled = iconSprite != null;
                if (icon.gameObject.activeSelf != (iconSprite != null)) icon.gameObject.SetActive(iconSprite != null);
            }

            _affordable = affordable;
            ApplyAppearance();

            if (button != null) button.interactable = affordable;
        }

        public void SetEmphasis(bool primary)
        {
            primaryAction = primary;
            ApplyAppearance();
        }

        private void ApplyAppearance()
        {
            if (background != null)
                background.color = authoredBackground
                    ? (_affordable ? Color.white : new Color(.55f, .55f, .55f, 1f))
                    : (_affordable ? (primaryAction ? UguiTheme.ActionGold : UguiTheme.RusticSurface) : UguiTheme.RusticSurfaceDark);
            var ink = _affordable && primaryAction && !authoredBackground ? UguiTheme.InfoInk : UguiTheme.TextPrimary;
            if (titleLabel != null) titleLabel.color = _affordable ? ink : UguiTheme.TextSecondary;
            if (costLabel != null) costLabel.color = _affordable ? ink : UguiTheme.TextSecondary;
        }

        public void SetInteractable(bool v)
        {
            if (button != null) button.interactable = v;
        }

        public void PlayMageFlare(System.Action complete, System.Action cancel)
        {
            if (flare != null) flare.Play(complete, cancel);
            else complete?.Invoke();
        }
    }
}
