using System;
using System.Collections.Generic;
using System.Linq;
using KingdomIdle.Balance;

/// <summary>One source for enhancement costs, previewed dismantling and future-drop policy.</summary>
public static class EquipmentEconomy
{
    public static long BaseYield(eEquipmentRarity rarity) => rarity switch
    { eEquipmentRarity.Normal => 1, eEquipmentRarity.Rare => 4, eEquipmentRarity.Epic => 12, _ => 0 };
    public static long EnhanceCost(EquipmentInstance item) => item == null || item.baseData == null || item.IsMaxLevel() ? 0 :
        2 * BaseYield(item.baseData.rarity) * (1 + item.enhancementLevel / 3);
    public static long Yield(EquipmentSave item) => checked(BaseYield(ItemCode.DecodeRarity(item.Code)) + BalanceMath.Floor(item.EnhancementStonesSpent * .8m));
    public static bool IsProtected(EquipmentSave item) => item.Locked || item.Player.HasValue;
    public static bool ShouldAutoDismantle(ProgressionState state, EquipmentSave item) =>
        item.Level == 0 && !IsProtected(item) && EquipmentManager.Instance?.GetData(item.Code) != null &&
        (state.AutoDismantleMask & (1 << (int)ItemCode.DecodeRarity(item.Code))) != 0;

    public sealed class DismantlePlan
    {
        internal readonly List<(string Id, int Code, int Level, long Spent, bool Pending)> Items = new();
        internal readonly List<(int Code, int Level, int Count)> Stacks = new();
        public long Count { get; internal set; }
        public long Stones { get; internal set; }
    }

    public static DismantlePlan Preview(Func<int, bool> acceptsCode, string singleId = null)
    {
        var plan = new DismantlePlan(); var state = LocalProgression.State;
        void Add(IEnumerable<EquipmentSave> items, bool pending)
        {
            foreach (var item in items)
            {
                if (IsProtected(item) || EquipmentManager.Instance?.GetData(item.Code) == null || !acceptsCode(item.Code) ||
                    (singleId != null ? item.Id != singleId : item.Level != 0)) continue;
                plan.Items.Add((item.Id, item.Code, item.Level, item.EnhancementStonesSpent, pending));
                plan.Count++; plan.Stones = checked(plan.Stones + Yield(item));
            }
        }
        Add(state.Equipment, false); Add(state.PendingEquipment, true);
        if (singleId == null)
            foreach (var stack in state.LegacyEquipment)
                if (stack.Level == 0 && acceptsCode(stack.Code) && EquipmentManager.Instance?.GetData(stack.Code) != null)
                {
                    plan.Stacks.Add((stack.Code, stack.Level, stack.Count)); plan.Count += stack.Count;
                    plan.Stones = checked(plan.Stones + BaseYield(ItemCode.DecodeRarity(stack.Code)) * stack.Count);
                }
        return plan;
    }

    public static bool Execute(DismantlePlan plan)
    {
        if (plan == null || plan.Count == 0) return false;
        // Validate the previewed items, not the account revision: battle income can keep
        // advancing while the dialog is open. New drops never join an existing preview.
        return LocalProgression.Execute("equipment-dismantle", state =>
        {
            foreach (var entry in plan.Items)
            {
                var list = entry.Pending ? state.PendingEquipment : state.Equipment;
                var item = list.Find(x => x.Id == entry.Id);
                if (item == null || IsProtected(item) || item.Code != entry.Code || item.Level != entry.Level || item.EnhancementStonesSpent != entry.Spent) return false;
                list.Remove(item);
            }
            foreach (var entry in plan.Stacks)
            {
                var stack = state.LegacyEquipment.Find(x => x.Code == entry.Code && x.Level == entry.Level);
                if (stack == null || stack.Count < entry.Count) return false;
                stack.Count -= entry.Count;
                if (stack.Count == 0) state.LegacyEquipment.Remove(stack);
            }
            LocalProgression.Credit(state, eCurrency.EquipmentStone, plan.Stones);
            return true;
        });
    }
}
