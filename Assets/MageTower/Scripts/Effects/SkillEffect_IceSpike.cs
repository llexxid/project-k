using UnityEngine;

namespace KingdomIdle.MageTower
{
    [CreateAssetMenu(
        menuName = "KingdomIdle/MageTower/Effects/IceSpike",
        fileName = "SkillEffect_IceSpike_New")]
    public class SkillEffect_IceSpike : SkillEffect
    {
        [Header("체인")]
        [Tooltip("총 연속 시전 횟수 (유니크 몬스터 대상)")]
        public int totalCasts = 6;

        [Header("투사체")]
        [Tooltip("데미지 판정 반경")]
        public float damageRadius = 1.5f;
    }
}
