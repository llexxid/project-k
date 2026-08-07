using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(menuName = "Quest/Quest Database")]
public class QuestDatabaseSO : ScriptableObject
{
    [SerializeField] private List<QuestDefinition> quests = new();

    private Dictionary<long, QuestDefinition> questDic;
    public IReadOnlyList<QuestDefinition> Quests => quests;

    public QuestDefinition GetQuestById(long questId)
    {
        EnsureDic();
        
        return questDic.GetValueOrDefault(questId);
    }
    private void EnsureDic()
    {
        if (questDic != null && questDic.Count == quests.Count) return;

        questDic = new Dictionary<long, QuestDefinition>();

        foreach (QuestDefinition quest in quests)
        {
            if (quest == null)
                continue;

            if (!questDic.TryAdd(quest.QuestId, quest))
            {
                Debug.LogWarning($"Duplicate quest id: {quest.QuestId}");
            }
        }
    }
}
