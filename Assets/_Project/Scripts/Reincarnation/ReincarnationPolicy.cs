using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Reincarnation
{
    public class ReincarnationPolicy
    {
        private const int MinimumStageLevel = 2;
        private const int LevelGainPerStage = 5;

        public ReincarnationPreview Evaluate(
            ReincarnationState currentState,
            bool isMainStage,
            int stageNumber)
        {
            if (!isMainStage)
            {
                return new ReincarnationPreview(
                    canReincarnate: false, 
                    failureReason: eReincarnationFailureReason.NotMainStage);
            }
            if (stageNumber < MinimumStageLevel)
            {
                return new ReincarnationPreview(
                    canReincarnate: false, 
                    failureReason: eReincarnationFailureReason.StageRequirementNotMet);
            }

            try
            {
                long levelGain = checked((long)stageNumber * LevelGainPerStage);
                long count = checked(currentState.Count + 1L);
                long nextLevel = checked(currentState.Level + levelGain);
                var nextState = new ReincarnationState(nextLevel, count);

                return new ReincarnationPreview(
                    canReincarnate: true,
                    levelGain: levelGain,
                    currentState: currentState,
                    nextState: nextState);
            }
            catch (OverflowException)
            {
                return new ReincarnationPreview(
                    canReincarnate: false,
                    failureReason: eReincarnationFailureReason.NumericOverflow,
                    currentState : currentState);
            }
        }
    }
}