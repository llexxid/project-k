using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// 왕국군 장비 탭. 프리팹: Panel_KAEquipment.prefab
    /// 상단 "장착 중" 카드(무기 슬롯) + 하단 "보유 장비" 그리드(Item_EquipCell 인스턴스).
    /// </summary>
    public sealed class KAEquipmentView : MonoBehaviour
    {
        [Header("장착 중 카드")]
        [SerializeField] internal Image equippedFrame;         // 장착 초록 테두리 (장착 시만 활성)
        [SerializeField] internal TMP_Text equippedSlotLabel;  // "무기"
        [SerializeField] internal RectTransform equippedIconWrap;
        [SerializeField] internal Image equippedIcon;
        [SerializeField] internal TMP_Text equippedNameLabel;  // 이름 / "비어있음"
        [SerializeField] internal TMP_Text equippedStatLabel;  // ATK/HP (빈 슬롯이면 숨김)
        [SerializeField] internal Button unequipButton;        // 해제 (장착 시만 활성)

        [Header("보유 장비")]
        [SerializeField] internal RectTransform inventoryGrid; // Item_EquipCell 부모 (GridLayout)
        [SerializeField] internal TMP_Text emptyLabel;         // 보유 장비 없음 placeholder
    }
}
