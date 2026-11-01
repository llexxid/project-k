using System;
using System.Collections.Generic;
using System.Linq;
using KingdomIdle.Balance;
using PlayFab;
using Scripts.Core;
using Scripts.Core.Manager;
using UnityEngine;
using Dto = Protocol.Message.Dto;

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
/// bootstrap 씬이 소유하는 퀘스트 UI 창구다. 퀘스트 진행·달성·수령은 서버가 판정한다.
/// 로그인·OnGetUserState·OnGetQuestState·OnClaimQuestReward 응답의 Quests(QuestStateDTO)를 그대로 보관하고,
/// 카테고리별 불변 snapshot(QuestBoardSnapshot)으로 투영해 실제 달라진 행만 발행한다. 로컬 집계·달성 판정·보상 지급은 하지 않는다.
/// 사냥·장비·가챠 응답에는 Quests가 없으므로 패널·가이드 HUD가 보이는 동안 주기적으로, 그리고 서버 조작이 끝난 뒤 OnGetQuestState로 다시 받는다.
/// 로그 태그 [Quest].
/// </summary>
public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance;
    public event Action<QuestRuntimeState, QuestDefinition> OnGuideQuestChanged;
    public event Action<QuestRuntimeState, QuestDefinition> OnQuestProgressChanged;
    public event Action<QuestChangeSet> QuestsChanged;
    /// <summary>수령 요청 완료. (토큰, 성공 여부, 실패 문구)</summary>
    public event Action<QuestClaimToken, bool, string> OnClaimCompleted;
    public IQuestRewardGranter RewardGranter => null;

    /// <summary>수령 요청이 서버 응답을 기다리는 중.</summary>
    public bool IsBusy { get; private set; }
    /// <summary>OnGetQuestState 응답을 기다리는 중.</summary>
    public bool IsRefreshing { get; private set; }
    /// <summary>마지막 서버 거절 또는 적용 실패. Business 오류면 CustomData에 서버 ErrorResponseDTO가 들어 있다.</summary>
    public PlayFabError LastFailure { get; private set; }
    /// <summary>마지막으로 받은 서버 퀘스트 상태. 로그인 전에는 null이다.</summary>
    public Dto.QuestStateDTO ServerQuests => _server;
    public int ServerRowCount => _server?.Rows?.Count ?? -1;
    public bool HasServerData => _server != null;
    /// <summary>로그인 응답으로 받아 둔 행 수. 매니저가 아직 없을 때도 클라이언트가 쥐고 있는 값이다([PlayerData] 로그 대조용).</summary>
    public static int ReceivedRowCount => _received?.Rows?.Count ?? -1;

    private const float PanelRefreshIntervalSec = 10f;
    private const float HudRefreshIntervalSec = 30f;
    private const float MinRefreshGapSec = 3f;
    private static readonly eQuestCategory[] Categories =
        { eQuestCategory.Guide, eQuestCategory.Daily, eQuestCategory.Weekly, eQuestCategory.Achievement };
    // 로그인 응답이 매니저보다 먼저 올 때(타이틀 씬) 보관해 두고 Start에서 적용한다.
    private static Dto.QuestStateDTO _received;
    private static string _receivedSource;
    private Dto.QuestStateDTO _server;
    private long _revision;
    private readonly QuestBoardSnapshot[] _boards = new QuestBoardSnapshot[4];
    private readonly QuestBoardSnapshot[] _published = new QuestBoardSnapshot[4];
    private QuestRowSnapshot _publishedGuide;
    private long _cachedRevision = -1, _cachedGeneration = -1;
    private bool _dirty = true, _accountChanged, _suspended, _paused, _started, _refreshQueued;
    private int _panelWatchers, _hudWatchers;
    private float _lastRefreshAt = float.NegativeInfinity;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        if (Instance != this) return;
        LocalProgression.AccountChanged += OnAccountChanged;
        _dirty = true;
        if (_started) UpdateActivityState();
    }

    private void OnDisable()
    {
        LocalProgression.AccountChanged -= OnAccountChanged;
        if (Instance == this) _suspended = true;
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        Instance = null;
    }

    private void Start()
    {
        if (Instance != this) return;
        _started = true;
        if (_received != null && _server == null) Apply(_received, _receivedSource ?? "login");
        PublishChanges();
    }

    /// <summary>패널(10초)·가이드 HUD(30초)가 보일 때 주기적으로, 그리고 서버 조작 뒤 요청된 갱신을 최소 간격(3초)으로 보낸다.</summary>
    private void Update()
    {
        if (_suspended || IsRefreshing || IsBusy) return;
        var network = NetworkManager.Instance;
        if (network == null || !network.IsGameDataReady || _server == null && !_refreshQueued) return;
        float since = Time.unscaledTime - _lastRefreshAt;
        float interval = _panelWatchers > 0 ? PanelRefreshIntervalSec : _hudWatchers > 0 ? HudRefreshIntervalSec : float.PositiveInfinity;
        if (since >= interval || (_refreshQueued && since >= MinRefreshGapSec)) Fetch(network);
    }

    private void LateUpdate() { if (_dirty) PublishChanges(); }

    private void OnApplicationPause(bool paused)
    {
        _paused = paused;
        UpdateActivityState();
    }

    /// <summary>포커스를 되찾으면 한 번 갱신한다. 포커스가 없어도(배치 모드·백그라운드 창) 갱신은 막지 않는다. 일시정지(pause)만 멈춘다.</summary>
    private void OnApplicationFocus(bool focused)
    {
        if (focused) _refreshQueued = true;
    }

    /// <summary>Android의 pause 알림에 맞춰 실제 활성 구간에서만 서버 갱신을 보낸다.</summary>
    private void UpdateActivityState()
    {
        if (Instance != this) return;
        bool suspended = _paused || !isActiveAndEnabled;
        if (_suspended == suspended) return;
        _suspended = suspended;
        if (!suspended) { _refreshQueued = true; MarkDirty(); }
    }

    private void MarkDirty() { _dirty = true; }
    private void OnAccountChanged() { _accountChanged = true; _dirty = true; _server = null; _revision++; }

    // ── 서버 상태 수신 ─────────────────────────────────────────────────

    /// <summary>로그인·OnGetUserState 응답의 Quests를 전달한다(UserManager가 호출). 매니저가 아직 없으면 Start에서 적용한다.</summary>
    public static void Receive(Dto.QuestStateDTO quests, string source)
    {
        if (quests == null) return;
        _received = quests;
        _receivedSource = source;
        if (Instance != null) Instance.Apply(quests, source);
    }

    /// <summary>서버 퀘스트 상태를 그대로 보관하고 화면용 snapshot을 다시 만든다. 로컬 값과 비교하지 않는다.</summary>
    public void Apply(Dto.QuestStateDTO quests, string source)
    {
        if (quests == null) return;
        _server = quests;
        _revision++;
        _lastRefreshAt = Time.unscaledTime;
        _refreshQueued = false;
        Rebuild();
        _dirty = true;
        Debug.Log(DescribeLine(source, true));
    }

    private string DescribeLine(string source, bool saved)
    {
        int claimable = 0, claimed = 0, pending = 0;
        if (_server?.Rows != null)
            foreach (var row in _server.Rows)
            {
                if (row == null) continue;
                if (row.State == Dto.QuestRowState.Claimable) claimable++;
                if (row.State == Dto.QuestRowState.Claimed) claimed++;
                if (row.IsPending) pending++;
            }
        var guide = ActiveGuide(_boards[(int)eQuestCategory.Guide]);
        return $"[Quest] source={source} rows={ServerRowCount} claimable={claimable} claimed={claimed} pending={pending} daily={_server?.DailyPeriodKey ?? "-"} weekly={_server?.WeeklyPeriodKey ?? "-"} serverTime={_server?.ServerTimeUtc ?? 0}" +
            $" | state rows={ServerRowCount} guide={(guide != null ? guide.Token.QuestId.ToString() : "-")} | saved={saved}";
    }

    /// <summary>다음 Update에서 OnGetQuestState를 보낸다(최소 3초 간격). 장비·가챠·클리어 같은 서버 조작 뒤에 호출한다.</summary>
    public void RequestRefresh(string reason) { _refreshQueued = true; }

    /// <summary>퀘스트 패널(panel=true)이나 가이드 HUD가 보이는 동안 주기 갱신을 켠다. 패널은 열 때 즉시 한 번 갱신한다.</summary>
    public void AddWatcher(bool panel)
    {
        if (panel) { _panelWatchers++; _refreshQueued = true; }
        else _hudWatchers++;
    }

    public void RemoveWatcher(bool panel)
    {
        if (panel) _panelWatchers = Math.Max(0, _panelWatchers - 1);
        else _hudWatchers = Math.Max(0, _hudWatchers - 1);
    }

    private void Fetch(NetworkManager network)
    {
        IsRefreshing = true;
        _refreshQueued = false;
        network.Call<Dto.OnGetQuestStateRequestDTO, Dto.OnGetQuestStateResponseDTO>(new Dto.OnGetQuestStateRequestDTO(),
            response => { IsRefreshing = false; Apply(response.Quests, "state"); },
            error =>
            {
                IsRefreshing = false;
                _lastRefreshAt = Time.unscaledTime;
                LastFailure = error;
                int code = NetworkManager.TryGetBusinessError(error, out var business) ? (int)business.ErrorCode : 0;
                Debug.LogWarning($"[Quest] state rejected code={code} kind={NetworkManager.GetFailureKind(error)} message={error?.ErrorMessage}");
            });
    }

    // ── 수령 ─────────────────────────────────────────────────────────

    /// <summary>
    /// 화면에 표시했던 토큰(퀘스트·기간)으로 OnClaimQuestReward를 요청한다. 보상·상태는 응답의 Currency·Equipment·MageTowerState·Quests를 그대로 쓴다.
    /// 요청이 나가면 Pending을 돌려주고 OnClaimCompleted로 결과를 알린다.
    /// </summary>
    public QuestClaimResult TryClaim(QuestClaimToken token)
    {
        if (!LocalProgression.IsReady) return new QuestClaimResult(QuestClaimStatus.NotReady, token);
        if (token.AccountGeneration != LocalProgression.AccountGeneration) return new QuestClaimResult(QuestClaimStatus.StaleAccount, token);
        if (token.QuestId <= 0 || string.IsNullOrEmpty(token.Period)) return new QuestClaimResult(QuestClaimStatus.UnknownQuest, token);
        if (IsBusy) return new QuestClaimResult(QuestClaimStatus.Busy, token);
        var network = NetworkManager.Instance;
        if (network == null || !network.IsGameDataReady) return new QuestClaimResult(QuestClaimStatus.NetworkNotReady, token);
        IsBusy = true;
        LastFailure = null;
        network.Call<Dto.OnClaimQuestRewardRequestDTO, Dto.OnClaimQuestRewardResponseDTO>(new Dto.OnClaimQuestRewardRequestDTO { QuestId = token.QuestId, PeriodKey = token.Period },
            response => ApplyClaim(token, response), error => RejectClaim(token, error));
        return new QuestClaimResult(QuestClaimStatus.Pending, token);
    }

    private void ApplyClaim(QuestClaimToken token, Dto.OnClaimQuestRewardResponseDTO response)
    {
        bool saved;
        try
        {
            saved = LocalProgression.Execute("server-quest-claim", s =>
            {
                UserManager.ApplyCurrency(s, response.Currency);
                UserManager.ApplyEquipment(s, response.Equipment);
                UserManager.ApplyMageTower(s, response.MageTowerState);
                return true;
            });
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            saved = false;
        }
        IsBusy = false;
        Apply(response.Quests, "claim");
        var state = LocalProgression.State;
        Debug.Log($"[Quest] claim quest={token.QuestId} period={token.Period} rewards={response.GrantedRewards?.Count ?? 0}" +
            $" | server Gold={response.Currency?.Gold} AncientCoin={response.Currency?.AncientCoin} ClassFragment={response.Currency?.ClassFragment} ArcaneKnowledge={response.Currency?.ArcaneKnowledge} Inventory={UserManager.DescribeInventory(response.Equipment)}" +
            $" | state Gold={LocalProgression.Balance(eCurrency.Gold)} AncientCoin={LocalProgression.Balance(eCurrency.AncientCoin)} ClassFragment={LocalProgression.Balance(eCurrency.ClassFragment)} ArcaneKnowledge={LocalProgression.Balance(eCurrency.ArcaneKnowledge)} Inventory={UserManager.DescribeInventory(state)} saved={saved}");
        if (!saved)
        {
            Direction.GameDirectInteraction.ReportSaveFailure();
            LastFailure = NetworkManager.CreateError("ApplyFailed", "서버 퀘스트 보상을 저장하지 못했습니다: " + LocalProgression.LastError, "OnClaimQuestReward");
            Notify(OnClaimCompleted, token, false, "서버 결과를 저장하지 못했습니다. 다시 로그인하면 서버 값으로 맞춰집니다.");
            return;
        }
        EquipmentManager.Instance?.RestoreEquipment();
        KingdomIdle.MageTower.MageTowerManager.Instance?.NotifyCommitted();
        Notify(OnClaimCompleted, token, true, null);
    }

    private void RejectClaim(QuestClaimToken token, PlayFabError error)
    {
        LastFailure = error;
        int code = NetworkManager.TryGetBusinessError(error, out var business) ? (int)business.ErrorCode : 0;
        string kind = NetworkManager.GetFailureKind(error);
        Debug.LogWarning($"[Quest] claim rejected quest={token.QuestId} period={token.Period} code={code} kind={kind} message={error?.ErrorMessage}");
        IsBusy = false;
        if (code == 0 && (kind == "Timeout" || kind == "Transport") && UserManager.Instance != null)
        {
            // 응답 유실: 서버가 이미 지급했을 수 있으므로 재전송하지 않고 플레이어 데이터(퀘스트 포함)를 다시 받는다.
            UserManager.Instance.RefreshFromServer(ok => Notify(OnClaimCompleted, token, false,
                ok ? "서버 응답이 늦어 퀘스트 상태를 서버에서 다시 불러왔습니다. 목록을 확인해 주세요." : NetworkManager.DescribeFailure(error, "보상 수령")));
            return;
        }
        // 거절 뒤에도 서버 행 상태가 바뀌었을 수 있으니(이미 수령 등) 다음 갱신을 예약한다.
        _refreshQueued = true;
        Notify(OnClaimCompleted, token, false, NetworkManager.DescribeFailure(error, "보상 수령"));
    }

    // ── 화면용 snapshot ───────────────────────────────────────────────

    /// <summary>Returns a cached immutable projection. This path never opens an account or writes a save.</summary>
    public QuestBoardSnapshot GetSnapshot(eQuestCategory category)
    {
        // 고정된 0~3 카테고리 범위를 검사해 빈번한 조회에서 enum boxing을 피한다.
        if ((uint)category >= (uint)Categories.Length) return QuestBoardSnapshot.NotReady(category);
        RefreshReadCache();
        return _boards[(int)category];
    }

    private void RefreshReadCache()
    {
        long generation = LocalProgression.AccountGeneration;
        if (_boards[0] != null && _cachedGeneration == generation && _cachedRevision == _revision) return;
        _cachedGeneration = generation; _cachedRevision = _revision;
        Rebuild();
        _dirty = true;
    }

    /// <summary>
    /// 서버 Rows를 카테고리별 snapshot으로 바꾼다. 서버는 미해금 체인 단계도 Locked 행으로 주므로 가이드·업적의 Locked 행은
    /// 이전 화면과 같게 숨긴다(일일·주간의 Locked는 1-11 해금 전이라 표시한다). ObjectiveType·PresentationType은 enum 이름 문자열이다.
    /// </summary>
    private void Rebuild()
    {
        long generation = LocalProgression.AccountGeneration;
        var rows = new List<QuestRowSnapshot>[Categories.Length];
        for (int i = 0; i < rows.Length; i++) rows[i] = new List<QuestRowSnapshot>();
        if (_server?.Rows != null)
            foreach (var row in _server.Rows)
            {
                if (row == null) continue;
                int index = (int)row.Category;
                if (index < 0 || index >= Categories.Length) continue;
                var category = Categories[index];
                if ((category == eQuestCategory.Guide || category == eQuestCategory.Achievement) && row.State == Dto.QuestRowState.Locked) continue;
                if (!Enum.TryParse(row.ObjectiveType, true, out eQuestObjectiveType objective)) objective = eQuestObjectiveType.StageClear;
                if (!Enum.TryParse(row.PresentationType, true, out eQuestPresentationType presentation)) presentation = eQuestPresentationType.None;
                var rewards = new List<QuestRewardSnapshot>();
                if (row.Rewards != null)
                    foreach (var reward in row.Rewards)
                        if (reward != null && reward.Kind == Dto.RewardKind.Currency && reward.CurrencyType.HasValue)
                            rewards.Add(new QuestRewardSnapshot((eCurrency)(int)reward.CurrencyType.Value, reward.Amount, reward.IsDynamicGold));
                var state = (QuestRowState)(int)row.State;
                int required = (int)Math.Max(1, Math.Min(int.MaxValue, row.RequiredCount));
                long progress = state == QuestRowState.Claimed ? required : Math.Min(required, Math.Max(0, row.Progress));
                rows[index].Add(new QuestRowSnapshot(new QuestClaimToken(generation, row.QuestId, row.PeriodKey), category, row.Title, row.Description,
                    objective, row.TargetId, required, progress, state, row.IsPending, row.ExpiresUtc, rewards, presentation));
            }
        for (int i = 0; i < Categories.Length; i++)
        {
            var category = Categories[i];
            if (_server == null) { _boards[i] = QuestBoardSnapshot.NotReady(category, generation); continue; }
            string period = category == eQuestCategory.Daily ? _server.DailyPeriodKey : category == eQuestCategory.Weekly ? _server.WeeklyPeriodKey : "permanent";
            long reset = category == eQuestCategory.Daily ? _server.DailyResetUtc : category == eQuestCategory.Weekly ? _server.WeeklyResetUtc : 0;
            _boards[i] = new QuestBoardSnapshot(generation, _revision, category, period, reset, rows[i]);
        }
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
        if (board == null) return null;
        foreach (var row in board.Rows) if (row.Token == token) return row;
        return null;
    }

    private static QuestRowSnapshot ActiveGuide(QuestBoardSnapshot board)
    {
        if (board == null) return null;
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

    private static void Notify(Action<QuestClaimToken, bool, string> handlers, QuestClaimToken token, bool ok, string message)
    {
        if (handlers == null) return;
        foreach (Action<QuestClaimToken, bool, string> handler in handlers.GetInvocationList())
            try { handler(token, ok, message); } catch (Exception exception) { Debug.LogException(exception); }
    }

    private static QuestRuntimeState LegacyState(QuestRowSnapshot row) => row == null ? null : new QuestRuntimeState
    {
        QuestId = row.Token.QuestId, CurrentProgress = (int)Math.Min(row.RequiredCount, row.Progress),
        IsCompleted = row.IsCompleted, IsRewardClaimed = row.State == QuestRowState.Claimed, ClampToRequire = true
    };

    /// <summary>표시용 정의(제목·설명·목표·보상 그룹). 서버 정의 파일과 같은 catalog.json을 읽는다.</summary>
    public QuestDefinition GetQuestDefinition(long id) => QuestCatalog.Instance.Get(id);

    public QuestRuntimeState GetActiveGuideState()
    {
        return LegacyState(ActiveGuide(GetSnapshot(eQuestCategory.Guide)));
    }

    private QuestRowSnapshot FindRow(long id)
    {
        QuestRowSnapshot found = null;
        for (int i = 0; i < Categories.Length; i++)
            foreach (var row in GetSnapshot(Categories[i]).Rows)
            {
                if (row.Token.QuestId != id) continue;
                if (row.State == QuestRowState.Claimable) return row;
                found ??= row;
            }
        return found;
    }

    public QuestRuntimeState AddQuestState(long id) => LegacyState(FindRow(id));

    // Kept for old view binding; ordinary progression commits use the diff-based events above.
    public void ClaimUIRefresh()
    {
        var state = GetActiveGuideState();
        Notify(OnGuideQuestChanged, state, state == null ? null : GetQuestDefinition(state.QuestId));
    }

    public void ClaimQuestReward(long id)
    {
        var row = FindRow(id);
        if (row != null && row.CanClaim) TryClaim(row.Token);
    }

    public bool CanClaimReward(long id) => FindRow(id)?.CanClaim == true;

    // Gameplay counting moved to the server. Replaying this legacy event only marks the view dirty.
    public void ApplyQuestEvent(QuestEvent evt) { MarkDirty(); }
    public void RefreshProgressFromSnapshot(QuestRuntimeState state, QuestDefinition quest) { MarkDirty(); }
}
