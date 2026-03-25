using UnityEngine;

public enum SkillType { Active, Passive }
public enum SkillEffectType { Normal, Parry }

[CreateAssetMenu(fileName = "NewSkillData", menuName = "ScriptableObjects/SkillData")]
public class SkillData : ScriptableObject
{
    [Header("기본 정보")]
    public string skillName;                // 스킬 이름
    public string animationStateName;       // 애니메이션 상태 이름
    public SkillType skillType;             // 액티브/패시브 구분
    public SkillEffectType skillEffectType; // 스킬 효과 타입 (Normal/Parry)

    [Header("전투 능력치 (베이스)")]
    public float damage;              // 데미지 계수 (Atk × damage)
    public float cooldown;            // 쿨타임 (초, 베이스)

    [Header("공격 범위 설정")]
    [Tooltip("정면 공격 원뿔 각도 (360=전방향, 180=정면 반원, 90=좁은 원뿔). Spearman=90, Knight=120 권장")]
    [Range(1f, 360f)]
    public float attackAngle = 360f;
    [Tooltip("VFX 스폰 위치 - 플레이어 정면 기준 거리 (0=플레이어 중심)")]
    public float vfxForwardOffset = 1f;
    [Tooltip("VFX 재생 지속 시간 (밀리초, 1000=1초)")]
    public int vfxDuration = 1000;
    [Tooltip("VFX 기본 방향이 반대일 때 체크 (좌우 반전)")]
    public bool flipVFX = false;
    [Tooltip("체크 시 VFX가 플레이어 위치가 아닌 피격 대상 위치에서 재생됨")]
    public bool vfxOnTarget = false;

    [Header("패링 설정")]
    [Tooltip("패링 지속 시간 (초). damage = 반격 데미지 계수 (ATK × damage)")]
    public float parryDuration = 2f;

    [Header("패시브 - 공격력 오라")]
    [Tooltip("패시브 공격력 보너스 (고정값, 범위 내 모든 플레이어에게 적용)")]
    public int passiveAtkBonus = 0;
    [Tooltip("패시브 적용 반경 (주변 플레이어 탐색 거리)")]
    public float passiveRange = 5f;

    [Header("패시브 - 보호막")]
    [Tooltip("HP가 이 비율 이하로 떨어지면 보호막 발동 (0.3 = 30% 이하). 0이면 비활성")]
    [Range(0f, 1f)]
    public float passiveShieldHPThreshold = 0f;
    [Tooltip("발동 시 부여되는 보호막 수치 (0이면 비활성)")]
    public int passiveShieldAmount = 0;

    [Header("강화 설정")]
    [Tooltip("최대 강화 가능 레벨")]
    public int maxLevel = 999999;
    [Tooltip("레벨당 데미지 계수 증가율 (0.1 = 10%)")]
    public float damagePerLevel = 0.1f;
    [Tooltip("레벨당 쿨타임 감소율 (0.02 = 2%). 최소는 원본의 10%")]
    public float cooldownReductionPerLevel = 0.02f;

    public GameObject skillPrefab;
}