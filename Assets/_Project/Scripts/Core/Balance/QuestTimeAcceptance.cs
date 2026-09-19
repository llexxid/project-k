#if UNITY_EDITOR || LOBBY_DEVICE_QA
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using KingdomIdle.UGUI;
using Newtonsoft.Json;
using UnityEngine;

namespace KingdomIdle.Balance
{
    /// <summary>
    /// 실제 LocalProgression 거래와 파일 교체를 사용하는 전투 시간 인수 검사다.
    /// 고유 QA 계정과 명시적 UTC를 사용하고, 끝나면 BeginTestSession이 원래 계정·시각을 복원한다.
    /// </summary>
    public static class QuestTimeAcceptance
    {
        /// <summary>자정·월요일 경계, 실패 재시도, ack 소유권과 계정 분리를 실제 저장 경로에서 검증한다.</summary>
        public static Dictionary<string, object> Run()
        {
            using var session = LocalProgression.BeginTestSession();
            var checks = new List<string>();
            void Check(bool condition, string description)
            {
                if (!condition) throw new InvalidOperationException("QUEST TIME ASSERT: " + description);
                checks.Add(description);
            }

            CheckModalTimeGate(Check);

            // 실행마다 새 계정을 만들어 이전 인수 검사 결과가 다음 실행을 통과시키지 못하게 한다.
            string run = "quest-time-" + Guid.NewGuid().ToString("N");
            QuestDefinition daily = QuestCatalog.Instance.Get(20003);
            QuestDefinition weekly = QuestCatalog.Instance.Get(30004);
            long midnight = Utc("2027-01-06T00:00:00+09:00");
            LocalProgression.TestUtcNow = midnight - 5;
            LocalProgression.OpenTestAccount(run + "-daily");
            Check(LocalProgression.Execute("qa-time-daily-setup", state =>
            {
                state.MainClears.Add(0x20001000B);
                QuestEconomy.Count(state, eQuestObjectiveType.BattleTime, 0, 415);
                return true;
            }), "Daily setup commits 415 seconds");
            string oldDailyKey = QuestEconomy.Key(daily, LocalProgression.State);
            Check(!LocalProgression.State.PendingQuests.ContainsKey(oldDailyKey), "415 seconds is not a completed daily goal");

            // 23:59:55~00:00:05의 10초 중 앞 5초가 이전 일일의 420초를 달성시켜야 한다.
            BattleEconomy.TestRecordQuestTime(midnight - 5, midnight + 5, 10);
            LocalProgression.TestUtcNow = midnight + 5;
            Check(LocalProgression.Execute("qa-time-midnight", _ => true), "Midnight time transaction commits");
            Check(LocalProgression.State.PendingQuests.ContainsKey(oldDailyKey), "Old daily reaches 420 seconds and keeps its pending reward");
            Check(QuestEconomy.Progress(daily, LocalProgression.State) == 5, "New daily receives only the five seconds after midnight");
            Check(ReadLifetimeSeconds(LocalProgression.State) == 425 && BattleEconomy.TestPendingQuestSeconds == 0,
                "Midnight split preserves all ten lifetime seconds and acknowledges the buffer");

            // 일요일/월요일 경계는 일일과 주간을 함께 넘긴다. 이전 주의 마지막 5초도 먼저 봉인해야 한다.
            long monday = Utc("2027-01-11T00:00:00+09:00");
            string weeklyAccount = run + "-weekly";
            LocalProgression.TestUtcNow = monday - 5;
            LocalProgression.OpenTestAccount(weeklyAccount);
            Check(LocalProgression.Execute("qa-time-weekly-setup", state =>
            {
                state.MainClears.Add(0x20001000B);
                QuestEconomy.Count(state, eQuestObjectiveType.BattleTime, 0, 12595);
                return true;
            }), "Weekly setup commits 12595 seconds");
            string oldWeeklyKey = QuestEconomy.Key(weekly, LocalProgression.State);
            BattleEconomy.TestRecordQuestTime(monday - 5, monday + 5, 10);
            LocalProgression.TestUtcNow = monday + 5;
            Check(LocalProgression.Execute("qa-time-monday", _ => true), "Monday time transaction commits");
            Check(LocalProgression.State.PendingQuests.ContainsKey(oldWeeklyKey), "Old week reaches 12600 seconds before rollover");
            Check(QuestEconomy.Progress(weekly, LocalProgression.State) == 5 && QuestEconomy.Progress(daily, LocalProgression.State) == 5,
                "New Monday day and week each receive five seconds");

            // 실패한 복제본은 공개하지 않고 시간도 ack하지 않는다. 다음 정상 거래가 같은 구간을 한 번만 반영한다.
            long beforeFailure = ReadLifetimeSeconds(LocalProgression.State);
            long revision = LocalProgression.State.Revision;
            BattleEconomy.TestRecordQuestTime(monday + 5, monday + 12, 7);
            LocalProgression.TestUtcNow = monday + 12;
            Check(!LocalProgression.TestFailedCommit(() => LocalProgression.Execute("qa-time-write-failure", _ => true)),
                "Forced snapshot failure rejects the time transaction");
            Check(LocalProgression.State.Revision == revision && ReadLifetimeSeconds(LocalProgression.State) == beforeFailure &&
                  BattleEconomy.TestPendingQuestSeconds == 7, "Failed write preserves committed state and all seven pending seconds");
            Check(LocalProgression.Execute("qa-time-write-retry", _ => true) &&
                  ReadLifetimeSeconds(LocalProgression.State) == beforeFailure + 7 && BattleEconomy.TestPendingQuestSeconds == 0,
                "Successful retry records the retained seven seconds once");
            Check(LocalProgression.Execute("qa-time-after-retry", _ => true) && ReadLifetimeSeconds(LocalProgression.State) == beforeFailure + 7,
                "Later transactions do not repeat acknowledged time");

            // 한 저장 시도가 준비한 토큰은 이후 준비된 시도의 버퍼를 차감할 권리가 없다.
            BattleEconomy.TestRecordQuestTime(monday + 12, monday + 16, 4);
            LocalProgression.TestUtcNow = monday + 16;
            long staleToken = BattleEconomy.PrepareQuestTime(CopyState(), LocalProgression.UtcNow);
            long abandonedToken = BattleEconomy.PrepareQuestTime(CopyState(), LocalProgression.UtcNow);
            BattleEconomy.AcknowledgeQuestTime(staleToken);
            Check(BattleEconomy.TestPendingQuestSeconds == 4, "Stale ack cannot consume a newer prepared batch");
            Check(LocalProgression.Execute("qa-time-new-batch", _ => true), "Current transaction commits its own batch");
            long afterCurrentBatch = ReadLifetimeSeconds(LocalProgression.State);
            BattleEconomy.AcknowledgeQuestTime(abandonedToken);
            BattleEconomy.AcknowledgeQuestTime(abandonedToken);
            Check(afterCurrentBatch == beforeFailure + 11 && BattleEconomy.TestPendingQuestSeconds == 0,
                "Abandoned and repeated ack do not change committed time");

            // 계정 전환은 기존 계정 flush부터 성공해야 한다. 실패 시 세대와 계정이 그대로여야 한다.
            string oldAccountKey = LocalProgression.AccountKey;
            long oldGeneration = LocalProgression.AccountGeneration;
            string nextAccount = run + "-next";
            BattleEconomy.TestRecordQuestTime(monday + 16, monday + 19, 3);
            LocalProgression.TestUtcNow = monday + 19;
            bool switched = LocalProgression.TestFailedCommit(() =>
            {
                try { LocalProgression.OpenTestAccount(nextAccount); return true; }
                catch (IOException) { return false; }
            });
            Check(!switched && LocalProgression.AccountKey == oldAccountKey && LocalProgression.AccountGeneration == oldGeneration &&
                  BattleEconomy.TestPendingQuestSeconds == 3, "Failed old-account flush blocks account switching without losing time");
            LocalProgression.OpenTestAccount(nextAccount);
            Check(LocalProgression.AccountGeneration > oldGeneration && ReadLifetimeSeconds(LocalProgression.State) == 0 &&
                  BattleEconomy.TestPendingQuestSeconds == 0, "New account starts without the previous account's pending time");
            LocalProgression.OpenTestAccount(weeklyAccount);
            Check(ReadLifetimeSeconds(LocalProgression.State) == afterCurrentBatch + 3,
                "Reopened old account contains its own final three seconds exactly once");

            // 저장은 13시인데 기기 시각을 12시로 되돌린 상황이다. raw Tick과 승인시각 prepare가
            // 번갈아 호출되어도 같은 샘플러가 실제 5초를 잃지 않고 승인된 날짜에 저장해야 한다.
            long approvedUtc = Utc("2027-01-12T13:00:00+09:00");
            LocalProgression.TestUtcNow = approvedUtc;
            LocalProgression.OpenTestAccount(run + "-clock-rollback");
            BattleEconomy.TestCaptureQuestTime(approvedUtc, 0, true, true);
            LocalProgression.TestUtcNow = approvedUtc - 3600;
            double elapsed = 0;
            for (int i = 0; i < 5; i++)
            {
                elapsed += 1;
                BattleEconomy.TestCaptureQuestTime(LocalProgression.UtcNow + i, elapsed, true);
                elapsed += .001;
                BattleEconomy.TestCaptureQuestTime(approvedUtc, elapsed, true);
            }
            Check(Math.Abs(BattleEconomy.TestPendingQuestSeconds - elapsed) < .000001,
                "Clock rollback preserves every monotonic second across raw Tick and clamped prepare samples");
            Check(LocalProgression.Execute("qa-time-clock-rollback", _ => true) &&
                  ReadLifetimeSeconds(LocalProgression.State) == 5 && LocalProgression.State.QuestDay == QuestPeriod.At(approvedUtc).Day,
                "Rollback samples commit five seconds in the last approved period");

            return new Dictionary<string, object> { { "passed", checks.Count }, { "checks", checks } };
        }

        /// <summary>운영체제 로캘과 무관한 명시적 KST 시각을 UTC 초로 바꾼다.</summary>
        private static long Utc(string text) => DateTimeOffset.Parse(text, CultureInfo.InvariantCulture).ToUnixTimeSeconds();

        /// <summary>공개된 상태를 직접 수정하지 않고 실패·중단한 준비 단계를 시험할 복제본을 만든다.</summary>
        private static ProgressionState CopyState() => JsonConvert.DeserializeObject<ProgressionState>(JsonConvert.SerializeObject(LocalProgression.State));

        /// <summary>스택 밖 임시 모달을 등록·비활성화하여 실제 UI 차단 조회가 팝업 생명주기를 따라가는지 검사한다.</summary>
        private static void CheckModalTimeGate(Action<bool, string> check)
        {
            bool wasOpen = ModalBackHandler.HasOpenModal;
            var modal = new GameObject("QuestTimeAcceptance_Modal");
            try
            {
                ModalBackHandler.Bind(modal, () => modal.SetActive(false));
                check(ModalBackHandler.HasOpenModal && (UIManager.Instance == null || UIManager.Instance.BlocksQuestBattleTime),
                    "Registered modal is visible to the battle-time UI gate");
                modal.SetActive(false);
                check(ModalBackHandler.HasOpenModal == wasOpen, "Closing the test modal restores the previous gate state");
            }
            finally
            {
                modal.SetActive(false);
#if UNITY_EDITOR
                UnityEngine.Object.DestroyImmediate(modal);
#else
                UnityEngine.Object.Destroy(modal);
#endif
            }
        }

        /// <summary>기간 정리와 별개로 모든 승인 초가 보존되었는지 비교하는 영구 누적값이다.</summary>
        private static long ReadLifetimeSeconds(ProgressionState state) =>
            QuestProgressEvaluator.Read(state, "L", eQuestObjectiveType.BattleTime, 0);
    }
}
#endif
