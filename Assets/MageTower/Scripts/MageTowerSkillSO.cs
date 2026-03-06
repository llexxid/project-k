using UnityEngine;

namespace KingdomIdle.MageTower
{
    public enum eCastPattern
    {
        Single,
        RandomAroundTarget,
        UniqueRandomMonster,
        PersistentOnTarget,
    }

    [CreateAssetMenu(menuName = "KingdomIdle/MageTower/Skill", fileName = "MageTowerSkill_New")]
    public class MageTowerSkillSO : ScriptableObject
    {
        public int id;
        public string skillName;
        public Sprite icon;
        public float baseDamage;
        public float baseCooldown;
        public int maxEnhanceLevel = 100;
        public int maxAwakeningLevel = 10;
        public GameObject prefab;
        public int totalCasts = 1;
        public float chainRadius = 1.5f;
        public eCastPattern castPattern = eCastPattern.Single;
        public float duration;
        public float tickInterval;
    }
}
