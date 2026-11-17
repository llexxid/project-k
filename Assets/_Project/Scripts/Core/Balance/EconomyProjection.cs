using System;
using System.Collections.Generic;
using System.Linq;
using Protocol.Message.Dto;
using Scripts.Core;
using Scripts.Core.Manager;

namespace KingdomIdle.Balance
{
    public readonly struct EconomyProjection
    {
        public readonly long Gold, Ruby;
        public readonly int AttackLevel, HealthLevel, RubyGoldLevel, RubyExpLevel;
        public readonly bool Valid;
        internal EconomyProjection(long gold, long ruby, int attack, int health, int rubyGold, int rubyExp, bool valid)
        { Gold = gold; Ruby = ruby; AttackLevel = attack; HealthLevel = health; RubyGoldLevel = rubyGold; RubyExpLevel = rubyExp; Valid = valid; }
        public int Level(EconomyCommandKind kind) => kind switch
        {
            EconomyCommandKind.GoldAttack => AttackLevel, EconomyCommandKind.GoldHealth => HealthLevel,
            EconomyCommandKind.RubyGold => RubyGoldLevel, EconomyCommandKind.RubyExperience => RubyExpLevel, _ => -1
        };
    }

    internal static class EconomyProjectionMath
    {
        internal static EconomyCommandDTO Quote(EconomyProjection p, EconomyCommandKind kind, int mode)
        {
            int level = p.Level(kind);
            bool gold = kind == EconomyCommandKind.GoldAttack || kind == EconomyCommandKind.GoldHealth;
            if (!p.Valid || level < 0 || (gold ? mode != 1 && mode != 10 && mode != -1 : mode != 1)) return null;
            int count = gold ? mode == -1 ? BalanceMath.AffordableGoldLevels(level, p.Gold) : Math.Min(mode, BalanceMath.GoldCap - level)
                : level < BalanceMath.RubyCap ? 1 : 0;
            if (count <= 0) return null;
            long cost = gold ? BalanceMath.GoldTotal(level, count) : BalanceMath.RubyCost(level).Value;
            if (cost > (gold ? p.Gold : p.Ruby)) return null;
            return new EconomyCommandDTO { Kind = kind, Mode = mode, ExpectedLevel = level, CountLimit = count, SpendLimit = cost };
        }

        internal static EconomyProjection Calculate(ProgressionState state, IEnumerable<EconomyCommandDTO> commands,
            Func<long, StageDefinition> definition = null)
        {
            long gold = state.Wallet.GetValueOrDefault(eCurrency.Gold), ruby = state.Wallet.GetValueOrDefault(eCurrency.Ruby);
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
                    var p = new EconomyProjection(gold, ruby, attack, health, rg, re, true);
                    int level = p.Level(command.Kind);
                    bool isGold = command.Kind == EconomyCommandKind.GoldAttack || command.Kind == EconomyCommandKind.GoldHealth;
                    if (level != command.ExpectedLevel) { valid = false; break; }
                    int count = isGold ? Math.Min(command.CountLimit, BalanceMath.GoldCap - level) : 1;
                    if (isGold && command.Mode == -1) count = Math.Min(count, BalanceMath.AffordableGoldLevels(level, Math.Min(gold, command.SpendLimit)));
                    if (count <= 0 || (!isGold && level >= BalanceMath.RubyCap)) { valid = false; break; }
                    long cost = isGold ? BalanceMath.GoldTotal(level, count) : BalanceMath.RubyCost(level).Value;
                    if (cost > command.SpendLimit || cost > (isGold ? gold : ruby)) { valid = false; break; }
                    if (isGold) gold -= cost; else ruby -= cost;
                    switch (command.Kind)
                    {
                        case EconomyCommandKind.GoldAttack: attack += count; break;
                        case EconomyCommandKind.GoldHealth: health += count; break;
                        case EconomyCommandKind.RubyGold: rg++; break;
                        case EconomyCommandKind.RubyExperience: re++; break;
                        default: valid = false; break;
                    }
                    if (!valid) break;
                }
            }
            catch (OverflowException) { valid = false; }
            return new EconomyProjection(gold, ruby, attack, health, rg, re, valid);
        }

        private static StageDefinition GetDefinition(long stage) => StageManager.Instance != null &&
            StageManager.Instance.TryGetStageDefinition((eStage)stage, out var definition) ? definition : null;
    }
}
