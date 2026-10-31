using System;
using System.Collections.Generic;
using System.Linq;
using KingdomIdle.Balance;
using PlayFab;
using Protocol.Message.Dto;
using Scripts.Core;
using Scripts.Core.Manager;
using UnityEngine;

namespace KingdomIdle.Mailbox
{
    public sealed class MailboxClaimResult
    {
        public IReadOnlyList<GachaRewardDTO> Rewards { get; internal set; }
        public string Message { get; internal set; }
    }

    /// <summary>Only server responses grant rewards. The list lives for one panel session.</summary>
    public sealed class MailboxService
    {
        internal delegate void ListCall(OnGetMailListRequestDTO request, Action<OnGetMailListResponseDTO> ok, Action<PlayFabError> fail);
        internal delegate void ClaimCall(OnClaimMailRewardRequestDTO request, Action<OnClaimMailRewardResponseDTO> ok, Action<PlayFabError> fail);
        private readonly ListCall _list;
        private readonly ClaimCall _claim;
        private readonly Func<OnClaimMailRewardResponseDTO, bool> _apply;
        private readonly Action<Action<bool>> _recover;
        private readonly Func<(long, long)> _identity;
        private readonly Func<bool> _ready;
        private readonly Func<double> _clock;
        private (long, long) _account;
        private int _loadVersion, _operationVersion, _viewVersion;
        private bool _open, _claiming, _recovering, _needsRecovery;
        private long _serverUtc;
        private double _observedAt;
        private IReadOnlyList<MailDTO> _messages = Array.Empty<MailDTO>();

        public event Action Changed;
        public event Action<MailboxClaimResult> ClaimCompleted;
        public IReadOnlyList<MailDTO> Messages => _messages;
        public string MailboxId { get; private set; }
        public string Error { get; private set; }
        public bool IsLoading { get; private set; }
        public bool HasSnapshot { get; private set; }
        public bool IsBusy => IsLoading || _claiming || _recovering;
        public bool CanClaim => _open && HasSnapshot && !IsBusy && !_needsRecovery && _ready();
        public long ServerNow => _serverUtc + (long)Math.Max(0, _clock() - _observedAt);

        public MailboxService() : this(
            (r, ok, fail) => NetworkManager.Instance.Call(r, ok, fail),
            (r, ok, fail) => NetworkManager.Instance.Call(r, ok, fail),
            Apply, Recover,
            () => (NetworkManager.Instance != null ? NetworkManager.Instance.AccountGeneration : -1, LocalProgression.AccountGeneration),
            () => NetworkManager.Instance != null && NetworkManager.Instance.IsGameDataReady && LocalProgression.IsReady &&
                  LocalProgression.AccountKey == NetworkManager.Instance.GetSessionID(),
            () => Time.realtimeSinceStartupAsDouble) { }

        // Injection keeps response-loss tests away from the live account.
        internal MailboxService(ListCall list, ClaimCall claim, Func<OnClaimMailRewardResponseDTO, bool> apply,
            Action<Action<bool>> recover, Func<(long, long)> identity, Func<bool> ready, Func<double> clock)
        {
            _list = list; _claim = claim; _apply = apply; _recover = recover;
            _identity = identity; _ready = ready; _clock = clock; _account = identity();
        }

        public void Open()
        {
            CheckAccount(); _open = true; _viewVersion++;
            ClearList(); Refresh();
        }

        public void Close()
        {
            _open = false; _viewVersion++;
            ClearList(); Changed?.Invoke();
        }

        private void ClearList()
        {
            _loadVersion++;
            _messages = Array.Empty<MailDTO>();
            MailboxId = null; HasSnapshot = false; IsLoading = false; Error = null;
        }

        public void CheckAccount()
        {
            if (_account == _identity()) return;
            _account = _identity(); _operationVersion++; _viewVersion++;
            _claiming = _recovering = _needsRecovery = false;
            ClearList(); Changed?.Invoke();
            if (_open) Refresh();
        }

        public void Refresh()
        {
            if (!_open || _claiming || _recovering || IsLoading) { Changed?.Invoke(); return; }
            if (!_ready()) { Error = "서버 로그인이 필요합니다."; Changed?.Invoke(); return; }
            if (_needsRecovery) { RecoverState(); return; }
            Load(0);
        }

        private void Load(int retry)
        {
            int version = ++_loadVersion;
            var account = _account;
            IsLoading = true; Error = null; Changed?.Invoke();
            var collected = new List<MailDTO>();
            var ids = new HashSet<long>();
            int total = -1;
            string mailbox = null;
            bool Current() => _open && version == _loadVersion && account == _identity();
            void Fail(string message)
            {
                if (!Current()) return;
                IsLoading = false; HasSnapshot = false; Error = message; Changed?.Invoke();
            }
            void Inconsistent()
            {
                if (retry == 0) Load(1);
                else Fail("우편 목록이 변경되었습니다. 다시 불러와 주세요.");
            }
            void Page(int start)
            {
                _list(new OnGetMailListRequestDTO { StartIndex = start, Count = 50 }, response =>
                {
                    if (!Current()) return;
                    try { MailboxProtocol.ValidateList(response); }
                    catch (Exception e) { Debug.LogWarning("[Mailbox] " + e.Message); Fail("우편 응답을 확인하지 못했습니다. 다시 불러와 주세요."); return; }
                    if (response.StartIndex != start) { Fail("우편 조회 범위가 일치하지 않습니다."); return; }
                    if (total < 0) { total = response.TotalCount; mailbox = response.MailboxId; }
                    if (total != response.TotalCount || mailbox != response.MailboxId ||
                        response.Mails.Count != Math.Min(50, Math.Max(0, total - start)) ||
                        response.Mails.Any(m => !ids.Add(m.MailId))) { Inconsistent(); return; }
                    collected.AddRange(response.Mails);
                    _serverUtc = response.ServerTimeUtc; _observedAt = _clock();
                    if (collected.Count < total) { Page(start + 50); return; }
                    _messages = collected.ToArray(); MailboxId = mailbox;
                    IsLoading = false; HasSnapshot = true; Changed?.Invoke();
                }, error => Fail(NetworkManager.DescribeFailure(error, "우편 조회")));
            }
            Page(0);
        }

        public void Claim(long mailId) => StartClaim(new[] { mailId });
        public void ClaimAll() => StartClaim(_messages.Where(m => m.ExpiresUtc > ServerNow).Select(m => m.MailId).ToArray());

        private void StartClaim(long[] ids)
        {
            CheckAccount();
            if (!CanClaim || ids.Length == 0 || ids.Distinct().Count() != ids.Length ||
                ids.Any(id => !_messages.Any(m => m.MailId == id && m.ExpiresUtc > ServerNow))) return;
            var account = _account;
            int operation = ++_operationVersion, view = _viewVersion;
            string mailbox = MailboxId;
            _claiming = true; Error = null; Changed?.Invoke();
            var granted = new List<GachaRewardDTO>();
            bool Current() => operation == _operationVersion && account == _identity();
            void Finish(string message, bool recover)
            {
                if (!Current()) return;
                _claiming = false; _needsRecovery |= recover; Error = message;
                // Reopening must not display a popup owned by an earlier panel session.
                if (_open && view == _viewVersion)
                    ClaimCompleted?.Invoke(new MailboxClaimResult { Rewards = granted.ToArray(), Message = message });
                if (_needsRecovery) RecoverState();
                else if (_open) Load(0);
                else Changed?.Invoke();
            }
            void Batch(int offset)
            {
                var batch = ids.Skip(offset).Take(50).ToList();
                _claim(new OnClaimMailRewardRequestDTO { MailIds = batch }, response =>
                {
                    if (!Current()) return;
                    try
                    {
                        MailboxProtocol.ValidateClaim(response);
                        if (response.MailboxId != mailbox || !batch.ToHashSet().SetEquals(response.ClaimedMailIds))
                            throw new InvalidOperationException("Claim identifiers do not match.");
                        if (!_apply(response)) throw new InvalidOperationException("Could not save the server reward snapshot.");
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning("[Mailbox] " + e.Message);
                        Finish("수령 결과를 확인하기 위해 서버 상태를 다시 불러옵니다.", true); return;
                    }
                    granted.AddRange(response.Rewards);
                    var removed = response.ClaimedMailIds.ToHashSet();
                    _messages = _messages.Where(m => !removed.Contains(m.MailId)).ToArray(); Changed?.Invoke();
                    if (offset + batch.Count < ids.Length) Batch(offset + batch.Count);
                    else Finish(null, false);
                }, error =>
                {
                    if (!Current()) return;
                    string kind = NetworkManager.GetFailureKind(error);
                    bool uncertain = kind != "Business" && kind != "NotAuthenticated" && kind != "IntegrationPending";
                    Finish(NetworkManager.DescribeFailure(error, "우편 수령"), uncertain);
                });
            }
            Batch(0);
        }

        private void RecoverState()
        {
            if (_recovering) return;
            _needsRecovery = _recovering = true;
            var account = _account;
            int operation = _operationVersion;
            Changed?.Invoke();
            _recover(ok =>
            {
                if (operation != _operationVersion || account != _identity()) return;
                _recovering = false; _needsRecovery = !ok;
                if (!ok) { Error = "서버 상태를 확인하지 못했습니다. 다시 불러와 주세요."; HasSnapshot = false; Changed?.Invoke(); }
                else if (_open) Load(0);
                else Changed?.Invoke();
            });
        }

        private static bool Apply(OnClaimMailRewardResponseDTO response)
        {
            bool saved = LocalProgression.Execute("server-mail-claim", s =>
            {
                UserManager.ApplyCurrency(s, response.Currency);
                UserManager.ApplyEquipment(s, response.Equipment);
                return true;
            });
            if (saved) EquipmentManager.Instance?.RestoreEquipment();
            return saved;
        }

        private static void Recover(Action<bool> done)
        {
            if (UserManager.Instance == null) done(false);
            else UserManager.Instance.RefreshFromServer(done);
        }
    }
}
