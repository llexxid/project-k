#if UNITY_EDITOR || LOBBY_DEVICE_QA
using System.Collections.Generic;
using UnityEngine;
namespace KingdomIdle.MageTower
{
    public static class MageSkillDiagnostics
    {
        public sealed class Entry { public int skill; public string kind, target; public long amount; public bool bloom; public float time, x, y; }
        public static readonly List<Entry> Events = new(256);
        public static void Record(int skill, string kind, string target, long amount, bool bloom, Vector3? position = null)
        {
            if (Events.Count >= 1024) Events.RemoveRange(0, 512);
            Events.Add(new Entry { skill=skill, kind=kind, target=target, amount=amount, bloom=bloom, time=Time.time, x=position?.x ?? 0, y=position?.y ?? 0 });
        }
    }
}
#endif
