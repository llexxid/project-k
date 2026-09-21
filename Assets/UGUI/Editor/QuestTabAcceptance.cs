#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using KingdomIdle.Balance;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>
    /// 원본 프리팹을 복제해 실제 탭 버튼과 패널 생명주기를 검사하는 PlayMode 전용 인수 검사다.
    /// Editor에만 포함되며, 실제 사용자 상태 대신 BeginTestSession의 격리 계정을 사용한다.
    /// </summary>
    public static class QuestTabAcceptance
    {
        private const string CatalogPath = "Assets/UGUI/UIViewCatalog.asset";
        private static readonly eQuestCategory[] Categories =
            { eQuestCategory.Guide, eQuestCategory.Daily, eQuestCategory.Weekly, eQuestCategory.Achievement };

        /// <summary>빈 검사 씬의 PlayMode에서 호출한다. 모든 결과는 동기 반환하며 실패 시 정리 후 예외를 전파한다.</summary>
        public static Dictionary<string, object> Run()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("QuestTabAcceptance requires PlayMode in an isolated test scene.");
            // BeginTestSession의 복구 Open도 파일을 쓸 수 있다. 실사용 계정이 열린 씬에서는 검사 자체를 시작하지 않는다.
            if (LocalProgression.IsReady && !(LocalProgression.AccountKey?.StartsWith("balance-qa-", StringComparison.Ordinal) ?? false))
                throw new InvalidOperationException("QuestTabAcceptance requires no open production account; enter PlayMode in the empty test scene first.");
            var checks = new List<string>();
            void Check(bool condition, string label)
            {
                if (!condition) throw new InvalidOperationException("Quest tab acceptance: " + label);
                checks.Add(label);
            }

            // 운영 singleton은 교체하기 전에 기억한다. finally는 성공·실패 양쪽에서 같은 복구 순서를 따른다.
            QuestManager originalQuest = QuestManager.Instance;
            UIManager originalUi = UIManager.Instance;
            GameObject managerRoot = null, uiRoot = null, canvasRoot = null;
            QuestManager manager = null;
            using (LocalProgression.BeginTestSession())
            {
                try
                {
                    var catalog = AssetDatabase.LoadAssetAtPath<UIViewCatalog>(CatalogPath);
                    Check(catalog != null && catalog.panelGuide != null && catalog.itemGuideStepRow != null &&
                        catalog.itemNavTabButton != null && catalog.itemGuideEmptyHint != null,
                        "Real catalog references the quest panel, tab, reward-row, and empty-state prefabs");
                    LocalProgression.TestUtcNow = new DateTimeOffset(2026, 9, 14, 12, 0, 0, TimeSpan.FromHours(9)).ToUnixTimeSeconds();
                    string account = "quest-tabs-" + Guid.NewGuid().ToString("N");
                    LocalProgression.OpenTestAccount(account);

                    // 실제 QuestManager의 Awake/OnEnable은 사용한다. UIManager는 원본 카탈로그 조회만 제공하는 비활성 fixture다.
                    managerRoot = new GameObject("QuestTabAcceptance_Manager");
                    managerRoot.SetActive(false);
                    manager = managerRoot.AddComponent<QuestManager>();
                    QuestManager.Instance = manager;
                    managerRoot.SetActive(true);
                    uiRoot = new GameObject("QuestTabAcceptance_UiCatalog");
                    uiRoot.SetActive(false);
                    var ui = uiRoot.AddComponent<UIManager>();
                    SetField(ui, "catalog", catalog);
                    SetUiSingleton(ui);

                    // 비활성 부모 아래 복제한 후 활성화하므로 원본 화면에는 Bind나 리스너가 추가되지 않는다.
                    canvasRoot = new GameObject("QuestTabAcceptance_Canvas", typeof(RectTransform), typeof(Canvas));
                    canvasRoot.SetActive(false);
                    canvasRoot.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                    var panelObject = UnityEngine.Object.Instantiate(catalog.panelGuide, canvasRoot.transform, false);
                    panelObject.name = "QuestTabAcceptance_Panel";
                    var view = panelObject.GetComponent<GuidePanelView>();
                    Check(view != null, "Real panel prefab contains GuidePanelView");
                    var panel = panelObject.GetComponent<BalanceQuestPanel>() ?? panelObject.AddComponent<BalanceQuestPanel>();
                    RectTransform tabs = Field<RectTransform>(view, "tabBar");
                    RectTransform content = Field<RectTransform>(view, "listContent");
                    ScrollRect scroll = Field<ScrollRect>(view, "scroll");
                    GameObject current = Field<GameObject>(view, "currentQuestRoot");
                    Check(tabs != null && content != null && scroll != null && current != null,
                        "Serialized tab bar, scroll content, and current-guide card are connected");
                    panelObject.SetActive(true);
                    canvasRoot.SetActive(true);
                    panel.Bind(view);
                    Publish(manager);

                    Button[] tabButtons = ActiveTabs(tabs);
                    Check(tabButtons.Length == 4, "Binding creates exactly four tab buttons");
                    var tabViews = tabs.GetComponentsInChildren<NavTabButtonView>(false);
                    Check(tabViews.Select(x => Field<TMP_Text>(x, "label").text).SequenceEqual(new[] { "가이드", "일일", "주간", "업적" }),
                        "Tab labels follow the approved Guide, Daily, Weekly, Achievement order");
                    Check(panel.SelectedCategory == eQuestCategory.Guide && !current.activeSelf,
                        "A new panel selects Guide without the redundant popup card");
                    Check(Visible(panel).Count > 0 && Visible(panel).All(x => x.Category == eQuestCategory.Guide),
                        "Initial rows contain only guide quests");

                    // API를 직접 대체 호출하지 않고 실제 각 탭의 UnityEvent를 실행한다.
                    for (int i = 0; i < Categories.Length; i++)
                    {
                        tabButtons[i].onClick.Invoke();
                        var category = Categories[i];
                        var expected = manager.GetSnapshot(category).Rows.OrderBy(x => x.State == QuestRowState.Claimable ? 0 :
                            x.State == QuestRowState.InProgress ? 1 : x.State == QuestRowState.Locked ? 2 : 3).Select(x => x.Token).ToArray();
                        Check(panel.SelectedCategory == category && Visible(panel).Select(x => x.Token).SequenceEqual(expected),
                            category + " tab renders only its committed category rows");
                        Check(!current.activeSelf, category + " tab hides only the redundant popup guide card");
                        Check(tabViews.Select(x => Field<Image>(x, "selectedFrame").color.a > 0).SequenceEqual(Categories.Select(x => x == category)),
                            category + " tab has exactly one matching visual selection");
                        Check(ActiveRowIds(content).Length == expected.Length && (i == 0 || Mathf.Abs(scroll.verticalNormalizedPosition - 1f) < .001f),
                            category + " tab excludes delayed-destroy rows and starts at the top");
                    }

                    // 같은 탭 재선택과 반복 Bind는 행을 재생성하거나 스크롤·저장을 바꾸지 않아야 한다.
                    tabButtons[1].onClick.Invoke();
                    int[] beforeRows = ActiveRowIds(content);
                    long revision = LocalProgression.State.Revision;
                    scroll.verticalNormalizedPosition = .37f;
                    panel.SelectCategory(eQuestCategory.Daily);
                    tabButtons[1].onClick.Invoke();
                    Check(beforeRows.SequenceEqual(ActiveRowIds(content)) && Mathf.Abs(scroll.verticalNormalizedPosition - .37f) < .001f &&
                        LocalProgression.State.Revision == revision, "Selecting the current tab is a no-op for rows, scroll, and saving");
                    for (int i = 0; i < 3; i++) panel.Bind(view);
                    Check(ActiveTabs(tabs).Length == 4 && panelObject.GetComponents<NumberNotationBinding>().Length == 1 &&
                        beforeRows.SequenceEqual(ActiveRowIds(content)), "Repeated Bind reuses tabs, notation binding, and unchanged rows");

                    // 실제 비활성화 동안 들어온 진행을 재활성화에서 읽는지 확인한다. 수동 OnEnable 중복 호출은 하지 않는다.
                    panelObject.SetActive(false);
                    Check(LocalProgression.Execute("qa-tabs-disabled-progress", state =>
                    {
                        state.MainClears.Add(0x20001000B);
                        QuestEconomy.Count(state, eQuestObjectiveType.MonsterKill, 0, 1);
                        return true;
                    }), "A committed gameplay change can arrive while the panel is disabled");
                    Publish(manager);
                    panelObject.SetActive(true);
                    Check(panel.SelectedCategory == eQuestCategory.Daily && Visible(panel).Single(x => x.Token.QuestId == 20001).Progress == 1,
                        "Re-enable preserves the selected tab and refreshes changes made while closed");
                    Check(ActiveTabs(tabs).Length == 4, "Re-enable does not duplicate tab buttons");

                    // 앞 단계 수령 후 다음 업적이 현재 탭에 나타나는 실제 보상 버튼 경로다.
                    Check(LocalProgression.Execute("qa-tabs-achievement", state => { state.AttackLevel = 50; return true; }),
                        "Achievement fixture completes two sequential enhancement tiers");
                    Publish(manager);
                    tabButtons[3].onClick.Invoke();
                    Check(Visible(panel).Any(x => x.Token.QuestId == 40401 && x.CanClaim) && !Visible(panel).Any(x => x.Token.QuestId == 40402),
                        "Achievement tab exposes the first unclaimed tier of a family");
                    Check(Visible(panel)[0].CanClaim, "Claimable achievements precede unfinished rows");
                    Button claim = ClaimButton(panel, 40401);
                    revision = LocalProgression.State.Revision;
                    claim.onClick.Invoke();
                    Check(LocalProgression.State.Revision == revision + 1 && LocalProgression.State.Claims.Contains(QuestEconomy.Key(40401, "permanent")) &&
                        panel.SelectedCategory == eQuestCategory.Achievement && Visible(panel).Any(x => x.Token.QuestId == 40401 && x.State == QuestRowState.Claimed) &&
                        Visible(panel).Any(x => x.Token.QuestId == 40402 && x.CanClaim),
                        "Claiming retains the completed achievement and reveals its next tier without changing tabs");
                    var completedAchievement = RowView(panel, 40401);
                    Check(completedAchievement.gameObject.activeInHierarchy && !completedAchievement.actionButton.interactable &&
                        completedAchievement.actionLabel.text == "완료" && completedAchievement.progressFill.fillAmount == 1 &&
                        completedAchievement.canvasGroup.alpha < 1,
                        "Claimed achievement remains visible, full, muted, and has a disabled Completed action");
                    revision = LocalProgression.State.Revision;
                    completedAchievement.actionButton.onClick.Invoke();
                    Check(LocalProgression.State.Revision == revision, "Even a directly invoked completed action cannot pay twice");

                    // 계정 B도 같은 목표를 완료한 상태에서 A 계정의 이미 표시된 버튼을 누른다.
                    Check(PrepareDailyBoss(), "First account has a claimable daily boss row");
                    Publish(manager);
                    tabButtons[1].onClick.Invoke();
                    Check(Visible(panel)[0].Token.QuestId == 20002, "Claimable daily boss moves ahead of unfinished monster goal");
                    Button stale = ClaimButton(panel, 20002);
                    LocalProgression.OpenTestAccount(account + "-other");
                    Check(PrepareDailyBoss(), "Second account independently completes the same daily boss goal");
                    revision = LocalProgression.State.Revision;
                    string bossKey = QuestEconomy.Key(20002, LocalProgression.State.QuestDay);
                    stale.onClick.Invoke();
                    Check(LocalProgression.State.Revision == revision && !LocalProgression.State.Claims.Contains(bossKey),
                        "A stale visible row cannot claim the new account's matching reward");
                    beforeRows = ActiveRowIds(content);
                    scroll.verticalNormalizedPosition = .37f;
                    Canvas.ForceUpdateCanvases();
                    var retainedRow = RowView(panel, 20001);
                    float retainedY = scroll.viewport.InverseTransformPoint(retainedRow.transform.position).y;
                    ClaimButton(panel, 20002).onClick.Invoke();
                    Check(LocalProgression.State.Revision == revision + 1 && LocalProgression.State.Claims.Contains(bossKey),
                        "The rebound row can claim the current account once");
                    Check(beforeRows.OrderBy(x => x).SequenceEqual(ActiveRowIds(content).OrderBy(x => x)) &&
                        Visible(panel).Last().Token.QuestId == 20002,
                        "Daily claim reuses the same row objects and moves the claimed card to the bottom");
                    Check(Mathf.Abs(scroll.viewport.InverseTransformPoint(retainedRow.transform.position).y - retainedY) < .5f,
                        "A neighbouring unfinished row keeps its screen position when the claimed first row moves down");
                    Check(RowView(panel, 20002).progressFill.fillAmount == 1 && !RowView(panel, 20002).actionButton.interactable,
                        "Daily claim keeps a full, disabled completed card");

                    // 저장 상태를 다시 읽어도 UI 전용 bool 없이 완료 카드가 복원되어야 한다.
                    LocalProgression.OpenTestAccount(account + "-other");
                    Publish(manager);
                    Check(Visible(panel).Any(x => x.Token.QuestId == 20002 && x.State == QuestRowState.Claimed),
                        "Reload restores the claimed card from durable claim receipts");

                    // 모든 보상을 받아도 빈 목록으로 바꾸지 않고 같은 기간의 완료 카드를 보존한다.
                    Check(LocalProgression.Execute("qa-tabs-empty", state =>
                    {
                        foreach (QuestDefinition definition in QuestEconomy.Definitions.Where(x => x.Category == eQuestCategory.Daily))
                        {
                            string key = QuestEconomy.Key(definition, state);
                            state.Claims.Add(key);
                            state.PendingQuests.Remove(key);
                        }
                        return true;
                    }), "Empty-state fixture marks only this account's daily rewards as claimed");
                    Publish(manager);
                    Check(panel.SelectedCategory == eQuestCategory.Daily && Visible(panel).Count > 0 &&
                        Visible(panel).All(x => x.State == QuestRowState.Claimed) && !current.activeSelf,
                        "All-claimed Daily tab keeps its completed rows instead of becoming empty");
                    Check(content.GetComponentsInChildren<GuideStepRowView>(false).All(x =>
                        x.progressFill.fillAmount == 1 && x.actionLabel.text == "완료" && !x.actionButton.interactable),
                        "All claimed cards retain full gauges and disabled Completed actions");
                    // 다음 날에는 새 기간의 토큰으로 교체되어 다시 진행할 수 있다.
                    LocalProgression.TestUtcNow += 86400;
                    Check(LocalProgression.SynchronizeQuests(), "Next-day quest period advances");
                    Publish(manager);
                    Check(Visible(panel).All(x => x.State != QuestRowState.Claimed),
                        "Current-period completed rows reset on the next day");

                    // 실제 ScrollRect의 드래그 전달 컴포넌트로 재배치만 지연시키는지 확인한다.
                    var drag = scroll.GetComponent<QuestScrollDragRelay>();
                    var pointer = new UnityEngine.EventSystems.PointerEventData(null)
                        { button = UnityEngine.EventSystems.PointerEventData.InputButton.Left };
                    beforeRows = ActiveRowIds(content);
                    drag.OnBeginDrag(pointer);
                    Check(PrepareDailyBoss(), "A goal can complete during a scroll drag");
                    Publish(manager);
                    Check(beforeRows.SequenceEqual(ActiveRowIds(content)) && ClaimButton(panel, 20002).interactable,
                        "Dragging keeps physical row order while updating claim availability");
                    drag.OnEndDrag(pointer);
                    Check(Visible(panel)[0].Token.QuestId == 20002 && beforeRows.OrderBy(x => x).SequenceEqual(ActiveRowIds(content).OrderBy(x => x)),
                        "Releasing the drag sorts the completed goal without recreating rows");

                    var multi = RowView(panel, 20010);
                    Check(multi.secondaryRewardIcon.transform.parent.gameObject.activeSelf &&
                        multi.secondaryRewardIcon.sprite == catalog.iconArcane,
                        "Multiple quest rewards remain visible as two typed icons");
                    Check(RowView(panel, 20001).rewardAmountLabel.text == "2분 골드",
                        "Dynamic gold uses its duration rule instead of displaying a false fixed amount");
                }
                finally
                {
                    // 패널 리스너를 먼저 해제한다. singleton은 fixture OnDestroy보다 먼저 복원해 운영 참조를 보호한다.
                    if (canvasRoot != null) canvasRoot.SetActive(false);
                    if (managerRoot != null) managerRoot.SetActive(false);
                    QuestManager.Instance = originalQuest;
                    SetUiSingleton(originalUi);
                    if (canvasRoot != null) UnityEngine.Object.DestroyImmediate(canvasRoot);
                    if (managerRoot != null) UnityEngine.Object.DestroyImmediate(managerRoot);
                    if (uiRoot != null) UnityEngine.Object.DestroyImmediate(uiRoot);
                }
            }
            Check(QuestManager.Instance == originalQuest && UIManager.Instance == originalUi,
                "Cleanup restores the original manager singletons and leaves no fixture panel");
            return new Dictionary<string, object> { { "passed", checks.Count }, { "checks", checks },
                { "fixture", "Real prefab in isolated PlayMode scene; original gameplay scene is not exercised" } };
        }

        private static bool PrepareDailyBoss() => LocalProgression.Execute("qa-tabs-boss", state =>
        {
            state.MainClears.Add(0x20001000B);
            QuestEconomy.Count(state, eQuestObjectiveType.BossKill, 0, 1);
            return true;
        });

        /// <summary>지연 Destroy 대상은 즉시 비활성화되므로 실제 표시되는 행만 센다.</summary>
        private static int[] ActiveRowIds(Transform content) => content.GetComponentsInChildren<GuideStepRowView>(false)
            .Select(x => x.GetInstanceID()).ToArray();
        private static Button[] ActiveTabs(Transform tabs) => tabs.GetComponentsInChildren<Button>(false);
        private static IReadOnlyList<QuestRowSnapshot> Visible(BalanceQuestPanel panel) => Field<List<QuestRowSnapshot>>(panel, "_visible");

        /// <summary>실제 생성된 카드 View를 조회해 snapshot뿐 아니라 표시와 입력 상태를 검증한다.</summary>
        private static GuideStepRowView RowView(BalanceQuestPanel panel, long id)
        {
            foreach (object row in Field<IEnumerable>(panel, "_rows"))
                if (Field<QuestRowSnapshot>(row, "Snapshot").Token.QuestId == id)
                    return Field<GuideStepRowView>(row, "View");
            throw new InvalidOperationException("Quest card missing: " + id);
        }

        /// <summary>동일 소스 어셈블리의 private Row는 Editor에서 복사 구현하지 않고 reflection으로 실제 버튼만 찾는다.</summary>
        private static Button ClaimButton(BalanceQuestPanel panel, long id)
        {
            foreach (object row in Field<IEnumerable>(panel, "_rows"))
                if (Field<QuestRowSnapshot>(row, "Snapshot").Token.QuestId == id)
                {
                    Button button = Field<GuideStepRowView>(row, "View").checkButton;
                    if (button == null || !button.interactable || !button.gameObject.activeInHierarchy)
                        throw new InvalidOperationException("Expected an active, interactable claim button: " + id);
                    return button;
                }
            throw new InvalidOperationException("Visible quest row was not found: " + id);
        }

        /// <summary>프레임을 기다리지 않고 실제 변경 알림 구간만 실행한다. UI OnEnable/OnDisable은 Unity가 호출한다.</summary>
        private static void Publish(QuestManager manager) => typeof(QuestManager)
            .GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(manager, null);
        private static T Field<T>(object target, string name) => (T)target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(target);
        private static void SetField(object target, string name, object value) => target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(target, value);
        private static void SetUiSingleton(UIManager value) => typeof(UIManager).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)
            .GetSetMethod(true).Invoke(null, new object[] { value });
    }
}
#endif
