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
    public bool CanReceiveBattleEquipment => LocalProgression.State.PendingEquipment.Count <= PendingCapacity - 25;
    public enum EnhancementResult { Success, ChanceFailed, NotEnoughMaterials, MaxLevel, InvalidItem, SaveFailed }
    private void Awake() { if (Instance != null && Instance != this) { Destroy(gameObject); return; } Instance = this; }
    private void OnDestroy() { if (Instance == this) Instance = null; }
    public void RegisterPlayer(Player player) { if (player != null) _players[player.PlayerIndex] = player; }
    public void ClearPlayer() => _players.Clear();
    public IEnumerable<string> GetCurrentJobNames() => _players.Values.Where(p => p != null).Select(p => p.playerStatus.JobName);
    public List<EquipmentData> GetByRarity(eEquipmentRarity rarity) => _database != null ? _database.GetEquipmentsByRarity(rarity) : new List<EquipmentData>();
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
        if (state.Equipment.Count < Capacity) state.Equipment.Add(item);
        else if (allowPending && state.PendingEquipment.Count < PendingCapacity)
        { item.ExpiresUtc = LocalProgression.UtcNow + 7 * 86400; state.PendingEquipment.Add(item); }
        else return false;
        if (recordQuestProgress) QuestEconomy.Count(state,eQuestObjectiveType.EquipmentObtain,0,1);
        return true;
    }
    public void GetEquipment(EquipmentInstance item, GetEffect effect)
    {
        if (item?.baseData == null) return;
        if (!LocalProgression.Execute("equipment-grant", s => Grant(s, new EquipmentSave { Id = item.instanceId, Code = item.baseData.itemCode,
            Level = item.enhancementLevel, Locked = item.IsLocked }, true))) return;
        RestoreEquipment(); if (effect != GetEffect.None) OnItemDropped?.Invoke(_inventory.Items.FirstOrDefault(x => x.instanceId == item.instanceId));
    }
    private static bool Material(EquipmentInstance target, EquipmentInstance item) => item != null && item != target && item.baseData == target.baseData && !item.IsEquipped && !item.IsLocked && item.enhancementLevel == 0;
    public int GetEnhanceMaterialCount(EquipmentInstance item) => item?.baseData == null ? 0 : _inventory.Items.Count(x => Material(item, x));
    public bool CanEnhance(EquipmentInstance item) => item?.baseData != null && !item.IsMaxLevel() && _inventory.Items.Contains(item) && GetEnhanceMaterialCount(item) >= 2;
    public bool TryEnhance(EquipmentInstance item) => TryEnhanceDetailed(item) == EnhancementResult.Success;
    public EnhancementResult TryEnhanceDetailed(EquipmentInstance item)
    {
        if (item?.baseData == null || !_inventory.Items.Contains(item)) return EnhancementResult.InvalidItem;
        if (item.IsMaxLevel()) return EnhancementResult.MaxLevel;
        if (!CanEnhance(item)) return EnhancementResult.NotEnoughMaterials;
        bool result = LocalProgression.Execute("equipment-enhance", s => {
            var target = s.Equipment.Find(x => x.Id == item.instanceId);
            if (target == null || target.Level >= item.baseData.maxEnhancementLevel) return false;
            var materials = s.Equipment.Where(x => x.Id != target.Id && x.Code == target.Code && !x.Player.HasValue && !x.Locked && x.Level == 0).Take(2).ToArray();
            if (materials.Length != 2) return false;
            foreach (var material in materials) s.Equipment.Remove(material);
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
        bool ok = LocalProgression.Execute("equipment-dismantle", s => {
            var target = s.Equipment.Find(x => x.Id == item.instanceId);
            if (target == null || target.Player.HasValue || target.Locked) return false;
            s.Equipment.Remove(target);
            LocalProgression.Credit(s, eCurrency.ArcaneKnowledge, item.baseData.rarity == eEquipmentRarity.Epic ? 8 : item.baseData.rarity == eEquipmentRarity.Rare ? 3 : 1);
            return true;
        });
        if (ok) { RestoreEquipment(); OnItemDropped?.Invoke(null); Scripts.Core.Manager.StageManager.Instance?.ResumeAfterInventory(); } return ok;
    }
    public bool ClaimPending(string id)
    {
        bool ok = LocalProgression.Execute("equipment-pending", s => {
            var item = s.PendingEquipment.Find(x => x.Id == id);
            if (item == null || s.Equipment.Count >= Capacity) return false;
            // Keep approved rewards even past the displayed expiry until a server expiry policy is supplied.
            s.PendingEquipment.Remove(item); item.ExpiresUtc = 0; s.Equipment.Add(item); return true;
        });
        if (ok) { RestoreEquipment(); OnItemDropped?.Invoke(null); Scripts.Core.Manager.StageManager.Instance?.ResumeAfterInventory(); } return ok;
    }
    public EquipmentSave RollFieldDrop(int stage)
    {
        if (UnityEngine.Random.value >= .02f + .001f * (stage - 1)) return null;
        float roll = UnityEngine.Random.value;
        var items = GetByRarity(roll < .80f ? eEquipmentRarity.Normal : roll < .98f ? eEquipmentRarity.Rare : eEquipmentRarity.Epic);
        if (items.Count == 0) return null;
        return new EquipmentSave { Id = Guid.NewGuid().ToString("N"), Code = items[UnityEngine.Random.Range(0, items.Count)].itemCode };
    }
    // Rewards now originate from an identified stage kill, never from the attacker.
    public void TryDropEquipment() { }
    public EquipmentInstance TrySynthesize(List<EquipmentInstance> equipment) => null;
}
