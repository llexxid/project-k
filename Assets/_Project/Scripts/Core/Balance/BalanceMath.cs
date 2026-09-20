using System;

namespace KingdomIdle.Balance
{
    /// <summary>Beta 2026-09-13. Pure decimal calculations shared by gameplay, previews and validation.</summary>
    public static class BalanceMath
    {
        public const string Version = "beta-20260913-v1";
        public const int GoldCap = 300, MageCap = 100, RubyCap = 50, AccountCap = 200, ReincarnationCap = 300;
        private static readonly long[] GoldCosts = Costs(50m, 1.07m, GoldCap);
        private static readonly long[] MageCosts = Costs(10m, 1.04m, MageCap);
        private static readonly long[] RubyCosts = Costs(20m, 1.08m, RubyCap);
        private static readonly long[] ExperienceCosts = Costs(100m, 1.04m, AccountCap - 1);
        private static readonly decimal[] GoldMultipliers = Powers(1.025m, GoldCap);
        private static readonly decimal[] MageMultipliers = Powers(1.04m, MageCap);

        public static decimal Pow(decimal value, int exponent)
        {
            if (exponent < 0) throw new ArgumentOutOfRangeException(nameof(exponent));
            decimal result = 1m;
            for (int i = 0; i < exponent; i++) result = checked(result * value);
            return result;
        }
        private static decimal[] Powers(decimal rate, int cap)
        {
            var values = new decimal[cap + 1]; values[0] = 1m;
            for (int i = 1; i <= cap; i++) values[i] = checked(values[i - 1] * rate);
            return values;
        }
        private static long[] Costs(decimal first, decimal rate, int cap)
        {
            var result = new long[cap];
            for (int i = 0; i < cap; i++) result[i] = Ceil(first * Pow(rate, i));
            return result;
        }
        public static long Round(decimal value) => checked((long)decimal.Round(value, 0, MidpointRounding.AwayFromZero));
        public static long Ceil(decimal value) => checked((long)decimal.Ceiling(decimal.Round(value, 6, MidpointRounding.AwayFromZero)));
        public static long Floor(decimal value) => checked((long)decimal.Floor(value));
        private static long? Next(long[] costs, int level) => level >= 0 && level < costs.Length ? costs[level] : (long?)null;
        public static long? GoldCost(int level) => Next(GoldCosts, level);
        public static long? MageCost(int level) => Next(MageCosts, level);
        public static long? RubyCost(int level) => Next(RubyCosts, level);
        public static long? NextExp(int level) => Next(ExperienceCosts, level - 1);
        public static decimal GoldMultiplier(int level) => GoldMultipliers[Clamp(level, 0, GoldCap)];
        public static decimal RubyMultiplier(int level) => 1m + .02m * Clamp(level, 0, RubyCap);
        public static int Clamp(int value, int min, int max) => Math.Max(min, Math.Min(max, value));
        public static long GoldTotal(int level, int count)
        {
            if (level < 0 || count < 0 || level + (long)count > GoldCap) throw new ArgumentOutOfRangeException(nameof(count));
            long sum = 0;
            for (int i = 0; i < count; i++) sum = checked(sum + GoldCosts[level + i]);
            return sum;
        }
        public static int AffordableGoldLevels(int level, long balance)
        {
            if (level < 0 || level > GoldCap || balance < 0) return 0;
            int count = 0;
            while (level + count < GoldCap && balance >= GoldCosts[level + count]) balance -= GoldCosts[level + count++];
            return count;
        }
        public static long Stat(long basis, long equipment, long passive, int goldLevel, decimal aura, int accountLevel, int reincarnationLevel)
            => Math.Max(1, Round(checked(basis + equipment + passive) * GoldMultiplier(goldLevel) *
                (1m + aura + .002m * (Clamp(accountLevel, 1, AccountCap) - 1) + .01m * Clamp(reincarnationLevel, 0, ReincarnationCap))));
        public static long Damage(long attack, decimal multiplier) => Math.Max(1, Round(checked(attack * multiplier)));
        public static long WeaponAttack(long basis, int level) => checked(basis + Floor(basis * .10m * level));
        public static long MageDamage(long basis, int enhance, int awaken)
            => Round(basis * MageMultipliers[Clamp(enhance, 0, MageCap)] * (1m + .05m * Clamp(awaken, 0, 10)));
        public static decimal MageAttackCoefficient(long basis, int enhance, int awaken)
            => basis / 400m * (1m + .005m * Clamp(enhance, 0, MageCap) + .025m * Clamp(awaken, 0, 10));
        public static long MageDamage(long basis, int enhance, int awaken, long partyAttack)
            => checked(MageDamage(basis, enhance, awaken) + Round(Math.Max(0, partyAttack) * MageAttackCoefficient(basis, enhance, awaken)));
        public static decimal MageInterval(decimal basis, int awaken) => Math.Max(1m, basis * (1m - .02m * Clamp(awaken, 0, 10)));
        public static int MageHits(int basis, int awaken, bool persistent) => basis + (Clamp(awaken, 0, 10) / 4) * (persistent ? 2 : 1);
        public static void GainExperience(ref int level, ref long exp, long amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            level = Clamp(level, 1, AccountCap);
            if (level == AccountCap) { exp = 0; return; }
            exp = checked(exp + amount);
            while (level < AccountCap && exp >= NextExp(level).Value) { exp -= NextExp(level).Value; level++; }
            if (level == AccountCap) exp = 0;
        }
        public static long WithRemainder(long amount, int rubyLevel, ref long remainderMicros)
        {
            if (amount < 0 || remainderMicros < 0 || remainderMicros >= 1000000) throw new ArgumentOutOfRangeException();
            decimal value = amount * RubyMultiplier(rubyLevel) + remainderMicros / 1000000m;
            long whole = Floor(value);
            remainderMicros = checked((long)((value - whole) * 1000000m));
            return whole;
        }
        public static Enemy MainEnemy(int stage, int wave)
        {
            if (stage < 1 || stage > 3 || wave < 1 || wave > 11) throw new ArgumentOutOfRangeException();
            int exponent = 11 * (stage - 1) + wave - 1;
            bool boss = wave == 11;
            return new Enemy(Ceil(80m * Pow(1.10m, exponent) * (boss ? 18 : 1)),
                Ceil(5m * Pow(1.065m, exponent) * (boss ? 3 : 1)),
                Floor(10m * Pow(1.08m, exponent) * (boss ? 10 : 1)),
                Floor(3m * Pow(1.07m, exponent) * (boss ? 20 : 1)));
        }
        public static Enemy Mimic(int difficulty) => new Enemy(Ceil(500m * Pow(2m, difficulty - 1)),
            Ceil(6m * Pow(1.35m, difficulty - 1)), Floor(450m * Pow(1.6m, difficulty - 1)), 0);
        public static Enemy RubyBoss(int difficulty, int index) => new Enemy(Ceil((1200m + index * 600m) * Pow(2.2m, difficulty - 1)),
            Ceil((12m + index * 3m) * Pow(1.35m, difficulty - 1)), 0, 0);
        public static long RubyClear(int difficulty) => Floor(50m * Pow(1.35m, difficulty - 1));
        public static int EquipmentRoll(int millionth, int pity)
        {
            if (millionth < 0 || millionth >= 1000000 || pity < 0 || pity > 39) throw new ArgumentOutOfRangeException();
            return pity == 39 ? 2 : millionth >= 950000 ? -1 : millionth >= 900000 ? 2 : millionth >= 700000 ? 1 : 0;
        }
        public readonly struct Enemy
        {
            public readonly long HP, Attack, Gold, Experience;
            public Enemy(long hp, long attack, long gold, long experience) { HP = hp; Attack = attack; Gold = gold; Experience = experience; }
        }
    }
}
