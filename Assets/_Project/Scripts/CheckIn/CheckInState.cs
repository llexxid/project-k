using System;
using System.Collections.Generic;
using KingdomIdle.Rewards;

namespace KingdomIdle.CheckIn
{
    [Serializable]
    public sealed class CheckInState
    {
        public string Month, LastDay;
        public List<RewardGrant> OpenedRewards = new();
        public HashSet<int> ClaimedSlots = new();
        public CheckInState Copy() => new()
        {
            Month = Month, LastDay = LastDay,
            OpenedRewards = RewardGrants.Copy(OpenedRewards), ClaimedSlots = new(ClaimedSlots)
        };
    }

    /// <summary>화면을 연 계정·월과 고정 수령 대상을 함께 보관한다. 보상 수량은 요청에서 받지 않는다.</summary>
    public sealed class CheckInSnapshot
    {
        public long AccountGeneration { get; internal set; }
        public string Month { get; internal set; }
        public int Count { get; internal set; }
        public long NextRefreshUtc { get; internal set; }
        public IReadOnlyList<RewardGrant> Rewards { get; internal set; }
        public IReadOnlyList<int> Available { get; internal set; }
        public IReadOnlyCollection<int> Claimed { get; internal set; }
    }
}
