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

    /// <summary>기존 씬의 Provider 참조를 유지하면서 검증된 공통 카탈로그를 제공한다.</summary>
    public IReadOnlyList<QuestDefinition> GetQuestDefinitions() => KingdomIdle.Balance.QuestCatalog.Instance.Definitions;
    public QuestDefinition GetQuestById(long questId) => KingdomIdle.Balance.QuestCatalog.Instance.Get(questId);
}
