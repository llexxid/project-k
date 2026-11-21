#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using KingdomIdle.Balance;
using KingdomIdle.MageTower;
using Newtonsoft.Json;
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
        private static bool _continue, _cancel, _running;
        public static void Continue() => _continue = true;
        public static void CancelRapidTaps() => _cancel = true;

        /// <summary>빈 PlayMode에서 코루틴으로 실행한다. 캡처 대기는 체크포인트마다 45초로 제한한다.</summary>
        public static IEnumerator RunRapidTaps(Action<object> completed = null) => RunTaps(false, completed);
        public static IEnumerator RunStorageRetry(Action<object> completed = null) => RunTaps(true, completed);

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
            return new { passed = checks.Count, checks };
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
            private GameObject _owner, _canvas;
            private UIManager _ui;
            private DevelopmentPanelView _panel;
            private MainScreenController _main;
            private UIViewCatalog _catalog;
            private QuestManager _quests;
            private QuestRowSnapshot _pendingQuest;
            private QuestBoardSnapshot _guideBoard;
            private MageTowerManager _mage;
            private bool _disposed;

            internal RapidTapFixture(bool throttle, bool mage = false)
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
                    typeof(EconomyBatchProgression).GetField("_clientId", Static).SetValue(null, Guid.NewGuid().ToString("N"));
                    _account = LocalProgression.BeginTestSession();
                    LocalProgression.OpenTestAccount("economy-rapid-ui-" + Guid.NewGuid().ToString("N"));
                    _owner = new GameObject("EconomyRapidTapAcceptance_Managers"); _owner.SetActive(false);
                    var network = _owner.AddComponent<NetworkManager>(); NetworkManager.Instance = network;
                    Invoke(network, "Init");
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

            internal void Check(bool condition, string message, bool log = true)
            { if (!condition) throw new InvalidOperationException("Rapid tap UI: " + message); if (log) Checks.Add(message); }
            private void OnInputCompleted(EconomyInputCompletion result)
            { if (result.Success) SuccessfulInputs++; else throw new InvalidOperationException(result.Message); }

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
            internal void Succeed(OnEconomyBatchResponseDTO response)
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
