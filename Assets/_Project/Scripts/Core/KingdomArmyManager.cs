using System;
using System.Collections.Generic;
using UnityEngine;
using KingdomIdle.Balance;
using PlayFab;
using Protocol.Message.Dto;
using Scripts.Core;
using Scripts.Core.Manager;

namespace KingdomIdle.KingdomArmy
{
    /// <summary>
    /// 왕국군 전직 매니저. 전직은 서버가 확정한다: 미해금 직업은 OnGetJob(파편 40/120 차감·해금·선택), 보유 직업은 OnChangeJob(무료).
    /// 응답의 JobCode·JobTrees(·CurrentClassFragment)를 플레이어 상태에 그대로 쓰고 그 캐릭터의 런타임 직업을 바로 적용한다.
    /// 로컬 검사(파편·선행 직업·보스 웨이브)는 안내용이고 최종 판정은 서버다. 서버는 메인 사냥 중 변경을 허용하고 던전 진행 중만 1025로 거절한다.
    /// </summary>
    public class KingdomArmyManager : MonoBehaviour
    {
        public static KingdomArmyManager Instance { get; private set; }

        [SerializeField] private JobDatabase jobDatabase;
        [SerializeField] private EquipmentDatabase equipmentDatabase;
        [SerializeField] private int defaultFragmentCost = 40;

        /// <summary>2차 전직 → 1차 전직 선행 조건 매핑</summary>
        private static readonly Dictionary<string, string> _eliteToBase = new()
        {
            { "Elite_Knight", "Knight" },
            { "Elite_Archer", "Archer" },
            { "Elite_Mage",   "Mage"   },
        };

        public JobDatabase JobDB => jobDatabase;
        public EquipmentDatabase EquipDB => equipmentDatabase;

        /// <summary>파편 수량 / 전직 상태 변경 시 발생. UI 갱신에 사용.</summary>
        public event Action OnStateChanged;

        public enum JobChangeResult { Pending, Busy, NotAvailable, SameJob, Guard, NetworkNotReady, UnequipFailed }
        /// <summary>완료 이벤트 인자. Function은 보낸 서버 함수("OnGetJob"/"OnChangeJob"), LogLine은 하네스가 읽는 [Job] 로그다.</summary>
        public sealed class JobChangeInfo
        {
            public int Slot;
            public string JobName;
            public string Function;
            public bool Ok;
            public bool IsNewUnlock;
            public long SpentClassFragment;
            public int ErrorCode;
            public string Failure;
            public string LogLine;
        }
        public event Action<JobChangeInfo> OnJobChangeCompleted;
        /// <summary>마지막 서버 거절 또는 적용 실패. Business 오류면 CustomData에 서버 ErrorResponseDTO가 들어 있다.</summary>
        public PlayFabError LastFailure { get; private set; }
        private bool _isChangingJob;
        public bool IsChangingJob => _isChangingJob;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── 파편 (통합) ──

        /// <summary>
        /// 호환성 유지용. 예전 per-job 구조가 남아있는 호출부를 위해 남겨둔다.
        /// 통합 파편 체제에서는 모든 jobName 에 대해 동일한 전체 파편 수량을 반환.
        /// </summary>
        public static string GetBaseFragmentName(string jobName) => jobName;

        /// <summary>2차 전직의 선행 조건 직업 이름. 선행 조건이 없으면 null.</summary>
        public static string GetPrerequisiteJob(string jobName) =>
            _eliteToBase.TryGetValue(jobName, out string baseName) ? baseName : null;

        /// <summary>해당 플레이어가 특정 전직을 완료했는지(플레이어 상태의 UnlockedJobs = 서버 JobTrees).</summary>
        public bool HasCompletedPromotion(Player player, string jobName)
        {
            if (player == null) return false;
            var changeJob = player.GetComponent<ChangeJob>();
            if (changeJob == null || jobDatabase == null) return false;

            int idx = jobDatabase.jobs.FindIndex(j => j.jobName == jobName);
            if (idx < 0) return false;
            return changeJob.IsJobUnlocked(idx);
        }

        /// <summary>현재 보유한 통합 전직 파편 수량(서버가 준 지갑 값).</summary>
        public int GetFragments()
        {
            EconomyBridge.TryGetAmount(eCurrency.ClassFragment, out long amount);
            if (amount < 0) amount = 0;
            if (amount > int.MaxValue) amount = int.MaxValue;
            return (int)amount;
        }

        /// <summary>
        /// [호환성] jobName 매개변수를 무시하고 통합 파편 수량을 반환한다.
        /// 과거 per-job 호출부를 깨지 않기 위해 남겨둔 오버로드.
        /// </summary>
        public int GetFragments(string jobName) => GetFragments();

        /// <summary>[QA 전용] 통합 전직 파편을 로컬 지갑에 더한다. 서버 잔액이 아니므로 다음 서버 응답에서 서버 값으로 덮인다.</summary>
        public void AddFragments(int amount)
        {
            if (amount == 0) return;
            EconomyBridge.Add(eCurrency.ClassFragment, amount);
            OnStateChanged?.Invoke();
        }

        /// <summary>
        /// [호환성] jobName 을 무시하고 통합 파편을 지급한다.
        /// 기존 per-job 호출부 호환용.
        /// </summary>
        public void AddFragments(string jobName, int amount) => AddFragments(amount);

        /// <summary>전직 1 회 비용(파편 수). 모든 전직에 동일 적용.</summary>
        public int GetFragmentCost() => 40;

        /// <summary>안내용 비용(서버 GetJobService와 같은 규칙: 상위 120, 기본 40, 창병 0). 실제 차감은 서버 응답의 SpentClassFragment다.</summary>
        public int GetFragmentCost(string jobName) => jobName != null && jobName.StartsWith("Elite_") ? 120 : jobName == "Spearman" ? 0 : 40;

        /// <summary>안내용 전직 가능 여부. 2차 전직은 1차 전직 완료 + 파편 충분. 최종 판정은 서버다(19·21·1018·1025).</summary>
        /// <remarks>해당 플레이어가 이미 그 직업을 해금한 적이 있으면 파편 없이도 자유롭게 재전직 가능.</remarks>
        public bool CanChangeJob(string jobName, Player player = null)
        {
            if (!JobData.IsAvailable(jobName)) return false;
            // 이미 해금된 직업이면 파편/선행 조건 무관 — 무료 재전직
            if (!ChangeJob.CanQueueChange) return false;
            if (player != null && HasCompletedPromotion(player, jobName))
                return true;

            if (!ChangeJob.CanQueueChange || GetFragments() < GetFragmentCost(jobName))
                return false;

            string prereq = GetPrerequisiteJob(jobName);
            if (prereq != null && player != null)
                return HasCompletedPromotion(player, prereq);

            return true;
        }

        /// <summary>해당 플레이어가 이 직업을 이미 해금했는지 (UI에서 "재전직 무료" 표시 등에 사용)</summary>
        public bool IsAlreadyUnlocked(Player player, string jobName) =>
            HasCompletedPromotion(player, jobName);

        // ── 전직 실행 (서버) ──

        /// <summary>기존 UI 시그니처. onSuccess는 "요청 접수"이고 결과는 OnJobChangeCompleted로 온다.</summary>
        public void TryChangeJob(Player player, string jobName,
                                 Action onSuccess, Action<string> onError)
        {
            var result = TryChangeJobEx(player, jobName);
            if (result == JobChangeResult.Pending) onSuccess?.Invoke();
            else onError?.Invoke(Describe(result));
        }

        /// <summary>
        /// 전직 요청을 서버로 보낸다. 보유 직업이면 OnChangeJob(무료), 아니면 OnGetJob(파편 차감·해금). 새 직업이 쓸 수 없는 무기를 끼고 있으면
        /// 먼저 OnUnequipEquipment를 요청한다(세션 요청은 한 번에 하나씩 보내므로 순서가 보장된다). 로컬 검사는 안내용이다.
        /// </summary>
        public JobChangeResult TryChangeJobEx(Player player, string jobName)
        {
            if (player == null || !JobData.IsAvailable(jobName)) return JobChangeResult.NotAvailable;
            if (_isChangingJob) return JobChangeResult.Busy;
            int slot = player.PlayerIndex;
            string current = LocalProgression.State.Jobs.TryGetValue(slot, out var saved) ? saved : player.playerStatus?.JobName;
            if (current == jobName) return JobChangeResult.SameJob;
            if (!CanChangeJob(jobName, player)) return JobChangeResult.Guard;
            var network = NetworkManager.Instance;
            if (network == null || !network.IsGameDataReady) return JobChangeResult.NetworkNotReady;
            var weapon = player.PlayerEquipmentManager?.GetSlotEquipment(eEquipmentSlot.Weapon);
            if (weapon != null && weapon.baseData != null && !weapon.baseData.IsAllowedForJob(jobName))
            {
                // 서버는 장비를 해제하지 않는다(장착 상태로 전직하면 1018). 해제 요청을 먼저 줄 세운다.
                var unequip = EquipmentManager.Instance != null ? EquipmentManager.Instance.UnequipEx(slot) : EquipmentManager.EnhancementResult.NetworkNotReady;
                if (unequip != EquipmentManager.EnhancementResult.Pending) return JobChangeResult.UnequipFailed;
            }
            bool unlocked = HasCompletedPromotion(player, jobName);
            var info = new JobChangeInfo { Slot = slot, JobName = jobName, Function = unlocked ? "OnChangeJob" : "OnGetJob" };
            ulong code = UserManager.JobCode(jobName);
            _isChangingJob = true;
            LastFailure = null;
            if (unlocked)
            {
                network.Call<OnChangeJobRequestDTO, OnChangeJobResponseDTO>(new OnChangeJobRequestDTO { Index = slot, JobCode = code },
                    response => ApplyServerResult(player, info, response, null), error => RejectServerResult(info, error));
            }
            else
            {
                network.Call<OnGetJobRequestDTO, OnGetJobResponseDTO>(new OnGetJobRequestDTO { Index = slot, JobCode = code },
                    response => ApplyServerResult(player, info, response, response), error => RejectServerResult(info, error));
            }
            return JobChangeResult.Pending;
        }

        /// <summary>서버가 확정한 직업·해금 트리(·파편 잔액)를 한 트랜잭션에 쓰고 그 캐릭터의 런타임 직업을 바로 적용한다.</summary>
        private void ApplyServerResult(Player player, JobChangeInfo info, JobActionResponseDTO response, OnGetJobResponseDTO unlock)
        {
            string op = unlock != null ? "getjob" : "changejob";
            long fragmentBefore = LocalProgression.Balance(eCurrency.ClassFragment);
            bool saved;
            try
            {
                saved = LocalProgression.Execute("server-job", s =>
                {
                    UserManager.ApplyJob(s, response.Index, response.JobCode, response.JobTrees);
                    if (unlock != null) s.Wallet[eCurrency.ClassFragment] = Math.Max(0, unlock.CurrentClassFragment);
                    return true;
                });
            }
            finally { _isChangingJob = false; }
            var state = LocalProgression.State;
            string stateJob = state.Jobs.TryGetValue(response.Index, out var name) ? UserManager.JobCode(name).ToString() : "-";
            string serverFragment = unlock != null ? unlock.CurrentClassFragment.ToString() : "-";
            info.Ok = saved;
            info.IsNewUnlock = unlock != null && unlock.IsNewUnlock;
            info.SpentClassFragment = unlock != null ? unlock.SpentClassFragment : 0;
            info.LogLine = $"[Job] {op} char={response.Index} job={info.JobName} code={response.JobCode} new={info.IsNewUnlock} spent={info.SpentClassFragment} fragmentBefore={fragmentBefore}" +
                $" | server job={response.JobCode} unlocked={UserManager.CountUnlocked(response.JobTrees)} fragment={serverFragment}" +
                $" | state job={stateJob} unlocked={UserManager.CountUnlocked(state)} fragment={LocalProgression.Balance(eCurrency.ClassFragment)} | saved={saved}";
            Debug.Log(info.LogLine);
            if (!saved)
            {
                Direction.GameDirectInteraction.ReportSaveFailure();
                LastFailure = NetworkManager.CreateError("ApplyFailed", "서버 전직 결과를 저장하지 못했습니다: " + LocalProgression.LastError, info.Function);
                info.Failure = "서버 전직 결과를 저장하지 못했습니다. 다시 로그인하면 서버 값으로 맞춰집니다.";
                OnJobChangeCompleted?.Invoke(info);
                return;
            }
            // 서버가 이미 바꿨으므로 그 캐릭터의 런타임 직업(스탯·스킬·스프라이트), 파티 보너스, 장비 슬롯을 바로 맞춘다.
            if (player != null) player.GetComponent<ChangeJob>()?.NotifyServerJobApplied(info.IsNewUnlock);
            ChangeJob.RefreshPartyAura();
            EquipmentManager.Instance?.RestoreEquipment();
            OnStateChanged?.Invoke();
            OnJobChangeCompleted?.Invoke(info);
        }

        private void RejectServerResult(JobChangeInfo info, PlayFabError error)
        {
            _isChangingJob = false;
            LastFailure = error;
            string kind = NetworkManager.GetFailureKind(error);
            int code = NetworkManager.TryGetBusinessError(error, out var business) ? (int)business.ErrorCode : 0;
            info.Ok = false;
            info.ErrorCode = code;
            info.LogLine = $"[Job] {(info.Function == "OnGetJob" ? "getjob" : "changejob")} char={info.Slot} job={info.JobName} rejected code={code} kind={kind} message={error?.ErrorMessage}";
            Debug.LogWarning(info.LogLine);
            if (kind == "Timeout" || kind == "Transport")
            {
                // 응답 유실: 서버가 이미 바꿨을 수 있다. 재전송하지 않고 서버 상태를 다시 받아 적용한다.
                UserManager.Instance?.RefreshFromServer(ok =>
                {
                    if (ok)
                    {
                        var players = UserManager.Instance?.GetPlayers();
                        if (players != null) foreach (var member in players) member?.GetComponent<ChangeJob>()?.ApplySavedJob();
                        ChangeJob.RefreshPartyAura();
                    }
                    info.Failure = ok ? "서버 응답이 없어 상태를 다시 불러왔습니다." : NetworkManager.DescribeFailure(error, "전직");
                    OnStateChanged?.Invoke();
                    OnJobChangeCompleted?.Invoke(info);
                });
                return;
            }
            // 거절: 서버도 저장하지 않았으므로 로컬 상태를 건드리지 않는다.
            info.Failure = NetworkManager.DescribeFailure(error, "전직");
            OnJobChangeCompleted?.Invoke(info);
        }

        /// <summary>로컬 가드 결과의 안내 문구. 서버 거절 문구는 NetworkManager.DescribeFailure가 만든다.</summary>
        public static string Describe(JobChangeResult result) => result switch
        {
            JobChangeResult.Pending => "전직을 요청했습니다.",
            JobChangeResult.Busy => "이전 전직 요청을 처리 중입니다.",
            JobChangeResult.NotAvailable => "선택할 수 없는 직업입니다.",
            JobChangeResult.SameJob => "이미 그 직업입니다.",
            JobChangeResult.Guard => "전직 조건을 확인해 주세요. 보스·던전 중에는 변경할 수 없습니다.",
            JobChangeResult.NetworkNotReady => "서버 로그인이 필요합니다.",
            JobChangeResult.UnequipFailed => "새 직업이 쓸 수 없는 장비를 먼저 해제하지 못했습니다.",
            _ => "전직에 실패했습니다."
        };

        // ── 플레이어 접근 헬퍼 ──

        public List<Player> GetPlayers()
        {
            return new List<Player>(UserManager.Instance?.GetPlayers() ?? Array.Empty<Player>());
        }
    }
}
