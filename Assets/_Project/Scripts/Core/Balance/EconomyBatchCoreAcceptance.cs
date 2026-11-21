#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Protocol.Message.Dto;
using Scripts.Core;
using Scripts.Core.Manager;
using UnityEngine;

namespace KingdomIdle.Balance
{
    public static class EconomyBatchCoreAcceptance
    {
        private const string Id = "12345678123456781234567812345678";
        private static readonly ulong Monster = (ulong)eMonsterType.MON_GOBLIN;

        public static object Run()
        {
            var checks = new List<string>();
            void Check(bool value, string name) { if (!value) throw new InvalidOperationException(name); checks.Add(name); }
            var request = new OnEconomyBatchRequestDTO
            {
                RequestId = Id, Generation = 1, Sequence = 1, ExpectedEconomyRevision = 1, AppliedRewardEpoch = 1,
                Commands = new() { new() { Kind = EconomyCommandKind.GoldAttack, Mode = 1, CountLimit = 1, SpendLimit = 50 } }
            };
            Check(EconomyInputJournal.Hash(request) == "47301BBC4516EA916E315779AF9C80E8DE82E72AA5BB1B0306340922CF06EFBB", "canonical hash agrees with server golden vector");
            request.SessionID = "replacement-session";
            Check(EconomyInputJournal.Hash(request) == "47301BBC4516EA916E315779AF9C80E8DE82E72AA5BB1B0306340922CF06EFBB", "session replacement cannot change the immutable economic body");
            request.QuestVersion = "quest-observer";
            Check(EconomyInputJournal.Hash(request) == "47301BBC4516EA916E315779AF9C80E8DE82E72AA5BB1B0306340922CF06EFBB", "quest version cannot change the durable economy payload hash");

            var state = Fixture(); state.Wallet[eCurrency.Gold] = 104;
            var initial = EconomyProjectionMath.Calculate(state, Array.Empty<EconomyCommandDTO>());
            var first = EconomyProjectionMath.Quote(initial, EconomyCommandKind.GoldAttack, 1);
            var second = EconomyProjectionMath.Quote(EconomyProjectionMath.Calculate(state, new[] { first }), EconomyCommandKind.GoldAttack, 1);
            var consecutive = EconomyProjectionMath.Calculate(state, new[] { first, second });
            Check(consecutive.Valid && consecutive.AttackLevel == 2 && consecutive.Gold == 0, "x1 repeats preserve level and spending order");
            Check(EconomyProjectionMath.Quote(initial, EconomyCommandKind.GoldAttack, 10) == null, "x10 is rejected whole when only two levels are affordable");
            var maximum = EconomyProjectionMath.Quote(initial, EconomyCommandKind.GoldAttack, -1);
            state.Wallet[eCurrency.Gold] = 100000;
            var capped = EconomyProjectionMath.Calculate(state, new[] { maximum });
            Check(capped.AttackLevel == 2 && capped.Gold == 100000 - 104, "MAX keeps the click-time count and spending ceiling after later income");
            Check(state.AttackLevel == 0 && state.Wallet[eCurrency.Gold] == 100000, "projection never mutates confirmed combat or currency");

            state = Fixture();
            var stage = new StageDefinition(eStage.Stage1_1, eStageFlowType.MainProgression, eEnvironmentId.MainField1, 1,
                new[]
                {
                    new StageMonsterEntry(eMonsterType.MON_GOBLIN, 4, 1, eMonsterSpawnPhase.Initial, "Melee", 0).WithCombat(new StageEnemyData(10, 1, 10, 1, 1, 1)),
                    new StageMonsterEntry(eMonsterType.MON_GOBLIN, 2, 1, eMonsterSpawnPhase.Initial, "Melee", 0).WithCombat(new StageEnemyData(10, 1, 20, 1, 1, 1))
                }, "test");
            var hunts = new[] { Hunt(3), Hunt(5) };
            var hunted = EconomyProjectionMath.Calculate(state, hunts, _ => stage);
            Check(hunted.Valid && hunted.Gold == 60, "cumulative 3 then 5 credits five kills using catalogue row order");
            state.Economy.Server.RewardEpoch = 2;
            state.Economy.Server.RewardEpochs.Add(new() { Id = 2, GoldLevel = 50 });
            hunts[1].RewardEpoch = 2;
            Check(EconomyProjectionMath.Calculate(state, hunts, _ => stage).Gold == 90, "new reward multiplier only affects kills in its acknowledged epoch");
            state.Economy.Server.GoldRemainderMicros = 980000;
            state.Economy.Server.RewardEpochs[0].GoldLevel = 1;
            Check(EconomyProjectionMath.Calculate(state, new[] { Hunt(1), Hunt(2) }, _ => stage).Gold == 21, "fractional income carries across cumulative commands");

            state = Fixture(); state.Economy.Hunts.Add(Hunt(3));
            var changed = NextMetadata(state);
            var draft = state.DeepClone();
            Check(EconomyBatchProgression.ApplyBatchMetadata(draft, changed, 2), "ACK metadata applies to isolated transaction draft");
            Check(state.Economy.Server.RewardEpoch == 1 && state.Economy.Cutoffs.Count == 0 && draft.Economy.Cutoffs.Single().TotalKills.Single().Count == 3,
                "ACK cutoff captures all old-epoch kills without mutating live state");
            changed.RewardEpochs[0].GoldLevel = 50;
            Check(draft.Economy.Server.RewardEpochs[0].GoldLevel == 0, "saved epoch metadata does not alias transport objects");
            Check(!EconomyBatchProgression.ApplyBatchMetadata(draft, NextMetadata(state), 0), "old authoritative snapshots cannot roll back economic revision");
            var takeover = EconomyCopies.Copy(state.Economy.Server); takeover.Generation = 2;
            state.PendingRubyRequestId = Id;
            Check(EconomyBatchProgression.ApplyBatchMetadata(state, takeover, 2) && state.Economy.Hunts.Count == 0 &&
                state.Modules.ContainsKey("retired-economy-hunts") && state.PendingRubyRequestId == Id,
                "owner generation change archives old hunts without discarding the legacy Ruby ID");

            string path = Path.Combine(Path.GetTempPath(), "economy-core-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                var journal = new EconomyInputJournal(path, "test");
                journal.Data.Generation = 1; journal.Data.NextInputSequence = 2;
                journal.Data.Inputs.Add(new() { Sequence = 1, Command = first }); journal.Changed(); journal.Pump();
                Check(journal.Committed.Inputs.Count == 0, "input is not accepted before the asynchronous write completes");
                journal.Pump(true);
                Check(journal.Committed.Inputs.Count == 1 && !journal.IsDirty, "durable small journal publishes the accepted input");
                journal.Data.InFlight = request; journal.Data.InFlightInputs.Add(journal.Data.Inputs.Single()); journal.Data.Inputs.Clear();
                journal.Changed(); journal.Pump(true);
                var reopened = new EconomyInputJournal(path, "test");
                Check(EconomyInputJournal.Hash(reopened.Data.InFlight) == EconomyInputJournal.Hash(request), "restart preserves the exact sealed request");
                reopened.Data.AbandonedRequest = reopened.Data.InFlight; reopened.Data.AbandonedAtRevision = 2;
                reopened.Data.Inputs.AddRange(reopened.Data.InFlightInputs); reopened.Data.Inputs.ForEach(x => x.Held = true);
                reopened.Data.InFlight = null; reopened.Data.InFlightInputs.Clear(); reopened.Changed(); reopened.Pump(true);
                var split = new EconomyInputJournal(path, "test");
                Check(split.Data.InFlight == null && split.Data.Inputs.Single().Held && split.Data.AbandonedRequest.RequestId == Id,
                    "startup disposition and held spending survive together before a new nonce can be sent");
                var failed = new EconomyInputJournal(Path.Combine(path, "unwritable.json"), "test");
                failed.Data.NextInputSequence = 2; failed.Data.Inputs.Add(new() { Sequence = 1, Command = first }); failed.Changed(); failed.Pump(true);
                Check(failed.Error != null && failed.Committed.Inputs.Count == 0, "failed disk write cannot accept spending");
                failed.RestoreCommitted(); Check(failed.Data.Inputs.Count == 0, "failed draft can be removed without deleting committed input");
            }
            finally { foreach (string file in new[] { path, path + ".tmp", path + ".bak" }) if (File.Exists(file)) File.Delete(file); }

            using (LocalProgression.BeginTestSession())
            {
                LocalProgression.OpenTestAccount("economy-core-" + Guid.NewGuid().ToString("N"));
                Check(LocalProgression.Execute("economy-test-initial", s =>
                { var fixture = Fixture(); s.ServerEconomyRevision = 1; s.Economy = fixture.Economy; s.MainBattle = fixture.MainBattle; s.Economy.Hunts.Add(Hunt(3)); return true; }), "initial durable epoch fixture");
                bool ApplyAck(ProgressionState s)
                {
                    if (!EconomyBatchProgression.ApplyBatchMetadata(s, NextMetadata(s), 2)) return false;
                    s.RubyGoldLevel = s.BestRubyGoldLevel = 1; s.RubyGoldSpent = 20; return true;
                }
                Check(!LocalProgression.TestFailedWrite(ApplyAck) && LocalProgression.State.Economy.Server.RewardEpoch == 1 && LocalProgression.State.RubyGoldLevel == 0,
                    "ACK disk failure leaves actual multiplier and epoch unchanged");
                Check(LocalProgression.Execute("economy-test-ack", ApplyAck) && LocalProgression.State.Economy.Server.RewardEpoch == 2 && LocalProgression.State.RubyGoldLevel == 1,
                    "successful ACK atomically publishes multiplier and cutoff");
                using (var response = EconomyResponseApply.Begin(new OnGetQuestStateResponseDTO { EconomyRevision = 3 }))
                {
                    Check(!LocalProgression.Execute("expected-rejection", _ => false), "failed response apply is observed");
                    LocalProgression.Execute("unrelated-save", s => { s.LastActiveUtc++; return true; });
                    Check(!response.Complete(true, false) && LocalProgression.State.ServerEconomyRevision == 2, "unrelated save cannot hide a failed response apply");
                }
                long observed = -1;
                void Observe() => observed = LocalProgression.State.ServerEconomyRevision;
                LocalProgression.Changed += Observe;
                try
                {
                    using var response = EconomyResponseApply.Begin(new OnGetQuestStateResponseDTO { EconomyRevision = 3 });
                    Check(LocalProgression.Execute("response-revision", s => { s.LastActiveUtc++; return true; }) && response.Complete(true, false) && observed == 3,
                        "response revision is durable before observers run");
                }
                finally { LocalProgression.Changed -= Observe; }
                var pending = new EconomyInputJournal(LocalProgression.EconomyJournalPath, LocalProgression.AccountKey);
                pending.Data.NextInputSequence = 2; pending.Data.Inputs.Add(new() { Sequence = 1, Command = first });
                pending.Changed(); pending.Pump(true);
                EconomyBatchProgression.ReceiveServerState(false);
                Check(!EconomyBatchProgression.Available && EconomyBatchProgression.HasUnknownResult, "unsupported server retains the pending journal and blocks economic fallback");
                EconomyBatchProgression.ConfirmFullRefresh();
                Check(EconomyBatchProgression.HasUnknownResult, "full refresh cannot erase unsupported journal protection");
                int before = LocalProgression.State.Economy.Hunts.Count;
                var noCapability = LocalProgression.State.DeepClone();
                EconomyBatchProgression.RecordHunt(noCapability, (long)eStage.Stage1_1, Id, null, new Dictionary<eMonsterType, int> { [eMonsterType.MON_GOBLIN] = 5 });
                Check(noCapability.Economy.Hunts.Count == before && noCapability.Economy.Hunts[0].TotalKills[0].Count == 3,
                    "stale saved metadata cannot record new hunts without current-login capability");
                var core = typeof(EconomyBatchProgression);
                FieldInfo Field(string name) => core.GetField(name, BindingFlags.Static | BindingFlags.NonPublic);
                var recoveryJournal = (EconomyInputJournal)Field("_journal").GetValue(null);
                recoveryJournal.Data.InFlight = request;
                recoveryJournal.Data.InFlightInputs = recoveryJournal.Data.Inputs.ToList(); recoveryJournal.Data.Inputs.Clear();
                recoveryJournal.Changed(); recoveryJournal.Pump(true);
                int failures = 0;
                Field("_recovering").SetValue(null, true); Field("_recovered").SetValue(null, false);
                Field("_startupMaySplit").SetValue(null, true); Field("_sentThisSession").SetValue(null, false);
                Field("_recoveryDone").SetValue(null, new Action<bool>(ok => { if (!ok) failures++; }));
                core.GetMethod("FailRequest", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { null });
                Check(failures == 1 && !(bool)Field("_recovering").GetValue(null) && !(bool)Field("_recovered").GetValue(null) &&
                    (bool)Field("_queryRequired").GetValue(null) && (bool)Field("_startupMaySplit").GetValue(null) &&
                    recoveryJournal.Data.InFlight.RequestId == Id,
                    "startup transport failure ends loading but preserves original request and fresh-session query proof");
                Field("_recovering").SetValue(null, true);
                Field("_recoveryDone").SetValue(null, new Action<bool>(ok => { if (!ok) failures++; }));
                core.GetMethod("ApplyResult", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { new OnEconomyBatchResponseDTO(), true });
                Check(failures == 2 && !(bool)Field("_recovering").GetValue(null) && recoveryJournal.Data.InFlight.RequestId == Id,
                    "invalid startup result also completes failure without replacing the sealed request");
                CheckFencesAndDiscard(Check);
                CheckStorageRetry(Check);
            }
            return new { passed = checks.Count, checks };
        }

        private static void CheckFencesAndDiscard(Action<bool, string> check)
        {
            var core = typeof(EconomyBatchProgression);
            FieldInfo Field(string name) => core.GetField(name, BindingFlags.Static | BindingFlags.NonPublic);
            object Call(string name, params object[] args) => core.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
            void Set(string name, object value) => Field(name).SetValue(null, value);
            var previousNetwork = NetworkManager.Instance;
            var owner = new GameObject("EconomyFenceAcceptance"); owner.SetActive(false);
            var previousClientId = Field("_clientId").GetValue(null);
            string path = Path.Combine(Path.GetTempPath(), "economy-fences-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                Call("ResetAccount");
                var network = owner.AddComponent<NetworkManager>(); NetworkManager.Instance = network;
                typeof(NetworkManager).GetProperty("LoginState").SetValue(network, GameLoginState.Ready);
                var journal = new EconomyInputJournal(path, LocalProgression.AccountKey);
                void Bind()
                {
                    Set("_journal", journal); Set("_network", network);
                    Set("_localGeneration", LocalProgression.AccountGeneration); Set("_networkGeneration", network.AccountGeneration);
                    Set("_supported", true); Set("_recovered", true); Set("_recovering", false);
                    Set("_batchUnknown", false); Set("_queryRequired", false); Set("_externalUnknown", false); Set("_blocked", false);
                    Set("_requestPending", false); Set("_externalWrite", true); Set("_nextHuntAt", float.MaxValue);
                }
                Bind();
                LocalProgression.Execute("economy-fence-fixture", s =>
                {
                    var fixture = Fixture(); s.Economy = fixture.Economy; s.MainBattle = fixture.MainBattle;
                    s.AttackLevel = s.HealthLevel = s.RubyGoldLevel = s.RubyExpLevel = 0;
                    s.Economy.Hunts.Add(Hunt(3)); return true;
                });
                var finished = new List<string>();
                check(EconomyBatchProgression.FlushBeforeWrite(ok => { finished.Add("quest:" + ok); EconomyBatchProgression.SetExternalWritePending(true); }),
                    "background quest fence is accepted");
                journal.Pump(true);
                LocalProgression.Execute("economy-five-kills", s => { s.Economy.Hunts[0] = Hunt(5); return true; });
                check(EconomyBatchProgression.FlushBeforeWrite(ok => finished.Add("wave:" + ok)), "wave completion queues behind an existing finite fence");
                journal.Pump(true);
                LocalProgression.Execute("economy-later-kills", s => { s.Economy.Hunts[0] = Hunt(7); return true; });
                check(journal.Data.Inputs.Count == 2 && journal.Data.Inputs[0].Command.TotalKills[0].Count == 3 &&
                    journal.Data.Inputs[1].Command.TotalKills[0].Count == 5, "each waiter captures its own cumulative boundary without later kills");
                journal.Data.Inputs.RemoveAt(0); journal.Changed(); journal.Pump(true);
                EconomyBatchProgression.SetExternalWritePending(false);
                check((bool)Call("TryFinishFence") && finished.SequenceEqual(new[] { "quest:True" }), "first fence completes without waiting for the later wave boundary");
                journal.Data.Inputs.Clear(); journal.Changed(); journal.Pump(true);
                check(!(bool)Call("TryFinishFence") && finished.Count == 1, "queued external write owns the boundary until Network Done");
                EconomyBatchProgression.SetExternalWritePending(false);
                check((bool)Call("TryFinishFence") && !(bool)Call("TryFinishFence") && finished.SequenceEqual(new[] { "quest:True", "wave:True" }),
                    "wave fence resumes once and is not extended by kills after capture");
                EconomyBatchProgression.SetExternalWritePending(true);
                EconomyBatchProgression.FlushBeforeWrite(ok => finished.Add("a:" + ok));
                EconomyBatchProgression.FlushBeforeWrite(ok => finished.Add("b:" + ok)); journal.Pump(true);
                Call("FinishFences", false); Call("FinishFences", false);
                check(finished.Skip(2).SequenceEqual(new[] { "a:False", "b:False" }) && journal.Data.Inputs.Count == 1,
                    "failure completes all queued waiters once and preserves unsent hunts");
                bool staleCallback = false;
                EconomyBatchProgression.FlushBeforeWrite(_ => staleCallback = true); Call("ResetAccount");
                check(!staleCallback, "account reset drops stale fence callbacks");
                Bind();

                var attack = new EconomyCommandDTO { Kind = EconomyCommandKind.GoldAttack, Mode = 1, CountLimit = 1, SpendLimit = 50 };
                var health = new EconomyCommandDTO { Kind = EconomyCommandKind.GoldHealth, Mode = 1, CountLimit = 1, SpendLimit = 50 };
                var ruby = new EconomyCommandDTO { Kind = EconomyCommandKind.RubyGold, Mode = 1, CountLimit = 1, SpendLimit = 20 };
                var request = new OnEconomyBatchRequestDTO
                { RequestId = Id, Generation = 1, Sequence = 1, ExpectedEconomyRevision = 1, AppliedRewardEpoch = 1, Commands = new() { attack, health, ruby, Hunt(3) } };
                journal.Data.NextInputSequence = 7; journal.Data.InFlight = request;
                journal.Data.InFlightInputs = request.Commands.Select((c, i) => new EconomyInput { Sequence = i + 1, Command = EconomyCopies.Copy(c) }).ToList();
                journal.Data.Inputs = new() { new() { Sequence = 5, Command = health }, new() { Sequence = 6, Command = Hunt(5) } };
                journal.Changed(); journal.Pump(true);
                var receipt = new EconomyBatchReceiptDTO
                {
                    Results = new()
                    {
                        new() { Index = 0, Kind = EconomyCommandKind.GoldAttack, Status = EconomyCommandStatus.Applied, AppliedCount = 1, SpentGold = 50 },
                        new() { Index = 1, Kind = EconomyCommandKind.GoldHealth, Status = EconomyCommandStatus.Rejected, ErrorCode = Protocol.Message.Data.eErrorCode.eNotEnoughGold },
                        new() { Index = 2, Kind = EconomyCommandKind.RubyGold, Status = EconomyCommandStatus.Unprocessed },
                        new() { Index = 3, Kind = EconomyCommandKind.Hunt, Status = EconomyCommandStatus.Unprocessed }
                    }
                };
                int successes = 0, failures = 0, notices = 0;
                void Completed(EconomyInputCompletion result)
                { if (result.Success) successes++; else { failures++; if (result.Message != EconomyBatchProgression.FailureMessage) throw new InvalidOperationException("Unexpected failure message"); } }
                void Failed() => notices++;
                EconomyBatchProgression.InputCompleted += Completed; EconomyBatchProgression.SpendingFailed += Failed;
                try
                {
                    Call("ResolveCompletedInputs", receipt, false);
                    check(successes == 1 && failures == 0 && notices == 0 && journal.Data.Inputs.All(x => x.Command.Kind == EconomyCommandKind.Hunt),
                        "terminal rejection preserves success and hunts while deferring all discard notifications");
                    check(journal.Committed.InFlight != null, "failure message cannot precede publishing the durable journal removal");
                    journal.Pump(true); Call("PublishDiscardedInputs"); Call("PublishDiscardedInputs");
                    check(failures == 3 && notices == 1 && journal.Data.Inputs.Count == 2 &&
                        new EconomyInputJournal(path, LocalProgression.AccountKey).Data.InFlight == null,
                        "mixed suffix and dependent queued growth are removed durably with one exact failure notice");
                    journal.Data.Inputs.Clear();
                    journal.Data.Inputs.Add(new() { Sequence = 7, Held = true, Command = attack }); journal.Data.NextInputSequence = 8;
                    journal.Changed(); journal.Pump(true);
                    LocalProgression.Execute("economy-held-cost-change", s => { s.AttackLevel = 1; s.Wallet[eCurrency.Gold] = 1000; s.Economy.Hunts.Clear(); return true; });
                    check(!EconomyBatchProgression.TryGetHeldPreview(out _, out _, out _, out _) && journal.Data.Inputs.Count == 1,
                        "busy held preview does not discard a known unexecuted input");
                    EconomyBatchProgression.SetExternalWritePending(false);
                    check(!EconomyBatchProgression.TryGetHeldPreview(out _, out _, out _, out _) && !EconomyBatchProgression.HasHeldSpending && notices == 1,
                        "held plan beyond its original spending cap is removed without a new dialog");
                    journal.Pump(true); Call("PublishDiscardedInputs");
                    check(failures == 4 && notices == 2, "invalid held plan emits one notice only after durable removal");
                    journal.Data.Inputs.Add(new() { Sequence = 8, Held = true, Command = health }); journal.Data.NextInputSequence = 9;
                    journal.Changed(); journal.Pump(true);
                    check(EconomyBatchProgression.TryGetHeldPreview(out int count, out long gold, out long rubyCost, out long knowledge) && count == 1 && gold == 50 && rubyCost == 0 && knowledge == 0 && journal.Data.Inputs[0].Held,
                        "valid same-device restart input remains held for explicit confirmation");

                    var mage = new EconomyCommandDTO { Kind = EconomyCommandKind.MageEnhance, SkillId = 3, Mode = 1, CountLimit = 1, SpendLimit = 10 };
                    var otherMage = EconomyCopies.Copy(mage); otherMage.SkillId = 1;
                    request.Commands = new() { mage, attack, ruby };
                    journal.Data.InFlight = request;
                    journal.Data.InFlightInputs = request.Commands.Select((c, i) => new EconomyInput { Sequence = 9 + i, Command = c }).ToList();
                    journal.Data.Inputs = new() { new() { Sequence = 12, Command = mage }, new() { Sequence = 13, Command = otherMage },
                        new() { Sequence = 14, Command = health }, new() { Sequence = 15, Command = ruby } };
                    journal.Data.NextInputSequence = 16;
                    receipt.Results = new()
                    {
                        new() { Index = 0, Kind = mage.Kind, Status = EconomyCommandStatus.Rejected, ErrorCode = Protocol.Message.Data.eErrorCode.eNotEnoughArcaneKnowledge },
                        new() { Index = 1, Kind = attack.Kind, Status = EconomyCommandStatus.Applied, AppliedCount = 1, SpentGold = 50 },
                        new() { Index = 2, Kind = ruby.Kind, Status = EconomyCommandStatus.Applied, AppliedCount = 1, SpentRuby = 20 }
                    };
                    Call("ResolveCompletedInputs", receipt, false);
                    journal.Pump(true); Call("PublishDiscardedInputs");
                    check(successes == 3 && notices == 3 && failures == 6 &&
                        new EconomyInputJournal(path, LocalProgression.AccountKey).Data.Inputs.Select(x => x.Sequence).SequenceEqual(new long[] { 13, 14, 15 }),
                        "rejected mage restores that skill only across the batch boundary; both character upgrades complete");

                    LocalProgression.Execute("mage-restart-fixture", s =>
                    {
                        s.Economy.Server.SupportsMageEnhance = true;
                        s.MageSkills[1] = new();
                        s.MageSkills.Remove(3); s.Wallet[eCurrency.ArcaneKnowledge] = 10;
                        return true;
                    });
                    journal.Data.Inputs = new() { new() { Sequence = 16, Held = true, Command = mage }, new() { Sequence = 17, Held = true, Command = otherMage },
                        new() { Sequence = 18, Held = true, Command = health } };
                    journal.Data.NextInputSequence = 19; journal.Changed(); journal.Pump(true);
                    check(!EconomyBatchProgression.TryGetHeldPreview(out _, out _, out _, out _) && journal.Data.Inputs.Count == 2 &&
                        EconomyBatchProgression.HasPendingFailureNotice,
                        "a lost mage target on restart preserves independently affordable held inputs");
                    journal.Pump(true);
                    check(EconomyBatchProgression.TryGetHeldPreview(out count, out gold, out rubyCost, out knowledge) &&
                        count == 2 && gold == 50 && rubyCost == 0 && knowledge == 10 && EconomyBatchProgression.ResumeHeldInputs(),
                        "same-device recovery confirms the knowledge cost together with gold before resuming");
                    journal.Pump(true);
                    Set("_clientId", Id);
                    LocalProgression.Execute("mage-older-server", s => { s.Economy.Server.SupportsMageEnhance = false; return true; });
                    EconomyBatchProgression.ReceiveServerState(true); EconomyBatchProgression.ConfirmFullRefresh();
                    check(!EconomyBatchProgression.Available && EconomyBatchProgression.HasUnknownResult && journal.Data.Inputs.Count == 2,
                        "older server keeps a saved mage batch and blocks fallback spending");
                    LocalProgression.Execute("mage-capable-server", s => { s.Economy.Server.SupportsMageEnhance = true; return true; });
                    EconomyBatchProgression.ReceiveServerState(true); EconomyBatchProgression.ConfirmFullRefresh();
                    check(EconomyBatchProgression.SupportsMageEnhance && !EconomyBatchProgression.HasUnknownResult && journal.Data.Inputs.Count == 2,
                        "restored server capability resumes the same journal without restarting the app");
                    request.Commands = new() { mage, health };
                    journal.Data.InFlight = request;
                    journal.Data.InFlightInputs = request.Commands.Select((c, i) => new EconomyInput { Sequence = 19 + i, Command = c }).ToList();
                    journal.Data.Inputs.Add(new() { Sequence = 21, Command = mage, Held = true });
                    journal.Data.Inputs.Add(new() { Sequence = 22, Command = ruby, Held = true });
                    journal.Data.Inputs.Add(new() { Sequence = 23, Command = Hunt(5) });
                    journal.Data.NextInputSequence = 24;
                    journal.Changed(); journal.Pump(true);
                    string frozen = EconomyInputJournal.Hash(request);
                    LocalProgression.Execute("mage-rollback", s =>
                    { s.Economy.Server.SupportsMageEnhance = false; s.Economy.Server.MageEnhanceMode = MageEnhanceBatchMode.RecoveryOnly; return true; });
                    EconomyBatchProgression.ReceiveServerState(true); EconomyBatchProgression.ConfirmFullRefresh();
                    journal.Pump(true); Call("PublishDiscardedInputs");
                    var rollback = new EconomyInputJournal(path, LocalProgression.AccountKey).Data;
                    check(EconomyBatchProgression.Available && !EconomyBatchProgression.SupportsMageEnhance && !EconomyBatchProgression.CanStartSingleMageEnhance &&
                        rollback.InFlight.RequestId == request.RequestId && EconomyInputJournal.Hash(rollback.InFlight) == frozen &&
                        rollback.Inputs.Select(x => x.Sequence).SequenceEqual(new long[] { 18, 22, 23 }) && rollback.Inputs[1].Held,
                        "rollback preserves frozen mage identity, held ruby, live gold and hunt, and cancels only unsent mage inputs");
                    var knownFailed = new EconomyBatchReceiptDTO { Results = new()
                    {
                        new() { Kind = mage.Kind, Status = EconomyCommandStatus.Rejected, ErrorCode = Protocol.Message.Data.eErrorCode.UnsupportedProtocolVersion },
                        new() { Index = 1, Kind = health.Kind, Status = EconomyCommandStatus.Applied, AppliedCount = 1, SpentGold = 50 }
                    } };
                    Call("ResolveCompletedInputs", knownFailed, false);
                    journal.Pump(true); Call("PublishDiscardedInputs");
                    check(journal.Data.InFlight == null && journal.Data.Inputs.Select(x => x.Sequence).SequenceEqual(new long[] { 18, 22, 23 }) &&
                        !EconomyBatchProgression.CanStartSingleMageEnhance, "a terminal rollback rejection preserves independent inputs and the held-input confirmation");
                    request.RequestId = Guid.NewGuid().ToString("N");
                    journal.Data.InFlight = request;
                    journal.Data.InFlightInputs = request.Commands.Select((c, i) => new EconomyInput { Sequence = 24 + i, Command = c }).ToList();
                    journal.Data.NextInputSequence = 26; journal.Changed(); journal.Pump(true);
                    Set("_retryAt", float.MaxValue); Set("_recovered", false); Set("_sentThisSession", false);
                    check(EconomyBatchProgression.RecoverPending(_ => { }, previousSessionReplaced: true), "restart checks the original mixed batch");
                    var absent = EconomyBatchContractAcceptance.Fixture();
                    absent.Status = EconomyBatchStatus.NotFound; absent.RequestId = request.RequestId; absent.Receipt = null;
                    absent.EconomyRevision = LocalProgression.State.ServerEconomyRevision;
                    absent.CurrentState.EconomyBatchState = EconomyCopies.Copy(LocalProgression.State.Economy.Server);
                    absent.CurrentState.EconomyBatchState.NextSequence = request.Sequence;
                    absent.CurrentState.MainBattle.RunId = Id;
                    absent.CurrentState.MageTowerState = new() { Skills = new(), EquippedSkillIds = new[] { -1, -1, -1, -1, -1 } };
                    Call("ApplyResult", absent, true); journal.Pump(true);
                    var unexecuted = new EconomyInputJournal(path, LocalProgression.AccountKey).Data;
                    check(unexecuted.InFlight == null && unexecuted.AbandonedRequest?.RequestId == request.RequestId &&
                        EconomyInputJournal.Hash(unexecuted.AbandonedRequest) == EconomyInputJournal.Hash(request) &&
                        unexecuted.Inputs.Select(x => x.Sequence).SequenceEqual(new long[] { 18, 22, 23, 25 }) &&
                        unexecuted.Inputs.All(x => x.Held == (x.Command.Kind != EconomyCommandKind.Hunt)),
                        "restart NotFound archives the original ID and body, cancels only unexecuted mage and requires confirmation for independent spending");
                }
                finally { EconomyBatchProgression.InputCompleted -= Completed; EconomyBatchProgression.SpendingFailed -= Failed; }
            }
            finally
            {
                Call("ResetAccount"); Set("_clientId", previousClientId); NetworkManager.Instance = previousNetwork; UnityEngine.Object.DestroyImmediate(owner);
                foreach (string file in new[] { path, path + ".tmp", path + ".bak" }) if (File.Exists(file)) File.Delete(file);
            }
        }

        private static void CheckStorageRetry(Action<bool, string> check)
        {
            var core = typeof(EconomyBatchProgression);
            FieldInfo Field(string name) => core.GetField(name, BindingFlags.Static | BindingFlags.NonPublic);
            object Call(string name, params object[] args) => core.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
            void Set(string name, object value) => Field(name).SetValue(null, value);
            var previousNetwork = NetworkManager.Instance;
            var received = typeof(QuestManager).GetField("_received", BindingFlags.Static | BindingFlags.NonPublic);
            var source = typeof(QuestManager).GetField("_receivedSource", BindingFlags.Static | BindingFlags.NonPublic);
            var previousQuests = received.GetValue(null); var previousSource = source.GetValue(null);
            var owner = new GameObject("EconomyStorageRetryAcceptance"); owner.SetActive(false);
            string path = Path.Combine(Path.GetTempPath(), "economy-retry-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                Call("ResetAccount");
                var network = owner.AddComponent<NetworkManager>(); NetworkManager.Instance = network;
                typeof(NetworkManager).GetProperty("LoginState").SetValue(network, GameLoginState.Ready);
                LocalProgression.Execute("storage-retry-fixture", s =>
                {
                    var fixture = Fixture(); s.Economy = fixture.Economy; s.MainBattle = fixture.MainBattle; s.ServerEconomyRevision = 1;
                    s.AttackLevel = s.HealthLevel = s.RubyGoldLevel = s.RubyExpLevel = 0;
                    s.Wallet[eCurrency.Gold] = 1000000000; s.Wallet[eCurrency.Ruby] = 100;
                    s.HighestMainClear = 0x200020005; s.PendingRubyRequestId = null; s.PendingMainCompletion = null; return true;
                });
                var request = new OnEconomyBatchRequestDTO
                {
                    RequestId = Id, Generation = 1, Sequence = 1, ExpectedEconomyRevision = 1, AppliedRewardEpoch = 1,
                    Commands = new() { new() { Kind = EconomyCommandKind.GoldAttack, Mode = 1, CountLimit = 1, SpendLimit = 50 } }
                };
                string hash = EconomyInputJournal.Hash(request);
                var journal = new EconomyInputJournal(path, LocalProgression.AccountKey);
                journal.Data.Generation = 1; journal.Data.NextInputSequence = 2; journal.Data.InFlight = request;
                journal.Data.InFlightInputs.Add(new() { Sequence = 1, Command = EconomyCopies.Copy(request.Commands[0]) });
                journal.Changed(); journal.Pump(true);
                Set("_journal", journal); Set("_network", network); Set("_localGeneration", LocalProgression.AccountGeneration);
                Set("_networkGeneration", network.AccountGeneration); Set("_supported", true); Set("_recovered", true);
                Set("_nextHuntAt", float.MaxValue);
                var ordinary = NetworkManager.CreateError("Business", "storage failure", details: new ErrorResponseDTO { ErrorCode = Protocol.Message.Data.eErrorCode.StorageFailure });
                Call("FailRequest", ordinary);
                check(!EconomyBatchProgression.IsStorageRetryWaiting && !EconomyBatchProgression.CanAcceptGrowth,
                    "unclassified storage failure cannot accept more growth");
                var throttle = NetworkManager.CreateError("Business", "storage throttle", details: new ErrorResponseDTO
                { ErrorCode = Protocol.Message.Data.eErrorCode.StorageFailure, StorageThrottled = true, RetryAfterSeconds = 5 });
                float before = Time.realtimeSinceStartup;
                Call("FailRequest", throttle);
                check(EconomyBatchProgression.IsStorageRetryWaiting && EconomyBatchProgression.CanAcceptGrowth &&
                    (float)Field("_retryAt").GetValue(null) >= before + 5, "confirmed live throttle permits projection while honoring server cooldown");
                Call("SendQuery"); Call("SendBatch");
                check(!(bool)Field("_requestPending").GetValue(null) && (int)Field("_storageRetryRemaining").GetValue(null) == 3,
                    "neither query nor resubmission can send early or consume retry budget");
                for (int i = 0; i < 62; i++)
                    check(EconomyBatchProgression.TryQueueGold(true, 1, null, out _) == EconomyInputStatus.PendingSave, "throttled input remains ordered " + (i + 2));
                check(EconomyBatchProgression.TryQueueRuby(false, out _) == EconomyInputStatus.PendingSave &&
                    EconomyBatchProgression.GetProjection().RubyGoldLevel == 1 && LocalProgression.State.RubyGoldLevel == 0 &&
                    LocalProgression.Balance(eCurrency.Ruby) == 100, "ruby input projects during 429 without consuming ruby or applying the multiplier");
                check(EconomyBatchProgression.PendingSpendingCount == 64 && EconomyBatchProgression.TryQueueGold(true, 1, null, out _) == EconomyInputStatus.QueueFull &&
                    EconomyBatchProgression.GetProjection().AttackLevel == 63 && LocalProgression.State.AttackLevel == 0 && LocalProgression.Balance(eCurrency.Gold) == 1000000000 &&
                    EconomyInputJournal.Hash(journal.Data.InFlight) == hash, "429 inputs retain cap and sealed body without changing combat or actual gold");
                journal.Pump(true);
                var reopened = new EconomyInputJournal(path, LocalProgression.AccountKey);
                check(EconomyInputJournal.Hash(reopened.Data.InFlight) == hash && reopened.Data.Inputs.Count == 63 &&
                    reopened.Data.StorageRetryNotBeforeUtcMs > DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    "process restart rehydrates original request, durable follow-up inputs and throttle deadline");
                Set("_journal", reopened); Set("_retryAt", 0f); Call("RestoreStorageRetryDeadline");
                check((float)Field("_retryAt").GetValue(null) > Time.realtimeSinceStartup && !(bool)Call("TryBeginRetrySend"),
                    "rehydrated request still cannot query before its durable cooldown");
                reopened.Data.InFlight.Generation = 2; Set("_retryAt", 0f); Call("RestoreStorageRetryDeadline");
                check((float)Field("_retryAt").GetValue(null) == 0, "old owner generation cannot apply a retained cooldown to another request");
                reopened.Data.InFlight.Generation = 1; Set("_journal", journal);
                var noDelay = NetworkManager.CreateError("Business", "storage throttle", details: new ErrorResponseDTO
                { ErrorCode = Protocol.Message.Data.eErrorCode.StorageFailure, StorageThrottled = true });
                for (int i = 1; i <= 3; i++)
                {
                    Set("_retryAt", 0f);
                    check((bool)Call("TryBeginRetrySend"), "bounded retry transmission " + i);
                    float retryStarted = Time.realtimeSinceStartup;
                    Call("FailRequest", noDelay);
                    float deadline = (float)Field("_retryAt").GetValue(null);
                    check(deadline >= retryStarted + Math.Min(30, 2 << i) &&
                        deadline <= Time.realtimeSinceStartup + Math.Min(30, 2 << i), "missing Retry-After backs off " + i);
                }
                check(!EconomyBatchProgression.IsStorageRetryWaiting && !EconomyBatchProgression.CanAcceptGrowth &&
                    !EconomyBatchProgression.TryResumeStorageRetry() && journal.Data.InFlight.RequestId == Id && journal.Data.Inputs.Count == 63,
                    "exhaustion preserves pending spending and manual confirmation cannot bypass cooldown");
                Set("_retryAt", 0f); journal.Changed();
                check(EconomyBatchProgression.TryResumeStorageRetry(), "one manual retry can resume after cooldown");
                // Pump above only starts this dirty journal write; this test never dispatches an SDK call.
                journal.Pump(true);
                check((bool)Call("TryBeginRetrySend") && (int)Field("_storageRetryRemaining").GetValue(null) == 0,
                    "manual confirmation grants exactly one further transmission");
                Call("FailRequest", noDelay);
                journal.Pump(true);
                Set("_journal", reopened); Set("_recovered", false); Set("_retryAt", float.MaxValue);
                int recoveryFailures = 0;
                check(EconomyBatchProgression.RecoverPending(ok => { if (!ok) recoveryFailures++; }, previousSessionReplaced: true), "restart begins by checking retained request");
                reopened.Pump(true); Call("FailRequest", throttle);
                check(recoveryFailures == 1 && !EconomyBatchProgression.IsStorageRetryWaiting && !EconomyBatchProgression.CanAcceptGrowth &&
                    reopened.Data.Inputs.All(x => x.Held) && EconomyInputJournal.Hash(reopened.Data.InFlight) == hash,
                    "restart throttle keeps spending held and completes failure for explicit reconfirmation");

                var response = EconomyBatchContractAcceptance.Fixture();
                response.RequestId = Id; response.EconomyRevision = 2;
                response.Receipt.RequestId = Id; response.Receipt.CommittedRevision = 2; response.Receipt.PayloadHash = hash;
                response.Receipt.Results = response.Receipt.Results.Take(1).ToList();
                response.CurrentState.Currency.Gold = 999999950;
                response.CurrentState.MainBattle.RunId = Id; response.CurrentState.EconomyBatchState.OwnerClientId = Id;
                response.Quests = new() { DailyPeriodKey = "2026-11-19", WeeklyPeriodKey = "2026-11-16", ServerTimeUtc = 1, Rows = new() };
                int completed = 0;
                void Completed(EconomyInputCompletion result) { if (result.Success) completed++; }
                EconomyBatchProgression.InputCompleted += Completed;
                try
                {
                    Set("_recovered", true); Set("_retryAt", 0f);
                    Call("ApplyResult", response, true);
                    check(completed == 1 && LocalProgression.State.AttackLevel == 1 && LocalProgression.Balance(eCurrency.Gold) == 999999950 &&
                        ReferenceEquals(received.GetValue(null), response.Quests) && reopened.Data.StorageRetryNotBeforeUtcMs == 0 &&
                        (float)Field("_retryAt").GetValue(null) == 0, "missing ACK recovers growth once and clears cooldown after applying optional quests");
                    reopened.Pump(true);
                    reopened.Data.InFlight = request; reopened.Data.InFlightInputs = new() { new() { Sequence = 1, Command = EconomyCopies.Copy(request.Commands[0]) } };
                    reopened.Changed(); reopened.Pump(true);
                    var existingQuests = received.GetValue(null);
                    response.Quests = new() { DailyPeriodKey = "2026-11-19", WeeklyPeriodKey = "2026-11-16", ServerTimeUtc = 2, Rows = new() };
                    Call("ApplyResult", response, true);
                    check(completed == 1 && LocalProgression.State.AttackLevel == 1 && LocalProgression.Balance(eCurrency.Gold) == 999999950 &&
                        ReferenceEquals(received.GetValue(null), existingQuests), "already committed receipt neither debits twice nor repeats callbacks or quest UI");
                    check(!(bool)Call("Owns", LocalProgression.AccountGeneration, network.AccountGeneration, Id), "late ACK loses ownership after its journal entry is cleared");
                    reopened.Pump(true);
                }
                finally { EconomyBatchProgression.InputCompleted -= Completed; }
            }
            finally
            {
                Call("ResetAccount"); NetworkManager.Instance = previousNetwork; UnityEngine.Object.DestroyImmediate(owner);
                received.SetValue(null, previousQuests); source.SetValue(null, previousSource);
                foreach (string file in new[] { path, path + ".tmp", path + ".bak" }) if (File.Exists(file)) File.Delete(file);
            }
        }

        private static ProgressionState Fixture() => new()
        {
            ServerEconomyRevision = 1, Wallet = new() { [eCurrency.Gold] = 0, [eCurrency.Ruby] = 100 },
            MainBattle = new() { RunId = Id, Stage = (long)eStage.Stage1_1 },
            Economy = new() { Server = new() { Version = 1, OwnerClientId = Id, Generation = 1, NextSequence = 1, RewardEpoch = 1, RewardEpochs = new() { new() { Id = 1 } } } }
        };
        private static EconomyCommandDTO Hunt(short count) => new()
        {
            Kind = EconomyCommandKind.Hunt, RewardEpoch = 1, MainRunId = Id, Stage = Protocol.Message.Data.eStage.Stage1_1,
            TotalKills = new() { new() { MonsterType = (Protocol.Message.Data.eMonsterType)Monster, Count = count } }
        };
        private static EconomyBatchStateDTO NextMetadata(ProgressionState s)
        {
            var result = EconomyCopies.Copy(s.Economy.Server); result.RewardEpoch = 2;
            result.RewardEpochs = new() { new() { Id = 1 }, new() { Id = 2, GoldLevel = 1 } };
            return result;
        }
    }
}
#endif
