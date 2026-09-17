using System.Collections.Generic;
using Scripts.Core;
using Scripts.Monster;
using UnityEngine;

namespace KingdomIdle.Combat
{
    /// <summary>Foot-space positioning; weapons and sprite margins never affect spacing.</summary>
    public static class CombatMotion
    {
        public static readonly List<Player> Players = new(3);
        public static readonly List<Monster> Monsters = new(24);
        public const float PlayerRadius = .27f;
        public const float MeleeLane = .16f;
        // Acquire a narrow lane, then tolerate a small body-separation drift during
        // the committed swing. The hit still requires forward reach and body spacing.
        public const float MeleeImpactLane = .24f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() { Players.Clear(); Monsters.Clear(); }

        public static bool InFront(Vector2 origin, Vector2 target, float facing, float reach, float lane = MeleeLane)
        {
            Vector2 delta = target - origin;
            return delta.x * facing >= .52f && Mathf.Abs(delta.x) <= reach && Mathf.Abs(delta.y) <= lane;
        }

        public static Vector2 Approach(Vector2 origin, Vector2 target, float reach, int laneId)
        {
            float side = origin.x < target.x ? -1 : 1;
            if (Mathf.Abs(origin.x - target.x) < .06f) side = (laneId & 1) == 0 ? -1 : 1;
            float gap = Mathf.Max(.62f, reach * .86f);
            Vector2 point = target + new Vector2(side * gap, ((laneId % 3) - 1) * .07f);
            if (point.x < -2.45f || point.x > 2.45f) point.x = target.x - side * gap;
            return Clamp(point);
        }

        public static Vector2 Clamp(Vector2 point) => new(Mathf.Clamp(point.x, -2.48f, 2.48f), Mathf.Clamp(point.y, -2.45f, 2.45f));

        public static Vector2 Separate(Transform actor, float radius, float speed)
        {
            Vector2 origin = actor.position, push = Vector2.zero;
            foreach (var player in Players)
                if (player != null && !player.IsDead && player.transform != actor) Add(player.transform, PlayerRadius);
            foreach (var monster in Monsters)
                if (monster != null && monster.MonAction != eMonsterAction.Dead && monster.transform != actor) Add(monster.transform, monster.BodyRadius);
            return Vector2.ClampMagnitude(push, speed * Time.deltaTime);

            void Add(Transform other, float otherRadius)
            {
                Vector2 delta = origin - (Vector2)other.position; delta.y *= 1.35f;
                float distance = delta.magnitude, spacing = radius + otherRadius;
                if (distance >= spacing) return;
                if (distance < .001f) delta = actor.GetInstanceID() < other.GetInstanceID() ? Vector2.left : Vector2.right;
                else delta /= distance;
                delta.y /= 1.35f;
                push += delta * (spacing - distance) * .5f;
            }
        }

        public static Monster NearestEnemy(Vector2 point, float radius = float.MaxValue)
        {
            Monster best = null; float distance = radius * radius;
            foreach (var monster in Monsters)
            {
                if (monster == null || monster.MonAction == eMonsterAction.Dead) continue;
                float d = ((Vector2)monster.transform.position - point).sqrMagnitude;
                if (d < distance) { best = monster; distance = d; }
            }
            return best;
        }
    }
}
