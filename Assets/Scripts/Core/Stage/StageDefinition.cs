using System;
using System.Collections.Generic;
using Scripts.Core.Manager;
using Scripts.Core.SO;
using UnityEngine;

namespace Scripts.Core
{
    //컨텐츠가 진행되는 방식
    public enum eStageFlowType
    {
        MainProgress, //메인 스토리
        BossChallenge, //보스 처치후 종료
        TimedSurvival, //일정시간 생존
        KillCountChallenge, //특정 마릿수 처치
        
        MaxCount
    }
    //컨텐츠의 종류
    public enum eStageType
    {
        Main,
        GoldDungeon, //골드 지급 던전
        RubyDungeon, //루비 지급 던전
        
        MaxCount //타입 카운팅용
    }

    //각 스테이지별 리소스(배경, 몬스터 등..)
    public enum eEnvironment
    {
        Main1,
        Main2,
        GoldDungeon,
        RubyDungeon
    }
    /// <summary>
    /// 특정 웨이브를 구성하는 정적 설정을 나타낸다.
    /// 실제 리소스 핸들과 생성된 몬스터는 소유하지 않는다.
    /// </summary>
    public sealed class StageDefinition
    {
        public eStage Id { get;  }
        public eStage? MainStageId { get; } //현재 스테이지 타입이 메인이면 Id, 아니면 null 반환
        public eStageType Type { get; }
        public eStageFlowType FlowType { get; }
        public eEnvironment Environment { get; } 
        
        public int StageNumber { get; }
        public int WaveNumber { get; }
        public double MonsterStatMultiplier { get; }
        
        public float TimeLimitSec { get; }
        public ulong ResourceGroupId { get; }
        public IStageFlowConfig FlowConfig { get; }
        
        public IReadOnlyList<StageMetaDataSO.StageInfo_v> MonsterEntries => _monsterEntries;
        public string RewardGroupId { get; }
        public eSFXType? BgmType { get; }
        public bool Enabled { get; }
        
        private readonly StageMetaDataSO.StageInfo_v[] _monsterEntries;
        
        public List<Vector2> SpawnPointSet { get; }
        

        public StageDefinition(
            eStage stageId,
            eStageFlowType flowType,
            eEnvironment environment,
            double monsterStatMultiplier,
            IReadOnlyList<StageMetaDataSO.StageInfo_v> monsterEntries,
            float timeLimitSec = 0f,
            IStageFlowConfig flowConfig = null,
            string rewardGroupId = null,
            eSFXType? bgmType = null,
            bool enabled = true)
        {
            Id = stageId;
            Type = StageParser.GetStageType(stageId);
            FlowType = flowType;
            Environment = environment;

            StageNumber = StageParser.GetStageNumber(stageId);
            WaveNumber = StageParser.GetWaveNumber(stageId);
            ResourceGroupId = StageParser.GetResourceGroupId(stageId);

            MainStageId = Type == eStageType.Main
                ? stageId
                : null;

            MonsterStatMultiplier = monsterStatMultiplier;
            TimeLimitSec = timeLimitSec;
            FlowConfig = flowConfig;
            RewardGroupId = rewardGroupId;
            BgmType = bgmType;
            Enabled = enabled;
            
            _monsterEntries = new StageMetaDataSO.StageInfo_v[monsterEntries.Count];

            int totalMonsterCount = 0;
            for (int i = 0; i < monsterEntries.Count; i++)
            {
                StageMetaDataSO.StageInfo_v entry = monsterEntries[i];
                _monsterEntries[i] = entry;

                if (entry._count > 0)
                    totalMonsterCount += entry._count;
            }
        }
    }
}
