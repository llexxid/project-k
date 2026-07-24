using UnityEngine;
using TMPro;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// 왕국군 스킬 탭. 프리팹: Panel_KASkill.prefab
    /// 섹션 타이틀 + 스킬 목록(Item_SkillRow 인스턴스) / placeholder.
    /// </summary>
    public sealed class KASkillView : MonoBehaviour
    {
        [SerializeField] internal RectTransform skillList;  // Item_SkillRow 부모
        [SerializeField] internal TMP_Text placeholder;     // 스킬 없음 안내 (없으면 숨김)
    }
}
