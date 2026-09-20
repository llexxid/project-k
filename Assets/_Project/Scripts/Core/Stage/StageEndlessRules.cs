using System;
using UnityEngine;

namespace Scripts.Core
{
    [Serializable]
    public struct StageGrowthCurve
    {
        [SerializeField] private double _basis, _growth, _boss, _power;
        public StageGrowthCurve(double basis, double growth, double boss, double power)
        { _basis = basis; _growth = growth; _boss = boss; _power = power; }

        public long Evaluate(int chapter, int wave, double role, bool roundUp)
        {
            double index = (chapter - 1d) * 11 + wave;
            // The authored opening remains exponential. Beyond it, a continuous
            // power curve keeps late-game growth usable without overflowing after a few loops.
            double value = _basis * Math.Pow(_growth, 32) * Math.Pow(index / 33d, _power) *
                (wave == 11 ? _boss : 1) * role;
            const long maximum = long.MaxValue / 4096;
            if (value >= maximum) return maximum;
            return Math.Max(roundUp ? 1 : 0, (long)(roundUp ? Math.Ceiling(value) : Math.Floor(value)));
        }
    }

    /// <summary>Generated from Stage_Catalog Inputs. Runtime, previews and offline income share this data.</summary>
    [Serializable]
    public sealed class StageEndlessRules
    {
        [SerializeField] private StageGrowthCurve _hp, _attack, _gold, _experience;
        [SerializeField] private double _dropBase, _dropStep, _dropCap;
        [SerializeField] private long _bossCoins, _bossFragments;
        public StageEndlessRules(StageGrowthCurve hp, StageGrowthCurve attack, StageGrowthCurve gold,
            StageGrowthCurve experience, double dropBase, double dropStep, double dropCap, long bossCoins, long bossFragments)
        {
            _hp = hp; _attack = attack; _gold = gold; _experience = experience;
            _dropBase = dropBase; _dropStep = dropStep; _dropCap = dropCap;
            _bossCoins = bossCoins; _bossFragments = bossFragments;
        }
        public StageEnemyData Enemy(int chapter, int wave, double attackRole, StageEnemyData source) => new(
            _hp.Evaluate(chapter, wave, 1, true), _attack.Evaluate(chapter, wave, attackRole, true),
            _gold.Evaluate(chapter, wave, 1, false), _experience.Evaluate(chapter, wave, 1, false),
            source.MoveSpeed, source.AttackIntervalSec);
        public double DropRate(int chapter, int wave) => wave == 11 ? 0 : Math.Min(_dropCap, _dropBase + _dropStep * (chapter - 1d));
        public StageFirstClearReward FirstClear(int wave) => wave == 11 ?
            new StageFirstClearReward(_bossCoins, _bossFragments, -1, -1, 0, null, 0, null) : default;
    }
}
