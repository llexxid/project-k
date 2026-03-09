using System.Collections.Generic;
using UnityEngine;

namespace KingdomIdle.MageTower
{
    [CreateAssetMenu(menuName = "KingdomIdle/MageTower/Skill Registry", fileName = "MageTowerSkillRegistry")]
    public class MageTowerSkillRegistrySO : ScriptableObject
    {
        public List<MageTowerSkillSO> skills = new List<MageTowerSkillSO>();
    }
}
