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
        public readonly long Gold, Ruby, ArcaneKnowledge, EquipmentStone;
        public readonly int AttackLevel, HealthLevel, RubyGoldLevel, RubyExpLevel;
        public readonly bool Valid;
        private readonly int[] _mageLevels;
        private readonly List<EquipmentSave> _inventory;
        private readonly Dictionary<long,int> _equipmentLevels;
        internal EquipmentSave FindEquipment(long? id) => id.HasValue ? _inventory?.Find(x => x.ServerInstanceId == id) : null;
        internal EconomyProjection(long gold, long ruby, long knowledge, int attack, int health, int rubyGold, int rubyExp,
            int[] mageLevels, bool valid, long stones = 0, List<EquipmentSave> inventory = null, Dictionary<long,int> equipmentLevels = null)
        {
            Gold = gold; Ruby = ruby; ArcaneKnowledge = knowledge;
            AttackLevel = attack; HealthLevel = health; RubyGoldLevel = rubyGold; RubyExpLevel = rubyExp;
            _mageLevels = mageLevels; Valid = valid; EquipmentStone = stones; _inventory = inventory; _equipmentLevels = equipmentLevels;
        }
        public int Level(EconomyCommandKind kind, int? skillId = null, long? gearId = null) => kind switch
        {
            EconomyCommandKind.GoldAttack => AttackLevel, EconomyCommandKind.GoldHealth => HealthLevel,
            EconomyCommandKind.RubyGold => RubyGoldLevel, EconomyCommandKind.RubyExperience => RubyExpLevel,
            EconomyCommandKind.EquipmentEnhance when gearId.HasValue => _equipmentLevels != null && _equipmentLevels.TryGetValue(gearId.Value,out int level)
                ? level : FindEquipment(gearId)?.Level ?? -1,
            EconomyCommandKind.MageEnhance when _mageLevels != null && skillId.HasValue && MageSkillRules.IsAvailable(skillId.Value)
                => _mageLevels[skillId.Value],
            _ => -1
        };
    }

    internal static class EconomyProjectionMath
    {
        internal static EconomyCommandDTO Quote(EconomyProjection p, EconomyCommandKind kind, int mode, int? skillId = null, long? gearId = null)
        {
            int level = p.Level(kind, skillId, gearId);
            bool gold = kind == EconomyCommandKind.GoldAttack || kind == EconomyCommandKind.GoldHealth;
            if (!p.Valid || level < 0 || (kind == EconomyCommandKind.EquipmentEnhance) != gearId.HasValue || (kind == EconomyCommandKind.MageEnhance) != skillId.HasValue ||
                (gold ? mode != 1 && mode != 10 && mode != -1 : mode != 1)) return null;
            int count = gold ? mode == -1 ? BalanceMath.AffordableGoldLevels(level, p.Gold) : Math.Min(mode, BalanceMath.GoldCap - level)
                : 1;
            if (count <= 0) return null;
            var cost = AffordableCost(p, kind, level, count, gearId);
            if (!cost.HasValue) return null;
            return new EconomyCommandDTO { Kind = kind, SkillId = skillId, EquipmentInstanceId = gearId, Mode = mode, ExpectedLevel = level, CountLimit = count, SpendLimit = cost.Value };
        }

        private static long? AffordableCost(EconomyProjection p, EconomyCommandKind kind, int level, int count, long? gearId)
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
                default: return null;
            }
            return cost <= balance ? cost : null;
        }

        internal static EconomyProjection Calculate(ProgressionState state, IEnumerable<EconomyCommandDTO> commands,
            Func<long, StageDefinition> definition = null)
        {
            long gold = state.Wallet.GetValueOrDefault(eCurrency.Gold), ruby = state.Wallet.GetValueOrDefault(eCurrency.Ruby);
            long knowledge = state.Wallet.GetValueOrDefault(eCurrency.ArcaneKnowledge);
            long stones = state.Wallet.GetValueOrDefault(eCurrency.EquipmentStone);
            Dictionary<long,int> equipmentLevels = null;
            var mageLevels = new int[MageSkillRules.IdCapacity];
            for (int id = 0; id < mageLevels.Length; id++)
                mageLevels[id] = state.MageSkills.TryGetValue(id, out var skill) ? skill.Enhance : -1;
            int attack = state.AttackLevel, health = state.HealthLevel, rg = state.RubyGoldLevel, re = state.RubyExpLevel;
            bool valid = true;
            var accepted = new Dictionary<string, Dictionary<ulong, int>>();
            if (state.MainBattle != null) accepted[state.MainBattle.RunId] = new(state.MainBattle.AcceptedKills);
            if (state.Economy?.Dungeon != null) accepted[state.Economy.Dungeon.BattleId] = EconomyCopies.Counts(state.Economy.Dungeon.AcceptedKills);
            long remainder = state.Economy?.Server?.GoldRemainderMicros ?? 0;
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
                        gold = checked(gold + BalanceMath.WithRemainder(basis, epoch.GoldLevel, ref remainder));
                        continue;
                    }
                    var p = new EconomyProjection(gold, ruby, knowledge, attack, health, rg, re, mageLevels, true, stones, state.Equipment, equipmentLevels);
                    int level = p.Level(command.Kind, command.SkillId, command.EquipmentInstanceId);
                    bool isGold = command.Kind == EconomyCommandKind.GoldAttack || command.Kind == EconomyCommandKind.GoldHealth;
                    if (level < 0 || level != command.ExpectedLevel || (command.Kind == EconomyCommandKind.MageEnhance) != command.SkillId.HasValue ||
                        (!isGold && (command.Mode != 1 || command.CountLimit != 1))) { valid = false; break; }
                    int count = isGold ? Math.Min(command.CountLimit, BalanceMath.GoldCap - level) : 1;
                    if (isGold && command.Mode == -1) count = Math.Min(count, BalanceMath.AffordableGoldLevels(level, Math.Min(gold, command.SpendLimit)));
                    if (count <= 0) { valid = false; break; }
                    var cost = AffordableCost(p, command.Kind, level, count, command.EquipmentInstanceId);
                    if (!cost.HasValue || cost.Value > command.SpendLimit) { valid = false; break; }
                    switch (command.Kind)
                    {
                        case EconomyCommandKind.GoldAttack: gold -= cost.Value; attack += count; break;
                        case EconomyCommandKind.GoldHealth: gold -= cost.Value; health += count; break;
                        case EconomyCommandKind.RubyGold: ruby -= cost.Value; rg++; break;
                        case EconomyCommandKind.RubyExperience: ruby -= cost.Value; re++; break;
                        case EconomyCommandKind.EquipmentEnhance:
                            (equipmentLevels ??= new Dictionary<long,int>())[command.EquipmentInstanceId.Value] = level + 1;
                            stones -= cost.Value; break;
                        case EconomyCommandKind.MageEnhance: knowledge -= cost.Value; mageLevels[command.SkillId.Value]++; break;
                        default: valid = false; break;
                    }
                    if (!valid) break;
                }
            }
            catch (OverflowException) { valid = false; }
            return new EconomyProjection(gold, ruby, knowledge, attack, health, rg, re, mageLevels, valid, stones, state.Equipment, equipmentLevels);
        }

        private static StageDefinition GetDefinition(long stage) => StageManager.Instance != null &&
            StageManager.Instance.TryGetStageDefinition((eStage)stage, out var definition) ? definition : null;
    }
}
