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
    public string equipmentName;    // 장비 이름 (예: "낡은 검", "용사의 검")
    public string description;      // 설명 문구
    public Sprite icon;             // Equipment/Sprite 폴더의 스프라이트 연결
    public eEquipmentSlot slot;     // 장착 슬롯 (현재 Weapon 고정)
    public eEquipmentRarity rarity; // 등급

    [Header("기본 스탯 보너스")]
    public int bonusAtk;            // 공격력 보너스 (강화 0레벨 기준)
    public int bonusMaxHP;          // 최대 체력 보너스 (강화 0레벨 기준)

    [Header("강화 설정")]
    [Tooltip("최대 강화 가능 레벨 (Normal 권장: 5, Rare 권장: 10, Epic 권장: 15)")]
    public int maxEnhancementLevel = 5;

    [Tooltip("강화 레벨당 bonusAtk 증가율 (0.15 = 15%)")]
    public float atkGrowthPerLevel = 0.15f;

    [Tooltip("강화 레벨당 bonusMaxHP 증가율 (0.15 = 15%)")]
    public float hpGrowthPerLevel = 0.15f;

    [Tooltip("강화 1회당 기본 골드 비용. 실제 비용 = baseEnhanceCost × (현재 강화 레벨 + 1)")]
    public int baseEnhanceCost = 100;
}
