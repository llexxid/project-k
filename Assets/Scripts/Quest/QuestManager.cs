using System;
using System.Collections.Generic;
using Scripts.Core;
using Scripts.Core.Manager;
using Scripts.Monster;
using UnityEngine;

[Serializable]
public class QuestRuntimeState
{
    public long QuestId;
    public int CurrentProgress;
    public bool IsCompleted;
    public bool IsRewardClaimed;
    public bool ClampToRequire;
}

public enum eQuestEventType
{
    StageCleared,
    MonsterKilled,
    LevelUp,
    Enhance,
    EquipmentObtained,
    EquipmentEquipped,
    CharacterDeployed,
    ItemUsed,
    MailboxOpened,
    GachaOpened,
    GachaUsed,
    DungeonEntered,
    DungeonCleared,
    JobChanged,
    SkillEquipped
}

public struct QuestEvent
{
    public eQuestEventType EventType;
    public long TargetId;
    public int Amount;
}

public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance; 
    
    public event Action<QuestRuntimeState, QuestDefinition> OnGuideQuestChanged;
    public event Action<QuestRuntimeState, QuestDefinition> OnQuestProgressChanged;

    [SerializeField] private MonoBehaviour definitionBehaviour;
    [SerializeField] private MonoBehaviour progressSnapshotBehaviour;
    [SerializeField] private long firstGuideQuestId = 1;

    private IQuestDefinitionProvider definitionProvider;
    private IQuestProgressSnapshot progressSnapshot;
    private readonly Dictionary<long, QuestRuntimeState> questStates = new();

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        definitionProvider = definitionBehaviour as IQuestDefinitionProvider;
        progressSnapshot = progressSnapshotBehaviour as IQuestProgressSnapshot;

        if (definitionProvider == null)
            Debug.LogError("Quest definition provider is missing.");
    }

    private void OnEnable()
    {
        if (StageManager.Instance == null)
            return;

        StageManager.Instance.OnStageCleared += HandleStageClear;
        StageManager.Instance.OnMonsterKilled += HandleMonsterKilled;
    }

    private void OnDisable()
    {
        if (StageManager.Instance == null)
            return;

        StageManager.Instance.OnStageCleared -= HandleStageClear;
        StageManager.Instance.OnMonsterKilled -= HandleMonsterKilled;
    }

    private void Start()
    {
        if (firstGuideQuestId != 0 && !questStates.ContainsKey(firstGuideQuestId))
            AddQuestState(firstGuideQuestId);
    }

    public void ClaimUIRefresh()
    {
        QuestDefinition quest;
        foreach (QuestRuntimeState state in questStates.Values)
        {
            quest = definitionProvider.GetQuestById(state.QuestId);
            if (quest.Category == eQuestCategory.Guide)
            {
                OnGuideQuestChanged?.Invoke(state,quest);
                return;
            }
        }
    }
    public QuestRuntimeState GetActiveGuideState()
    {
        foreach (QuestRuntimeState state in questStates.Values)
        {
            QuestDefinition definition = definitionProvider?.GetQuestById(state.QuestId);
            if (definition != null && definition.Category == eQuestCategory.Guide)
                return state;
        }

        return null;
    }

    public QuestDefinition GetQuestDefinition(long questId)
    {
        return definitionProvider?.GetQuestById(questId);
    }

    public void ClaimQuestReward(long questId)
    {
        if (!questStates.TryGetValue(questId, out QuestRuntimeState state))
            return;

        QuestDefinition quest = definitionProvider?.GetQuestById(state.QuestId);
        if (quest == null || !state.IsCompleted || state.IsRewardClaimed)
            return;

        state.IsRewardClaimed = true;
        // TODO: Give reward by quest.RewardGroupId.

        questStates.Remove(questId);

        if (quest.Category == eQuestCategory.Guide && quest.NextQuestId != 0)
            AddQuestState(quest.NextQuestId);
    }

    public QuestRuntimeState AddQuestState(long questId)
    {
        if (questStates.TryGetValue(questId, out QuestRuntimeState existingState))
            return existingState;

        QuestRuntimeState state = new QuestRuntimeState
        {
            QuestId = questId,
            CurrentProgress = 0,
            IsCompleted = false,
            IsRewardClaimed = false
        };

        questStates.Add(state.QuestId, state);

        QuestDefinition quest = definitionProvider?.GetQuestById(questId);
        if (quest != null)
        {
            RefreshProgressFromSnapshot(state, quest);
            NotifyQuestChanged(state, quest);
        }

        return state;
    }

    public void ApplyQuestEvent(QuestEvent questEvent)
    {
        foreach (QuestRuntimeState state in questStates.Values)
        {
            if (state.IsCompleted)
                continue;

            QuestDefinition quest = definitionProvider?.GetQuestById(state.QuestId);
            if (quest == null)
                continue;

            if (QuestObjectMatcher.CanProgressByEvent(quest, questEvent))
            {
                AddProgress(state, quest, questEvent.Amount);
                continue;
            }

            if (QuestObjectMatcher.ShouldRefreshFromSnapshot(quest, questEvent))
                RefreshProgressFromSnapshot(state, quest);
        }
    }

    public void RefreshProgressFromSnapshot(QuestRuntimeState state, QuestDefinition quest)
    {
        if (!QuestObjectMatcher.TryGetSnapshotProgress(quest, progressSnapshot, out int progress))
            return;

        state.CurrentProgress = progress;

        if (QuestObjectMatcher.IsCompleted(quest, state.CurrentProgress))
            state.IsCompleted = true;

        OnQuestProgressChanged?.Invoke(state, quest);
    }

    private void AddProgress(QuestRuntimeState state, QuestDefinition quest, int amount)
    {
        state.CurrentProgress += amount;

        if (QuestObjectMatcher.IsCompleted(quest, state.CurrentProgress))
            state.IsCompleted = true;

        OnQuestProgressChanged?.Invoke(state, quest);
    }

    private void NotifyQuestChanged(QuestRuntimeState state, QuestDefinition quest)
    {
        if (quest.Category == eQuestCategory.Guide)
            OnGuideQuestChanged?.Invoke(state, quest);

        OnQuestProgressChanged?.Invoke(state, quest);
    }

    private void HandleStageClear(StageDefinition definition)
    {
        ApplyQuestEvent(new QuestEvent
        {
            EventType = eQuestEventType.StageCleared,
            TargetId = (long)definition.Id,
            Amount = 1
        });
    }

    private void HandleMonsterKilled(StageDefinition definition, Monster monster)
    {
        ApplyQuestEvent(new QuestEvent
        {
            EventType = eQuestEventType.MonsterKilled,
            TargetId = (long)monster.Type,
            Amount = 1
        });
    }
}