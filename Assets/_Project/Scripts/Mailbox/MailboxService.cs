using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using KingdomIdle.Balance;
using KingdomIdle.Rewards;
using PlayFab;
using Protocol.Message.Dto;
using Scripts.Core.Manager;
using Scripts.Core;
using UnityEngine;

namespace KingdomIdle.Mailbox
{
    public enum MailboxState { Empty, Loading, Ready, Claiming, Recovering, RecoveryRequired }

    /// <summary>열려 있는 우편함의 목록과 서버 수령 작업을 소유한다. 닫기는 진행 중인 지급을 취소하지 않는다.</summary>
    public sealed class MailboxService
    {
        internal delegate void ListCall(OnGetMailListRequestDTO request, Action<OnGetMailListResponseDTO> ok, Action<PlayFabError> fail);
        internal delegate void ClaimCall(OnClaimMailRewardRequestDTO request, Action<OnClaimMailRewardResponseDTO> ok, Action<PlayFabError> fail);
        private const int PageSize = 50;
        private const double LoadTimeoutSeconds = 30;
        private const int MaxLoadAttempts = 3;
        private readonly ListCall _list;
        private readonly ClaimCall _claim;
        private readonly Func<OnClaimMailRewardResponseDTO, bool> _apply;
        private readonly Action<Action<bool>> _recover;
        private readonly Func<(long, long)> _identity;
        private readonly Func<bool> _ready;
        private readonly Func<double> _clock;
        private readonly Func<string> _locale;
        private readonly Func<string, long> _readSeen;
        private readonly Action<string, long> _saveSeen;
        private (long, long) _account;
        private (string MailboxId, long MailId) _unseenMail;
        private int _summaryVersion;
        private bool _open, _refreshQueued, _loginChecked;
        private int _loadVersion, _viewVersion;
        private double _retryAt, _observedAt, _readyDeadline;
        private long _serverUtc, _lastExpiredUtc;
        private ClaimRequest _claiming;
        private ListRequest _loading;
        private string _listLocale;

        private sealed class ListRequest
        {
            internal int Version, Retry, Total = -1;
            internal string MailboxId;
            internal string Locale;
            internal double Deadline;
            internal bool Recovery;
            internal (long, long) Account;
            internal readonly List<MailDTO> Mails = new();
            internal readonly HashSet<long> Ids = new();
        }
        private sealed class ClaimRequest
        {
            internal long[] Ids;
            internal int Offset, View;
            internal string MailboxId;
            internal (long, long) Account;
            internal readonly List<long> Confirmed = new();
            internal readonly List<GachaRewardDTO> Rewards = new();
        }

        public event Action Changed;
        public event Action<MailClaimInfo> ClaimCompleted;
        public IReadOnlyList<MailDTO> Messages { get; private set; } = Array.Empty<MailDTO>();
        public string MailboxId { get; private set; }
        public MailboxState State { get; private set; }
        public PlayFabError LastFailure { get; private set; }
        public string Error => LastFailure == null ? null : NetworkManager.DescribeFailure(LastFailure, "우편함");
        public bool HasSnapshot { get; private set; }
        public bool LoadFailed { get; private set; }
        public bool HasNewMail => !_open && _unseenMail.MailId > 0 && _account == _identity() && _ready();
        public bool IsBusy => State == MailboxState.Loading || State == MailboxState.Claiming || State == MailboxState.Recovering;
        public bool CanClaim => _open && HasSnapshot && State == MailboxState.Ready && _account == _identity() && _ready();
        public long ServerNow => _serverUtc + (long)Math.Max(0, _clock() - _observedAt);

        public MailboxService(Func<string> locale)
        {
            _locale = locale;
            _list = (r, ok, fail) => NetworkManager.Instance.Call(r, ok, fail);
            _claim = (r, ok, fail) => NetworkManager.Instance.Call(r, ok, fail);
            _apply = Apply;
            _recover = done => { if (UserManager.Instance == null) done(false); else UserManager.Instance.RefreshFromServer(done); };
            _identity = () => (NetworkManager.Instance != null ? NetworkManager.Instance.AccountGeneration : -1, LocalProgression.AccountGeneration);
            _ready = () => NetworkManager.Instance != null && NetworkManager.Instance.IsGameDataReady && LocalProgression.IsReady &&
                LocalProgression.AccountKey == NetworkManager.Instance.PlayFabId;
            _clock = () => Time.realtimeSinceStartupAsDouble;
            _readSeen = ReadSeen;
            _saveSeen = SaveSeen;
            _account = _identity();
        }

#if UNITY_EDITOR
        // 가짜 응답·계정 전환·저장 실패 검사는 운영의 검증과 완료 처리 경로를 그대로 실행한다.
        internal MailboxService(ListCall list, ClaimCall claim, Func<OnClaimMailRewardResponseDTO, bool> apply,
            Action<Action<bool>> recover, Func<(long, long)> identity, Func<bool> ready, Func<double> clock, Func<string> locale = null,
            Func<string, long> readSeen = null, Action<string, long> saveSeen = null)
        {
            _locale = locale ?? (() => "ko");
            _list = (r, ok, fail) => list(r, response => ValidateTestResponse(response, ok, fail), fail);
            _claim = (r, ok, fail) => claim(r, response => ValidateTestResponse(response, ok, fail), fail);
            _apply = apply; _recover = recover;
            _identity = identity; _ready = ready; _clock = clock; _account = identity();
            _readSeen = readSeen ?? (_ => 0);
            _saveSeen = saveSeen ?? ((_, _) => { });
        }
        private static void ValidateTestResponse<T>(T response, Action<T> ok, Action<PlayFabError> fail) where T : ApiResponseDTO
        {
            try { NetworkManager.ValidateMailboxResponse(response); }
            catch (Exception ex) { fail(NetworkManager.CreateError("InvalidResponse", ex.Message)); return; }
            ok(response);
        }
#endif

        public void Open()
        {
            CheckAccount();
            _open = true; _viewVersion++; _lastExpiredUtc = 0;
            LoadFailed = false;
            if (_unseenMail.MailId > 0) { MarkLatestSeen(); Notify(); }
            RequestRefresh();
        }
        public void Close()
        {
            _open = false; _viewVersion++; _loadVersion++; _retryAt = 0; _refreshQueued = false;
            _loading = null; _readyDeadline = 0; LoadFailed = false;
            ClearList();
            if (State == MailboxState.Loading || State == MailboxState.Ready) State = MailboxState.Empty;
            Notify();
        }
        private void ClearList() { Messages = Array.Empty<MailDTO>(); MailboxId = null; HasSnapshot = false; }

        public void CheckAccount()
        {
            if (_account == _identity()) return;
            _account = _identity(); _loadVersion++; _viewVersion++; _claiming = null;
            _retryAt = 0; _lastExpiredUtc = 0; _refreshQueued = false; _serverUtc = 0;
            _loading = null; _listLocale = null; _readyDeadline = 0; LoadFailed = false; _loginChecked = false;
            _unseenMail = default;
            LastFailure = null; State = MailboxState.Empty; ClearList(); Notify();
            if (_open) RequestRefresh();
        }

        public void CheckAfterLogin()
        {
            CheckAccount();
            if (_loginChecked || !_ready()) return;
            _loginChecked = true;
            if (!_open) CheckNewMail("login");
        }

        internal void RefreshAfterCarryover()
        {
            CheckAccount();
            if (_open) { RequestRefresh("carryover"); return; }
            if (!_ready()) return;
            _loginChecked = true;
            CheckNewMail("carryover");
        }

        // 우편함이 닫혀 있으면 첫 페이지만 확인한다. 열린 목록이나 지급 상태에는 반영하지 않는다.
        private void CheckNewMail(string reason)
        {
            var account = _account;
            int view = _viewVersion;
            int version = ++_summaryVersion;
            bool Current() => account == _identity() && !_open && view == _viewVersion &&
                version == _summaryVersion && _ready();
            _list(new OnGetMailListRequestDTO { Locale = _locale(), StartIndex = 0, Count = 1 }, response =>
            {
                if (!Current() || response.StartIndex != 0 ||
                    response.Mails.Count != Math.Min(1, response.TotalCount)) return;
                var latest = response.Mails.Count == 1 ? response.Mails[0] : null;
                if (latest != null && latest.ExpiresUtc <= response.ServerTimeUtc) return;
                bool wasNew = HasNewMail;
                ObserveLatest(response.MailboxId, latest?.MailId ?? 0);
                if (wasNew != HasNewMail) Notify();
            }, error =>
            {
                if (Current()) Debug.LogWarning($"[Mail] {reason} check failed: {NetworkManager.DescribeFailure(error, "우편함")}");
            });
        }

        private void ObserveLatest(string mailboxId, long mailId)
        {
            _unseenMail = mailId > 0 && mailId > _readSeen(mailboxId) ? (mailboxId, mailId) : default;
            if (_open && _unseenMail.MailId > 0) MarkLatestSeen();
        }

        private void MarkLatestSeen()
        {
            var seen = _unseenMail;
            _unseenMail = default;
            _saveSeen(seen.MailboxId, seen.MailId);
        }

        private static string SeenKey(string mailboxId) =>
            $"ki.mail.seen.v1.{PlayFabSettings.TitleId}.{LocalProgression.AccountKey}.{mailboxId}";

        private static long ReadSeen(string mailboxId) =>
            long.TryParse(PlayerPrefs.GetString(SeenKey(mailboxId)), NumberStyles.None, CultureInfo.InvariantCulture, out long id) ? id : 0;

        private static void SaveSeen(string mailboxId, long id)
        {
            try
            {
                PlayerPrefs.SetString(SeenKey(mailboxId), id.ToString(CultureInfo.InvariantCulture));
                PlayerPrefs.Save();
            }
            catch (PlayerPrefsException ex) { Debug.LogWarning("[Mail] could not save seen state: " + ex.Message); }
        }

        public void RequestRefresh(string reason = "manual")
        {
            CheckAccount();
            if (!_open) return;
            if (IsBusy) { _refreshQueued = true; return; }
            _retryAt = 0;
            if (!_ready())
            {
                if (_readyDeadline == 0) _readyDeadline = _clock() + LoadTimeoutSeconds;
                LastFailure = NetworkManager.CreateError("NotAuthenticated", "서버 로그인이 필요합니다.");
                _retryAt = _clock() + 1; Notify(); return;
            }
            Debug.Log("[Mail] refresh reason=" + reason);
            if (State == MailboxState.RecoveryRequired) RecoverState();
            else Load();
        }

        /// <summary>자동 재시도와 만료만 확인한다. 정상 목록을 주기적으로 다시 요청하지 않는다.</summary>
        public void Tick()
        {
            CheckAccount();
            if (!_open) return;
            if (_readyDeadline > 0 && _clock() >= _readyDeadline)
            { FailList(State == MailboxState.RecoveryRequired, NetworkManager.CreateError("Timeout", "우편함 조회 제한시간이 지났습니다.")); return; }
            if ((State == MailboxState.Loading || State == MailboxState.Ready) && _listLocale != _locale())
            { _loadVersion++; _loading = null; ClearList(); State = MailboxState.Empty; Load(); return; }
            if (_loading != null && _clock() >= _loading.Deadline)
            { FailList(_loading.Recovery, NetworkManager.CreateError("Timeout", "우편함 조회 제한시간이 지났습니다.")); return; }
            if (_loading != null && _retryAt > 0 && _clock() >= _retryAt)
            { Load(_loading.Retry + 1, _loading.Recovery, _loading.Deadline); return; }
            if (!_open || IsBusy || !_ready()) return;
            if (_retryAt > 0 && _clock() >= _retryAt) { RequestRefresh("retry"); return; }
            if (State != MailboxState.Ready || Messages.Count == 0) return;
            long expiry = Messages.Min(m => m.ExpiresUtc);
            if (expiry <= ServerNow && expiry != _lastExpiredUtc)
            { _lastExpiredUtc = expiry; RequestRefresh("expired"); }
        }

        private void Load(int retry = 0, bool recovery = false, double? deadline = null)
        {
            _listLocale = _locale(); LoadFailed = false;
            _refreshQueued = false; _retryAt = 0; LastFailure = null; State = MailboxState.Loading; Notify();
            _loading = new ListRequest { Version = ++_loadVersion, Retry = retry, Recovery = recovery, Account = _account,
                Locale = _listLocale, Deadline = deadline ?? (_readyDeadline > 0 ? _readyDeadline : _clock() + LoadTimeoutSeconds) };
            _readyDeadline = 0;
            if (retry >= MaxLoadAttempts || _clock() >= _loading.Deadline)
            { FailList(_loading.Recovery, NetworkManager.CreateError("Timeout", "우편함 조회를 완료하지 못했습니다.")); return; }
            Fetch(_loading);
        }
        private bool Current(ListRequest request) => _open && request.Version == _loadVersion && request.Account == _identity() && request.Locale == _locale();
        private void Fetch(ListRequest request)
        {
            int start = request.Mails.Count;
            _list(new OnGetMailListRequestDTO { Locale = request.Locale, StartIndex = start, Count = PageSize },
                response => ApplyList(request, start, response), error => RejectList(request, error));
        }
        private void ApplyList(ListRequest request, int start, OnGetMailListResponseDTO response)
        {
            if (!Current(request)) return;
            if (_clock() >= request.Deadline)
            { FailList(request.Recovery, NetworkManager.CreateError("Timeout", "우편함 조회 제한시간이 지났습니다.")); return; }
            if (request.Total < 0) { request.Total = response.TotalCount; request.MailboxId = response.MailboxId; }
            bool consistent = response.StartIndex == start && request.Total == response.TotalCount && request.MailboxId == response.MailboxId &&
                response.Mails.Count == Math.Min(PageSize, Math.Max(0, request.Total - start)) && response.Mails.All(m => request.Ids.Add(m.MailId));
            if (!consistent)
            {
                if (request.Retry == 0) Load(1, request.Recovery, request.Deadline);
                else RejectList(request, NetworkManager.CreateError("InvalidResponse", "우편 목록이 변경되었습니다. 다시 불러와 주세요."));
                return;
            }
            request.Mails.AddRange(response.Mails);
            _serverUtc = response.ServerTimeUtc; _observedAt = _clock();
            if (request.Mails.Count < request.Total) { Fetch(request); return; }
            if (_refreshQueued) { Load(request.Retry + 1, request.Recovery, request.Deadline); return; }
            _loading = null;
            Messages = request.Mails.ToArray(); MailboxId = request.MailboxId; HasSnapshot = true;
            long newestId = 0;
            foreach (var mail in Messages)
                if (mail.ExpiresUtc > ServerNow && mail.MailId > newestId) newestId = mail.MailId;
            ObserveLatest(MailboxId, newestId);
            LastFailure = null; State = MailboxState.Ready;
            Debug.Log($"[Mail] list server={request.Total} cached={Messages.Count} recovery={request.Recovery}");
            Notify();
        }
        private void RejectList(ListRequest request, PlayFabError error)
        {
            if (!Current(request)) return;
            string kind = NetworkManager.GetFailureKind(error);
            bool retryable = kind == "Transport" || kind == "Timeout" ||
                (NetworkManager.TryGetBusinessError(error, out var business) &&
                 (business.ErrorCode == Protocol.Message.Data.eErrorCode.StorageFailure || business.ErrorCode == Protocol.Message.Data.eErrorCode.PlayerBusy));
            if (retryable && request.Retry + 1 < MaxLoadAttempts && _clock() + .5 < request.Deadline)
            { LastFailure = error; _retryAt = _clock() + .5; Notify(); return; }
            FailList(request.Recovery, error);
        }
        private void FailList(bool recovery, PlayFabError error)
        {
            _loading = null; _loadVersion++; _retryAt = 0; _readyDeadline = 0; LoadFailed = true;
            LastFailure = error; HasSnapshot = false;
            State = recovery ? MailboxState.RecoveryRequired : MailboxState.Empty;
            Debug.LogWarning($"[Mail] list rejected kind={NetworkManager.GetFailureKind(error)} message={error?.ErrorMessage}");
            Notify();
        }

        public RewardResult TryClaim(long id) => Claim(new[] { id });
        public RewardResult TryClaimAll() => Claim(Messages.Where(m => m.ExpiresUtc > ServerNow).Select(m => m.MailId).ToArray());
        private RewardResult Claim(long[] ids)
        {
            CheckAccount();
            if (!CanClaim) return new(IsBusy ? RewardStatus.Pending : RewardStatus.NotReady);
            if (ids.Length == 0) return new(RewardStatus.NothingAvailable);
            if (ids.Distinct().Count() != ids.Length || ids.Any(id => !Messages.Any(m => m.MailId == id && m.ExpiresUtc > ServerNow)))
            { RequestRefresh("stale-claim"); return new(RewardStatus.Stale); }
            var request = new ClaimRequest { Ids = ids, MailboxId = MailboxId, Account = _account, View = _viewVersion };
            _claiming = request; LastFailure = null; State = MailboxState.Claiming; Notify();
            ClaimNextBatch(request);
            return new(RewardStatus.Pending);
        }
        private bool Current(ClaimRequest request) => ReferenceEquals(_claiming, request) && request.Account == _identity();
        private void ClaimNextBatch(ClaimRequest request)
        {
            if (!Current(request)) return;
            var ids = request.Ids.Skip(request.Offset).Take(PageSize).ToList();
            _claim(new OnClaimMailRewardRequestDTO { MailIds = ids },
                response => ApplyClaim(request, ids, response), error => RejectClaim(request, error));
        }
        private void ApplyClaim(ClaimRequest request, List<long> ids, OnClaimMailRewardResponseDTO response)
        {
            if (!Current(request)) return;
            try
            {
                if (response.MailboxId != request.MailboxId || !ids.ToHashSet().SetEquals(response.ClaimedMailIds))
                    throw new InvalidOperationException("수령 요청과 응답의 우편 ID가 다릅니다.");
                if (!_apply(response)) throw new InvalidOperationException("우편 수령 결과 저장 실패: " + LocalProgression.LastError);
            }
            catch (Exception ex) { RejectClaim(request, NetworkManager.CreateError("ApplyFailed", ex.Message)); return; }
            if (!Current(request)) return;
            request.Confirmed.AddRange(response.ClaimedMailIds); request.Rewards.AddRange(response.Rewards); request.Offset += ids.Count;
            var removed = response.ClaimedMailIds.ToHashSet();
            Messages = Messages.Where(m => !removed.Contains(m.MailId)).ToArray();
            Debug.Log($"[Mail] confirmed ids={string.Join(".", ids)} batches={request.Offset}/{request.Ids.Length} remaining={response.RemainingCount}");
            Notify();
            if (request.Offset < request.Ids.Length) ClaimNextBatch(request);
            else FinishClaim(request, null, false);
        }
        private void RejectClaim(ClaimRequest request, PlayFabError error)
        {
            if (!Current(request)) return;
            string kind = NetworkManager.GetFailureKind(error);
            bool storage = NetworkManager.TryGetBusinessError(error, out var business) &&
                (business.ErrorCode == Protocol.Message.Data.eErrorCode.StorageFailure || business.ErrorCode == Protocol.Message.Data.eErrorCode.InvalidStoredData);
            bool uncertain = storage || (kind != "Business" && kind != "NotAuthenticated" && kind != "IntegrationPending");
            Debug.LogWarning($"[Mail] claim rejected confirmed={request.Confirmed.Count} kind={kind} recovery={uncertain} message={error?.ErrorMessage}");
            FinishClaim(request, error, uncertain);
        }
        private void FinishClaim(ClaimRequest request, PlayFabError error, bool recover)
        {
            if (!Current(request)) return;
            _claiming = null; LastFailure = error;
            State = recover ? MailboxState.RecoveryRequired : MailboxState.Empty;
            // 화면 재진입은 지급 완료를 취소하지 않지만 이전 화면의 팝업을 다시 열지는 않는다.
            if (_open && request.View == _viewVersion)
                NotifyClaim(new MailClaimInfo { Ok = error == null, MailIds = request.Confirmed.ToArray(),
                    Rewards = RewardGrants.FromServer(request.Rewards), Message = Error,
                    ErrorCode = NetworkManager.TryGetBusinessError(error, out var business) ? (int)business.ErrorCode : 0 });
            if (request.Account != _identity()) return;
            if (recover) RecoverState();
            else if (_open) Load();
            else Notify();
        }
        private void RecoverState()
        {
            if (State == MailboxState.Recovering) return;
            State = MailboxState.Recovering; Notify();
            var account = _account;
            _recover(ok =>
            {
                if (account != _identity()) return;
                if (!ok)
                {
                    State = MailboxState.RecoveryRequired; HasSnapshot = false;
                    LastFailure = NetworkManager.CreateError("ApplyFailed", "서버 상태를 확인하지 못했습니다. 다시 불러와 주세요."); Notify();
                }
                else if (_open) Load(recovery: true);
                else { State = MailboxState.Empty; LastFailure = null; Notify(); }
            });
        }

        internal static bool Apply(OnClaimMailRewardResponseDTO response)
        {
            bool saved = LocalProgression.Execute("server-mail-claim", s =>
            {
                UserManager.ApplyCurrency(s, response.Currency); UserManager.ApplyEquipment(s, response.Equipment);
                s.RewardsRevision++; return true;
            });
            Debug.Log($"[Mail] apply serverGold={response.Currency.Gold} stateGold={LocalProgression.Balance(eCurrency.Gold)} " +
                $"serverInventory={UserManager.DescribeInventory(response.Equipment)} stateInventory={UserManager.DescribeInventory(LocalProgression.State)} saved={saved}");
            if (saved)
            {
                EquipmentManager.Instance?.RestoreEquipment();
                if (response.Rewards.Any(r => r.Kind == RewardKind.Equipment)) QuestManager.Instance?.RequestRefresh("mail");
            }
            return saved;
        }
        private void Notify()
        {
            if (Changed == null) return;
            foreach (Action listener in Changed.GetInvocationList())
                try { listener(); } catch (Exception ex) { Debug.LogException(ex); }
        }
        private void NotifyClaim(MailClaimInfo result)
        {
            if (ClaimCompleted == null) return;
            foreach (Action<MailClaimInfo> listener in ClaimCompleted.GetInvocationList())
                try { listener(result); } catch (Exception ex) { Debug.LogException(ex); }
        }
    }

    public sealed class MailClaimInfo
    {
        public bool Ok { get; internal set; }
        public long[] MailIds { get; internal set; }
        public IReadOnlyList<RewardGrant> Rewards { get; internal set; }
        public string Message { get; internal set; }
        public int ErrorCode { get; internal set; }
    }
}
