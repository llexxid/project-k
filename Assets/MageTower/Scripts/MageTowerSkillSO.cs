using UnityEngine;

namespace KingdomIdle.MageTower
{
    [CreateAssetMenu(menuName = "KingdomIdle/MageTower/Skill", fileName = "MageTowerSkill_New")]
    public class MageTowerSkillSO : ScriptableObject
    {
        public int id;
        public string skillName;
        public Sprite icon;
        public float baseDamage;
        public float baseCooldown;
        public int maxEnhanceLevel = 10;
        public int maxAwakeningLevel = 5;
    }
}
