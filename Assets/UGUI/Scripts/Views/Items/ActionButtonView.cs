using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// 범용 액션 버튼 (강화 / 장착 / 전직 등). 아이콘 + 라벨.
    /// 프리팹: Item_ActionButton.prefab — 팀원이 인스펙터에서 외형 편집 가능.
    /// </summary>
    public sealed class ActionButtonView : MonoBehaviour
    {
        [SerializeField] internal Button button;
        [SerializeField] internal Image background;
        [SerializeField] internal Image icon;
        [SerializeField] internal TMP_Text label;

        public Button Button => button;

        public void Set(string text, Color bg, bool interactable = true, Sprite iconSprite = null)
        {
            if (label != null) label.text = text;
            if (background != null) background.color = interactable ? bg : UguiTheme.DisabledGrey;
            if (icon != null)
            {
                icon.sprite = iconSprite;
                icon.enabled = iconSprite != null;
                icon.gameObject.SetActive(iconSprite != null);
            }
            if (button != null) button.interactable = interactable;
        }

        public void OnClick(Action handler)
        {
            if (button == null || handler == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => handler());
        }
    }
}
