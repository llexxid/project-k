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
                LastClaimResult = new OfflineRewardClaimResult("server-offline:" + LocalProgression.UtcNow, plan, r.GainedGold, r.GainedExp, 0,
                    state.AccountLevel, state.Experience, state.Kills);
                OfflineRewardPopupController.Show(LastClaimResult);
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
        /// 오프라인 광고 2배(2026-11-01 서버 연결). 광고를 다 보면 OnClaimAdReward(AdType=OfflineBonus)가 마지막 정산의 골드·경험치를 한 번 더 지급한다.
        /// 광고 시청 여부는 서버가 클라이언트 보고를 믿고, 정산 1회당 한 번만 지급한다. 팝업은 이 값이 false면 광고 버튼을 숨긴다.
        /// </summary>
        public static bool AdBonusAvailable => true;

        /// <summary>팝업에 넘긴 가장 최근 정산(E2E 하네스가 광고 2배 요청에 쓴다).</summary>
        public static OfflineRewardClaimResult LastClaimResult { get; private set; }
        public static bool AdBonusBusy { get; private set; }
        /// <summary>광고 2배 요청 결과. Success는 지급(또는 이미 지급됨), NothingAvailable은 받을 정산 없음, SaveFailed는 다시 시도 가능. 메시지가 있으면 그 문구를 보여 준다.</summary>
        public static event Action<OfflineRewardClaimResult, RewardStatus, string> AdBonusCompleted;

        /// <summary>서버에 광고 2배를 요청한다. 결과는 AdBonusCompleted로 온다(Pending). 서버가 정산 1회만 지급하므로 실패 뒤 다시 불러도 두 번 받지 않는다.</summary>
        public static RewardStatus TryGrantAdBonus(OfflineRewardClaimResult result)
        {
            if (!IsCurrentAccount(result)) return RewardStatus.Stale;
            var network = NetworkManager.Instance;
            if (AdBonusBusy || network == null || !network.IsGameDataReady) return RewardStatus.NotReady;
            AdBonusBusy = true;
            network.Call<OnClaimAdRewardRequestDTO, OnClaimAdRewardResponseDTO>(new OnClaimAdRewardRequestDTO { AdType = Protocol.Message.Data.eAdType.OfflineBonus },
                response => ApplyAdBonus(result, response), error => RejectAdBonus(result, error));
            return RewardStatus.Pending;
        }

        /// <summary>응답의 레벨·경험치·재화 절대값을 한 트랜잭션으로 쓴다. 처치 수는 바꾸지 않는다(서버도 세지 않는다).</summary>
        private static void ApplyAdBonus(OfflineRewardClaimResult result, OnClaimAdRewardResponseDTO r)
        {
            AdBonusBusy = false;
            if (!IsCurrentAccount(result)) { AdBonusCompleted?.Invoke(result, RewardStatus.Stale, null); return; }
            bool saved = LocalProgression.Execute("server-offline-ad-bonus", s => { UserManager.ApplyAdReward(s, r); return true; });
            // 광고 응답은 종류와 관계없이 오늘의 상점 광고 기록을 담아 온다.
            ShopAdRewardService.ReceiveClaimRecord(r.AdRewards);
            var state = LocalProgression.State;
            Debug.Log($"[Offline] adbonus already={r.AlreadyClaimed} gold={ShopAdRewardService.Granted(r, eCurrency.Gold)} exp={r.GrantedExp}" +
                $" | server Level={r.Level} Exp={r.Exp} Gold={r.Currency?.Gold} | state Level={state.AccountLevel} Exp={state.Experience} Gold={LocalProgression.Balance(eCurrency.Gold)} | saved={saved}");
            if (!saved)
            {
                Direction.GameDirectInteraction.ReportSaveFailure();
                AdBonusCompleted?.Invoke(result, RewardStatus.SaveFailed, null);
                return;
            }
            AdBonusCompleted?.Invoke(result, RewardStatus.Success, null);
        }

        private static void RejectAdBonus(OfflineRewardClaimResult result, PlayFabError error)
        {
            string kind = NetworkManager.GetFailureKind(error);
            int code = NetworkManager.TryGetBusinessError(error, out var business) ? (int)business.ErrorCode : 0;
            Debug.LogWarning($"[Offline] adbonus rejected code={code} kind={kind} message={error?.ErrorMessage}");
            string message = NetworkManager.DescribeFailure(error, "오프라인 2배 보상");
            if (kind == "Timeout" || kind == "Transport")
            {
                // 응답 유실: 서버가 지급했을 수 있다. 재전송 대신 상태를 다시 받는다. 다시 누르면 서버가 AlreadyClaimed로 이어 준다.
                var users = UserManager.Instance;
                if (users == null) { AdBonusBusy = false; AdBonusCompleted?.Invoke(result, RewardStatus.SaveFailed, message); return; }
                users.RefreshFromServer(_ => { AdBonusBusy = false; AdBonusCompleted?.Invoke(result, RewardStatus.SaveFailed, message); });
                return;
            }
            // 거절(1051 받을 정산 없음 등): 서버는 저장하지 않았다. 다시 보내도 같다.
            AdBonusBusy = false;
            AdBonusCompleted?.Invoke(result, RewardStatus.NothingAvailable, message);
        }

        /// <summary>에디터 QA(OfflineRewardAdAcceptance)가 컴파일되도록 남긴 스텁이다. 정산은 서버(OnClaimOfflineReward)가 하므로 로컬 정산 QA는 더 이상 유효하지 않다.</summary>
        internal static bool Claim(out OfflineRewardClaimResult result)
        {
            result = null;
            return false;
        }
    }
}
