#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using KingdomIdle.Balance;
using KingdomIdle.Rewards;
using Scripts.Core;
using Scripts.Core.Manager;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Dto = Protocol.Message.Dto;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>서버 DTO를 실제 프리팹에 투영하는 표시/입력 검사. 계정·네트워크·보상 지급을 만들지 않는다.</summary>
    public static class QuestTabAcceptance
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly eQuestCategory[] Categories =
            { eQuestCategory.Guide, eQuestCategory.Daily, eQuestCategory.Weekly, eQuestCategory.Achievement };

        /// <summary>계정·파일을 열지 않는 메모리 fixture로 실제 bulk 실패→조회→명시적 재탭 경로를 검사한다.</summary>
        public static Dictionary<string, object> RunClaimFailureRecovery()
        {
            if (!Application.isPlaying || LocalProgression.IsReady || NetworkManager.Instance != null ||
                UserManager.Instance != null || QuestManager.Instance != null)
                throw new InvalidOperationException("Run quest recovery in empty-scene PlayMode without a profile or game managers.");
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            var stateField = typeof(LocalProgression).GetField("_state", flags);
            var openingField = typeof(LocalProgression).GetField("_opening", flags);
            var pathField = typeof(LocalProgression).GetField("_path", flags);
            object savedState = stateField.GetValue(null), savedOpening = openingField.GetValue(null), savedPath = pathField.GetValue(null);
            var savedNetwork = NetworkManager.Instance; var savedUsers = UserManager.Instance; var savedQuest = QuestManager.Instance;
            var savedTransport = PlayFab.PluginManager.GetPlugin<PlayFab.ITransportPlugin>(PlayFab.PluginContract.PlayFab_Transport);
            var transport = new ClaimFailureHttp();
            long generation = LocalProgression.AccountGeneration; string account = LocalProgression.AccountKey;
            var checks = new List<string>();
            void Check(bool condition, string label)
            { if (!condition) throw new InvalidOperationException("Quest claim recovery: " + label); checks.Add(label); }
            object Call(object target, string name, params object[] values) => target.GetType().GetMethod(name, flags).Invoke(target, values);
            var owner = new GameObject("QuestFailureRecovery_Managers"); owner.SetActive(false);
            try
            {
                PlayFab.PluginManager.SetPlugin(transport, PlayFab.PluginContract.PlayFab_Transport);
                // Ensure의 계정 개방을 막고 읽기 전용 입력조건만 만족시킨다. 성공 DTO/로컬 지급/저장은 호출하지 않는다.
                var memory = new ProgressionState(); openingField.SetValue(null, true); stateField.SetValue(null, memory);
                var network = owner.AddComponent<NetworkManager>(); NetworkManager.Instance = network; Call(network, "Init");
                SetField(network, "_sessionGUID", "quest-recovery-fixture");
                SetField(network, "_context", new PlayFab.PlayFabAuthenticationContext { EntityToken = "quest-recovery-fixture" });
                typeof(NetworkManager).GetProperty("LoginState").SetValue(network, GameLoginState.Ready);
                UserManager.Instance = owner.AddComponent<UserManager>();
                var manager = owner.AddComponent<QuestManager>(); QuestManager.Instance = manager;
                int completed = 0, confirmed = -1, total = 0;
                QuestClaimToken Prepare()
                {
                    SetField(manager, "_claim", null); SetField(manager, "_refreshQueued", false);
                    manager.Apply(State(), "qa-claim-failure-dto");
                    completed = 0; confirmed = -1; total = 0;
                    return manager.GetSnapshot(eQuestCategory.Daily).Rows.First(x => x.CanClaim).Token;
                }
                IReadOnlyList<RewardGrant> completedRewards = null;
                string completedMessage = null;
                Action<int, int, string, IReadOnlyList<RewardGrant>> done = (count, requested, message, rewards) =>
                    { completed++; confirmed = count; total = requested; completedRewards = rewards; completedMessage = message; };
                var individual = Prepare(); int individualCompletions = 0;
                QuestClaimToken completedToken = default;
                Action<QuestClaimToken, bool, string, IReadOnlyList<RewardGrant>> individualDone = (token, success, message, rewards) =>
                    { individualCompletions++; completedToken = token; completedRewards = rewards; };
                manager.OnClaimCompleted += individualDone;
                int beforeIndividual = transport.ClaimCalls;
                Check(manager.TryClaim(individual).Status == QuestClaimStatus.Pending && manager.IsBusy &&
                    transport.ClaimCalls == beforeIndividual + 1 && transport.LastQuestId == individual.QuestId && transport.LastPeriod == individual.Period &&
                    Field<QuestClaimToken[]>(Field<object>(manager, "_claim"), "Tokens").SequenceEqual(new[] { individual }),
                    "Individual claim freezes exactly the displayed token and sends one matching quest/period request");
                Check(manager.TryClaimTab(eQuestCategory.Daily, individual, done).Status == QuestClaimStatus.Busy &&
                    transport.ClaimCalls == beforeIndividual + 1, "Individual claim blocks a simultaneous claim-all request");
                transport.Fail(FixtureError("Business", 1029));
                Check(!manager.IsBusy && individualCompletions == 1 && completedToken == individual && completed == 0 && completedRewards.Count == 0,
                    "Individual completion reports only its own token without invoking the bulk callback");
                manager.OnClaimCompleted -= individualDone;

                Prepare();
                var daily = manager.GetSnapshot(eQuestCategory.Daily);
                var previousPeriod = daily.Rows.Single(x => x.IsPending).Token;
                var expectedTokens = new[] { previousPeriod }.Concat(daily.Rows.Where(x => x.CanClaim && x.Token != previousPeriod)
                    .Select(x => x.Token)).Distinct().ToArray();
                int beforeBulk = transport.ClaimCalls;
                Check(manager.TryClaimTab(eQuestCategory.Daily, previousPeriod, done).Status == QuestClaimStatus.Pending &&
                    transport.ClaimCalls == beforeBulk + 1 && transport.LastQuestId == previousPeriod.QuestId && transport.LastPeriod == previousPeriod.Period &&
                    Field<QuestClaimToken[]>(Field<object>(manager, "_claim"), "Tokens").SequenceEqual(expectedTokens),
                    "Claim-all freezes only current-tab claimable tokens and preserves the selected previous-period identity");
                Check(manager.TryClaim(previousPeriod).Status == QuestClaimStatus.Busy && transport.ClaimCalls == beforeBulk + 1,
                    "Claim-all blocks a simultaneous individual reward request");
                manager.Apply(State(dailyComplete: true), "qa-new-goal-after-claim-all");
                Check(Field<QuestClaimToken[]>(Field<object>(manager, "_claim"), "Tokens").SequenceEqual(expectedTokens) &&
                    !expectedTokens.Any(x => x.QuestId == 20001), "Newly claimable goals do not join the running claim-all operation");
                transport.Fail(FixtureError("Business", 1029));
                Check(!manager.IsBusy && completed == 1 && confirmed == 0 && total == expectedTokens.Length &&
                    individualCompletions == 1 && completedRewards.Count == 0,
                    "Claim-all rejection reports its frozen total and no invented rewards without an individual completion");
                var errors = new (string Name, PlayFab.PlayFabError Error)[]
                {
                    ("StorageFailure1014", FixtureError("Business", 1014)), ("InvalidStoredData1015", FixtureError("Business", 1015)),
                    ("AzureFunctionTimeout1470", new PlayFab.PlayFabError { Error = PlayFab.PlayFabErrorCode.CloudScriptAzureFunctionsExecutionTimeLimitExceeded, ErrorMessage = "fixture" }),
                    ("AzureFunctionHttp1473", new PlayFab.PlayFabError { Error = PlayFab.PlayFabErrorCode.CloudScriptAzureFunctionsHTTPRequestError, ErrorMessage = "fixture" }),
                    ("CloudScriptTimeout1206", new PlayFab.PlayFabError { Error = PlayFab.PlayFabErrorCode.CloudScriptExecutionTimeLimitExceeded, ErrorMessage = "fixture" }),
                    ("CloudScriptHttp", new PlayFab.PlayFabError { Error = PlayFab.PlayFabErrorCode.CloudScriptHTTPRequestError, ErrorMessage = "fixture" }),
                    ("ConnectionError", new PlayFab.PlayFabError { Error = PlayFab.PlayFabErrorCode.ConnectionError, ErrorMessage = "fixture" }),
                    ("Timeout", FixtureError("Timeout")), ("Transport", FixtureError("Transport")), ("Function", FixtureError("Function")),
                    ("InvalidResponse", FixtureError("InvalidResponse")), ("ApplyFailed", FixtureError("ApplyFailed"))
                };
                foreach (var error in errors)
                {
                    var token = Prepare(); int claimCalls = transport.ClaimCalls, readCalls = transport.ReadCalls;
                    var start = manager.TryClaimTab(eQuestCategory.Daily, token, done);
                    Check(start.Status == QuestClaimStatus.Pending && manager.IsBusy && transport.ClaimCalls == claimCalls + 1 &&
                        manager.TryClaimTab(eQuestCategory.Daily, token, done).Status == QuestClaimStatus.Busy,
                        error.Name + ": initial whole-tab request blocks duplicate taps");
                    var original = transport.Pending; transport.Fail(error.Error);
                    Check(manager.IsBusy && completed == 0 && transport.ReadCalls == readCalls + 1 && transport.Pending != null &&
                        manager.TryClaimTab(eQuestCategory.Daily, token, done).Status == QuestClaimStatus.Busy && transport.ClaimCalls == claimCalls + 1,
                        error.Name + ": failure sends one state read and keeps input locked without reclaiming");
                    transport.Fail(FixtureError("Transport"));
                    Check(!manager.IsBusy && completed == 1 && confirmed == 0 && total > 0 && transport.ClaimCalls == claimCalls + 1 &&
                        manager.GetSnapshot(eQuestCategory.Daily).Rows.Any(x => x.Token == token && x.CanClaim) && completedRewards.Count == 0,
                        error.Name + ": failed state read preserves remaining rows and confirms no reward");
                    original.ErrorCallback(error.Error);
                    Check(completed == 1 && transport.Pending == null, error.Name + ": duplicate late response cannot finish or send another request");
                    Check(manager.TryClaimTab(eQuestCategory.Daily, token, done).Status == QuestClaimStatus.Pending &&
                        transport.ClaimCalls == claimCalls + 2 && transport.LastQuestId == token.QuestId && transport.LastPeriod == token.Period,
                        error.Name + ": explicit retap keeps the same quest and period identity");
                    transport.Fail(FixtureError("Business", 1029));
                    Check(!manager.IsBusy && transport.ReadCalls == readCalls + 1 && confirmed == 0 && transport.Pending == null,
                        error.Name + ": server already-claimed rejection stops the bulk without another grant attempt");
                }
                // 완료 전달만 검사한다. 메모리 작업에 확정 표시용 명세를 넣고 성공 응답 적용·저장은 호출하지 않는다.
                var rewardToken = Prepare(); manager.TryClaimTab(eQuestCategory.Daily, rewardToken, done);
                var rewardOperation = Field<object>(manager, "_claim");
                Field<List<Dto.QuestRewardDTO>>(rewardOperation, "Rewards").AddRange(new[]
                    { Reward(eCurrency.Gold, 123), Reward(eCurrency.Gold, 7), Reward(eCurrency.AncientCoin, 2) });
                SetField(rewardOperation, "Confirmed", 1);
                Call(manager, "FinishClaim", rewardOperation, "fixture partial failure");
                Check(completed == 1 && confirmed == 1 && total > confirmed && completedMessage == "fixture partial failure" &&
                    completedRewards.Count == 2 && completedRewards[0].Currency == eCurrency.Gold && completedRewards[0].Amount == 130 &&
                    completedRewards[1].Currency == eCurrency.AncientCoin && completedRewards[1].Amount == 2,
                    "Real claim completion merges only accumulated reward receipts and preserves them alongside a partial-failure message");
                Call(manager, "FinishClaim", rewardOperation, "fixture duplicate"); transport.Fail(FixtureError("Business", 1029));
                Check(completed == 1 && !manager.IsBusy && transport.Pending == null,
                    "Repeated finish and the intercepted request's late rejection cannot deliver the reward receipts twice");
                var claimedToken = Prepare(); manager.Apply(State(allDailyClaimed: true), "qa-server-claimed-dto");
                int beforeClaimed = transport.ClaimCalls;
                Check(manager.TryClaimTab(eQuestCategory.Daily, claimedToken, done).Status == QuestClaimStatus.Incomplete &&
                    transport.ClaimCalls == beforeClaimed && !manager.IsBusy, "Latest server Claimed DTO blocks a stale cell tap before sending");
                var switched = Prepare(); manager.TryClaimTab(eQuestCategory.Daily, switched, done); transport.Fail(errors[2].Error);
                typeof(NetworkManager).GetProperty("AccountGeneration").SetValue(network, network.AccountGeneration + 1);
                transport.Fail(FixtureError("Transport"));
                Check(completed == 0 && manager.IsBusy, "Old authentication-generation state callback cannot finish a newer claim");
                Call(network, "ResetCallQueue"); SetField(manager, "_claim", null);
                Check(ReferenceEquals(stateField.GetValue(null), memory) && memory.Revision == 0 &&
                    Equals(pathField.GetValue(null), savedPath) && LocalProgression.AccountGeneration == generation && LocalProgression.AccountKey == account &&
                    transport.UnexpectedCalls == 0, "Fixture performs no profile open, wallet mutation, saved file change or real HTTP");
                return new Dictionary<string, object> { { "passed", checks.Count }, { "checks", checks } };
            }
            finally
            {
                stateField.SetValue(null, savedState); openingField.SetValue(null, savedOpening);
                UnityEngine.Object.DestroyImmediate(owner);
                NetworkManager.Instance = savedNetwork; UserManager.Instance = savedUsers; QuestManager.Instance = savedQuest;
                PlayFab.PluginManager.SetPlugin(savedTransport, PlayFab.PluginContract.PlayFab_Transport);
            }
        }

        private static PlayFab.PlayFabError FixtureError(string kind, int code = 0)
        {
            object detail = code == 0 ? null : new Dto.ErrorResponseDTO
                { ErrorCode = (Protocol.Message.Data.eErrorCode)code, Message = "fixture" };
            return (PlayFab.PlayFabError)typeof(NetworkManager).GetMethod("CreateError", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { kind, "fixture", "OnClaimQuestReward", detail });
        }

        private sealed class ClaimFailureHttp : PlayFab.ITransportPlugin
        {
            public bool IsInitialized => true;
            public int ClaimCalls, ReadCalls, UnexpectedCalls;
            public long LastQuestId; public string LastPeriod;
            public PlayFab.Internal.CallRequestContainer Pending;
            public void Initialize() { } public void Update() { } public void OnDestroy() { }
            public int GetPendingMessages() => Pending == null ? 0 : 1;
            public void MakeApiCall(object request)
            {
                var container = request as PlayFab.Internal.CallRequestContainer;
                if (container?.ApiRequest is not PlayFab.CloudScriptModels.ExecuteFunctionRequest call || Pending != null)
                { Block(); return; }
                if (call.FunctionName == "OnClaimQuestReward")
                {
                    var payload = Newtonsoft.Json.Linq.JObject.FromObject(call.FunctionParameter);
                    LastQuestId = (long)payload["QuestId"]; LastPeriod = (string)payload["PeriodKey"]; ClaimCalls++;
                }
                else if (call.FunctionName == "OnGetUserState") ReadCalls++;
                else { Block(); return; }
                Pending = container;
            }
            public void Fail(PlayFab.PlayFabError error)
            {
                var pending = Pending ?? throw new InvalidOperationException("Expected an intercepted fixture request.");
                Pending = null; pending.ErrorCallback(error);
            }
            public void SimpleGetCall(string url, Action<byte[]> success, Action<string> error) => Block();
            public void SimplePutCall(string url, byte[] payload, Action<byte[]> success, Action<string> error) => Block();
            public void SimplePostCall(string url, byte[] payload, Action<byte[]> success, Action<string> error) => Block();
            private void Block() { UnexpectedCalls++; throw new InvalidOperationException("Quest fixture blocks external HTTP."); }
        }

        public static Dictionary<string, object> Run()
        {
            if (!Application.isPlaying || LocalProgression.IsReady || NetworkManager.Instance != null || QuestManager.Instance != null)
                throw new InvalidOperationException("Run QuestTabAcceptance in empty-scene PlayMode with no profile, network session or production quest manager.");
            var checks = new List<string>();
            void Check(bool value, string message)
            {
                if (!value) throw new InvalidOperationException("Quest DTO UI acceptance: " + message);
                checks.Add(message);
            }
            long generation = LocalProgression.AccountGeneration;
            string account = LocalProgression.AccountKey;
            var originalUi = UIManager.Instance;
            var originalQuest = QuestManager.Instance;
            GameObject managerRoot = null, uiRoot = null, canvasRoot = null;
            try
            {
                var catalog = AssetDatabase.LoadAssetAtPath<UIViewCatalog>("Assets/UGUI/UIViewCatalog.asset");
                Check(catalog != null && catalog.panelGuide != null && catalog.itemGuideStepRow != null &&
                    catalog.itemNavTabButton != null && catalog.itemGuideEmptyHint != null, "Real catalog references all quest presentation prefabs");
                var rootView = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UGUI/Prefabs/UGUI_UIRoot.prefab").GetComponent<UIManager>();
                Check(rootView.LayerPopups.parent == rootView.LayerOverlays.parent &&
                    rootView.LayerPopups.GetSiblingIndex() < rootView.LayerOverlays.GetSiblingIndex() &&
                    rootView.LayerPopups.GetComponent<Canvas>() == null && rootView.LayerOverlays.GetComponent<Canvas>() == null &&
                    catalog.panelGuide.GetComponentsInChildren<Canvas>(true).All(x => !x.overrideSorting) &&
                    catalog.popupRewardInfo.GetComponentsInChildren<Canvas>(true).All(x => !x.overrideSorting),
                    "Real UI root draws the separate overlay layer after the popup layer without per-layer or quest/reward sorting overrides");
                // 비활성 매니저는 Start/Update/Fetch를 실행하지 않는다. DTO 투영과 변경 발행만 명시적으로 사용한다.
                managerRoot = new GameObject("QuestDtoAcceptance_Manager"); managerRoot.SetActive(false);
                var manager = managerRoot.AddComponent<QuestManager>(); QuestManager.Instance = manager;
                Check(!manager.GetSnapshot(eQuestCategory.Daily).IsReady, "Missing server DTO stays NotReady instead of becoming a local quest board");
                uiRoot = new GameObject("QuestDtoAcceptance_Catalog"); uiRoot.SetActive(false);
                var ui = uiRoot.AddComponent<UIManager>(); SetField(ui, "catalog", catalog); SetUi(ui);
                var data = State(); manager.Apply(data, "qa-dto-presentation-only");
                Check(manager.HasServerData && manager.GetSnapshot(eQuestCategory.Daily).IsReady,
                    "Supplied QuestStateDTO projects to ready snapshots without opening a profile");
                canvasRoot = new GameObject("QuestDtoAcceptance_Canvas", typeof(RectTransform), typeof(Canvas)); canvasRoot.SetActive(false);
                canvasRoot.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                ((RectTransform)canvasRoot.transform).sizeDelta = new Vector2(1080, 1920);
                var popupLayer = new GameObject("LayerPopups", typeof(RectTransform)).GetComponent<RectTransform>();
                var overlayLayer = new GameObject("LayerOverlays", typeof(RectTransform)).GetComponent<RectTransform>();
                foreach (var layer in new[] { popupLayer, overlayLayer })
                {
                    layer.SetParent(canvasRoot.transform, false);
                    layer.anchorMin = Vector2.zero; layer.anchorMax = Vector2.one; layer.sizeDelta = Vector2.zero;
                }
                SetField(ui, "layerPopups", popupLayer); SetField(ui, "layerOverlays", overlayLayer);
                var panelObject = UnityEngine.Object.Instantiate(catalog.panelGuide, overlayLayer, false);
                var view = panelObject.GetComponent<GuidePanelView>();
                var panel = panelObject.GetComponent<BalanceQuestPanel>() ?? panelObject.AddComponent<BalanceQuestPanel>();
                var tabs = view.tabBar; var content = view.listContent; var scroll = view.scroll;
                Check(tabs != null && content != null && scroll != null && view.currentQuestRoot != null &&
                    view.claimBar != null && view.claimAllButton != null && view.claimAllLabel != null && view.claimSummaryLabel != null &&
                    view.claimAllButton.transform.IsChildOf(view.claimBar) && view.claimSummaryLabel.transform.IsChildOf(view.claimBar),
                    "Serialized tabs, list and dedicated claim-bar controls remain connected");
                panelObject.SetActive(true); canvasRoot.SetActive(true); panel.Bind(view); Publish(manager);
                Canvas.ForceUpdateCanvases();
                float Edge(RectTransform rect, bool top) => view.Sheet.InverseTransformPoint(rect.TransformPoint(
                    new Vector3(0, top ? rect.rect.yMax : rect.rect.yMin, 0))).y;
                Check(Edge(tabs, false) >= Edge(view.claimBar, true) - 1 &&
                    Edge(view.claimBar, false) >= Edge((RectTransform)scroll.transform, true) - 1 && view.claimBar.rect.height > 0,
                    "Claim bar occupies its own fixed band below the tabs and above the scroll list");
                Check(panelObject.activeInHierarchy && content.GetComponent<VerticalLayoutGroup>().isActiveAndEnabled,
                    "UGUI geometry fixture is active for layout calculations");
                var buttons = tabs.GetComponentsInChildren<Button>(false);
                var tabViews = tabs.GetComponentsInChildren<NavTabButtonView>(false);
                Check(buttons.Length == 4 && tabViews.Select(x => Field<TMP_Text>(x, "label").text)
                    .SequenceEqual(new[] { "가이드", "일일", "주간", "업적" }), "Four real tab buttons retain their approved order and labels");
                Check(panel.SelectedCategory == eQuestCategory.Guide && !view.currentQuestRoot.activeSelf,
                    "Initial Guide selection hides only the redundant popup summary");
                var dots = Field<List<GameObject>>(panel, "_tabDots");
                Check(catalog.circle != null && dots.Count == 4 && dots.Select((dot, index) => dot != null &&
                    dot.transform.parent == buttons[index].transform && dot.GetComponent<Image>().sprite == catalog.circle &&
                    dot.GetComponent<Image>().color == UguiTheme.DangerRedBright && !dot.GetComponent<Image>().raycastTarget).All(x => x),
                    "Each tab reuses the existing circle notification asset without intercepting input");
                Check(dots.Select(x => x.activeSelf).SequenceEqual(new[] { false, true, false, true }) &&
                    !view.claimAllButton.interactable && view.claimAllLabel.text.StartsWith("모두받기", StringComparison.Ordinal),
                    "Initial tab dots represent unclaimed rewards and an empty Guide disables claim-all");
                foreach (var category in Categories)
                {
                    Click(buttons[(int)category]);
                    Check(panel.SelectedCategory == category && Visible(panel).Select(x => x.Token).SequenceEqual(
                        manager.GetSnapshot(category).Rows.OrderBy(x => Order(x.State)).Select(x => x.Token)), category + " shows only the selected tab's latest DTO rows");
                    Check(tabViews.Select(x => Field<Image>(x, "selectedFrame").color.a > 0).SequenceEqual(Categories.Select(x => x == category)),
                        category + " has exactly one matching selected tab");
                    Check(content.GetComponentsInChildren<GuideStepRowView>(false).All(x => x.actionButton.gameObject != x.gameObject &&
                        (x.GetComponent<Button>() == null || !x.GetComponent<Button>().enabled) &&
                        x.checkButton == x.actionButton && x.GetComponentsInChildren<Button>(false).Count(button => button.enabled) == 1 && x.claimBorder != null) &&
                        Visible(panel).Where(x => x.CanClaim).All(x => RowView(panel, x.Token.QuestId).actionLabel.text == "보상받기"),
                        category + " uses a separate individual reward action while the card remains display-only");
                    int claimable = manager.GetSnapshot(category).Rows.Count(x => x.CanClaim);
                    Check(view.claimAllButton.interactable == (claimable > 0) &&
                        view.claimSummaryLabel.text.Contains(new[] { "가이드", "일일", "주간", "업적" }[(int)category]) &&
                        view.claimSummaryLabel.text.Contains(claimable.ToString()), category + " summary and claim-all use only the selected tab's claimable count");
                    Check(dots.Select(x => x.activeSelf).SequenceEqual(new[] { false, true, false, true }),
                        category + " selection alone does not clear unclaimed-reward dots");
                }
                Check(!manager.GetSnapshot(eQuestCategory.Guide).Rows.Any(x => x.State == QuestRowState.Locked) &&
                    !manager.GetSnapshot(eQuestCategory.Achievement).Rows.Any(x => x.State == QuestRowState.Locked), "Locked guide/achievement chain steps remain hidden by the real DTO projection");
                Click(buttons[1]); int[] rowIds = ActiveIds(content); scroll.verticalNormalizedPosition = .37f;
                Click(buttons[1]); panel.SelectCategory(eQuestCategory.Daily);
                Check(rowIds.SequenceEqual(ActiveIds(content)) && Mathf.Abs(scroll.verticalNormalizedPosition - .37f) < .001f,
                    "Reselecting the current tab preserves rows and scroll position");
                for (int i = 0; i < 3; i++) panel.Bind(view);
                Check(tabs.GetComponentsInChildren<Button>(false).Length == 4 && rowIds.SequenceEqual(ActiveIds(content)) &&
                    panelObject.GetComponents<NumberNotationBinding>().Length == 1 && dots.Count == 4 &&
                    buttons.All(x => x.GetComponentsInChildren<Image>(true).Count(image => image.gameObject.name == "NotificationDot") == 1) &&
                    view.claimBar.GetComponentsInChildren<Button>(true).Length == 1,
                    "Repeated Bind reuses tabs, dots, row objects, claim-all control and the single notation binding");

                var claimView = RowView(panel, 20002); var token = Visible(panel).Single(x => x.Token.QuestId == 20002).Token;
                Check(claimView.actionButton.interactable && claimView.actionLabel.text == "보상받기" &&
                    view.claimAllButton.interactable && view.claimSummaryLabel.text.Contains("3"), "Claimable DTO enables individual reward and current-tab claim-all controls");
                int clicks = 0; UnityEngine.Events.UnityAction observer = () => clicks++; claimView.actionButton.onClick.AddListener(observer);
                ExecuteEvents.Execute(claimView.gameObject, new PointerEventData(null)
                    { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
                Check(clicks == 0 && !manager.IsBusy && ReferenceEquals(manager.ServerQuests, data),
                    "Clicking a quest card background cannot request any reward");
                Click(claimView.actionButton);
                Check(clicks == 1 && !manager.IsBusy && ReferenceEquals(manager.ServerQuests, data) &&
                    manager.TryClaim(token).Status == QuestClaimStatus.NotReady,
                    "Only the row button reaches the guarded individual route; no profile means no request or invented reward");
                int bulkClicks = 0; UnityEngine.Events.UnityAction bulkObserver = () => bulkClicks++;
                view.claimAllButton.onClick.AddListener(bulkObserver);
                Click(view.claimAllButton);
                Check(bulkClicks == 1 && !manager.IsBusy && ReferenceEquals(manager.ServerQuests, data),
                    "Dedicated claim-all button reaches its guarded route without creating a profile or local reward");
                // 거래를 시작하지 않고 기존 IsBusy 표시만 고정한다. 비활성 매니저는 전송·재시도를 실행하지 않는다.
                SetBusyPresentation(manager, true); Tick(panel);
                Check(!claimView.actionButton.interactable && claimView.actionLabel.text == "수령 중…" &&
                    !view.claimAllButton.interactable && view.claimAllLabel.text == "수령 중…" && dots[1].activeSelf,
                    "Busy presentation disables both reward actions and retains the unconfirmed-reward dot");
                Click(claimView.actionButton); Click(claimView.actionButton);
                Click(view.claimAllButton); Click(view.claimAllButton);
                Check(clicks == 1 && bulkClicks == 1, "Repeated pointer clicks on busy reward buttons do not invoke their UnityEvents");
                SetBusyPresentation(manager, false); Tick(panel);
                Check(claimView.actionButton.interactable && claimView.actionLabel.text == "보상받기" &&
                    view.claimAllButton.interactable && view.claimAllLabel.text.StartsWith("모두받기", StringComparison.Ordinal), "Clearing busy restores individual and claim-all controls for the same displayed rewards");
                view.claimAllButton.onClick.RemoveListener(bulkObserver);
                // 계정을 바꾸지 않고 화면에 남은 옛 세대 token만 구성하여 실제 셀의 방어 경로를 실행한다.
                foreach (object binding in Field<IEnumerable>(panel, "_rows"))
                {
                    var shown = Field<QuestRowSnapshot>(binding, "Snapshot");
                    if (shown.Token != token) continue;
                    SetField(binding, "Snapshot", new QuestRowSnapshot(new QuestClaimToken(generation + 1, token.QuestId, token.Period),
                        shown.Category, shown.Title, shown.Description, shown.ObjectiveType, shown.TargetId, shown.RequiredCount,
                        shown.Progress, shown.State, shown.IsPending, shown.ExpiresUtc, shown.Rewards, shown.PresentationType));
                    break;
                }
                Click(claimView.actionButton);
                Check(Visible(panel).Single(x => x.Token.QuestId == token.QuestId).Token == token && !manager.IsBusy &&
                    ReferenceEquals(manager.ServerQuests, data) && LocalProgression.AccountGeneration == generation,
                    "Stale-generation row input restores the current DTO token without switching an account or claiming");
                rowIds = ActiveIds(content);
                claimView.actionButton.onClick.RemoveListener(observer);
                var route = Members(typeof(BalanceQuestPanel).GetMethod("Claim", Private)).ToArray();
                Check(route.OfType<MethodBase>().Any(x => x.DeclaringType == typeof(QuestManager) && x.Name == "TryClaim") &&
                    !route.OfType<MethodBase>().Any(x => x.DeclaringType == typeof(QuestManager) && x.Name == "TryClaimTab"),
                    "Compiled individual row route calls TryClaim and never the whole-tab operation");
                var bulkRoute = Members(typeof(BalanceQuestPanel).GetMethod("ClaimAll", Private)).ToArray();
                Check(bulkRoute.OfType<MethodBase>().Any(x => x.DeclaringType == typeof(QuestManager) && x.Name == "TryClaimTab") &&
                    bulkRoute.OfType<FieldInfo>().Any(x => x.DeclaringType == typeof(BalanceQuestPanel) && x.Name == "_selectedCategory"),
                    "Compiled dedicated claim-all route calls TryClaimTab using the selected category");
                Check(Members(typeof(BalanceQuestPanel).GetMethod("Act", Private)).OfType<MethodBase>()
                    .Any(x => x.DeclaringType == typeof(LocalProgression) && x.Name == "get_AccountGeneration"), "Compiled row action retains its account-generation gate");
                var pending = Visible(panel).Single(x => x.IsPending);
                Check(RowView(panel, pending.Token.QuestId).periodLabel.text.Contains("2026-11-11"), "Previous-period reward retains its server period label");
                Check(RowView(panel, 20001).rewardAmountLabel.text == "2분\n골드" && RowView(panel, 20001).rewardAmountLabel.rectTransform.rect.height >= 88,
                    "Dynamic gold displays duration and currency without inventing a payout amount");
                Check(RowView(panel, 20010).secondaryRewardIcon.transform.parent.gameObject.activeSelf, "Two server reward DTOs remain visible as distinct icons");
                var completed = RowView(panel, 20004); var locked = RowView(panel, 20005);
                Check(completed.actionLabel.text == "완료" && completed.progressFill.fillAmount == 1 && !completed.actionButton.interactable &&
                    locked.actionLabel.text == "잠김" && !locked.actionButton.interactable, "Claimed and Locked DTO rows retain distinct disabled states");

                data = State(weeklyProgress: 9); manager.Apply(data, "qa-dto-other-category"); Publish(manager);
                Check(panel.SelectedCategory == eQuestCategory.Daily && rowIds.SequenceEqual(ActiveIds(content)),
                    "Unrelated Weekly DTO changes do not rebuild the open Daily rows");
                data = State(); data.Rows.Single(x => x.QuestId == 30001).State = Dto.QuestRowState.Claimable;
                manager.Apply(data, "qa-closed-tab-claimable"); Publish(manager);
                Check(dots[2].activeSelf && panel.SelectedCategory == eQuestCategory.Daily && rowIds.SequenceEqual(ActiveIds(content)) &&
                    view.claimSummaryLabel.text.Contains("3"), "An unselected tab's new reward lights its dot without changing the current tab or count");
                data.Rows.Single(x => x.QuestId == 30001).State = Dto.QuestRowState.Claimed;
                manager.Apply(data, "qa-closed-tab-claimed"); Publish(manager);
                Check(!dots[2].activeSelf && dots[1].activeSelf && dots[3].activeSelf && rowIds.SequenceEqual(ActiveIds(content)),
                    "An unselected tab's confirmed claim clears only its own dot without rebuilding the current rows");
                var drag = scroll.GetComponent<QuestScrollDragRelay>(); var pointer = new PointerEventData(null) { button = PointerEventData.InputButton.Left };
                drag.OnBeginDrag(pointer); rowIds = ActiveIds(content);
                data = State(dailyComplete: true); manager.Apply(data, "qa-dto-during-drag"); Publish(manager);
                Check(rowIds.SequenceEqual(ActiveIds(content)) && RowView(panel, 20001).actionButton.interactable,
                    "New DTO availability updates while physical rows remain stable during drag");
                drag.OnEndDrag(pointer);
                Check(Visible(panel)[0].Token.QuestId == 20001 && rowIds.OrderBy(x => x).SequenceEqual(ActiveIds(content).OrderBy(x => x)),
                    "Drag release reorders existing row objects using the latest DTO");
                panelObject.SetActive(false); data = State(allDailyClaimed: true); manager.Apply(data, "qa-dto-claimed-display"); Publish(manager); panelObject.SetActive(true);
                Check(panel.SelectedCategory == eQuestCategory.Daily && Visible(panel).All(x => x.State == QuestRowState.Claimed) &&
                    content.GetComponentsInChildren<GuideStepRowView>(false).All(x => x.progressFill.fillAmount == 1 && !x.actionButton.interactable) &&
                    !dots[1].activeSelf && dots[3].activeSelf && !view.claimAllButton.interactable && view.claimSummaryLabel.text.Contains("0"),
                    "Re-enable reads supplied Claimed DTO rows and preserves their cards; reward payment is not exercised");
                data = State(day: "2026-11-13"); manager.Apply(data, "qa-dto-new-period"); Publish(manager);
                Check(Visible(panel).Where(x => !x.IsPending).All(x => x.Token.Period == "2026-11-13") &&
                    Visible(panel).Any(x => x.IsPending && x.Token.Period == "2026-11-11"), "New server periods replace current tokens while preserving supplied pending tokens");
                CheckRewardPopups(ui, manager, panel, token, Check);
                Check(!LocalProgression.IsReady && LocalProgression.AccountGeneration == generation && LocalProgression.AccountKey == account && NetworkManager.Instance == null,
                    "Fixture never opens/switches a profile, creates a network session or changes account generation");
            }
            finally
            {
                if (canvasRoot != null) canvasRoot.SetActive(false);
                QuestManager.Instance = originalQuest; SetUi(originalUi);
                if (canvasRoot != null) UnityEngine.Object.DestroyImmediate(canvasRoot);
                if (managerRoot != null) UnityEngine.Object.DestroyImmediate(managerRoot);
                if (uiRoot != null) UnityEngine.Object.DestroyImmediate(uiRoot);
            }
            Check(QuestManager.Instance == originalQuest && UIManager.Instance == originalUi && !LocalProgression.IsReady,
                "Cleanup restores original singletons and leaves no open profile or fixture listeners");
            return new Dictionary<string, object> { { "passed", checks.Count }, { "checks", checks },
                { "fixture", "Actual prefabs and QuestStateDTO projection; inactive manager; active geometry; no account/network/payment" },
                { "not_exercised", new[] { "Server claim success/failure", "Bulk reward payment/durable receipts", "Live account switching", "Backend period transition" } } };
        }

        private static void CheckRewardPopups(UIManager ui, QuestManager manager, BalanceQuestPanel panel,
            QuestClaimToken token, Action<bool, string> check)
        {
            // 지급 완료 이벤트의 표시만 호출한다. 수령 요청·성공 DTO 적용·계정 저장은 실행하지 않는다.
            object Call(object owner, string method, params object[] values) => owner.GetType().GetMethod(method, Private).Invoke(owner, values);
            RewardPopupView[] Popups() => ui.LayerOverlays.GetComponentsInChildren<RewardPopupView>(false);
            void Clear() { foreach (var popup in Popups()) UnityEngine.Object.DestroyImmediate(popup.gameObject); }
            var rewards = RewardGrants.FromServer(new[]
            {
                Reward(eCurrency.Gold, 123, true), Reward(eCurrency.AncientCoin, 2), Reward(eCurrency.Gold, 7)
            });
            check(rewards.Count == 2 && rewards[0].Currency == eCurrency.Gold && rewards[0].Amount == 130 &&
                rewards[1].Currency == eCurrency.AncientCoin && rewards[1].Amount == 2 && rewards.All(x => x.ServerReward),
                "Quest payout conversion preserves the server dynamic-gold amount and merges matching received currencies");
            check(RewardGrants.FromServer(new Dto.QuestRewardDTO[]
            {
                null, Reward(eCurrency.Gold, 0), Reward(eCurrency.Gold, -1), Reward(eCurrency.Gold, 1.5m),
                Reward(eCurrency.Gold, (decimal)long.MaxValue + 1)
            }).Count == 0 && RewardGrants.FromServer((IEnumerable<Dto.QuestRewardDTO>)null).Count == 0,
                "Quest payout conversion skips absent, nonpositive, fractional and overflowing amounts without inventing rewards");

            SetField(panel, "_pendingClaim", (QuestClaimToken?)token);
            Call(panel, "OnClaimCompleted", new QuestClaimToken(token.AccountGeneration, token.QuestId + 1, token.Period), true, null, rewards);
            check(Popups().Length == 0, "A different individual completion cannot show this panel's reward popup");
            Call(panel, "OnClaimCompleted", token, true, null, rewards);
            var individual = Popups().Single();
            var slots = individual.slots.GetComponentsInChildren<RewardSlotView>(false);
            check(individual.transform.parent == ui.LayerOverlays && individual.transform.GetSiblingIndex() > panel.transform.GetSiblingIndex() &&
                ui.LayerPopups.childCount == 0 && slots.Length == 2 &&
                slots[0].quantity.text == NumberNotation.Format(130) && slots[0].icon.sprite == ui.Catalog.iconCoin &&
                slots[1].quantity.text == NumberNotation.Format(2) && slots[1].icon.sprite == ui.Catalog.iconAncientCoin,
                "Individual quest reward draws above the quest in the overlay layer and reuses exact received icons and amounts");
            Call(panel, "OnClaimCompleted", token, true, null, rewards);
            check(Popups().Length == 1, "A duplicate individual completion cannot open another reward popup");
            Clear();
            SetField(panel, "_pendingClaim", (QuestClaimToken?)token);
            Call(panel, "OnClaimCompleted", token, false, "fixture failure", Array.Empty<RewardGrant>());
            SetField(panel, "_pendingClaim", (QuestClaimToken?)token);
            Call(panel, "OnClaimCompleted", token, true, null, Array.Empty<RewardGrant>());
            check(Popups().Length == 0, "Failed or rewardless individual completion does not open an empty reward popup");

            var hudObject = new GameObject("QuestRewardPresentation_Hud"); hudObject.SetActive(false);
            hudObject.transform.SetParent(ui.LayerOverlays, false);
            try
            {
                var hud = hudObject.AddComponent<GuideGoalView>();
                SetField(hud, "_manager", manager);
                var guide = manager.GetSnapshot(eQuestCategory.Guide).Rows.First().Token;
                SetField(hud, "_claimToken", guide); SetField(hud, "_claimRequested", true);
                SetField(hud, "_requestedClaimToken", guide);
                Call(hud, "OnClaimCompleted", token, true, null, rewards);
                check(Popups().Length == 0 && Field<bool>(hud, "_claimRequested"),
                    "A different quest completion cannot consume the guide HUD's pending reward presentation");
                SetField(hud, "_claimToken", token);
                Call(hud, "OnClaimCompleted", guide, true, null, rewards);
                Call(hud, "OnClaimCompleted", guide, true, null, rewards);
                check(Popups().Length == 1 && Popups()[0].transform.parent == ui.LayerOverlays &&
                    Popups()[0].transform.GetSiblingIndex() > panel.transform.GetSiblingIndex() && ui.LayerPopups.childCount == 0 &&
                    Popups()[0].slots.GetComponentsInChildren<RewardSlotView>(false).Length == 2,
                    "Rebinding the displayed guide during a request preserves its original reward popup and duplicate-completion guard");
                Clear();
                SetField(hud, "_claimRequested", true);
                Call(hud, "OnClaimCompleted", guide, false, "fixture failure", Array.Empty<RewardGrant>());
                SetField(hud, "_claimRequested", true);
                Call(hud, "OnClaimCompleted", guide, true, null, Array.Empty<RewardGrant>());
                check(Popups().Length == 0, "Failed or rewardless guide completion does not show a reward popup");
            }
            finally { UnityEngine.Object.DestroyImmediate(hudObject); }

            Call(panel, "OnTabClaimCompleted", 2, 2, null, rewards);
            check(Popups().Length == 1 && Popups()[0].transform.parent == ui.LayerOverlays &&
                Popups()[0].transform.GetSiblingIndex() > panel.transform.GetSiblingIndex() && ui.LayerPopups.childCount == 0 &&
                Popups()[0].slots.GetComponentsInChildren<RewardSlotView>(false).Length == 2,
                "Claim-all completion shows one combined popup above the quest rather than in the lower popup layer");
            Clear();
            Call(panel, "OnTabClaimCompleted", 1, 2, "fixture partial failure", rewards);
            check(Popups().Length == 1 && Popups()[0].slots.GetComponentsInChildren<RewardSlotView>(false)[0].quantity.text == NumberNotation.Format(130),
                "Partially failed claim-all still displays the rewards already confirmed by successful responses");
            Clear();
            Call(panel, "OnTabClaimCompleted", 0, 2, "fixture failure", Array.Empty<RewardGrant>());
            Call(panel, "OnTabClaimCompleted", 1, 1, null, Array.Empty<RewardGrant>());
            check(Popups().Length == 0, "Failed or rewardless claim-all does not show a fabricated payout popup");
        }

        private static Dto.QuestStateDTO State(string day = "2026-11-12", long weeklyProgress = 1, bool dailyComplete = false, bool allDailyClaimed = false)
        {
            var rows = new List<Dto.QuestRowDTO>
            {
                Row(10015, Dto.QuestCategory.Guide, "permanent", Dto.QuestRowState.InProgress, 0, 1),
                Row(10016, Dto.QuestCategory.Guide, "permanent", Dto.QuestRowState.Locked, 0, 1),
                Row(20001, Dto.QuestCategory.Daily, day, dailyComplete ? Dto.QuestRowState.Claimable : Dto.QuestRowState.InProgress, dailyComplete ? 10 : 5, 10, dynamicGold: true),
                Row(20002, Dto.QuestCategory.Daily, day, Dto.QuestRowState.Claimable, 1, 1),
                Row(20003, Dto.QuestCategory.Daily, day, Dto.QuestRowState.Claimable, 1, 1),
                Row(20004, Dto.QuestCategory.Daily, day, Dto.QuestRowState.Claimed, 0, 1),
                Row(20005, Dto.QuestCategory.Daily, day, Dto.QuestRowState.Locked, 0, 1),
                Row(20006, Dto.QuestCategory.Daily, "2026-11-11", Dto.QuestRowState.Claimable, 1, 1, pending: true),
                Row(20010, Dto.QuestCategory.Daily, day, Dto.QuestRowState.InProgress, 1, 2),
                Row(30001, Dto.QuestCategory.Weekly, "2026-11-09", Dto.QuestRowState.InProgress, weeklyProgress, 10),
                Row(40401, Dto.QuestCategory.Achievement, "permanent", Dto.QuestRowState.Claimable, 1, 1),
                Row(40402, Dto.QuestCategory.Achievement, "permanent", Dto.QuestRowState.Locked, 0, 1)
            };
            rows.Single(x => x.QuestId == 20010).Rewards.Add(Reward(eCurrency.ArcaneKnowledge, 2));
            if (allDailyClaimed) foreach (var row in rows.Where(x => x.Category == Dto.QuestCategory.Daily)) row.State = Dto.QuestRowState.Claimed;
            return new Dto.QuestStateDTO { ServerTimeUtc = 1794448800, DailyPeriodKey = day, DailyResetUtc = 1794495600,
                WeeklyPeriodKey = "2026-11-09", WeeklyResetUtc = 1794754800, Rows = rows };
        }
        private static Dto.QuestRowDTO Row(long id, Dto.QuestCategory category, string period, Dto.QuestRowState state, long progress, long required, bool dynamicGold = false, bool pending = false) =>
            new Dto.QuestRowDTO { QuestId = id, Category = category, PeriodKey = period, State = state, Progress = progress, RequiredCount = required,
                Title = "표시 검사 " + id, Description = "몬스터 " + required + "마리 처치", PresentationType = "None", ObjectiveType = "MonsterKill",
                TargetId = 0, IsPending = pending, ExpiresUtc = pending ? 1794754800 : 0,
                Rewards = new List<Dto.QuestRewardDTO> { Reward(dynamicGold ? eCurrency.Gold : eCurrency.AncientCoin, dynamicGold ? 0 : 5, dynamicGold) } };
        private static Dto.QuestRewardDTO Reward(eCurrency currency, decimal amount, bool dynamic = false) => new Dto.QuestRewardDTO
            { Kind = Dto.RewardKind.Currency, CurrencyType = (Protocol.Message.Data.eCurrency)(int)currency, Amount = amount, IsDynamicGold = dynamic };
        private static int Order(QuestRowState state) => state == QuestRowState.Claimable ? 0 : state == QuestRowState.InProgress ? 1 : state == QuestRowState.Locked ? 2 : 3;
        private static void Click(Button button) => ExecuteEvents.Execute(button.gameObject, new PointerEventData(null)
            { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
        private static void Publish(QuestManager manager) => typeof(QuestManager).GetMethod("LateUpdate", Private).Invoke(manager, null);
        private static void Tick(BalanceQuestPanel panel) => typeof(BalanceQuestPanel).GetMethod("Update", Private).Invoke(panel, null);
        private static void SetBusyPresentation(QuestManager manager, bool busy) => typeof(QuestManager).GetField("_claim", Private).SetValue(manager,
            busy ? Activator.CreateInstance(typeof(QuestManager).GetNestedType("ClaimOperation", BindingFlags.NonPublic), true) : null);
        private static int[] ActiveIds(Transform content) => content.GetComponentsInChildren<GuideStepRowView>(false).Select(x => x.GetInstanceID()).ToArray();
        private static IReadOnlyList<QuestRowSnapshot> Visible(BalanceQuestPanel panel) => Field<List<QuestRowSnapshot>>(panel, "_visible");
        private static GuideStepRowView RowView(BalanceQuestPanel panel, long id)
        {
            foreach (object row in Field<IEnumerable>(panel, "_rows"))
                if (Field<QuestRowSnapshot>(row, "Snapshot").Token.QuestId == id) return Field<GuideStepRowView>(row, "View");
            throw new InvalidOperationException("Missing DTO row " + id);
        }
        private static T Field<T>(object value, string name) => (T)value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(value);
        private static void SetField(object value, string name, object field) => value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(value, field);
        private static void SetUi(UIManager manager) => typeof(UIManager).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, manager);

        // 컴파일된 실제 입력 경로만 읽는다. 수령 메서드 교체나 가짜 서버 성공을 만들지 않는다.
        private static IEnumerable<MemberInfo> Members(MethodInfo method)
        {
            var codes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static).Where(x => x.FieldType == typeof(OpCode))
                .Select(x => (OpCode)x.GetValue(null)).GroupBy(x => unchecked((ushort)x.Value)).ToDictionary(x => x.Key, x => x.First());
            var bytes = method.GetMethodBody().GetILAsByteArray(); int offset = 0;
            while (offset < bytes.Length)
            {
                ushort code = bytes[offset++]; if (code == 0xfe) code = (ushort)(0xfe00 | bytes[offset++]);
                var op = codes[code];
                if (op.OperandType == OperandType.InlineMethod || op.OperandType == OperandType.InlineField)
                    yield return method.Module.ResolveMember(BitConverter.ToInt32(bytes, offset));
                offset += op.OperandType switch
                {
                    OperandType.InlineNone => 0,
                    OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                    OperandType.InlineVar => 2,
                    OperandType.InlineI8 or OperandType.InlineR => 8,
                    OperandType.InlineSwitch => 4 + BitConverter.ToInt32(bytes, offset) * 4,
                    _ => 4
                };
            }
        }
    }
}
#endif
