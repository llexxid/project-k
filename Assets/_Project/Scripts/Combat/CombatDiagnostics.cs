using System.Collections.Generic;
using UnityEngine;

namespace KingdomIdle.Combat
{
    public static class CombatDiagnostics
    {
        public struct Event
        {
            public string kind, actor, target;
            public int actorId, targetId, actorGeneration, targetGeneration;
            public float time, distance, deltaY, interval, facing, deltaX;
        }
        public static readonly List<Event> Events = new(2048);
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("LOBBY_DEVICE_QA")]
        public static void Record(string kind, Component actor, Component target, float interval = 0)
        {
            if (actor == null) return;
            if (Events.Count >= 2048) Events.RemoveRange(0,1024);
            Vector2 delta = target != null ? target.transform.position - actor.transform.position : Vector3.zero;
            Events.Add(new Event { kind=kind,actor=actor.name,target=target?.name,actorId=actor.GetInstanceID(),targetId=target!=null?target.GetInstanceID():0,
                actorGeneration=Generation(actor),targetGeneration=Generation(target),facing=actor is Scripts.Monster.Monster monster ? monster.FacingDir : actor.transform.localScale.x,deltaX=delta.x,
                time=Time.time,distance=delta.magnitude,deltaY=delta.y,interval=interval });
        }
        static int Generation(Component actor) => actor is Player p?p.LifeGeneration:actor is Scripts.Monster.Monster m?m.AllocGen:-1;
    }
}
