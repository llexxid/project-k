using System.Collections.Generic;
using Scripts.Core;
using Scripts.Core.Manager;
using Scripts.Core.SO;
using UnityEngine;

/// <summary>
/// StageDatabaseSO의 직렬화 레코드를 실제 게임에서 사용하는 StageDefinition으로 변환한다.
/// 같은 StageId는 실행 중 여러 번 요청될 수 있으므로 생성한 Definition을 캐시한다.
/// </summary>
public sealed class StageDefinitionProvider : IStageDefinitionProvider
{
    private readonly StageDatabaseSO _database;
    private readonly Dictionary<eStage, StageDefinition> _cache =
        new Dictionary<eStage, StageDefinition>();

    public StageDefinitionProvider(StageDatabaseSO database)
    {
        _database = database;
    }

    public bool TryGet(eStage id, out StageDefinition definition)
    {
        if (_cache.TryGetValue(id, out definition))
            return true;

        if (_database == null)
        {
            definition = null;
            return false;
        }

        eStage lookupId = id;
        if (!_database.TryGetStage(lookupId, out StageDatabaseRecord record))
        {
            // 메인은 엑셀의 1·2스테이지를 반복 템플릿으로 사용한다.
            // 예를 들어 Stage3_1은 Stage1_1의 몬스터/환경 데이터를 사용하되 Id는 Stage3_1로 유지한다.
            if (StageParser.GetStageType(id) != eStageType.Main)
            {
                definition = null;
                return false;
            }

            lookupId = StageParser.GetFixedStageKey(id);
            if (!_database.TryGetStage(lookupId, out record))
            {
                definition = null;
                return false;
            }
        }

        bool usesMainTemplate = lookupId != id;

        if (!record.Enabled)
        {
            Debug.LogWarning($"[StageDefinitionProvider] 비활성화된 스테이지입니다: {id}");
            definition = null;
            return false;
        }

        if (!TryCreateFlowConfig(record, out IStageFlowConfig flowConfig))
        {
            definition = null;
            return false;
        }

        eStage? nextDifficultyId = _database.TryGetNextDifficulty(id, out eStage nextId)
            ? nextId
            : (eStage?)null;

        definition = new StageDefinition(
            // 템플릿을 사용하는 가상 메인 스테이지만 StageParser의 성장 배율을 추가로 적용한다.
            stageId: id,
            flowType: record.FlowType,
            environmentId: record.EnvironmentId,
            monsterStatMultiplier: usesMainTemplate
                ? record.MonsterStatMultiplier * StageParser.GetRatio(id)
                : record.MonsterStatMultiplier,
            monsterEntries: record.MonsterEntries,
            spawnPointSetId: record.SpawnPointSetId,
            timeLimitSec: record.TimeLimitSec,
            flowConfig: flowConfig,
            loopSpawnIntervalSec: record.LoopSpawnIntervalSec,
            loopSpawnAliveThreshold: record.LoopSpawnAliveThreshold,
            rewardGroupId: record.RewardGroupId,
            bgmType: record.HasBgm ? record.BgmType : (eSFXType?)null,
            enabled: record.Enabled,
            nextDifficultyId: nextDifficultyId);

        _cache.Add(id, definition);
        return true;
    }

    /// <summary>
    /// FlowType에 맞는 설정 시트를 찾아 IStageFlowConfig 구현체로 바꾼다.
    /// MainProgression은 MainStageRule 자체에 진행 규칙이 있으므로 별도 설정이 필요 없다.
    /// </summary>
    private bool TryCreateFlowConfig(StageDatabaseRecord stage, out IStageFlowConfig flowConfig)
    {
        switch (stage.FlowType)
        {
            case eStageFlowType.MainProgression:
                flowConfig = null;
                return true;

            case eStageFlowType.BossChallenge:
                if (!_database.TryGetBossFlow(stage.FlowConfigId, out BossFlowRecord bossFlow))
                {
                    Debug.LogError(
                        $"[StageDefinitionProvider] BossFlow를 찾을 수 없습니다. " +
                        $"Stage: {stage.Id}, FlowConfigId: {stage.FlowConfigId}");
                    flowConfig = null;
                    return false;
                }

                flowConfig = new BossChallengeConfig(
                    bossFlow.ConfigId,
                    bossFlow.BossMonsterType,
                    bossFlow.ClearRemainingMonsters,
                    bossFlow.DefeatAction);
                return true;

            case eStageFlowType.KillCountChallenge:
                if (!_database.TryGetKillCountFlow(stage.FlowConfigId, out KillCountFlowRecord killFlow))
                {
                    Debug.LogError(
                        $"[StageDefinitionProvider] KillCountFlow를 찾을 수 없습니다. " +
                        $"Stage: {stage.Id}, FlowConfigId: {stage.FlowConfigId}");
                    flowConfig = null;
                    return false;
                }

                // TargetsAnyMonster가 true면 null을 전달해 모든 몬스터 처치를 카운트한다.
                eMonsterType? targetMonsterType = killFlow.TargetsAnyMonster
                    ? (eMonsterType?)null
                    : killFlow.TargetMonsterType;

                flowConfig = new KillCountChallengeConfig(
                    killFlow.ConfigId,
                    killFlow.RequiredKillCount,
                    targetMonsterType,
                    killFlow.DefeatAction);
                return true;

            default:
                Debug.LogError(
                    $"[StageDefinitionProvider] 아직 지원하지 않는 FlowType입니다. " +
                    $"Stage: {stage.Id}, FlowType: {stage.FlowType}");
                flowConfig = null;
                return false;
        }
    }
}
