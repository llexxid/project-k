using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using KingdomIdle.Combat;
using Scripts.Core;
using Scripts.Monster;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace KingdomIdle.EditorTools
{
    public static class MonsterMotionAcceptance
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private sealed class Spec
        {
            public eMonsterType type;
            public float interval;
            public GameObject prefab;
        }
        private sealed class Report
        {
            public readonly List<string> cases = new(), failures = new();
            public void Check(bool ok, string name) { cases.Add(name); if (!ok) failures.Add(name); }
            public Dictionary<string, object> Result() => new()
            { ["passed"] = failures.Count == 0, ["count"] = cases.Count, ["cases"] = cases, ["failures"] = failures };
        }

        public static Dictionary<string, object> Run()
        {
            var report = new Report(); int shortened = 0, unchanged = 0;
            var durationMethod = typeof(Monster).GetMethod("AttackMotionDuration", BindingFlags.NonPublic | BindingFlags.Static);
            foreach (var spec in Roster())
            {
                try
                {
                    string name = spec.type.ToString();
                    var monster = spec.prefab.GetComponent<Monster>();
                    var animator = spec.prefab.GetComponentInChildren<Animator>();
                    var controller = animator.runtimeAnimatorController as AnimatorController;
                    var state = controller.layers[0].stateMachine.states.Single(s => s.state.name == "Attack").state;
                    var clip = (AnimationClip)state.motion;
                    var data = new SerializedObject(monster);
                    float normalized = data.FindProperty("_impactNormalized").floatValue;
                    float oldDuration = Mathf.Clamp(spec.interval * .6f, .55f, 1.05f);
                    float duration = (float)durationMethod.Invoke(null, new object[] { spec.interval, clip.length });
                    if (duration < oldDuration - .001f) shortened++; else unchanged++;
                    report.Check(duration <= oldDuration && duration <= clip.length + .0001f && duration > 0,
                        name + " no stretching or slower cadence");
                    report.Check(Mathf.Abs(duration - Mathf.Min(oldDuration, clip.length)) < .0001f,
                        name + " authored short motion or existing long-motion cap");
                    var cues = clip.events.Where(e => e.functionName == "CombatImpact").ToArray();
                    report.Check(cues.Length == 1 && Mathf.Abs(cues[0].time - clip.length * normalized) <= .011f,
                        name + " event and fallback share the same normalized frame");
                    if (cues.Length == 1)
                        report.Check(Mathf.Abs(cues[0].time / (clip.length / duration) - duration * normalized) <= .011f,
                            name + " actual playback and impact clock agree");
                    report.Check(state.speed == 1 && spec.interval > duration,
                        name + " motion ends before the catalog start interval");
                }
                catch (Exception e) { report.Check(false, spec.type + ": " + e.GetBaseException().Message); }
            }
            report.Check(shortened == 13 && unchanged == 2, "Thirteen short motions restored; mimic and goblin king unchanged");
            report.Check(durationMethod != null && Mathf.Approximately((float)durationMethod.Invoke(null, new object[] { 2.2f, 0f }), 1.05f),
                "Missing or zero-length clip retains the existing safe target duration");
            return report.Result();
        }

        // Synchronous PlayMode fixture. No account, manager, network, profile or progression is opened.
        // Actual prefab events and actual projectile Update are exercised with isolated actors.
        public static Dictionary<string, object> RunRuntime()
        {
            var report = new Report();
            if (!Application.isPlaying || CombatMotion.Players.Count != 0 || CombatMotion.Monsters.Count != 0)
            {
                report.Check(false, "RunRuntime requires an empty PlayMode scene without live combat actors");
                return report.Result();
            }
            var priorDiagnostics = CombatDiagnostics.Events.ToArray();
            GameObject root = null;
            try
            {
                root = new GameObject("MonsterMotionAcceptance");
                var targetObject = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/_Project/Prefabs/RoyalGuard/Lancer.prefab"), root.transform);
                var target = targetObject.GetComponent<Player>();
                foreach (var behaviour in targetObject.GetComponents<MonoBehaviour>())
                    if (behaviour != target) behaviour.enabled = false; // Prevent ChangeJob.Start from accessing saved jobs.
                target.playerOrder.InterruptBT();
                target.transform.position += new Vector3(.7f, 0, 0) - target.VfxFootPosition;
                var roster = Roster();
                foreach (string name in new[] { "GOBLIN", "HERESY_MIMIC", "GOBLIN_BOMBER", "ORC_HUNTER" })
                {
                    GameObject actor = null, shotTemplate = null;
                    try
                    {
                        var spec = roster.Single(s => s.type.ToString() == name);
                        actor = Object.Instantiate(spec.prefab, root.transform);
                        var monster = actor.GetComponent<Monster>();
                        monster.Init(spec.type, new Monster.MonsterStat(1000, 0, 1, 1, spec.interval), 0);
                        monster.OnAlloc();
                        actor.transform.position -= monster.FootPosition;
                        monster.SetTarget(target);
                        target.GrantShield(10000, 3600);
                        Set(monster, "_lastAttackTime", Time.time - spec.interval - .001f);
                        float began = Time.time;
                        report.Check(monster.TryBeginAttack(), name + " real attack begins");
                        float duration = Get<float>(monster, "_attackEnds") - began;
                        float impact = Get<float>(monster, "_impactAt") - began;
                        float normalized = Get<float>(monster, "_impactNormalized");
                        float clipLength = monster.GetAnimationLength(eMonsterAction.Attack);
                        report.Check(Mathf.Abs(duration - Mathf.Min(clipLength, Mathf.Clamp(spec.interval * .6f, .55f, 1.05f))) < .001f &&
                            Mathf.Abs(impact - duration * normalized) < .001f, name + " runtime lock and impact share duration");
                        report.Check(Mathf.Abs(monster.Animator.speed * duration - clipLength) < .001f &&
                            Mathf.Abs(monster.AttackInterval - spec.interval) < .001f && monster.damage == 1,
                            name + " playback speed, catalog interval and damage preserved");

                        var sourceProjectile = Get<GameObject>(monster, "_projectilePrefab");
                        if (sourceProjectile != null)
                        {
                            shotTemplate = Object.Instantiate(sourceProjectile, root.transform);
                            shotTemplate.name = sourceProjectile.name; shotTemplate.SetActive(false);
                            Set(monster, "_projectilePrefab", shotTemplate);
                        }
                        long before = target.ShieldHP;
                        monster.Animator.Update(0);
                        monster.Animator.Update(impact + .002f);
                        report.Check(!Get<bool>(monster, "_pendingImpact"), name + " authored AnimationEvent resolves pending impact");
                        if (shotTemplate == null)
                        {
                            report.Check(target.ShieldHP == before - 1, name + " melee or tongue event deals exactly one damage");
                            Set(monster, "_impactAt", Time.time - .041f); Tick(monster);
                            monster.ResolveAttackImpact();
                            report.Check(target.ShieldHP == before - 1, name + " late fallback and duplicate event cannot double-hit");
                        }
                        else
                        {
                            report.Check(target.ShieldHP == before, name + " throw event launches without instant damage");
                            var shot = OwnedShots(shotTemplate).Single();
                            float flight = Get<float>(shot, "_duration");
                            float minimum = name == "GOBLIN_BOMBER" ? .45f : .12f;
                            float speed = Get<float>(monster, "_projectileSpeed");
                            float distance = Vector3.Distance(Get<Vector3>(shot, "_start"), Get<Vector3>(shot, "_destination"));
                            report.Check(Mathf.Abs(flight - Mathf.Max(minimum, distance / Mathf.Max(1, speed))) < .001f,
                                name + " existing speed and minimum flight time preserved");
                            Set(shot, "_elapsed", -Time.deltaTime); Tick(shot);
                            report.Check(target.ShieldHP == before && Get<bool>(shot, "_flying"), name + " zero elapsed flight cannot hit");
                            Set(shot, "_elapsed", flight); Tick(shot);
                            report.Check(target.ShieldHP == before - 1 && !Get<bool>(shot, "_flying"), name + " landing deals one damage");
                            Tick(shot); monster.ResolveAttackImpact();
                            report.Check(target.ShieldHP == before - 1, name + " completed flight and duplicate event cannot double-hit");
                        }

                        Set(monster, "_attackEnds", Time.time - .001f);
                        Set(monster, "_lastAttackTime", Time.time - spec.interval + .01f);
                        report.Check(!monster.TryBeginAttack(), name + " completed motion cannot bypass start interval");
                        Set(monster, "_lastAttackTime", Time.time - spec.interval - .001f);
                        report.Check(monster.TryBeginAttack(), name + " catalog interval permits the next attack");
                        if (shotTemplate == null)
                        {
                            before = target.ShieldHP;
                            Set(monster, "_impactAt", Time.time - .041f); Tick(monster);
                            report.Check(target.ShieldHP == before - 1, name + " absent event uses the fallback exactly once");
                            monster.GetComponentInChildren<MonsterAttackCue>().CombatImpact(); Tick(monster);
                            report.Check(target.ShieldHP == before - 1, name + " event after fallback cannot double-hit");
                        }
                        before = target.ShieldHP;
                        actor.SetActive(false); monster.ResolveAttackImpact();
                        report.Check(target.ShieldHP == before && !Get<bool>(monster, "_pendingImpact"), name + " pool disable cancels pending damage");
                    }
                    catch (Exception e) { report.Check(false, name + ": " + e.GetBaseException().Message); }
                    finally
                    {
                        if (shotTemplate != null)
                        {
                            foreach (var shot in OwnedShots(shotTemplate)) Object.DestroyImmediate(shot.gameObject);
                            var pools = (Dictionary<GameObject, Stack<MonsterProjectile>>)typeof(MonsterProjectile)
                                .GetField("Pools", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                            pools.Remove(shotTemplate);
                            Object.DestroyImmediate(shotTemplate);
                        }
                        if (actor != null) Object.DestroyImmediate(actor);
                    }
                }
            }
            catch (Exception e) { report.Check(false, "Fixture: " + e.GetBaseException().Message); }
            finally
            {
                if (root != null) Object.DestroyImmediate(root);
                CombatDiagnostics.Events.Clear(); CombatDiagnostics.Events.AddRange(priorDiagnostics);
            }
            return report.Result();
        }

        private static List<Spec> Roster()
        {
            var database = new SerializedObject(AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/_Project/Resources/StageDatabaseSO.asset"));
            var stages = database.FindProperty("_stages"); var intervals = new Dictionary<long, float>();
            for (int s = 0; s < stages.arraySize; s++)
            {
                var stage = stages.GetArrayElementAtIndex(s);
                if (!stage.FindPropertyRelative("_enabled").boolValue) continue;
                var entries = stage.FindPropertyRelative("_monsterEntries");
                for (int e = 0; e < entries.arraySize; e++)
                {
                    var entry = entries.GetArrayElementAtIndex(e);
                    intervals[entry.FindPropertyRelative("_monsterTypeValue").longValue] =
                        entry.FindPropertyRelative("_combat").FindPropertyRelative("_attackIntervalSec").floatValue;
                }
            }
            var group = new SerializedObject(AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/AddressableAssetsData/AssetGroups/Stage Monsters.asset"));
            var addresses = group.FindProperty("m_SerializeEntries"); var result = new List<Spec>();
            foreach (var item in intervals)
            {
                var type = (eMonsterType)item.Key;
                for (int a = 0; a < addresses.arraySize; a++)
                {
                    var address = addresses.GetArrayElementAtIndex(a);
                    if (address.FindPropertyRelative("m_Address").stringValue != type.ToString()) continue;
                    result.Add(new Spec { type = type, interval = item.Value, prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                        AssetDatabase.GUIDToAssetPath(address.FindPropertyRelative("m_GUID").stringValue)) });
                    break;
                }
            }
            if (result.Count != intervals.Count) throw new InvalidOperationException("Stage monster address is missing.");
            return result;
        }

        private static T Get<T>(object owner, string name) => (T)owner.GetType().GetField(name, Private).GetValue(owner);
        private static void Set(object owner, string name, object value) => owner.GetType().GetField(name, Private).SetValue(owner, value);
        private static void Tick(object owner) => owner.GetType().GetMethod(owner is Monster ? "TickCombat" : "Update", Private).Invoke(owner, null);
        private static MonsterProjectile[] OwnedShots(GameObject template) => Object.FindObjectsByType<MonsterProjectile>(
            FindObjectsInactive.Include, FindObjectsSortMode.None).Where(s => Get<GameObject>(s, "_prefab") == template).ToArray();
    }
}
