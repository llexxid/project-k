using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// 인벤토리 장비 상세/강화 페이지 셸 (프리팹 Item_InventoryEquipDetail).
    /// 뒤로가기 + 장비 정보(아이콘/스탯) + 액션 버튼(상세/강화) + 강화 정보로 구성된 고정 구조.
    /// 컨트롤러는 값과 클릭 핸들러만 지정한다(코드 생성 없음).
    /// </summary>
    public sealed class InventoryEquipDetailView : MonoBehaviour
    {
        [Header("Navigation / actions")]
        [SerializeField] internal Button backButton;
        [SerializeField] internal Button detailButton;
        [SerializeField] internal Button enhanceButton;
        [SerializeField] internal Image enhanceButtonBg;
        [SerializeField] internal TMP_Text enhanceButtonLabel;

        [Header("Info")]
        [SerializeField] internal Image iconImage;
        [SerializeField] internal TMP_Text nameLabel;
        [SerializeField] internal TMP_Text rarityLabel;
        [SerializeField] internal TMP_Text atkLabel;
        [SerializeField] internal TMP_Text hpLabel;
        [SerializeField] internal TMP_Text enhLabel;
        [SerializeField] internal TMP_Text equippedLabel;   // "현재 장착 중"
        [SerializeField] internal TMP_Text ownerLabel;      // "소유: 왕국군N"

        [Header("Enhance info")]
        [SerializeField] internal GameObject enhanceSection; // MAX 레벨이면 통째로 숨김
        [SerializeField] internal TMP_Text matLabel;
        [SerializeField] internal TMP_Text rateLabel;
        [SerializeField] internal TMP_Text expectedLabel;

        private static readonly Color Dim70 = new Color(1f, 1f, 1f, 0.70f);

        /// <summary>
        /// 상세 페이지 값 채우기. 텍스트는 컨트롤러가 원본과 동일하게 포맷해 넘긴다.
        /// maxLevel이면 강화 정보 섹션을 숨기고 강화 버튼을 "강화 MAX"(비활성)로 표시한다.
        /// </summary>
        public void Set(
            Sprite iconSprite, string nameText, string rarityText, string atkText, string hpText, string enhText,
            bool equipped, string ownerText,
            bool maxLevel, string matText, bool matShortage, string rateText, string expectedText)
        {
            if (iconImage != null && iconSprite != null)
            {
                iconImage.sprite = iconSprite;
                iconImage.color = Color.white;
                iconImage.preserveAspect = true;
            }

            if (nameLabel != null) nameLabel.text = nameText;
            if (rarityLabel != null) rarityLabel.text = rarityText;
            if (atkLabel != null) atkLabel.text = atkText;
            if (hpLabel != null) hpLabel.text = hpText;
            if (enhLabel != null) enhLabel.text = enhText;

            if (equippedLabel != null) equippedLabel.gameObject.SetActive(equipped);

            if (ownerLabel != null)
            {
                bool hasOwner = !string.IsNullOrEmpty(ownerText);
                ownerLabel.gameObject.SetActive(hasOwner);
                if (hasOwner) ownerLabel.text = ownerText;
            }

            if (enhanceSection != null) enhanceSection.SetActive(!maxLevel);

            if (maxLevel)
            {
                if (enhanceButtonLabel != null) enhanceButtonLabel.text = "강화 MAX";
                if (enhanceButton != null) enhanceButton.interactable = false;
                var catalog = UIManager.Instance != null ? UIManager.Instance.Catalog : null;
                if (enhanceButtonBg != null && enhanceButton != null)
                    UguiPixelSkin.ApplyButton(enhanceButtonBg, enhanceButton, UguiTheme.DisabledGrey, catalog);
            }
            else
            {
                if (matLabel != null)
                {
                    matLabel.text = matText;
                    matLabel.color = matShortage ? UguiTheme.WarnRed : Dim70;
                }
                if (rateLabel != null) rateLabel.text = rateText;
                if (expectedLabel != null) expectedLabel.text = expectedText;
            }
        }
    }
}
