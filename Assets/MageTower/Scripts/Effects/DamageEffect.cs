using UnityEngine;

namespace KingdomIdle.MageTower
{
    [CreateAssetMenu(
        menuName = "KingdomIdle/MageTower/Effects/Damage",
        fileName = "DamageEffect_New")]
    public class DamageEffect : SkillEffect
    {
        [Tooltip("강화/각성 배율 적용 전 기본 데미지")]
        public float baseDamage;
    }
}
