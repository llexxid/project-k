using System;
using Scripts.Core;
using Scripts.Core.Manager;

namespace KingdomIdle.UGUI
{
    [Serializable]
    public struct DungeonDifficultyDisplayData
    {
        public eStage stageId;
        public int stageNumber;
        public bool isUnlocked;
        public long recommendedPower;

        public bool HasStage => (long)stageId != 0;
        public int StageNumber => HasStage
            ? StageParser.GetStageNumber(stageId)
            : stageNumber;

        public DungeonDifficultyDisplayData(int stageNumber, bool isUnlocked, long recommendedPower)
        {
            stageId = default;
            this.stageNumber = stageNumber;
            this.isUnlocked = isUnlocked;
            this.recommendedPower = recommendedPower;
        }

        public DungeonDifficultyDisplayData(eStage stageId, bool isUnlocked, long recommendedPower)
        {
            this.stageId = stageId;
            stageNumber = StageParser.GetStageNumber(stageId);
            this.isUnlocked = isUnlocked;
            this.recommendedPower = recommendedPower;
        }
    }
}
