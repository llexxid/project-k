using KingdomIdle.Balance;
using System;
using System.Collections.Generic;

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
    /// <summary> 장비를 장착하고 있는 플레이어(캐릭터)의 인덱스, null이면 미장착</summary>
    public int? equipmentPlayerIndex;

    public bool IsLocked;
    public bool IsEquipped => equipmentPlayerIndex.HasValue;
        /// <summary>
        /// 아이템 코드(32bit) + 강화 수치(8bit) + 개수(16bit)를 64비트 long으로 패킹한 값.
        ///   [63-56] 예약 공간  (8bit)
        ///   [55-24] itemCode  (32bit)
        ///   [23-16] 강화 수치  (8bit)
        ///   [15- 0] 개수      (16bit)
        /// 네트워크 전송, DB 저장, 디버그 로그에 활용한다.
        /// </summary>
        public long PackedData => ItemCode.PackInstance(baseData.itemCode, enhancementLevel);
        
    #region 수치 계산
    
    public EquipmentInstance(EquipmentData data, string id = null)
    {
        baseData         = data;
        enhancementLevel = 0;
        instanceId       = id ?? Guid.NewGuid().ToString("N");

        equipmentPlayerIndex = null;
    }
    public void AddStatsTo(EquipmentStatBlock block)
    {
        if (block != null && baseData != null) block.Add(EquipmentStatType.AtkFlat, GetFinalAtk());
    }
    

    #endregion
    #region (구)수치 계산
    
    /// <summary>강화 레벨이 반영된 최종 공격력 보너스</summary>
    public int GetFinalAtk()
        => GetAttackAtLevel(enhancementLevel);

    public int GetAttackAtLevel(int level)
    {
        if (baseData == null) return 0;
        long basis = 0;
        foreach (var option in baseData.MainOption) if (option.type == EquipmentStatType.AtkFlat && !option.isPercent) basis += BalanceMath.Floor((decimal)option.value);
        return checked((int)BalanceMath.WeaponAttack(basis, level));
    }

    /// <summary>강화 레벨이 반영된 최종 최대 체력 보너스</summary>
    public int GetFinalMaxHP()
    {
        return 0;
    }
    
    #endregion

    #region 강화 / 합성

    /// <summary>최대 강화 레벨에 도달했는지 여부</summary>
    public bool IsMaxLevel() => enhancementLevel >= baseData.maxEnhancementLevel;

    /// <summary>
    /// 현재 강화 레벨에서의 성공 확률(0~1)을 반환한다.
    /// </summary>
    public float GetEnhanceSuccessRate() => 1f;

    /// <summary>
    /// 강화에 필요한 동일 장비 소모 개수.
    /// </summary>
    public int GetMaterialCount() => 2;

    #endregion

}
