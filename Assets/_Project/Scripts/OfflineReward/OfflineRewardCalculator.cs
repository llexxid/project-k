using System;
using Scripts.Core;
namespace KingdomIdle.OfflineRewards
{
    public static class OfflineRewardCalculator
    {
        public const int MaxOfflineSeconds = 28800;
        public static OfflineRewardPlan CreatePlan(TimeSpan duration, long clearedStage, decimal kpm)
        {
            long seconds = Math.Max(0, (long)duration.TotalSeconds);
            int wave = (int)(clearedStage & 0xFFFF), stage = (int)((clearedStage >> 16) & 0xFFF);
            bool safe = (clearedStage & 0xF0000000L) == 0 && stage >= 1 && stage <= 3 && wave >= 1 && wave <= 10;
            return new OfflineRewardPlan { actualOfflineSeconds = seconds, appliedOfflineSeconds = Math.Min(seconds, MaxOfflineSeconds),
                estimatedKillCount = safe ? (int)decimal.Floor(Math.Min(seconds, MaxOfflineSeconds) / 60m * Math.Max(0m, Math.Min(30m, kpm)) * .60m) : 0 };
        }
        public static OfflineRewardPlan CreatePlan(TimeSpan duration, StageDefinition definition)
            => CreatePlan(duration, definition == null ? 0 : (long)definition.Id, 0);
    }
}
