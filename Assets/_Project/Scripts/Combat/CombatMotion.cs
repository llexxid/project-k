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

        public static Vector2 Ground(Component actor) => actor is Player player ? player.VfxFootPosition :
            actor is Monster monster ? monster.FootPosition : actor.transform.position;

        // The mover yields at an opponent's footprint. Walking is not a knockback.
        // Projection adjusts only the moving actor, never the actor being approached.
        public static void MoveTowards(Component actor, Vector2 destination, float distance, float radius)
        {
            Vector2 current = Ground(actor), next = Vector2.MoveTowards(current, destination, distance);
            if (actor is Player)
            {
                foreach (var monster in Monsters)
                    if (monster != null && monster.MonAction != eMonsterAction.Dead)
                        Avoid(monster.FootPosition, monster.BodyRadius);
            }
            else
            {
                foreach (var player in Players)
                    if (player != null && !player.IsDead) Avoid(player.VfxFootPosition, PlayerRadius);
            }
            Vector2 delta = Vector2.ClampMagnitude(Clamp(next) - current, distance);
            actor.transform.position += new Vector3(delta.x, delta.y, 0);

            void Avoid(Vector2 other, float otherRadius)
            {
                Vector2 offset = next - other; offset.y *= 1.35f;
                float spacing = radius + otherRadius;
                if (offset.sqrMagnitude >= spacing * spacing) return;
                if (offset.sqrMagnitude < .00001f) offset = current.x <= other.x ? Vector2.left : Vector2.right;
                offset = offset.normalized * spacing; offset.y /= 1.35f;
                next = other + offset;
            }
        }

        public static Vector2 Separate(Transform actor, float radius, float speed)
        {
            var owner = actor.GetComponent<Player>();
            Vector2 origin = owner != null ? owner.VfxFootPosition : actor.GetComponent<Monster>().FootPosition, push = Vector2.zero;
            if (owner != null)
            {
                foreach (var player in Players)
                    if (player != null && !player.IsDead && player.transform != actor) Add(player.VfxFootPosition, PlayerRadius, player.transform.GetInstanceID());
            }
            else
            {
                foreach (var monster in Monsters)
                    if (monster != null && monster.MonAction != eMonsterAction.Dead && monster.transform != actor) Add(monster.FootPosition, monster.BodyRadius, monster.transform.GetInstanceID());
            }
            return Vector2.ClampMagnitude(push, speed * Time.deltaTime);

            void Add(Vector2 other, float otherRadius, int otherId)
            {
                Vector2 delta = origin - other; delta.y *= 1.35f;
                float distance = delta.magnitude, spacing = radius + otherRadius;
                if (distance >= spacing) return;
                if (distance < .001f) delta = actor.GetInstanceID() < otherId ? Vector2.left : Vector2.right;
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
                float d = ((Vector2)monster.FootPosition - point).sqrMagnitude;
                if (d < distance) { best = monster; distance = d; }
            }
            return best;
        }
    }
}
