using System;
using KingdomIdle.Balance;
using KingdomIdle.Rewards;
using KingdomIdle.UGUI;
using PlayFab;
using Protocol.Message.Dto;
using Scripts.Core;
using Scripts.Core.Manager;
using UnityEngine;
namespace KingdomIdle.OfflineRewards
{
    /// <summary>
    /// 오프라인 보상은 서버(OnClaimOfflineReward)가 서버 시각과 저장값으로 정산한다. 클라이언트는 마지막 KPM 표본만 보내고
    /// 응답의 레벨·경험치·재화를 플레이어 상태에 그대로 쓴다. 정산 중에는 사냥 배치를 보류해 서버의 기준 시각이 먼저 밀리지 않게 한다.
    /// </summary>
    public sealed class OfflineRewardManager : MonoBehaviour
    {
        public bool IsBusy { get; private set; }
        public PlayFabError LastFailure { get; private set; }
        private bool _ready;

        /// <summary>메인 진입 때 호출한다. 정산이 끝나면(거절·실패 포함) onCompleted를 불러 스테이지를 시작한다.</summary>
        public void TryClaim(eStage stageId, Action onCompleted)
        {
            _ready = true;
            Claim(onCompleted);
        }

        private void OnApplicationPause(bool paused)
        {
            // 복귀 뒤 사냥을 다시 보고하기 전에 정산한다. 서버는 마지막 사냥 수락 시각을 기준으로 쓴다.
            if (_ready && !paused) Claim(null);
        }

        private void Claim(Action onCompleted)
        {
            if (IsBusy) { onCompleted?.Invoke(); return; }
            var network = NetworkManager.Instance;
            if (network == null || !network.IsGameDataReady)
            {
                Debug.LogWarning("[Offline] claim skipped: server login is not ready");
                onCompleted?.Invoke();
                return;
            }
            IsBusy = true;
            LastFailure = null;
            StageManager.Instance?.HoldHunts();
            decimal kpm = Math.Max(0m, Math.Min(30m, LocalProgression.State.OfflineKpm));
            network.Call<OnClaimOfflineRewardRequestDTO, OnClaimOfflineRewardResponseDTO>(new OnClaimOfflineRewardRequestDTO { KillsPerMinute = kpm },
                response => ApplyServerResult(kpm, response, onCompleted), error => RejectServerResult(kpm, error, onCompleted));
        }

        /// <summary>응답의 레벨·경험치·재화 절대값을 한 트랜잭션으로 쓴다. 획득량은 팝업 표시에만 쓴다.</summary>
        private void ApplyServerResult(decimal kpm, OnClaimOfflineRewardResponseDTO r, Action onCompleted)
        {
            bool saved = LocalProgression.Execute("server-offline-claim", s =>
            {
                s.AccountLevel = BalanceMath.Clamp(r.Level, 1, BalanceMath.AccountCap);
                s.Experience = Math.Max(0, r.Exp);
                BalanceMath.GainExperience(ref s.AccountLevel, ref s.Experience, 0);
                UserManager.ApplyCurrency(s, r.Currency);
                s.LastActiveUtc = LocalProgression.UtcNow;
                return true;
            });
            IsBusy = false;
            var state = LocalProgression.State;
            Debug.Log($"[Offline] claim kpm={kpm} elapsed={r.ElapsedSeconds} applied={r.AppliedSeconds} kills={r.KillCount} gold={r.GainedGold} exp={r.GainedExp}" +
                $" | server Level={r.Level} Exp={r.Exp} Gold={r.Currency?.Gold} | state Level={state.AccountLevel} Exp={state.Experience} Gold={LocalProgression.Balance(eCurrency.Gold)} | saved={saved}");
            StageManager.Instance?.ReleaseHunts();
            if (!saved)
            {
                Direction.GameDirectInteraction.ReportSaveFailure();
                LastFailure = NetworkManager.CreateError("ApplyFailed", "서버 오프라인 보상 결과를 저장하지 못했습니다: " + LocalProgression.LastError);
                UIManager.Instance?.ShowToast("오프라인 보상 결과를 저장하지 못했습니다. 다시 로그인하면 서버 값으로 맞춰집니다.");
            }
            else if (r.KillCount > 0)
            {
                // 팝업은 서버가 돌려준 정산 값만 보여 준다.
                var plan = new OfflineRewardPlan
                {
                    actualOfflineSeconds = r.ElapsedSeconds,
                    appliedOfflineSeconds = r.AppliedSeconds,
                    estimatedKillCount = r.KillCount > int.MaxValue ? int.MaxValue : (int)r.KillCount
                };
                OfflineRewardPopupController.Show(new OfflineRewardClaimResult("server-offline:" + LocalProgression.UtcNow, plan, r.GainedGold, r.GainedExp, 0,
                    state.AccountLevel, state.Experience, state.Kills));
            }
            onCompleted?.Invoke();
        }

        private void RejectServerResult(decimal kpm, PlayFabError error, Action onCompleted)
        {
            IsBusy = false;
            LastFailure = error;
            string kind = NetworkManager.GetFailureKind(error);
            int code = NetworkManager.TryGetBusinessError(error, out var business) ? (int)business.ErrorCode : 0;
            Debug.LogWarning($"[Offline] claim rejected kpm={kpm} code={code} kind={kind} message={error?.ErrorMessage}");
            if (kind == "Timeout" || kind == "Transport")
            {
                // 응답 유실: 서버가 이미 정산했을 수 있다. 상태를 다시 받아 적용한 뒤 게임을 계속한다.
                UserManager.Instance?.RefreshFromServer(ok =>
                {
                    StageManager.Instance?.ReleaseHunts();
                    onCompleted?.Invoke();
                });
                return;
            }
            StageManager.Instance?.ReleaseHunts();
            if (kind == "Business") UIManager.Instance?.ShowToast(NetworkManager.DescribeFailure(error, "오프라인 보상"));
            onCompleted?.Invoke();
        }

        public static bool IsCurrentAccount(OfflineRewardClaimResult result)
        {
            if (result == null || !LocalProgression.IsReady) return false;
            try { LocalProgression.Ensure(); }
            catch { return false; }
            if (result.AccountGeneration != LocalProgression.AccountGeneration ||
                result.AccountKey != LocalProgression.AccountKey ||
                result.NetworkGeneration != (NetworkManager.Instance?.AccountGeneration ?? -1)) return false;
#if UNITY_EDITOR || LOBBY_DEVICE_QA
            if (result.AccountKey.StartsWith("balance-qa-", StringComparison.Ordinal)) return true;
#endif
            return NetworkManager.Instance != null && NetworkManager.Instance.IsGameDataReady;
        }

        /// <summary>
        /// 오프라인 광고 보너스는 서버 API가 생길 때까지 보류한다. 로컬 지갑에 더해도 다음 서버 응답이 덮어쓰므로 지급하지 않는다.
        /// 팝업은 이 값이 false면 광고 버튼을 숨긴다.
        /// </summary>
        public static bool AdBonusAvailable => false;

        public static RewardStatus TryGrantAdBonus(OfflineRewardClaimResult result)
        {
            if (!IsCurrentAccount(result)) return RewardStatus.Stale;
            return RewardStatus.NothingAvailable;
        }

        /// <summary>에디터 QA(OfflineRewardAdAcceptance)가 컴파일되도록 남긴 스텁이다. 정산은 서버(OnClaimOfflineReward)가 하므로 로컬 정산 QA는 더 이상 유효하지 않다.</summary>
        internal static bool Claim(out OfflineRewardClaimResult result)
        {
            result = null;
            return false;
        }
    }
}
