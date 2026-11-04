#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Direction;
using Newtonsoft.Json;
using Protocol.Message.Dto;
using Scripts.Core.Manager;
using UnityEngine;

namespace KingdomIdle.Balance
{
    public static class ServerTutorialAcceptance
    {
        // The fixture has no authentication context and never writes a save file or contacts the server.
        public static object Run()
        {
            if (GameDirectInteraction.Step != null) throw new InvalidOperationException("Stop the active guide before running the isolated acceptance.");
            const BindingFlags statics = BindingFlags.Static | BindingFlags.NonPublic;
            const BindingFlags instance = BindingFlags.Instance | BindingFlags.NonPublic;
            var stateField = typeof(LocalProgression).GetField("_state", statics);
            var openingField = typeof(LocalProgression).GetField("_opening", statics);
            object previousState = stateField.GetValue(null), previousOpening = openingField.GetValue(null);
            var generationProperty = typeof(LocalProgression).GetProperty("AccountGeneration");
            long generation = LocalProgression.AccountGeneration;
            var previousNetwork = NetworkManager.Instance;
            var cache = new Dictionary<FieldInfo, object>();
            foreach (string name in new[] { "_records", "_network", "_accountGeneration", "_networkGeneration" })
            {
                var field = typeof(GameDirectProgressStore).GetField(name, statics);
                cache[field] = field.GetValue(null);
            }
            var owner = new GameObject("ServerTutorialAcceptance") { hideFlags = HideFlags.HideAndDontSave };
            owner.SetActive(false);
            var checks = new List<string>();
            void Check(bool value, string label)
            { if (!value) throw new InvalidOperationException("Server tutorial acceptance: " + label); checks.Add(label); }
            TutorialProgressDTO Record(string id, bool finished, params string[] steps) => new()
            { SequenceId = id, IsFinished = finished, ConfirmedSteps = new List<string>(steps) };
            try
            {
                var network = owner.AddComponent<NetworkManager>();
                typeof(NetworkManager).GetMethod("Init", instance).Invoke(network, null);
                typeof(NetworkManager).GetField("_sessionGUID", instance).SetValue(network, "fixture");
                typeof(NetworkManager).GetProperty("LoginState").SetValue(network, GameLoginState.Ready);
                NetworkManager.Instance = network;
                var state = new ProgressionState();
                state.Modules["game-direct:guide_development"] = "{\"Completed\":true,\"ConfirmedSteps\":[\"attack_once\"]}";
                state.Modules["game-direct-action:guide_development:attack_once"] = "1";
                state.Claims.Add("game-direct-grant:guide_gacha:equipment_once");
                state.Wallet[eCurrency.AncientCoin] = 123;
                stateField.SetValue(null, state); openingField.SetValue(null, true);
                string original = JsonConvert.SerializeObject(state);
                var store = new GameDirectProgressStore();

                GameDirectProgressStore.ReceiveServerState(null);
                Check(!store.TryLoad("guide_development", generation, out _), "missing server field defers guides instead of starting at zero");
                GameDirectProgressStore.ReceiveServerState(new List<TutorialProgressDTO>());
                Check(store.TryLoad("guide_development", generation, out var progress) && !progress.Completed && progress.ConfirmedSteps.Count == 0,
                    "empty server records start from zero despite completed local progress");
                var source = Record("guide_development", false, "open");
                GameDirectProgressStore.ReceiveServerState(new List<TutorialProgressDTO> { source });
                source.ConfirmedSteps.Add("attack_once");
                Check(store.TryLoad(source.SequenceId, generation, out progress) && progress.ConfirmedSteps.Count == 1,
                    "server snapshot is copied and resumes only confirmed server steps");
                progress.ConfirmedSteps.Clear();
                Check(store.TryLoad(source.SequenceId, generation, out progress) && progress.ConfirmedSteps.Count == 1,
                    "caller edits cannot mutate the server cache");

                var sent = Record(source.SequenceId, false, "open", "gold");
                Check(!GameDirectProgressStore.AcceptSavedProgress(sent, Record("other", false, "open", "gold"), generation, network.AccountGeneration),
                    "a response for another sequence is rejected");
                Check(!GameDirectProgressStore.AcceptSavedProgress(sent, Record(sent.SequenceId, false, "open"), generation, network.AccountGeneration),
                    "a response missing the requested confirmation is rejected");
                Check(GameDirectProgressStore.AcceptSavedProgress(sent, sent, generation, network.AccountGeneration) &&
                    GameDirectProgressStore.AcceptSavedProgress(sent, sent, generation, network.AccountGeneration),
                    "an identical merge acknowledgement is safe to receive again");
                var finished = Record(sent.SequenceId, true, "open", "gold");
                Check(!GameDirectProgressStore.AcceptSavedProgress(finished, sent, generation, network.AccountGeneration),
                    "a completion or skip cannot be acknowledged as unfinished");
                Check(GameDirectProgressStore.AcceptSavedProgress(finished, finished, generation, network.AccountGeneration) &&
                    store.TryLoad(sent.SequenceId, generation, out progress) && progress.Completed,
                    "server finished records suppress both completed and skipped guides");
                Check(!GameDirectProgressStore.AcceptSavedProgress(sent, sent, generation, network.AccountGeneration),
                    "an older save response cannot regress a finished sequence");
                Check(!GameDirectProgressStore.AcceptSavedProgress(finished, finished, generation + 1, network.AccountGeneration),
                    "another local account generation cannot accept this response");
                generationProperty.SetValue(null, generation + 1);
                Check(!GameDirectProgressStore.Available, "changing local account hides the prior server cache");
                generationProperty.SetValue(null, generation);

                var failed = store.TrySaveAsync("guide_gacha", generation, new GameDirectProgress { Skipped = true }, CancellationToken.None).GetAwaiter();
                Check(failed.IsCompleted && !failed.GetResult() && store.TryLoad("guide_gacha", generation, out progress) && !progress.Completed,
                    "unauthenticated save fails without publishing the unconfirmed skip");
                Check(JsonConvert.SerializeObject(state) == original, "server tutorial reads and writes preserve local game data and reward claims");

                var step = new GameDirectStep(new GameDirectStepData
                { id = "attack_once", completion = GuideCompletion.Action, action = GuideAction.AttackOnce }, 1, 1);
                GameDirectInteraction.Begin("guide_development", step, generation, () => true);
                Check(!GameDirectInteraction.OwnsInput && GameDirectInteraction.CanPerform(GuideAction.AttackOnce, 1) &&
                    GameDirectInteraction.CanPerform(GuideAction.AttackOnce, 10) && GameDirectInteraction.CanPerform(GuideAction.None, 1) &&
                    GameDirectInteraction.CanPerform(GuideAction.EquipmentPullOnce, 1) && GameDirectInteraction.CanPerform(GuideAction.SkillPullOnce, 1),
                    "an undisplayed guide leaves ordinary economic input available");
                string pendingReceipt = GameDirectInteraction.CaptureReceipt(GuideAction.AttackOnce, 1);
                string pendingTenReceipt = GameDirectInteraction.CaptureReceipt(GuideAction.AttackOnce, 10);
                Check(pendingReceipt == null && pendingTenReceipt == null, "pending requests have no practical receipt");
                long pendingEpoch = GameDirectInteraction.Epoch;
                GameDirectInteraction.Suspend();
                Check(!GameDirectInteraction.OwnsInput && GameDirectInteraction.Epoch == pendingEpoch && GameDirectInteraction.CanPerform(GuideAction.AttackOnce, 1),
                    "suspending a never-displayed guide leaves ordinary input available");
                generationProperty.SetValue(null, generation + 1);
                Check(!GameDirectInteraction.CanPerform(GuideAction.AttackOnce, 1), "pending input rejects another local account generation");
                generationProperty.SetValue(null, generation);
                var networkGenerationProperty = typeof(NetworkManager).GetProperty("AccountGeneration");
                long networkGeneration = network.AccountGeneration;
                networkGenerationProperty.SetValue(network, networkGeneration + 1);
                Check(!GameDirectInteraction.CanPerform(GuideAction.SkillPullOnce, 1), "pending input rejects another authentication generation");
                networkGenerationProperty.SetValue(network, networkGeneration);
                GameDirectInteraction.ReportSaveFailure();
                Check(GameDirectInteraction.SaveFailed && !GameDirectInteraction.CanPerform(GuideAction.AttackOnce, 1),
                    "save failure blocks consumption before guide display");
                GameDirectInteraction.End();
                int entered = 0;
                GameDirectInteraction.Begin("guide_development", step, generation, () => { entered++; return true; });
                pendingReceipt = GameDirectInteraction.CaptureReceipt(GuideAction.AttackOnce, 1);
                pendingTenReceipt = GameDirectInteraction.CaptureReceipt(GuideAction.AttackOnce, 10);
                GameDirectInteraction.Arm();
                Check(GameDirectInteraction.OwnsInput && GameDirectInteraction.CanPerform(GuideAction.AttackOnce, 1) &&
                    !GameDirectInteraction.CanPerform(GuideAction.AttackOnce, 10) && !GameDirectInteraction.CanPerform(GuideAction.None, 1),
                    "a shown practice owns input and permits only its assigned one action");
                var pendingDraft = state.DeepClone();
                GameDirectInteraction.RecordSuccess(pendingDraft, GuideAction.AttackOnce, pendingReceipt);
                GameDirectInteraction.RecordSuccess(pendingDraft, GuideAction.AttackOnce, pendingTenReceipt);
                Check(pendingDraft.Modules["game-direct-action:guide_development:attack_once"] == "1",
                    "normal one or ten requests cannot become practical receipts after Arm");
                string receipt = GameDirectInteraction.CaptureReceipt(GuideAction.AttackOnce, 1);
                Check(receipt != null && GameDirectInteraction.CaptureReceipt(GuideAction.AttackOnce, 10) == null,
                    "only a shown single action captures the execution receipt");
                long shownEpoch = GameDirectInteraction.Epoch;
                GameDirectInteraction.Suspend();
                Check(GameDirectInteraction.OwnsInput && !GameDirectInteraction.Armed && GameDirectInteraction.Epoch != shownEpoch &&
                    !GameDirectInteraction.CanPerform(GuideAction.AttackOnce, 1), "a covered practice keeps ownership and cancels delayed UI callbacks");
                GameDirectInteraction.Arm();
                Check(entered == 1 && GameDirectInteraction.CaptureReceipt(GuideAction.AttackOnce, 1) == receipt,
                    "the same practice resumes with the same receipt and no second enter");
                Check(!GameDirectInteraction.Succeeded, "legacy action receipt cannot skip the practical exercise");
                var draft = state.DeepClone();
                GameDirectInteraction.RecordSuccess(draft, GuideAction.AttackOnce, receipt);
                Check(!GameDirectInteraction.Succeeded, "an uncommitted action draft is not a success");
                stateField.SetValue(null, draft);
                Check(GameDirectInteraction.Succeeded && draft.Claims.Contains("game-direct-grant:guide_gacha:equipment_once"),
                    "only the committed current execution receipt counts and reward claims remain intact");
                Check(!GameDirectInteraction.CanPerform(GuideAction.AttackOnce, 1), "a committed practical action rejects another press");
                GameDirectInteraction.End();
                Check(!GameDirectInteraction.OwnsInput && GameDirectInteraction.CanPerform(GuideAction.AttackOnce, 10), "End releases input ownership");
                GameDirectInteraction.Begin("guide_development", step, generation, () => true);
                Check(!GameDirectInteraction.Succeeded, "a new execution does not reuse a prior local action receipt");
                GameDirectInteraction.Arm();
                var staleDraft = state.DeepClone();
                GameDirectInteraction.RecordSuccess(staleDraft, GuideAction.AttackOnce, receipt);
                Check(staleDraft.Modules["game-direct-action:guide_development:attack_once"] == "1",
                    "an old request cannot record success in a newly armed execution");
                GameDirectInteraction.End();
                foreach (var action in new[] { GuideAction.EquipmentPullOnce, GuideAction.SkillPullOnce })
                {
                    var gachaStep = new GameDirectStep(new GameDirectStepData
                    { id = "fixture_once", completion = GuideCompletion.Action, action = action }, 1, 1);
                    GameDirectInteraction.Begin("guide_gacha", gachaStep, generation, () => true);
                    Check(GameDirectInteraction.CaptureReceipt(action, 1) == null && GameDirectInteraction.CaptureReceipt(action, 10) == null,
                        "pending gacha requests have no practical receipt: " + action);
                    GameDirectInteraction.Arm();
                    Check(GameDirectInteraction.CaptureReceipt(action, 1) != null && GameDirectInteraction.CaptureReceipt(action, 10) == null,
                        "shown gacha practice captures only one pull: " + action);
                    GameDirectInteraction.End();
                }

                long oldNetwork = network.AccountGeneration;
                network.BeginAuthentication();
                typeof(NetworkManager).GetProperty("LoginState").SetValue(network, GameLoginState.Ready);
                Check(!GameDirectProgressStore.Available && !GameDirectProgressStore.AcceptSavedProgress(finished, finished, generation, oldNetwork),
                    "same-account reauthentication invalidates cached progress and late responses");
                var manager = owner.AddComponent<GameDirectManager>();
                var requestType = typeof(GameDirectManager).GetNestedType("Request", BindingFlags.NonPublic);
                object active = Activator.CreateInstance(requestType);
                requestType.GetField("AccountGeneration").SetValue(active, generation);
                requestType.GetField("NetworkGeneration").SetValue(active, oldNetwork);
                typeof(GameDirectManager).GetField("_activeRequest", instance).SetValue(manager, active);
                var cancellationField = typeof(GameDirectManager).GetField("_currentCancellation", instance);
                using (var cancellation = new CancellationTokenSource())
                {
                    cancellationField.SetValue(manager, cancellation);
                    typeof(GameDirectManager).GetMethod("Update", instance).Invoke(manager, null);
                    Check(cancellation.IsCancellationRequested, "reauthentication cancels an active tutorial save or retry wait");
                    cancellationField.SetValue(manager, null);
                }
                using (var cancellation = new CancellationTokenSource())
                {
                    cancellationField.SetValue(manager, cancellation);
                    typeof(GameDirectManager).GetMethod("OnDisable", instance).Invoke(manager, null);
                    Check(cancellation.IsCancellationRequested && manager.PendingCount == 0, "disabling the manager cancels the wait and clears queued guides");
                    cancellationField.SetValue(manager, null);
                }
                GameDirectProgressStore.ReceiveServerState(new List<TutorialProgressDTO> { finished });
                Check(store.TryLoad(finished.SequenceId, generation, out progress) && progress.Completed,
                    "fresh login snapshot restores completion without local migration");
                string beforeReset = JsonConvert.SerializeObject(LocalProgression.State);
                Scripts.Test.GameTest.ResetGuideTestRecords();
                Check(JsonConvert.SerializeObject(LocalProgression.State) == beforeReset,
                    "obsolete reset menu leaves game data and reward records unchanged");
                Check(typeof(NetworkManager).GetField("_context", instance).GetValue(network) == null,
                    "the entire fixture remained unauthenticated and sent no server request");
                return new { passed = true, count = checks.Count, checks };
            }
            finally
            {
                GameDirectInteraction.End();
                NetworkManager.Instance = previousNetwork;
                generationProperty.SetValue(null, generation);
                stateField.SetValue(null, previousState); openingField.SetValue(null, previousOpening);
                foreach (var saved in cache) saved.Key.SetValue(null, saved.Value);
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }
    }
}
#endif
