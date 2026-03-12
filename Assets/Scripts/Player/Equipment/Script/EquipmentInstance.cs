using System;

/// <summary>
/// 인벤토리에서 관리되는 장비 인스턴스.
/// ScriptableObject(EquipmentData)는 정적 데이터, 이 클래스는 런타임 상태(강화 레벨)를 담는다.
/// 같은 EquipmentData라도 별도의 인스턴스로 존재할 수 있다 (합성 재료 구분에 사용).
/// </summary>
public class EquipmentInstance
{
    /// <summary>장비의 기반 데이터 (ScriptableObject)</summary>
    public EquipmentData baseData;

    /// <summary>현재 강화 레벨 (0 = 미강화)</summary>
    public int enhancementLevel;

    /// <summary>인스턴스 고유 ID (인벤토리에서 동일 장비 구분용)</summary>
    public readonly string instanceId;

    public EquipmentInstance(EquipmentData data)
    {
        baseData         = data;
        enhancementLevel = 0;
        instanceId       = Guid.NewGuid().ToString();
    }

    // ── 최종 스탯 계산 (강화 레벨 반영) ──────────────────────────

    /// <summary>강화 레벨이 반영된 최종 공격력 보너스</summary>
    public int GetFinalAtk()
    {
        return baseData.bonusAtk + (int)(baseData.bonusAtk * baseData.atkGrowthPerLevel * enhancementLevel);
    }

    /// <summary>강화 레벨이 반영된 최종 최대 체력 보너스</summary>
    public int GetFinalMaxHP()
    {
        return baseData.bonusMaxHP + (int)(baseData.bonusMaxHP * baseData.hpGrowthPerLevel * enhancementLevel);
    }

    // ── 강화 관련 ────────────────────────────────────────────────

    /// <summary>다음 강화에 필요한 골드 비용. 레벨이 높을수록 비용 증가.</summary>
    public int GetNextEnhanceCost()
    {
        return baseData.baseEnhanceCost * (enhancementLevel + 1);
    }

    /// <summary>최대 강화 레벨에 도달했는지 여부</summary>
    public bool IsMaxLevel() => enhancementLevel >= baseData.maxEnhancementLevel;
}
