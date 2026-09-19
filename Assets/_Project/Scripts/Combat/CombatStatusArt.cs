using UnityEngine;

namespace KingdomIdle.Combat
{
    public sealed class CombatStatusArt : ScriptableObject
    {
        public Sprite[] stun, footRing;
        public Sprite taunt;
        static CombatStatusArt _cached;
        public static CombatStatusArt Current => _cached != null ? _cached : _cached = Resources.Load<CombatStatusArt>("CombatStatusArt");
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() => _cached = null;
    }
}
