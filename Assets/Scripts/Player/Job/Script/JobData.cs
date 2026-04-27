using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 직업 하나의 스탯·비주얼·스킬 구성을 담는 ScriptableObject.
/// Assets > Create > ScriptableObjects > JobData 로 에셋 생성.
/// </summary>
[CreateAssetMenu(fileName = "NewJobData", menuName = "ScriptableObjects/JobData")]
public class JobData : ScriptableObject
{
    [Header("직업 정보")]
    public string jobName;                              // 직업 이름 (예: "Knight", "Mage")

    [Header("비주얼")]
    public Sprite jobSprite;                            // 전직 시 교체할 캐릭터 스프라이트
    public RuntimeAnimatorController animatorController;// 전직 시 교체할 애니메이터 컨트롤러

    [Header("기본 스탯")]
    public int maxHP;               // 최대 체력
    public int atk;                 // 공격력
    public int movSpeed;            // 이동 속도

    [Header("기본공격")]
    public BasicAttackConfig basicAttack = new BasicAttackConfig();

    [Header("스페셜 스킬")]
    public List<SpecialSkillConfig> specialSkills = new List<SpecialSkillConfig>();
}

/// <summary>기본공격의 타입.</summary>
public enum BasicAttackType
{
    Single,     // 단일 대상 (Spearman, Knight, Archer, Elite_Archer)
    Rect,       // 전방 직사각형 (Elite_Knight)
    Projectile  // 투사체 (Mage, Elite_Mage)
}

/// <summary>스페셜 스킬 종류.</summary>
public enum SpecialSkillKind
{
    None,
    IronWill,
    ChargeShot,
    EnergyPulse
}

[Serializable]
public class BasicAttackConfig
{
    public BasicAttackType type = BasicAttackType.Single;

    [Tooltip("공격 사거리 (이동 정지 거리 · 탐지 반경 결정).")]
    public float range = 2f;

    [Tooltip("공격 애니메이션 종료 후 쿨다운(초).")]
    public float cooldown = 1f;

    [Tooltip("공격력에 곱해지는 피해 배율. 1.0 = 100%.")]
    public float damageMultiplier = 1f;

    [Header("Rect 전용")]
    [Tooltip("타격 범위의 반너비(전방 거리).")]
    public float halfWidth = 1.5f;
    [Tooltip("타격 범위의 반높이(상하 거리).")]
    public float halfHeight = 1f;

    [Header("Projectile 전용")]
    [Tooltip("투사체 폭발 반경.")]
    public float aoeRadius = 0.5f;
    [Tooltip("투사체 이동 속도.")]
    public float projectileSpeed = 4f;
}

[Serializable]
public class SpecialSkillConfig
{
    public SpecialSkillKind kind = SpecialSkillKind.None;

    [Tooltip("스킬 쿨다운(초). IronWill/ChargeShot 은 효과 종료 후 시작.")]
    public float cooldown = 10f;

    [Tooltip("공격력에 곱해지는 피해 배율 (ChargeShot · EnergyPulse).")]
    public float damageMultiplier = 1f;

    [Header("ChargeShot")]
    [Tooltip("ChargeShot: 사거리.")]
    public float range = 4f;
    [Tooltip("ChargeShot: 연속 타격 횟수.")]
    public int hitCount = 3;

    [Header("IronWill")]
    [Tooltip("IronWill: 초당 회복 비율 (0.1 = MaxHP의 10%/초).")]
    public float healPercent = 0.1f;
    [Tooltip("IronWill: 회복 지속시간(초).")]
    public float duration = 15f;
    [Tooltip("IronWill: 자동 발동 HP 비율 (0.5 = HP 50% 미만).")]
    public float triggerHPRatio = 0.5f;

    [Header("EnergyPulse")]
    [Tooltip("EnergyPulse: 발동 판정 반경.")]
    public float triggerRange = 1.5f;
    [Tooltip("EnergyPulse: 넉백 세기.")]
    public float knockbackForce = 5f;
}
