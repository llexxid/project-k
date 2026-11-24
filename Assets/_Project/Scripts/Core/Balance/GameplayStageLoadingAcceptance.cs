#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Core.Stage;
using Cysharp.Threading.Tasks;
using KingdomIdle.UGUI;
using Newtonsoft.Json;
using PlayFab;
using PlayFab.CloudScriptModels;
using Protocol.Message.Dto;
using Scripts.Core;
using Scripts.Core.Manager;
using Scripts.Core.Utils;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomIdle.Balance
{
    public static class GameplayStageLoadingAcceptance
    {
        public static string CurrentCase { get; private set; }
        public static bool PauseAtCheckpoints { get; set; }
        public static bool WaitingForCapture { get; private set; }
        private static bool _continue;
        public static void Continue() => _continue = true;

        /// <summary>빈 signed-out PlayMode에서 코루틴으로 실행한다. 캡처는 PauseAtCheckpoints=true와 Continue()를 사용한다.</summary>
        public static IEnumerator Run(Action<object> completed = null)
        {
            using var fixture = CreateFixture();
            try
            {
                fixture.Prepare(fixture.Main);
                fixture.Set("_huntInFlight", true);
                fixture.Check(fixture.Manager.TryMoveToMainStage(fixture.Previous), "manual movement accepts a valid earlier stage");
                yield return Checkpoint(fixture, "main-move-settlement");
                fixture.Call("FinishHuntSettlement", false);
                fixture.CheckEnded("manual movement settlement failure releases loading without rewards");

                fixture.Prepare(fixture.Main);
                fixture.Check(fixture.Manager.TryMoveToMainStage(fixture.Previous), "manual movement starts the real queued request");
                var moving = fixture.Loading;
                fixture.Check(fixture.Transport.Function == "OnChangeMainStage", "movement waits on the isolated HTTP transport");
                fixture.Transport.Fail(NetworkManager.CreateError("Timeout", "stage acceptance"));
                fixture.Check(fixture.Transport.Function == "OnGetUserState" && ReferenceEquals(moving, fixture.Loading),
                    "uncertain movement retains the same loading while confirming server position");
                yield return Checkpoint(fixture, "main-move-recovery");
                fixture.Transport.Fail(NetworkManager.CreateError("Transport", "stage acceptance state unavailable"));
                fixture.CheckEnded("movement recovery failure releases loading and preserves the original state");

                fixture.Prepare(fixture.Main);
                fixture.Set("_huntsHeld", true);
                fixture.Manager.HandleRuleResult(fixture.Session, StageRuleResult.MoveTo(fixture.Next));
                var clear = fixture.Loading;
                float readyAt = (float)Field(typeof(GameplayLoading.Scope), "_notBefore").GetValue(clear);
                fixture.Check(clear != null && !GameplayLoading.IsVisible && readyAt > Time.realtimeSinceStartup + .8f,
                    "automatic clear keeps its normal 0.9-second presentation outside loading time");
                yield return Checkpoint(fixture, "main-clear-server-wait", readyAt);
                fixture.Call("OnHuntRewardError", fixture.Head, NetworkManager.CreateError("InvalidResponse", "stage acceptance"));
                fixture.CheckEnded("terminal automatic-clear failure releases loading without clearing saved rewards");

                fixture.Prepare(fixture.Main);
                fixture.Set("_huntsHeld", true);
                fixture.Buffer[(long)fixture.Main] = new Dictionary<eMonsterType, int> { [(eMonsterType)4294967574UL] = 1 };
                fixture.Call("FlushHuntBuffer", (long)fixture.Main);
                fixture.Call("TrySendNextHunt");
                fixture.Check(fixture.Loading == null && !GameplayLoading.IsVisible, "background hunt buffering and dispatch never start loading");

                fixture.Prepare(fixture.Dungeon);
                fixture.Set("_currentState", eStageRunState.ResultPending);
                fixture.Set("_resultPopupTimer", 1f);
                LocalProgression.State.ActiveDungeon = null;
                LocalProgression.State.GoldDungeonClear = 1;
                var nextDifficulty = StageParser.MakeStage(eStageType.GoldDungeon, 2);
                LocalProgression.State.GoldTickets = 0;
                fixture.Check(fixture.Manager.AutoDungeonDestination == null, "no ticket returns to main without a new entry");
                LocalProgression.State.GoldTickets = 1;
                fixture.Manager.AutoAdvanceDungeon = false;
                fixture.Check(fixture.Manager.AutoDungeonDestination == fixture.Dungeon, "default auto action repeats the same difficulty");
                fixture.Manager.AutoAdvanceDungeon = true;
                fixture.Check(fixture.Manager.AutoDungeonDestination == nextDifficulty, "toggle chooses the newly cleared server-unlocked next difficulty");
                LocalProgression.State.GoldDungeonClear = 0;
                fixture.Check(fixture.Manager.AutoDungeonDestination == fixture.Dungeon, "a locked next difficulty falls back to the current one");
                fixture.Call("TickDungeonResult", .5f);
                fixture.Check(fixture.Transport.Function == null, "remaining reading time does not start an entry");
                fixture.Call("TickDungeonResult", 1f);
                var resultRequest = fixture.Loading;
                fixture.Check(fixture.Transport.Function == "OnGetUserState" && !fixture.Manager.DungeonResultCountdownActive,
                    "timer starts one normal server-verified entry with no local ticket consumption");
                fixture.Manager.RestartDungeon();
                fixture.Call("TickDungeonResult", 10f);
                fixture.Check(ReferenceEquals(resultRequest, fixture.Loading) && LocalProgression.State.GoldTickets == 1,
                    "manual and elapsed retry cannot race an outstanding entry");
                fixture.Manager.AutoAdvanceDungeon = false;

                fixture.Prepare(fixture.Main);
                fixture.Set("_huntInFlight", true);
                fixture.Check(fixture.Manager.TryEnterDungeon(fixture.Dungeon), "dungeon entry accepts the unlocked fixture dungeon");
                yield return Checkpoint(fixture, "dungeon-enter-settlement");
                fixture.Call("FinishHuntSettlement", false);
                fixture.CheckEnded("dungeon entry settlement failure releases loading without spending tickets");

                fixture.Prepare(fixture.Main);
                fixture.Check(fixture.Manager.TryEnterDungeon(fixture.Dungeon), "dungeon entry starts server-state confirmation");
                var entering = fixture.Loading;
                fixture.Check(fixture.Transport.Function == "OnGetUserState", "dungeon entry confirms the previous server run before spending a ticket");
                fixture.Transport.Fail(NetworkManager.CreateError("Transport", "stage acceptance"));
                fixture.Check(ReferenceEquals(entering, fixture.Loading) && fixture.Get("_retryDungeon") != null,
                    "entry confirmation retry keeps the original loading scope");
                yield return Checkpoint(fixture, "dungeon-enter-recovery");
                fixture.Call("DungeonRequestFailed", "stage acceptance terminal entry rejection");
                fixture.CheckEnded("terminal entry recovery handler releases loading");

                fixture.Prepare(fixture.Dungeon);
                fixture.Manager.HandleRuleResult(fixture.Session, StageRuleResult.ShowResult);
                fixture.Check(fixture.Transport.Function == "OnCompleteDungeon", "dungeon result waits on the actual completion request");
                yield return Checkpoint(fixture, "dungeon-complete");
                fixture.CheckRetryAndReject("OnCompleteDungeon");
                fixture.CheckEnded("dungeon completion retry ends loading on a definite rejection");

                fixture.Prepare(fixture.Dungeon);
                fixture.Manager.ReturnToMainStage();
                fixture.Check(fixture.Transport.Function == "OnExitDungeon", "return to main waits on the actual dungeon exit request");
                yield return Checkpoint(fixture, "dungeon-exit");
                fixture.CheckRetryAndReject("OnExitDungeon");
                fixture.CheckEnded("dungeon exit retry ends loading on a definite rejection");

                fixture.Prepare(fixture.Main);
                fixture.Set("_huntInFlight", true);
                fixture.Manager.TryMoveToMainStage(fixture.Previous);
                fixture.Call("FinishHuntSettlement", true);
                fixture.Transport.Fail(NetworkManager.CreateError("Timeout", "stage acceptance"));
                fixture.Transport.Discard();
                fixture.Call("ResumeMainHuntAfterRefresh", 1f);
                fixture.CheckEnded("confirmed partial-recovery completion releases loading when no clear is pending");

                fixture.Prepare(fixture.Main);
                fixture.Check((bool)fixture.Call("TransitionStage", fixture.Previous, 0f), "same-scene resource transition is accepted");
                yield return null;
                fixture.CheckEnded("missing required background ends resource loading through the production failure handler");

                fixture.Prepare(fixture.Main);
                using (var oldToken = new CancellationTokenSource())
                using (var replacementToken = new CancellationTokenSource())
                {
                    fixture.Set("_token", oldToken);
                    fixture.Set("_huntInFlight", true);
                    fixture.Manager.TryMoveToMainStage(fixture.Previous);
                    var obsolete = fixture.Loading;
                    fixture.Call("EndStageLoading");
                    fixture.Set("_token", replacementToken);
                    fixture.Call("BeginStageLoading", "stage-loading-replacement", 0f);
                    var replacement = fixture.Loading;
                    fixture.Call("HandleStageLoadFailure", new InvalidOperationException("late resource fixture"), null, obsolete, oldToken);
                    fixture.Check(ReferenceEquals(replacement, fixture.Loading) && !replacement.IsDisposed &&
                        (eStageRunState)fixture.Get("_currentState") == eStageRunState.Running,
                        "late resource failure cannot close or stop a newer stage scope");
                    var ignored = (Cysharp.Threading.Tasks.UniTask)fixture.Call("PrepareStageBackground", Definitions.Definition(fixture.Previous), obsolete, oldToken);
                    fixture.Check(ignored.GetAwaiter().IsCompleted && ReferenceEquals(replacement, fixture.Loading),
                        "obsolete resource completion cannot prepare or close the replacement stage");
                    fixture.Set("_token", null);
                }

                fixture.Prepare(fixture.Main);
                fixture.Set("_huntInFlight", true);
                fixture.Manager.TryMoveToMainStage(fixture.Previous);
                var reset = fixture.Loading;
                GameplayLoading.Reset();
                fixture.Check(reset.IsDisposed && !GameplayLoading.IsVisible, "account loading reset disposes an active stage scope");

                fixture.Prepare(fixture.Main);
                fixture.Set("_huntInFlight", true);
                fixture.Manager.TryMoveToMainStage(fixture.Previous);
                var destroyed = fixture.Loading;
                fixture.Call("OnDestroy");
                fixture.Check(destroyed.IsDisposed && fixture.Loading == null && !GameplayLoading.IsVisible,
                    "stage destruction handler disposes its pending scope");
                fixture.Check(fixture.Transport.Unexpected == 0 && LocalProgression.AccountKey == null &&
                    LocalProgression.AccountGeneration == fixture.AccountGeneration,
                    "all requests were intercepted without opening an account or changing its generation");
                completed?.Invoke(new { passed = true, count = fixture.Checks.Count, checks = fixture.Checks });
            }
            finally
            {
                CurrentCase = null;
                WaitingForCapture = false;
                PauseAtCheckpoints = false;
            }
        }

        /// <summary>기존 7개 파트를 반복하지 않고 로그인 복구와 필수 전투 리소스 대기만 캡처한다.</summary>
        public static IEnumerator RunAdditionalEvidence(Action<object> completed = null)
        {
            using var fixture = CreateFixture();
            bool supported = MainWaveProgression.Available;
            try
            {
                fixture.Prepare(fixture.Main);
                var state = LocalProgression.State;
                state.MainBattle = new MainBattleSave { RunId = Guid.NewGuid().ToString("N"), Stage = (long)fixture.Main };
                state.PendingMainCompletion = new MainWaveCompletionSave
                {
                    RunId = state.MainBattle.RunId, RequestId = Guid.NewGuid().ToString("N"), Stage = (long)fixture.Main,
                    NextAction = 0, TotalKills = new Dictionary<ulong, int> { [4294967574UL] = 1 }
                };
                fixture.RememberState();
                MainWaveProgression.ReceiveServerState(true);
                fixture.Manager.BeginStage(fixture.Main);
                fixture.Check(fixture.Transport.Function == "OnCompleteMainWave" && fixture.Loading?.Operation == "main-completion-recovery",
                    "main scene entry resumes the exact saved completion before starting a battle");
                yield return Checkpoint(fixture, "login-main-completion-recovery");
                fixture.Transport.Fail(NetworkManager.CreateError("InvalidResponse", "stage acceptance pending completion"));
                fixture.Check(fixture.Transport.Function == "OnGetUserState", "uncertain login completion confirms the server state");
                fixture.Transport.Fail(NetworkManager.CreateError("Transport", "stage acceptance recovery unavailable"));
                fixture.CheckEnded("login completion recovery failure releases loading and preserves the saved request");
                MainWaveProgression.ReceiveServerState(false);

                fixture.Prepare(fixture.Dungeon);
                fixture.Manager.BeginStage(fixture.Main);
                fixture.Check(fixture.Transport.Function == "OnExitDungeon" && fixture.Loading?.Operation == "dungeon-exit",
                    "main scene entry exits the saved unfinished dungeon before returning to main");
                yield return Checkpoint(fixture, "login-dungeon-recovery");
                fixture.Transport.Fail(NetworkManager.CreateError("Business", "stage acceptance definite recovery rejection", details: new ErrorResponseDTO
                { ErrorCode = (Protocol.Message.Data.eErrorCode)1025, Message = "stage acceptance" }));
                fixture.CheckEnded("login dungeon recovery failure releases loading without returning the ticket");

                fixture.Prepare(fixture.Main);
                var destination = StageParser.MakeStage(eStageType.Main, 2);
                var resources = fixture.HoldRequiredResources(destination);
                fixture.Check((bool)fixture.Call("TransitionStage", destination, 0f), "resource-group transition enters the real LoadManager path");
                fixture.Check(fixture.Loading != null && !resources.Task.GetAwaiter().IsCompleted,
                    "required monster resources remain pending in the production stage resource cache");
                yield return Checkpoint(fixture, "stage-required-resources");
                resources.TrySetException(new InvalidOperationException("stage acceptance required resource unavailable"));
                yield return null;
                fixture.CheckEnded("required resource failure releases loading through the real LoadStage failure callback");
                fixture.Check(fixture.Transport.Unexpected == 0 && LocalProgression.AccountKey == null &&
                    LocalProgression.AccountGeneration == fixture.AccountGeneration,
                    "additional evidence opens no account, sends no external HTTP and writes no progression file");
                completed?.Invoke(new { passed = true, count = fixture.Checks.Count, checks = fixture.Checks });
            }
            finally
            {
                MainWaveProgression.ReceiveServerState(supported);
                CurrentCase = null;
                WaitingForCapture = false;
                PauseAtCheckpoints = false;
            }
        }

        public static object RunEconomyTransportRecovery()
        {
            using var fixture = CreateFixture();
            using var account = LocalProgression.BeginTestSession();
            Field(typeof(LocalProgression), "_opening").SetValue(null, false);
            LocalProgression.OpenTestAccount("economy-transport-" + Guid.NewGuid().ToString("N"));
            var network = NetworkManager.Instance;
            var snapshot = JsonConvert.DeserializeObject<OnGetUserStateResponseDTO>(JsonConvert.SerializeObject(
                Invoke(typeof(ServerApiContractAcceptance), null, "LegacyLogin")));
            snapshot.EconomyRevision = 1;
            snapshot.ProtocolVersion = IdleKingdomFunction.Common.ApiVersions.ProtocolVersion;
            snapshot.RubyGrowthState = new() { NextRequestSequence = 1, LastResetDay = "" };
            snapshot.EconomyBatchState = new()
            {
                Version = 1, OwnerClientId = EconomyBatchProgression.ClientId, Generation = 1, NextSequence = 1,
                RewardEpoch = 1, RewardEpochs = new() { new() { Id = 1 } }
            };
            snapshot.DungeonState.TicketResetUtc = LocalProgression.UtcNow + 86400;
            snapshot.ServerTimeUtc = LocalProgression.UtcNow;
            fixture.Check(UserManager.Instance.ApplyServerPlayerData(snapshot), "economy full state is durably installed for isolated transport");
            bool recovered = false;
            fixture.Check(EconomyBatchProgression.RecoverPending(ok => recovered = ok, true), "fresh login recovery begins");
            void Drain()
            {
                EconomyBatchProgression.FlushLocalWrites();
                EconomyBatchProgression.Pump();
            }
            Drain(); Drain();
            fixture.Check(recovered, "empty input recovery completes before background queries");
            int failures = 0;
            network.Call<OnGetQuestStateRequestDTO, OnGetQuestStateResponseDTO>(new(), _ => { }, _ => failures++);
            Drain(); Drain();
            fixture.Check(fixture.Transport.Function == "OnGetQuestState", "automatic quest query uses the real network queue and isolated HTTP");
            fixture.Transport.Fail(NetworkManager.CreateError("Timeout", "isolated economy query timeout"));
            fixture.Check(failures == 1 && EconomyBatchProgression.HasUnknownResult && fixture.Transport.Function == "OnGetUserState",
                "uncertain background query schedules one full-state recovery and blocks spending");
            snapshot.EconomyRevision = 2;
            fixture.Transport.Succeed(snapshot);
            fixture.Check(!EconomyBatchProgression.HasUnknownResult && LocalProgression.State.ServerEconomyRevision == 2 && EconomyBatchProgression.CanAcceptGrowth,
                "durable full-state response releases the unknown gate and growth without relogin");
            fixture.Check(fixture.Transport.Unexpected == 0 && fixture.Transport.GetPendingMessages() == 0,
                "recovery sends no external HTTP and leaves no queued transport");
            return new { passed = fixture.Checks.Count, checks = fixture.Checks };
        }

        public static object RunDungeonEconomyCompletionRecovery()
        {
            using var fixture = CreateFixture();
            using var account = LocalProgression.BeginTestSession();
            const string battle = "1234567890abcdef1234567890abcdef";
            var client = Field(typeof(EconomyBatchProgression), "_clientId");
            object previousClient = client.GetValue(null);
            float previousTime = Time.timeScale;
            try
            {
                client.SetValue(null, battle);
                Field(typeof(LocalProgression), "_opening").SetValue(null, false);
                LocalProgression.OpenTestAccount("dungeon-economy-recovery-" + Guid.NewGuid().ToString("N"));
                var network = NetworkManager.Instance;
                var snapshot = JsonConvert.DeserializeObject<OnGetUserStateResponseDTO>(JsonConvert.SerializeObject(
                    Invoke(typeof(ServerApiContractAcceptance), null, "LegacyLogin")));
                snapshot.ProtocolVersion = IdleKingdomFunction.Common.ApiVersions.ProtocolVersion;
                snapshot.RubyGrowthState = new() { NextRequestSequence = 1, LastResetDay = "" };
                snapshot.EconomyBatchState = new()
                {
                    Version = 1, OwnerClientId = battle, Generation = 1, NextSequence = 1,
                    RewardEpoch = 1, RewardEpochs = new() { new() { Id = 1 } }
                };
                snapshot.DungeonState.GoldTickets = 1; snapshot.DungeonState.ActiveBattleId = battle;
                snapshot.DungeonState.ActiveDungeonId = (long)fixture.Dungeon;
                snapshot.DungeonState.TicketResetUtc = LocalProgression.UtcNow + 86400;
                snapshot.EconomyDungeonBattle = new()
                { BattleId = battle, DungeonId = (long)fixture.Dungeon, AcceptedKills = new(), RunGold = 100 };
                snapshot.Currency.Gold = 4475151;
                snapshot.ServerTimeUtc = LocalProgression.UtcNow;
                void Drain() { EconomyBatchProgression.FlushLocalWrites(); EconomyBatchProgression.Pump(); }
                void Prepare()
                {
                    Invoke(typeof(EconomyBatchProgression), null, "ResetAccount");
                    fixture.Prepare(fixture.Dungeon);
                    snapshot.EconomyRevision = 1;
                    fixture.Check(UserManager.Instance.ApplyServerPlayerData(snapshot), "isolated active dungeon and its spent ticket are durably installed");
                    bool recovered = false;
                    fixture.Check(EconomyBatchProgression.RecoverPending(ok => recovered = ok, true), "empty dungeon journal recovery begins");
                    Drain(); Drain();
                    fixture.Check(recovered, "dungeon journal recovery completes before the failed external query");
                    fixture.Set("_currentState", eStageRunState.Resolving);
                    typeof(StageSession).GetProperty(nameof(StageSession.HasPendingResult)).SetValue(fixture.Session, true);
                    Time.timeScale = 0;
                    EconomyBatchProgression.MarkUnknown("isolated provider failure followed by unavailable full-state read");
                    fixture.Call("CompleteServerDungeon", fixture.Session);
                }
                Prepare();
                string before = JsonConvert.SerializeObject(LocalProgression.State);
                fixture.Refresh();
                fixture.Check(fixture.Loading == null && !fixture.Overlay.gameObject.activeSelf &&
                    (bool)fixture.Get("_dungeonRequestPending") && fixture.Get("_retryDungeon") != null &&
                    (eStageRunState)fixture.Get("_currentState") == eStageRunState.Resolving && Time.timeScale == 0,
                    "unknown dungeon settlement releases loading but retains a recoverable completion for the same paused run");
                for (int i = 0; i < 3; i++)
                {
                    fixture.Set("_retryDungeonAt", Time.realtimeSinceStartup); fixture.Call("RetryDungeonRequest");
                    fixture.Check(fixture.Transport.Function == null && fixture.Loading == null &&
                        fixture.Get("_retryDungeon") != null && JsonConvert.SerializeObject(LocalProgression.State) == before,
                        "unknown retry " + i + " neither writes nor returns tickets or rewards");
                }
                bool refreshed = false;
                UserManager.Instance.RefreshFromServer(ok => refreshed = ok);
                fixture.Check(fixture.Transport.Function == "OnGetUserState", "manual recovery uses the existing full-state query");
                snapshot.EconomyRevision = 2; fixture.Transport.Succeed(snapshot);
                fixture.Check(refreshed && !EconomyBatchProgression.HasUnknownResult &&
                    LocalProgression.State.ActiveDungeon == battle && LocalProgression.State.GoldTickets == 1 &&
                    LocalProgression.Balance(eCurrency.Gold) == 4475151,
                    "confirmed refresh preserves the active run, earned gold, and already spent ticket");
                fixture.Set("_retryDungeonAt", Time.realtimeSinceStartup); fixture.Call("RetryDungeonRequest"); Drain(); Drain();
                var request = JsonConvert.DeserializeObject<OnCompleteDungeonRequestDTO>(JsonConvert.SerializeObject(fixture.Transport.Parameters));
                fixture.Check(fixture.Transport.Function == "OnCompleteDungeon" && request.BattleId == battle &&
                    request.DungeonId == (long)fixture.Dungeon && fixture.Get("_retryDungeon") == null,
                    "confirmed recovery resumes completion once with the captured original battle identity");
                fixture.Call("RetryDungeonRequest"); Drain();
                fixture.Check(fixture.Transport.Function == "OnCompleteDungeon" && fixture.Transport.Unexpected == 0,
                    "later retry ticks cannot duplicate an in-flight completion");
                string confirmed = JsonConvert.SerializeObject(LocalProgression.State);
                fixture.Transport.Fail(NetworkManager.CreateError("Business", "isolated definite rejection", details: new ErrorResponseDTO
                { ErrorCode = (Protocol.Message.Data.eErrorCode)1025, Message = "isolated definite rejection" }));
                fixture.Check(fixture.Loading == null && !(bool)fixture.Get("_dungeonRequestPending") && fixture.Get("_retryDungeon") == null &&
                    JsonConvert.SerializeObject(LocalProgression.State) == confirmed,
                    "confirmed terminal failure still ends the request without inventing a clear or refund");
                Prepare();
                EconomyBatchProgression.ConfirmFullRefresh();
                fixture.Set("_retryDungeonAt", Time.realtimeSinceStartup); fixture.Call("RetryDungeonRequest"); Drain(); Drain();
                fixture.Check(fixture.Transport.Function == "OnCompleteDungeon", "post-send replay fixture starts the captured completion");
                fixture.Transport.Fail(NetworkManager.CreateError("Timeout", "isolated completion response loss"));
                fixture.Check(fixture.Transport.Function == "OnGetUserState", "completion response loss uses the existing automatic state read");
                snapshot.EconomyRevision = 2;
                snapshot.DungeonState.ActiveBattleId = null; snapshot.DungeonState.ActiveDungeonId = null; snapshot.EconomyDungeonBattle = null;
                snapshot.DungeonState.GoldDungeonClear = 1;
                fixture.Transport.Succeed(snapshot);
                fixture.Check(!EconomyBatchProgression.HasUnknownResult && LocalProgression.State.ActiveDungeon == null,
                    "full-state confirmation can observe an already completed server run without fabricating its result");
                fixture.Set("_retryDungeonAt", Time.realtimeSinceStartup); fixture.Call("RetryDungeonRequest"); Drain(); Drain();
                request = JsonConvert.DeserializeObject<OnCompleteDungeonRequestDTO>(JsonConvert.SerializeObject(fixture.Transport.Parameters));
                fixture.Check(fixture.Transport.Function == "OnCompleteDungeon" && request.BattleId == battle && request.DungeonId == (long)fixture.Dungeon,
                    "an already completed server run retains its original idempotent receipt request after response loss");
                int resultCount = 0;
                void Cleared(StageDefinition _) => resultCount++;
                fixture.Manager.OnStageCleared += Cleared;
                try
                {
                    fixture.Transport.Succeed(new OnCompleteDungeonResponseDTO
                    {
                        ProtocolVersion = IdleKingdomFunction.Common.ApiVersions.ProtocolVersion, EconomyRevision = 3,
                        DungeonState = snapshot.DungeonState, Currency = snapshot.Currency, Equipment = snapshot.Equipment,
                        MageTowerState = snapshot.MageTowerState, GrantedRewards = new(),
                        TotalRunRewards = new() { new() { Kind = RewardKind.Currency, CurrencyType = Protocol.Message.Data.eCurrency.Gold, Amount = 100 } }
                    });
                    fixture.Call("RetryDungeonRequest"); Drain();
                    fixture.Check(resultCount == 1 && (eStageRunState)fixture.Get("_currentState") == eStageRunState.ResultPending &&
                        fixture.Loading == null && fixture.Transport.Function == null && LocalProgression.State.LastDungeonGold == 100 &&
                        LocalProgression.Balance(eCurrency.Gold) == 4475151 && LocalProgression.State.GoldTickets == 1,
                        "replayed completion presents one authoritative result without a second gold grant, ticket refund, or request");
                }
                finally { fixture.Manager.OnStageCleared -= Cleared; }
                snapshot.DungeonState.ActiveBattleId = battle; snapshot.DungeonState.ActiveDungeonId = (long)fixture.Dungeon;
                snapshot.EconomyDungeonBattle = new()
                { BattleId = battle, DungeonId = (long)fixture.Dungeon, AcceptedKills = new(), RunGold = 100 };
                foreach (string stale in new[] { "account", "network", "network-generation", "session", "token", "battle" })
                {
                    Prepare();
                    long generation = LocalProgression.AccountGeneration, networkGeneration = network.AccountGeneration;
                    var oldSession = fixture.Session; var oldToken = fixture.Get("_token");
                    GameObject replacement = null; CancellationTokenSource replacementToken = null;
                    try
                    {
                        if (stale == "account") typeof(LocalProgression).GetProperty(nameof(LocalProgression.AccountGeneration)).SetValue(null, generation + 1);
                        if (stale == "network") { replacement = new GameObject("DungeonRecoveryReplacement"); replacement.SetActive(false); NetworkManager.Instance = replacement.AddComponent<NetworkManager>(); }
                        if (stale == "network-generation") typeof(NetworkManager).GetProperty(nameof(NetworkManager.AccountGeneration)).SetValue(network, networkGeneration + 1);
                        if (stale == "session") fixture.Set("_currentSession", new StageSession(Definitions.Definition(fixture.Dungeon), new MainStageRule(),
                            (MonsterSpawner)Field(typeof(StageSession), "_monsterSpawner").GetValue(oldSession)));
                        if (stale == "token") { replacementToken = new CancellationTokenSource(); fixture.Set("_token", replacementToken); }
                        if (stale == "battle") LocalProgression.State.ActiveDungeon = "fedcba0987654321fedcba0987654321";
                        string current = JsonConvert.SerializeObject(LocalProgression.State);
                        Field(typeof(EconomyBatchProgression), "_externalUnknown").SetValue(null, false);
                        fixture.Set("_retryDungeonAt", Time.realtimeSinceStartup); fixture.Call("RetryDungeonRequest"); Drain();
                        fixture.Check(fixture.Transport.Function == null && JsonConvert.SerializeObject(LocalProgression.State) == current,
                            "captured completion cannot resume after its " + stale + " owner changes");
                    }
                    finally
                    {
                        typeof(LocalProgression).GetProperty(nameof(LocalProgression.AccountGeneration)).SetValue(null, generation);
                        NetworkManager.Instance = network;
                        typeof(NetworkManager).GetProperty(nameof(NetworkManager.AccountGeneration)).SetValue(network, networkGeneration);
                        fixture.Set("_currentSession", oldSession); fixture.Set("_token", oldToken);
                        replacementToken?.Dispose(); if (replacement != null) UnityEngine.Object.DestroyImmediate(replacement);
                    }
                }
                fixture.Check(fixture.Transport.Unexpected == 0, "all dungeon scenarios use only isolated transport without external HTTP");
                return new { passed = fixture.Checks.Count, checks = fixture.Checks };
            }
            finally { client.SetValue(null, previousClient); Time.timeScale = previousTime; }
        }

        private static Fixture CreateFixture()
        {
            if (!Application.isPlaying || LocalProgression.IsReady || NetworkManager.Instance != null || UserManager.Instance != null ||
                StageManager.Instance != null || UIManager.Instance != null || MonsterSpawner.Instance != null || LoadManager.Instance != null ||
                CameraFade.Instance != null || UnityEngine.Object.FindFirstObjectByType<StageBackgroundController>() != null)
                throw new InvalidOperationException("Stage loading acceptance requires empty signed-out PlayMode.");
            var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<UIViewCatalog>("Assets/UGUI/UIViewCatalog.asset");
            if (catalog == null || catalog.overlayGameplayLoading == null)
                throw new InvalidOperationException("Stage loading acceptance requires the production circular overlay catalog reference.");
            return new Fixture(catalog);
        }

        private static IEnumerator Checkpoint(Fixture fixture, string name, float notBefore = 0f)
        {
            CurrentCase = name;
            float until = Mathf.Max(Time.realtimeSinceStartup, notBefore) + GameplayLoading.DelaySeconds + .05f;
            while (Time.realtimeSinceStartup < until) { fixture.Refresh(); yield return null; }
            fixture.Refresh();
            fixture.Check(GameplayLoading.IsVisible && fixture.Overlay.gameObject.activeSelf, name + " displays the real circular overlay after the delay");
            _continue = false;
            WaitingForCapture = true;
            while (PauseAtCheckpoints && !_continue) { fixture.Refresh(); yield return null; }
            WaitingForCapture = false;
        }

        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        private static FieldInfo Field(Type type, string name) => type.GetField(name, Flags) ?? throw new MissingFieldException(type.Name, name);
        private static object Invoke(Type type, object target, string name, params object[] args) => type.GetMethod(name, Flags).Invoke(target, args);

        private sealed class Fixture : IDisposable
        {
            public readonly List<string> Checks = new();
            public readonly long AccountGeneration = LocalProgression.AccountGeneration;
            public readonly eStage Main = StageParser.MakeStage(eStageType.Main, 1, 2);
            public readonly eStage Previous = StageParser.MakeStage(eStageType.Main, 1, 1);
            public readonly eStage Next = StageParser.MakeStage(eStageType.Main, 1, 3);
            public readonly eStage Dungeon = StageParser.MakeStage(eStageType.GoldDungeon, 1);
            public readonly StageManager Manager;
            public readonly IsolatedHttp Transport = new();
            public readonly GameplayLoadingOverlayView Overlay;
            public StageSession Session;
            public Dictionary<long, Dictionary<eMonsterType, int>> Buffer => (Dictionary<long, Dictionary<eMonsterType, int>>)Get("_huntBuffer");
            public GameplayLoading.Scope Loading => (GameplayLoading.Scope)Get("_stageLoading");
            public object Head => ((Array)Get("_huntOutbox").GetType().GetMethod("ToArray").Invoke(Get("_huntOutbox"), null)).GetValue(0);
            private readonly object _state = Field(typeof(LocalProgression), "_state").GetValue(null);
            private readonly object _opening = Field(typeof(LocalProgression), "_opening").GetValue(null);
            private readonly ITransportPlugin _transport = PluginManager.GetPlugin<ITransportPlugin>(PluginContract.PlayFab_Transport);
            private readonly float _timeScale = Time.timeScale;
            private readonly GameObject _owner, _canvas;
            private readonly NetworkManager _network;
            private readonly MonsterSpawner _spawner;
            private readonly UIManager _ui;
            private LoadManager _loader;
            private CancellationTokenSource _resourceToken;
            private UniTaskCompletionSource _resourceSource;
            private string _before;

            public Fixture(UIViewCatalog catalog)
            {
                PluginManager.SetPlugin(Transport, PluginContract.PlayFab_Transport);
                _owner = new GameObject("GameplayStageLoadingAcceptance"); _owner.SetActive(false);
                _network = _owner.AddComponent<NetworkManager>(); NetworkManager.Instance = _network;
                Invoke(typeof(NetworkManager), _network, "Init");
                Field(typeof(NetworkManager), "_sessionGUID").SetValue(_network, "stage-loading-fixture");
                Field(typeof(NetworkManager), "_context").SetValue(_network, new PlayFabAuthenticationContext { EntityToken = "stage-loading-fixture" });
                typeof(NetworkManager).GetProperty("LoginState").SetValue(_network, GameLoginState.Ready);
                UserManager.Instance = _owner.AddComponent<UserManager>();
                _spawner = _owner.AddComponent<MonsterSpawner>();
                Manager = _owner.AddComponent<StageManager>(); StageManager.Instance = Manager;
                Set("_provider", new Definitions());
                Set("_huntBuffer", new Dictionary<long, Dictionary<eMonsterType, int>>());
                Field(typeof(LocalProgression), "_opening").SetValue(null, true);
                _ui = _owner.AddComponent<UIManager>(); typeof(UIManager).GetProperty("Instance").SetValue(null, _ui);
                _canvas = new GameObject("GameplayStageLoadingAcceptance_Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
                _canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = _canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080, 1920); scaler.matchWidthOrHeight = .5f;
                Field(typeof(UIManager), "catalog").SetValue(_ui, catalog);
                Field(typeof(UIManager), "layerOverlays").SetValue(_ui, (RectTransform)_canvas.transform);
                Invoke(typeof(UIManager), _ui, "BuildOverlays");
                Overlay = _canvas.GetComponentInChildren<GameplayLoadingOverlayView>(true);
                Check(Overlay != null, "production overlay prefab is instantiated without game initialization");
            }

            public object Get(string name) => Field(typeof(StageManager), name).GetValue(Manager);
            public void Set(string name, object value) => Field(typeof(StageManager), name).SetValue(Manager, value);
            public object Call(string name, params object[] args) => Invoke(typeof(StageManager), Manager, name, args);
            public void Refresh() => Invoke(typeof(UIManager), _ui, "RefreshLoading");
            public void RememberState() => _before = JsonConvert.SerializeObject(LocalProgression.State);
            public void Check(bool value, string message)
            { if (!value) throw new InvalidOperationException("Stage loading acceptance: " + message); Checks.Add(message); }
            public void CheckEnded(string message)
            {
                Refresh();
                Check(Loading == null && !GameplayLoading.IsVisible && !Overlay.gameObject.activeSelf &&
                    JsonConvert.SerializeObject(LocalProgression.State) == _before, message);
            }

            public void Prepare(eStage stage)
            {
                Call("EndStageLoading"); GameplayLoading.Reset(); Transport.Discard();
                CancelStageToken();
                Get("_huntOutbox").GetType().GetMethod("Clear").Invoke(Get("_huntOutbox"), null); Buffer.Clear();
                foreach (string name in new[] { "_huntInFlight", "_huntsHeld", "_huntSyncFailed", "_mainStageRequestPending", "_dungeonRequestPending", "_recoveringMainCompletion" }) Set(name, false);
                Set("_onHuntsSettled", null); Set("_retryDungeon", null); Set("_mainRunForSession", null);
                Set("_currentStage", stage); Set("_currentState", eStageRunState.Running);
                var state = new ProgressionState { MainStage = (long)Main, CycleFrontierStage = (long)Next,
                    HighestMainClear = (long)StageParser.MakeStage(eStageType.Main, 3), DungeonTicketResetUtc = LocalProgression.UtcNow + 86400 };
                if (StageParser.GetStageType(stage) != eStageType.Main) { state.ActiveDungeon = "stage-fixture-run"; state.ServerDungeonId = (long)stage; }
                Field(typeof(LocalProgression), "_state").SetValue(null, state);
                Session = new StageSession(Definitions.Definition(stage), new MainStageRule(), _spawner);
                Set("_currentSession", Session);
                _before = JsonConvert.SerializeObject(state); Time.timeScale = 1f; Refresh();
            }

            public UniTaskCompletionSource HoldRequiredResources(eStage target)
            {
                _loader = _owner.AddComponent<LoadManager>(); LoadManager.Instance = _loader;
                _resourceToken = new CancellationTokenSource();
                Field(typeof(LoadManager), "_loadStageToken").SetValue(_loader, _resourceToken);
                var cacheType = typeof(LoadManager).GetNestedType("StageResourceCache", BindingFlags.NonPublic);
                object cache = Activator.CreateInstance(cacheType, true);
                _resourceSource = new UniTaskCompletionSource();
                Field(cacheType, "MonsterLoadTask").SetValue(cache, _resourceSource.Task);
                var caches = (IDictionary)Field(typeof(LoadManager), "_stageResourceCaches").GetValue(_loader);
                caches.Add(StageParser.GetResourceGroupId(target), cache);
                return _resourceSource;
            }

            private void CancelStageToken()
            {
                var token = (CancellationTokenSource)Get("_token");
                token?.Cancel(); token?.Dispose(); Set("_token", null);
            }

            public void CheckRetryAndReject(string function)
            {
                var loading = Loading;
                Transport.Fail(NetworkManager.CreateError("Timeout", "stage acceptance"));
                Check(ReferenceEquals(loading, Loading) && Get("_retryDungeon") != null &&
                    (float)Get("_retryDungeonAt") > Time.realtimeSinceStartup + 4.8f,
                    function + " retains loading and its existing five-second retry delay");
                Set("_retryDungeonAt", Time.realtimeSinceStartup);
                Call("RetryDungeonRequest");
                Check(Transport.Function == function && ReferenceEquals(loading, Loading), function + " retries within the same loading scope");
                Transport.Fail(NetworkManager.CreateError("Business", "stage acceptance definite rejection", details: new ErrorResponseDTO
                { ErrorCode = (Protocol.Message.Data.eErrorCode)1025, Message = "stage acceptance" }));
            }

            public void Dispose()
            {
                Call("EndStageLoading"); Transport.Discard(); GameplayLoading.Reset();
                CancelStageToken();
                _resourceSource?.TrySetCanceled();
                _resourceToken?.Cancel(); _resourceToken?.Dispose();
                if (_loader != null) Field(typeof(LoadManager), "_loadStageToken").SetValue(_loader, null);
                UnityEngine.Object.DestroyImmediate(_canvas); UnityEngine.Object.DestroyImmediate(_owner);
                NetworkManager.Instance = null; UserManager.Instance = null; StageManager.Instance = null; LoadManager.Instance = null;
                typeof(UIManager).GetProperty("Instance").SetValue(null, null);
                Field(typeof(LocalProgression), "_state").SetValue(null, _state);
                Field(typeof(LocalProgression), "_opening").SetValue(null, _opening);
                PluginManager.SetPlugin(_transport, PluginContract.PlayFab_Transport);
                Time.timeScale = _timeScale;
            }
        }

        private sealed class Definitions : IStageDefinitionProvider
        {
            public bool TryGet(eStage id, out StageDefinition definition) { definition = Definition(id); return true; }
            public static StageDefinition Definition(eStage id) => new(id, eStageFlowType.MainProgression, eEnvironmentId.MainField1,
                1, Array.Empty<StageMonsterEntry>(), null, nextDifficultyId: id == eStage.GoldDungeon1_1 ? eStage.GoldDungeon2_1 : null);
        }

        private sealed class IsolatedHttp : ITransportPlugin
        {
            public bool IsInitialized => true;
            public int Unexpected;
            private PlayFab.Internal.CallRequestContainer _pending;
            public string Function => (_pending?.ApiRequest as ExecuteFunctionRequest)?.FunctionName;
            public object Parameters => (_pending?.ApiRequest as ExecuteFunctionRequest)?.FunctionParameter;
            public void Initialize() { }
            public void Update() { }
            public void OnDestroy() { }
            public int GetPendingMessages() => _pending == null ? 0 : 1;
            public void MakeApiCall(object request)
            {
                if (request is not PlayFab.Internal.CallRequestContainer container || container.ApiRequest is not ExecuteFunctionRequest function ||
                    _pending != null || (function.FunctionName != "OnChangeMainStage" && function.FunctionName != "OnGetUserState" &&
                    function.FunctionName != "OnCompleteDungeon" && function.FunctionName != "OnExitDungeon" && function.FunctionName != "OnCompleteMainWave" &&
                    function.FunctionName != "OnGetQuestState"))
                { Unexpected++; throw new InvalidOperationException("Stage acceptance blocks unexpected HTTP."); }
                _pending = container;
            }
            public void Fail(PlayFabError error)
            {
                var pending = _pending ?? throw new InvalidOperationException("Expected isolated HTTP request was not sent.");
                _pending = null; pending.ErrorCallback(error);
            }
            public void Discard()
            {
                _pending = null;
                if (NetworkManager.Instance != null) Invoke(typeof(NetworkManager), NetworkManager.Instance, "ResetCallQueue");
            }
            public void Succeed(ApiResponseDTO response)
            {
                var pending = _pending ?? throw new InvalidOperationException("Expected isolated HTTP request was not sent.");
                _pending = null;
                pending.ApiResult = new ExecuteFunctionResult { FunctionResult = JsonConvert.SerializeObject(response) };
                pending.InvokeSuccessCallback();
            }
            public void SimpleGetCall(string url, Action<byte[]> success, Action<string> error) => Block();
            public void SimplePutCall(string url, byte[] payload, Action<byte[]> success, Action<string> error) => Block();
            public void SimplePostCall(string url, byte[] payload, Action<byte[]> success, Action<string> error) => Block();
            private void Block() { Unexpected++; throw new InvalidOperationException("Stage acceptance blocks external HTTP."); }
        }
    }
}
#endif
