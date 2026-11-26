using System;
using System.Collections.Generic;
using System.Linq;
using KingdomIdle.MageTower;
using Protocol.Message.Dto;
using Scripts.Core;
using Scripts.Core.Manager;

namespace KingdomIdle.Balance
{
    public readonly struct EconomyProjection
    {
        public long Gold => Balance(eCurrency.Gold);
        public long Ruby => Balance(eCurrency.Ruby);
        public long ArcaneKnowledge => Balance(eCurrency.ArcaneKnowledge);
        public long EquipmentStone => Balance(eCurrency.EquipmentStone);
        public long Balance(eCurrency currency) => _wallet?.GetValueOrDefault(currency) ?? 0;
        public readonly int AttackLevel, HealthLevel, RubyGoldLevel, RubyExpLevel;
        public readonly bool Valid;
        private readonly Dictionary<eCurrency, long> _wallet;
        private readonly int[] _mageLevels, _awakeningLevels, _fragments;
        private readonly List<EquipmentSave> _inventory;
        private readonly Dictionary<long,int> _equipmentLevels;
        internal EquipmentSave FindEquipment(long? id) => id.HasValue ? _inventory?.Find(x => x.ServerInstanceId == id) : null;
        public int Fragments(int skillId) => _fragments != null && MageSkillRules.IsAvailable(skillId) ? _fragments[skillId] : 0;
        internal EconomyProjection(Dictionary<eCurrency, long> wallet, int attack, int health, int rubyGold, int rubyExp,
            int[] mageLevels, int[] awakeningLevels, int[] fragments, bool valid, List<EquipmentSave> inventory, Dictionary<long,int> equipmentLevels)
        {
            _wallet = wallet;
            AttackLevel = attack; HealthLevel = health; RubyGoldLevel = rubyGold; RubyExpLevel = rubyExp;
            _mageLevels = mageLevels; _awakeningLevels = awakeningLevels; _fragments = fragments;
            Valid = valid; _inventory = inventory; _equipmentLevels = equipmentLevels;
        }
        public int Level(EconomyCommandKind kind, int? skillId = null, long? gearId = null) => kind switch
        {
            EconomyCommandKind.GoldAttack => AttackLevel, EconomyCommandKind.GoldHealth => HealthLevel,
            EconomyCommandKind.RubyGold => RubyGoldLevel, EconomyCommandKind.RubyExperience => RubyExpLevel,
            EconomyCommandKind.EquipmentEnhance when gearId.HasValue => _equipmentLevels != null && _equipmentLevels.TryGetValue(gearId.Value,out int level)
                ? level : FindEquipment(gearId)?.Level ?? -1,
            EconomyCommandKind.MageEnhance when _mageLevels != null && skillId.HasValue && MageSkillRules.IsAvailable(skillId.Value)
                => _mageLevels[skillId.Value],
            EconomyCommandKind.MageAwakening when _awakeningLevels != null && skillId.HasValue && MageSkillRules.IsAvailable(skillId.Value)
                => _awakeningLevels[skillId.Value],
            _ => -1
        };
    }

    internal static class EconomyProjectionMath
    {
        internal static EconomyCommandDTO Quote(EconomyProjection p, EconomyCommandKind kind, int mode, int? skillId = null, long? gearId = null)
        {
            int level = p.Level(kind, skillId, gearId);
            bool gold = kind == EconomyCommandKind.GoldAttack || kind == EconomyCommandKind.GoldHealth;
            if (!p.Valid || level < 0 || (kind == EconomyCommandKind.EquipmentEnhance) != gearId.HasValue ||
                (kind is EconomyCommandKind.MageEnhance or EconomyCommandKind.MageAwakening) != skillId.HasValue ||
                (gold ? mode != 1 && mode != 10 && mode != -1 : mode != 1)) return null;
            int count = gold ? mode == -1 ? BalanceMath.AffordableGoldLevels(level, p.Gold) : Math.Min(mode, BalanceMath.GoldCap - level)
                : 1;
            if (count <= 0) return null;
            var cost = AffordableCost(p, kind, level, count, skillId, gearId);
            if (!cost.HasValue) return null;
            return new EconomyCommandDTO { Kind = kind, SkillId = skillId, EquipmentInstanceId = gearId, Mode = mode, ExpectedLevel = level, CountLimit = count, SpendLimit = cost.Value };
        }

        private static long? AffordableCost(EconomyProjection p, EconomyCommandKind kind, int level, int count, int? skillId, long? gearId)
        {
            long? cost;
            long balance;
            switch (kind)
            {
                case EconomyCommandKind.GoldAttack:
                case EconomyCommandKind.GoldHealth: cost = BalanceMath.GoldTotal(level, count); balance = p.Gold; break;
                case EconomyCommandKind.RubyGold:
                case EconomyCommandKind.RubyExperience: cost = BalanceMath.RubyCost(level); balance = p.Ruby; break;
                case EconomyCommandKind.EquipmentEnhance:
                    var gear = p.FindEquipment(gearId);
                    if (gear == null) return null;
                    var rarity = ItemCode.DecodeRarity(gear.Code);
                    if (level >= EquipmentEconomy.EnhanceCap(rarity)) return null;
                    cost = EquipmentEconomy.EnhanceCost(rarity, level); balance = p.EquipmentStone; break;
                case EconomyCommandKind.MageEnhance: cost = BalanceMath.MageCost(level); balance = p.ArcaneKnowledge; break;
                case EconomyCommandKind.MageAwakening:
                    if (level >= MageSkillRules.BloomAwakening || !skillId.HasValue) return null;
                    cost = level + 1; balance = p.Fragments(skillId.Value); break;
                default: return null;
            }
            return cost <= balance ? cost : null;
        }

        internal static EconomyProjection Calculate(ProgressionState state, IEnumerable<EconomyCommandDTO> commands,
            Func<long, StageDefinition> definition = null, Func<QuestClaimTargetDTO, QuestRowDTO> questRow = null,
            bool allowUnavailable = false)
        {
            var wallet = new Dictionary<eCurrency, long>(state.Wallet);
            Dictionary<long,int> equipmentLevels = null;
            var mageLevels = new int[MageSkillRules.IdCapacity];
            var awakenings = new int[mageLevels.Length];
            var fragments = new int[mageLevels.Length];
            for (int id = 0; id < mageLevels.Length; id++)
            {
                state.MageSkills.TryGetValue(id, out var skill);
                mageLevels[id] = skill?.Enhance ?? -1;
                awakenings[id] = skill?.Awaken ?? -1;
                fragments[id] = skill?.Fragments ?? 0;
            }
            int attack = state.AttackLevel, health = state.HealthLevel, rg = state.RubyGoldLevel, re = state.RubyExpLevel;
            bool valid = true;
            var accepted = new Dictionary<string, Dictionary<ulong, int>>();
            if (state.MainBattle != null) accepted[state.MainBattle.RunId] = new(state.MainBattle.AcceptedKills);
            if (state.Economy?.Dungeon != null) accepted[state.Economy.Dungeon.BattleId] = EconomyCopies.Counts(state.Economy.Dungeon.AcceptedKills);
            long remainder = state.Economy?.Server?.GoldRemainderMicros ?? 0;
            var questGold = state.Economy?.Server?.QuestGoldPreview;
            var claimed = new HashSet<(long, string)>();
            try
            {
                foreach (var command in commands)
                {
                    if (command.Kind == EconomyCommandKind.Hunt)
                    {
                        string run = command.MainRunId ?? command.BattleId;
                        var epoch = state.Economy?.Server?.RewardEpochs?.FirstOrDefault(x => x.Id == command.RewardEpoch);
                        var stage = definition != null ? definition((long)command.Stage) : GetDefinition((long)command.Stage);
                        if (run == null || epoch == null || stage == null || !accepted.TryGetValue(run, out var counts)) { valid = false; break; }
                        long basis = 0;
                        foreach (var total in command.TotalKills)
                        {
                            ulong type = (ulong)total.MonsterType;
                            int before = counts.GetValueOrDefault(type), remaining = Math.Max(0, total.Count - before), skip = before;
                            foreach (var row in stage.MonsterEntries.Where(x => (ulong)x.MonsterType == type))
                            {
                                int available = Math.Max(0, row.Count - skip);
                                skip = Math.Max(0, skip - row.Count);
                                int take = Math.Min(remaining, available);
                                remaining -= take;
                                basis = checked(basis + take * row.Combat.Numbers.Gold);
                            }
                            if (remaining != 0) { valid = false; break; }
                            counts[type] = Math.Max(before, total.Count);
                        }
                        if (!valid) break;
                        wallet[eCurrency.Gold] = checked(wallet.GetValueOrDefault(eCurrency.Gold) +
                            BalanceMath.WithRemainder(basis, epoch.GoldLevel, ref remainder));
                        continue;
                    }
                    if (command.Kind == EconomyCommandKind.QuestClaim)
                    {
                        bool any = false;
                        foreach (var target in command.QuestClaim.Targets)
                        {
                            var row = questRow?.Invoke(target);
                            if (row?.State != Protocol.Message.Dto.QuestRowState.Claimable || claimed.Contains((target.QuestId, target.PeriodKey)) ||
                                !TryProjectQuest(row, wallet, questGold, rg, ref remainder))
                                break;
                            claimed.Add((target.QuestId, target.PeriodKey));
                            any = true;
                        }
                        if (!any && !allowUnavailable) { valid = false; break; }
                        continue;
                    }
                    var p = new EconomyProjection(wallet, attack, health, rg, re, mageLevels, awakenings, fragments, true, state.Equipment, equipmentLevels);
                    int level = p.Level(command.Kind, command.SkillId, command.EquipmentInstanceId);
                    bool isGold = command.Kind == EconomyCommandKind.GoldAttack || command.Kind == EconomyCommandKind.GoldHealth;
                    if (level < 0 || level != command.ExpectedLevel ||
                        (command.Kind is EconomyCommandKind.MageEnhance or EconomyCommandKind.MageAwakening) != command.SkillId.HasValue ||
                        (!isGold && (command.Mode != 1 || command.CountLimit != 1)))
                    { if (allowUnavailable) continue; valid = false; break; }
                    int count = isGold ? Math.Min(command.CountLimit, BalanceMath.GoldCap - level) : 1;
                    if (isGold && command.Mode == -1) count = Math.Min(count, BalanceMath.AffordableGoldLevels(level, Math.Min(p.Gold, command.SpendLimit)));
                    var cost = count > 0 ? AffordableCost(p, command.Kind, level, count, command.SkillId, command.EquipmentInstanceId) : null;
                    if (!cost.HasValue || cost.Value > command.SpendLimit)
                    { if (allowUnavailable) continue; valid = false; break; }
                    switch (command.Kind)
                    {
                        case EconomyCommandKind.GoldAttack: wallet[eCurrency.Gold] -= cost.Value; attack += count; break;
                        case EconomyCommandKind.GoldHealth: wallet[eCurrency.Gold] -= cost.Value; health += count; break;
                        case EconomyCommandKind.RubyGold: wallet[eCurrency.Ruby] -= cost.Value; rg++; break;
                        case EconomyCommandKind.RubyExperience: wallet[eCurrency.Ruby] -= cost.Value; re++; break;
                        case EconomyCommandKind.EquipmentEnhance:
                            (equipmentLevels ??= new Dictionary<long,int>())[command.EquipmentInstanceId.Value] = level + 1;
                            wallet[eCurrency.EquipmentStone] -= cost.Value; break;
                        case EconomyCommandKind.MageEnhance: wallet[eCurrency.ArcaneKnowledge] -= cost.Value; mageLevels[command.SkillId.Value]++; break;
                        case EconomyCommandKind.MageAwakening:
                            int skillId = command.SkillId.Value;
                            fragments[skillId] -= (int)cost.Value;
                            awakenings[skillId]++;
                            break;
                        default: valid = false; break;
                    }
                    if (!valid) break;
                }
            }
            catch (OverflowException) { valid = false; }
            return new EconomyProjection(wallet, attack, health, rg, re, mageLevels, awakenings, fragments, valid, state.Equipment, equipmentLevels);
        }

        private static bool TryProjectQuest(QuestRowDTO row, Dictionary<eCurrency, long> wallet, QuestGoldPreviewDTO gold,
            int rubyGold, ref long remainder)
        {
            var credits = new Dictionary<eCurrency, long>();
            long nextRemainder = remainder;
            foreach (var reward in row.Rewards)
            {
                if (reward?.Kind != RewardKind.Currency || !reward.CurrencyType.HasValue) continue;
                var currency = (eCurrency)(int)reward.CurrencyType.Value;
                if (!Enum.IsDefined(typeof(eCurrency), currency)) return false;
                decimal amount = reward.Amount;
                if (reward.IsDynamicGold)
                {
                    if (currency != eCurrency.Gold || gold == null) return false;
                    amount = gold.BaseAmount;
                    if (gold.UsesRubyMultiplier) amount *= BalanceMath.RubyMultiplier(rubyGold);
                }
                if (currency == eCurrency.Gold)
                {
                    amount += nextRemainder / 1000000m;
                    nextRemainder = (long)((amount - decimal.Floor(amount)) * 1000000m);
                }
                if (amount < 0 || decimal.Floor(amount) > long.MaxValue) return false;
                long credit = BalanceMath.Floor(amount);
                long old = credits.GetValueOrDefault(currency);
                if (credit > long.MaxValue - old - wallet.GetValueOrDefault(currency)) return false;
                credits[currency] = old + credit;
            }
            foreach (var credit in credits) wallet[credit.Key] = wallet.GetValueOrDefault(credit.Key) + credit.Value;
            remainder = nextRemainder;
            return true;
        }

        private static StageDefinition GetDefinition(long stage) => StageManager.Instance != null &&
            StageManager.Instance.TryGetStageDefinition((eStage)stage, out var definition) ? definition : null;
    }
}
