#if UNITY_EDITOR || LOBBY_DEVICE_QA
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace KingdomIdle.Balance
{
    /// <summary>
    /// 실제 Unity MonoBehaviour로 UI 조회·변경 알림·구형 가이드 계약을 검증한다.
    /// 임시 매니저는 비활성 상태로 두어 기존 singleton과 화면을 교체하지 않으며, 저장은 격리 계정만 사용한다.
    /// </summary>
    public static class QuestUiAcceptance
    {
        /// <summary>모든 검사를 동기 실행하고 검사 이름을 반환한다. 실패 시 예외를 던지되 계정과 테스트 시각은 복구한다.</summary>
        public static Dictionary<string, object> Run()
        {
            var checks = new List<string>();
            var changes = new List<QuestChangeSet>();
            QuestManager original = QuestManager.Instance;
            GameObject fixture = null;
            QuestManager manager = null;
            Action committed = null, accountChanged = null;
            double cachedReadMilliseconds = 0;
            long cachedReadAllocatedBytes = 0;

            using (LocalProgression.BeginTestSession())
            {
                try
                {
                    // 월요일 정오 KST를 시작점으로 잡아 다음 날은 일일 기간만 바뀌도록 한다.
                    LocalProgression.TestUtcNow = new DateTimeOffset(2026, 9, 14, 12, 0, 0, TimeSpan.FromHours(9)).ToUnixTimeSeconds();
                    string account = "quest-ui-" + Guid.NewGuid().ToString("N");
                    LocalProgression.OpenTestAccount(account);
                    fixture = new GameObject("Quest UI acceptance (inactive)");
                    fixture.SetActive(false);
                    manager = fixture.AddComponent<QuestManager>();
                    Check(QuestManager.Instance == original, "Fixture preserves the live QuestManager singleton", checks);

                    // 비활성 fixture에는 Unity OnEnable 대신 실제 저장 이벤트를 연결한다.
                    committed = () => Invoke(manager, "MarkDirty");
                    accountChanged = () => Invoke(manager, "OnAccountChanged");
                    LocalProgression.Changed += committed;
                    LocalProgression.AccountChanged += accountChanged;
                    manager.QuestsChanged += changes.Add;
                    int guideNotifications = 0;
                    manager.OnGuideQuestChanged += (_, _) => guideNotifications++;
                    manager.OnQuestProgressChanged += (_, _) => guideNotifications++;

                    long revision = LocalProgression.State.Revision;
                    long generation = LocalProgression.AccountGeneration;
                    var firstBoard = manager.GetSnapshot(eQuestCategory.Daily);
                    foreach (eQuestCategory category in Enum.GetValues(typeof(eQuestCategory))) manager.GetSnapshot(category);
                    Check(LocalProgression.State.Revision == revision && LocalProgression.AccountGeneration == generation,
                        "Snapshot reads neither save nor switch account", checks);
                    Check(ReferenceEquals(firstBoard, manager.GetSnapshot(eQuestCategory.Daily)),
                        "Repeated reads reuse the same committed snapshot", checks);
                    Check(firstBoard.IsReady && firstBoard.Rows.All(x => x.State == QuestRowState.Locked),
                        "Locked daily rows are readable without becoming claimable", checks);
                    // 워밍업된 UI 조회의 실제 할당·시간을 남긴다. 파일 저장 시간과는 별개 측정이다.
                    var readTimer = new System.Diagnostics.Stopwatch();
                    long allocationsBefore = GC.GetAllocatedBytesForCurrentThread();
                    readTimer.Start();
                    for (int i = 0; i < 10000; i++) manager.GetSnapshot(eQuestCategory.Daily);
                    readTimer.Stop();
                    cachedReadAllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocationsBefore;
                    cachedReadMilliseconds = readTimer.Elapsed.TotalMilliseconds;
                    Check(LocalProgression.State.Revision == revision, "Ten thousand cached reads perform no saves", checks);
                    CheckDefensiveCopies(checks);

                    Publish(manager);
                    Check(changes.Count == 1 && guideNotifications == 1,
                        "Initial binding publishes one board change and the active guide", checks);
                    changes.Clear();
                    int originalGuideNotifications = guideNotifications;
                    Check(LocalProgression.Execute("qa-quest-ui-unrelated", s => { s.Modules["quest-ui-probe"] = "1"; return true; }),
                        "Unrelated committed revision fixture", checks);
                    Publish(manager);
                    Check(changes.Count == 0 && guideNotifications == originalGuideNotifications,
                        "Unchanged quest rows emit no events after another subsystem commits", checks);

                    QuestDefinition daily = QuestEconomy.Definitions.First(x => x.Category == eQuestCategory.Daily &&
                        x.ObjectiveType == eQuestObjectiveType.MonsterKill);
                    Check(LocalProgression.Execute("qa-quest-ui-unlock", s =>
                    {
                        s.MainClears.Add(0x20001000B);
                        return true;
                    }), "Daily unlock fixture", checks);
                    Publish(manager);
                    changes.Clear();
                    originalGuideNotifications = guideNotifications;
                    var token = manager.GetSnapshot(eQuestCategory.Daily).Rows.First(x => x.Token.QuestId == daily.QuestId).Token;
                    Check(LocalProgression.Execute("qa-quest-ui-progress", s =>
                    {
                        QuestEconomy.Count(s, daily.ObjectiveType, daily.TargetId, 1);
                        return true;
                    }), "Committed gameplay progress fixture", checks);
                    Publish(manager);
                    Check(changes.Count == 1 && changes[0].ChangedTokens.Contains(token) &&
                        changes[0].ChangedCategories.Contains(eQuestCategory.Daily),
                        "Progress publishes its changed claim token and category", checks);
                    Check(guideNotifications == originalGuideNotifications,
                        "Daily progress does not redraw an unchanged guide", checks);

                    revision = LocalProgression.State.Revision;
                    long progress = QuestEconomy.Progress(daily, LocalProgression.State);
                    changes.Clear();
                    manager.ApplyQuestEvent(new QuestEvent { EventType = eQuestEventType.MonsterKilled, Amount = 777 });
                    Publish(manager);
                    Check(LocalProgression.State.Revision == revision && QuestEconomy.Progress(daily, LocalProgression.State) == progress && changes.Count == 0,
                        "Legacy ApplyQuestEvent neither counts twice nor emits a false change", checks);

                    var guide = manager.GetActiveGuideState();
                    var definition = manager.GetQuestDefinition(guide.QuestId);
                    Check(definition.Category == eQuestCategory.Guide && manager.AddQuestState(guide.QuestId).QuestId == guide.QuestId,
                        "Existing guide definition and runtime projection APIs remain compatible", checks);
                    Check(LocalProgression.Execute("qa-quest-ui-guide", s => { s.MainClears.Add(definition.TargetId); return true; }),
                        "Active guide completion fixture", checks);
                    Check(manager.GetActiveGuideState().IsCompleted && manager.CanClaimReward(guide.QuestId),
                        "Existing guide HUD sees a completed claimable quest", checks);
                    manager.ClaimQuestReward(guide.QuestId);
                    Check(LocalProgression.State.Claims.Contains(QuestEconomy.Key(definition, LocalProgression.State)) &&
                        manager.GetActiveGuideState()?.QuestId != guide.QuestId,
                        "Legacy guide claim grants once and advances its active row", checks);
                    Publish(manager);

                    Check(LocalProgression.Execute("qa-quest-ui-complete", s =>
                    {
                        QuestEconomy.Count(s, daily.ObjectiveType, daily.TargetId, daily.RequiredCount);
                        return true;
                    }), "Daily completion fixture", checks);
                    token = manager.GetSnapshot(eQuestCategory.Daily).Rows.First(x => x.Token.QuestId == daily.QuestId).Token;
                    Check(manager.TryClaim(token).Succeeded, "Snapshot token claims its exact daily reward", checks);
                    var claimedRow = manager.GetSnapshot(eQuestCategory.Daily).Rows.First(x => x.Token == token);
                    Check(claimedRow.State == QuestRowState.Claimed && claimedRow.Progress == claimedRow.RequiredCount && !claimedRow.CanClaim,
                        "Claim receipt keeps a full completed snapshot after pending reward removal", checks);
                    Publish(manager);
                    changes.Clear();

                    LocalProgression.TestUtcNow += 86400;
                    Check(LocalProgression.SynchronizeQuests(), "Next-day synchronization fixture", checks);
                    Publish(manager);
                    var nextToken = manager.GetSnapshot(eQuestCategory.Daily).Rows.First(x => x.Token.QuestId == daily.QuestId && !x.IsPending).Token;
                    Check(nextToken.Period != token.Period && changes.Count == 1 &&
                        changes[0].ChangedTokens.Contains(token) && changes[0].ChangedTokens.Contains(nextToken),
                        "Period change reports the removed old token and added new token", checks);

                    LocalProgression.OpenTestAccount(account + "-other");
                    revision = LocalProgression.State.Revision;
                    Check(manager.TryClaim(nextToken).Status == QuestClaimStatus.StaleAccount && LocalProgression.State.Revision == revision,
                        "A stale account button cannot mutate the newly opened account", checks);
                    changes.Clear();
                    Publish(manager);
                    Check(changes.Count == 1 && changes[0].AccountChanged &&
                        manager.GetSnapshot(eQuestCategory.Daily).AccountGeneration == LocalProgression.AccountGeneration,
                        "Account notification replaces the cached board generation", checks);
                    CheckStaleGuideHud(manager, fixture, account, checks);
                }
                finally
                {
                    if (committed != null) LocalProgression.Changed -= committed;
                    if (accountChanged != null) LocalProgression.AccountChanged -= accountChanged;
                    // 비활성 fixture는 singleton을 차지하지 않는다. 예외 경로도 원래 참조를 보존한다.
                    if (manager != null && QuestManager.Instance == manager) QuestManager.Instance = original;
                    if (fixture != null) UnityEngine.Object.DestroyImmediate(fixture);
                }
            }
            Check(QuestManager.Instance == original, "Acceptance cleanup preserves the original singleton", checks);
            return new Dictionary<string, object> { { "passed", checks.Count }, { "checks", checks },
                { "cachedReadCount", 10000 }, { "cachedReadTotalMs", cachedReadMilliseconds },
                { "cachedReadAllocatedBytes", cachedReadAllocatedBytes } };
        }

        /// <summary>실제 Update 루프를 기다리지 않고 한 프레임의 변경 알림 처리만 실행한다.</summary>
        private static void Publish(QuestManager manager) => Invoke(manager, "LateUpdate");

        /// <summary>비활성 GameObject는 SendMessage 대상에서 제외될 수 있으므로 실제 메서드를 명시적으로 호출한다.</summary>
        private static void Invoke(QuestManager manager, string method) => typeof(QuestManager)
            .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(manager, null);

        /// <summary>A 계정 가이드를 표시한 HUD를 갱신하지 않고 B 계정에서 클릭하여 token 바인딩을 검증한다.</summary>
        private static void CheckStaleGuideHud(QuestManager manager, GameObject fixture, string account, List<string> checks)
        {
            LocalProgression.OpenTestAccount(account + "-hud-a");
            var definition = manager.GetQuestDefinition(manager.GetActiveGuideState().QuestId);
            Check(LocalProgression.Execute("qa-guide-hud-a", s => { s.MainClears.Add(definition.TargetId); return true; }),
                "First account's guide HUD completion fixture", checks);

            // UI singleton이나 실제 HUD는 교체하지 않는다. 표시 필드가 없어도 바인딩과 Act는 동일한 코드다.
            var hud = fixture.AddComponent<KingdomIdle.UGUI.GuideGoalView>();
            var type = typeof(KingdomIdle.UGUI.GuideGoalView);
            type.GetField("_manager", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(hud, manager);
            type.GetMethod("ReadCurrent", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(hud, null);

            LocalProgression.OpenTestAccount(account + "-hud-b");
            Check(LocalProgression.Execute("qa-guide-hud-b", s => { s.MainClears.Add(definition.TargetId); return true; }),
                "Second account has the same completed guide before stale HUD click", checks);
            long revision = LocalProgression.State.Revision;
            string claimKey = QuestEconomy.Key(definition, LocalProgression.State);

            // LateUpdate/Refresh를 호출하지 않으므로 이 버튼은 여전히 A 계정의 완료 화면이다.
            type.GetMethod("Act", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(hud, null);
            Check(LocalProgression.State.Revision == revision && !LocalProgression.State.Claims.Contains(claimKey),
                "Stale guide HUD token cannot claim another account's matching guide", checks);

            // 거절 뒤 ReadCurrent로 B에 다시 바인딩된 화면에서는 다음 클릭을 정상 처리한다.
            type.GetMethod("Act", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(hud, null);
            Check(LocalProgression.State.Claims.Contains(claimKey),
                "Guide HUD can claim after rebinding to the new account", checks);
        }

        /// <summary>UI가 원본 컬렉션을 수정해도 이미 발행한 보상/행이 변하지 않는지 확인한다.</summary>
        private static void CheckDefensiveCopies(List<string> checks)
        {
            var token = new QuestClaimToken(1, 20001, "2026-09-14");
            var rewards = new List<QuestRewardSnapshot> { new(eCurrency.Gold, 66.3m) };
            var row = new QuestRowSnapshot(token, eQuestCategory.Daily, "Daily", "Kill", eQuestObjectiveType.MonsterKill,
                0, 10, 10, QuestRowState.Claimable, false, 0, rewards);
            rewards.Clear();
            var rows = new List<QuestRowSnapshot> { row };
            var board = new QuestBoardSnapshot(1, 1, eQuestCategory.Daily, token.Period, 0, rows);
            rows.Clear();
            Check(board.Rows.Count == 1 && row.Rewards.Count == 1 && row.Rewards[0].Amount == 66.3m,
                "Snapshot constructors defensively copy rows and fractional reward values", checks);
            bool blocked = false;
            try { ((IList<QuestRowSnapshot>)board.Rows).Clear(); }
            catch (NotSupportedException) { blocked = true; }
            Check(blocked, "Snapshot rows reject mutation through IList", checks);
        }

        /// <summary>실패한 조건은 이름을 포함해 즉시 중단하여 통과로 기록하지 않는다.</summary>
        private static void Check(bool condition, string name, List<string> checks)
        {
            if (!condition) throw new InvalidOperationException("Quest UI acceptance: " + name);
            checks.Add(name);
        }
    }
}
#endif
