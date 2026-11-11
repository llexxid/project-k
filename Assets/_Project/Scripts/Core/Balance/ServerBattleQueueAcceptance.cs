#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using Core.Stage;
using Newtonsoft.Json;
using Protocol.Message.Dto;
using Scripts.Core;
using Scripts.Core.Manager;
using Scripts.Core.Utils;
using UnityEngine;

namespace KingdomIdle.Balance
{
    public static class ServerBattleQueueAcceptance
    {
        /// <summary>빈 에디터 검증에서만 고유 로컬 파일을 사용한다. 실제 계정·네트워크는 열지 않는다.</summary>
        public static object RunMainPersistence()
        {
            if (NetworkManager.Instance != null || StageManager.Instance != null || UserManager.Instance != null || LocalProgression.IsReady)
                throw new InvalidOperationException("Main persistence acceptance requires an empty signed-out context.");
            var checks = new List<string>();
            void Check(bool valid, string name) { if (!valid) throw new InvalidOperationException("Main persistence acceptance: " + name); checks.Add(name); }
            var paths = new HashSet<string>();
            string account = "main-wave-" + Guid.NewGuid().ToString("N");
            var scope = LocalProgression.BeginTestSession();
            try
            {
                LocalProgression.OpenTestAccount(account); paths.Add(LocalProgression.SnapshotPath);
                const ulong monster = 4294967301;
                Check(LocalProgression.Execute("main-pending-fixture", state =>
                {
                    state.AttackLevel = 42; state.Wallet[eCurrency.Ruby] = 876;
                    state.MainBattle = new MainBattleSave { RunId = "1234567890abcdef1234567890abcdef", Stage = state.MainStage,
                        AcceptedKills = new() { [monster] = 2 } };
                    state.PendingMainCompletion = new MainWaveCompletionSave { RunId = state.MainBattle.RunId,
                        RequestId = "fedcba0987654321fedcba0987654321", Stage = state.MainStage, NextAction = 1,
                        TotalKills = new() { [monster] = 5 } };
                    return true;
                }), "pending identity, intent and full count are durably saved before sending");
                string before = JsonConvert.SerializeObject(LocalProgression.State);
                Check(!LocalProgression.TestFailedWrite(state => { state.PendingMainCompletion.RequestId = Guid.NewGuid().ToString("N"); return true; }) &&
                    JsonConvert.SerializeObject(LocalProgression.State) == before, "failed pending write leaves the previous committed request and investments intact");
                Check(!LocalProgression.Execute("malformed-main-pending", state => { state.PendingMainCompletion.RequestId = "bad"; return true; }) &&
                    JsonConvert.SerializeObject(LocalProgression.State) == before, "malformed pending identity cannot enter committed storage");
                string request = JsonConvert.SerializeObject(MainWaveProgression.Request(LocalProgression.State.PendingMainCompletion));
                LocalProgression.OpenTestAccount(account);
                Check(JsonConvert.SerializeObject(MainWaveProgression.Request(LocalProgression.State.PendingMainCompletion)) == request &&
                    LocalProgression.State.AttackLevel == 42 && LocalProgression.Balance(eCurrency.Ruby) == 876,
                    "restart reload retains exact request bytes and unrelated account investments");
                LocalProgression.OpenTestAccount(account + "-other"); paths.Add(LocalProgression.SnapshotPath);
                Check(LocalProgression.State.PendingMainCompletion == null && LocalProgression.State.MainBattle == null && !MainWaveProgression.Available,
                    "account change never reuses another account pending run or persisted capability");
                LocalProgression.OpenTestAccount(account);
                Check(JsonConvert.SerializeObject(MainWaveProgression.Request(LocalProgression.State.PendingMainCompletion)) == request,
                    "returning to the original account preserves its unresolved request");
                return new { passed = true, count = checks.Count, checks };
            }
            finally
            {
                scope.Dispose();
                string allowed = Path.GetFullPath(Path.Combine(Application.persistentDataPath, "progression-local-v1")) + Path.DirectorySeparatorChar;
                foreach (string path in paths)
                {
                    string full = Path.GetFullPath(path);
                    if (!full.StartsWith(allowed, StringComparison.OrdinalIgnoreCase)) throw new IOException("Unexpected acceptance snapshot path.");
                    foreach (string file in new[] { full, full + ".bak", full + ".tmp" }) if (File.Exists(file)) File.Delete(file);
                }
            }
        }

        public static object Run()
        {
            const BindingFlags instance = BindingFlags.Instance | BindingFlags.NonPublic;
            const BindingFlags statics = BindingFlags.Static | BindingFlags.NonPublic;
            var stateField = typeof(LocalProgression).GetField("_state", statics);
            var openingField = typeof(LocalProgression).GetField("_opening", statics);
            object savedState = stateField.GetValue(null), savedOpening = openingField.GetValue(null);
            var savedNetwork = NetworkManager.Instance;
            bool savedMainSupport = MainWaveProgression.Available;
            var savedUsers = UserManager.Instance;
            var savedSpawner = MonsterSpawner.Instance;
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
                void SetItem(object item, string name, object value) => item.GetType().GetField(name).SetValue(item, value);
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

                // Hold the transport: all clear/error fixtures below use the real queue without sending a request.
                Set("_huntsHeld", true);
                Set("_huntSyncFailed", false);
                fixture.ActiveDungeon = null;
                var spawner = owner.AddComponent<MonsterSpawner>();
                eStage Main(int chapter, int wave) => StageParser.MakeStage(eStageType.Main, chapter, wave);
                StageDefinition Definition(eStage id) => new StageDefinition(id, eStageFlowType.MainProgression,
                    eEnvironmentId.MainField1, 1, Array.Empty<StageMonsterEntry>(), null);
                StageSession Session(eStage id) => new StageSession(Definition(id), new MainStageRule(), spawner);
                object Clear(eStage id, bool loop, bool autoBoss, bool showResult = false)
                {
                    Empty(); buffer.Clear(); Set("_huntSyncFailed", false); Set("_huntInFlight", false);
                    Set("_currentStage", id); Set("_isLoopMode", loop); Set("_bossAutoChallenge", autoBoss);
                    Call("EnqueueStageClear", Definition(id), showResult);
                    Check(Count() == 1, "clear queues exactly once while transport is held");
                    return Items().GetValue(0);
                }
                void CheckAction(eStage id, bool loop, bool autoBoss, Protocol.Message.Data.eStageClearAction action, string name)
                {
                    object item = Clear(id, loop, autoBoss);
                    Check((Protocol.Message.Data.eStageClearAction)ItemField(item, "NextAction") == action, name);
                    Set("_isLoopMode", !loop); Set("_bossAutoChallenge", !autoBoss);
                    Check((Protocol.Message.Data.eStageClearAction)ItemField(item, "NextAction") == action,
                        name + " remains captured after preferences change");
                }
                CheckAction(Main(1, 2), true, true, Protocol.Message.Data.eStageClearAction.Repeat, "ordinary loop requests Repeat");
                CheckAction(Main(1, 10), false, false, Protocol.Message.Data.eStageClearAction.Repeat, "auto boss off repeats the pre-boss wave");
                CheckAction(Main(1, 10), false, true, Protocol.Message.Data.eStageClearAction.Advance, "auto boss on advances to the boss");
                CheckAction(Main(1, 2), false, false, Protocol.Message.Data.eStageClearAction.Advance, "auto boss off still advances ordinary waves");
                CheckAction(Main(1, 11), true, true, Protocol.Message.Data.eStageClearAction.Advance, "boss clear advances despite ordinary loop mode");

                Empty();
                var session = Session(Main(1, 2));
                Set("_currentSession", session); Set("_currentStage", session.Definition.Id);
                Set("_currentState", eStageRunState.Resolving);
                string beforeClear = JsonConvert.SerializeObject(fixture);
                manager.HandleRuleResult(session, StageRuleResult.MoveTo(Main(1, 3)));
                Check(Count() == 1 && (bool)ItemField(Items().GetValue(0), "IsClear") &&
                    (eStage)Get("_currentStage") == session.Definition.Id && ReferenceEquals(Get("_currentSession"), session) &&
                    (eStageRunState)Get("_currentState") == eStageRunState.Resolving && JsonConvert.SerializeObject(fixture) == beforeClear,
                    "clear waits for the server before stage transition or local rewards");
                Empty();
                manager.HandleRuleResult(session, StageRuleResult.ShowResult);
                Check(Count() == 1 && (bool)ItemField(Items().GetValue(0), "ShowResult") &&
                    (eStageRunState)Get("_currentState") == eStageRunState.Resolving && JsonConvert.SerializeObject(fixture) == beforeClear,
                    "main result popup also waits for the clear acknowledgement");

                PlayFab.PlayFabError Failure(int code) => code == 0 ? NetworkManager.CreateError("Timeout", "acceptance fixture") :
                    NetworkManager.CreateError("Business", "acceptance fixture", details: new ErrorResponseDTO
                    { ErrorCode = (Protocol.Message.Data.eErrorCode)code, Message = "acceptance fixture" });
                void CheckRetry(bool clear, int code, int attempts, bool retry)
                {
                    object item = Clear(Main(1, 2), true, true);
                    SetItem(item, "IsClear", clear); SetItem(item, "Attempts", attempts);
                    Time.timeScale = .5f;
                    Call("OnHuntRewardError", item, Failure(code));
                    Check((bool)Get("_huntSyncFailed") == !retry && Count() == 1 && ReferenceEquals(Items().GetValue(0), item) &&
                        Time.timeScale == (retry ? .5f : 0f),
                        (clear ? "clear" : "hunt") + " error " + code + " attempt " + attempts + (retry ? " waits for retry" : " halts with original queue preserved"));
                    Call("TrySendNextHunt");
                    Check(Count() == 1 && !(bool)Get("_huntInFlight"), "held or failed queue does not issue another request");
                }
                CheckRetry(true, 0, 1, false);
                CheckRetry(true, 20, 1, false);
                CheckRetry(true, 1014, 1, false);
                CheckRetry(true, 1017, 1, true);
                int maxAttempts = (int)typeof(StageManager).GetField("HuntMaxAttempts", statics).GetRawConstantValue();
                CheckRetry(true, 1017, maxAttempts, false);
                CheckRetry(false, 0, 1, true);
                CheckRetry(false, 20, 1, true);
                CheckRetry(false, 1014, 1, false);
                CheckRetry(false, 1025, 1, false);

                UserStageQuery ServerStage(eStage current, eStage highest, long cleared) => new UserStageQuery
                {
                    CurrentStage = (Protocol.Message.Data.eStage)(long)current,
                    HighestStage = (Protocol.Message.Data.eStage)(long)highest,
                    MainClear = (Protocol.Message.Data.eStage)cleared
                };
                void RejectClear(UserStageQuery stage, eStage sessionStage, string name)
                {
                    object item = Clear(Main(1, 2), false, true);
                    Set("_currentSession", Session(sessionStage));
                    var seen = (Dictionary<long, HashSet<ulong>>)Get("_saturatedTypes");
                    seen[(long)Main(1, 2)] = new HashSet<ulong> { (ulong)monster };
                    string before = JsonConvert.SerializeObject(fixture);
                    Call("OnStageClearSuccess", item, new OnStageClearResponseDTO { StageState = stage });
                    Check((bool)Get("_huntSyncFailed") && Count() == 1 && ReferenceEquals(Items().GetValue(0), item) &&
                        seen.ContainsKey((long)Main(1, 2)) && JsonConvert.SerializeObject(fixture) == before,
                        name + " is rejected before local rewards, dequeue or saturation changes");
                    seen.Clear();
                }
                RejectClear(null, Main(1, 2), "missing server stage");
                RejectClear(ServerStage(Main(1, 2), Main(1, 3), (long)Main(1, 2)), Main(1, 2), "wrong Advance destination");
                RejectClear(ServerStage(Main(1, 3), Main(1, 3), (long)Main(1, 2)), Main(1, 4), "stale stage session");
                RejectClear(ServerStage(Main(1, 3), Main(1, 2), (long)Main(1, 2)), Main(1, 2), "frontier below current");
                RejectClear(ServerStage(Main(1, 3), Main(1, 3), (long)Main(1, 1)), Main(1, 2), "missing permanent clear record");
                RejectClear(ServerStage(Main(1, 3), Main(1, 3), dungeon), Main(1, 2), "non-main permanent clear record");

                var apply = typeof(StageManager).GetMethod("ApplyMainStageState", statics);
                var projection = new ProgressionState { MainStage = (long)Main(9, 9), CycleFrontierStage = (long)Main(9, 10),
                    HighestMainClear = (long)Main(9, 8), AttackLevel = 42, HealthLevel = 40, RubyExpLevel = 1 };
                projection.Wallet[eCurrency.Ruby] = 276;
                var expectedProjection = projection.DeepClone();
                expectedProjection.MainStage = (long)Main(2, 1); expectedProjection.CycleFrontierStage = (long)Main(3, 1);
                expectedProjection.CycleBossStage = 2; expectedProjection.HighestMainClear = (long)Main(7, 11);
                Check((bool)apply.Invoke(null, new object[] { projection, ServerStage(Main(2, 1), Main(3, 1), (long)Main(7, 11)) }) &&
                    JsonConvert.SerializeObject(projection) == JsonConvert.SerializeObject(expectedProjection),
                    "server projection replaces current-cycle fields and preserves all unrelated investment");
                Check((bool)apply.Invoke(null, new object[] { projection, ServerStage(Main(1, 1), Main(1, 1), (long)Main(7, 11)) }) &&
                    projection.MainStage == (long)Main(1, 1) && projection.CycleFrontierStage == (long)Main(1, 1) &&
                    projection.CycleBossStage == 0 && projection.HighestMainClear == (long)Main(7, 11),
                    "post-reincarnation current-cycle frontier may be below permanent clear history");
                foreach (var invalid in new[] { null, ServerStage((eStage)dungeon, Main(1, 1), 0),
                    ServerStage(Main(2, 1), Main(1, 1), 0), ServerStage(Main(1, 1), Main(1, 1), dungeon),
                    ServerStage((eStage)((long)Main(1, 1) + 11), Main(2, 1), 0) })
                {
                    string before = JsonConvert.SerializeObject(projection);
                    Check(!(bool)apply.Invoke(null, new object[] { projection, invalid }) && JsonConvert.SerializeObject(projection) == before,
                        "invalid server stage projection leaves the entire state untouched");
                }
                Set("_huntSyncFailed", false); Set("_mainStageRequestPending", true);
                Check(!(bool)Call("TransitionStage", Main(1, 1), 0f), "pending server movement blocks another local transition");
                Set("_mainStageRequestPending", false); Set("_dungeonRequestPending", true);
                Check(!(bool)Call("TransitionStage", Main(1, 1), 0f), "pending dungeon request blocks a local main transition");
                Set("_dungeonRequestPending", false); Set("_currentSession", null); Empty();

                const string mainRun = "1234567890abcdef1234567890abcdef";
                const string requestId = "fedcba0987654321fedcba0987654321";
                object newMain = Clear(Main(1, 2), false, true);
                var completion = new MainWaveCompletionSave { RunId = mainRun, RequestId = requestId,
                    Stage = (long)Main(1, 2), NextAction = 0, TotalKills = new() { [(ulong)monster] = 1 } };
                SetItem(newMain, "MainRunId", mainRun); SetItem(newMain, "Completion", completion); SetItem(newMain, "Attempts", 1);
                Time.timeScale = .5f;
                Call("OnMainWaveError", newMain, Failure(1014));
                Check(!(bool)Get("_huntSyncFailed") && Count() == 1 && ReferenceEquals(Items().GetValue(0), newMain) &&
                    (float)Get("_nextHuntSendAt") > Time.realtimeSinceStartup && completion.RequestId == requestId &&
                    completion.TotalKills[(ulong)monster] == 1 && Time.timeScale == .5f,
                    "new completion storage uncertainty retries the same durable identity and payload on its own deadline");
                SetItem(newMain, "Attempts", maxAttempts);
                Call("OnMainWaveError", newMain, Failure(1014));
                Check((bool)Get("_huntSyncFailed") && Count() == 1 && completion.RequestId == requestId && Time.timeScale == 0,
                    "exhausted completion retry preserves the original request and halts without a confirmed state");
                newMain = Clear(Main(1, 2), false, true);
                SetItem(newMain, "MainRunId", mainRun); SetItem(newMain, "Completion", completion); SetItem(newMain, "Attempts", 1);
                Call("OnMainWaveError", newMain, Failure(1007));
                Check((bool)Get("_huntSyncFailed") && Count() == 1 && ReferenceEquals(Items().GetValue(0), newMain),
                    "new main invalid-loot response never recursively splits or discards the completed run");
                fixture.PendingMainCompletion = completion.Copy();
                string pendingBefore = JsonConvert.SerializeObject(fixture);
                Set("_huntSyncFailed", false);
                Call("OnMainCompletionSuccess", newMain, new OnCompleteMainWaveResponseDTO { RequestId = mainRun, CompletedRunId = mainRun });
                Check((bool)Get("_huntSyncFailed") && JsonConvert.SerializeObject(fixture) == pendingBefore && Count() == 1,
                    "mismatched main completion identity cannot change local progress or discard pending data");
                fixture.PendingMainCompletion = null;
                Set("_nextHuntSendAt", 0f); Set("_huntInFlight", false); Set("_huntSyncFailed", false); Empty();

                buffer[main] = new Dictionary<eMonsterType, int> { [monster] = 1 };
                Call("FlushHuntBuffer", main);
                var uncertainPartial = Items().GetValue(0);
                SetItem(uncertainPartial, "MainRunId", mainRun); SetItem(uncertainPartial, "BatchId", requestId); SetItem(uncertainPartial, "Attempts", 1);
                SetItem(uncertainPartial, "StageId", (long)Main(1, 2));
                var queueFinal = Clear(Main(1, 2), false, true);
                SetItem(queueFinal, "MainRunId", mainRun); SetItem(queueFinal, "Completion", completion);
                Empty();
                var uncertainQueue = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(uncertainPartial.GetType()));
                uncertainQueue.Add(uncertainPartial); uncertainQueue.Add(queueFinal);
                Call("PushFront", uncertainQueue);
                Call("OnMainWaveError", uncertainPartial, Failure(0));
                Check(Count() == 1 && ReferenceEquals(Items().GetValue(0), queueFinal) && completion.RequestId == requestId,
                    "partial ACK loss promotes its already durable cumulative final without replaying partial or changing its identity");
                var responseToken = Get("_token");
                Check((bool)Call("OwnsHuntResponse", queueFinal, network, LocalProgression.AccountGeneration, network.AccountGeneration, responseToken) &&
                    !(bool)Call("OwnsHuntResponse", uncertainPartial, network, LocalProgression.AccountGeneration, network.AccountGeneration, responseToken) &&
                    !(bool)Call("OwnsHuntResponse", queueFinal, network, LocalProgression.AccountGeneration + 1, network.AccountGeneration, responseToken) &&
                    !(bool)Call("OwnsHuntResponse", queueFinal, network, LocalProgression.AccountGeneration, network.AccountGeneration + 1, responseToken),
                    "late partial callback and changed account generations cannot own the final or overwrite its balances");
                Empty();

                var tailDefinition = new StageDefinition(Main(1, 2), eStageFlowType.MainProgression, eEnvironmentId.MainField1, 1,
                    new[] { new StageMonsterEntry(monster, 5, 0, eMonsterSpawnPhase.Initial, null, 0) }, null);
                var tailSession = new StageSession(tailDefinition, new MainStageRule(), spawner);
                typeof(StageSession).GetField("_totalKillCount", instance).SetValue(tailSession, 2);
                Set("_currentSession", tailSession); Set("_mainRunForSession", mainRun); Set("_currentState", eStageRunState.Running);
                SetItem(uncertainPartial, "Attempts", 0);
                uncertainQueue.Clear(); uncertainQueue.Add(uncertainPartial); Call("PushFront", uncertainQueue);
                long tailAccount = LocalProgression.AccountGeneration, tailNetwork = network.AccountGeneration;
                Call("BindMainTailWindow", mainRun, tailAccount, tailNetwork);
                Check((bool)Call("DeferMainTail", uncertainPartial, 100f) && (float)Get("_mainTailDeadline") == 103f,
                    "periodic main partial with three definition kills left gets one bounded grace window");
                Check((bool)Call("DeferMainTail", uncertainPartial, 102.9f) && (float)Get("_mainTailDeadline") == 103f,
                    "repeated reports never extend the tail deadline");
                Call("PumpMainTail", 102.99f);
                Check((bool)Get("_mainTailWaiting") && Count() == 1, "tail pump preserves the unsent partial before its deadline");
                Call("PumpMainTail", 103f);
                Check(!(bool)Get("_mainTailWaiting") && !(bool)Call("DeferMainTail", uncertainPartial, 103.01f) &&
                    (float)Get("_mainTailDeadline") == 103f && Count() == 1,
                    "slow last enemy resumes from the exact deadline without another three-second poll or repeated grace");
                Call("BindMainTailWindow", mainRun, tailAccount, tailNetwork);
                Check((float)Get("_mainTailDeadline") == 103f, "same server run rebind does not grant another grace window");
                Call("BindMainTailWindow", requestId, tailAccount, tailNetwork);
                Set("_mainRunForSession", requestId); SetItem(uncertainPartial, "MainRunId", requestId);
                Check((float)Get("_mainTailDeadline") == 0f && (bool)Call("DeferMainTail", uncertainPartial, 200f),
                    "a newly confirmed server run receives a fresh grace window");
                typeof(StageSession).GetField("_totalKillCount", instance).SetValue(tailSession, 5);
                SetItem(uncertainPartial, "IsClear", true);
                Check(!(bool)Call("DeferMainTail", uncertainPartial, 200.5f) && !(bool)Get("_mainTailWaiting"),
                    "fast final completion bypasses and cancels the waiting periodic partial window");
                SetItem(uncertainPartial, "IsClear", false);
                typeof(StageSession).GetField("_totalKillCount", instance).SetValue(tailSession, 2);
                Set("_onHuntsSettled", (Action<bool>)(_ => { }));
                Check(!(bool)Call("DeferMainTail", uncertainPartial, 201f) && (float)Get("_mainTailDeadline") == 203f,
                    "explicit move, defeat, dungeon and reincarnation drain bypass the tail grace immediately");
                Set("_onHuntsSettled", null); SetItem(uncertainPartial, "Attempts", 1);
                Check(!(bool)Call("DeferMainTail", uncertainPartial, 201f), "already attempted partial retries never gain a periodic grace delay");
                Call("BindMainTailWindow", requestId, tailAccount + 1, tailNetwork);
                Check((float)Get("_mainTailDeadline") == 0f && !(bool)Get("_mainTailWaiting"), "account generation changes discard the previous grace deadline");
                Set("_mainTailDeadline", 300f); Call("BindMainTailWindow", requestId, tailAccount + 1, tailNetwork + 1);
                Check((float)Get("_mainTailDeadline") == 0f, "network session changes do not reuse an old account request deadline");
                Empty();

                var defeatedSession = Session(Main(1, 2));
                var defeatedKills = (Dictionary<eMonsterType, int>)typeof(StageSession).GetField("_killCounts", instance).GetValue(defeatedSession);
                defeatedKills[monster] = 3;
                typeof(StageSession).GetProperty(nameof(StageSession.HasPendingResult)).SetValue(defeatedSession, true);
                typeof(StageSession).GetField("_spawningCompleted", instance).SetValue(defeatedSession, true);
                fixture.MainStage = (long)Main(1, 2);
                fixture.MainBattle = new MainBattleSave { RunId = mainRun, Stage = fixture.MainStage,
                    AcceptedKills = new() { [(ulong)monster] = 2 } };
                MainWaveProgression.ReceiveServerState(true);
                Set("_mainRunForSession", mainRun); Set("_currentSession", defeatedSession);
                Set("_currentState", eStageRunState.DefeatPending); Set("_huntSyncFailed", false);
                Check(!(bool)Call("FlushMainWaveHunts", false) && Count() == 0,
                    "periodic main reporting does not run inside a defeat result");
                Set("_huntsHeld", false); Set("_huntInFlight", true);
                int tailCallbacks = 0;
                Time.timeScale = .5f;
                Check(manager.FlushPendingHunts(_ => tailCallbacks++) && Count() == 1 &&
                    Loots(Items().GetValue(0))[0].Count == 1 && (string)ItemField(Items().GetValue(0), "MainRunId") == mainRun &&
                    Time.timeScale == 0f && tailCallbacks == 0,
                    "explicit defeat retreat or retry queues the tail even after late effects remove the remaining monsters");
                Set("_huntsHeld", true);
                Time.timeScale = .5f;
                Call("ResumeMainHuntAfterRefresh", .5f);
                Check(Time.timeScale == 0f && tailCallbacks == 0 && Count() == 1,
                    "state confirmation during explicit settlement preserves the battle pause while a tail remains");
                Set("_onHuntsSettled", null);
                Call("ResumeMainHuntAfterRefresh", .5f);
                Check(Time.timeScale == .5f, "ordinary partial state confirmation restores the captured battle speed");
                fixture.MainBattle = null; fixture.MainStage = main;
                Set("_mainRunForSession", null); Set("_currentSession", null); Set("_huntInFlight", false);
                MainWaveProgression.ReceiveServerState(false);
                Empty();

                var automaticMethod = typeof(StageManager).GetMethod("IsAutomaticMainClearTransition", statics);
                var fadeMethod = typeof(StageManager).GetMethod("StageFadeOutSeconds", statics);
                bool automaticFade = (bool)automaticMethod.Invoke(null, new object[] { Definition(Main(1, 2)), Definition(Main(1, 3)), 100f });
                Set("_currentSession", null);
                Check(automaticFade && (float)fadeMethod.Invoke(null, new object[] { Main(1, 2), Definition(Main(1, 3)), automaticFade }) == .1f,
                    "captured automatic-clear context keeps its short same-resource fade after source session teardown or hold");
                Check(!(bool)automaticMethod.Invoke(null, new object[] { Definition(Main(1, 2)), Definition(Main(1, 3)), 0f }) &&
                    (float)fadeMethod.Invoke(null, new object[] { Main(1, 2), Definition(Main(1, 3)), false }) == .3f,
                    "manual same-resource movement keeps the previous fade duration");
                Check((float)fadeMethod.Invoke(null, new object[] { Main(1, 11), Definition(Main(2, 1)), true }) == .4f &&
                    (float)fadeMethod.Invoke(null, new object[] { (eStage)dungeon, Definition((eStage)dungeon), true }) == .3f,
                    "resource changes and dungeon transitions retain their previous fades");
                float clearHold = (float)typeof(StageManager).GetField("MainWaveClearHoldSeconds", statics).GetRawConstantValue();
                Check(Math.Abs(clearHold + .1f - 1f) < .0001f, "automatic main hold and short fade preserve an approximately one-second minimum");
                var originalToken = (CancellationTokenSource)Get("_token");
                using (var heldToken = new CancellationTokenSource())
                using (var replacementToken = new CancellationTokenSource())
                {
                    try
                    {
                        Set("_token", heldToken);
                        Check((bool)Call("CanPresentHeldStage", LocalProgression.AccountGeneration, heldToken.Token, network, network.AccountGeneration) &&
                            !(bool)Call("CanPresentHeldStage", LocalProgression.AccountGeneration + 1, heldToken.Token, network, network.AccountGeneration) &&
                            !(bool)Call("CanPresentHeldStage", LocalProgression.AccountGeneration, heldToken.Token, network, network.AccountGeneration + 1),
                            "held presentation resumes only for its original local and network account generations");
                        Set("_token", replacementToken);
                        Check(!(bool)Call("CanPresentHeldStage", LocalProgression.AccountGeneration, heldToken.Token, network, network.AccountGeneration),
                            "new stage session cannot reuse an earlier asynchronous hold");
                        Set("_token", heldToken); heldToken.Cancel();
                        Check(!(bool)Call("CanPresentHeldStage", LocalProgression.AccountGeneration, heldToken.Token, network, network.AccountGeneration),
                            "canceled hold never resumes stage presentation");
                    }
                    finally { Set("_token", originalToken); }
                }

                buffer[main] = new Dictionary<eMonsterType, int> { [monster] = 1 };
                Call("FlushHuntBuffer", main);
                buffer[dungeon] = new Dictionary<eMonsterType, int> { [monster] = 1 };
                var saturated = (Dictionary<long, HashSet<ulong>>)Get("_saturatedTypes");
                saturated[main] = new HashSet<ulong> { (ulong)monster };
                Set("_huntInFlight", true); Set("_onHuntsSettled", (Action<bool>)(_ => callbacks++));
                Set("_dungeonRequestPending", true); Set("_retryDungeon", (Action)(() => callbacks++));
                Set("_mainStageRequestPending", true);
                // Block scene loading; only BeginStage's fresh-session reset is under test.
                Set("_currentState", eStageRunState.Transitioning);
                fixture.ActiveDungeon = null;
                manager.BeginStage((eStage)main);
                Check(Count() == 0 && buffer.Count == 0 && saturated.Count == 0 && !(bool)Get("_huntInFlight") && !(bool)Get("_huntSyncFailed"),
                    "fresh begin clears old queue, buffers, saturation and failure state");
                Check(Get("_onHuntsSettled") == null && Get("_retryDungeon") == null && !(bool)Get("_dungeonRequestPending") &&
                    !(bool)Get("_mainStageRequestPending") && callbacks == 1,
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
                MonsterSpawner.Instance = savedSpawner;
                stateField.SetValue(null, savedState);
                openingField.SetValue(null, savedOpening);
                MainWaveProgression.ReceiveServerState(savedMainSupport);
                Time.timeScale = savedTime;
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }
    }
}
#endif
