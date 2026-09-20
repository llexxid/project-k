using System;
using System.Collections.Generic;
using KingdomIdle.Balance;

namespace KingdomIdle.MageTower
{
    /// <summary>Stable catalog IDs and pure rules shared by gameplay, UI and future server adapters.</summary>
    public static class MageSkillRules
    {
        public const int SkillCount = 8;
        // IDs are save keys, not roster indices. Retired IDs 6 and 8 must never be reused.
        public const int IdCapacity = 10;
        public static bool IsAvailable(int id) => id >= 0 && id < IdCapacity && id != 6 && id != 8;
        public const int BloomAwakening = 10;
        public const string CatalogVersion = "mage-5";
        public const int DuplicateFragments = 30;
        public const float LightningScatterRadius = 1.15f;

        public static bool ValidateRoster(IReadOnlyList<MageTowerSkillSO> skills)
        {
            if (skills == null || skills.Count != SkillCount) return false;
            int seen = 0;
            foreach (var skill in skills)
            {
                if (skill == null || !IsAvailable(skill.id) || (seen & (1 << skill.id)) != 0) return false;
                seen |= 1 << skill.id;
            }
            return seen == ((1 << IdCapacity) - 1 & ~(1 << 6) & ~(1 << 8));
        }

        public static MageTowerSkillSO SelectSkill(IReadOnlyList<MageTowerSkillSO> skills, int ticket)
        {
            if (skills == null || ticket < 0 || ticket >= skills.Count) throw new ArgumentOutOfRangeException(nameof(ticket));
            return skills[ticket];
        }

        public static int HitCount(MageTowerSkillSO skill, int awakening) =>
            BalanceMath.MageHits(skill.baseHits, awakening, skill.spellKind == MageSpellKind.FireTornado);

        // Combat power remains a single-target damage estimate; healing/control utility is separate.
        public static decimal SingleTargetPowerUnits(MageTowerSkillSO skill, int awakening, bool bloom)
        {
            if (skill.IsHealing) return 0;
            if (bloom && skill.spellKind == MageSpellKind.ArcaneVolley)
                return (decimal)skill.bloomPowerMultiplier +
                    (decimal)Math.Ceiling(skill.bloomDuration / skill.bloomTickInterval) * (decimal)skill.bloomAreaPowerMultiplier;
            if (bloom && (skill.spellKind == MageSpellKind.Lightning || skill.spellKind == MageSpellKind.IceSpike)) return (decimal)skill.bloomPowerMultiplier;
            decimal units;
            switch (skill.spellKind)
            {
                case MageSpellKind.Meteor: units = 1m + 2m * (decimal)skill.secondaryPowerRatio; break;
                case MageSpellKind.VoidRift: units = HitCount(skill, awakening) + (decimal)skill.secondaryPowerRatio; break;
                default: units = HitCount(skill, awakening); break;
            }
            return units * (bloom ? (decimal)skill.bloomPowerMultiplier : 1m);
        }
    }
}
