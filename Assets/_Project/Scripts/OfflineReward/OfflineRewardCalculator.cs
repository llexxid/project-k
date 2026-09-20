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
            int wave = Scripts.Core.Manager.StageParser.GetWaveNumber((eStage)clearedStage),
                stage = Scripts.Core.Manager.StageParser.GetStageNumber((eStage)clearedStage);
            bool safe = stage >= 1 && wave >= 1 && wave <= 10 &&
                (long)Scripts.Core.Manager.StageParser.MakeStage(eStageType.Main, stage, wave) == clearedStage;
            return new OfflineRewardPlan { actualOfflineSeconds = seconds, appliedOfflineSeconds = Math.Min(seconds, MaxOfflineSeconds),
                estimatedKillCount = safe ? (int)decimal.Floor(Math.Min(seconds, MaxOfflineSeconds) / 60m * Math.Max(0m, Math.Min(30m, kpm)) * .60m) : 0 };
        }
        public static OfflineRewardPlan CreatePlan(TimeSpan duration, StageDefinition definition)
            => CreatePlan(duration, definition == null ? 0 : (long)definition.Id, 0);
    }
}
