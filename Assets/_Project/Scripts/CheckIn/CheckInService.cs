using System;
using System.Collections.Generic;
using System.Linq;
using KingdomIdle.Balance;
using KingdomIdle.Mailbox;
using KingdomIdle.Rewards;
using PlayFab;
using Scripts.Core;
using Scripts.Core.Manager;
using UnityEngine;
using Dto = Protocol.Message.Dto;

namespace KingdomIdle.CheckIn
{
    /// <summary>
    /// 서버 월별 출석판(2026-10-31). 출석(OnCheckAttendance)은 KST 하루에 한 칸을 열고, 수령(OnClaimAttendanceReward)이 재화·장비를 지급한다.
    /// 출석판 상태는 플레이어 상태 파일이 아니라 메모리에 둔다(QuestManager와 같은 방식). 로그인·OnGetUserState·출석·수령 응답이 통째로 교체한다.
    /// 새 달의 이월(지난 판의 미수령 칸 → 우편)과 초기화는 서버가 새 달 첫 출석 때 한다. 클라이언트는 계산하지 않는다.
    /// </summary>
    public sealed class CheckInService
    {
        private static Dto.AttendanceStateDTO _server;
        // 서버 시각 − 로컬 시각(로그인 응답의 ServerTimeUtc 기준). 날짜·달 경계는 이 추정 서버 시각으로 본다.
        private static long _serverOffset;
        // 이 서버 시각부터 다음 출석 요청을 보낸다(로그인 직후 0, 성공하면 NextResetUtc, 28칸이면 BoardResetUtc).
        private static long _nextCheckUtc;
        private static long _boardResetUtc;
        public static int Revision { get; private set; }
        public static long ServerNow => LocalProgression.UtcNow + _serverOffset;

        public bool IsBusy { get; private set; }
        public PlayFabError LastFailure { get; private set; }
        public event Action<CheckInClaimInfo> ClaimCompleted;

        /// <summary>보상표는 서버 표 사본(ServerAttendanceTable)을 쓴다. 인자는 기존 호출부(GameManager) 호환용이다.</summary>
        public CheckInService(CheckInRewardTableSO table) { }
        public bool IsRewardTableValid => true;

        /// <summary>로그인·OnGetUserState 응답의 출석판을 받는다(UserManager가 호출). 로그인 직후에는 출석을 한 번 보낸다.</summary>
        public static void Receive(Dto.AttendanceStateDTO attendance, long serverTimeUtc, string source)
        {
            if (attendance == null) return;
            if (serverTimeUtc > 0) _serverOffset = serverTimeUtc - LocalProgression.UtcNow;
            _server = attendance;
            if (source == "login") _nextCheckUtc = 0;
            Revision++;
            Debug.Log($"[Attendance] receive source={source} board={Describe(attendance)} serverTime={serverTimeUtc}");
        }

        public static string Describe() => Describe(_server);
        public static string Describe(Dto.AttendanceStateDTO a) => a == null ? "-" :
            $"{(string.IsNullOrEmpty(a.Month) ? "none" : a.Month)}/{a.OpenedDays}/{(a.ClaimedDays == null || a.ClaimedDays.Count == 0 ? "-" : string.Join(".", a.ClaimedDays))}";

        public CheckInSnapshot GetSnapshot()
        {
            if (_server == null || !LocalProgression.IsReady) return null;
            var period = QuestPeriod.At(ServerNow);
            // 판이 이번 달이 아니면 서버가 아직 이월하지 않은 것이다. 출석 응답이 올 때까지 갱신 중으로 본다.
            if (_server.Month != period.Month) return null;
            int opened = Math.Max(0, Math.Min(ServerAttendanceTable.Days, _server.OpenedDays));
            var claimed = new HashSet<int>();
            if (_server.ClaimedDays != null)
                foreach (int day in _server.ClaimedDays) if (day >= 1 && day <= opened) claimed.Add(day - 1);
            var rewards = new RewardGrant[ServerAttendanceTable.Days];
            var dayRewards = new IReadOnlyList<RewardGrant>[ServerAttendanceTable.Days];
            for (int i = 0; i < rewards.Length; i++)
            {
                rewards[i] = ServerAttendanceTable.Representative(i + 1);
                dayRewards[i] = ServerAttendanceTable.RewardsOf(i + 1);
            }
            return new CheckInSnapshot
            {
                AccountGeneration = LocalProgression.AccountGeneration,
                Month = _server.Month,
                Count = opened,
                NextRefreshUtc = opened >= ServerAttendanceTable.Days ? (_boardResetUtc > 0 ? _boardResetUtc : period.MonthEndUtc) : period.DayEndUtc,
                Rewards = rewards,
                DayRewards = dayRewards,
                Available = Enumerable.Range(0, opened).Where(i => !claimed.Contains(i)).ToArray(),
                Claimed = claimed.ToArray()
            };
        }

        /// <summary>GameManager가 1초마다 부른다. 로그인 직후와 다음 KST 0시가 지난 뒤에만 OnCheckAttendance를 보낸다.</summary>
        public bool RecordVisit()
        {
            var network = NetworkManager.Instance;
            if (IsBusy || _server == null || network == null || !network.IsGameDataReady) return false;
            if (ServerNow < _nextCheckUtc) return true;
            IsBusy = true;
            network.Call<Dto.OnCheckAttendanceRequestDTO, Dto.OnCheckAttendanceResponseDTO>(new Dto.OnCheckAttendanceRequestDTO(),
                ApplyCheckIn, RejectCheckIn);
            return true;
        }

        private void ApplyCheckIn(Dto.OnCheckAttendanceResponseDTO r)
        {
            IsBusy = false;
            LastFailure = null;
            var period = QuestPeriod.At(ServerNow);
            _server = r.Attendance;
            _boardResetUtc = r.BoardResetUtc > 0 ? r.BoardResetUtc : period.MonthEndUtc;
            _nextCheckUtc = r.Attendance.OpenedDays >= ServerAttendanceTable.Days ? _boardResetUtc : (r.NextResetUtc > 0 ? r.NextResetUtc : period.DayEndUtc);
            Revision++;
            int carryover = r.CarryoverMailIds?.Count ?? 0;
            if (carryover > 0) Scripts.Core.GameManager.Instance?.Mailbox?.RequestRefresh("carryover");
            Debug.Log($"[Attendance] check day={r.CycleDay} carryover={carryover} next={_nextCheckUtc} | server board={Describe(r.Attendance)} | state board={Describe()} | saved=True");
        }

        private void RejectCheckIn(PlayFabError error)
        {
            IsBusy = false;
            LastFailure = error;
            string kind = NetworkManager.GetFailureKind(error);
            int code = NetworkManager.TryGetBusinessError(error, out var business) ? (int)business.ErrorCode : 0;
            long now = ServerNow;
            var period = QuestPeriod.At(now);
            // 이미 출석(1038)은 다음 날, 판이 가득 참(1047)은 다음 달까지 기다린다. 출석은 소비 요청이 아니라 응답 유실이면 잠시 뒤 다시 보낸다.
            _nextCheckUtc = code == 1038 ? period.DayEndUtc : code == 1047 ? period.MonthEndUtc :
                kind == "Timeout" || kind == "Transport" ? now + 60 : now + 600;
            if (code == 1041) UIManager_ShowToast("우편함이 가득 차 지난 달 출석 보상을 보낼 수 없습니다. 우편을 받은 뒤 다시 시도합니다.");
            Debug.Log($"[Attendance] check rejected code={code} kind={kind} next={_nextCheckUtc} message={error?.ErrorMessage}");
        }

        /// <summary>
        /// 화면에 보인 달과 받을 수 있는 칸 전체를 서버에 보낸다. 결과는 ClaimCompleted로 온다(Pending).
        /// 지급량은 서버 보상표로 정하고, 응답의 재화 잔액 전체와 바뀐 장비 묶음만 플레이어 상태에 쓴다.
        /// </summary>
        public RewardResult TryClaimAvailable(CheckInSnapshot snapshot)
        {
            if (IsBusy) return new(RewardStatus.Pending);
            var network = NetworkManager.Instance;
            if (!LocalProgression.IsReady || network == null || !network.IsGameDataReady) return new(RewardStatus.NotReady);
            if (snapshot == null || snapshot.AccountGeneration != LocalProgression.AccountGeneration) return new(RewardStatus.Stale);
            if (snapshot.Available.Count == 0) return new(RewardStatus.NothingAvailable);
            var days = snapshot.Available.Select(i => i + 1).ToList();
            IsBusy = true;
            Revision++;
            network.Call<Dto.OnClaimAttendanceRewardRequestDTO, Dto.OnClaimAttendanceRewardResponseDTO>(
                new Dto.OnClaimAttendanceRewardRequestDTO { Month = snapshot.Month, Days = days },
                r => ApplyClaim(days, r), e => RejectClaim(days, e));
            return new(RewardStatus.Pending);
        }

        private void ApplyClaim(List<int> days, Dto.OnClaimAttendanceRewardResponseDTO r)
        {
            var changes = r.EquipmentChanges;
            int changed = (changes?.Items?.Count ?? 0) + (changes?.PendingItems?.Count ?? 0) + (changes?.ReserveStacks?.Count ?? 0);
            bool saved = LocalProgression.Execute("server-attendance-claim", s =>
            {
                UserManager.ApplyCurrency(s, r.Currency);
                UserManager.ApplyEquipmentChanges(s, changes);
                s.RewardsRevision++;
                return true;
            });
            IsBusy = false;
            LastFailure = null;
            // 출석판은 서버가 이미 저장했으므로 플레이어 상태 저장과 무관하게 응답 값으로 바꾼다.
            _server = r.Attendance;
            Revision++;
            if (saved)
            {
                EquipmentManager.Instance?.RestoreEquipment();
                if (changed > 0) QuestManager.Instance?.RequestRefresh("attendance");
            }
            var state = LocalProgression.State;
            Debug.Log($"[Attendance] claim days={string.Join(".", days)} rewards={r.Rewards?.Count ?? 0} changed={changed}" +
                $" | server board={Describe(r.Attendance)} Gold={r.Currency?.Gold} AncientCoin={r.Currency?.AncientCoin} ArcaneKnowledge={r.Currency?.ArcaneKnowledge} ClassFragment={r.Currency?.ClassFragment} Ruby={r.Currency?.Ruby} EquipmentStone={r.Currency?.EquipmentStone}" +
                $" | state board={Describe()} Gold={LocalProgression.Balance(eCurrency.Gold)} AncientCoin={LocalProgression.Balance(eCurrency.AncientCoin)} ArcaneKnowledge={LocalProgression.Balance(eCurrency.ArcaneKnowledge)} ClassFragment={LocalProgression.Balance(eCurrency.ClassFragment)} Ruby={LocalProgression.Balance(eCurrency.Ruby)} EquipmentStone={LocalProgression.Balance(eCurrency.EquipmentStone)} Inventory={UserManager.DescribeInventory(state)} | saved={saved}");
            if (!saved)
            {
                Direction.GameDirectInteraction.ReportSaveFailure();
                LastFailure = NetworkManager.CreateError("ApplyFailed", "출석 보상 결과를 저장하지 못했습니다: " + LocalProgression.LastError);
            }
            ClaimCompleted?.Invoke(new CheckInClaimInfo
            {
                Ok = saved,
                Days = days.ToArray(),
                Rewards = RewardGrants.FromServer(r.Rewards),
                Message = saved ? null : "출석 보상 결과를 저장하지 못했습니다. 다시 로그인하면 서버 값으로 맞춰집니다."
            });
        }

        private void RejectClaim(List<int> days, PlayFabError error)
        {
            IsBusy = false;
            LastFailure = error;
            string kind = NetworkManager.GetFailureKind(error);
            int code = NetworkManager.TryGetBusinessError(error, out var business) ? (int)business.ErrorCode : 0;
            Revision++;
            Debug.Log($"[Attendance] claim rejected days={string.Join(".", days)} code={code} kind={kind} message={error?.ErrorMessage}");
            string message = NetworkManager.DescribeFailure(error, "출석 보상");
            void Complete(string text) => ClaimCompleted?.Invoke(new CheckInClaimInfo { Ok = false, Days = days.ToArray(), Rewards = Array.Empty<RewardGrant>(), Message = text, ErrorCode = code });
            // 달이 바뀌었으면 출석이 이월을 맡는다. 다음 틱에 출석을 보낸다.
            if (code == 1045) _nextCheckUtc = 0;
            if (kind == "Timeout" || kind == "Transport" || code == 1029 || code == 1046)
            {
                // 응답 유실·화면과 서버 판 불일치: 소비 요청은 재전송하지 않고 서버 상태를 다시 받는다(출석판 포함).
                var user = UserManager.Instance;
                if (user == null) { Complete(message); return; }
                user.RefreshFromServer(ok => Complete(ok && (kind == "Timeout" || kind == "Transport") ? "결과를 서버에서 다시 불러왔습니다." : message));
                return;
            }
            Complete(message);
        }

        private static void UIManager_ShowToast(string message) => KingdomIdle.UGUI.UIManager.Instance?.ShowToast(message);
    }

    /// <summary>출석 보상 수령 결과. Rewards는 서버 지급 결과를 합친 표시용 목록이다.</summary>
    public sealed class CheckInClaimInfo
    {
        public bool Ok { get; internal set; }
        public int[] Days { get; internal set; }
        public IReadOnlyList<RewardGrant> Rewards { get; internal set; }
        public string Message { get; internal set; }
        public int ErrorCode { get; internal set; }
    }
}
