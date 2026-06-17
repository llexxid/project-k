using System.Collections;
using System.Collections.Generic;
using Scripts.Core;
using UnityEngine;

namespace Scripts.Core
{
    public class StageClass 
    {
        public eStage CurrentStage { get; private set; }
        public int StageNumber { get; private set; }
        public int WaveNumber { get; private set; }
        public bool IsBossWave { get; private set; }
        public bool IsLoopMode { get; private set; }
        public void SetLoopMode(bool value) => IsLoopMode = value;
        public bool BossAutoChallenge { get; private set; }
        public void SetBossAutoChallenge(bool value) => BossAutoChallenge = value;
        
        public void Reset(eStage stage)
        {
            CurrentStage = stage;
            StageNumber = StageRule.GetStageNumber(stage);
            WaveNumber = StageRule.GetWaveNumber(stage);
            IsBossWave = (StageRule.GetWaveNumber(stage) == (int)StageRule.BossWaveNumber);
            IsLoopMode = false;
            BossAutoChallenge = true;
        }
    
        public eStageResult MoveNext()
        {
            eStageResult result = StageRule.GetNextWave(CurrentStage, out eStage nextStage);
    
            CurrentStage = nextStage;
            StageNumber = StageRule.GetStageNumber(CurrentStage);
            WaveNumber = StageRule.GetWaveNumber(CurrentStage);
            IsBossWave = result == eStageResult.BossWaveEntered;
    
            if (result == eStageResult.WaveChanged)
                IsLoopMode = false;
    
            return result;
        }
    
        public eStageResult MovePrev()
        {
            eStageResult result = StageRule.GetPreviousWave(CurrentStage, out eStage prevStage);
    
            //스테이지의 이동이 없었을 때
            if (result == eStageResult.None) 
                return result;
            CurrentStage = prevStage;
            StageNumber = StageRule.GetStageNumber(CurrentStage);
            WaveNumber = StageRule.GetWaveNumber(CurrentStage);
            IsBossWave = false;
            IsLoopMode = true;
            
            return result;
        }
    
        public void EnterBossWave()
        {
            CurrentStage = StageRule.GetBossStage(CurrentStage);
            StageNumber = StageRule.GetStageNumber(CurrentStage);
            WaveNumber = StageRule.GetWaveNumber(CurrentStage);
            IsBossWave = true;
        }
    }
}
