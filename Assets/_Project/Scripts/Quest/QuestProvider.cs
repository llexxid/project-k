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

    public IReadOnlyList<QuestDefinition> GetQuestDefinitions() => KingdomIdle.Balance.QuestEconomy.Definitions;
    public QuestDefinition GetQuestById(long questId) => System.Linq.Enumerable.FirstOrDefault(GetQuestDefinitions(), x => x.QuestId == questId);
}
