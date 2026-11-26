#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using KingdomIdle.Balance;
using KingdomIdle.Gacha;
using KingdomIdle.KingdomArmy;
using KingdomIdle.MageTower;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PlayFab;
using PlayFab.CloudScriptModels;
using Protocol.Message.Dto;
using Scripts.Core;
using Scripts.Core.Manager;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomIdle.UGUI.Editor
{
    public static class EconomyGrowthUiAcceptance
    {
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
        public static bool PauseAtCheckpoints { get; set; }
        public static bool WaitingForCapture { get; private set; }
        public static string CurrentCase { get; private set; }
        public static object LastRapidTapReport { get; private set; }
        public static object LastRecoveryLabelReport { get; private set; }
        private static bool _continue, _cancel, _running;
        public static void Continue() => _continue = true;
        public static void CancelRapidTaps() => _cancel = true;

        /// <summary>실제 육성 카드에서 복구·보류 동작 문구와 확인 전 미소비를 검사한다. 빈 PlayMode에서 실행한다.</summary>
        public static IEnumerator RunRecoveryLabels(Action<object> completed = null)
        {
            if (_running) throw new InvalidOperationException("Growth UI acceptance is already running.");
            _running = true; _cancel = false; LastRecoveryLabelReport = null;
            RapidTapFixture fixture = null;
            try
            {
                fixture = new RapidTapFixture(false);
                yield return WaitFor(fixture, () => EconomyBatchProgression.RecoveryComplete, "initial recovery");
                fixture.BuildPanel();
                yield return null;
                typeof(EconomyBatchProgression).GetField("_recovered", Static).SetValue(null, false);
                fixture.CheckRecoveryLabels("결과 확인", "확인 후 육성", false);
                typeof(EconomyBatchProgression).GetField("_recovered", Static).SetValue(null, true);
                fixture.Check(EconomyBatchProgression.TryQueueGold(true, 1, null, out _) == EconomyInputStatus.PendingSave &&
                    EconomyBatchProgression.RecoverPending(_ => { }, true), "A real saved growth input enters the existing held recovery path");
                yield return WaitFor(fixture, () => EconomyBatchProgression.RecoveryComplete && !EconomyBatchProgression.HasPendingWork, "held input persistence");
                fixture.Check(EconomyBatchProgression.HasHeldSpending, "The unsent input stays held before explicit confirmation");
                fixture.CheckRecoveryLabels("입력 확인", "수량·비용 확인", true);
                fixture.Check(fixture.Transport.Calls == 0 && fixture.Transport.Unexpected == 0,
                    "Recovery labels and cancelled confirmations never send HTTP");
                LastRecoveryLabelReport = fixture.Report(true);
                completed?.Invoke(LastRecoveryLabelReport);
            }
            finally
            {
                LastRecoveryLabelReport ??= fixture?.Report(false);
                fixture?.Dispose();
                _running = false; WaitingForCapture = false; CurrentCase = null; PauseAtCheckpoints = false;
            }
        }

        /// <summary>빈 PlayMode에서 코루틴으로 실행한다. 캡처 대기는 체크포인트마다 45초로 제한한다.</summary>
        public static IEnumerator RunRapidTaps(Action<object> completed = null) => RunTaps(false, completed);
        public static IEnumerator RunStorageRetry(Action<object> completed = null) => RunTaps(true, completed);

        public static object RunIndependentGoldFailure()
        {
            using var fixture = new RapidTapFixture(false, mage: true);
            fixture.Pump();
            fixture.Check(LocalProgression.Execute("qa-independent-growth", state =>
            {
                state.Economy.Server.IndependentGrowthFailures = true;
                return true;
            }), "The isolated server advertises independent growth failures");
            fixture.BuildPanel();
            var health = fixture.Body.CardsRoot.GetComponentsInChildren<EnhanceCardView>()
                .Single(card => card.nameLabel.text == StatEnhanceManager.GetTypeName(StatEnhanceManager.EnhanceType.MaxHP));
            var healthButton = health.ButtonRow.GetComponentInChildren<GachaPullButtonView>().Button;
            fixture.AttackButton.onClick.Invoke();
            fixture.AttackButton.onClick.Invoke();
            healthButton.onClick.Invoke();
            var projected = EconomyBatchProgression.GetProjection();
            fixture.Check(projected.AttackLevel == 2 && projected.HealthLevel == 1 &&
                LocalProgression.State.AttackLevel == 0 && LocalProgression.State.HealthLevel == 0,
                "The real attack and health buttons project individually before confirmation");
            EconomyBatchProgression.FlushBeforeWrite(_ => { });
            fixture.Pump(); fixture.Pump();
            fixture.AcknowledgeIndependentGoldFailure();
            fixture.Check(fixture.AttackCard.levelLabel.text == "Lv. 1" && health.levelLabel.text == "Lv. 1" &&
                LocalProgression.State.AttackLevel == 1 && LocalProgression.State.HealthLevel == 1 &&
                LocalProgression.Balance(eCurrency.Gold) == RapidTapFixture.InitialGold - 50 &&
                EconomyBatchProgression.PendingSpendingCount == 0 && !EconomyBatchProgression.HasUnknownResult,
                "The attack preview rolls back to the confirmed level and the health upgrade stays applied");
            return new { passed = fixture.Checks.Count, checks = fixture.Checks };
        }

        public static object RunQuestFundedPull()
        {
            var checks = new List<string>();
            foreach (var test in new[]
            {
                (Coins: 30L, Reward: eCurrency.AncientCoin, Amount: 20L, ClaimSucceeds: true, Failure: (string)null),
                (Coins: 30L, Reward: eCurrency.AncientCoin, Amount: 20L, ClaimSucceeds: false, Failure: "고대주화가 20개 부족합니다."),
                (Coins: 50L, Reward: eCurrency.AncientCoin, Amount: 20L, ClaimSucceeds: false, Failure: (string)null),
                (Coins: 50L, Reward: eCurrency.EquipmentTicket, Amount: 1L, ClaimSucceeds: false,
                    Failure: "네트워크가 불안정해 뽑기를 진행하지 못했습니다. 다시 시도해 주세요.")
            })
            {
                using var fixture = new RapidTapFixture(true);
                fixture.Pump();
                fixture.CheckQuestFundedPull(test.Coins, test.Reward, test.Amount, test.ClaimSucceeds, test.Failure);
                checks.AddRange(fixture.Checks);
            }
            return new { passed = checks.Count, checks };
        }

        public static object RunQuestFundedPromotion()
        {
            var checks = new List<string>();
            foreach (bool claimSucceeds in new[] { true, false })
            {
                using var fixture = new RapidTapFixture(true);
                fixture.Pump();
                fixture.CheckQuestFundedPromotion(claimSucceeds);
                checks.AddRange(fixture.Checks);
            }
            return new { passed = checks.Count, checks };
        }

        public static object RunQuestFundedGrowth()
        {
            var checks = new List<string>();
            foreach (bool mage in new[] { false, true })
            foreach (bool claimSucceeds in new[] { true, false })
            {
                using var fixture = new RapidTapFixture(true, mage: mage);
                fixture.Pump();
                fixture.CheckQuestFundedGrowth(mage, claimSucceeds);
                checks.AddRange(fixture.Checks);
            }
            return new { passed = checks.Count, checks };
        }

        public static object RunQuestSnapshotAdmission()
        {
            var checks = new List<string>();
            using (var fixture = new RapidTapFixture(true, activeNetwork: true))
            {
                fixture.Pump();
                fixture.CheckQuestSnapshotAdmission();
                checks.AddRange(fixture.Checks);
            }
            using (var fixture = new RapidTapFixture(true, activeNetwork: true))
            {
                fixture.Pump();
                fixture.CheckQuestSnapshotTimeout();
                checks.AddRange(fixture.Checks);
            }
            return new { passed = checks.Count, checks };
        }

        public static object RunMageAwakening()
        {
            using var fixture = new RapidTapFixture(false, mage: true);
            fixture.Pump();
            fixture.CheckMageAwakening();
            return new { passed = fixture.Checks.Count, checks = fixture.Checks };
        }

        public static object RunMageEnhance()
        {
            var checks = new List<string>();
            foreach (bool supported in new[] { true, false })
            {
                using var fixture = new RapidTapFixture(false, mage: true);
                fixture.Pump();
                fixture.Check(EconomyBatchProgression.RecoveryComplete, "Isolated economy session is ready");
                var view = fixture.OpenMageDetail(supported);
                var mage = MageTowerManager.Instance;
                long damage = mage.GetEffectiveDamage(0);
                string shownDamage = view.lblEffDmg.text;
                fixture.Check(view.btnEnhance.interactable, "Owned skill has enough knowledge for its first enhancement");
                if (supported)
                {
                    view.btnEnhance.onClick.Invoke();
                    fixture.Check(view.btnEnhance.interactable && !mage.IsBusy, "First click leaves ordinary enhancement available");
                    view.btnEnhance.onClick.Invoke();
                    var projection = EconomyBatchProgression.GetProjection();
                    fixture.Check(projection.Valid && projection.Level(EconomyCommandKind.MageEnhance, 0) == 2 &&
                        projection.ArcaneKnowledge == 0 && projection.Level(EconomyCommandKind.MageEnhance, 3) == 4 &&
                        view.lblEnhLevel.text == "강화 레벨: 2 / 100" && view.lblEnhCost.text.Contains("보유 0") && !view.btnEnhance.interactable,
                        "Two clicks update only the selected skill and exhaust the projected balance");
                    fixture.Check(view.lblEffDmg.text == shownDamage && mage.GetEffectiveDamage(0) == damage && mage.GetEnhanceLevel(0) == 0 &&
                        mage.GetTotalAKSpent(0) == 0 && LocalProgression.Balance(eCurrency.ArcaneKnowledge) == 21,
                        "Displayed damage, combat and refundable spending remain confirmed while two inputs are pending");
                    EconomyBatchProgression.FlushLocalWrites();
                    var saved = new EconomyInputJournal(LocalProgression.EconomyJournalPath, LocalProgression.AccountKey).Data.Inputs;
                    fixture.Check(saved.Count == 2 && saved.All(x => x.Command.Kind == EconomyCommandKind.MageEnhance && x.Command.SkillId == 0) &&
                        saved.Select(x => x.Command.ExpectedLevel).SequenceEqual(new[] { 0, 1 }) &&
                        saved.Select(x => x.Command.SpendLimit).SequenceEqual(new long[] { 10, 11 }),
                        "Durable inputs retain the skill, expected level and exact price of each click");
                    fixture.AcknowledgeMageWithThrowingObserver();
                    fixture.Check(mage.GetEnhanceLevel(0) == 2 && mage.GetEffectiveDamage(0) > damage && view.lblEffDmg.text != shownDamage &&
                        mage.GetTotalAKSpent(0) == 21 && mage.GetResetRefund(0) == 16 && view.btnReset.interactable &&
                        LocalProgression.Balance(eCurrency.ArcaneKnowledge) == 0 && mage.GetEnhanceLevel(3) == 4,
                        "Confirmed damage, refund and unchanged second skill agree with the saved server result");
                }
                else
                {
                    // Hold the shared write fence so the legacy request can be observed without dispatching it.
                    EconomyBatchProgression.SetExternalWritePending(true);
                    view.btnEnhance.onClick.Invoke();
                    fixture.Check(mage.IsBusy && !view.btnEnhance.interactable && view.btnEnhanceLabel.text == "요청 중" &&
                        mage.TryEnhanceEx(0) == MageTowerManager.MageActionResult.Busy && EconomyBatchProgression.PendingSpendingCount == 0,
                        "A server without mage batching uses the fenced single request and blocks a second click");
                }
                fixture.Check(fixture.Transport.Calls == (supported ? 1 : 0) && fixture.Transport.Unexpected == 0,
                    "Only the supported two-click batch reaches the isolated HTTP transport");
                checks.AddRange(fixture.Checks);
            }
            using (var fixture = new RapidTapFixture(false, mage: true))
            {
                fixture.Pump();
                var view = fixture.OpenMageDetail(false);
                LocalProgression.Execute("qa-mage-independent-gold", state =>
                { state.Economy.Server.MageEnhanceMode = MageEnhanceBatchMode.RecoveryOnly; return true; });
                fixture.Check(EconomyBatchProgression.TryQueueGold(true, 1, null, out _) == EconomyInputStatus.PendingSave,
                    "Independent gold input is accepted");
                EconomyBatchProgression.FlushBeforeWrite(_ => { });
                fixture.Pump(); fixture.Pump();
                fixture.Check(fixture.Transport.Pending && fixture.Transport.Request?.Commands.Single().Kind == EconomyCommandKind.GoldAttack &&
                    view.btnEnhance.interactable, "An independent gold batch leaves the single mage button available");
                view.btnEnhance.onClick.Invoke();
                fixture.Check(MageTowerManager.Instance.IsBusy && fixture.Transport.Calls == 1,
                    "Single mage click waits for the existing gold call");
                fixture.Acknowledge();
                fixture.Pump(); fixture.Pump();
                fixture.Check(fixture.Transport.Single != null && fixture.Transport.Calls == 2 && fixture.Transport.Unexpected == 0,
                    "Gold confirmation releases exactly one single mage request");
                checks.AddRange(fixture.Checks);
            }
            return new { passed = checks.Count, checks };
        }

        // The release harness runs both server binaries with the button's original request and returns their wire responses.
        public static object RunMageRollback(JObject login, Action<OnEconomyBatchRequestDTO> send,
            Func<OnGetEconomyBatchResultRequestDTO, JObject> recover, Func<OnEnchantSkillRequestDTO, JObject> single,
            Func<JObject> restartState = null)
        {
            var active = login["EconomyBatchState"].ToObject<EconomyBatchStateDTO>();
            using var fixture = new RapidTapFixture(false, mage: true, clientId: active.OwnerClientId);
            NetworkManager.Instance.SetGameSession((string)login["SessionID"]);
            fixture.Pump();
            var view = fixture.OpenMageDetail(false);
            fixture.Check(LocalProgression.Execute("qa-mage-cutover-seed", state =>
            {
                state.Economy.Server = active;
                state.ServerEconomyRevision = (long)login["EconomyRevision"];
                state.Wallet[eCurrency.ArcaneKnowledge] = (long)login["Currency"]["ArcaneKnowledge"];
                return true;
            }) && EconomyBatchProgression.SupportsMageEnhance, "Active server enables the batch button");
            view.btnEnhance.onClick.Invoke();
            EconomyBatchProgression.FlushBeforeWrite(_ => { });
            fixture.Pump(); fixture.Pump();
            var http = fixture.Transport;
            var request = http.Request;
            fixture.Check(http.Pending && http.Calls == 1 && request?.Commands.Single().Kind == EconomyCommandKind.MageEnhance,
                "First button click sends one mage command to the active server");
            send(request);
            var lostReply = http.TakePending();
            Set(NetworkManager.Instance, "_callStartedAt", Time.realtimeSinceStartup - 31f);
            Invoke(NetworkManager.Instance, "Update");
            fixture.Check(EconomyBatchProgression.HasUnknownResult && MageTowerManager.Instance.GetEnhanceLevel(0) == 0,
                "Request timeout leaves the old result unconfirmed");
            if (restartState != null)
            {
                var journal = typeof(EconomyBatchProgression).GetField("_journal", Static);
                var previous = (EconomyInputJournal)journal.GetValue(null);
                string frozen = JsonConvert.SerializeObject(previous.Data.InFlight);
                LocalProgression.Open(LocalProgression.AccountKey);
                fixture.Check(journal.GetValue(null) == null && !EconomyBatchProgression.RecoveryComplete,
                    "Reopening the same account drops the live journal and recovery flags");
                var startup = restartState().ToObject<OnGetUserStateResponseDTO>();
                fixture.Check(startup.EconomyBatchState.OwnerClientId == active.OwnerClientId &&
                    startup.EconomyBatchState.MageEnhanceMode == MageEnhanceBatchMode.RecoveryOnly &&
                    UserManager.Instance.ApplyServerPlayerData(startup), "Same owner receives the compatibility startup state");
                var restored = (EconomyInputJournal)journal.GetValue(null);
                fixture.Check(!ReferenceEquals(previous, restored) && frozen == JsonConvert.SerializeObject(restored.Data.InFlight) &&
                    restored.Data.InFlight.RequestId == request.RequestId && EconomyInputJournal.Hash(restored.Data.InFlight) == EconomyInputJournal.Hash(request) &&
                    EconomyBatchProgression.HasUnresolvedBatch && !EconomyBatchProgression.CanStartSingleMageEnhance,
                    "Restart reloads the frozen request body, original ID and hash from disk before allowing any single write");
                fixture.Check(EconomyBatchProgression.RecoverPending(_ => { }, previousSessionReplaced: true),
                    "Startup recovery accepts the restored journal");
            }
            else typeof(EconomyBatchProgression).GetField("_retryAt", Static).SetValue(null, 0f);
            fixture.Pump();
            fixture.Check(http.Pending && http.Calls == 2 && http.Lookup?.RequestId == request.RequestId &&
                http.Lookup.Generation == request.Generation && http.Lookup.Sequence == request.Sequence, "Lookup uses the original batch identity");
            var wire = recover(http.Lookup);
            http.Succeed(wire["lookup"]);
            fixture.Pump();
            var mage = MageTowerManager.Instance;
            fixture.Check(fixture.SuccessfulInputs == 1 && EconomyBatchProgression.RecoveryComplete && !EconomyBatchProgression.HasUnknownResult &&
                !EconomyBatchProgression.SupportsMageEnhance && LocalProgression.State.Economy.LastAppliedRequestId == request.RequestId &&
                mage.GetEnhanceLevel(0) == 1 && mage.GetTotalAKSpent(0) == 10 && LocalProgression.Balance(eCurrency.ArcaneKnowledge) == 90 &&
                view.btnEnhance.interactable, "Compatibility receipt confirms one enhancement and enables the next click");
            view.btnEnhance.onClick.Invoke();
            fixture.Pump();
            fixture.Check(http.Pending && http.Calls == 3 && http.Single?.SkillCode.GetSkillId() == 0 && mage.IsBusy &&
                EconomyBatchProgression.PendingSpendingCount == 0, "Next click issues one OnEnChantSkill request");
            http.Succeed(single(http.Single)["single"]);
            fixture.Pump();
            lostReply.ApiResult = new ExecuteFunctionResult { FunctionResult = wire["activeResponseDiscarded"].ToString() };
            lostReply.InvokeSuccessCallback();
            fixture.Pump();
            fixture.Check(!mage.IsBusy && !http.Pending && http.Calls == 3 && http.Unexpected == 0 && fixture.SuccessfulInputs == 1 &&
                !EconomyBatchProgression.SupportsMageEnhance && mage.GetEnhanceLevel(0) == 2 &&
                mage.GetTotalAKSpent(0) == 21 && LocalProgression.Balance(eCurrency.ArcaneKnowledge) == 79,
                "Single response confirms level 2 with 79 knowledge; a late original response cannot reapply the batch");
            return new { passed = fixture.Checks.Count, checks = fixture.Checks, requestId = request.RequestId };
        }

        private static IEnumerator RunTaps(bool throttle, Action<object> completed)
        {
            if (_running) throw new InvalidOperationException("Rapid tap acceptance is already running.");
            _running = true; _cancel = false; LastRapidTapReport = null;
            RapidTapFixture fixture = null;
            try
            {
                fixture = new RapidTapFixture(throttle);
                yield return WaitFor(fixture, () => EconomyBatchProgression.RecoveryComplete, "initial recovery");
                fixture.BuildPanel();
                yield return null;
                fixture.Check(fixture.AttackButton.interactable, "Production attack button is ready before the first tap");
                yield return Capture("before-rapid-taps");

                fixture.AttackButton.onClick.Invoke();
                yield return WaitFor(fixture, () => fixture.Transport.Pending, "first HTTP request");
                fixture.Check(fixture.Transport.Request.Commands.Count == 1 && LocalProgression.State.AttackLevel == 0,
                    "First tap reaches isolated HTTP without changing confirmed attack");
                string firstId = fixture.Transport.Request.RequestId;
                string firstPayload = EconomyInputJournal.Hash(fixture.Transport.Request);
                float limitedAt = 0;
                if (throttle)
                {
                    limitedAt = Time.realtimeSinceStartup;
                    fixture.Transport.Throttle();
                    fixture.Check(fixture.AttackButton.interactable && fixture.Body.descLabel.text.Contains("서버 확인 중"),
                        "Storage 429 shows confirmation status without replacing the production attack button");
                }
                var immediate = System.Diagnostics.Stopwatch.StartNew();
                for (int i = 1; i < 10; i++)
                {
                    fixture.Check(fixture.AttackButton.interactable, "Attack tap " + (i + 1) + " remains enabled during the in-flight batch");
                    fixture.AttackButton.onClick.Invoke();
                }
                immediate.Stop();
                fixture.ImmediateNineTapMilliseconds = immediate.Elapsed.TotalMilliseconds;
                long expectedGold = RapidTapFixture.InitialGold - BalanceMath.GoldTotal(0, 10);
                var projection = EconomyBatchProgression.GetProjection();
                fixture.Check(projection.Valid && projection.AttackLevel == 10 && projection.Gold == expectedGold &&
                    LocalProgression.State.AttackLevel == 0 && LocalProgression.Balance(eCurrency.Gold) == RapidTapFixture.InitialGold &&
                    fixture.Enhance.GetBonusAtkRate() == 0, "Nine queued taps update the projection immediately while confirmed balance and combat bonus stay unchanged");
                fixture.Check(fixture.AttackCard.levelLabel.text == "Lv. 10" &&
                    fixture.AttackCard.bonusLabel.text == $"×{BalanceMath.GoldMultiplier(10):0.###}  →  ×{BalanceMath.GoldMultiplier(11):0.###}" &&
                    fixture.Body.goldLabel.text == $"보유 골드  {NumberNotation.Format(expectedGold)} G" &&
                    !fixture.Body.descLabel.text.Contains("예상") && fixture.Transport.Calls == 1,
                    "Growth labels display projected values using the normal format before confirmation");
                yield return Capture("projected-ten-taps-before-ack");
                fixture.TopGold.ForceMeshUpdate();
                fixture.Check(fixture.TopGold.text == NumberNotation.Format(expectedGold) && !fixture.TopGold.isTextTruncated &&
                    fixture.ReadPopupGold() == NumberNotation.Exact(expectedGold),
                    "Projected gold uses the normal number format in both the top HUD and currency popup");

                if (throttle)
                {
                    while (Time.realtimeSinceStartup < limitedAt + 1.8f)
                    {
                        fixture.Pump();
                        fixture.Check(fixture.Transport.Calls == 1, "No early recovery call before Retry-After", log: false);
                        yield return null;
                    }
                    yield return WaitFor(fixture, () => fixture.Transport.Pending, "same ID lookup after Retry-After");
                    fixture.Check(fixture.Transport.Lookup?.RequestId == firstId && fixture.Transport.Calls == 2 &&
                        fixture.Transport.LastSendAt >= limitedAt + 2f, "First recovery call looks up the same ID only after Retry-After");
                    fixture.ReplyNotFound();
                    yield return WaitFor(fixture, () => fixture.Transport.Pending, "same request resend");
                    fixture.Check(fixture.Transport.Lookup == null && fixture.Transport.Request.RequestId == firstId &&
                        EconomyInputJournal.Hash(fixture.Transport.Request) == firstPayload && fixture.Transport.Request.Commands.Count == 1,
                        "NotFound resends the original request without appending the nine later taps");
                }
                for (int batch = 0; batch < 2; batch++)
                {
                    yield return WaitFor(fixture, () => fixture.Transport.Pending, "batch " + (batch + 1));
                    float delayUntil = Time.realtimeSinceStartup + 1f;
                    while (Time.realtimeSinceStartup < delayUntil) { CheckCancelled(); yield return null; }
                    fixture.Acknowledge();
                    fixture.Check(LocalProgression.State.AttackLevel == (batch == 0 ? 1 : 10),
                        "Batch " + (batch + 1) + " applies only its durable server-confirmed prefix");
                }
                yield return WaitFor(fixture, () => !EconomyBatchProgression.HasPendingWork, "journal completion");
                var saved = JsonConvert.DeserializeObject<ProgressionState>(File.ReadAllText(LocalProgression.SnapshotPath));
                fixture.Check(saved.AttackLevel == 10 && saved.Wallet[eCurrency.Gold] == expectedGold &&
                    saved.Economy.LastAppliedRequestId == fixture.LastRequestId && fixture.SuccessfulInputs == 10,
                    "Disk snapshot and ten completion callbacks agree after the two acknowledgements");
                fixture.Check(fixture.AttackCard.levelLabel.text == "Lv. 10" && fixture.Body.goldLabel.text == $"보유 골드  {NumberNotation.Format(expectedGold)} G" &&
                    fixture.TopGold.text == NumberNotation.Format(expectedGold) && fixture.ReadPopupGold() == NumberNotation.Exact(expectedGold) &&
                    fixture.Enhance.GetBonusAtkRate() > 0 && fixture.AttackButton.interactable,
                    "Labels keep their format after confirmation and the acknowledged combat bonus takes effect");
                if (throttle)
                    fixture.Check(QuestManager.Instance.ServerQuests?.DailyPeriodKey == "2026-11-19" &&
                        !(bool)typeof(QuestManager).GetField("_refreshQueued", Instance).GetValue(QuestManager.Instance),
                        "Changed rows and unchanged acknowledgements need no separate quest read");
                for (int i = 0; i < 5; i++) { fixture.Pump(); yield return null; }
                fixture.Check(fixture.Transport.Calls == (throttle ? 4 : 2) && fixture.Transport.Unexpected == 0 && !fixture.Transport.Pending &&
                    fixture.SuccessfulInputs == 10, "Draining the queue does not resend or apply completed taps twice");
                yield return Capture("confirmed-after-two-batches");
                if (throttle) fixture.CheckHeldQuestRefresh();
                LastRapidTapReport = fixture.Report(true);
                completed?.Invoke(LastRapidTapReport);
            }
            finally
            {
                LastRapidTapReport ??= fixture?.Report(false);
                fixture?.Dispose();
                _running = false; WaitingForCapture = false; CurrentCase = null; PauseAtCheckpoints = false;
            }
        }

        private static IEnumerator WaitFor(RapidTapFixture fixture, Func<bool> condition, string name)
        {
            float deadline = Time.realtimeSinceStartup + 5f;
            while (!condition())
            {
                CheckCancelled(); fixture.Pump();
                if (Time.realtimeSinceStartup > deadline) throw new TimeoutException(name + ": " + EconomyBatchProgression.Status);
                yield return null;
            }
        }

        private static IEnumerator Capture(string name)
        {
            CurrentCase = name; _continue = false; WaitingForCapture = true;
            Canvas.ForceUpdateCanvases();
            yield return null;
            float deadline = Time.realtimeSinceStartup + 45f;
            while (PauseAtCheckpoints && !_continue)
            {
                CheckCancelled();
                if (Time.realtimeSinceStartup > deadline) throw new TimeoutException("Capture checkpoint expired: " + name);
                yield return null;
            }
            WaitingForCapture = false;
        }

        private static void CheckCancelled()
        { if (_cancel) throw new OperationCanceledException("Rapid tap acceptance cancelled."); }

        private sealed class RapidTapFixture : IDisposable
        {
            internal const long InitialGold = 100000;
            internal readonly List<string> Checks = new();
            internal readonly List<double> CallbackMilliseconds = new();
            internal readonly IsolatedGrowthHttp Transport = new();
            internal double ImmediateNineTapMilliseconds;
            internal int SuccessfulInputs;
            internal string LastRequestId;
            internal StatEnhanceManager Enhance;
            internal EnhanceCardView AttackCard;
            internal GachaPullButtonView AttackPull;
            internal UnityEngine.UI.Button AttackButton => AttackPull.Button;
            internal DevelopmentBodyView Body;
            internal TMP_Text TopGold;
            private readonly ITransportPlugin _previousTransport = PluginManager.GetPlugin<ITransportPlugin>(PluginContract.PlayFab_Transport);
            private readonly object _previousClientId = typeof(EconomyBatchProgression).GetField("_clientId", Static).GetValue(null);
            private readonly object _previousCount = typeof(DevelopmentPanelController).GetField("_selectedCount", Static).GetValue(null);
            private readonly object _previousRubyTab = typeof(DevelopmentPanelController).GetField("_rubyTab", Static).GetValue(null);
            private readonly object _previousQuests = typeof(QuestManager).GetField("_received", Static).GetValue(null);
            private readonly object _previousQuestSource = typeof(QuestManager).GetField("_receivedSource", Static).GetValue(null);
            private readonly Type _walletType = typeof(EconomyBridge).Assembly.GetType("Scripts.Wallets.Wallet");
            private readonly bool _previousWalletSubscription;
            private readonly Dictionary<eCurrency, long> _previousPublished;
            private IDisposable _account;
            private GameObject _owner, _canvas, _activeNetwork;
            private UIManager _ui;
            private DevelopmentPanelView _panel;
            private MainScreenController _main;
            private UIViewCatalog _catalog;
            private QuestManager _quests;
            private QuestRowSnapshot _pendingQuest;
            private QuestBoardSnapshot _guideBoard;
            private MageTowerManager _mage;
            private bool _disposed;

            internal RapidTapFixture(bool throttle, bool mage = false, string clientId = null, bool activeNetwork = false)
            {
                if ((!Application.isPlaying && !mage) || LocalProgression.IsReady || NetworkManager.Instance != null || UserManager.Instance != null ||
                    StatEnhanceManager.Instance != null || UIManager.Instance != null || StageManager.Instance != null ||
                    QuestManager.Instance != null || (mage && MageTowerManager.Instance != null) || ModalBackHandler.HasOpenModal)
                    throw new InvalidOperationException("Rapid tap acceptance requires empty signed-out PlayMode.");
                _previousWalletSubscription = (bool)_walletType.GetField("_subscribed", Static).GetValue(null);
                _previousPublished = new((Dictionary<eCurrency, long>)_walletType.GetField("Published", Static).GetValue(null));
                try
                {
                    PluginManager.SetPlugin(Transport, PluginContract.PlayFab_Transport);
                    typeof(EconomyBatchProgression).GetField("_clientId", Static).SetValue(null, clientId ?? Guid.NewGuid().ToString("N"));
                    _account = LocalProgression.BeginTestSession();
                    LocalProgression.OpenTestAccount("economy-rapid-ui-" + Guid.NewGuid().ToString("N"));
                    _owner = new GameObject("EconomyRapidTapAcceptance_Managers"); _owner.SetActive(false);
                    if (activeNetwork) _activeNetwork = new GameObject("EconomyRapidTapAcceptance_ActiveNetwork");
                    var network = (_activeNetwork ?? _owner).AddComponent<NetworkManager>();
                    if (!activeNetwork) { NetworkManager.Instance = network; Invoke(network, "Init"); }
                    Set(network, "_sessionGUID", "economy-rapid-ui-fixture");
                    Set(network, "_context", new PlayFabAuthenticationContext { EntityToken = "economy-rapid-ui-fixture" });
                    typeof(NetworkManager).GetProperty("LoginState").SetValue(network, GameLoginState.Ready);
                    var users = _owner.AddComponent<UserManager>(); UserManager.Instance = users; Invoke(users, "Init");
                    Enhance = _owner.AddComponent<StatEnhanceManager>(); Invoke(Enhance, "Awake");
                    if (throttle)
                    {
                        QuestManager.Instance = _quests = _owner.AddComponent<QuestManager>();
                        _quests.Apply(new QuestStateDTO
                        {
                            Version = "board-0", DailyPeriodKey = "2026-11-19", WeeklyPeriodKey = "2026-11-16", Rows = new()
                            {
                                QuestRow("2026-11-18", 10, true), QuestRow("2026-11-19", 0, false)
                            }
                        }, "fixture");
                        _pendingQuest = _quests.GetSnapshot(eQuestCategory.Daily).Rows[0];
                        _guideBoard = _quests.GetSnapshot(eQuestCategory.Guide);
                        Invoke(_quests, "LateUpdate");
                    }
                    Check(LocalProgression.Execute("qa-rapid-ui-seed", state =>
                    {
                        state.Wallet[eCurrency.Gold] = InitialGold; state.AttackLevel = state.HealthLevel = 0;
                        state.ServerEconomyRevision = 1; state.CycleFrontierStage = state.MainStage;
                        state.Economy.Server = new EconomyBatchStateDTO
                        {
                            Version = 1, OwnerClientId = EconomyBatchProgression.ClientId, Generation = 1, NextSequence = 1,
                            RewardEpoch = 1, RewardEpochs = new() { new() { Id = 1 } }
                        };
                        return true;
                    }), "Isolated initial balance and batch metadata are durably saved");
                    EconomyBatchProgression.ReceiveServerState(true);
                    Check(EconomyBatchProgression.RecoverPending(_ => { }, true), "Production startup recovery accepts the isolated account");
                    EconomyBatchProgression.InputCompleted += OnInputCompleted;
                    _catalog = AssetDatabase.LoadAssetAtPath<UIViewCatalog>("Assets/UGUI/UIViewCatalog.asset");
                    Check(_catalog?.panelDevelopment != null && _catalog.screenMain != null, "Production development panel and main screen are available");
                    _ui = _owner.AddComponent<UIManager>(); Set(_ui, "catalog", _catalog); SetUi(_ui);
                    _canvas = new GameObject("EconomyRapidTapAcceptance_Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                    _canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                    var scaler = _canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    scaler.referenceResolution = new Vector2(1080, 1920); scaler.matchWidthOrHeight = .5f;
                    Set(_ui, "layerOverlays", _canvas.transform);
                }
                catch { Dispose(); throw; }
            }

            internal MageTowerDetailPopupView OpenMageDetail(bool supported)
            {
                Check(LocalProgression.Execute("qa-mage-rapid-ui", state =>
                {
                    state.Economy.Server.SupportsMageEnhance = supported;
                    state.Wallet[eCurrency.ArcaneKnowledge] = 21;
                    state.MageSkills[0] = new(); state.MageSkills[3] = new() { Enhance = 4 };
                    return true;
                }), "Owned skills and capability are saved in the isolated account");
                _mage = _owner.AddComponent<MageTowerManager>();
                Set(_mage, "skillRegistry", AssetDatabase.LoadAssetAtPath<MageTowerSkillRegistrySO>("Assets/MageTower/SO/MageTowerSkillList.asset"));
                Invoke(_mage, "Awake");
                MageTowerDetailPopupController.Show(0);
                return _canvas.GetComponentInChildren<MageTowerDetailPopupView>();
            }

            internal void BuildPanel()
            {
                var screen = UnityEngine.Object.Instantiate(_catalog.screenMain, _canvas.transform, false).GetComponent<MainScreenView>();
                TopGold = screen.lblGold;
                _main = new MainScreenController(); Set(_main, "_view", screen); Set(_main, "_host", _ui); Invoke(_main, "BindCurrency");
                _panel = UnityEngine.Object.Instantiate(_catalog.panelDevelopment, _canvas.transform, false).GetComponent<DevelopmentPanelView>();
                _panel.SetTitle("육성");
                DevelopmentPanelController.PrepareGuide(); DevelopmentPanelController.Populate(_panel);
                Body = _panel.GetComponentInChildren<DevelopmentBodyView>();
                AttackCard = Body.CardsRoot.GetComponentsInChildren<EnhanceCardView>().First();
                AttackPull = AttackCard.GetComponentInChildren<GachaPullButtonView>();
            }

            internal void Pump() { EconomyBatchProgression.FlushLocalWrites(); EconomyBatchProgression.Pump(); }
            internal string ReadPopupGold()
            {
                Refresh(_main);
                var lines = Get<Dictionary<eCurrency, CurrencyLineItemView>>(_main, "_currencyLines");
                return Get<TMP_Text>(lines[eCurrency.Gold], "valueLabel").text;
            }

            internal void CheckRecoveryLabels(string title, string cost, bool checkConfirmation)
            {
                string before = JsonConvert.SerializeObject(LocalProgression.State);
                for (int tab = 0; tab < 2; tab++)
                {
                    typeof(DevelopmentPanelController).GetField("_rubyTab", Static).SetValue(null, tab == 1);
                    typeof(DevelopmentPanelController).GetMethod("Refresh", Static).Invoke(null, null);
                    var root = tab == 1 ? Body.rubyCardsRoot : Body.CardsRoot;
                    if (tab == 1) Invoke(root.GetComponent<RubyGrowthCards>(), "Refresh");
                    LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)_panel.transform);
                    Canvas.ForceUpdateCanvases();
                    var buttons = root.GetComponentsInChildren<GachaPullButtonView>();
                    Check(buttons.Length == 2, (tab == 1 ? "Ruby" : "Gold") + " has two production recovery actions");
                    foreach (var button in buttons)
                    {
                        var titleLabel = Get<TMP_Text>(button, "titleLabel");
                        var costLabel = Get<TMP_Text>(button, "costLabel");
                        foreach (var label in new[] { titleLabel, costLabel })
                        {
                            label.ForceMeshUpdate();
                            float width = label.GetPreferredValues(label.text, float.PositiveInfinity, float.PositiveInfinity).x;
                            Check(!label.isTextTruncated && label.textInfo.lineCount == 1 && width <= label.rectTransform.rect.width + .5f,
                                $"{(tab == 1 ? "Ruby" : "Gold")} '{label.text}' fits without ellipsis: preferred={width:0.##}, available={label.rectTransform.rect.width:0.##}");
                        }
                        Check(titleLabel.text == title && costLabel.text == cost && button.Button.interactable,
                            (tab == 1 ? "Ruby" : "Gold") + " keeps the explicit recovery action and enabled input");
                        if (!checkConfirmation) continue;
                        button.Button.onClick.Invoke();
                        var dialog = _canvas.GetComponentsInChildren<EquipmentActionDialog>().Single();
                        Check(dialog.gameObject.activeInHierarchy && EconomyBatchProgression.HasHeldSpending &&
                            JsonConvert.SerializeObject(LocalProgression.State) == before,
                            "Clicking input confirmation preserves held inputs and confirmed balances");
                        dialog.cancel.onClick.Invoke();
                        Check(EconomyBatchProgression.HasHeldSpending && JsonConvert.SerializeObject(LocalProgression.State) == before,
                            "Cancelling confirmation keeps the same held input without spending");
                    }
                }
            }
            internal void Check(bool condition, string message, bool log = true)
            { if (!condition) throw new InvalidOperationException("Rapid tap UI: " + message); if (log) Checks.Add(message); }
            private int FailedInputs;
            private void OnInputCompleted(EconomyInputCompletion result)
            { if (result.Success) SuccessfulInputs++; else FailedInputs++; }

            internal void AcknowledgeMageWithThrowingObserver()
            {
                bool flushed = false;
                Check(EconomyBatchProgression.FlushBeforeWrite(ok => flushed = ok), "A write fence waits for both mage inputs");
                Pump(); Pump();
                var request = Transport.Request;
                Check(Transport.Pending && request?.Commands.Count == 2, "Both clicks reached one isolated HTTP request");
                var state = LocalProgression.State;
                var receipt = new EconomyBatchReceiptDTO
                {
                    RequestId = request.RequestId, Generation = request.Generation, Sequence = request.Sequence,
                    PayloadHash = EconomyInputJournal.Hash(request), CommittedRevision = state.ServerEconomyRevision + 1,
                    Results = new()
                    {
                        new() { Index = 0, Kind = EconomyCommandKind.MageEnhance, Status = EconomyCommandStatus.Applied, AppliedCount = 1, SpentArcaneKnowledge = 10 },
                        new() { Index = 1, Kind = EconomyCommandKind.MageEnhance, Status = EconomyCommandStatus.Applied, AppliedCount = 1, SpentArcaneKnowledge = 11 }
                    }
                };
                var metadata = EconomyCopies.Copy(state.Economy.Server);
                metadata.NextSequence = request.Sequence + 1; metadata.PendingReceipt = receipt;
                var response = Response(request.RequestId, state.AttackLevel, LocalProgression.Balance(eCurrency.Gold), metadata, receipt);
                response.CurrentState.MageTowerState = new()
                {
                    Skills = new() { new() { SkillId = 0, EnhancementLevel = 2, KnowledgeSpent = 21 }, new() { SkillId = 3, EnhancementLevel = 4 } },
                    EquippedSkillIds = state.MageSlots.ToArray()
                };
                int faultyCalls = 0, observedLevel = -1;
                void FaultyObserver() { faultyCalls++; throw new InvalidOperationException("Expected mage acceptance observer failure"); }
                void LaterObserver() => observedLevel = _mage.GetEnhanceLevel(0);
                _mage.OnStateChanged += FaultyObserver;
                _mage.OnStateChanged += LaterObserver;
                try { Transport.Succeed(response); }
                finally { _mage.OnStateChanged -= FaultyObserver; _mage.OnStateChanged -= LaterObserver; }
                Pump();
                var journal = new EconomyInputJournal(LocalProgression.EconomyJournalPath, LocalProgression.AccountKey).Data;
                Check(faultyCalls == 1 && observedLevel == 2 && SuccessfulInputs == 2 && flushed && !EconomyBatchProgression.HasUnknownResult &&
                    journal.InFlight == null && journal.Inputs.Count == 0 && LocalProgression.State.Economy.LastAppliedRequestId == request.RequestId,
                    "A throwing mage subscriber leaves the next subscriber, both completion callbacks, durable journal and write fence intact");
            }

            internal void AcknowledgeIndependentGoldFailure()
            {
                var request = Transport.Request;
                Check(Transport.Pending && Transport.Calls == 1 && request.IndependentGrowthFailures &&
                    request.Commands.Select(command => command.Kind).SequenceEqual(new[]
                    { EconomyCommandKind.GoldAttack, EconomyCommandKind.GoldAttack, EconomyCommandKind.GoldHealth }),
                    "Two attack clicks and one health click reach one versioned batch");
                var state = LocalProgression.State;
                var receipt = new EconomyBatchReceiptDTO
                {
                    RequestId = request.RequestId, Generation = request.Generation, Sequence = request.Sequence,
                    PayloadHash = EconomyInputJournal.Hash(request), CommittedRevision = state.ServerEconomyRevision + 1,
                    IndependentGrowthFailures = true,
                    Results = new()
                    {
                        new() { Index = 0, Kind = EconomyCommandKind.GoldAttack, Status = EconomyCommandStatus.Rejected,
                            ErrorCode = Protocol.Message.Data.eErrorCode.EconomyExpectedLevelMismatch },
                        new() { Index = 1, Kind = EconomyCommandKind.GoldAttack, Status = EconomyCommandStatus.Unprocessed },
                        new() { Index = 2, Kind = EconomyCommandKind.GoldHealth, Status = EconomyCommandStatus.Applied, AppliedCount = 1, SpentGold = 50 }
                    }
                };
                var metadata = EconomyCopies.Copy(state.Economy.Server);
                metadata.NextSequence = request.Sequence + 1;
                metadata.PendingReceipt = receipt;
                var response = Response(request.RequestId, 1, InitialGold - 50, metadata, receipt);
                response.CurrentState.EnchantHPCount = 1;
                var completed = new List<EconomyInputCompletion>();
                EconomyBatchProgression.InputCompleted -= OnInputCompleted;
                EconomyBatchProgression.InputCompleted += completed.Add;
                try { Transport.Succeed(response); Pump(); }
                finally { EconomyBatchProgression.InputCompleted -= completed.Add; }
                Check(completed.Count == 3 && completed.Count(x => x.Success && x.Kind == EconomyCommandKind.GoldHealth) == 1 &&
                    completed.Count(x => !x.Success && x.Kind == EconomyCommandKind.GoldAttack && x.Message == EconomyBatchProgression.FailureMessage) == 2,
                    "The health click completes and only the two failed attack clicks show the failure message");
            }

            private QuestRowDTO SeedQuestGrant(QuestRewardDTO grant, Action<ProgressionState> seed)
            {
                const string period = "2026-11-19";
                var claimable = QuestRow(period, 10, true);
                claimable.Rewards.Add(grant);
                _quests.Apply(new QuestStateDTO
                {
                    Version = "quest-funding-before", DailyPeriodKey = period, WeeklyPeriodKey = "2026-11-16", Rows = new() { claimable }
                }, "quest-funding-fixture");
                Check(LocalProgression.Execute("qa-quest-funding", state =>
                {
                    state.Economy.Server.SupportsQuestAwakeningBatch = state.Economy.Server.SupportsQuestAwakeningRecovery = true;
                    state.Economy.Server.QuestGoldPreview = new() { BaseAmount = 60 };
                    seed(state);
                    return true;
                }), "Seed confirmed balances and advertised quest support");
                Check(EconomyBatchProgression.TryQueueQuestClaim(new OnClaimQuestRewardRequestDTO
                    { QuestId = claimable.QuestId, PeriodKey = period }, null) == EconomyInputStatus.PendingSave && Transport.Calls == 0,
                    "The real quest claim enters the shared queue without an early send");
                return claimable;
            }

            private void ReplyQuestBatch(QuestRowDTO row, bool claimSucceeds, Action<OnEconomyBatchResponseDTO> update,
                params EconomyCommandResultDTO[] followingResults)
            {
                var request = Transport.Request;
                var state = LocalProgression.State;
                var status = claimSucceeds ? EconomyCommandStatus.Applied : EconomyCommandStatus.Rejected;
                Protocol.Message.Data.eErrorCode? error = claimSucceeds ? null : Protocol.Message.Data.eErrorCode.QuestNotClaimable;
                var receipt = new EconomyBatchReceiptDTO
                {
                    RequestId = request.RequestId, Generation = request.Generation, Sequence = request.Sequence,
                    PayloadHash = EconomyInputJournal.Hash(request), CommittedRevision = state.ServerEconomyRevision + 1,
                    IndependentGrowthFailures = request.IndependentGrowthFailures,
                    Results = new()
                    {
                        new() { Index = 0, Kind = EconomyCommandKind.QuestClaim, Status = status, AppliedCount = claimSucceeds ? 1 : 0,
                            ErrorCode = error, QuestClaimResults = new()
                            {
                                new() { QuestId = row.QuestId, PeriodKey = row.PeriodKey, Status = status, ErrorCode = error,
                                    GrantedRewards = claimSucceeds ? row.Rewards.Select(grant => grant.IsDynamicGold ? new QuestRewardDTO
                                    {
                                        Kind = grant.Kind, CurrencyType = grant.CurrencyType, Amount = state.Economy.Server.QuestGoldPreview.BaseAmount
                                    } : grant).ToList() : null }
                            } }
                    }
                };
                receipt.Results.AddRange(followingResults);
                var metadata = EconomyCopies.Copy(state.Economy.Server);
                metadata.NextSequence = request.Sequence + 1;
                metadata.PendingReceipt = receipt;
                var response = Response(request.RequestId, state.AttackLevel, LocalProgression.Balance(eCurrency.Gold), metadata, receipt);
                var changed = QuestRow(row.PeriodKey, claimSucceeds ? 10 : 0, false);
                changed.Rewards.AddRange(row.Rewards);
                if (claimSucceeds) changed.State = Protocol.Message.Dto.QuestRowState.Claimed;
                response.QuestChanges = new() { BaseVersion = "quest-funding-before", Version = "quest-funding-after", Rows = new() { changed } };
                update(response);
                Transport.Succeed(response);
                Pump(); Pump();
            }

            internal void CheckQuestFundedPull(long coins, eCurrency currency, long amount, bool claimSucceeds, string expectedFailure)
            {
                var grant = new QuestRewardDTO { Kind = RewardKind.Currency, CurrencyType = (Protocol.Message.Data.eCurrency)currency, Amount = amount };
                var claimable = SeedQuestGrant(grant, state =>
                {
                    state.Wallet[eCurrency.AncientCoin] = coins;
                    state.Wallet[eCurrency.EquipmentTicket] = 0;
                    state.EquipmentSchemaVersion = EquipmentManager.InstanceSchemaVersion;
                });
                var previousEquipment = EquipmentManager.Instance;
                var gacha = _owner.AddComponent<GachaManager>();
                var table = ScriptableObject.CreateInstance<GachaTableSO>();
                try
                {
                    EquipmentManager.Instance = _owner.AddComponent<EquipmentManager>();
                    table.gachaType = eGachaType.Equipment;
                    table.isImplemented = true;
                    table.costCurrency = eCurrency.AncientCoin;
                    int cap = currency == eCurrency.EquipmentTicket ? 0 : 50;
                    Check(gacha.CanPullMulti(table, 1) && gacha.GetTotalCost(table, 1) == cap &&
                        LocalProgression.Balance(eCurrency.AncientCoin) == coins && LocalProgression.Balance(eCurrency.EquipmentTicket) == 0 &&
                        Transport.Calls == 0, "Quest preview enables the exact shown pull cost without crediting the confirmed wallet or sending early");

                    string failure = null, duplicate = null;
                    int successes = 0;
                    gacha.TryPull(table, 1, _ => successes++, message => failure = message);
                    gacha.TryPull(table, 1, _ => successes++, message => duplicate = message);
                    Pump(); Pump();
                    var request = Transport.Request;
                    Check(gacha.IsPulling && failure == null && duplicate == "이미 뽑기가 진행 중입니다." && Transport.Calls == 1 &&
                        Transport.DrawCalls == 0 && LocalProgression.State.PendingEquipmentDrawId == null &&
                        request.Commands.Count == 1 && request.Commands[0].Kind == EconomyCommandKind.QuestClaim,
                        "One click holds input, flushes the real QuestClaim, and leaves the draw identity unissued while it waits");

                    long committed = claimSucceeds && currency == eCurrency.AncientCoin ? coins + amount : coins;
                    ReplyQuestBatch(claimable, claimSucceeds, response => response.CurrentState.Currency.AncientCoin = committed);
                    if (expectedFailure != null)
                    {
                        Check(failure == expectedFailure && !gacha.IsPulling && successes == 0 && Transport.DrawCalls == 0 &&
                            LocalProgression.State.PendingEquipmentDrawId == null && LocalProgression.Balance(eCurrency.AncientCoin) == coins &&
                            LocalProgression.Balance(eCurrency.EquipmentTicket) == 0 && gacha.GetTotalCost(table, 1) == 50,
                            "Post-fence refusal restores the quote, gives the precise reason, and creates no draw ID, send, or debit");
                        return;
                    }

                    var draw = Transport.Draw;
                    Check(failure == null && gacha.IsPulling && Transport.DrawCalls == 1 && draw?.Count == 1 &&
                        !string.IsNullOrEmpty(draw.RequestId) && draw.RequestId == LocalProgression.State.PendingEquipmentDrawId && draw.ExpectedDrawSequence.HasValue &&
                        LocalProgression.Balance(eCurrency.AncientCoin) == committed,
                        "The same click creates one durable draw ID and sends once using the confirmed balance without a local debit");
                    Transport.Succeed(new OnGachaEquipmentClassFragmentResponseDTO
                    {
                        ProtocolVersion = IdleKingdomFunction.Common.ApiVersions.ProtocolVersion, ServerVersion = 1,
                        EconomyRevision = LocalProgression.State.ServerEconomyRevision + 1,
                        Currency = new CurrencyQueryDTO(LocalProgression.Balance(eCurrency.Gold), committed - 50, 0, 0),
                        GachaState = new() { NextEquipmentDrawSequence = draw.ExpectedDrawSequence.Value + 1 },
                        Equipment = new()
                        {
                            Items = new(), PendingItems = new(), ReserveStacks = new(), SchemaVersion = EquipmentManager.InstanceSchemaVersion,
                            Capacity = EquipmentManager.InventoryCapacity, PendingCapacity = EquipmentManager.PendingCapacity, AutoLockFirstEnhancement = false
                        },
                        GachaList = new(), Rewards = new()
                    });
                    Check(successes == 1 && failure == null && !gacha.IsPulling && Transport.DrawCalls == 1 &&
                        LocalProgression.State.PendingEquipmentDrawId == null && LocalProgression.Balance(eCurrency.AncientCoin) == committed - 50,
                        "Only the successful gacha response saves the debit and finishes the original click");
                }
                finally
                {
                    EquipmentManager.Instance = previousEquipment;
                    UnityEngine.Object.DestroyImmediate(gacha);
                    UnityEngine.Object.DestroyImmediate(table);
                }
            }

            internal void CheckQuestFundedPromotion(bool claimSucceeds)
            {
                var grant = new QuestRewardDTO { Kind = RewardKind.Currency, CurrencyType = Protocol.Message.Data.eCurrency.ClassFragment, Amount = 10 };
                var claimable = SeedQuestGrant(grant, state =>
                {
                    state.Wallet[eCurrency.ClassFragment] = 30;
                    state.Jobs[0] = "Spearman";
                });
                var manager = _owner.AddComponent<KingdomArmyManager>();
                var player = _owner.AddComponent<Player>();
                KingdomArmyManager.JobChangeInfo completion = null;
                manager.OnJobChangeCompleted += info => completion = info;
                Check(manager.GetFragments() == 40 && LocalProgression.Balance(eCurrency.ClassFragment) == 30 &&
                    manager.CanChangeJob("Knight", player), "Projected fragments enable a paid promotion without a saved grant");
                Check(manager.TryChangeJobEx(player, "Knight") == KingdomArmyManager.JobChangeResult.Pending &&
                    manager.TryChangeJobEx(player, "Knight") == KingdomArmyManager.JobChangeResult.Busy,
                    "The promotion is accepted once and waits for the spending fence");
                Pump(); Pump();
                Check(Transport.Calls == 1 && Transport.Job == null && Transport.Request?.Commands.Single().Kind == EconomyCommandKind.QuestClaim,
                    "Only the quest command has reached transport before the promotion can send");
                ReplyQuestBatch(claimable, claimSucceeds, response => response.CurrentState.Currency.ClassFragment = claimSucceeds ? 40 : 30);
                if (!claimSucceeds)
                {
                    Check(completion?.Failure == "전직 파편이 10개 부족합니다." && !manager.IsChangingJob && Transport.Job == null &&
                        manager.GetFragments() == 30 && LocalProgression.State.Jobs[0] == "Spearman",
                        "Rejection restores the confirmed fragments, reports the deficit and sends no promotion");
                    return;
                }
                ulong code = UserManager.JobCode("Knight");
                Check(Transport.Calls == 2 && Transport.Job?.JobCode == code && Transport.Job.Index == 0 &&
                    LocalProgression.Balance(eCurrency.ClassFragment) == 40, "The original click sends one paid promotion after claim settlement");
                Transport.Succeed(new OnGetJobResponseDTO
                {
                    ProtocolVersion = IdleKingdomFunction.Common.ApiVersions.ProtocolVersion, ServerVersion = 1,
                    EconomyRevision = LocalProgression.State.ServerEconomyRevision + 1,
                    Index = 0, JobCode = code, JobTrees = new() { new() { Index = 0, JobList = new() { code } } },
                    IsNewUnlock = true, SpentClassFragment = 40, CurrentClassFragment = 0
                });
                Check(completion?.Ok == true && completion.SpentClassFragment == 40 && !manager.IsChangingJob &&
                    LocalProgression.Balance(eCurrency.ClassFragment) == 0 && LocalProgression.State.Jobs[0] == "Knight",
                    "The successful promotion response alone saves its debit and selected job");
            }

            internal void CheckQuestFundedGrowth(bool mage, bool claimSucceeds)
            {
                var currency = mage ? eCurrency.ArcaneKnowledge : eCurrency.Gold;
                var kind = mage ? EconomyCommandKind.MageEnhance : EconomyCommandKind.GoldAttack;
                MageTowerDetailPopupView detail = null;
                if (mage) detail = OpenMageDetail(true);
                else BuildPanel();
                var button = mage ? detail.btnEnhance : AttackButton;
                int ConfirmedLevel() => mage ? _mage.GetEnhanceLevel(0) : LocalProgression.State.AttackLevel;
                var row = SeedQuestGrant(new QuestRewardDTO
                {
                    Kind = RewardKind.Currency, CurrencyType = (Protocol.Message.Data.eCurrency)currency,
                    Amount = mage ? 10 : 0, IsDynamicGold = !mage
                }, state => state.Wallet[currency] = 0);
                long reward = mage ? 10 : 60;
                long spend = mage ? 10 : 50;
                Check(button.interactable && EconomyBatchProgression.GetDisplayBalance(currency) == reward &&
                    (mage ? detail.lblEnhCost.text.Contains("보유 10") : TopGold.text == NumberNotation.Format(reward)) &&
                    LocalProgression.Balance(currency) == 0,
                    $"The real {kind} button and balance display include the projected quest reward");
                button.onClick.Invoke();
                Check(LocalProgression.Balance(currency) == 0 && ConfirmedLevel() == 0 &&
                    EconomyBatchProgression.GetDisplayBalance(currency) == reward - spend &&
                    EconomyBatchProgression.GetProjection().Level(kind, mage ? 0 : null) == 1,
                    $"A single {kind} click spends only from the display state until the batch receipt");
                EconomyBatchProgression.FlushBeforeWrite(_ => { });
                Pump(); Pump();
                Check(Transport.Calls == 1 && Transport.Request?.Commands.Select(command => command.Kind)
                    .SequenceEqual(new[] { EconomyCommandKind.QuestClaim, kind }) == true,
                    $"The actual batch sends QuestClaim before {kind} without a separate spend request");
                var result = new EconomyCommandResultDTO
                {
                    Index = 1, Kind = kind, Status = claimSucceeds ? EconomyCommandStatus.Applied : EconomyCommandStatus.Rejected,
                    AppliedCount = claimSucceeds ? 1 : 0,
                    SpentGold = claimSucceeds && !mage ? spend : 0, SpentArcaneKnowledge = claimSucceeds && mage ? spend : 0,
                    ErrorCode = claimSucceeds ? null : mage ? Protocol.Message.Data.eErrorCode.eNotEnoughArcaneKnowledge : Protocol.Message.Data.eErrorCode.eNotEnoughGold
                };
                ReplyQuestBatch(row, claimSucceeds, response =>
                {
                    if (!mage)
                    {
                        response.CurrentState.Currency.Gold = claimSucceeds ? reward - spend : 0;
                        response.CurrentState.EnchantATKCount = claimSucceeds ? 1 : 0;
                    }
                    else
                    {
                        response.CurrentState.MageTowerState = new()
                        {
                            Skills = new() { new() { SkillId = 0, EnhancementLevel = claimSucceeds ? 1 : 0,
                                KnowledgeSpent = claimSucceeds ? spend : 0 }, new() { SkillId = 3, EnhancementLevel = 4 } },
                            EquippedSkillIds = LocalProgression.State.MageSlots.ToArray()
                        };
                    }
                }, result);
                long settled = claimSucceeds ? reward - spend : 0;
                int level = claimSucceeds ? 1 : 0;
                Check(LocalProgression.Balance(currency) == settled && EconomyBatchProgression.GetDisplayBalance(currency) == settled &&
                    ConfirmedLevel() == level && EconomyBatchProgression.GetProjection().Level(kind, mage ? 0 : null) == level &&
                    (mage ? detail.lblEnhLevel.text == $"강화 레벨: {level} / 100" : TopGold.text == NumberNotation.Format(settled)) &&
                    SuccessfulInputs == (claimSucceeds ? 1 : 0) && FailedInputs == (claimSucceeds ? 0 : 1) &&
                    EconomyBatchProgression.PendingSpendingCount == 0 && !EconomyBatchProgression.HasUnknownResult,
                    $"The real {kind} display settles after a {(claimSucceeds ? "successful" : "rejected")} claim and spend");
            }

            internal void CheckQuestSnapshotAdmission()
            {
                const string period = "2026-11-19";
                const long equipmentId = 12;
                var network = NetworkManager.Instance;
                var target = QuestRow(period, 10, false);
                target.State = Protocol.Message.Dto.QuestRowState.Claimable;
                target.Rewards.Add(new() { Kind = RewardKind.Currency, CurrencyType = Protocol.Message.Data.eCurrency.AncientCoin, Amount = 5 });
                QuestStateDTO Board(string version, params QuestRowDTO[] rows) => new()
                {
                    Version = version, DailyPeriodKey = period, WeeklyPeriodKey = "2026-11-16", Rows = rows.ToList()
                };
                _quests.Apply(Board("before-quest-read", target), "quest-read-fixture");
                Check(LocalProgression.Execute("qa-admission-funds", state =>
                {
                    state.Economy.Server.SupportsQuestAwakeningBatch = state.Economy.Server.SupportsQuestAwakeningRecovery = true;
                    state.Economy.Server.QuestGoldPreview = new() { BaseAmount = 60 };
                    state.Economy.Server.SupportsMageEnhance = true;
                    state.Economy.Server.EquipmentEnhanceAvailable = true;
                    state.EquipmentSchemaVersion = EquipmentManager.InstanceSchemaVersion;
                    state.HighestMainClear = (long)Protocol.Message.Data.eStage.Stage2_5;
                    state.Wallet[eCurrency.Ruby] = 1000;
                    state.Wallet[eCurrency.ArcaneKnowledge] = 100;
                    state.Wallet[eCurrency.EquipmentStone] = 100;
                    state.MageSkills[0] = new();
                    state.Equipment.Add(new() { Id = "quest-read-gear", ServerInstanceId = equipmentId,
                        Code = ItemCode.Encode(eEquipmentRarity.Rare, eEquipmentSlot.Weapon, eJobFlag.None, 3) });
                    return true;
                }), "The fixture saves an eligible quest and all four growth balances");
                Check(EconomyBatchProgression.TryQueueGold(true, 1, null, out _) == EconomyInputStatus.PendingSave,
                    "An earlier Gold input is ready for the snapshot boundary");
                typeof(QuestManager).GetMethod("Fetch", Instance).Invoke(_quests, new object[] { network });
                Pump(); Pump();
                var prior = Transport.Request;
                Check(prior != null && Transport.Calls == 1 && Transport.Pending && Transport.QuestReads == 0 && _quests.IsRefreshing &&
                    prior.Commands.Single().Kind == EconomyCommandKind.GoldAttack,
                    "The actual menu snapshot waits behind the original single-command batch");
                var token = _quests.GetSnapshot(eQuestCategory.Daily).Rows.Single().Token;
                Check(_quests.TryClaimTab(eQuestCategory.Daily, token, null).IsPending &&
                    EconomyBatchProgression.GetProjection().Balance(eCurrency.AncientCoin) == 5,
                    "The selected reward enters the journal and projection while the snapshot waits for the prior batch");

                var receipt = new EconomyBatchReceiptDTO
                {
                    RequestId = prior.RequestId, Generation = prior.Generation, Sequence = prior.Sequence,
                    PayloadHash = EconomyInputJournal.Hash(prior), CommittedRevision = 2,
                    Results = new() { new() { Index = 0, Kind = EconomyCommandKind.GoldAttack,
                        Status = EconomyCommandStatus.Applied, AppliedCount = 1, SpentGold = 50 } }
                };
                var metadata = EconomyCopies.Copy(LocalProgression.State.Economy.Server);
                metadata.NextSequence = prior.Sequence + 1;
                metadata.PendingReceipt = receipt;
                var priorResponse = Response(prior.RequestId, 1, InitialGold - 50, metadata, receipt);
                priorResponse.CurrentState.Currency.Ruby = 1000;
                priorResponse.CurrentState.Currency.ArcaneKnowledge = 100;
                priorResponse.CurrentState.Currency.EquipmentStone = 100;
                priorResponse.CurrentState.StageState.MainClear = Protocol.Message.Data.eStage.Stage2_5;
                Transport.Succeed(priorResponse);
                Pump(); Pump();
                Check(Transport.Calls == 2 && Transport.QuestReads == 1 && Transport.Pending && _quests.IsRefreshing,
                    "The quest snapshot goes on the wire before the later claim");
                Check(EconomyBatchProgression.TryQueueGold(false, 1, null, out _) == EconomyInputStatus.PendingSave &&
                    EconomyBatchProgression.TryQueueRuby(false, out _) == EconomyInputStatus.PendingSave &&
                    EconomyBatchProgression.TryQueueMageEnhance(0) == EconomyInputStatus.PendingSave &&
                    EconomyBatchProgression.TryQueueEquipmentEnhance(equipmentId) == EconomyInputStatus.PendingSave,
                    "Gold, ruby, mage and equipment inputs are accepted while the actual quest read is on the wire");
                Pump(); Pump();
                var expectedOrder = new[] { EconomyCommandKind.QuestClaim, EconomyCommandKind.GoldHealth,
                    EconomyCommandKind.RubyGold, EconomyCommandKind.MageEnhance, EconomyCommandKind.EquipmentEnhance };
                string oldJournalPath = LocalProgression.EconomyJournalPath;
                string oldAccountKey = LocalProgression.AccountKey;
                var durable = new EconomyInputJournal(oldJournalPath, oldAccountKey).Data;
                Check(durable.InFlight == null && durable.Inputs.Select(x => x.Command.Kind).SequenceEqual(expectedOrder) &&
                    Transport.Calls == 2 && LocalProgression.State.HealthLevel == 0 && LocalProgression.Balance(eCurrency.AncientCoin) == 0,
                    "All later clicks are durable, with no extra HTTP or confirmed reward or upgrade before the read completes");
                Check(EconomyBatchProgression.FlushBeforeWrite(_ => { }) && !EconomyBatchProgression.CanAcceptGrowth,
                    "A general external-write boundary still blocks fresh clicks alongside the quest read");

                var newlyClaimable = QuestRow(period, 10, false);
                newlyClaimable.QuestId = 101; newlyClaimable.State = target.State;
                Transport.Succeed(new OnGetQuestStateResponseDTO
                {
                    ProtocolVersion = IdleKingdomFunction.Common.ApiVersions.ProtocolVersion, ServerVersion = 1,
                    EconomyRevision = 3, Quests = Board("after-quest-read", target, newlyClaimable)
                });
                Pump(); Pump();
                var batch = Transport.Request;
                Check(Transport.Calls == 3 && Transport.Pending && !_quests.IsRefreshing &&
                    LocalProgression.State.ServerEconomyRevision == 3 && batch.ExpectedEconomyRevision == 3 &&
                    batch.QuestVersion == "after-quest-read" && batch.Sequence == prior.Sequence + 1 &&
                    batch.Commands.Select(x => x.Kind).SequenceEqual(expectedOrder) &&
                    batch.Commands[0].QuestClaim.Targets.Select(x => x.QuestId).SequenceEqual(new[] { target.QuestId }),
                    "The next batch seals with the refreshed revision and board, original reward selection, and exact click order");

                Transport.TakePending();
                Set(network, "_callStartedAt", Time.realtimeSinceStartup - 31f);
                Invoke(network, "Update");
                Check(EconomyBatchProgression.HasUnknownResult && !EconomyBatchProgression.CanAcceptGrowth &&
                    EconomyBatchProgression.TryQueueGold(true, 1, null, out _) == EconomyInputStatus.Busy,
                    "Losing the batch response preserves the existing recovery input block");
                typeof(EconomyBatchProgression).GetField("_retryAt", Static).SetValue(null, 0f);
                Pump(); Pump();
                Check(Transport.Calls == 4 && Transport.Lookup?.RequestId == batch.RequestId &&
                    Transport.Lookup.Sequence == batch.Sequence && Transport.Lookup.Generation == batch.Generation,
                    "Recovery looks up the exact same batch identity");
                network.BeginAuthentication();
                LocalProgression.OpenTestAccount("economy-quest-read-next-" + Guid.NewGuid().ToString("N"));
                Transport.Succeed(new { });
                Pump();
                var retained = new EconomyInputJournal(oldJournalPath, oldAccountKey).Data.InFlight;
                Check(!EconomyBatchProgression.Available && EconomyBatchProgression.PendingSpendingCount == 0 &&
                    retained?.RequestId == batch.RequestId && EconomyInputJournal.Hash(retained) == EconomyInputJournal.Hash(batch) &&
                    Transport.Calls == 4 && Transport.Unexpected == 0,
                    "Account switch ignores the old reply and leaves the unresolved original payload in its own journal");
            }

            internal void CheckQuestSnapshotTimeout()
            {
                var network = NetworkManager.Instance;
                typeof(QuestManager).GetMethod("Fetch", Instance).Invoke(_quests, new object[] { network });
                Check(Transport.QuestReads == 1 && Transport.Pending &&
                    EconomyBatchProgression.TryQueueGold(true, 1, null, out _) == EconomyInputStatus.PendingSave,
                    "A Gold click is accepted during a quest snapshot that will lose its reply");
                Pump();
                var late = Transport.TakePending();
                Set(network, "_callStartedAt", Time.realtimeSinceStartup - 31f);
                Invoke(network, "Update");
                Pump();
                var saved = new EconomyInputJournal(LocalProgression.EconomyJournalPath, LocalProgression.AccountKey).Data;
                Check(EconomyBatchProgression.HasUnknownResult && !EconomyBatchProgression.CanAcceptGrowth &&
                    EconomyBatchProgression.TryQueueGold(false, 1, null, out _) == EconomyInputStatus.Busy &&
                    Transport.Calls == 2 && Transport.UserStateReads == 1 && Transport.Request == null &&
                    saved.InFlight == null && saved.Inputs.Single().Command.Kind == EconomyCommandKind.GoldAttack,
                    "A lost quest response retains the click and permits only the existing state recovery request");
                late.ApiResult = new ExecuteFunctionResult { FunctionResult = "{}" };
                late.InvokeSuccessCallback();
                Pump();
                Check(EconomyBatchProgression.HasUnknownResult && Transport.Calls == 2 && Transport.Pending &&
                    EconomyBatchProgression.PendingSpendingCount == 1 && Transport.Unexpected == 0,
                    "A late quest response does not admit another click or send the retained input before recovery");
            }

            internal void CheckMageAwakening()
            {
                var detail = OpenMageDetail(true);
                void SeedFragments(int fragments)
                {
                    Check(LocalProgression.Execute("qa-mage-awakening", state =>
                    {
                        state.Economy.Server.SupportsQuestAwakeningBatch = state.Economy.Server.SupportsQuestAwakeningRecovery = true;
                        state.Economy.Server.QuestGoldPreview = new() { BaseAmount = 60 };
                        state.MageSkills[0].Awaken = 9;
                        state.MageSkills[0].Fragments = fragments;
                        return true;
                    }), $"Seed a confirmed 9-stage skill with {fragments} fragments");
                    _mage.NotifyCommitted();
                }

                SeedFragments(9);
                Check(!detail.btnAwaken.interactable && detail.lblAwkCost.text.Contains("필요 10") &&
                    _mage.TryAwakenEx(0) == MageTowerManager.MageActionResult.NotEnough && Transport.Calls == 0,
                    "Nine fragments are insufficient to awaken the ninth stage");
                SeedFragments(10);
                float cooldown = _mage.GetEffectiveCooldown(0);
                long damage = _mage.GetEffectiveDamage(0);
                var deadline = typeof(EconomyBatchProgression).GetField("_sealAfter", Static);
                Check(EconomyBatchProgression.TryQueueGold(true, 1, null, out _) == EconomyInputStatus.PendingSave,
                    "An independently queued Gold input opens the common batch");
                float openedDeadline = (float)deadline.GetValue(null);
                Check(detail.btnAwaken.interactable && openedDeadline > Time.realtimeSinceStartup && detail.awakening.stars.Length == 10,
                    "The real awakening action is available before the shared deadline");
                detail.btnAwaken.onClick.Invoke();
                Pump();
                var animation = detail.awakening;
                Color lit = animation.stars[8].color;
                Check(Transport.Calls == 0 && (float)deadline.GetValue(null) == openedDeadline && animation.gameObject.activeSelf &&
                    animation.stars[9].color != lit && _mage.GetAwakeningLevel(0) == 9 && _mage.GetFragments(0) == 10 &&
                    _mage.GetEffectiveCooldown(0) == cooldown && _mage.GetEffectiveDamage(0) == damage && !_mage.IsBloomEnabled(0) &&
                    !detail.btnBloom.interactable && detail.lblAwkLevel.text == "각성 레벨: 9 / 10",
                    "Awakening preserves the existing send deadline and leaves the last star, skill effects and bloom unconfirmed");

                deadline.SetValue(null, 0f);
                Pump(); Pump();
                var request = Transport.Request;
                var awaken = request?.Commands.LastOrDefault();
                Check(Transport.Calls == 1 && request != null && request.Commands.Select(command => command.Kind)
                    .SequenceEqual(new[] { EconomyCommandKind.GoldAttack, EconomyCommandKind.MageAwakening }) &&
                    awaken.SkillId == 0 && awaken.ExpectedLevel == 9 && awaken.SpendLimit == 10,
                    "Normal expiry sends the Gold action and exact awakening quote in one request");
                var state = LocalProgression.State;
                var receipt = new EconomyBatchReceiptDTO
                {
                    RequestId = request.RequestId, Generation = request.Generation, Sequence = request.Sequence,
                    PayloadHash = EconomyInputJournal.Hash(request), CommittedRevision = state.ServerEconomyRevision + 1,
                    IndependentGrowthFailures = request.IndependentGrowthFailures,
                    Results = new()
                    {
                        new() { Index = 0, Kind = EconomyCommandKind.GoldAttack, Status = EconomyCommandStatus.Applied, AppliedCount = 1, SpentGold = 50 },
                        new() { Index = 1, Kind = EconomyCommandKind.MageAwakening, Status = EconomyCommandStatus.Applied, AppliedCount = 1,
                            SkillId = 0, AwakeningLevel = 10, SpentFragments = 10 }
                    }
                };
                var metadata = EconomyCopies.Copy(state.Economy.Server);
                metadata.NextSequence = request.Sequence + 1;
                metadata.PendingReceipt = receipt;
                var response = Response(request.RequestId, 1, InitialGold - 50, metadata, receipt);
                response.CurrentState.Currency.ArcaneKnowledge = LocalProgression.Balance(eCurrency.ArcaneKnowledge);
                response.CurrentState.MageTowerState = new()
                {
                    Skills = new() { new() { SkillId = 0, AwakeningLevel = 10, BloomEnabled = true }, new() { SkillId = 3, EnhancementLevel = 4 } },
                    EquippedSkillIds = state.MageSlots.ToArray()
                };
                Transport.Succeed(response);
                Pump();
                Check(_mage.GetAwakeningLevel(0) == 10 && _mage.GetFragments(0) == 0 && _mage.IsBloomEnabled(0) &&
                    detail.btnBloom.interactable && detail.lblAwkLevel.text == "각성 레벨: 10 / 10" && !detail.btnAwaken.interactable &&
                    _mage.TryAwakenEx(0) == MageTowerManager.MageActionResult.MaxLevel && Transport.Calls == 1,
                    "Only the saved receipt reaches stage ten, consumes ten fragments, enables bloom and disables the capped action");
                Set(animation, "confirmed", Time.unscaledTime - .8f);
                Invoke(animation, "Update");
                Check(animation.stars[9].color == lit && animation.caption.text.EndsWith(" · 개화", StringComparison.Ordinal),
                    "The original effect lights its tenth star and reveals bloom after confirmation");
            }

            internal void Acknowledge()
            {
                var request = Transport.Request;
                var state = LocalProgression.State;
                long gold = LocalProgression.Balance(eCurrency.Gold); int level = state.AttackLevel;
                var receipt = new EconomyBatchReceiptDTO
                {
                    RequestId = request.RequestId, Generation = request.Generation, Sequence = request.Sequence,
                    PayloadHash = EconomyInputJournal.Hash(request), CommittedRevision = state.ServerEconomyRevision + 1, Results = new()
                };
                for (int i = 0; i < request.Commands.Count; i++)
                {
                    var command = request.Commands[i];
                    Check(command.Kind == EconomyCommandKind.GoldAttack && command.Mode == 1 && command.ExpectedLevel == level,
                        "Isolated server receives the next ordered x1 attack command");
                    long cost = BalanceMath.GoldTotal(level, 1); gold -= cost; level++;
                    receipt.Results.Add(new() { Index = i, Kind = command.Kind, Status = EconomyCommandStatus.Applied, AppliedCount = 1, SpentGold = cost });
                }
                var metadata = EconomyCopies.Copy(state.Economy.Server); metadata.NextSequence = request.Sequence + 1; metadata.PendingReceipt = receipt;
                LastRequestId = request.RequestId;
                var response = Response(request.RequestId, level, gold, metadata, receipt);
                long questRevision = _quests?.GetSnapshot(eQuestCategory.Daily).Revision ?? -1;
                if (_quests != null)
                {
                    Check(request.QuestVersion == (level == 1 ? "board-0" : "board-1"), "Batch seals the applied quest version");
                    if (level == 1) response.QuestChanges = new()
                    {
                        BaseVersion = "board-0", Version = "board-1", Rows = new() { QuestRow("2026-11-19", 1, false) }
                    };
                }
                var callback = System.Diagnostics.Stopwatch.StartNew();
                Transport.Succeed(response); callback.Stop();
                CallbackMilliseconds.Add(callback.Elapsed.TotalMilliseconds);
                if (_quests != null)
                {
                    var board = _quests.GetSnapshot(eQuestCategory.Daily);
                    Check(board.Revision == questRevision + (level == 1 ? 1 : 0) && board.Rows[1].Progress == 1 &&
                        ReferenceEquals(_pendingQuest, board.Rows[0]) && ReferenceEquals(_guideBoard, _quests.GetSnapshot(eQuestCategory.Guide)),
                        "Only the updated period row is replaced; pending claims and unrelated boards are kept");
                    QuestChangeSet published = null;
                    void StoreChanges(QuestChangeSet changes) { published = changes; }
                    _quests.QuestsChanged += StoreChanges;
                    Invoke(_quests, "LateUpdate");
                    _quests.QuestsChanged -= StoreChanges;
                    Check(level == 1 ? published != null && published.ChangedTokens.SequenceEqual(new[] { board.Rows[1].Token }) &&
                        published.ChangedCategories.SequenceEqual(new[] { eQuestCategory.Daily }) : published == null,
                        "UI notification identifies only the changed row and category, and skips an unchanged response");
                    if (level == 1)
                    {
                        Check(_quests.TryApplyChanges(response.QuestChanges) && ReferenceEquals(board, _quests.GetSnapshot(eQuestCategory.Daily)),
                            "Duplicate quest delta is a no-op");
                        Check(!_quests.TryApplyChanges(new QuestChangesDTO
                        {
                            BaseVersion = "older-board", Version = "other-board", Rows = new() { QuestRow("2026-11-19", 2, false) }
                        }) && ReferenceEquals(board, _quests.GetSnapshot(eQuestCategory.Daily)), "Stale quest delta leaves the board intact");
                    }
                }
            }

            internal void ReplyNotFound()
            {
                var state = LocalProgression.State;
                Check(Transport.Lookup.QuestVersion == "board-0", "Result lookup sends the current quest version separately");
                Transport.Succeed(Response(Transport.Lookup.RequestId, state.AttackLevel,
                    LocalProgression.Balance(eCurrency.Gold), EconomyCopies.Copy(state.Economy.Server), null));
            }

            internal void CheckHeldQuestRefresh()
            {
                int calls = Transport.Calls;
                EconomyBatchProgression.MarkUnknown("fixture hold");
                _quests.RequestRefresh("fixture needs authoritative rows");
                Set(_quests, "_lastRefreshAt", float.NegativeInfinity);
                Invoke(_quests, "Update");
                Check(NetworkManager.GetFailureKind(_quests.LastFailure) == "EconomyHeld" && Transport.Calls == calls &&
                    (bool)typeof(QuestManager).GetField("_refreshQueued", Instance).GetValue(_quests),
                    "A full refresh held by unresolved economy work stays queued without sending another request");
                EconomyBatchProgression.ConfirmFullRefresh();
            }

            private OnEconomyBatchResponseDTO Response(string id, int level, long gold, EconomyBatchStateDTO metadata, EconomyBatchReceiptDTO receipt)
            {
                var state = LocalProgression.State;
                return new OnEconomyBatchResponseDTO
                {
                    ProtocolVersion = IdleKingdomFunction.Common.ApiVersions.ProtocolVersion, ServerVersion = 1,
                    EconomyRevision = receipt?.CommittedRevision ?? state.ServerEconomyRevision,
                    RequestId = id, Status = receipt == null ? EconomyBatchStatus.NotFound : EconomyBatchStatus.Completed, Receipt = receipt,
                    QuestRefreshRequired = _quests != null ? false : null,
                    CurrentState = new()
                    {
                        Currency = new CurrencyQueryDTO(gold, 0, 0, 0), EnchantATKCount = level, Level = 1,
                        RubyGrowthState = new() { NextRequestSequence = 1, LastResetDay = "" }, EconomyBatchState = metadata,
                        StageState = new() { CurrentStage = (Protocol.Message.Data.eStage)state.MainStage, HighestStage = (Protocol.Message.Data.eStage)state.CycleFrontierStage }
                    }
                };
            }

            private static QuestRowDTO QuestRow(string period, long progress, bool pending) => new()
            {
                QuestId = 100, Category = QuestCategory.Daily, PeriodKey = period, Title = "몬스터 처치", Description = "",
                ObjectiveType = "MonsterKill", PresentationType = "None", RequiredCount = 10, Progress = progress,
                State = pending ? Protocol.Message.Dto.QuestRowState.Claimable : Protocol.Message.Dto.QuestRowState.InProgress,
                IsPending = pending, Rewards = new()
            };

            internal object Report(bool success) => new
            {
                success, count = Checks.Count, checks = Checks, transportCalls = Transport.Calls, successfulInputs = SuccessfulInputs,
                immediateNineTapMilliseconds = ImmediateNineTapMilliseconds, responseApplyMilliseconds = CallbackMilliseconds,
                simulatedDelayPerResponseSeconds = 1, timingScope = "isolated fake HTTP; excludes capture pauses; not real server RTT"
            };

            public void Dispose()
            {
                if (_disposed) return; _disposed = true;
                EconomyBatchProgression.InputCompleted -= OnInputCompleted;
                if (_mage != null)
                {
                    MageTowerDetailPopupController.Hide();
                    Invoke(_mage, "OnDestroy");
                }
                _panel?.OnClosed?.Invoke();
                if (_main != null) Invoke(_main, "UnhookEconomyChangeHandler");
                if (_canvas != null) UnityEngine.Object.DestroyImmediate(_canvas);
                if (Enhance != null) Invoke(Enhance, "OnDestroy");
                if (NetworkManager.Instance != null) Invoke(NetworkManager.Instance, "ResetCallQueue");
                NetworkManager.Instance = null; UserManager.Instance = null; SetUi(null);
                if (_quests != null && QuestManager.Instance == _quests) QuestManager.Instance = null;
                if (_activeNetwork != null) UnityEngine.Object.DestroyImmediate(_activeNetwork);
                if (_owner != null) UnityEngine.Object.DestroyImmediate(_owner);
                var publish = (Action)Delegate.CreateDelegate(typeof(Action), _walletType.GetMethod("Publish", Static));
                LocalProgression.Changed -= publish;
                _walletType.GetField("_subscribed", Static).SetValue(null, _previousWalletSubscription);
                _account?.Dispose();
                if (_previousWalletSubscription) LocalProgression.Changed += publish;
                var published = (Dictionary<eCurrency, long>)_walletType.GetField("Published", Static).GetValue(null);
                published.Clear(); foreach (var pair in _previousPublished) published[pair.Key] = pair.Value;
                typeof(EconomyBatchProgression).GetField("_clientId", Static).SetValue(null, _previousClientId);
                typeof(DevelopmentPanelController).GetField("_selectedCount", Static).SetValue(null, _previousCount);
                typeof(DevelopmentPanelController).GetField("_rubyTab", Static).SetValue(null, _previousRubyTab);
                typeof(QuestManager).GetField("_received", Static).SetValue(null, _previousQuests);
                typeof(QuestManager).GetField("_receivedSource", Static).SetValue(null, _previousQuestSource);
                PluginManager.SetPlugin(_previousTransport, PluginContract.PlayFab_Transport);
            }
        }

        private sealed class IsolatedGrowthHttp : ITransportPlugin
        {
            private PlayFab.Internal.CallRequestContainer _pending;
            internal OnEconomyBatchRequestDTO Request;
            internal OnGetEconomyBatchResultRequestDTO Lookup;
            internal OnEnchantSkillRequestDTO Single;
            internal OnGachaEquipmentClassFragmentRequestDTO Draw;
            internal OnGetJobRequestDTO Job;
            internal int DrawCalls, QuestReads, UserStateReads;
            internal int Calls, Unexpected;
            internal float LastSendAt;
            internal bool Pending => _pending != null;
            public bool IsInitialized => true;
            public void Initialize() { }
            public void Update() { }
            public void OnDestroy() { }
            public int GetPendingMessages() => Pending ? 1 : 0;
            public void MakeApiCall(object request)
            {
                if (request is not PlayFab.Internal.CallRequestContainer container || container.ApiRequest is not ExecuteFunctionRequest function || Pending)
                { Block(); return; }
                string payload = JsonConvert.SerializeObject(function.FunctionParameter);
                if (function.FunctionName == "OnEconomyBatch")
                { Request = JsonConvert.DeserializeObject<OnEconomyBatchRequestDTO>(payload); Lookup = null; }
                else if (function.FunctionName == "OnGetEconomyBatchResult") Lookup = JsonConvert.DeserializeObject<OnGetEconomyBatchResultRequestDTO>(payload);
                else if (function.FunctionName == "OnEnChantSkill") Single = JsonConvert.DeserializeObject<OnEnchantSkillRequestDTO>(payload);
                else if (function.FunctionName == "OnGachaEquipmentClassFragment")
                { Draw = JsonConvert.DeserializeObject<OnGachaEquipmentClassFragmentRequestDTO>(payload); DrawCalls++; }
                else if (function.FunctionName == "OnGetJob") Job = JsonConvert.DeserializeObject<OnGetJobRequestDTO>(payload);
                else if (function.FunctionName == "OnGetQuestState") QuestReads++;
                else if (function.FunctionName == "OnGetUserState") UserStateReads++;
                else { Block(); return; }
                _pending = container; Calls++; LastSendAt = Time.realtimeSinceStartup;
            }
            internal void Throttle()
            {
                var pending = _pending ?? throw new InvalidOperationException("No pending growth HTTP request."); _pending = null;
                pending.ApiResult = new ExecuteFunctionResult { Error = new FunctionExecutionError
                {
                    Message = "Invoking an Azure Function failed with HttpStatus 500 and response body " +
                        "{\"ProtocolVersion\":2,\"ServerVersion\":1,\"ErrorCode\":1014,\"Message\":\"limited\",\"StorageThrottled\":true,\"RetryAfterSeconds\":2}"
                } };
                pending.InvokeSuccessCallback();
            }
            internal PlayFab.Internal.CallRequestContainer TakePending()
            {
                if (!Pending) throw new InvalidOperationException("No response to drop.");
                var pending = _pending; _pending = null; return pending;
            }
            internal void Succeed(object response)
            {
                var pending = _pending ?? throw new InvalidOperationException("No pending growth HTTP request."); _pending = null;
                pending.ApiResult = new ExecuteFunctionResult { FunctionResult = JsonConvert.SerializeObject(response) };
                pending.InvokeSuccessCallback();
            }
            public void SimpleGetCall(string url, Action<byte[]> success, Action<string> error) => Block();
            public void SimplePutCall(string url, byte[] payload, Action<byte[]> success, Action<string> error) => Block();
            public void SimplePostCall(string url, byte[] payload, Action<byte[]> success, Action<string> error) => Block();
            private void Block() { Unexpected++; throw new InvalidOperationException("Rapid tap acceptance blocks all external and unexpected HTTP."); }
        }

        private static object Invoke(object target, string name) => target.GetType().GetMethod(name, Instance).Invoke(target, null);

        public static object Run()
        {
            if (!Application.isPlaying || LocalProgression.IsReady || UIManager.Instance != null ||
                UserManager.Instance != null || NetworkManager.Instance != null || ModalBackHandler.HasOpenModal)
                throw new InvalidOperationException("Run in empty PlayMode without an account, managers, or open modals.");
            var checks = new List<string>();
            void Check(bool condition, string name)
            { if (!condition) throw new InvalidOperationException(name); checks.Add(name); }
            GameObject managers = null, canvas = null;
            using (LocalProgression.BeginTestSession())
            {
                try
                {
                    LocalProgression.OpenTestAccount("economy-ui-" + Guid.NewGuid().ToString("N"));
                    var catalog = AssetDatabase.LoadAssetAtPath<UIViewCatalog>("Assets/UGUI/UIViewCatalog.asset");
                    Check(catalog != null && catalog.screenMain != null && catalog.itemCurrencyLine != null &&
                        catalog.popupEquipmentAction != null, "Real main, currency row, and confirmation prefabs exist");
                    managers = new GameObject("EconomyUiAcceptance_Managers");
                    managers.SetActive(false);
                    var ui = managers.AddComponent<UIManager>();
                    Set(ui, "catalog", catalog);
                    canvas = new GameObject("EconomyUiAcceptance_Canvas", typeof(RectTransform), typeof(Canvas));
                    Set(ui, "layerOverlays", canvas.transform);
                    SetUi(ui);
                    var view = UnityEngine.Object.Instantiate(catalog.screenMain, canvas.transform, false).GetComponent<MainScreenView>();
                    var controller = new MainScreenController();
                    Set(controller, "_view", view); Set(controller, "_host", ui);
                    var content = Get<RectTransform>(view, "popupCurrenciesContent");
                    int decorations = content.childCount;
                    Refresh(controller);
                    var firstRows = content.GetComponentsInChildren<CurrencyLineItemView>(true);
                    Check(firstRows.Length == 6 && content.childCount == decorations + 6,
                        "Soft currency rows are created once and preserve authored frame decorations");
                    var value = Get<TMP_Text>(firstRows[1], "valueLabel");
                    value.text = "stale";
                    Refresh(controller);
                    Check(firstRows.SequenceEqual(content.GetComponentsInChildren<CurrencyLineItemView>(true)) && value.text == "0",
                        "Repeated refresh updates values without recreating currency rows");
                    Set(controller, "_currencyPremiumGroup", true); Refresh(controller);
                    Check(content.GetComponentsInChildren<CurrencyLineItemView>(true).Length == 7 &&
                        firstRows.Skip(1).All(row => !row.gameObject.activeSelf), "Premium group reuses the title and hides soft rows");
                    Set(controller, "_currencyPremiumGroup", false); Refresh(controller);
                    Check(content.GetComponentsInChildren<CurrencyLineItemView>(true).Length == 7 &&
                        firstRows.All(row => row.gameObject.activeSelf), "Returning to soft currencies reuses every original row");

                    int calls = 0, listeners = ConfirmationListeners();
                    EquipmentActionDialog Dialog() => canvas.GetComponentsInChildren<EquipmentActionDialog>(false).Single();
                    Check(EquipmentActionDialog.ShowConfirmation("육성 재개", "입력 2건 · 104 골드", "재개하기", () => calls++),
                        "Existing equipment prefab can show an explicit growth confirmation");
                    Check(calls == 0 && ConfirmationListeners() == listeners + 1, "Opening a preview does not resume spending");
                    Dialog().cancel.onClick.Invoke();
                    Check(calls == 0 && ConfirmationListeners() == listeners, "Cancel preserves held inputs and removes the account listener");
                    EquipmentActionDialog.ShowConfirmation("육성 재개", "입력 2건 · 104 골드", "재개하기", () => calls++);
                    var accepted = Dialog();
                    accepted.confirm.onClick.Invoke(); accepted.confirm.onClick.Invoke();
                    Check(calls == 1 && ConfirmationListeners() == listeners, "Confirmation invokes the resume callback only once");
                    EquipmentActionDialog.ShowConfirmation("육성 재개", "입력 2건 · 104 골드", "재개하기", () => calls++);
                    var stale = Dialog();
                    LocalProgression.OpenTestAccount("economy-ui-next-" + Guid.NewGuid().ToString("N"));
                    stale.confirm.onClick.Invoke();
                    Check(!stale.gameObject.activeSelf && calls == 1 && ConfirmationListeners() == listeners,
                        "Account switch closes the preview and prevents a stale resume callback");
                }
                finally
                {
                    if (canvas != null) UnityEngine.Object.DestroyImmediate(canvas);
                    SetUi(null);
                    if (managers != null) UnityEngine.Object.DestroyImmediate(managers);
                }
            }
            return new Dictionary<string, object> { { "passed", checks.Count }, { "checks", checks } };
        }

        private static void Refresh(MainScreenController controller) =>
            typeof(MainScreenController).GetMethod("RefreshCurrencyPopupContents", Instance).Invoke(controller, null);
        private static int ConfirmationListeners() =>
            ((Delegate)typeof(LocalProgression).GetField("AccountChanged", Static).GetValue(null))?.GetInvocationList()
                .Count(handler => handler.Method.DeclaringType == typeof(EquipmentActionDialog)) ?? 0;
        private static T Get<T>(object target, string name) => (T)target.GetType().GetField(name, Instance).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, Instance).SetValue(target, value);
        private static void SetUi(UIManager value) => typeof(UIManager).GetProperty("Instance").GetSetMethod(true).Invoke(null, new object[] { value });
    }
}
#endif
