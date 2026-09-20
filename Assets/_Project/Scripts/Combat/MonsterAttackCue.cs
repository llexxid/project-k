using Scripts.Monster;
using UnityEngine;

namespace KingdomIdle.Combat
{
    public sealed class MonsterAttackCue : MonoBehaviour
    {
        private Monster _owner;
        private void Awake() => _owner = GetComponentInParent<Monster>();
        public void CombatImpact() => _owner?.ResolveAttackImpact();
    }
}
