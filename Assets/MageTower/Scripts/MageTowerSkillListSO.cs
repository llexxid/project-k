using System.Collections.Generic;
using UnityEngine;

namespace KingdomIdle.MageTower
{
    [CreateAssetMenu(menuName = "KingdomIdle/MageTower/Skill List", fileName = "MageTowerSkillList")]
    public class MageTowerSkillListSO : ScriptableObject
    {
        public List<MageTowerSkillSO> skills = new List<MageTowerSkillSO>();
    }
}
