using System;
using System.Collections.Generic;
using KingdomIdle.Balance;

namespace KingdomIdle.MageTower
{
    /// <summary>Stable catalog IDs and pure rules shared by gameplay, UI and future server adapters.</summary>
    public static class MageSkillRules
    {
        public const int SkillCount = 10;
        public const int BloomAwakening = 10;
        public const string CatalogVersion = "mage-3";
        public const int DuplicateFragments = 30;

        public static bool ValidateRoster(IReadOnlyList<MageTowerSkillSO> skills)
        {
            if (skills == null || skills.Count != SkillCount) return false;
            int seen = 0;
            foreach (var skill in skills)
            {
                if (skill == null || skill.id < 0 || skill.id >= SkillCount || (seen & (1 << skill.id)) != 0) return false;
                seen |= 1 << skill.id;
            }
            return seen == (1 << SkillCount) - 1;
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
