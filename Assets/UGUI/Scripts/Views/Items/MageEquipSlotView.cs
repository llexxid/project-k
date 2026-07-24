using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KingdomIdle.UGUI
{
    /// <summary>마탑 장착 슬롯 셀 (좌측 슬롯 열). 프리팹 Item_MageEquipSlot. 인스펙터 편집 가능.</summary>
    public sealed class MageEquipSlotView : MonoBehaviour
    {
        public Button button;
        public Image borderImage;   // 미선택 흰0.2 / 선택모드 활성 초록
        public Image icon;          // 장착 스킬 아이콘 (없으면 비활성)
        public TMP_Text label;      // 아이콘 없을 때 스킬명 / 빈 슬롯 "-"

        private static readonly Color BorderNormal = new Color(1f, 1f, 1f, 0.20f);
        private static readonly Color BorderActive = new Color(100f / 255f, 210f / 255f, 130f / 255f, 0.7f);
        private static readonly Color EmptyTextColor = new Color(1f, 1f, 1f, 0.3f);

        /// <summary>슬롯 표시 갱신. iconSprite 있으면 아이콘, 없고 스킬 장착시 name, 빈 슬롯이면 "-".</summary>
        public void Set(Sprite iconSprite, string name, bool empty, bool active)
        {
            if (borderImage != null) borderImage.color = active ? BorderActive : BorderNormal;

            bool hasIcon = iconSprite != null && icon != null;
            if (icon != null)
            {
                icon.enabled = hasIcon;
                if (hasIcon) icon.sprite = iconSprite;
                icon.gameObject.SetActive(hasIcon);
            }
            if (label != null)
            {
                label.text = hasIcon ? "" : (empty ? "-" : name);
                label.color = empty ? EmptyTextColor : UguiTheme.TextPrimary;
            }
        }
    }
}
