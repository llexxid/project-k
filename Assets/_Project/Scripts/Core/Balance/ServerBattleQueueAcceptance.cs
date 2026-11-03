#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Protocol.Message.Dto;
using Scripts.Core;
using Scripts.Core.Manager;
using UnityEngine;

namespace KingdomIdle.Balance
{
    public static class ServerBattleQueueAcceptance
    {
        public static object Run()
        {
            const BindingFlags instance = BindingFlags.Instance | BindingFlags.NonPublic;
            const BindingFlags statics = BindingFlags.Static | BindingFlags.NonPublic;
            var stateField = typeof(LocalProgression).GetField("_state", statics);
            var openingField = typeof(LocalProgression).GetField("_opening", statics);
            object savedState = stateField.GetValue(null), savedOpening = openingField.GetValue(null);
            var savedNetwork = NetworkManager.Instance;
            var savedUsers = UserManager.Instance;
            float savedTime = Time.timeScale;
            var owner = new GameObject("ServerBattleQueueAcceptance") { hideFlags = HideFlags.HideAndDontSave };
            owner.SetActive(false);
            var manager = owner.AddComponent<StageManager>();
            object Get(string name) => typeof(StageManager).GetField(name, instance).GetValue(manager);
            void Set(string name, object value) => typeof(StageManager).GetField(name, instance).SetValue(manager, value);
            object Call(string name, params object[] args) => typeof(StageManager).GetMethod(name, instance).Invoke(manager, args);
            var checks = new List<string>();
            void Check(bool valid, string name)
            { if (!valid) throw new InvalidOperationException("Server battle queue acceptance: " + name); checks.Add(name); }
            try
            {
                // Pure in-memory fixture: Ensure must not open an account or publish account-change events.
                openingField.SetValue(null, true);
                var fixture = new ProgressionState { ActiveDungeon = "server-battle" };
                stateField.SetValue(null, fixture);
                var network = owner.AddComponent<NetworkManager>();
                typeof(NetworkManager).GetProperty("LoginState").SetValue(network, GameLoginState.Ready);
                NetworkManager.Instance = network;
                UserManager.Instance = null;
                var buffer = new Dictionary<long, Dictionary<eMonsterType, int>>();
                Set("_huntBuffer", buffer);
                object queue = Get("_huntOutbox");
                Array Items() => (Array)queue.GetType().GetMethod("ToArray").Invoke(queue, null);
                int Count() => ((ICollection)queue).Count;
                void Empty()
                { foreach (object item in Items()) Call("Dequeue", item); }
                object ItemField(object item, string name) => item.GetType().GetField(name).GetValue(item);
                List<HuntResult> Loots(object item) => (List<HuntResult>)ItemField(item, "Loots");
                int Kills(object item)
                { int sum = 0; foreach (var loot in Loots(item)) sum += loot.Count; return sum; }
                long dungeon = (long)StageParser.MakeStage(eStageType.GoldDungeon, 1);
                long main = (long)StageParser.MakeStage(eStageType.Main, 1);
                var monster = (eMonsterType)4294967574UL;
                buffer[dungeon] = new Dictionary<eMonsterType, int> { [monster] = 300 };
                Call("FlushHuntBuffer", dungeon);
                Check(Count() == 3 && !buffer.ContainsKey(dungeon), "large dungeon buffer becomes three bounded batches");
                int total = 0;
                foreach (object item in Items())
                {
                    total += Kills(item);
                    Check((long)ItemField(item, "StageId") == dungeon && (string)ItemField(item, "BattleId") == "server-battle" &&
                        Kills(item) <= 149, "each dungeon batch retains its server battle identity");
                }
                Check(total == 300, "batching preserves every kill");
                fixture.ActiveDungeon = "a-later-run";
                object first = Items().GetValue(0);
                var halves = (IEnumerable)typeof(StageManager).GetMethod("SplitHuntItem", statics).Invoke(null, new[] { first });
                int splitCount = 0, splitKills = 0;
                foreach (object half in halves)
                {
                    splitCount++; splitKills += Kills(half);
                    Check((long)ItemField(half, "StageId") == dungeon && (string)ItemField(half, "BattleId") == "server-battle",
                        "retry split keeps the original identity after active run changes");
                }
                Check(splitCount == 2 && splitKills == Kills(first), "retry split neither duplicates nor loses kills");
                Empty();
                buffer[main] = new Dictionary<eMonsterType, int> { [monster] = 2 };
                Call("FlushHuntBuffer", main);
                Check(Count() == 1 && ItemField(Items().GetValue(0), "BattleId") == null,
                    "main batches never inherit the active dungeon identity");

                int callbacks = 0; bool settled = false;
                Set("_huntInFlight", true);
                Time.timeScale = .5f;
                Check(manager.FlushPendingHunts(ok => { callbacks++; settled = ok; }) && callbacks == 0 && Time.timeScale == 0,
                    "settlement pauses battle and waits while queued work remains");
                Check(!manager.FlushPendingHunts(_ => callbacks++), "a second operation cannot replace the waiting callback");
                Empty(); Set("_huntInFlight", false);
                Call("TrySendNextHunt"); Call("TrySendNextHunt");
                Check(callbacks == 1 && settled && Time.timeScale == .5f, "empty successful queue releases its callback once and restores time");
                Set("_huntSyncFailed", true);
                Check(!manager.FlushPendingHunts(_ => callbacks++) && callbacks == 1 && Time.timeScale == .5f,
                    "unresolved hunt failure rejects the drain shared by dungeon entry and reincarnation");

                buffer[main] = new Dictionary<eMonsterType, int> { [monster] = 1 };
                Call("FlushHuntBuffer", main);
                buffer[dungeon] = new Dictionary<eMonsterType, int> { [monster] = 1 };
                var saturated = (Dictionary<long, HashSet<ulong>>)Get("_saturatedTypes");
                saturated[main] = new HashSet<ulong> { (ulong)monster };
                Set("_huntInFlight", true); Set("_onHuntsSettled", (Action<bool>)(_ => callbacks++));
                Set("_dungeonRequestPending", true); Set("_retryDungeon", (Action)(() => callbacks++));
                // Block scene loading; only BeginStage's fresh-session reset is under test.
                Set("_currentState", eStageRunState.Transitioning);
                fixture.ActiveDungeon = null;
                manager.BeginStage((eStage)main);
                Check(Count() == 0 && buffer.Count == 0 && saturated.Count == 0 && !(bool)Get("_huntInFlight") && !(bool)Get("_huntSyncFailed"),
                    "fresh begin clears old queue, buffers, saturation and failure state");
                Check(Get("_onHuntsSettled") == null && Get("_retryDungeon") == null && !(bool)Get("_dungeonRequestPending") && callbacks == 1,
                    "fresh begin removes stale callbacks without running them");
                Check(typeof(NetworkManager).GetField("_context", instance).GetValue(network) == null &&
                    ((ICollection)typeof(NetworkManager).GetField("_pendingCalls", instance).GetValue(network)).Count == 0,
                    "fixture remained unauthenticated and sent no queued network calls");
                return new { passed = true, count = checks.Count, checks };
            }
            finally
            {
                var token = (CancellationTokenSource)Get("_token");
                token?.Cancel(); token?.Dispose(); Set("_token", null);
                NetworkManager.Instance = savedNetwork;
                UserManager.Instance = savedUsers;
                stateField.SetValue(null, savedState);
                openingField.SetValue(null, savedOpening);
                Time.timeScale = savedTime;
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }
    }
}
#endif
