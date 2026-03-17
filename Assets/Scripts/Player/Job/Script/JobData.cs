using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 직업 단위의 스킬 강화 레벨 정보.
/// JobData.skillLevels 리스트의 원소로 사용된다.
/// </summary>
[Serializable]
public struct JobSkillLevel
{
    [Tooltip("스킬 이름 (SkillData.skillName과 일치해야 함)")]
    public string skillName;

    [Tooltip("이 직업에서 해당 스킬의 현재 강화 레벨")]
    public int level;
}

/// <summary>
/// 직업 하나의 스탯·비주얼·스킬 목록을 담는 ScriptableObject.
/// Assets > Create > ScriptableObjects > JobData 로 에셋 생성.
/// </summary>
[CreateAssetMenu(fileName = "NewJobData", menuName = "ScriptableObjects/JobData")]
public class JobData : ScriptableObject
{
    [Header("직업 정보")]
    public string jobName;                              // 직업 이름 (예: "Warrior", "Mage")

    [Header("비주얼")]
    public Sprite jobSprite;                            // 전직 시 교체할 캐릭터 스프라이트
    public RuntimeAnimatorController animatorController;// 전직 시 교체할 애니메이터 컨트롤러

    [Header("기본 스탯")]
    public int maxHP;               // 최대 체력
    public int atk;                 // 공격력
    public int movSpeed;            // 이동 속도
    public float atkSpeed;          // 공격 속도 (초)

    [Header("전직 비용")]
    [Tooltip("첫 전직 시 필요한 골드. 이미 해금된 직업은 무료.")]
    public int unlockCost;

    [Header("직업 전용 스킬")]
    public List<SkillData> skills;  // 이 직업이 사용할 스킬 목록

    [Header("스킬 레벨")]
    [Tooltip("이 직업에서 각 스킬의 강화 레벨. 전직 시 PlayerSkillRuntime에 적용된다.")]
    public List<JobSkillLevel> skillLevels;
}

