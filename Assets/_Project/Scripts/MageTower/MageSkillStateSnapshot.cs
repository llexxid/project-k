using System;
using System.Linq;
using KingdomIdle.Balance;

namespace KingdomIdle.MageTower
{
    /// <summary>Lossless export contract for a future server adapter; never applies remote state.</summary>
    [Serializable]
    public sealed class MageSkillStateSnapshot
    {
        public int schema = 1;
        public string catalogVersion = MageSkillRules.CatalogVersion;
        public long progressionRevision;
        public Skill[] skills;
        public int[] equippedSkillIds;
        [Serializable]
        public sealed class Skill
        {
            public int id, enhance, awakening, fragments;
            public long knowledgeSpent;
            public bool bloomEnabled;
        }
        public static MageSkillStateSnapshot Capture(ProgressionState state) => new()
        {
            progressionRevision = state.Revision,
            equippedSkillIds = (int[])state.MageSlots.Clone(),
            skills = state.MageSkills.OrderBy(p=>p.Key).Select(p=>new Skill {
                id=p.Key, enhance=p.Value.Enhance, awakening=p.Value.Awaken,
                fragments=p.Value.Fragments, knowledgeSpent=p.Value.Spent, bloomEnabled=p.Value.BloomEnabled
            }).ToArray()
        };
    }
}
