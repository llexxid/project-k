namespace KingdomIdle.OfflineRewards
{
    /// <summary>서버가 확정한 오프라인 사냥 결과와 팝업 표시에 사용할 값들</summary>
    public sealed class OfflineRewardClaimResult
    {
        public OfflineRewardPlan Plan { get; }
        public long GoldGained { get; }
        public long AncientCoinGained { get; }
        public int CurrentLevel { get; }
        public long CurrentExp { get; }
        public long CurrentKillScore { get; }

        public OfflineRewardClaimResult(
            OfflineRewardPlan plan,
            long goldGained,
            long ancientCoinGained,
            int currentLevel,
            long currentExp,
            long currentKillScore)
        {
            Plan = plan;
            GoldGained = goldGained;
            AncientCoinGained = ancientCoinGained;
            CurrentLevel = currentLevel;
            CurrentExp = currentExp;
            CurrentKillScore = currentKillScore;
        }
    }
}
