using System;
using System.Collections.Generic;
using Scripts.Core.SO;

namespace Scripts.Core
{
    /// <summary>
    /// 특정 웨이브를 구성하는 정적 설정을 나타낸다.
    /// 실제 리소스 핸들과 생성된 몬스터는 소유하지 않는다.
    /// </summary>
    public sealed class StageDefinition
    {
        private readonly StageMetaDataSO.StageInfo_v[] _monsterEntries;

        public eStage Id { get; }
        public int StageNumber { get; }
        public int WaveNumber { get; }
        public bool IsBossWave { get; }
        public ulong ResourceGroupId { get; }
        public double SpawnRatio { get; }
        public int TotalMonsterCount { get; }

        public IReadOnlyList<StageMetaDataSO.StageInfo_v> MonsterEntries => _monsterEntries;

        public StageDefinition(
            eStage id,
            ulong resourceGroupId,
            double spawnRatio,
            IReadOnlyList<StageMetaDataSO.StageInfo_v> monsterEntries)
        {
            Id = id;
            StageNumber = StageRule.GetStageNumber(id);
            WaveNumber = StageRule.GetWaveNumber(id);
            IsBossWave = StageRule.IsBossWave(id);
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
