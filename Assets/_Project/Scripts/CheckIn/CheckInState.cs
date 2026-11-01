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

    /// <summary>화면을 연 계정·월과 고정 수령 대상을 함께 보관한다. 서버 출석판(CheckInService)에서 만든다. 보상 수량은 요청에 넣지 않는다.</summary>
    public sealed class CheckInSnapshot
    {
        public long AccountGeneration { get; internal set; }
        public string Month { get; internal set; }
        public int Count { get; internal set; }
        public long NextRefreshUtc { get; internal set; }
        // 칸마다 대표로 보일 보상(장비가 있으면 장비).
        public IReadOnlyList<RewardGrant> Rewards { get; internal set; }
        // 칸마다 받는 보상 전체(서버 보상표 사본).
        public IReadOnlyList<IReadOnlyList<RewardGrant>> DayRewards { get; internal set; }
        public IReadOnlyList<int> Available { get; internal set; }
        public IReadOnlyCollection<int> Claimed { get; internal set; }
    }
}
