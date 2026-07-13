using System;
using System.Collections.Generic;
using Scripts.Core.SO;

namespace Scripts.Core
{
    //컨텐츠가 진행되는 방식
    public enum eStageFlowType
    {
        MainProgress, //메인 스토리
        BossKill, //보스 처치후 종료
        Survival, //일정시간 생존
        KillCount, //특정 마릿수 처치
        
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
    /// <summary>
    /// 특정 웨이브를 구성하는 정적 설정을 나타낸다.
    /// 실제 리소스 핸들과 생성된 몬스터는 소유하지 않는다.
    /// </summary>
    public sealed class StageDefinition
    {
        private readonly StageMetaDataSO.StageInfo_v[] _monsterEntries;

        public long ContentId { get;  }
        public eStage? MainStageId { get; }
        public eStageType Type { get; }
        public int StageNumber { get; }
        public int WaveNumber { get; }
        public bool IsBossWave { get; }
        public ulong ResourceGroupId { get; }
        public double SpawnRatio { get; }
        public int TotalMonsterCount { get; }

        public IReadOnlyList<StageMetaDataSO.StageInfo_v> MonsterEntries => _monsterEntries;

        public StageDefinition(
            eStage mainStageId,
            ulong resourceGroupId,
            double spawnRatio,
            IReadOnlyList<StageMetaDataSO.StageInfo_v> monsterEntries)
        {
            MainStageId = mainStageId;
            StageNumber = StageRule.GetStageNumber(mainStageId);
            WaveNumber = StageRule.GetWaveNumber(mainStageId);
            IsBossWave = StageRule.IsBossWave(mainStageId);
            ResourceGroupId = resourceGroupId;
            SpawnRatio = spawnRatio;

            if (monsterEntries == null)
            {
                _monsterEntries = Array.Empty<StageMetaDataSO.StageInfo_v>();
                return;
            }

            _monsterEntries = new StageMetaDataSO.StageInfo_v[monsterEntries.Count];

            int totalMonsterCount = 0;
            for (int i = 0; i < monsterEntries.Count; i++)
            {
                StageMetaDataSO.StageInfo_v entry = monsterEntries[i];
                _monsterEntries[i] = entry;

                if (entry._count > 0)
                    totalMonsterCount += entry._count;
            }

            TotalMonsterCount = totalMonsterCount;
        }
    }
}
