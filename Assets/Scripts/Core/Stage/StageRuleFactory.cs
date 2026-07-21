using System;
using System.Collections;
using System.Collections.Generic;
using Core.Stage;
using Scripts.Core;
using UnityEngine;

public class StageRuleFactory
{
    public static IStageRule Create(StageDefinition definition)
    {
        if (definition == null)
        {
            throw new ArgumentNullException(nameof(definition));
        }

        return definition.FlowType switch
        {
            eStageFlowType.MainProgression => new MainStageRule(),
            eStageFlowType.BossChallenge => new BossStageRule(),
            eStageFlowType.KillCountChallenge => new KillCountRule(),
            _ => throw new ArgumentOutOfRangeException(
                nameof(definition.FlowType),
                definition.FlowType,
                "지원하지 않는 스테이지 진행 방식입니다.")
        };
    }
}
