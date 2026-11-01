using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using KingdomIdle.Balance;
using KingdomIdle.CheckIn;
using KingdomIdle.Rewards;
using PlayFab;
using Scripts.Core;
using Scripts.Core.Manager;
using UnityEngine;
using Dto = Protocol.Message.Dto;

namespace KingdomIdle.Mailbox
{
    /// <summary>
    /// 서버 우편함(2026-10-31). 목록(OnGetMailList)과 수령(OnClaimMailReward)을 서버로 보내고 서버 우편을 메모리에 보관한다.
    /// 우편 발송은 서버만 한다(현재는 출석판 월 이월). 수령한 우편은 서버가 바로 지우므로 "수령 완료" 우편은 남지 않는다.
    /// 지급은 응답의 재화 잔액 전체와 장비 인벤토리 전체를 플레이어 상태에 쓴다.
    /// </summary>
    public sealed class MailboxService
    {
        private const int PageSize = 50, MaxMails = 100, MaxClaim = 50;
        // 우편 행이 한국시간으로 표시할 수 있는 날짜까지만 저장한다(옛 로컬 우편 검증용).
        private static readonly long MaxMailUtc = new DateTimeOffset(9999, 12, 31, 23, 59, 59, TimeSpan.FromHours(9)).ToUnixTimeSeconds();
        private static List<MailMessage> _messages = new();
        private static bool _refreshQueued = true;
        private static string _queuedReason = "start";
        private static long _account = -1;
        public static int Revision { get; private set; }
        public static bool Loaded { get; private set; }

        public bool IsBusy { get; private set; }
        public PlayFabError LastFailure { get; private set; }
        public event Action<MailClaimInfo> ClaimCompleted;
        private float _retryAt;

        /// <summary>다음 Refresh 틱에 우편 목록을 다시 받는다(로그인·이월 우편·우편함 열기·수령 뒤).</summary>
        public static void RequestRefresh(string reason) { _refreshQueued = true; _queuedReason = reason; }

        public MailboxSnapshot GetSnapshot()
        {
            if (!Loaded || !LocalProgression.IsReady || _account != LocalProgression.AccountGeneration) return null;
            long now = CheckInService.ServerNow;
            return new MailboxSnapshot { AccountGeneration = LocalProgression.AccountGeneration,
                Messages = _messages.Where(m => m.ExpiresUtc > now).Select(m => m.Copy()).ToArray() };
        }

        /// <summary>GameManager가 1초마다 부른다. 다시 받기 요청이 있을 때만 OnGetMailList를 보낸다. 실패하면 30초 뒤 다시 시도한다.</summary>
        public bool Refresh()
        {
            if (_account != LocalProgression.AccountGeneration)
            {
                _account = LocalProgression.AccountGeneration;
                _messages = new List<MailMessage>();
                Loaded = false;
                _refreshQueued = true;
                Revision++;
            }
            if (!_refreshQueued || IsBusy || Time.unscaledTime < _retryAt) return true;
            var network = NetworkManager.Instance;
            if (network == null || !network.IsGameDataReady) return false;
            _refreshQueued = false;
            IsBusy = true;
            Fetch(network, 0, new List<MailMessage>(), _queuedReason);
            return true;
        }

        private void Fetch(NetworkManager network, int start, List<MailMessage> collected, string reason)
        {
            network.Call<Dto.OnGetMailListRequestDTO, Dto.OnGetMailListResponseDTO>(new Dto.OnGetMailListRequestDTO { StartIndex = start, Count = PageSize },
                r =>
                {
                    foreach (var mail in r.Mails) collected.Add(Convert(mail));
                    int next = start + r.Mails.Count;
                    if (r.Mails.Count > 0 && next < r.TotalCount && next < MaxMails) { Fetch(network, next, collected, reason); return; }
                    _messages = collected;
                    Loaded = true;
                    IsBusy = false;
                    Revision++;
                    Debug.Log($"[Mail] list reason={reason} count={collected.Count} total={r.TotalCount} attendance={collected.Count(m => m.Sender == AttendanceSender)} | server count={r.TotalCount} | state count={collected.Count} | saved=True");
                },
                error =>
                {
                    IsBusy = false;
                    LastFailure = error;
                    _refreshQueued = true;
                    _retryAt = Time.unscaledTime + 30f;
                    Debug.LogWarning($"[Mail] list rejected reason={reason} kind={NetworkManager.GetFailureKind(error)} message={error?.ErrorMessage}");
                });
        }

        private const string AttendanceSender = "출석 보상";

        // 서버 우편은 본문 대신 현지화 키만 준다. 아는 키는 한국어 문구로, 모르는 키는 분류별 기본 문구로 보인다.
        private static MailMessage Convert(Dto.MailDTO mail)
        {
            bool attendance = mail.Category == Dto.MailCategory.Attendance;
            string title = mail.TitleKey switch
            {
                "mail.attendance.carryover.title" => "지난 달 출석 미수령 보상",
                _ => attendance ? "출석 보상" : "알림"
            };
            string body = mail.BodyKey switch
            {
                "mail.attendance.carryover.body" => "지난 달 출석부에서 받지 않은 보상입니다.",
                _ => ""
            };
            var rewards = new List<RewardGrant>();
            if (mail.Rewards != null)
                foreach (var reward in mail.Rewards)
                {
                    var grant = RewardGrants.FromServer(reward);
                    if (grant != null) rewards.Add(grant);
                    else Debug.LogWarning($"[Mail] 우편 {mail.MailId}의 알 수 없는 첨부는 표시하지 않는다.");
                }
            return new MailMessage
            {
                Id = mail.MailId.ToString(CultureInfo.InvariantCulture),
                Title = title,
                Sender = attendance ? AttendanceSender : "운영팀",
                Body = body,
                CreatedUtc = mail.SentUtc,
                ExpiresUtc = mail.ExpiresUtc,
                Rewards = rewards
            };
        }

        /// <summary>테스트 메뉴용. 서버 우편함은 서버만 발송하므로 로컬 우편을 만들지 않는다.</summary>
        public RewardResult TryAddMail(MailMessage mail)
        {
            Debug.LogWarning("[Mail] 서버 우편함으로 바뀌어 로컬 우편 추가는 지원하지 않는다.");
            return new(RewardStatus.NotReady);
        }

        public RewardResult TryClaim(MailboxSnapshot snapshot, string id) => Claim(snapshot, new[] { id });
        public RewardResult TryClaimAll(MailboxSnapshot snapshot) => Claim(snapshot,
            snapshot?.Messages.Where(m => !m.Claimed).Select(m => m.Id).ToArray());

        /// <summary>서버에 우편 수령을 보낸다(한 번에 50통까지). 결과는 ClaimCompleted로 온다(Pending).</summary>
        private RewardResult Claim(MailboxSnapshot snapshot, string[] ids)
        {
            if (IsBusy) return new(RewardStatus.Pending);
            var network = NetworkManager.Instance;
            if (!LocalProgression.IsReady || network == null || !network.IsGameDataReady) return new(RewardStatus.NotReady);
            if (snapshot == null || snapshot.AccountGeneration != LocalProgression.AccountGeneration) return new(RewardStatus.Stale);
            if (ids == null || ids.Length == 0) return new(RewardStatus.NothingAvailable);
            var mailIds = new List<long>();
            foreach (string id in ids)
            {
                if (!long.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out long mailId) || mailId <= 0 || mailIds.Contains(mailId))
                    return new(RewardStatus.Stale);
                if (mailIds.Count < MaxClaim) mailIds.Add(mailId);
            }
            IsBusy = true;
            Revision++;
            network.Call<Dto.OnClaimMailRewardRequestDTO, Dto.OnClaimMailRewardResponseDTO>(new Dto.OnClaimMailRewardRequestDTO { MailIds = mailIds },
                r => ApplyClaim(mailIds, r), e => RejectClaim(mailIds, e));
            return new(RewardStatus.Pending);
        }

        private void ApplyClaim(List<long> mailIds, Dto.OnClaimMailRewardResponseDTO r)
        {
            bool saved = LocalProgression.Execute("server-mail-claim", s =>
            {
                UserManager.ApplyCurrency(s, r.Currency);
                UserManager.ApplyEquipment(s, r.Equipment);
                s.RewardsRevision++;
                return true;
            });
            IsBusy = false;
            LastFailure = null;
            // 서버가 수령한 우편을 지웠다. 목록에서 빼고 다시 받는다.
            var claimed = new HashSet<string>(r.ClaimedMailIds.Select(id => id.ToString(CultureInfo.InvariantCulture)));
            _messages.RemoveAll(m => claimed.Contains(m.Id));
            Revision++;
            RequestRefresh("claim");
            bool equipment = r.Rewards != null && r.Rewards.Any(x => x != null && x.Kind == Dto.RewardKind.Equipment);
            if (saved)
            {
                EquipmentManager.Instance?.RestoreEquipment();
                if (equipment) QuestManager.Instance?.RequestRefresh("mail");
            }
            var state = LocalProgression.State;
            Debug.Log($"[Mail] claim ids={string.Join(".", mailIds)} rewards={r.Rewards?.Count ?? 0} remaining={r.RemainingCount}" +
                $" | server Gold={r.Currency?.Gold} AncientCoin={r.Currency?.AncientCoin} ArcaneKnowledge={r.Currency?.ArcaneKnowledge} ClassFragment={r.Currency?.ClassFragment} Ruby={r.Currency?.Ruby} EquipmentStone={r.Currency?.EquipmentStone} Inventory={UserManager.DescribeInventory(r.Equipment)}" +
                $" | state Gold={LocalProgression.Balance(eCurrency.Gold)} AncientCoin={LocalProgression.Balance(eCurrency.AncientCoin)} ArcaneKnowledge={LocalProgression.Balance(eCurrency.ArcaneKnowledge)} ClassFragment={LocalProgression.Balance(eCurrency.ClassFragment)} Ruby={LocalProgression.Balance(eCurrency.Ruby)} EquipmentStone={LocalProgression.Balance(eCurrency.EquipmentStone)} Inventory={UserManager.DescribeInventory(state)} | saved={saved}");
            if (!saved)
            {
                Direction.GameDirectInteraction.ReportSaveFailure();
                LastFailure = NetworkManager.CreateError("ApplyFailed", "우편 수령 결과를 저장하지 못했습니다: " + LocalProgression.LastError);
            }
            ClaimCompleted?.Invoke(new MailClaimInfo
            {
                Ok = saved,
                MailIds = mailIds.ToArray(),
                Rewards = RewardGrants.FromServer(r.Rewards),
                Message = saved ? null : "우편 수령 결과를 저장하지 못했습니다. 다시 로그인하면 서버 값으로 맞춰집니다."
            });
        }

        private void RejectClaim(List<long> mailIds, PlayFabError error)
        {
            IsBusy = false;
            LastFailure = error;
            string kind = NetworkManager.GetFailureKind(error);
            int code = NetworkManager.TryGetBusinessError(error, out var business) ? (int)business.ErrorCode : 0;
            Revision++;
            RequestRefresh("claim-rejected");
            Debug.Log($"[Mail] claim rejected ids={string.Join(".", mailIds)} code={code} kind={kind} message={error?.ErrorMessage}");
            string message = NetworkManager.DescribeFailure(error, "우편 수령");
            void Complete(string text) => ClaimCompleted?.Invoke(new MailClaimInfo { Ok = false, MailIds = mailIds.ToArray(), Rewards = Array.Empty<RewardGrant>(), Message = text, ErrorCode = code });
            if (kind == "Timeout" || kind == "Transport")
            {
                // 응답 유실: 서버가 이미 지급했을 수 있다. 재전송하지 않고 플레이어 데이터를 다시 받는다.
                var user = UserManager.Instance;
                if (user == null) { Complete(message); return; }
                user.RefreshFromServer(ok => Complete(ok ? "결과를 서버에서 다시 불러왔습니다." : message));
                return;
            }
            Complete(message);
        }

        /// <summary>서버는 수령한 우편을 바로 지우므로 지울 수령 완료 우편이 없다.</summary>
        public RewardResult DeleteClaimed(MailboxSnapshot snapshot) => new(RewardStatus.NothingAvailable);

        // 옛 로컬 우편(ProgressionState.Mail) 검증. LocalProgression.Validate가 쓴다.
        internal static bool IsValid(MailMessage mail) => mail != null && !string.IsNullOrWhiteSpace(mail.Id) &&
            !string.IsNullOrWhiteSpace(mail.Title) && mail.CreatedUtc > 0 && mail.ExpiresUtc > mail.CreatedUtc &&
            mail.ExpiresUtc <= MaxMailUtc && mail.ClaimedAtUtc >= 0 && mail.ClaimedAtUtc <= MaxMailUtc &&
            mail.Rewards != null && mail.Rewards.Count > 0 && mail.Rewards.All(RewardGrants.IsValid);
    }

    /// <summary>우편 수령 결과. Rewards는 서버 지급 결과를 합친 표시용 목록이다.</summary>
    public sealed class MailClaimInfo
    {
        public bool Ok { get; internal set; }
        public long[] MailIds { get; internal set; }
        public IReadOnlyList<RewardGrant> Rewards { get; internal set; }
        public string Message { get; internal set; }
        public int ErrorCode { get; internal set; }
    }
}
