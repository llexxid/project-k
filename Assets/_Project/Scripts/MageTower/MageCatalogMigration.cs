using System;
using System.Linq;
using KingdomIdle.Balance;
using Newtonsoft.Json;
using Scripts.Core;

namespace KingdomIdle.MageTower
{
    /// <summary>One durable catalog migration; the removed ID is never reused.</summary>
    public static class MageCatalogMigration
    {
        public const string MeteorArchive = "mage-meteor-merged-v1";

        public static bool Apply(ProgressionState state)
        {
            if (state.Modules.ContainsKey(MeteorArchive) || !state.MageSkills.TryGetValue(8, out var meteor)) return false;
            bool ownedStarfall = state.MageSkills.TryGetValue(3, out var starfall);
            starfall ??= new MageSave();
            bool meteorEquipped = state.MageSlots.Contains(8), starfallEquipped = state.MageSlots.Contains(3);
            long keptSpend = starfall.Enhance > meteor.Enhance ? starfall.Spent :
                meteor.Enhance > starfall.Enhance ? meteor.Spent : Math.Max(starfall.Spent, meteor.Spent);
            long refund = checked(starfall.Spent + meteor.Spent - keptSpend);
            int duplicateAwakening = Math.Min(starfall.Awaken, meteor.Awaken);
            int fragments = checked(starfall.Fragments + meteor.Fragments + duplicateAwakening * (duplicateAwakening + 1) / 2 +
                (ownedStarfall ? MageSkillRules.DuplicateFragments : 0));
            state.Modules[MeteorArchive] = JsonConvert.SerializeObject(new { originalMeteor = meteor, originalStarfall = ownedStarfall ? starfall : null,
                refundedKnowledge = refund, duplicateAwakeningFragments = duplicateAwakening * (duplicateAwakening + 1) / 2,
                duplicateOwnershipFragments = ownedStarfall ? MageSkillRules.DuplicateFragments : 0 });
            starfall.Enhance = Math.Max(starfall.Enhance, meteor.Enhance);
            starfall.Awaken = Math.Max(starfall.Awaken, meteor.Awaken);
            starfall.Spent = keptSpend; starfall.Fragments = fragments;
            starfall.BloomEnabled = starfall.Awaken >= MageSkillRules.BloomAwakening &&
                (starfall.BloomEnabled || meteor.BloomEnabled || (meteorEquipped && !starfallEquipped));
            state.MageSkills[3] = starfall;
            state.MageSkills.Remove(8);
            for (int i = 0; i < state.MageSlots.Length; i++)
                if (state.MageSlots[i] == 8)
                {
                    state.MageSlots[i] = starfallEquipped ? -1 : 3;
                    starfallEquipped = true;
                }
            LocalProgression.Credit(state, eCurrency.ArcaneKnowledge, refund);
            return true;
        }
    }
}
