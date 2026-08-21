using System;
using System.Collections.Generic;
using Scripts.Core.Manager;
using UnityEngine;

namespace Scripts.Core
{
    /// <summary>스테이지가 어떤 규칙으로 진행되는지를 구분한다.</summary>
    public enum eStageFlowType
    {
        MainProgression,
        BossChallenge,
        TimedSurvival,
        KillCountChallenge,

        MaxCount
    }

    /// <summary>같은 eStage ID 체계 안에서 콘텐츠의 종류를 구분한다.</summary>
    public enum eStageType
    {
        Main,
        GoldDungeon,
        RubyDungeon,

        MaxCount
    }

    /// <summary>배경, 마탑 등 한 묶음으로 교체할 환경 프리셋의 ID다.</summary>
    public enum eEnvironmentId
    {
        MainField1,
        MainField2,
        GoldDungeon,
        RubyDungeon
    }

    /// <summary>몬스터가 스테이지 진행 중 어느 시점에 스폰되는지 나타낸다.</summary>
    public enum eMonsterSpawnPhase
    {
        Initial,
        Boss,
        LoopPool
    }

    /// <summary>
    /// StageMonsters 시트의 한 행. 몬스터의 종류나 소환량, 
    /// 좌표 자체를 보관하지 않고 SpawnPointGroupId만 보관하므로 위치 프리셋을 나중에 교체할 수 있다.
    /// </summary>
    [Serializable]
    public struct StageMonsterEntry
    {        
        public eMonsterType MonsterType => (eMonsterType)_monsterTypeValue;
        public int Count => _count;
        public int SpawnWeight => _spawnWeight;
        public eMonsterSpawnPhase SpawnPhase => _spawnPhase;
        public string SpawnPointGroupId => _spawnPointGroupId;
        public float SpawnDelaySec => _spawnDelaySec;
        
        // eMonsterType은 ulong 기반 enum이다. Unity는 64비트 enum을 직접 직렬화하지 못하므로 원시값을 저장한다.
        [SerializeField] private ulong _monsterTypeValue;
        [SerializeField] private int _count;
        [SerializeField] private int _spawnWeight;
        [SerializeField] private eMonsterSpawnPhase _spawnPhase;
        [SerializeField] private string _spawnPointGroupId;
        [SerializeField] private float _spawnDelaySec;

        public StageMonsterEntry(
            eMonsterType monsterType,
            int count,
            int spawnWeight,
            eMonsterSpawnPhase spawnPhase,
            string spawnPointGroupId,
            float spawnDelaySec)
        {
            _monsterTypeValue = (ulong)monsterType;
            _count = count;
            _spawnWeight = spawnWeight;
            _spawnPhase = spawnPhase;
            _spawnPointGroupId = spawnPointGroupId;
            _spawnDelaySec = spawnDelaySec;
        }
    }

    /// <summary>
    /// 한 스테이지를 시작하는 데 필요한 읽기 전용 설정이다.
    /// SO 원본을 직접 노출하지 않고 복사본을 가지므로 실행 중 원본 데이터가 변경되지 않는다.
    /// </summary>
    public sealed class StageDefinition
    {
        public eStage Id { get; } //스테이지의 id값
        public eStage? MainStageId { get; } 
        public eStageType Type { get; } //스테이지 종류(메인 스테이지, 골드던전, 루비던전 ....)
        public eStageFlowType FlowType { get; } //스테이지 진행규칙(메인 스테이지, 보스처치, 몬스터 처치 ....)
        public eEnvironmentId EnvironmentId { get; } //환경 프리셋 그룹(배경+오디오+리소스 등..)의 id

        public int StageNumber { get; } //현재 스테이지의 정보(메인 스테이지에서만 사용)
        public int WaveNumber { get; } //현재 웨이브의 정보(메인 스테이지에서만 사용)
        public double MonsterStatMultiplier { get; } //몬스터 스탯 계수
        public float TimeLimitSec { get; } //제한시간(없으면 0)
        public float LoopSpawnIntervalSec { get; } //몬스터 반복소환 간격
        public int LoopSpawnAliveThreshold { get; } //몬스터 소환 한도
        public ulong ResourceGroupId { get; } //리소스 그룹 id

        public IStageFlowConfig FlowConfig { get; } //각 스테이지 진행마다 필요한 추가정보(보스flow -> 보스 정보, 몬스터 처치flow -> 목표 처치수량 등...)
        public IReadOnlyList<StageMonsterEntry> MonsterEntries => _monsterEntries; //각 스테이지별 등장하는 몬스터의 리스트
        public string SpawnPointSetId { get; } //몬스터 스폰 지점의 id
        public string RewardGroupId { get; } //보상그룹
        public eSFXType? BgmType { get; } //BGM 종류
        public bool Enabled { get; } //활성화 여부

        // 던전 결과 팝업의 "다음 단계" 버튼에서 사용한다. 메인 진행은 MainStageRule이 계산한다.
        public eStage? NextDifficultyId { get; }
        public bool HasNextDifficulty => NextDifficultyId.HasValue;

        private readonly StageMonsterEntry[] _monsterEntries;

        public StageDefinition(
            eStage stageId,
            eStageFlowType flowType,
            eEnvironmentId environmentId,
            double monsterStatMultiplier,
            IReadOnlyList<StageMonsterEntry> monsterEntries,
            string spawnPointSetId,
            float timeLimitSec = 0f,
            IStageFlowConfig flowConfig = null,
            string rewardGroupId = null,
            eSFXType? bgmType = null,
            bool enabled = true,
            eStage? nextDifficultyId = null,
            float loopSpawnIntervalSec = 0f,
            int loopSpawnAliveThreshold = 0)
        {
            if (monsterEntries == null)
                throw new ArgumentNullException(nameof(monsterEntries));

            Id = stageId;
            Type = StageParser.GetStageType(stageId);
            FlowType = flowType;
            EnvironmentId = environmentId;

            // eStage에서 파생할 수 있는 값은 엑셀에 중복 저장하지 않는다.
            StageNumber = StageParser.GetStageNumber(stageId);
            WaveNumber = StageParser.GetWaveNumber(stageId);
            ResourceGroupId = StageParser.GetResourceGroupId(stageId);
            MainStageId = Type == eStageType.Main ? stageId : null;

            MonsterStatMultiplier = monsterStatMultiplier;
            TimeLimitSec = timeLimitSec;
            LoopSpawnIntervalSec = loopSpawnIntervalSec;
            LoopSpawnAliveThreshold = loopSpawnAliveThreshold;
            FlowConfig = flowConfig;
            SpawnPointSetId = spawnPointSetId;
            RewardGroupId = rewardGroupId;
            BgmType = bgmType;
            Enabled = enabled;
            NextDifficultyId = nextDifficultyId;

            _monsterEntries = new StageMonsterEntry[monsterEntries.Count];
            for (int i = 0; i < monsterEntries.Count; i++)
                _monsterEntries[i] = monsterEntries[i];
        }
    }
}
