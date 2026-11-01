using System;
using System.Collections.Generic;
using System.Linq;
using KingdomIdle.Balance;
using Dto = Protocol.Message.Dto;

/// <summary>
/// 장비 비용·환급 표시 공식과 분해 요청 구성. 수치는 서버와 같은 공식(기본 환급 1/4/12, 비용 2×기본×(1+단계/3), 환급 80%)이지만
/// 표시용이다. 실제 차감·지급은 서버 응답(SpentEquipmentStone·GrantedEquipmentStone·CurrentEquipmentStone)이 확정한다.
/// </summary>
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

    /// <summary>분해 요청 명세. Items/ReserveStacks는 서버 OnDismantleEquipment 요청에 그대로 들어가고, Count/Stones는 확인 창 표시용이다.</summary>
    public sealed class DismantlePlan
    {
        public Dto.DismantleMode Mode { get; internal set; }
        public List<Dto.DismantleItemDTO> Items { get; } = new();
        public List<Dto.DismantleReserveDTO> ReserveStacks { get; } = new();
        public long Count { get; internal set; }
        public long Stones { get; internal set; }
    }

    /// <summary>
    /// singleId가 있으면 그 장비 1개(Single), 없으면 잠금·장착되지 않은 +0 장비 전부와 예비 보관(Bulk)을 분해 대상으로 묶는다.
    /// 서버 규칙: Single은 1개·수량 1, Bulk는 강화 0만, 잠금·장착분은 1019로 거절된다. 예비는 잠긴 묶음을 제외한다.
    /// </summary>
    public static DismantlePlan Preview(Func<int, bool> acceptsCode, string singleId = null)
    {
        var plan = new DismantlePlan { Mode = singleId != null ? Dto.DismantleMode.Single : Dto.DismantleMode.Bulk };
        var state = LocalProgression.State;
        var groups = new Dictionary<(int Code, int Level, long Expires, Dto.EquipmentLocation Location), (int Count, long Stones)>();
        void Add(IEnumerable<EquipmentSave> items, Dto.EquipmentLocation location)
        {
            foreach (var item in items)
            {
                if (IsProtected(item) || EquipmentManager.Instance?.GetData(item.Code) == null || !acceptsCode(item.Code) ||
                    (singleId != null ? item.Id != singleId : item.Level != 0)) continue;
                var key = (item.Code, item.Level, item.ExpiresUtc, location);
                groups.TryGetValue(key, out var group);
                groups[key] = (group.Count + 1, checked(group.Stones + Yield(item)));
                plan.Count++;
            }
        }
        Add(state.Equipment, Dto.EquipmentLocation.Inventory); Add(state.PendingEquipment, Dto.EquipmentLocation.Pending);
        foreach (var pair in groups)
        {
            int quantity = Math.Min(0xFFFF, pair.Value.Count);
            plan.Items.Add(new Dto.DismantleItemDTO { ItemCode = EquipmentInstance.MakeItemCode(pair.Key.Code, pair.Key.Level, pair.Key.Expires, quantity), Location = pair.Key.Location });
            plan.Stones = checked(plan.Stones + pair.Value.Stones);
        }
        if (singleId == null)
            foreach (var stack in state.LegacyEquipment)
                if (stack.Level == 0 && !stack.Locked && stack.Count > 0 && acceptsCode(stack.Code) && EquipmentManager.Instance?.GetData(stack.Code) != null)
                {
                    plan.ReserveStacks.Add(new Dto.DismantleReserveDTO { ItemCode = EquipmentInstance.MakeItemCode(stack.Code, 0, 0, 0), Count = stack.Count });
                    plan.Count += stack.Count;
                    plan.Stones = checked(plan.Stones + BaseYield(ItemCode.DecodeRarity(stack.Code)) * stack.Count);
                }
        return plan;
    }
}
