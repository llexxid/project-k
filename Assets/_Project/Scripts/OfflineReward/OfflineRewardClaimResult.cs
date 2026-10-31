using KingdomIdle.Balance;
using Scripts.Core.Manager;

namespace KingdomIdle.OfflineRewards
{
    /// <summary>확정된 오프라인 사냥 결과와 팝업 표시에 사용할 값들</summary>
    public sealed class OfflineRewardClaimResult
    {
        public OfflineRewardPlan Plan { get; }
        public string ClaimId { get; }
        public string AccountKey { get; }
        public long AccountGeneration { get; }
        public long NetworkGeneration { get; }
        public long GoldGained { get; }
        public long ExperienceGained { get; }
        public long AncientCoinGained { get; }
        public int CurrentLevel { get; }
        public long CurrentExp { get; }
        public long CurrentKillScore { get; }

        internal OfflineRewardClaimResult(
            string claimId,
            OfflineRewardPlan plan,
            long goldGained,
            long experienceGained,
            long ancientCoinGained,
            int currentLevel,
            long currentExp,
            long currentKillScore)
        {
            ClaimId = claimId;
            AccountKey = LocalProgression.AccountKey;
            AccountGeneration = LocalProgression.AccountGeneration;
            NetworkGeneration = NetworkManager.Instance?.AccountGeneration ?? -1;
            Plan = plan;
            GoldGained = goldGained;
            ExperienceGained = experienceGained;
            AncientCoinGained = ancientCoinGained;
            CurrentLevel = currentLevel;
            CurrentExp = currentExp;
            CurrentKillScore = currentKillScore;
        }
    }
}
