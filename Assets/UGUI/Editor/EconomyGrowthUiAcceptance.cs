#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using KingdomIdle.Balance;
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
                fixture = new RapidTapFixture();
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
        public static IEnumerator RunRapidTaps(Action<object> completed = null)
        {
            if (_running) throw new InvalidOperationException("Rapid tap acceptance is already running.");
            _running = true; _cancel = false; LastRapidTapReport = null;
            RapidTapFixture fixture = null;
            try
            {
                fixture = new RapidTapFixture();
                yield return WaitFor(fixture, () => EconomyBatchProgression.RecoveryComplete, "initial recovery");
                fixture.BuildPanel();
                yield return null;
                fixture.Check(fixture.AttackButton.interactable, "Production attack button is ready before the first tap");
                yield return Capture("before-rapid-taps");

                fixture.AttackButton.onClick.Invoke();
                yield return WaitFor(fixture, () => fixture.Transport.Pending, "first HTTP request");
                fixture.Check(fixture.Transport.Request.Commands.Count == 1 && LocalProgression.State.AttackLevel == 0,
                    "First tap reaches isolated HTTP without changing confirmed attack");
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
                fixture.Check(fixture.AttackCard.levelLabel.text == "Lv. 10 (예상)" &&
                    fixture.Body.goldLabel.text.Contains(NumberNotation.Format(expectedGold)) && fixture.Body.goldLabel.text.Contains("예상") &&
                    fixture.Transport.Calls == 1, "Real card and balance labels show the projected ten taps while only one HTTP request is active");
                yield return Capture("projected-ten-taps-before-ack");
                fixture.TopGold.ForceMeshUpdate();
                fixture.Check(fixture.TopGold.text.Contains(NumberNotation.Format(expectedGold)) && !fixture.TopGold.isTextTruncated,
                    "Projected top HUD gold keeps its full amount visible");

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
                fixture.Check(fixture.AttackCard.levelLabel.text == "Lv. 10" && !fixture.Body.goldLabel.text.Contains("예상") &&
                    fixture.Enhance.GetBonusAtkRate() > 0 && fixture.AttackButton.interactable,
                    "Confirmed card removes the estimate marker and enables the acknowledged combat bonus");
                for (int i = 0; i < 5; i++) { fixture.Pump(); yield return null; }
                fixture.Check(fixture.Transport.Calls == 2 && fixture.Transport.Unexpected == 0 && !fixture.Transport.Pending &&
                    fixture.SuccessfulInputs == 10, "Draining the queue does not resend or apply completed taps twice");
                yield return Capture("confirmed-after-two-batches");
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
            private readonly Type _walletType = typeof(EconomyBridge).Assembly.GetType("Scripts.Wallets.Wallet");
            private readonly bool _previousWalletSubscription;
            private readonly Dictionary<eCurrency, long> _previousPublished;
            private IDisposable _account;
            private GameObject _owner, _canvas;
            private UIManager _ui;
            private DevelopmentPanelView _panel;
            private MainScreenController _main;
            private UIViewCatalog _catalog;
            private bool _disposed;

            internal RapidTapFixture()
            {
                if (!Application.isPlaying || LocalProgression.IsReady || NetworkManager.Instance != null || UserManager.Instance != null ||
                    StatEnhanceManager.Instance != null || UIManager.Instance != null || StageManager.Instance != null || ModalBackHandler.HasOpenModal)
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
            internal void Check(bool condition, string message)
            { if (!condition) throw new InvalidOperationException("Rapid tap UI: " + message); Checks.Add(message); }
            private void OnInputCompleted(EconomyInputCompletion result)
            { if (result.Success) SuccessfulInputs++; else throw new InvalidOperationException(result.Message); }

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
                var response = new OnEconomyBatchResponseDTO
                {
                    ProtocolVersion = IdleKingdomFunction.Common.ApiVersions.ProtocolVersion, ServerVersion = 1, EconomyRevision = receipt.CommittedRevision,
                    RequestId = request.RequestId, Status = EconomyBatchStatus.Completed, Receipt = receipt,
                    CurrentState = new()
                    {
                        Currency = new CurrencyQueryDTO(gold, 0, 0, 0), EnchantATKCount = level, Level = 1,
                        RubyGrowthState = new() { NextRequestSequence = 1, LastResetDay = "" }, EconomyBatchState = metadata,
                        StageState = new() { CurrentStage = (Protocol.Message.Data.eStage)state.MainStage, HighestStage = (Protocol.Message.Data.eStage)state.CycleFrontierStage }
                    }
                };
                LastRequestId = request.RequestId;
                var callback = System.Diagnostics.Stopwatch.StartNew(); Transport.Succeed(response); callback.Stop();
                CallbackMilliseconds.Add(callback.Elapsed.TotalMilliseconds);
            }

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
                _panel?.OnClosed?.Invoke();
                if (_main != null) Invoke(_main, "UnhookEconomyChangeHandler");
                if (_canvas != null) UnityEngine.Object.DestroyImmediate(_canvas);
                if (Enhance != null) Invoke(Enhance, "OnDestroy");
                if (NetworkManager.Instance != null) Invoke(NetworkManager.Instance, "ResetCallQueue");
                NetworkManager.Instance = null; UserManager.Instance = null; SetUi(null);
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
                PluginManager.SetPlugin(_previousTransport, PluginContract.PlayFab_Transport);
            }
        }

        private sealed class IsolatedGrowthHttp : ITransportPlugin
        {
            private PlayFab.Internal.CallRequestContainer _pending;
            internal OnEconomyBatchRequestDTO Request;
            internal int Calls, Unexpected;
            internal bool Pending => _pending != null;
            public bool IsInitialized => true;
            public void Initialize() { }
            public void Update() { }
            public void OnDestroy() { }
            public int GetPendingMessages() => Pending ? 1 : 0;
            public void MakeApiCall(object request)
            {
                if (request is not PlayFab.Internal.CallRequestContainer container || container.ApiRequest is not ExecuteFunctionRequest function ||
                    Pending || function.FunctionName != "OnEconomyBatch") { Block(); return; }
                Request = JsonConvert.DeserializeObject<OnEconomyBatchRequestDTO>(JsonConvert.SerializeObject(function.FunctionParameter));
                _pending = container; Calls++;
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
