using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// 왕국군 전직 상세 화면. 프리팹: Panel_KAJobDetail.prefab
    /// 헤더(이미지/이름/상태배지/역할) + 스탯 비교표(Item_StatCompareRow) +
    /// 직업 스킬(Item_SkillRow) + 전직 조건 + 전직 버튼(Item_ActionButton).
    /// </summary>
    public sealed class KAJobDetailView : MonoBehaviour
    {
        [SerializeField] internal Button backButton;

        [Header("헤더")]
        [SerializeField] internal Image image;
        [SerializeField] internal TMP_Text jobNameLabel;
        [SerializeField] internal TMP_Text stateBadge;
        [SerializeField] internal TMP_Text roleLabel;

        [Header("스탯 비교")]
        [SerializeField] internal RectTransform compareTable;  // Item_StatCompareRow 부모

        [Header("직업 스킬")]
        [SerializeField] internal RectTransform skillGroup;    // 스킬 없으면 통째로 숨김
        [SerializeField] internal RectTransform skillList;     // Item_SkillRow 부모

        [Header("전직 조건")]
        [SerializeField] internal TMP_Text freeLabel;          // 무료 재전직 안내
        [SerializeField] internal RectTransform fragCondRow;
        [SerializeField] internal TMP_Text fragCondValue;
        [SerializeField] internal RectTransform prereqCondRow;
        [SerializeField] internal TMP_Text prereqCondValue;

        [Header("전직 버튼")]
        [SerializeField] internal RectTransform changeRow;     // Item_ActionButton 부모
    }
}
