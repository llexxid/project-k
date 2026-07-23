using System.Collections;
using System.Collections.Generic;
using Scripts.Core;
using Scripts.Core.Manager;
using UnityEngine;

namespace Reincarnation
{
    public interface IReincarnationStageGateway
    {
        ReincarnationStageSnapshot GetSnapshot();
        bool TryResetToStartStage();
    }
    public readonly struct ReincarnationStageSnapshot
    {
        public bool IsAvailable { get; }
        public bool IsRunning { get; }
        public bool IsMainStage { get; }
        public int StageNumber { get; }
        
        public ReincarnationStageSnapshot(
            bool isAvailable,
            bool isRunning,
            bool isMainStage,
            int stageNumber)
        {
            IsAvailable = isAvailable;
            IsRunning = isRunning;
            IsMainStage = isMainStage;
            StageNumber = stageNumber;
        }
    }
    
    public sealed class ReincarnationGateway : IReincarnationStageGateway
    {
        private StageManager _stageManager;

        public ReincarnationStageSnapshot GetSnapshot()
        {
            _stageManager = StageManager.Instance;
            if (_stageManager == null)
            {
                return new ReincarnationStageSnapshot(
                    isAvailable: false,
                    isRunning: false,
                    isMainStage: false,
                    stageNumber: 0);
            }

            StageDefinition definition =
                _stageManager.CurrentDefinition;

            if (definition == null)
            {
                return new ReincarnationStageSnapshot(
                    isAvailable: false,
                    isRunning: false,
                    isMainStage: false,
                    stageNumber: 0);
            }

            bool isRunning =
                _stageManager.CurrentRunState
                == eStageRunState.Running;

            bool isMainStage =
                definition.Type == eStageType.Main;

            int stageNumber =
                StageParser.GetStageNumber(definition.Id);

            return new ReincarnationStageSnapshot(
                isAvailable: true,
                isRunning: isRunning,
                isMainStage: isMainStage,
                stageNumber: stageNumber);
        }

        public bool TryResetToStartStage()
        {
            _stageManager = StageManager.Instance;
            if (_stageManager == null) return false;
            return _stageManager.TryResetMainProgress();
        }

    }
}