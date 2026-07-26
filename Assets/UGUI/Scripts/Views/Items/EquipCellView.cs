using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// 장비 그리드 셀 (왕국군 장비 / 인벤토리 공용).
    /// 프리팹: Item_EquipCell.prefab — 외형은 인스펙터에서 편집.
    /// </summary>
    public sealed class EquipCellView : MonoBehaviour
    {
        [SerializeField] internal Button button;
        [SerializeField] internal Image rarityBar;      // 상단 등급색 띠
        [SerializeField] internal Image icon;
        [SerializeField] internal TMP_Text nameLabel;
        [SerializeField] internal TMP_Text subLabel;    // 스탯
        [SerializeField] internal TMP_Text stateLabel;  // "장착 중" 등
        [SerializeField] internal Image equippedFrame;  // 장착 시 초록 테두리
        [SerializeField] internal CanvasGroup dimGroup; // 직업 불가 시 흐리게
        [SerializeField] internal Image background;     // 셀 배경 (등급색 다크 틴트)

        public Button Button => button;

        public void Set(Sprite iconSprite, string name, Color nameColor, string sub,
            Color rarityColor, bool equipped, bool dimmed, string state = null, Color? stateColor = null)
        {
            if (icon != null)
            {
                icon.sprite = iconSprite;
                icon.enabled = iconSprite != null;
                icon.gameObject.SetActive(iconSprite != null);
            }
            if (nameLabel != null) { nameLabel.text = name; nameLabel.color = nameColor; }
            if (subLabel != null) subLabel.text = sub;
            if (rarityBar != null) rarityBar.color = rarityColor;
            // 셀 배경을 등급색으로 살짝 물들여 등급감을 강조(텍스트 가독성 위해 어둡게 바이어스)
            if (background != null)
                background.color = Color.Lerp(rarityColor, new Color(0.09f, 0.10f, 0.14f, 1f), 0.64f);

            if (equippedFrame != null)
                equippedFrame.gameObject.SetActive(equipped);

            if (stateLabel != null)
            {
                bool has = !string.IsNullOrEmpty(state);
                stateLabel.gameObject.SetActive(has);
                if (has)
                {
                    stateLabel.text = state;
                    stateLabel.color = stateColor ?? UguiTheme.SuccessGreenBright;
                }
            }

            if (dimGroup != null) dimGroup.alpha = dimmed ? 0.35f : 1f;
        }

        public void OnClick(Action handler)
        {
            if (button == null || handler == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => handler());
        }
    }
}
