using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct EquipmentDropRarityWeight
{
    [Tooltip("이 항목이 해당하는 장비 등급")]
    public eEquipmentRarity rarity;

    [Tooltip("이 등급이 선택될 가중치 (높을수록 자주 나옴)")]
    public float weight;
}

[CreateAssetMenu(fileName = "NewEquipmentDropTable", menuName = "ScriptableObjects/EquipmentDropTableSO")]
public class EquipmentDropTableSO : ScriptableObject
{
    [Header("드롭 확률")]
    [Range(0f, 1f)]
    [Tooltip("몬스터 처치 시 장비가 드롭될 확률 (0 = 절대 드롭 안 함 / 1 = 항상 드롭)")]
    public float dropChance = 0.1f;

    [Header("등급별 가중치 (드롭 발생 시 등급 결정)")]
    [Tooltip("Normal / Rare / Epic 순서로 가중치 입력\n예) Normal:70 / Rare:25 / Epic:5")]
    public List<EquipmentDropRarityWeight> rarityWeights = new List<EquipmentDropRarityWeight>();

    /// <summary>
    /// 드롭 발생 여부와 등급을 한 번에 판정한다.
    /// 드롭이 없으면 null, 있으면 결정된 등급을 반환한다.
    /// Player.GiveReward()에서 호출된다.
    /// </summary>
    public eEquipmentRarity? RollDrop()
    {
        if (UnityEngine.Random.value > dropChance) return null;

        return RollRarity();
    }

    private eEquipmentRarity RollRarity()
    {
        float totalWeight = 0f;
        foreach (var rw in rarityWeights) totalWeight += rw.weight;

        if (totalWeight <= 0f) return eEquipmentRarity.Normal; // fallback

        float roll = UnityEngine.Random.Range(0f, totalWeight);
        float cumulative = 0f;

        foreach (var rw in rarityWeights)
        {
            cumulative += rw.weight;
            if (roll <= cumulative) return rw.rarity;
        }

        return eEquipmentRarity.Normal; // fallback
    }
}
