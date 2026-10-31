using KingdomIdle.Balance;
using System;
using System.Collections.Generic;

/// <summary>
/// 인벤토리에서 관리되는 장비 인스턴스.
/// ScriptableObject(EquipmentData)는 정적 데이터, 이 클래스는 런타임 상태(강화 레벨·장착·잠금)를 담는다.
/// 서버는 장비를 "정의 코드+강화+만료" 묶음(수량)으로 관리하므로, 인스턴스는 서버 묶음의 한 사본이다.
/// 서버 요청의 ItemCode는 ToItemCode()로 만든다(수량 비트는 잠금·장착·강화에서 무시되고 수령·분해에서만 옮길 수다).
/// </summary>
public class EquipmentInstance
{
    /// <summary>장비의 기반 데이터 (ScriptableObject)</summary>
    public EquipmentData baseData;

    /// <summary>현재 강화 레벨 (0 = 미강화)</summary>
    public int enhancementLevel;
    /// <summary>인스턴스 고유 ID (인벤토리에서 동일 장비 구분용). 서버 묶음을 펼칠 때 같은 (Code, Level)의 기존 Id를 이어받는다.</summary>
    public readonly string instanceId;
    /// <summary> 장비를 장착하고 있는 플레이어(캐릭터)의 인덱스, null이면 미장착</summary>
    public int? equipmentPlayerIndex;

    public bool IsLocked;
    /// <summary>서버 ItemCode.ExpireTimesc. 0이면 무기한.</summary>
    public long expiresUtc;
    /// <summary>서버가 기록한 이 장비 1개의 강화석 누적 지출. 분해 환급 미리보기에만 쓴다.</summary>
    public long stonesSpent;
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

    /// <summary>서버 요청용 ItemCode. 정의 코드·강화·만료로 묶음을 지정하고 quantity는 옮길 수량이다.</summary>
    public Protocol.Message.Dto.ItemCode ToItemCode(int quantity = 1)
    {
        return MakeItemCode(baseData != null ? baseData.itemCode : 0, enhancementLevel, expiresUtc, quantity);
    }

    /// <summary>정의 코드(32bit)·강화·만료·수량으로 서버 ItemCode를 만든다. 음수 코드는 부호 확장을 막기 위해 uint로 자른다.</summary>
    public static Protocol.Message.Dto.ItemCode MakeItemCode(int code, int level, long expiresUtc, int quantity)
    {
        ulong packed = ((ulong)(uint)code << 24) | ((ulong)(level & 0xFF) << 16) | (ulong)(quantity & 0xFFFF);
        return new Protocol.Message.Dto.ItemCode { Code = packed, ExpireTimesc = expiresUtc > 0 ? (ulong)expiresUtc : 0UL };
    }

    /// <summary>같은 서버 묶음(정의·강화·만료)의 사본인지.</summary>
    public bool SameStack(EquipmentInstance other)
    {
        return other != null && baseData != null && other.baseData != null && baseData.itemCode == other.baseData.itemCode &&
            enhancementLevel == other.enhancementLevel && expiresUtc == other.expiresUtc;
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
    /// 현재 강화 레벨에서의 성공 확률(0~1)을 반환한다. 서버 강화는 항상 성공한다.
    /// </summary>
    public float GetEnhanceSuccessRate() => 1f;

    #endregion

}
