#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using KingdomIdle.Balance;
using Newtonsoft.Json;
using Protocol.Message.Dto;
using Scripts.Core;
using Scripts.Core.Manager;
using UnityEngine;

namespace KingdomIdle.Rewards
{
    /// <summary>Isolated synthetic receipt presentation only. Never signs in or sends a backend request.</summary>
    public static class EconomyBatchLootAcceptance
    {
        private const BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;
        private static readonly Type Core = typeof(EconomyBatchProgression);
        private static FieldInfo Field(string name) => Core.GetField(name, PrivateStatic);
        private static void Set(string name, object value) => Field(name).SetValue(null, value);
        private static object Call(string name, params object[] args) => Core.GetMethod(name, PrivateStatic).Invoke(null, args);

        public static Dictionary<string, object> Run()
        {
            if (!Application.isPlaying || LocalProgression.IsReady || NetworkManager.Instance != null ||
                UserManager.Instance != null || StageManager.Instance != null || QuestManager.Instance != null)
                throw new InvalidOperationException("Run EconomyBatchLootAcceptance in empty-scene PlayMode without an account or production managers.");
            if (Field("_journal").GetValue(null) != null || ((ICollection)Field("_fences").GetValue(null)).Count != 0 ||
                ((ICollection)Field("_discardedInputs").GetValue(null)).Count != 0 || LootNotifications.Count != 0)
                throw new InvalidOperationException("Economy/loot fixture requires no existing pending work.");

            var savedFields = Core.GetFields(PrivateStatic).Where(field => !field.IsInitOnly && !field.IsLiteral)
                .ToDictionary(field => field, field => field.GetValue(null));
            var clock = typeof(LootNotifications).GetField("_clock", PrivateStatic);
            var filter = typeof(LootNotifications).GetField("_filter", PrivateStatic);
            var sequence = typeof(LootNotifications).GetField("_sequence", PrivateStatic);
            object oldClock = clock.GetValue(null), oldFilter = filter.GetValue(null), oldSequence = sequence.GetValue(null);
            var paths = new HashSet<string>();
            var results = new List<Result>();
            var session = LocalProgression.BeginTestSession();
            var owner = new GameObject("IsolatedEconomyBatchLoot") { hideFlags = HideFlags.HideAndDontSave };
            owner.SetActive(false);
            try
            {
                var network = owner.AddComponent<NetworkManager>(); NetworkManager.Instance = network;
                typeof(NetworkManager).GetProperty(nameof(NetworkManager.LoginState)).SetValue(network, GameLoginState.Ready);
                clock.SetValue(null, (Func<double>)(() => 0d));
                LootNotifications.SetFilter(null); LootNotifications.Clear();
                void Case(string name, Action<Fixture, Result> action, long gross = 100, bool spend = false, bool hunt = true, bool recovering = false)
                {
                    var result = new Result(name); results.Add(result);
                    try { using var fixture = new Fixture(network, paths, result, gross, spend, hunt, recovering); action(fixture, result); }
                    catch (Exception error)
                    {
                        if (error is TargetInvocationException invocation && invocation.InnerException != null) error = invocation.InnerException;
                        result.Check(false, "Fixture exception type only: " + error.GetType().Name);
                    }
                }

                Case("PureHuntPostDurableAndDuplicate", (f, r) =>
                {
                    f.Apply(false); f.CheckSaved(r);
                    r.Check(f.Arrivals > 0 && f.AllArrivalsDurable, "Gold is published only after the matching receipt and wallet reached the durable snapshot");
                    f.ExpectGold(r, 100);
                    f.Rebind(); f.Response.Replayed = true; f.Apply(true);
                    r.Check(LootNotifications.Count == 0 && LocalProgression.Balance(eCurrency.Gold) == f.AfterGold,
                        "An already locally applied receipt/query cannot duplicate the notification or currency");
                    LootNotifications.Clear(); f.Rebind(); f.Apply(true);
                    r.Check(LootNotifications.Count == 0, "Durable already-applied identity prevents replay even after the in-memory display receipt cache is cleared");
                });
                Case("MixedHuntAndSpendPositiveNet", (f, r) =>
                {
                    f.Apply(false); f.CheckSaved(r); f.ExpectGold(r, 100);
                    r.Check(f.AfterGold - f.BeforeGold < 100 && f.CompletedGrowth == 1,
                        "Gross hunt notification survives same-batch gold spending without changing input completion");
                }, spend: true);
                Case("MixedHuntAndSpendNegativeNet", (f, r) =>
                {
                    f.Apply(false); f.CheckSaved(r); f.ExpectGold(r, 25);
                    r.Check(f.AfterGold < f.BeforeGold, "A negative wallet delta does not erase an authoritative hunt grant");
                }, gross: 25, spend: true);
                Case("GrowthOnlyHasNoLoot", (f, r) =>
                {
                    f.Apply(false); f.CheckSaved(r);
                    r.Check(LootNotifications.Count == 0 && f.CompletedGrowth == 1, "Growth alone changes level/spending without an acquisition notification");
                }, gross: 0, spend: true, hunt: false);
                Case("FailedDiskThenLiveQuery", (f, r) =>
                {
                    LocalProgression.TestFailedCommit(() => { f.Apply(false); return false; });
                    r.Check(LocalProgression.Balance(eCurrency.Gold) == f.BeforeGold &&
                        LocalProgression.State.Economy.LastAppliedRequestId != f.Request.RequestId && LootNotifications.Count == 0,
                        "A failed durable receipt apply publishes nothing and preserves the prior wallet");
                    r.Check(f.Journal.Data.InFlight?.RequestId == f.Request.RequestId, "A failed save preserves the exact sealed request for result confirmation");
                    f.Response.Replayed = true; f.Apply(true); f.CheckSaved(r); f.ExpectGold(r, 100);
                });
                Case("LiveReplayedQueryFirstLocalApply", (f, r) =>
                {
                    f.Response.Replayed = true; f.Apply(true); f.CheckSaved(r); f.ExpectGold(r, 100);
                });
                Case("StartupRecoveryIsNotNewLoot", (f, r) =>
                {
                    f.Response.Replayed = true; f.Apply(true); f.CheckSaved(r);
                    r.Check(LootNotifications.Count == 0, "Login/startup result recovery does not replay historical acquisition notifications");
                }, recovering: true);
                Case("NotFoundCanonicalStateHasNoReceiptLoot", (f, r) =>
                {
                    f.Response.Status = EconomyBatchStatus.NotFound; f.Response.Receipt = null;
                    f.Response.CurrentState.EconomyBatchState.PendingReceipt = null;
                    f.Response.CurrentState.EconomyBatchState.NextSequence = f.Request.Sequence;
                    f.Apply(true);
                    r.Check(LocalProgression.Balance(eCurrency.Gold) == f.AfterGold && LootNotifications.Count == 0,
                        "A NotFound state refresh is not a new grant even when its canonical wallet is higher");
                    r.Check(f.Journal.Data.InFlight?.RequestId == f.Request.RequestId, "NotFound retains the original request; the fixture write gate prevents any send");
                });
                Case("ArrivalFilterAndNoRetroactiveReplay", (f, r) =>
                {
                    f.Allows = false; f.Apply(false); f.CheckSaved(r);
                    r.Check(LootNotifications.Count == 0, "The existing arrival filter can hide confirmed gold without affecting settlement");
                    f.Allows = true; f.Rebind(); f.Apply(true);
                    r.Check(LootNotifications.Count == 0, "Enabling a filter does not replay an already locally committed receipt");
                });
                Case("DisplayObserverFailureIsIsolated", (f, r) =>
                {
                    f.ThrowFilter = true; f.Apply(false); f.CheckSaved(r);
                    r.Check(!(bool)Field("_batchUnknown").GetValue(null) && !(bool)Field("_queryRequired").GetValue(null) && f.CompletedGrowth == 1,
                        "A display filter failure cannot turn saved settlement into unknown or block growth completion");
                }, spend: true);
                Case("OldAccountCallbackIsRejected", (f, r) =>
                {
                    long generation = LocalProgression.AccountGeneration, networkGeneration = network.AccountGeneration;
                    LocalProgression.OpenTestAccount("economy-loot-other-" + Guid.NewGuid().ToString("N"));
                    paths.Add(LocalProgression.SnapshotPath); paths.Add(LocalProgression.EconomyJournalPath);
                    bool owns = (bool)Call("Owns", generation, networkGeneration, f.Request.RequestId);
                    r.Check(!owns && LootNotifications.Count == 0 && LocalProgression.Balance(eCurrency.Gold) == 0,
                        "The actual callback ownership gate rejects the previous account receipt before apply");
                });
            }
            finally
            {
                Call("ResetAccount"); NetworkManager.Instance = null;
                UnityEngine.Object.DestroyImmediate(owner);
                session.Dispose();
                foreach (var pair in savedFields) pair.Key.SetValue(null, pair.Value);
                LootNotifications.Clear(); clock.SetValue(null, oldClock); filter.SetValue(null, oldFilter); sequence.SetValue(null, oldSequence);
                foreach (string path in paths)
                {
                    string root = Path.GetFullPath(Application.persistentDataPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                    if (string.IsNullOrEmpty(path) || !Path.GetFullPath(path).StartsWith(root, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Fixture cleanup path escaped the isolated persistent-data root");
                    foreach (string file in new[] { path, path + ".tmp", path + ".bak" }) if (File.Exists(file)) File.Delete(file);
                }
            }
            var failures = results.SelectMany(result => result.Failures.Select(label => result.Name + ": " + label)).ToArray();
            return new Dictionary<string, object>
            {
                ["passed"] = failures.Length == 0 ? results.Sum(result => result.Checks.Count) : 0,
                ["success"] = failures.Length == 0, ["failures"] = failures,
                ["scope"] = "ISOLATED LOCAL SYNTHETIC RECEIPTS; no real account/server/backend calls; not live payout evidence",
                ["cases"] = results.Select(result => new Dictionary<string, object>
                { ["name"] = result.Name, ["checks"] = result.Checks.ToArray(), ["failures"] = result.Failures.ToArray() }).ToArray()
            };
        }

        private sealed class Fixture : IDisposable
        {
            internal readonly EconomyInputJournal Journal;
            internal readonly OnEconomyBatchRequestDTO Request;
            internal readonly OnEconomyBatchResponseDTO Response;
            internal readonly long BeforeGold = 1000, AfterGold;
            internal bool Allows = true, ThrowFilter, AllArrivalsDurable = true;
            internal int Arrivals, CompletedGrowth;
            private readonly string _snapshot;
            private readonly List<EconomyInput> _inputs;
            private readonly Action<EconomyInputCompletion> _completed;

            internal Fixture(NetworkManager network, HashSet<string> paths, Result result, long gross, bool spend, bool hunt, bool recovering)
            {
                LocalProgression.OpenTestAccount("economy-loot-" + Guid.NewGuid().ToString("N"));
                _snapshot = LocalProgression.SnapshotPath; paths.Add(_snapshot); paths.Add(LocalProgression.EconomyJournalPath);
                Response = EconomyBatchContractAcceptance.Fixture();
                Response.RequestId = Guid.NewGuid().ToString("N"); Response.EconomyRevision = 2;
                Response.CurrentState.EnchantATKCount = spend ? 1 : 0;
                long spent = spend ? BalanceMath.GoldTotal(0, 1) : 0;
                AfterGold = BeforeGold + gross - spent;
                Response.CurrentState.Currency.Gold = AfterGold;
                Response.CurrentState.KillScore = hunt ? 1 : 0;
                Response.CurrentState.MainBattle.AcceptedKills = new();
                var commands = new List<EconomyCommandDTO>(); var applied = new List<EconomyCommandResultDTO>();
                if (hunt)
                {
                    var kill = new HuntResult { MonsterType = (Protocol.Message.Data.eMonsterType)(ulong)eMonsterType.MON_GOBLIN, Count = 1 };
                    commands.Add(new() { Kind = EconomyCommandKind.Hunt, Stage = Response.CurrentState.MainBattle.Stage,
                        MainRunId = Response.CurrentState.MainBattle.RunId, RewardEpoch = 1, TotalKills = new() { kill } });
                    applied.Add(new() { Index = 0, Kind = EconomyCommandKind.Hunt, Status = EconomyCommandStatus.Applied, AppliedCount = 1, GrantedGold = gross });
                    Response.CurrentState.MainBattle.AcceptedKills.Add(kill);
                }
                if (spend)
                {
                    commands.Add(new() { Kind = EconomyCommandKind.GoldAttack, Mode = 1, ExpectedLevel = 0, CountLimit = 1, SpendLimit = spent });
                    applied.Add(new() { Index = applied.Count, Kind = EconomyCommandKind.GoldAttack, Status = EconomyCommandStatus.Applied, AppliedCount = 1, SpentGold = spent });
                }
                Request = new() { RequestId = Response.RequestId, Generation = 1, Sequence = 1, ExpectedEconomyRevision = 1, AppliedRewardEpoch = 1, Commands = commands };
                Response.Receipt = new() { RequestId = Request.RequestId, Generation = 1, Sequence = 1,
                    PayloadHash = EconomyInputJournal.Hash(Request), CommittedRevision = 2, Results = applied };
                Response.CurrentState.EconomyBatchState.NextSequence = 2;
                Response.CurrentState.EconomyBatchState.PendingReceipt = EconomyCopies.Copy(Response.Receipt);
                EconomyBatchProtocol.ValidateRequest(Request); EconomyBatchProtocol.ValidateResponse(Response); EconomyBatchProtocol.MatchRequest(Response, Request);
                var initial = EconomyInputJournal.Clone(Response.CurrentState);
                initial.Currency.Gold = BeforeGold; initial.EnchantATKCount = 0; initial.KillScore = 0; initial.MainBattle.AcceptedKills.Clear();
                initial.EconomyBatchState.NextSequence = 1; initial.EconomyBatchState.PendingReceipt = null;
                result.Check(LocalProgression.Execute("isolated-economy-loot-initial", state => EconomyBatchProgression.ApplyServerState(state, initial, 1)),
                    "Isolated synthetic initial state is durably saved");
                Journal = new EconomyInputJournal(LocalProgression.EconomyJournalPath, LocalProgression.AccountKey);
                Journal.Data.Generation = 1; Journal.Data.NextInputSequence = commands.Count + 1;
                _inputs = commands.Select((command, index) => new EconomyInput { Sequence = index + 1, Command = EconomyCopies.Copy(command) }).ToList();
                Set("_journal", Journal); Set("_network", network); Set("_localGeneration", LocalProgression.AccountGeneration);
                Set("_networkGeneration", network.AccountGeneration); Set("_supported", true); Set("_recovered", !recovering);
                Set("_recovering", recovering); Set("_sentThisSession", !recovering); Set("_startupMaySplit", false);
                Set("_requestPending", false); Set("_queryRequired", false); Set("_batchUnknown", false); Set("_externalUnknown", false);
                Set("_blocked", false); Set("_externalWrite", true); Set("_nextHuntAt", float.MaxValue);
                Set("_clientId", Response.CurrentState.EconomyBatchState.OwnerClientId);
                result.Check(EconomyBatchProgression.Available && network.GetSessionID() == null &&
                    typeof(NetworkManager).GetField("_context", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(network) == null,
                    "Isolated batch capability is active on an inactive unauthenticated manager; outgoing writes stay gated");
                _completed = completion => { if (completion.Success) CompletedGrowth++; };
                EconomyBatchProgression.InputCompleted += _completed;
                Rebind();
                LootNotifications.SetFilter(item =>
                {
                    Arrivals++;
                    var durable = JsonConvert.DeserializeObject<ProgressionState>(File.ReadAllText(_snapshot));
                    AllArrivalsDurable &= durable.Economy.LastAppliedRequestId == Request.RequestId &&
                        durable.ServerEconomyRevision == 2 && durable.Wallet[eCurrency.Gold] == AfterGold;
                    if (ThrowFilter) throw new InvalidOperationException("isolated display observer");
                    return Allows;
                });
            }
            internal void Rebind()
            {
                Journal.Pump(true); Journal.Data.InFlight = EconomyInputJournal.Clone(Request);
                Journal.Data.InFlightInputs = EconomyInputJournal.Clone(_inputs); Journal.Data.Inputs.Clear();
                Journal.Changed(); Journal.Pump(true);
            }
            internal void Apply(bool query) => Call("ApplyResult", Response, query);
            internal void CheckSaved(Result result) => result.Check(LocalProgression.Balance(eCurrency.Gold) == AfterGold &&
                LocalProgression.State.Economy.LastAppliedRequestId == Request.RequestId && LocalProgression.State.ServerEconomyRevision == 2 && Journal.Data.InFlight == null,
                "Synthetic receipt reaches confirmed wallet/revision/input completion in the isolated local snapshot");
            internal void ExpectGold(Result result, long amount)
            {
                bool found = LootNotifications.TryTake(out var item);
                result.Check(found && item.Kind == LootNotificationKind.Currency && item.Currency == eCurrency.Gold && item.Amount == amount && LootNotifications.Count == 0,
                    "Exactly one gross hunt Gold notification is shown: " + amount);
            }
            public void Dispose()
            {
                EconomyBatchProgression.InputCompleted -= _completed;
                Call("ResetAccount"); LootNotifications.Clear(); LootNotifications.SetFilter(null);
            }
        }

        private sealed class Result
        {
            internal readonly string Name;
            internal readonly List<string> Checks = new List<string>(), Failures = new List<string>();
            internal Result(string name) { Name = name; }
            internal void Check(bool passed, string label) { (passed ? Checks : Failures).Add(label); }
        }
    }
}
#endif
