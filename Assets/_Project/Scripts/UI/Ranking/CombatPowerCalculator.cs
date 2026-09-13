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
        // Retain public V1 method names for existing bindings; the metric uses beta single-target DPS.
        public static long CalculateCharacterPowerV1(long attack, long maxHp) => BalanceMath.Round(5m * attack / .5m + .25m * maxHp);
        public static decimal CharacterDps(Player player)
        {
            if (player?.playerStatus == null) return 0;
            var status = player.playerStatus;
            var data = KingdomArmyManager.Instance?.JobDB?.jobs.Find(x => x != null && x.jobName == status.JobName);
            decimal interval = data != null ? (decimal)data.basicAttack.cooldown : status.JobName == "Spearman" ? .5m : 1m;
            decimal uptime = status.JobName == "Elite_Archer" ? .92m : status.JobName == "Elite_Mage" ? .93m : 1m;
            decimal special = status.JobName == "Elite_Archer" ? status.Atk * 3m / 10m : status.JobName == "Elite_Mage" ? BalanceMath.Damage(status.Atk,2m) / 10m : 0m;
            return status.Atk / Math.Max(.01m,interval) * uptime + special;
        }
        public static long CalculateCharacterPowerV1(Player player) => player?.playerStatus == null ? 0 : BalanceMath.Round(5m * CharacterDps(player) + .25m * player.playerStatus.MaxHP);
        public static long CalculatePartyPowerV1(IReadOnlyList<Player> players)
        {
            if (players == null) return 0;
            decimal dps = 0, health = 0;
            foreach(var player in players.Take(3)) if(player?.playerStatus != null) { dps += CharacterDps(player); health += player.playerStatus.MaxHP; }
            var mage = MageTowerManager.Instance;
            if (mage != null) foreach(int id in LocalProgression.State.MageSlots.Where(x => x >= 0).Distinct())
            {
                int hits = BalanceMath.MageHits(id == 0 ? 3 : id == 1 ? 4 : 10,mage.GetAwakeningLevel(id),id == 2);
                dps += mage.GetEffectiveDamage(id) * (decimal)hits / (decimal)mage.GetEffectiveCooldown(id);
            }
            return BalanceMath.Round(5m * dps + .25m * health);
        }
    }
}
