using System;
using System.Collections.Generic;
using KingdomIdle.Balance;

namespace KingdomIdle.Rewards
{
    [Serializable]
    public sealed class RewardGrant
    {
        public eCurrency Currency;
        public long Amount;
        public RewardGrant(eCurrency currency, long amount) { Currency = currency; Amount = amount; }
        public RewardGrant Copy() => new(Currency, Amount);
    }

    public enum RewardStatus { Success, NotReady, NothingAvailable, Stale, InvalidReward, SaveFailed }

    public sealed class RewardResult
    {
        public RewardStatus Status { get; }
        public IReadOnlyList<RewardGrant> Rewards { get; }
        public bool Succeeded => Status == RewardStatus.Success;
        public RewardResult(RewardStatus status, IReadOnlyList<RewardGrant> rewards = null)
        { Status = status; Rewards = rewards ?? Array.Empty<RewardGrant>(); }
    }

    /// <summary>재화 명세 검증과 초안 지급만 담당한다. 저장과 출석/우편 정책은 서비스가 소유한다.</summary>
    public static class RewardGrants
    {
        private const int CurrencyCount = (int)eCurrency.MageTicket + 1;
        public static long Now => LocalProgression.TryGetCommittedState(out var state)
            ? Math.Max(LocalProgression.UtcNow, state.QuestLastObservedUtc) : LocalProgression.UtcNow;

        public static bool IsValid(RewardGrant reward) => reward != null && reward.Amount > 0 &&
            (uint)reward.Currency < CurrencyCount;

        public static List<RewardGrant> Copy(IEnumerable<RewardGrant> rewards)
        {
            var copy = new List<RewardGrant>();
            foreach (var reward in rewards) copy.Add(reward.Copy());
            return copy;
        }

        /// <summary>같은 재화의 합계와 잔액 상한을 먼저 검사한다. 예상 가능한 오버플로는 예외로 처리하지 않는다.</summary>
        public static bool TryApply(ProgressionState draft, IEnumerable<RewardGrant> rewards, out List<RewardGrant> granted)
        {
            granted = new List<RewardGrant>();
            var totals = new long[CurrencyCount];
            foreach (var reward in rewards)
            {
                if (!IsValid(reward)) return false;
                int index = (int)reward.Currency;
                if (reward.Amount > long.MaxValue - totals[index]) return false;
                totals[index] += reward.Amount;
            }
            for (int i = 0; i < totals.Length; i++)
            {
                long balance = draft.Wallet.TryGetValue((eCurrency)i, out var value) ? value : 0;
                if (balance < 0 || totals[i] > long.MaxValue - balance) return false;
            }
            for (int i = 0; i < totals.Length; i++)
            {
                if (totals[i] == 0) continue;
                LocalProgression.Credit(draft, (eCurrency)i, totals[i]);
                granted.Add(new RewardGrant((eCurrency)i, totals[i]));
            }
            return granted.Count > 0;
        }
    }
}
