using System;
using System.Collections;
using System.Collections.Generic;
using Core.Stage;
using Scripts.Core;
using UnityEngine;

public interface IStageFlowConfig
{
    string ConfigId { get; }
}

public sealed class BossChallengeConfig : IStageFlowConfig
{
    public string ConfigId { get; }

    public eMonsterType BossMonsterType { get; }
    public bool ClearRemainingMonsters { get; }
    public eStageFlowAction DefeatAction { get; }

    public BossChallengeConfig(
        string configId,
        eMonsterType bossMonsterType,
        bool clearRemainingMonsters,
        eStageFlowAction defeatAction)
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
    public eStageFlowAction DefeatAction { get; }

    public KillCountChallengeConfig(
        string configId,
        int requiredKillCount,
        eMonsterType? targetMonsterType,
        eStageFlowAction defeatAction)
    {
        ConfigId = configId;
        RequiredKillCount = requiredKillCount;
        TargetMonsterType = targetMonsterType;
        DefeatAction = defeatAction;
    }
}
