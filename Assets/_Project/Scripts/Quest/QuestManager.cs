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

/// <summary>Grant idempotently by quest ID. False preserves the pending reward.</summary>
public interface IQuestRewardGranter { bool TryGrant(long questId, int rewardGroupId); }


public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance;
    public event Action<QuestRuntimeState, QuestDefinition> OnGuideQuestChanged;
    public event Action<QuestRuntimeState, QuestDefinition> OnQuestProgressChanged;
    public IQuestRewardGranter RewardGranter => null;
    private void Awake() { if (Instance != null && Instance != this) { Destroy(gameObject); return; } Instance = this; DontDestroyOnLoad(gameObject); }
    private void OnEnable() { KingdomIdle.Balance.LocalProgression.Changed += ClaimUIRefresh; }
    private void OnDisable() { KingdomIdle.Balance.LocalProgression.Changed -= ClaimUIRefresh; }
    private void Start() { ClaimUIRefresh(); }
    public QuestDefinition GetQuestDefinition(long id) => System.Linq.Enumerable.FirstOrDefault(KingdomIdle.Balance.QuestEconomy.Definitions, x => x.QuestId == id);
    public QuestRuntimeState GetActiveGuideState()
    {
        var s = KingdomIdle.Balance.LocalProgression.State;
        var q = System.Linq.Enumerable.FirstOrDefault(KingdomIdle.Balance.QuestEconomy.Definitions, x => x.Category == eQuestCategory.Guide && !s.Claims.Contains(KingdomIdle.Balance.QuestEconomy.Key(x,s)));
        return q == null ? null : AddQuestState(q.QuestId);
    }
    public QuestRuntimeState AddQuestState(long id)
    {
        var q = GetQuestDefinition(id); if (q == null) return null;
        var s = KingdomIdle.Balance.LocalProgression.State;
        int value = (int)Math.Min(q.RequiredCount, KingdomIdle.Balance.QuestEconomy.Progress(q,s));
        return new QuestRuntimeState { QuestId = id, CurrentProgress = value, IsCompleted = value >= q.RequiredCount || s.PendingQuests.ContainsKey(KingdomIdle.Balance.QuestEconomy.Key(q,s)), IsRewardClaimed = s.Claims.Contains(KingdomIdle.Balance.QuestEconomy.Key(q,s)), ClampToRequire = true };
    }
    public void ClaimUIRefresh() { var state = GetActiveGuideState(); OnGuideQuestChanged?.Invoke(state, state == null ? null : GetQuestDefinition(state.QuestId)); }
    public void ClaimQuestReward(long id) { KingdomIdle.Balance.QuestEconomy.Claim(id); }
    public bool CanClaimReward(long id) => KingdomIdle.Balance.QuestEconomy.CanClaim(GetQuestDefinition(id), KingdomIdle.Balance.LocalProgression.State);
    public void ApplyQuestEvent(QuestEvent evt) { ClaimUIRefresh(); }
    public void RefreshProgressFromSnapshot(QuestRuntimeState state, QuestDefinition quest) { OnQuestProgressChanged?.Invoke(AddQuestState(quest.QuestId), quest); }
}
