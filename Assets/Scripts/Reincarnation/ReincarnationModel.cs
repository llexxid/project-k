using System;

namespace Reincarnation
{
    //실제 환생 실행 중 실패원인
    public enum ReincarnationExecutionResult
    {
        None,
        AlreadyProcessing,
        StageNotRunning,
        SaveFailed,
        StorageUnavailable,
        UnsupportedSaveVersion,
        StageResetRejected,
        RollbackFailed
    }
    
    //환생조건 제약으로 인한 실패원인
    public enum eReincarnationFailureReason
    {
        None,
        NotMainStage,
        StageRequirementNotMet,
        NumericOverflow,
        RequestDuplication,
        StateIsNotRunning
    }
    public readonly struct ReincarnationState
    {
        public long Level { get; }
        public long Count { get; }

        public ReincarnationState(long level, long count)
        {
            if (level < 0)
                throw new ArgumentOutOfRangeException(nameof(level));

            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));

            Level = level;
            Count = count;
        }
    }

    public readonly struct ReincarnationPreview
    {
        public bool CanReincarnate { get; }
        public long LevelGain { get; }
        public ReincarnationState CurrentState { get; }
        public ReincarnationState NextState { get; }
        public eReincarnationFailureReason FailureReason { get; }
        public ReincarnationPreview(
            bool canReincarnate, 
            eReincarnationFailureReason failureReason = eReincarnationFailureReason.None, 
            ReincarnationState currentState = default,
            ReincarnationState nextState = default,
            long levelGain = 0)
        {
            CanReincarnate = canReincarnate;
            LevelGain = levelGain;
            CurrentState = currentState;
            NextState = nextState;
            FailureReason = failureReason;
        }
    }
}