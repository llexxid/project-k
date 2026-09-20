using System;
using System.Globalization;

namespace KingdomIdle.Balance
{
    /// <summary>
    /// 거래에서 한 번 읽은 UTC 시각으로 일일·주간 기간을 함께 계산한다.
    /// 날짜 문자열은 기존 JSON의 KST yyyy-MM-dd 키를 유지하며, Unity와 저장소에 의존하지 않는다.
    /// </summary>
    public readonly struct QuestPeriod
    {
        // 서버 시계를 도입해도 이 오프셋과 기간 계산 규칙은 그대로 사용할 수 있다.
        private static readonly TimeSpan KstOffset = TimeSpan.FromHours(9);
        private const string DateFormat = "yyyy-MM-dd";

        /// <summary>해당 KST 날짜의 일일 저장 키.</summary>
        public string Day { get; }
        /// <summary>해당 주의 월요일 KST 날짜로 표현한 주간 저장 키.</summary>
        public string Week { get; }
        /// <summary>이 값 묶음을 계산할 때 사용한 동일 UTC 초.</summary>
        public long NowUtc { get; }
        /// <summary>일일 기간이 끝나는 다음 KST 자정. 이 시각부터 새 기간이다.</summary>
        public long DayEndUtc { get; }
        /// <summary>주간 기간이 끝나는 다음 월요일 KST 자정.</summary>
        public long WeekEndUtc { get; }

        /// <summary>외부에서는 At만 사용하여 날짜 키와 종료 시각이 서로 어긋나지 않게 한다.</summary>
        private QuestPeriod(long utc, DateTimeOffset day, DateTimeOffset week)
        {
            NowUtc = utc;
            Day = day.ToString(DateFormat, CultureInfo.InvariantCulture);
            Week = week.ToString(DateFormat, CultureInfo.InvariantCulture);
            DayEndUtc = day.AddDays(1).ToUnixTimeSeconds();
            WeekEndUtc = week.AddDays(7).ToUnixTimeSeconds();
        }

        /// <summary>명시적으로 받은 UTC 초를 KST 일일·월요일 시작 주간에 대응시킨다.</summary>
        public static QuestPeriod At(long utc)
        {
            // DateTimeOffset.Date는 시각 없는 날짜이므로 KST 오프셋을 다시 명시한다.
            DateTime date = DateTimeOffset.FromUnixTimeSeconds(utc).ToOffset(KstOffset).Date;
            var day = new DateTimeOffset(date, KstOffset);
            int daysSinceMonday = ((int)date.DayOfWeek + 6) % 7;
            return new QuestPeriod(utc, day, day.AddDays(-daysSinceMonday));
        }

        /// <summary>읽기 전용으로 기간 변경·보관 보상 만료만 검사한다. 실제 정리는 거래의 AdvancePeriod가 수행한다.</summary>
        public static bool NeedsSynchronization(ProgressionState state, long nowUtc)
        {
            if (state == null) return true;
            // 기기 시간을 과거로 바꿔도 이미 승인한 기간을 다시 열지 않는다.
            long effectiveUtc = Math.Max(nowUtc, state.QuestLastObservedUtc);
            QuestPeriod period = At(effectiveUtc);
            if (state.QuestDay != period.Day || state.QuestWeek != period.Week) return true;
            foreach (QuestPending pending in state.PendingQuests.Values)
                if (pending.ExpiresUtc <= effectiveUtc) return true;
            return false;
        }

        /// <summary>저장된 일일/주간 키의 종료 UTC 초를 복구한다. 영구 퀘스트에는 사용하지 않는다.</summary>
        public static long EndUtc(eQuestCategory category, string period)
        {
            if (category != eQuestCategory.Daily && category != eQuestCategory.Weekly)
                throw new ArgumentOutOfRangeException(nameof(category), "반복 퀘스트의 기간만 종료 시각을 갖습니다.");

            DateTime date = DateTime.ParseExact(period, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None);
            return new DateTimeOffset(date, KstOffset).AddDays(category == eQuestCategory.Daily ? 1 : 7).ToUnixTimeSeconds();
        }
    }
}
