using System.Collections;
using System.Collections.Generic;
using Scripts.Core;
using Scripts.Core.Manager;
using UnityEngine;

public class QuestProgressSnapshot : MonoBehaviour, IQuestProgressSnapshot
{
    public int GetCurrentStateValue(eQuestObjectiveType type, long targetId)
    {
        if (type == eQuestObjectiveType.StageClear)
            return StageManager.Instance.IsStageCleared((eStage)targetId) ? 1 : 0;

        return 0;

    }

    public int GetLifetimeValue(eQuestObjectiveType objectiveType, long targetId)
    {
        return 0;
    }
}
