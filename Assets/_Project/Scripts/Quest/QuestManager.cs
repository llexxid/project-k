using System;
using System.Collections.Generic;
using System.Linq;
using KingdomIdle.Balance;
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


/// <summary>
/// bootstrap 씬이 소유하는 퀘스트 UI 창구다. 저장 완료 알림을 불변 snapshot으로 투영하고,
/// 실제 달라진 행만 발행한다. 게임플레이 집계·보상 지급은 QuestEconomy와 같은 거래 경계에 남긴다.
/// </summary>
public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance;
    public event Action<QuestRuntimeState, QuestDefinition> OnGuideQuestChanged;
    public event Action<QuestRuntimeState, QuestDefinition> OnQuestProgressChanged;
    public event Action<QuestChangeSet> QuestsChanged;
    public IQuestRewardGranter RewardGranter => null;

    private static readonly eQuestCategory[] Categories =
        { eQuestCategory.Guide, eQuestCategory.Daily, eQuestCategory.Weekly, eQuestCategory.Achievement };
    private readonly QuestBoardSnapshot[] _boards = new QuestBoardSnapshot[4];
    private readonly QuestBoardSnapshot[] _published = new QuestBoardSnapshot[4];
    private QuestRowSnapshot _publishedGuide;
    private long _cachedRevision = -1, _cachedGeneration = -1;
    private bool _cacheReady, _dirty = true, _accountChanged, _suspended, _paused, _started;
    private bool _focused = true;
    private float _nextPeriodCheck;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        if (Instance != this) return;
        LocalProgression.Changed += MarkDirty;
        LocalProgression.AccountChanged += OnAccountChanged;
        _dirty = true;
        if (_started) UpdateActivityState();
    }

    private void OnDisable()
    {
        LocalProgression.Changed -= MarkDirty;
        LocalProgression.AccountChanged -= OnAccountChanged;
        if (Instance == this) { _suspended = true; BattleEconomy.Suspend(); }
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        BattleEconomy.Suspend();
        Instance = null;
    }

    private void Start()
    {
        if (Instance != this) return;
        _started = true;
        Synchronize(true);
        PublishChanges();
    }

    private void Update()
    {
        if (_suspended || Time.unscaledTime < _nextPeriodCheck) return;
        _nextPeriodCheck = Time.unscaledTime + .5f;
        Synchronize();
    }

    private void LateUpdate() { if (_dirty) PublishChanges(); }

    /// <summary>Initialization and period writes belong to lifecycle, never to GetSnapshot.</summary>
    private void Synchronize(bool force = false)
    {
        try
        {
            LocalProgression.Ensure();
            if (LocalProgression.TryGetCommittedState(out var state) &&
                (force || QuestPeriod.NeedsSynchronization(state, LocalProgression.UtcNow)))
                LocalProgression.SynchronizeQuests();
        }
        catch (Exception exception) { Debug.LogWarning("[Quest] Synchronization failed: " + exception.Message); }
    }

    private void OnApplicationPause(bool paused)
    {
        _paused = paused;
        UpdateActivityState();
    }

    private void OnApplicationFocus(bool focused)
    {
        _focused = focused;
        UpdateActivityState();
    }

    /// <summary>Android의 focus/pause 알림 순서와 관계없이 실제 활성 구간만 전투 시간에 포함한다.</summary>
    private void UpdateActivityState()
    {
        if (Instance != this) return;
        bool suspended = _paused || !_focused || !isActiveAndEnabled;
        if (_suspended == suspended) return;
        _suspended = suspended;
        if (suspended) BattleEconomy.Suspend();
        else { Synchronize(true); BattleEconomy.Resume(); MarkDirty(); }
    }

    private void OnApplicationQuit() { if (Instance == this) { _suspended = true; BattleEconomy.Suspend(); } }
    private void MarkDirty() { _dirty = true; }
    private void OnAccountChanged() { _accountChanged = true; _dirty = true; }

    /// <summary>Returns a cached immutable projection. This path never opens an account or writes a save.</summary>
    public QuestBoardSnapshot GetSnapshot(eQuestCategory category)
    {
        // 고정된 0~3 카테고리 범위를 검사해 빈번한 조회에서 enum boxing을 피한다.
        if ((uint)category >= (uint)Categories.Length) return QuestBoardSnapshot.NotReady(category);
        RefreshReadCache();
        return _boards[(int)category];
    }

    public QuestClaimResult TryClaim(QuestClaimToken token)
    {
        var result = QuestEconomy.TryClaim(token);
        // A rejected stale button must also rebind to the current committed account/period.
        _dirty = true;
        return result;
    }

    private void RefreshReadCache()
    {
        bool ready = LocalProgression.TryGetCommittedState(out var state);
        long generation = LocalProgression.AccountGeneration;
        long revision = ready ? state.Revision : -1;
        if (_boards[0] != null && _cacheReady == ready && _cachedGeneration == generation && _cachedRevision == revision) return;
        _cacheReady = ready; _cachedGeneration = generation; _cachedRevision = revision;
        for (int i = 0; i < Categories.Length; i++)
            _boards[i] = ready ? QuestEconomy.GetSnapshot(Categories[i]) : QuestBoardSnapshot.NotReady(Categories[i], generation);
        _dirty = true;
    }

    private void PublishChanges()
    {
        RefreshReadCache();
        _dirty = false;
        var tokens = new HashSet<QuestClaimToken>();
        var categories = new List<eQuestCategory>();
        for (int i = 0; i < Categories.Length; i++)
        {
            var before = _published[i];
            var after = _boards[i];
            bool changed = before == null || before.IsReady != after.IsReady || before.Period != after.Period ||
                before.AccountGeneration != after.AccountGeneration || before.ResetUtc != after.ResetUtc;
            if (before != null)
                foreach (var old in before.Rows)
                {
                    var current = FindRow(after, old.Token);
                    if (!old.ContentEquals(current)) { tokens.Add(old.Token); changed = true; }
                }
            foreach (var current in after.Rows)
                if (before == null || FindRow(before, current.Token) == null) { tokens.Add(current.Token); changed = true; }
            if (changed) categories.Add(Categories[i]);
            _published[i] = after;
        }

        var guide = ActiveGuide(_boards[(int)eQuestCategory.Guide]);
        bool guideChanged = _publishedGuide == null ? guide != null : !_publishedGuide.ContentEquals(guide);
        bool sameGuide = _publishedGuide != null && guide != null && _publishedGuide.Token == guide.Token;
        _publishedGuide = guide;
        bool accountChanged = _accountChanged;
        _accountChanged = false;
        if (categories.Count > 0 || accountChanged)
            Notify(QuestsChanged, new QuestChangeSet(tokens, categories, accountChanged));
        if (guideChanged)
            Notify(sameGuide ? OnQuestProgressChanged : OnGuideQuestChanged, LegacyState(guide),
                guide == null ? null : GetQuestDefinition(guide.Token.QuestId));
    }

    private static QuestRowSnapshot FindRow(QuestBoardSnapshot board, QuestClaimToken token)
    {
        foreach (var row in board.Rows) if (row.Token == token) return row;
        return null;
    }

    private static QuestRowSnapshot ActiveGuide(QuestBoardSnapshot board)
    {
        foreach (var row in board.Rows) if (!row.IsPending && row.State != QuestRowState.Claimed) return row;
        return null;
    }

    // One faulty view must not prevent the other subscribers from receiving committed state.
    private static void Notify<T>(Action<T> handlers, T value)
    {
        if (handlers == null) return;
        foreach (Action<T> handler in handlers.GetInvocationList())
            try { handler(value); } catch (Exception exception) { Debug.LogException(exception); }
    }

    private static void Notify(Action<QuestRuntimeState, QuestDefinition> handlers, QuestRuntimeState state, QuestDefinition definition)
    {
        if (handlers == null) return;
        foreach (Action<QuestRuntimeState, QuestDefinition> handler in handlers.GetInvocationList())
            try { handler(state, definition); } catch (Exception exception) { Debug.LogException(exception); }
    }

    private static QuestRuntimeState LegacyState(QuestRowSnapshot row) => row == null ? null : new QuestRuntimeState
    {
        QuestId = row.Token.QuestId, CurrentProgress = (int)Math.Min(row.RequiredCount, row.Progress),
        IsCompleted = row.IsCompleted, IsRewardClaimed = row.State == QuestRowState.Claimed, ClampToRequire = true
    };

    public QuestDefinition GetQuestDefinition(long id) => QuestEconomy.Definitions.FirstOrDefault(x => x.QuestId == id);

    public QuestRuntimeState GetActiveGuideState()
    {
        return LegacyState(ActiveGuide(GetSnapshot(eQuestCategory.Guide)));
    }

    public QuestRuntimeState AddQuestState(long id)
    {
        var q = GetQuestDefinition(id);
        if (q == null || !LocalProgression.TryGetCommittedState(out var state)) return null;
        int value = (int)Math.Min(q.RequiredCount, QuestEconomy.Progress(q, state));
        string key = QuestEconomy.Key(q, state);
        return new QuestRuntimeState { QuestId = id, CurrentProgress = value,
            IsCompleted = value >= q.RequiredCount || state.PendingQuests.ContainsKey(key),
            IsRewardClaimed = state.Claims.Contains(key), ClampToRequire = true };
    }

    // Kept for old view binding; ordinary progression commits use the diff-based events above.
    public void ClaimUIRefresh()
    {
        var state = GetActiveGuideState();
        Notify(OnGuideQuestChanged, state, state == null ? null : GetQuestDefinition(state.QuestId));
    }

    public void ClaimQuestReward(long id)
    {
        var q = GetQuestDefinition(id);
        if (q == null || !LocalProgression.TryGetCommittedState(out var state)) return;
        string period = q.Category == eQuestCategory.Daily ? state.QuestDay : q.Category == eQuestCategory.Weekly ? state.QuestWeek : "permanent";
        TryClaim(new QuestClaimToken(LocalProgression.AccountGeneration, id, period));
    }

    public bool CanClaimReward(long id) => LocalProgression.TryGetCommittedState(out var state) &&
        QuestEconomy.CanClaim(GetQuestDefinition(id), state);

    // Gameplay already counts inside its transaction. Replaying this legacy event must never count twice.
    public void ApplyQuestEvent(QuestEvent evt) { MarkDirty(); }
    public void RefreshProgressFromSnapshot(QuestRuntimeState state, QuestDefinition quest) { MarkDirty(); }
}
