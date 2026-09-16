using System;
using KingdomIdle.MageTower;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Serialization;

namespace KingdomIdle.UGUI
{
    public sealed class MageSkillCellView : MonoBehaviour
    {
        public Button button;
        public Image frameImage;
        public Image background;
        public Image icon;
        public TMP_Text nameLabel;
        public TMP_Text dmgLabel;
        [FormerlySerializedAs("rarityLabel")] public TMP_Text stateLabel;
        public CanvasGroup canvasGroup;

        public void Set(MageTowerSkillSO skill, bool owned, bool equipped, float power, bool bloom, Action onClick)
        {
            if (frameImage != null) frameImage.color = equipped ? UguiTheme.BronzeLight : bloom ? MageSkillPresentation.BloomAccent : MageSkillPresentation.Frame;
            if (background != null) background.color = UguiTheme.RusticSurfaceDark;
            if (icon != null)
            {
                icon.enabled = skill.DisplayIcon(bloom) != null;
                icon.sprite = skill.DisplayIcon(bloom);
                icon.color = owned ? Color.white : new Color(.62f, .62f, .62f, 1f);
            }
            if (nameLabel != null) nameLabel.text = skill.nameKor;
            if (stateLabel != null) { stateLabel.text = equipped ? "◆ 장착 중" : bloom ? "개화" : owned ? "보유" : "미보유"; stateLabel.color = equipped ? UguiTheme.BronzeLight : bloom ? MageSkillPresentation.BloomAccent : UguiTheme.TextSecondary; }
            Action refresh = () => {
                if (dmgLabel == null) return;
                dmgLabel.text = !owned ? "미보유 · 자세히" : $"{(bloom ? "개화 · " : "")}{(skill.IsHealing ? "회복" : "피해")} {NumberNotation.Format(power)}";
            };
            NumberNotationBinding.Bind(this, refresh); refresh();
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.interactable = true;
                if (onClick != null) button.onClick.AddListener(() => onClick());
            }
            if (canvasGroup != null) { canvasGroup.alpha = 1f; canvasGroup.interactable = true; canvasGroup.blocksRaycasts = true; }
        }
    }
}
