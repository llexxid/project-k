using System;
using System.Collections.Generic;
using Core.Stage;
using Scripts.Core.Manager;
using UnityEngine;

namespace Scripts.Core.SO
{
    /// <summary>StageDefinitions 시트 한 행을 직렬화하는 원본 레코드다.</summary>
    [Serializable]
    public sealed class StageDatabaseRecord
    {
        [SerializeField] private long _idValue;
        [SerializeField] private eStageFlowType _flowType;
        [SerializeField] private eEnvironmentId _environmentId;
        [SerializeField] private double _monsterStatMultiplier;
        [SerializeField] private string _spawnPointSetId;
        [SerializeField] private string _flowConfigId;
        [SerializeField] private string _rewardGroupId;
        [SerializeField] private float _timeLimitSec;
        [SerializeField] private float _loopSpawnIntervalSec;
        [SerializeField] private int _loopSpawnAliveThreshold;
        [SerializeField] private bool _hasBgm;
        [SerializeField] private ulong _bgmTypeValue;
        [SerializeField] private bool _enabled;
        [SerializeField] private List<StageMonsterEntry> _monsterEntries;

        public eStage Id => (eStage)_idValue;
        public eStageFlowType FlowType => _flowType;
        public eEnvironmentId EnvironmentId => _environmentId;
        public double MonsterStatMultiplier => _monsterStatMultiplier;
        public string SpawnPointSetId => _spawnPointSetId;
        public string FlowConfigId => _flowConfigId;
        public string RewardGroupId => _rewardGroupId;
        public float TimeLimitSec => _timeLimitSec;
        public float LoopSpawnIntervalSec => _loopSpawnIntervalSec;
        public int LoopSpawnAliveThreshold => _loopSpawnAliveThreshold;
        public bool HasBgm => _hasBgm;
        public eSFXType BgmType => (eSFXType)_bgmTypeValue;
        public bool Enabled => _enabled;
        public IReadOnlyList<StageMonsterEntry> MonsterEntries => _monsterEntries;

        public StageDatabaseRecord(
            eStage id,
            eStageFlowType flowType,
            eEnvironmentId environmentId,
            double monsterStatMultiplier,
            string spawnPointSetId,
            string flowConfigId,
            string rewardGroupId,
            float timeLimitSec,
            float loopSpawnIntervalSec,
            int loopSpawnAliveThreshold,
            bool hasBgm,
            eSFXType bgmType,
            bool enabled,
            List<StageMonsterEntry> monsterEntries)
        {
            _idValue = (long)id;
            _flowType = flowType;
            _environmentId = environmentId;
            _monsterStatMultiplier = monsterStatMultiplier;
            _spawnPointSetId = spawnPointSetId;
            _flowConfigId = flowConfigId;
            _rewardGroupId = rewardGroupId;
            _timeLimitSec = timeLimitSec;
            _loopSpawnIntervalSec = loopSpawnIntervalSec;
            _loopSpawnAliveThreshold = loopSpawnAliveThreshold;
            _hasBgm = hasBgm;
            _bgmTypeValue = (ulong)bgmType;
            _enabled = enabled;
            _monsterEntries = monsterEntries ?? new List<StageMonsterEntry>();
        }
    }

    /// <summary>BossFlow 시트 한 행을 직렬화한다.</summary>
    [Serializable]
    public sealed class BossFlowRecord
    {
        [SerializeField] private string _configId;
        [SerializeField] private ulong _bossMonsterTypeValue;
        [SerializeField] private bool _clearRemainingMonsters;
        [SerializeField] private eStageFlowAction _defeatAction;

        public string ConfigId => _configId;
        public eMonsterType BossMonsterType => (eMonsterType)_bossMonsterTypeValue;
        public bool ClearRemainingMonsters => _clearRemainingMonsters;
        public eStageFlowAction DefeatAction => _defeatAction;

        public BossFlowRecord(
            string configId,
            eMonsterType bossMonsterType,
            bool clearRemainingMonsters,
            eStageFlowAction defeatAction)
        {
            _configId = configId;
            _bossMonsterTypeValue = (ulong)bossMonsterType;
            _clearRemainingMonsters = clearRemainingMonsters;
            _defeatAction = defeatAction;
        }
    }

    /// <summary>KillCountFlow 시트 한 행을 직렬화한다.</summary>
    [Serializable]
    public sealed class KillCountFlowRecord
    {
        [SerializeField] private string _configId;
        [SerializeField] private int _requiredKillCount;
        [SerializeField] private bool _targetsAnyMonster;
        [SerializeField] private ulong _targetMonsterTypeValue;
        [SerializeField] private eStageFlowAction _defeatAction;

        public string ConfigId => _configId;
        public int RequiredKillCount => _requiredKillCount;
        public bool TargetsAnyMonster => _targetsAnyMonster;
        public eMonsterType TargetMonsterType => (eMonsterType)_targetMonsterTypeValue;
        public eStageFlowAction DefeatAction => _defeatAction;

        public KillCountFlowRecord(
            string configId,
            int requiredKillCount,
            bool targetsAnyMonster,
            eMonsterType targetMonsterType,
            eStageFlowAction defeatAction)
        {
            _configId = configId;
            _requiredKillCount = requiredKillCount;
            _targetsAnyMonster = targetsAnyMonster;
            _targetMonsterTypeValue = (ulong)targetMonsterType;
            _defeatAction = defeatAction;
        }
    }

    /// <summary>
    /// Stage_Revised.xlsx를 Unity가 런타임에 읽을 수 있는 형태로 변환한 데이터베이스다.
    /// 엑셀은 빌드에 포함하지 않고, Editor 생성기가 이 SO의 직렬화 목록을 갱신한다.
    /// </summary>
    [CreateAssetMenu(fileName = "StageDatabaseSO", menuName = "SO/Stage Database")]
    public sealed class StageDatabaseSO : ScriptableObject
    {
        [SerializeField] private List<StageDatabaseRecord> _stages = new List<StageDatabaseRecord>();
        [SerializeField] private List<BossFlowRecord> _bossFlows = new List<BossFlowRecord>();
        [SerializeField] private List<KillCountFlowRecord> _killCountFlows = new List<KillCountFlowRecord>();

        private Dictionary<eStage, StageDatabaseRecord> _stageById;
        private Dictionary<string, BossFlowRecord> _bossFlowById;
        private Dictionary<string, KillCountFlowRecord> _killCountFlowById;
        private Dictionary<eStage, eStage> _nextDifficultyById;
        private Dictionary<ulong, eMonsterType[]> _monsterTypesByResourceGroup;

        /// <summary>직렬화 목록을 빠른 조회용 Dictionary로 한 번 변환한다.</summary>
        public void Init()
        {
            _stageById = new Dictionary<eStage, StageDatabaseRecord>();
            _bossFlowById = new Dictionary<string, BossFlowRecord>(StringComparer.Ordinal);
            _killCountFlowById = new Dictionary<string, KillCountFlowRecord>(StringComparer.Ordinal);
            _nextDifficultyById = new Dictionary<eStage, eStage>();

            var monsterSets = new Dictionary<ulong, HashSet<eMonsterType>>();

            foreach (StageDatabaseRecord stage in _stages)
            {
                if (!_stageById.TryAdd(stage.Id, stage))
                {
                    Debug.LogError($"[StageDatabase] 중복 StageId: {stage.Id}");
                    continue;
                }

                ulong resourceGroupId = StageParser.GetResourceGroupId(stage.Id);
                if (!monsterSets.TryGetValue(resourceGroupId, out HashSet<eMonsterType> monsterTypes))
                {
                    monsterTypes = new HashSet<eMonsterType>();
                    monsterSets.Add(resourceGroupId, monsterTypes);
                }

                foreach (StageMonsterEntry entry in stage.MonsterEntries)
                    monsterTypes.Add(entry.MonsterType);
            }

            foreach (BossFlowRecord flow in _bossFlows)
                AddFlow(_bossFlowById, flow.ConfigId, flow, "BossFlow");

            foreach (KillCountFlowRecord flow in _killCountFlows)
                AddFlow(_killCountFlowById, flow.ConfigId, flow, "KillCountFlow");

            _monsterTypesByResourceGroup = new Dictionary<ulong, eMonsterType[]>();
            foreach (KeyValuePair<ulong, HashSet<eMonsterType>> pair in monsterSets)
            {
                var values = new eMonsterType[pair.Value.Count];
                pair.Value.CopyTo(values);
                _monsterTypesByResourceGroup.Add(pair.Key, values);
            }

            BuildNextDifficultyLookup();
        }

        public bool TryGetStage(eStage id, out StageDatabaseRecord record)
        {
            EnsureInitialized();
            return _stageById.TryGetValue(id, out record);
        }

        public bool TryGetBossFlow(string configId, out BossFlowRecord record)
        {
            EnsureInitialized();
            return _bossFlowById.TryGetValue(configId ?? string.Empty, out record);
        }

        public bool TryGetKillCountFlow(string configId, out KillCountFlowRecord record)
        {
            EnsureInitialized();
            return _killCountFlowById.TryGetValue(configId ?? string.Empty, out record);
        }

        public bool TryGetNextDifficulty(eStage id, out eStage nextId)
        {
            EnsureInitialized();
            return _nextDifficultyById.TryGetValue(id, out nextId);
        }

        public bool TryGetMonsterTypes(eStage resourceGroupId, out IReadOnlyList<eMonsterType> monsterTypes)
        {
            EnsureInitialized();
            ulong normalizedId = StageParser.GetResourceGroupId(resourceGroupId);

            // 실제 Stage3 이후 메인 ID는 엑셀에 없으므로 1·2스테이지 템플릿의 리소스 그룹으로 매핑한다.
            if (!_monsterTypesByResourceGroup.ContainsKey(normalizedId) &&
                StageParser.GetStageType(resourceGroupId) == eStageType.Main)
            {
                eStage templateId = StageParser.GetFixedStageKey(resourceGroupId);
                normalizedId = StageParser.GetResourceGroupId(templateId);
            }

            if (_monsterTypesByResourceGroup.TryGetValue(normalizedId, out eMonsterType[] values))
            {
                monsterTypes = values;
                return true;
            }

            monsterTypes = null;
            return false;
        }

        private void EnsureInitialized()
        {
            if (_stageById == null)
                Init();
        }

        private static void AddFlow<T>(Dictionary<string, T> dictionary, string id, T flow, string sheetName)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                Debug.LogError($"[StageDatabase] {sheetName}에 빈 ConfigId가 있습니다.");
                return;
            }

            if (dictionary.ContainsKey(id))
            {
                Debug.LogError($"[StageDatabase] {sheetName} 중복 ConfigId: {id}");
                return;
            }

            dictionary.Add(id, flow);
        }

        /// <summary>
        /// 엑셀에 NextStageId를 반복 입력하지 않고 같은 던전 타입의 다음 Stage 번호를 찾는다.
        /// 메인 스테이지의 다음 웨이브 계산은 MainStageRule의 책임이므로 이 조회에서 제외한다.
        /// </summary>
        private void BuildNextDifficultyLookup()
        {
            var firstStageByDifficulty = new Dictionary<eStageType, SortedDictionary<int, eStage>>();

            foreach (StageDatabaseRecord stage in _stages)
            {
                if (!stage.Enabled)
                    continue;

                eStageType type = StageParser.GetStageType(stage.Id);
                if (type == eStageType.Main)
                    continue;

                int difficulty = StageParser.GetStageNumber(stage.Id);
                int wave = StageParser.GetWaveNumber(stage.Id);

                if (!firstStageByDifficulty.TryGetValue(type, out SortedDictionary<int, eStage> stages))
                {
                    stages = new SortedDictionary<int, eStage>();
                    firstStageByDifficulty.Add(type, stages);
                }

                // 같은 난이도에 여러 웨이브가 생기면 가장 앞 웨이브를 입장점으로 사용한다.
                if (!stages.TryGetValue(difficulty, out eStage first) ||
                    wave < StageParser.GetWaveNumber(first))
                {
                    stages[difficulty] = stage.Id;
                }
            }

            foreach (KeyValuePair<eStageType, SortedDictionary<int, eStage>> typePair in firstStageByDifficulty)
            {
                var ordered = new List<eStage>(typePair.Value.Values);
                for (int i = 0; i < ordered.Count - 1; i++)
                {
                    int currentDifficulty = StageParser.GetStageNumber(ordered[i]);
                    eStage nextDifficulty = ordered[i + 1];

                    // 해당 난이도의 모든 웨이브가 같은 다음 난이도를 가리키게 한다.
                    foreach (StageDatabaseRecord stage in _stages)
                    {
                        if (stage.Enabled &&
                            StageParser.GetStageType(stage.Id) == typePair.Key &&
                            StageParser.GetStageNumber(stage.Id) == currentDifficulty)
                        {
                            _nextDifficultyById[stage.Id] = nextDifficulty;
                        }
                    }
                }
            }
        }

#if UNITY_EDITOR
        /// <summary>Editor 생성기만 호출한다. 엑셀 전체 내용을 한 번에 교체한다.</summary>
        public void ReplaceData(
            List<StageDatabaseRecord> stages,
            List<BossFlowRecord> bossFlows,
            List<KillCountFlowRecord> killCountFlows)
        {
            _stages = stages ?? new List<StageDatabaseRecord>();
            _bossFlows = bossFlows ?? new List<BossFlowRecord>();
            _killCountFlows = killCountFlows ?? new List<KillCountFlowRecord>();

            // 생성 직후 Inspector나 검증 코드에서도 최신 목록을 조회할 수 있도록 캐시를 즉시 다시 만든다.
            Init();
        }
#endif
    }
}
