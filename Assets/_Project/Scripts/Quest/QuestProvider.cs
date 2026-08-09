using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IQuestDefinitionProvider
{
    IReadOnlyList<QuestDefinition> GetQuestDefinitions();
    QuestDefinition GetQuestById(long questId);
}

public class QuestProvider : MonoBehaviour, IQuestDefinitionProvider
{
    [SerializeField] private QuestDatabaseSO questDatabase;

    public IReadOnlyList<QuestDefinition> GetQuestDefinitions() => questDatabase.Quests;
    public QuestDefinition GetQuestById(long questId) => questDatabase.GetQuestById(questId); 
}
