using System.Collections;
using System.Collections.Generic;
using Scripts.Core;
using Scripts.Core.Manager;
using Scripts.Core.SO;
using UnityEngine;

public interface IStageDefinitionProvider
{
    bool TryGet(
        eStage id,
        out StageDefinition definition);
}

public sealed class LegacyStageDefinitionProvider : IStageDefinitionProvider
{
    private readonly StageMetaDataSO _stageSO;

    public LegacyStageDefinitionProvider(StageMetaDataSO stageSo)
    {
        _stageSO = stageSo;
    }

    public bool TryGet(eStage id, out StageDefinition definition)
    {
        eStageType type = StageParser.GetStageType(id);

        eStage lookupId = type == eStageType.Main ? StageParser.GetFixedStageKey(id) : id;
        if (!_stageSO.TryGetStageInfo(lookupId, out var entries))
        {
            definition = null;
            return false;
        }

        switch (type)
        {
            case eStageType.Main:
                definition = CreateMainDefinition(id, entries);
                return true;
            case eStageType.GoldDungeon:
                definition = CreateGoldDungeonDefinition(id, entries);
                return true;
            default:
                definition = null;
                return false;
        }
    }
    
    private StageDefinition CreateMainDefinition(
        eStage stageId,
        IReadOnlyList<StageMetaDataSO.StageInfo_v> entries)
    {
        int stageNumber =
            StageParser.GetStageNumber(stageId);

        int waveNumber =
            StageParser.GetWaveNumber(stageId);

        return new StageDefinition(
            stageId: stageId,
            flowType: eStageFlowType.MainProgress,
            environment: stageNumber % 2 == 1
                ? eEnvironment.Main1
                : eEnvironment.Main2,
            monsterStatMultiplier:
            StageParser.GetRatio(stageId),
            monsterEntries: entries,
            timeLimitSec:
            waveNumber == 11 ? 30f : 0f);
    }

    private StageDefinition CreateGoldDungeonDefinition(
        eStage stageId,
        IReadOnlyList<StageMetaDataSO.StageInfo_v> entries)
    {
        var flowConfig = new BossChallengeConfig(
            configId: "TEMP_GOLD_01",
            bossMonsterType: eMonsterType.BANDIT_KING,
            clearRemainingMonsters: true,
            defeatAction:
            eStageDefeatAction.RetryOrReturn);

        return new StageDefinition(
            stageId: stageId,
            flowType: eStageFlowType.BossChallenge,
            environment: eEnvironment.GoldDungeon,
            monsterStatMultiplier: 1.0,
            monsterEntries: entries,
            timeLimitSec: 30f,
            flowConfig: flowConfig);
    }
}
