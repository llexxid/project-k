public interface IQuestProgressSnapshot
{
    /// <summary>
    /// 현재 상태만으로 계산 가능한 목표 진행도를 반환한다
    /// <br/>ex: 스테이지 클리어 여부, 장비 장착 여부, 기능 오픈 여부
    /// </summary>
    int GetCurrentStateValue(eQuestObjectiveType objectiveType, long targetId);  
    /// <summary>
    /// 플레이어의 전체 누적 기록 기준 목표 진행도를 반환한다
    /// <br/>ex: 누적 뽑기 횟수, 누적 처치 수, 누적 강화 횟수
    /// </summary>
    int GetLifetimeValue(eQuestObjectiveType objectiveType, long targetId);
}

public static class QuestObjectMatcher
{
    //퀘스트의 목표와 이벤트 종류가 동일한지 확인 + 대상도 맞는지 확인
    public static bool IsMatchedByEvent(QuestDefinition quest, QuestEvent questEvent)
    {
        if (!IsSameObjective(quest.ObjectiveType, questEvent.EventType))
            return false;
        //TargetId가 0이면 아무거나 허용(ex. 모든 몬스터 처치..), 아니면 해당하는 대상만 허용(ex. 스테이지 1-1(8590000129) 클리어)
        return quest.TargetId == 0 || quest.TargetId == questEvent.TargetId;
    }

    //현재 발행된 이벤트에서 결과를 확인할 수 있는지 검사
    public static bool CanProgressByEvent(QuestDefinition quest, QuestEvent questEvent)
    {
        if (quest.ProgressMode == eQuestProgressMode.CurrentState)
            return false;

        return IsMatchedByEvent(quest, questEvent);
    }
    
    //SnapShot을 최신화 할 필요가 있는지 검사
    public static bool ShouldRefreshFromSnapshot(QuestDefinition quest, QuestEvent questEvent)
    {
        if (quest.ProgressMode != eQuestProgressMode.CurrentState)
            return false;

        return IsMatchedByEvent(quest, questEvent);
    }
    
    //SnapShot에서 진행된 정보(상태 / 누적)을 불러옴
    public static bool TryGetSnapshotProgress(
        QuestDefinition quest,
        IQuestProgressSnapshot snapshot,
        out int progress)
    {
        progress = 0;

        if (snapshot == null)
            return false;

        switch (quest.ProgressMode)
        {
            case eQuestProgressMode.CurrentState:
                progress = snapshot.GetCurrentStateValue(quest.ObjectiveType, quest.TargetId);
                return true;

            case eQuestProgressMode.LifetimeTotal:
                progress = snapshot.GetLifetimeValue(quest.ObjectiveType, quest.TargetId);
                return true;

            default:
                return false;
        }
    }
    
    //퀘스트가 완료되었는지 확인
    public static bool IsCompleted(QuestDefinition quest, int progress)
    {
        return progress >= quest.RequiredCount;
    }

    /// <summary>발행된 이벤트의 타입이 현재 목표와 동일한지 검사</summary>
    /// <returns>일치하면 True, 다르면 False</returns>
    private static bool IsSameObjective(
        eQuestObjectiveType objectiveType,
        eQuestEventType eventType)
    {
        return objectiveType switch
        {
            eQuestObjectiveType.StageClear => eventType == eQuestEventType.StageCleared,
            eQuestObjectiveType.MonsterKill => eventType == eQuestEventType.MonsterKilled,
            eQuestObjectiveType.LevelUp => eventType == eQuestEventType.LevelUp,
            eQuestObjectiveType.Enhance => eventType == eQuestEventType.Enhance,
            eQuestObjectiveType.EquipmentObtain => eventType == eQuestEventType.EquipmentObtained,
            eQuestObjectiveType.EquipmentEquip => eventType == eQuestEventType.EquipmentEquipped,
            eQuestObjectiveType.ItemUse => eventType == eQuestEventType.ItemUsed,
            eQuestObjectiveType.GachaUse => eventType == eQuestEventType.GachaUsed,
            eQuestObjectiveType.DungeonEnter => eventType == eQuestEventType.DungeonEntered,
            eQuestObjectiveType.DungeonClear => eventType == eQuestEventType.DungeonCleared,
            eQuestObjectiveType.JobChange => eventType == eQuestEventType.JobChanged,
            eQuestObjectiveType.SkillEquip => eventType == eQuestEventType.SkillEquipped,
            _ => false
        };
    }
}