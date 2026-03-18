using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 장비 슬롯 종류 (현재 무기만 사용, 추후 확장 가능)
/// </summary>
public enum eEquipmentSlot
{
    Weapon,
    SLOT_COUNT,
}

/// <summary>
/// 장비 등급
/// </summary>
public enum eEquipmentRarity
{
    Normal = 0,
    Rare   = 1,
    Epic   = 2,
}

/// <summary>
/// 장비 아이템 하나의 데이터를 담는 ScriptableObject.
/// Assets > Create > ScriptableObjects > EquipmentData 로 에셋 생성.
///
/// [현재 구성]
///   Normal 3종 / Rare 2종 / Epic 1종 (무기만)
///   Sprite 경로: Assets/Scripts/Player/Equipment/Sprite/
/// </summary>
[CreateAssetMenu(fileName = "NewEquipment", menuName = "ScriptableObjects/EquipmentData")]
public class EquipmentData : ScriptableObject
{
    [Header("장비 정보")]
    public string equipmentName;
    public string description;
    public Sprite icon;
    public eEquipmentSlot slot;
    public eEquipmentRarity rarity;

    [Header("기본 스탯 보너스")]
    public int bonusAtk;
    public int bonusMaxHP;

    [Header("강화 설정 (재료 소모)")]
    [Tooltip("최대 강화 가능 레벨 (Normal 권장: 5, Rare 권장: 10, Epic 권장: 15)")]
    public int maxEnhancementLevel = 5;

    [Tooltip("강화 레벨당 bonusAtk 증가율 (0.15 = 15%)")]
    public float atkGrowthPerLevel = 0.15f;

    [Tooltip("강화 레벨당 bonusMaxHP 증가율 (0.15 = 15%)")]
    public float hpGrowthPerLevel = 0.15f;

    [Tooltip("강화 1회 시 소모할 동일 장비 개수")]
    public int enhanceMaterialCount = 2;

    [Tooltip("강화 레벨별 성공 확률 (0~1). 인덱스 = 현재 강화 레벨.\n"
           + "예) [1.0, 0.8, 0.6, 0.4, 0.2] → +0→+1은 100%, +4→+5는 20%\n"
           + "리스트보다 레벨이 높으면 마지막 값 사용.")]
    public List<float> enhanceSuccessRates = new List<float> { 1f, 0.8f, 0.6f, 0.4f, 0.2f };

    [Header("허용 직업 (비트 플래그)")]
    [Tooltip("이 장비를 드롭/사용할 수 있는 직업 플래그.\n"
           + "None(0)이면 모든 직업 공용.\n"
           + "예) Warrior | Knight → 전사·기사만 허용.")]
    [SerializeField] private eJobFlag _jobMask;

    [Header("아이템 코드")]
    [Tooltip("이 장비의 고유 번호 (0~65535). 같은 레어도·슬롯 내에서 중복 없이 입력.")]
    [SerializeField] private int _itemId;

    // ── 아이템 코드 ───────────────────────────────────────────────────

    /// <summary>
    /// rarity + slot + _jobMask + _itemId를 합산한 32비트 아이템 코드.
    ///   [31-28] 레어도 (4bit)
    ///   [27-24] 슬롯   (4bit)
    ///   [23-16] 직업마스크 (8bit) — 0 = 모든 직업 공용
    ///   [15- 0] 아이템ID (16bit)
    /// </summary>
    public int itemCode => ItemCode.Encode(rarity, slot, _jobMask, _itemId);

    // ── 헬퍼 ─────────────────────────────────────────────────────────

    /// <summary>
    /// 지정 직업이 이 장비를 사용 가능한지 비트 연산으로 반환한다.
    /// _jobMask == None(0)이면 모든 직업 허용.
    /// jobName이 eJobFlag 멤버 이름과 다르면 false 반환.
    /// </summary>
    public bool IsAllowedForJob(string jobName)
    {
        if (_jobMask == eJobFlag.None) return true;
        if (!System.Enum.TryParse<eJobFlag>(jobName, out eJobFlag flag)) return false;
        return (_jobMask & flag) != 0;
    }

    /// <summary>
    /// 현재 강화 레벨에서의 성공 확률(0~1)을 반환한다.
    /// 리스트가 비어있으면 100% 반환.
    /// </summary>
    public float GetSuccessRate(int currentEnhanceLevel)
    {
        if (enhanceSuccessRates == null || enhanceSuccessRates.Count == 0) return 1f;
        int idx = Mathf.Clamp(currentEnhanceLevel, 0, enhanceSuccessRates.Count - 1);
        return enhanceSuccessRates[idx];
    }
}
