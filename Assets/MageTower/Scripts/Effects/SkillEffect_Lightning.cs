using UnityEngine;

namespace KingdomIdle.MageTower
{
    [CreateAssetMenu(
        menuName = "KingdomIdle/MageTower/Effects/Lightning",
        fileName = "SkillEffect_Lightning_New")]
    public class SkillEffect_Lightning : SkillEffect
    {
        [Header("체인")]
        [Tooltip("총 연속 시전 횟수")]
        public int totalCasts = 3;

        [Tooltip("체인 시전 시 랜덤 오프셋 반경")]
        public float chainRadius = 0.8f;

        [Header("투사체")]
        [Tooltip("데미지 판정 반경")]
        public float damageRadius = 1.5f;

        [Header("화면 흔들림")]
        [Tooltip("히트 시 화면 흔들림 여부")]
        public bool shakeOnHit = true;

        [Tooltip("화면 흔들림 지속 시간 (초)")]
        public float shakeDuration = 0.15f;

        [Tooltip("화면 흔들림 세기")]
        public float shakeMagnitude = 0.08f;
    }
}
