using UnityEngine;

// 스킬 타입을 구분하기 위한 Enum
public enum SkillType { Active, Passive }

[CreateAssetMenu(fileName = "NewSkillData", menuName = "ScriptableObjects/SkillData")]
public class SkillData : ScriptableObject
{
    [Header("기본 정보")]
    public string skillName;         // 스킬 이름
    public string animationStateName;// 애니메이션 상태 이름
    public SkillType skillType;      // 액티브/패시브 구분

    [Header("전투 능력치")]
    public float damage;             // 데미지
    public float cooldown;           // 쿨타임

    [Header("프리팹 설정")]
    public GameObject skillPrefab;   // 오브젝트 풀링에서 생성할 프리팹
}