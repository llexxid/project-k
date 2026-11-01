using System;
using System.Collections.Generic;
using System.Linq;
using KingdomIdle.Balance;
using KingdomIdle.Rewards;

namespace KingdomIdle.CheckIn
{
    public sealed class CheckInService
    {
        private readonly CheckInRewardTableSO _table;
        public CheckInService(CheckInRewardTableSO table) { _table = table; }
        public bool IsRewardTableValid => _table != null && _table.IsValid();

        public CheckInSnapshot GetSnapshot()
        {
            if (!LocalProgression.TryGetCommittedState(out var state)) return null;
            var visit = state.CheckIn;
            var period = QuestPeriod.At(RewardGrants.Now);
            if (visit.Month != period.Month) return null;
            return new CheckInSnapshot
            {
                AccountGeneration = LocalProgression.AccountGeneration, Month = visit.Month, Count = visit.OpenedRewards.Count,
                NextRefreshUtc = visit.OpenedRewards.Count == 28 ? period.MonthEndUtc : period.DayEndUtc,
                Rewards = Enumerable.Range(0, 28).Select(i =>
                {
                    var reward = i < visit.OpenedRewards.Count ? visit.OpenedRewards[i] :
                        _table != null && _table.days != null && i < _table.days.Length ? _table.days[i] : null;
                    return RewardGrants.IsValid(reward) ? reward.Copy() : null;
                }).ToArray(),
                Available = Enumerable.Range(0, visit.OpenedRewards.Count).Where(i => !visit.ClaimedSlots.Contains(i)).ToArray(),
                Claimed = visit.ClaimedSlots.ToArray()
            };
        }

        /// <summary>접속 기록은 UI 조회와 분리한다. 월 전환 시 미수령 보상은 이월하지 않는다.</summary>
        public bool RecordVisit()
        {
            if (!LocalProgression.TryGetCommittedState(out var state)) return false;
            long now = RewardGrants.Now;
            var period = QuestPeriod.At(now);
            if (!QuestPeriod.IsLaterDay(period.Day, state.CheckIn.LastDay)) return true;
            if (state.CheckIn.Month == period.Month && state.CheckIn.OpenedRewards.Count == 28) return true;
            if (!IsRewardTableValid) return false;
            long account = LocalProgression.AccountGeneration;
            return LocalProgression.Execute("checkin-visit", draft =>
            {
                if (account != LocalProgression.AccountGeneration) return false;
                var visit = draft.CheckIn;
                if (!QuestPeriod.IsLaterDay(period.Day, visit.LastDay)) return false;
                if (visit.Month != period.Month)
                {
                    visit = draft.CheckIn = new CheckInState { Month = period.Month };
                }
                if (visit.OpenedRewards.Count < 28) visit.OpenedRewards.Add(_table.days[visit.OpenedRewards.Count].Copy());
                visit.LastDay = period.Day;
                draft.RewardsRevision++;
                return true;
            });
        }

        public RewardResult TryClaimAvailable(CheckInSnapshot snapshot)
        {
            if (!LocalProgression.IsReady) return new(RewardStatus.NotReady);
            if (snapshot == null || snapshot.AccountGeneration != LocalProgression.AccountGeneration) return new(RewardStatus.Stale);
            if (snapshot.Available.Count == 0) return new(RewardStatus.NothingAvailable);
            // 확정된 보상은 현재 편집 중인 보상표와 독립적으로 지급한다.
            if (IsRewardTableValid && !RecordVisit()) return new(RewardStatus.SaveFailed);
            var ids = snapshot.Available.ToArray();
            var failure = RewardStatus.SaveFailed;
            List<RewardGrant> granted = null;
            bool saved = LocalProgression.Execute("checkin-claim", draft =>
            {
                var visit = draft.CheckIn;
                if (snapshot.AccountGeneration != LocalProgression.AccountGeneration || snapshot.Month != visit.Month ||
                    visit.Month != QuestPeriod.At(RewardGrants.Now).Month ||
                    ids.Distinct().Count() != ids.Length || ids.Any(i => i < 0 || i >= visit.OpenedRewards.Count || visit.ClaimedSlots.Contains(i)))
                { failure = RewardStatus.Stale; return false; }
                if (!RewardGrants.TryApply(draft, ids.Select(i => visit.OpenedRewards[i]), out granted))
                { failure = RewardStatus.InvalidReward; return false; }
                foreach (int id in ids) visit.ClaimedSlots.Add(id);
                draft.RewardsRevision++;
                return true;
            });
            return new(saved ? RewardStatus.Success : failure, saved ? granted : null);
        }
    }
}
