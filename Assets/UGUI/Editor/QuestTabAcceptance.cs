#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using KingdomIdle.Balance;
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
