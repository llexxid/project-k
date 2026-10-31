using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 각 플레이어(캐릭터)의 장비 슬롯 표시와 스탯 반영을 담당한다.
/// 장착/해제는 EquipmentManager가 서버(OnEquipEquipment/OnUnequipEquipment)에 요청하고, 응답이 적용되면 RestoreSelection으로 슬롯이 갱신된다.
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
    /// 장비 착용을 서버에 요청한다. 같은 슬롯에 장비가 이미 있으면 서버가 교체한다.
    /// 착용한 장비는 인벤토리에서 제거하지 않는다 (인벤토리에 남아있는 채로 착용 표시).
    /// </summary>
    public EquipmentManager.EnhancementResult Equip(EquipmentInstance equipment)
    {
        return EquipmentManager.Instance != null ? EquipmentManager.Instance.TryEquipEx(_playerIndex, equipment) : EquipmentManager.EnhancementResult.NetworkNotReady;
    }

    /// <summary>지정 슬롯의 장비 해제를 서버에 요청한다.</summary>
    public EquipmentManager.EnhancementResult Unequip(eEquipmentSlot slot)
    {
        return EquipmentManager.Instance != null ? EquipmentManager.Instance.UnequipEx(_playerIndex) : EquipmentManager.EnhancementResult.NetworkNotReady;
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

    /// <summary>직업이 바뀌어 현재 무기를 쓸 수 없으면 서버에 해제를 요청한다. 서버는 비호환 장비를 자동으로 벗기지 않는다.</summary>
    public void SetCurrentJob(string name)
    {
        var item = GetSlotEquipment(eEquipmentSlot.Weapon);
        if (item != null && !item.baseData.IsAllowedForJob(name)) Unequip(eEquipmentSlot.Weapon);
    }
    internal void RestoreSelection(EquipmentInstance item)
    {
        _equipped.Clear();
        if (item != null) _equipped[eEquipmentSlot.Weapon] = item;
        RecalculateStats();
    }
}
