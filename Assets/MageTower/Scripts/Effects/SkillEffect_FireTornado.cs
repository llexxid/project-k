using UnityEngine;

namespace KingdomIdle.MageTower
{
    [CreateAssetMenu(
        menuName = "KingdomIdle/MageTower/Effects/FireTornado",
        fileName = "SkillEffect_FireTornado_New")]
    public class SkillEffect_FireTornado : SkillEffect
    {
        [Header("지속")]
        [Tooltip("지속 시간 (초)")]
        public float duration = 6f;

        [Tooltip("데미지 틱 간격 (초)")]
        public float tickInterval = 0.2f;

        [Header("이동")]
        [Tooltip("타겟 이동 속도")]
        public float moveSpeed = 8f;

        [Tooltip("타겟 도착 판정 거리")]
        public float arrivalThreshold = 0.05f;
    }
}
