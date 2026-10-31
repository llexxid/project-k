using System;
using KingdomIdle.Balance;
using PlayFab;
using Protocol.Message.Dto;
using Scripts.Core;
using Scripts.Core.Manager;
using UnityEngine;
namespace Reincarnation
{
    /// <summary>
    /// 환생은 서버(OnReincarnation)가 즉시 판정·적용한다. 클라이언트는 요청을 보내고 응답의 환생 상태·현재 스테이지·골드 강화·재화를
    /// 플레이어 상태에 그대로 쓴 뒤 1-1로 전환한다. 로컬 조건 검사는 안내용이고 최종 판정은 서버다.
    /// </summary>
    public class ReincarnationService
    {
        public event Action<ReincarnationState> ReincarnationStateChanged;
        /// <summary>서버 응답 처리 결과. ok=true면 message는 성공 안내, false면 거절·실패 문구다.</summary>
        public event Action<bool, string> OnReincarnationCompleted;
        public bool IsBusy { get; private set; }
        public PlayFabError LastFailure { get; private set; }
        public ReincarnationState CurrentState => new(LocalProgression.State.ReincarnationLevel, LocalProgression.State.ReincarnationCount);
        public ReincarnationService(ReincarnationPolicy policy, IReincarnationProgressStore store, IReincarnationStageGateway gateway)
        { LocalProgression.Changed += () => ReincarnationStateChanged?.Invoke(CurrentState); }

        /// <summary>서버 규칙과 같은 안내용 사전 검사. 보스 클리어 수는 현재 장 − 1, 쿨다운은 서버가 준 LastUtc 기준이다.</summary>
        public static eReincarnationFailureReason Eligibility(ProgressionState s)
        {
            if (s.ReincarnationLevel >= 300) return eReincarnationFailureReason.MaximumLevel;
            if (s.ReincarnationDay == LocalProgression.KstDay && s.ReincarnationsToday >= 3) return eReincarnationFailureReason.DailyLimit;
            if (UserManager.CycleBossClears(s.MainStage) < 1) return eReincarnationFailureReason.StageRequirementNotMet;
            if (LocalProgression.UtcNow - s.LastReincarnationUtc < 600) return eReincarnationFailureReason.Cooldown;
            return eReincarnationFailureReason.None;
        }
        public ReincarnationPreview GetPreview()
        {
            if (IsBusy) return new(false, eReincarnationFailureReason.RequestDuplication);
            var manager = StageManager.Instance;
            if (manager?.CurrentDefinition?.Type != eStageType.Main || manager.IsBossWave) return new(false, eReincarnationFailureReason.NotMainStage);
            if (manager.CurrentRunState != eStageRunState.Running) return new(false, eReincarnationFailureReason.StateIsNotRunning);
            var s = LocalProgression.State; var error = Eligibility(s);
            return error != eReincarnationFailureReason.None ? new(false, error) : new ReincarnationPolicy().Evaluate(CurrentState, true, UserManager.CycleBossClears(s.MainStage));
        }

        /// <summary>서버에 환생을 요청한다. Pending이면 OnReincarnationCompleted로 결과가 온다.</summary>
        public ReincarnationExecutionResult TryReincarnate()
        {
            if (IsBusy) return ReincarnationExecutionResult.AlreadyProcessing;
            if (!GetPreview().CanReincarnate) return ReincarnationExecutionResult.StageResetRejected;
            var network = NetworkManager.Instance;
            if (network == null || !network.IsGameDataReady) return ReincarnationExecutionResult.StorageUnavailable;
            IsBusy = true;
            LastFailure = null;
            // 서버가 1-1로 되돌리면 이전 스테이지의 배치는 거절되므로 아직 보내지 않은 처치는 버린다.
            StageManager.Instance?.DiscardPendingHunts();
            network.Call<OnReincarnationRequestDTO, OnReincarnationResponseDTO>(new OnReincarnationRequestDTO(), ApplyServerResult, RejectServerResult);
            return ReincarnationExecutionResult.Pending;
        }

        /// <summary>응답의 환생 상태·스테이지·골드 강화·재화를 한 트랜잭션으로 쓴다. 레벨·경험치는 응답에 없으므로 건드리지 않고, 영구 최고 클리어는 응답 StageState.MainClear로 맞춘다.</summary>
        private void ApplyServerResult(OnReincarnationResponseDTO r)
        {
            long stage = (long)r.StageState.CurrentStage;
            bool saved = LocalProgression.Execute("server-reincarnation", s =>
            {
                UserManager.ApplyReincarnation(s, r.Reincarnation);
                UserManager.ApplyCurrency(s, r.Currency);
                s.AttackLevel = (int)Math.Min(BalanceMath.GoldCap, Math.Max(0, r.EnchantATKCount));
                s.HealthLevel = (int)Math.Min(BalanceMath.GoldCap, Math.Max(0, r.EnchantHPCount));
                if (StageParser.GetStageType((eStage)stage) == eStageType.Main)
                {
                    s.MainStage = stage;
                    s.CycleFrontierStage = Math.Max(stage, (long)r.StageState.HighestStage);
                    s.CycleBossStage = UserManager.CycleBossClears(stage);
                }
                s.HighestMainClear = Math.Max(0, (long)r.StageState.MainClear);
                s.GoldRemainder = 0;
                s.ReincarnatedBattle = null;
                return true;
            });
            IsBusy = false;
            var state = LocalProgression.State;
            Debug.Log($"[Reincarnation] gained={r.GainedLevel} | server Level={r.Reincarnation?.Level} Count={r.Reincarnation?.Count} Today={r.Reincarnation?.TodayCount} Stage=0x{stage:X} ATK={r.EnchantATKCount} HP={r.EnchantHPCount} Gold={r.Currency?.Gold}" +
                $" | state Level={state.ReincarnationLevel} Count={state.ReincarnationCount} Today={state.ReincarnationsToday} Stage=0x{state.MainStage:X} ATK={state.AttackLevel} HP={state.HealthLevel} Gold={LocalProgression.Balance(eCurrency.Gold)} | saved={saved}");
            if (!saved)
            {
                Direction.GameDirectInteraction.ReportSaveFailure();
                LastFailure = NetworkManager.CreateError("ApplyFailed", "서버 환생 결과를 저장하지 못했습니다: " + LocalProgression.LastError);
                OnReincarnationCompleted?.Invoke(false, "서버 결과를 저장하지 못했습니다. 다시 로그인하면 서버 값으로 맞춰집니다.");
                return;
            }
            StatEnhanceManager.Instance?.ApplyToAllPlayers();
            MoveToServerStage();
            OnReincarnationCompleted?.Invoke(true, $"환생 완료! 환생 레벨 +{r.GainedLevel}");
        }

        private void RejectServerResult(PlayFabError error)
        {
            IsBusy = false;
            LastFailure = error;
            string kind = NetworkManager.GetFailureKind(error);
            int code = NetworkManager.TryGetBusinessError(error, out var business) ? (int)business.ErrorCode : 0;
            Debug.LogWarning($"[Reincarnation] rejected code={code} kind={kind} message={error?.ErrorMessage}");
            if (kind == "Timeout" || kind == "Transport")
            {
                // 응답 유실: 서버가 이미 환생했을 수 있다. 재전송 대신 상태를 다시 받아 적용하고 서버 스테이지로 맞춘다.
                UserManager.Instance?.RefreshFromServer(ok =>
                {
                    if (ok) MoveToServerStage();
                    OnReincarnationCompleted?.Invoke(false, ok ? "결과를 서버에서 다시 불러왔습니다." : NetworkManager.DescribeFailure(error, "환생"));
                });
                return;
            }
            OnReincarnationCompleted?.Invoke(false, NetworkManager.DescribeFailure(error, "환생"));
        }

        /// <summary>플레이어 상태의 메인 스테이지가 현재 전투와 다르면 그 스테이지로 전환한다(환생 뒤 1-1).</summary>
        private static void MoveToServerStage()
        {
            var manager = StageManager.Instance;
            if (manager == null) return;
            var target = (eStage)LocalProgression.State.MainStage;
            if (StageParser.GetStageType(target) != eStageType.Main || manager.CurrentStage == target) return;
            if (target == eStage.Stage1_1 && manager.TryResetMainProgress()) return;
            manager.BeginStage(target);
        }

        // 서버 연결로 무효화된 로컬 웨이브 경계 처리. 에디터 QA 스크립트 호환용으로 시그니처만 남긴다.
        public static void CommitAtBoundary(ProgressionState s, string battleId) { }
    }
}
