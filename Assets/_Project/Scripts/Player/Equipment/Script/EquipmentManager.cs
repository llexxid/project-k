using System;
using System.Collections.Generic;
using System.Linq;
using KingdomIdle.Balance;
using UnityEngine;

public enum GetEffect { None, Gacha, Popup }
public class EquipmentManager : MonoBehaviour
{
    public static EquipmentManager Instance;
    public event Action<EquipmentInstance> OnItemDropped, OnEnhanced, OnEnhanceFailed, OnSynthesized;
    public event Action<Player, eEquipmentSlot, EquipmentInstance> OnEquipped, OnUnequipped;
    [SerializeField] private EquipmentDatabase _database;
    [SerializeField] private EquipmentDropTableSO _dropTable;
    private EquipmentInventory _inventory = new();
    private readonly Dictionary<int, Player> _players = new();
    public EquipmentInventory Inventory => _inventory;
    public const int Capacity = 300, PendingCapacity = 100;
    public int AvailableSlots => Math.Max(0, Capacity - LocalProgression.State.Equipment.Count);
    public enum EnhancementResult { Success, ChanceFailed, NotEnoughMaterials, MaxLevel, InvalidItem, SaveFailed }
    private void Awake() { if (Instance != null && Instance != this) { Destroy(gameObject); return; } Instance = this; }
    private void OnDestroy() { if (Instance == this) Instance = null; }
    public void RegisterPlayer(Player player) { if (player != null) _players[player.PlayerIndex] = player; }
    public void ClearPlayer() => _players.Clear();
    public IEnumerable<string> GetCurrentJobNames() => _players.Values.Where(p => p != null).Select(p => p.playerStatus.JobName);
    public List<EquipmentData> GetByRarity(eEquipmentRarity rarity) => _database != null ? _database.GetEquipmentsByRarity(rarity) : new List<EquipmentData>();
    public List<EquipmentData> GetRewardPool(eEquipmentRarity rarity) => GetByRarity(rarity).FindAll(item => item.CanBeRewarded);
    public EquipmentData GetData(int code) => _database?.GetEquipmentByCode(code);
    public void RestoreEquipment()
    {
        var previous = _inventory.Items.ToDictionary(x => x.instanceId);
        var next = new EquipmentInventory();
        foreach (var saved in LocalProgression.State.Equipment)
        {
            var data = GetData(saved.Code);
            if (data == null) { Debug.LogError("[Equipment] Preserved unknown item code: " + saved.Code); continue; }
            if (!previous.TryGetValue(saved.Id, out var item)) item = new EquipmentInstance(data, saved.Id);
            item.enhancementLevel = saved.Level; item.equipmentPlayerIndex = saved.Player; item.IsLocked = saved.Locked;
            next.Add(item);
        }
        _inventory = next;
        foreach (var pair in _players)
            pair.Value?.PlayerEquipmentManager?.RestoreSelection(_inventory.Items.FirstOrDefault(x => x.equipmentPlayerIndex == pair.Key));
    }
    /// <summary>실제 지급만 획득 이벤트로 센다. 저장 이관은 recordQuestProgress=false로 보유 상태만 복구한다.</summary>
    public static bool Grant(ProgressionState state, EquipmentSave item, bool allowPending, bool recordQuestProgress = true)
    {
        if (state.Equipment.Any(x => x.Id == item.Id) || state.PendingEquipment.Any(x => x.Id == item.Id)) return false;
        if (EquipmentEconomy.ShouldAutoDismantle(state, item)) LocalProgression.Credit(state, eCurrency.EquipmentStone, EquipmentEconomy.Yield(item));
        else if (state.Equipment.Count < Capacity) state.Equipment.Add(item);
        else if (allowPending && state.PendingEquipment.Count < PendingCapacity)
        { item.ExpiresUtc = 0; state.PendingEquipment.Add(item); }
        else if (allowPending && !item.Locked && !item.Player.HasValue)
        {
            var stack = state.LegacyEquipment.Find(x => x.Code == item.Code && x.Level == item.Level);
            if (stack == null) state.LegacyEquipment.Add(new LegacyEquipmentStack { Code = item.Code, Level = item.Level, Count = 1 });
            else stack.Count = checked(stack.Count + 1);
        }
        else return false;
        if (recordQuestProgress) QuestEconomy.Count(state,eQuestObjectiveType.EquipmentObtain,0,1);
        return true;
    }
    // Old accounts stored quantities instead of individual instances. Reserve the excess
    // by code/level so a large owned stack cannot block login or allocate thousands of objects.
    // This reserve never expires and does not consume the live battle-reward inbox.
    // Restoring already-owned items is not a new quest acquisition; only Grant records that event.
    public static void ImportLegacy(ProgressionState state, int code, int level, int amount)
    {
        if (amount < 0 || level < 0 || level > 15) throw new ArgumentOutOfRangeException();
        int direct = Math.Min(amount, Math.Max(0, Capacity - state.Equipment.Count));
        for (int n = 0; n < direct; n++)
            state.Equipment.Add(new EquipmentSave { Id = Guid.NewGuid().ToString("N"), Code = code, Level = level });
        int remainder = amount - direct;
        if (remainder > 0)
        {
            var stack = state.LegacyEquipment.Find(x => x.Code == code && x.Level == level);
            if (stack == null) state.LegacyEquipment.Add(new LegacyEquipmentStack { Code = code, Level = level, Count = remainder });
            else stack.Count = checked(stack.Count + remainder);
        }
    }
    public static bool TakeLegacy(ProgressionState state, int code, int level)
    {
        var stack = state.LegacyEquipment.Find(x => x.Code == code && x.Level == level);
        if (stack == null || stack.Count <= 0 || state.Equipment.Count >= Capacity) return false;
        state.Equipment.Add(new EquipmentSave { Id = Guid.NewGuid().ToString("N"), Code = code, Level = level });
        if (--stack.Count == 0) state.LegacyEquipment.Remove(stack);
        return true;
    }
    public bool ClaimLegacy(int code, int level)
    {
        if (GetData(code) == null) return false;
        bool ok = LocalProgression.Execute("equipment-legacy-claim", state => TakeLegacy(state, code, level));
        if (ok) { RestoreEquipment(); OnItemDropped?.Invoke(null); }
        return ok;
    }
    public void GetEquipment(EquipmentInstance item, GetEffect effect)
    {
        if (item?.baseData == null) return;
        if (!LocalProgression.Execute("equipment-grant", s => Grant(s, new EquipmentSave { Id = item.instanceId, Code = item.baseData.itemCode,
            Level = item.enhancementLevel, Locked = item.IsLocked }, true))) return;
        RestoreEquipment(); if (effect != GetEffect.None) OnItemDropped?.Invoke(_inventory.Items.FirstOrDefault(x => x.instanceId == item.instanceId));
    }
    public bool CanEnhance(EquipmentInstance item) => item?.baseData != null && !item.IsMaxLevel() && _inventory.Items.Contains(item) && LocalProgression.Balance(eCurrency.EquipmentStone) >= EquipmentEconomy.EnhanceCost(item);
    public bool TryEnhance(EquipmentInstance item) => TryEnhanceDetailed(item) == EnhancementResult.Success;
    public EnhancementResult TryEnhanceDetailed(EquipmentInstance item)
    {
        if (item?.baseData == null || !_inventory.Items.Contains(item)) return EnhancementResult.InvalidItem;
        if (item.IsMaxLevel()) return EnhancementResult.MaxLevel;
        if (!CanEnhance(item)) return EnhancementResult.NotEnoughMaterials;
        bool result = LocalProgression.Execute("equipment-enhance", s => {
            var target = s.Equipment.Find(x => x.Id == item.instanceId);
            if (target == null || target.Level >= item.baseData.maxEnhancementLevel) return false;
            long cost = EquipmentEconomy.EnhanceCost(item);
            if (cost <= 0 || target.Level != item.enhancementLevel || !LocalProgression.Spend(s, eCurrency.EquipmentStone, cost)) return false;
            target.EnhancementStonesSpent = checked(target.EnhancementStonesSpent + cost);
            target.Level++; return true;
        });
        if (!result) return EnhancementResult.SaveFailed;
        RestoreEquipment(); OnEnhanced?.Invoke(item); return EnhancementResult.Success;
    }
    public bool TryEquip(int playerIndex, EquipmentInstance item)
    {
        if (item?.baseData == null || !_players.TryGetValue(playerIndex, out var player) || !item.baseData.IsAllowedForJob(player.playerStatus.JobName)) return false;
        var previous = player.PlayerEquipmentManager.GetSlotEquipment(eEquipmentSlot.Weapon);
        bool result = LocalProgression.Execute("equipment-equip", s => {
            var target = s.Equipment.Find(x => x.Id == item.instanceId);
            if (target == null || target.Player.HasValue) return false;
            foreach (var entry in s.Equipment) if (entry.Player == playerIndex) entry.Player = null;
            target.Player = playerIndex; return true;
        });
        if (!result) return false;
        RestoreEquipment(); if (previous != null) OnUnequipped?.Invoke(player, eEquipmentSlot.Weapon, previous);
        OnEquipped?.Invoke(player, eEquipmentSlot.Weapon, item); return true;
    }
    public void Unequip(int playerIndex)
    {
        if (!_players.TryGetValue(playerIndex, out var player)) return;
        var item = player.PlayerEquipmentManager.GetSlotEquipment(eEquipmentSlot.Weapon);
        if (item == null) return;
        if (!LocalProgression.Execute("equipment-unequip", s => { var target = s.Equipment.Find(x => x.Id == item.instanceId); if (target == null) return false; target.Player = null; return true; })) return;
        RestoreEquipment(); OnUnequipped?.Invoke(player, eEquipmentSlot.Weapon, item);
    }
    public bool SetLocked(EquipmentInstance item, bool locked)
    {
        bool ok = LocalProgression.Execute("equipment-lock", s => { var target = s.Equipment.Find(x => x.Id == item?.instanceId); if (target == null) return false; target.Locked = locked; return true; });
        if (ok) RestoreEquipment(); return ok;
    }
    public bool Dismantle(EquipmentInstance item)
    {
        if (item?.baseData == null) return false;
        return Dismantle(EquipmentEconomy.Preview(_ => true, item.instanceId));
    }
    public bool Dismantle(EquipmentEconomy.DismantlePlan plan)
    { bool ok = EquipmentEconomy.Execute(plan); if (ok) { RestoreEquipment(); OnItemDropped?.Invoke(null); } return ok; }
    public bool ClaimPending(string id)
    {
        bool ok = LocalProgression.Execute("equipment-pending", s => {
            var item = s.PendingEquipment.Find(x => x.Id == id);
            if (item == null || s.Equipment.Count >= Capacity) return false;
            // Keep approved rewards even past the displayed expiry until a server expiry policy is supplied.
            s.PendingEquipment.Remove(item); item.ExpiresUtc = 0; s.Equipment.Add(item); return true;
        });
        if (ok) { RestoreEquipment(); OnItemDropped?.Invoke(null); } return ok;
    }
    public EquipmentSave RollFieldDrop(double probability)
    {
        if (UnityEngine.Random.value >= probability) return null;
        float roll = UnityEngine.Random.value;
        var items = GetRewardPool(roll < .80f ? eEquipmentRarity.Normal : roll < .98f ? eEquipmentRarity.Rare : eEquipmentRarity.Epic);
        if (items.Count == 0) return null;
        return new EquipmentSave { Id = Guid.NewGuid().ToString("N"), Code = items[UnityEngine.Random.Range(0, items.Count)].itemCode };
    }
    // Rewards now originate from an identified stage kill, never from the attacker.
    public void TryDropEquipment() { }
    public EquipmentInstance TrySynthesize(List<EquipmentInstance> equipment) => null;
}
