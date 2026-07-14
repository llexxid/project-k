using System.Collections;
using System.Collections.Generic;
using Scripts.Core;
using UnityEngine;

public enum eStageDefeatAction
{
    RetryOrReturn,
    Retry,
    ReturnToMain
}
public interface IStageFlowConfig
{
    string ConfigId { get; }
}

public sealed class BossChallengeConfig : IStageFlowConfig
{
    public string ConfigId { get; }

    public eMonsterType BossMonsterType { get; }
    public bool ClearRemainingMonsters { get; }
    public eStageDefeatAction DefeatAction { get; }

    public BossChallengeConfig(
        string configId,
        eMonsterType bossMonsterType,
        bool clearRemainingMonsters,
        eStageDefeatAction defeatAction)
    {
        ConfigId = configId;
        BossMonsterType = bossMonsterType;
        ClearRemainingMonsters = clearRemainingMonsters;
        DefeatAction = defeatAction;
    }
}

public sealed class KillCountChallengeConfig : IStageFlowConfig
{
    public string ConfigId { get; }

    public int RequiredKillCount { get; }
    public eMonsterType? TargetMonsterType { get; }
    public float SpawnIntervalSec { get; }
    public int MaxAliveCount { get; }
    public eStageDefeatAction DefeatAction { get; }

    public KillCountChallengeConfig(
        string configId,
        int requiredKillCount,
        eMonsterType? targetMonsterType,
        float spawnIntervalSec,
        int maxAliveCount,
        eStageDefeatAction defeatAction)
    {
        ConfigId = configId;
        RequiredKillCount = requiredKillCount;
        TargetMonsterType = targetMonsterType;
        SpawnIntervalSec = spawnIntervalSec;
        MaxAliveCount = maxAliveCount;
        DefeatAction = defeatAction;
    }
}