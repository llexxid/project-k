using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KingdomIdle.UGUI
{
    /// <summary>마탑 보유 스킬 그리드 셀. 프리팹 Item_MageSkillCell. 인스펙터 편집 가능.</summary>
    public sealed class MageSkillCellView : MonoBehaviour
    {
        public Button button;
        public Image frameImage;      // 장착중 초록 테두리 / 평시 투명
        public Image background;      // 버튼 타겟 그래픽 (안쪽 배경)
        public Image icon;
        public TMP_Text nameLabel;
        public TMP_Text dmgLabel;     // 미보유시 숨김
        public CanvasGroup canvasGroup;

        private static readonly Color EquippedBorder = new Color(100f / 255f, 210f / 255f, 130f / 255f, 0.6f);
        private const float LockedAlpha = 0.35f;

        /// <summary>셀 표시 갱신. onClick은 owned일 때만 연결(미보유는 입력 차단).</summary>
        public void Set(Sprite iconSprite, string name, bool owned, bool equipped, float dmg, Action onClick)
        {
            if (frameImage != null) frameImage.color = equipped ? EquippedBorder : Color.clear;

            if (icon != null)
            {
                icon.enabled = iconSprite != null;
                if (iconSprite != null) icon.sprite = iconSprite;
            }
            if (nameLabel != null) nameLabel.text = name;
            if (dmgLabel != null)
            {
                dmgLabel.gameObject.SetActive(owned);
                if (owned) dmgLabel.text = $"DMG {dmg:F0}";
            }
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.interactable = owned;
                if (owned && onClick != null) button.onClick.AddListener(() => onClick());
            }
            if (canvasGroup != null)
            {
                canvasGroup.alpha = owned ? 1f : LockedAlpha;
                canvasGroup.interactable = owned;
                canvasGroup.blocksRaycasts = owned;
            }
        }
    }
}
