using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// 왕국군 장비 상세/액션 화면. 프리팹: Panel_KAEquipDetail.prefab
    /// 뒤로가기 + 장비 정보 + 액션 버튼 행(Item_ActionButton) + 강화 정보.
    /// </summary>
    public sealed class KAEquipDetailView : MonoBehaviour
    {
        [SerializeField] internal Button backButton;

        [Header("정보")]
        [SerializeField] internal Image icon;
        [SerializeField] internal TMP_Text nameLabel;
        [SerializeField] internal TMP_Text rarityLabel;
        [SerializeField] internal TMP_Text atkLabel;
        [SerializeField] internal TMP_Text hpLabel;
        [SerializeField] internal TMP_Text enhanceLabel;
        [SerializeField] internal TMP_Text equippedNowLabel;   // "현재 장착 중" (장착 시만)

        [Header("액션 / 강화")]
        [SerializeField] internal RectTransform actionRow;      // Item_ActionButton 2개 부모
        [SerializeField] internal RectTransform enhanceInfoGroup; // 최대강화 시 통째로 숨김
        [SerializeField] internal TMP_Text materialLabel;
        [SerializeField] internal TMP_Text successRateLabel;
        [SerializeField] internal TMP_Text expectedLabel;
    }
}
