using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 각 플레이어(캐릭터)의 장비 시스템 메인 매니저.
/// 장착/해제, 드롭, 강화(재료 소모+확률), 합성, 직업 필터를 담당한다.
/// Player.Awake()에서 new → Init() 순서로 초기화한다.
/// </summary>
public class PlayerEquipmentManager
{
    /// <summary>장비 착용 완료 시 발행. (슬롯, 착용된 인스턴스)</summary>
    public event Action<PlayerEquipmentManager, eEquipmentSlot, EquipmentInstance> OnEquipped;
    /// <summary>장비 해제 완료 시 발행. (슬롯)</summary>
    public event Action<PlayerEquipmentManager, eEquipmentSlot, EquipmentInstance> OnUnequipped;

    public int PlayerIndex => _playerIndex;
    
    // 현재 착용 중인 장비 (슬롯 → 인스턴스)
    private readonly Dictionary<eEquipmentSlot, EquipmentInstance> _equipped = new();

    private int _playerIndex = -1;
    private PlayerStatus _playerStatus;
    
    /// <summary>
    /// Player.Awake()에서 반드시 호출해야 한다.
    /// </summary>
    public void Init(PlayerStatus playerStatus,  int playerIndex)
    {
        _playerStatus = playerStatus;
        _playerIndex = playerIndex;
    }

    #region 장비 장착 / 해제

    /// <summary>
    /// 장비를 착용한다. 같은 슬롯에 장비가 이미 있으면 교체된다.
    /// 착용한 장비는 인벤토리에서 제거하지 않는다 (인벤토리에 남아있는 채로 착용 표시).
    /// </summary>
    public void Equip(EquipmentInstance equipment)
    {
        if (equipment == null) return;
        if (equipment.IsEquipped)
        {
            Debug.LogWarning($"플레이어{equipment.equipmentPlayerIndex}가 이미 장착중");
            return;
        }
        eEquipmentSlot slot = equipment.baseData.slot;
        if (_equipped.TryGetValue(slot, out EquipmentInstance prevEquip))
        {
            prevEquip.equipmentPlayerIndex = null;
        }
        _equipped[slot] = equipment;
        equipment.equipmentPlayerIndex = _playerIndex;
        
        RecalculateStats();
        OnEquipped?.Invoke(this,equipment.baseData.slot, equipment);
    }

    /// <summary>지정 슬롯의 장비를 해제한다.</summary>
    public void Unequip(eEquipmentSlot slot)
    {
        if (!_equipped.TryGetValue(slot, out EquipmentInstance equipment))
        {
            return;
        }

        string removedName = equipment.baseData.equipmentName;
        
        equipment.equipmentPlayerIndex = null;
        _equipped.Remove(slot);
        
        RecalculateStats();
        OnUnequipped?.Invoke(this,slot,equipment);
    }

    /// <summary>슬롯에 착용된 인스턴스를 반환한다. 없으면 null.</summary>
    public EquipmentInstance GetSlotEquipment(eEquipmentSlot slot)
    {
        _equipped.TryGetValue(slot, out var result);
        return result;
    }

    /// <summary>해당 슬롯에 장비가 착용되어 있는지 여부</summary>
    public bool IsEquipped(eEquipmentSlot slot) => _equipped.ContainsKey(slot);

    #endregion

    /// <summary>
    /// 현재 착용 중인 모든 장비의 강화 레벨을 반영한 최종 보너스를 PlayerStatus에 적용한다.
    /// 장착/해제/강화 시 자동 호출된다.
    /// </summary>
    public void RecalculateStats()
    {
        int totalAtk   = 0;
        int totalMaxHP = 0;
        
        EquipmentStatBlock block = new();
        foreach (var equipment in _equipped.Values)
        {
            equipment.AddStatsTo(block);
        }

        totalAtk = (int)block.Get(EquipmentStatType.AtkFlat);
        totalMaxHP = (int)block.Get(EquipmentStatType.HpFlat);
        
        _playerStatus.SetEquipmentBonus(totalAtk,totalMaxHP);
    }

    //현재는 사용하지 않으나 ChangeJob.cs에서 사용
    public void SetCurrentJob(string name)
    {
        
    }
}
