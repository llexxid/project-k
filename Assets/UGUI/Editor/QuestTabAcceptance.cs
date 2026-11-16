#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using KingdomIdle.Balance;
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
                Action<int, int, string> done = (count, requested, message) => { completed++; confirmed = count; total = requested; };
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
                        manager.GetSnapshot(eQuestCategory.Daily).Rows.Any(x => x.Token == token && x.CanClaim),
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
                var panelObject = UnityEngine.Object.Instantiate(catalog.panelGuide, canvasRoot.transform, false);
                var view = panelObject.GetComponent<GuidePanelView>();
                var panel = panelObject.GetComponent<BalanceQuestPanel>() ?? panelObject.AddComponent<BalanceQuestPanel>();
                var tabs = view.tabBar; var content = view.listContent; var scroll = view.scroll;
                Check(tabs != null && content != null && scroll != null && view.currentQuestRoot != null,
                    "Serialized tab, list, scroll and legacy summary references remain connected");
                panelObject.SetActive(true); canvasRoot.SetActive(true); panel.Bind(view); Publish(manager);
                Check(panelObject.activeInHierarchy && content.GetComponent<VerticalLayoutGroup>().isActiveAndEnabled,
                    "UGUI geometry fixture is active for layout calculations");
                var buttons = tabs.GetComponentsInChildren<Button>(false);
                var tabViews = tabs.GetComponentsInChildren<NavTabButtonView>(false);
                Check(buttons.Length == 4 && tabViews.Select(x => Field<TMP_Text>(x, "label").text)
                    .SequenceEqual(new[] { "가이드", "일일", "주간", "업적" }), "Four real tab buttons retain their approved order and labels");
                Check(panel.SelectedCategory == eQuestCategory.Guide && !view.currentQuestRoot.activeSelf,
                    "Initial Guide selection hides only the redundant popup summary");
                foreach (var category in Categories)
                {
                    Click(buttons[(int)category]);
                    Check(panel.SelectedCategory == category && Visible(panel).Select(x => x.Token).SequenceEqual(
                        manager.GetSnapshot(category).Rows.OrderBy(x => Order(x.State)).Select(x => x.Token)), category + " shows only the selected tab's latest DTO rows");
                    Check(tabViews.Select(x => Field<Image>(x, "selectedFrame").color.a > 0).SequenceEqual(Categories.Select(x => x == category)),
                        category + " has exactly one matching selected tab");
                    Check(content.GetComponentsInChildren<GuideStepRowView>(false).All(x => x.actionButton.gameObject == x.gameObject &&
                        x.checkButton == x.actionButton && x.GetComponentsInChildren<Button>(false).Length == 1 && x.claimBorder != null &&
                        !x.actionLabel.text.Contains("받기")), category + " uses one whole-cell action and a claim border");
                }
                Check(!manager.GetSnapshot(eQuestCategory.Guide).Rows.Any(x => x.State == QuestRowState.Locked) &&
                    !manager.GetSnapshot(eQuestCategory.Achievement).Rows.Any(x => x.State == QuestRowState.Locked), "Locked guide/achievement chain steps remain hidden by the real DTO projection");
                Click(buttons[1]); int[] rowIds = ActiveIds(content); scroll.verticalNormalizedPosition = .37f;
                Click(buttons[1]); panel.SelectCategory(eQuestCategory.Daily);
                Check(rowIds.SequenceEqual(ActiveIds(content)) && Mathf.Abs(scroll.verticalNormalizedPosition - .37f) < .001f,
                    "Reselecting the current tab preserves rows and scroll position");
                for (int i = 0; i < 3; i++) panel.Bind(view);
                Check(tabs.GetComponentsInChildren<Button>(false).Length == 4 && rowIds.SequenceEqual(ActiveIds(content)) &&
                    panelObject.GetComponents<NumberNotationBinding>().Length == 1, "Repeated Bind reuses tabs, row objects and the single notation binding");

                var claimView = RowView(panel, 20002); var token = Visible(panel).Single(x => x.Token.QuestId == 20002).Token;
                Check(claimView.actionButton.interactable && claimView.actionLabel.text == "수령 가능", "Claimable DTO enables the whole cell");
                int clicks = 0; UnityEngine.Events.UnityAction observer = () => clicks++; claimView.actionButton.onClick.AddListener(observer);
                Click(claimView.actionButton);
                Check(clicks == 1 && !manager.IsBusy && ReferenceEquals(manager.ServerQuests, data) &&
                    manager.TryClaimTab(eQuestCategory.Daily, token, null).Status == QuestClaimStatus.NotReady,
                    "Whole-cell pointer reaches the guarded route; no account means no request or locally invented reward");
                // 거래를 시작하지 않고 기존 IsBusy 표시만 고정한다. 비활성 매니저는 전송·재시도를 실행하지 않는다.
                SetBusyPresentation(manager, true); Tick(panel);
                Check(!claimView.actionButton.interactable && claimView.actionLabel.text == "수령 중…", "Busy presentation disables duplicate reward input");
                Click(claimView.actionButton); Click(claimView.actionButton);
                Check(clicks == 1, "Two pointer clicks on the busy cell do not invoke its UnityEvent");
                SetBusyPresentation(manager, false); Tick(panel);
                Check(claimView.actionButton.interactable && claimView.actionLabel.text == "수령 가능", "Clearing busy presentation restores the same token and cell");
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
                    "Stale-generation cell input restores the current DTO token without switching an account or claiming");
                rowIds = ActiveIds(content);
                claimView.actionButton.onClick.RemoveListener(observer);
                var route = Members(typeof(BalanceQuestPanel).GetMethod("Claim", Private)).ToArray();
                Check(route.OfType<MethodBase>().Any(x => x.DeclaringType == typeof(QuestManager) && x.Name == "TryClaimTab") &&
                    route.OfType<FieldInfo>().Any(x => x.DeclaringType == typeof(BalanceQuestPanel) && x.Name == "_selectedCategory"),
                    "Compiled claim route calls existing TryClaimTab using the currently selected category");
                Check(Members(typeof(BalanceQuestPanel).GetMethod("Act", Private)).OfType<MethodBase>()
                    .Any(x => x.DeclaringType == typeof(LocalProgression) && x.Name == "get_AccountGeneration"), "Compiled whole-cell action retains its account-generation gate");
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
                    content.GetComponentsInChildren<GuideStepRowView>(false).All(x => x.progressFill.fillAmount == 1 && !x.actionButton.interactable),
                    "Re-enable reads supplied Claimed DTO rows and preserves their cards; reward payment is not exercised");
                data = State(day: "2026-11-13"); manager.Apply(data, "qa-dto-new-period"); Publish(manager);
                Check(Visible(panel).Where(x => !x.IsPending).All(x => x.Token.Period == "2026-11-13") &&
                    Visible(panel).Any(x => x.IsPending && x.Token.Period == "2026-11-11"), "New server periods replace current tokens while preserving supplied pending tokens");
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
