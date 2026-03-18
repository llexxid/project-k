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

    // ── 비트 패킹 ────────────────────────────────────────────────

    /// <summary>
    /// 아이템 코드(32bit) + 강화 수치(16bit)를 64비트 long으로 패킹한 값.
    ///   [63-32] itemCode
    ///   [31-16] enhancementLevel
    ///   [15- 8] 개수 (예약)
    ///   [ 7- 0] 유효기간 (예약)
    /// 네트워크 전송, DB 저장, 디버그 로그에 활용한다.
    /// </summary>
    public long PackedData => ItemCode.PackInstance(baseData.itemCode, enhancementLevel);

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

    /// <summary>최대 강화 레벨에 도달했는지 여부</summary>
    public bool IsMaxLevel() => enhancementLevel >= baseData.maxEnhancementLevel;

    /// <summary>
    /// 현재 강화 레벨에서의 성공 확률(0~1)을 반환한다.
    /// </summary>
    public float GetEnhanceSuccessRate() => baseData.GetSuccessRate(enhancementLevel);

    /// <summary>
    /// 강화에 필요한 동일 장비 소모 개수.
    /// </summary>
    public int GetMaterialCount() => baseData.enhanceMaterialCount;
}
