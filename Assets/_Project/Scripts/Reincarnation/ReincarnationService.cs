using System;
using KingdomIdle.Balance;
using Scripts.Core;
using Scripts.Core.Manager;
namespace Reincarnation
{
    public class ReincarnationService
    {
        public event Action<ReincarnationState> ReincarnationStateChanged;
        public ReincarnationState CurrentState => new(LocalProgression.State.ReincarnationLevel, LocalProgression.State.ReincarnationCount);
        public ReincarnationService(ReincarnationPolicy policy, IReincarnationProgressStore store, IReincarnationStageGateway gateway)
        { LocalProgression.Changed += () => ReincarnationStateChanged?.Invoke(CurrentState); }
        public static eReincarnationFailureReason Eligibility(ProgressionState s)
        {
            if (s.PendingReincarnation) return eReincarnationFailureReason.RequestDuplication;
            if (s.ReincarnationLevel >= 300) return eReincarnationFailureReason.MaximumLevel;
            if (s.CycleBossStage < 1) return eReincarnationFailureReason.StageRequirementNotMet;
            if (LocalProgression.UtcNow - Math.Max(s.LastReincarnationUtc, s.CycleStartedUtc) < 600) return eReincarnationFailureReason.Cooldown;
            if (s.ReincarnationDay == LocalProgression.KstDay && s.ReincarnationsToday >= 3) return eReincarnationFailureReason.DailyLimit;
            return eReincarnationFailureReason.None;
        }
        public ReincarnationPreview GetPreview()
        {
            var manager = StageManager.Instance;
            if (manager?.CurrentDefinition?.Type != eStageType.Main || manager.IsBossWave) return new(false, eReincarnationFailureReason.NotMainStage);
            if (manager.CurrentRunState != eStageRunState.Running) return new(false,eReincarnationFailureReason.StateIsNotRunning);
            var s = LocalProgression.State; var error = Eligibility(s);
            return error != eReincarnationFailureReason.None ? new(false,error) : new ReincarnationPolicy().Evaluate(CurrentState,true,s.CycleBossStage);
        }
        public ReincarnationExecutionResult TryReincarnate()
        {
            if (!GetPreview().CanReincarnate) return ReincarnationExecutionResult.StageResetRejected;
            return LocalProgression.Execute("reincarnation-request", s => { if (Eligibility(s) != eReincarnationFailureReason.None) return false; s.PendingReincarnation = true; return true; }) ? ReincarnationExecutionResult.None : ReincarnationExecutionResult.SaveFailed;
        }
        public static void CommitAtBoundary(ProgressionState s, string battleId)
        {
            if (!s.PendingReincarnation) return;
            s.PendingReincarnation = false;
            if (Eligibility(s) != eReincarnationFailureReason.None) return;
            if (s.ReincarnationDay != LocalProgression.KstDay) { s.ReincarnationDay = LocalProgression.KstDay; s.ReincarnationsToday = 0; }
            s.ReincarnationLevel += Math.Min(300 - s.ReincarnationLevel, 5 * s.CycleBossStage);
            s.ReincarnationCount++; s.ReincarnationsToday++;
            s.CycleBossStage = 0; s.MainStage = 0x200010001;
            s.CycleStartedUtc = s.LastReincarnationUtc = LocalProgression.UtcNow;
            s.ReincarnatedBattle = battleId;
            QuestEconomy.Count(s,eQuestObjectiveType.Reincarnate,0,1);
        }
    }
}
