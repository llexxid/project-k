using System;
using System.Collections.Generic;
using System.Linq;
using KingdomIdle.Balance;
using KingdomIdle.KingdomArmy;
using KingdomIdle.MageTower;
namespace KingdomIdle.UGUI
{
    public static class CombatPowerCalculator
    {
        // Retain public V1 bindings. The two-number overload describes the starting spearman.
        public static long CalculateCharacterPowerV1(long attack, long maxHp) => BalanceMath.Round(5m * BalanceMath.Damage(attack,1.2m) / 1.2m + .25m * maxHp);
        public static decimal CharacterDps(Player player)
        {
            if (player?.playerStatus == null) return 0;
            var status = player.playerStatus;
            return CharacterDps(status.JobName, status.Atk);
        }

        private static decimal CharacterDps(string jobName, long attack)
        {
            var data = KingdomArmyManager.Instance?.JobDB?.jobs.Find(x => x != null && x.jobName == jobName);
            decimal interval = data != null ? (decimal)data.basicAttack.cooldown : 1.2m;
            decimal multiplier = data != null ? (decimal)data.basicAttack.damageMultiplier : jobName == "Spearman" ? 1.2m : 1m;
            decimal uptime = jobName == "Elite_Archer" ? .92m : jobName == "Elite_Mage" ? .93m : 1m;
            decimal special = jobName == "Elite_Archer" ? attack * 3m / 10m : 0m;
            if (jobName == "Elite_Mage")
            {
                var pulse = data?.specialSkills?.Find(skill => skill != null && skill.kind == SpecialSkillKind.EnergyPulse);
                if (pulse != null)
                    special = BalanceMath.Damage(attack, (decimal)pulse.damageMultiplier) / Math.Max(.01m, (decimal)pulse.cooldown);
            }
            return BalanceMath.Damage(attack,multiplier) / Math.Max(.01m,interval) * uptime + special;
        }
        public static long CalculateCharacterPowerV1(Player player) => player?.playerStatus == null ? 0 :
            CalculateCharacterPowerV1(player.playerStatus.JobName, player.playerStatus.Atk, player.playerStatus.MaxHP);
        public static long CalculateCharacterPowerV1(string jobName, long attack, long maxHp) =>
            BalanceMath.Round(5m * CharacterDps(jobName, attack) + .25m * maxHp);
        public static long CalculatePartyPowerV1(IReadOnlyList<Player> players)
        {
            if (players == null) return 0;
            decimal dps = 0, health = 0;
            foreach(var player in players.Take(3)) if(player?.playerStatus != null) { dps += CharacterDps(player); health += player.playerStatus.MaxHP; }
            var mage = MageTowerManager.Instance;
            if (mage != null) foreach(int id in LocalProgression.State.MageSlots.Where(x => x >= 0).Distinct())
            {
                dps += mage.SingleTargetDps(id);
            }
            return BalanceMath.Round(5m * dps + .25m * health);
        }
    }
}
