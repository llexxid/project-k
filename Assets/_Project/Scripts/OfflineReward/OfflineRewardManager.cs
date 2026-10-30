using System;
using KingdomIdle.Ads;
using KingdomIdle.Balance;
using KingdomIdle.Rewards;
using KingdomIdle.UGUI;
using Scripts.Core;
using Scripts.Core.Manager;
using UnityEngine;
namespace KingdomIdle.OfflineRewards
{
    // One durable transaction advances the interval and credits its exact reward.
    public sealed class OfflineRewardManager : MonoBehaviour
    {
        private bool _ready, _suspended, _adSuspended, _resumePending;
        private float _heartbeat;
        private void Update()
        {
            if (!_ready || (_suspended && !_resumePending)) return;
            _heartbeat += Time.unscaledDeltaTime;
            if (_heartbeat >= 15f)
            {
                _heartbeat = 0;
                if (_resumePending) Resume();
                else Checkpoint();
            }
        }
        public void TryClaim(eStage stageId, Action onCompleted)
        {
            if (!Claim()) { UIManager.Instance?.ShowToast("보상 저장에 실패했습니다. 저장 공간을 확인한 뒤 다시 접속해 주세요."); return; }
            _ready = true; onCompleted?.Invoke();
        }
        private void OnApplicationPause(bool paused)
        {
            if (!_ready) return;
            if (paused)
            {
                // The SDK may finish before Unity receives resume, so remember the cause at suspension.
                _adSuspended |= RewardedAdService.IsShowing;
                Checkpoint();
                _suspended = true;
                _resumePending = false;
            }
            else if (_suspended) { _resumePending = true; Resume(); }
        }
        private void Resume()
        {
            if (!(_adSuspended ? Checkpoint() : Claim())) return;
            _suspended = _adSuspended = _resumePending = false;
            _heartbeat = 0;
        }
        private void OnApplicationQuit() { if (_ready && !_suspended) Checkpoint(); }
        private bool Checkpoint()
        {
            return LocalProgression.Execute("activity-checkpoint", s => {
                s.LastActiveUtc = Math.Max(s.LastActiveUtc, LocalProgression.UtcNow);
                s.OfflineRubyGold = s.RubyGoldLevel; s.OfflineRubyExp = s.RubyExpLevel;
                return true;
            });
        }
        public static bool Claim()
        {
            if (!Claim(out var result)) return false;
            if (result != null) OfflineRewardPopupController.Show(result);
            return true;
        }
        internal static bool Claim(out OfflineRewardClaimResult result)
        {
            result = null;
            var old = LocalProgression.State;
            long account = LocalProgression.AccountGeneration;
            long to = LocalProgression.UtcNow, from = old.LastActiveUtc;
            if (to < from) return true; // Do not rewind the watermark when the local clock moves backward.
            var plan = OfflineRewardCalculator.CreatePlan(TimeSpan.FromSeconds(from > 0 ? to - from : 0), old.OfflineStage, old.OfflineKpm);
            string claim = $"offline:{from}:{to}";
            long gold = 0, exp = 0;
            OfflineRewardClaimResult settled = null;
            bool ok = LocalProgression.Execute("offline-claim", s => {
                if (account != LocalProgression.AccountGeneration || s.LastActiveUtc != from) return false;
                if (plan.HasReward)
                {
                    var reward = Scripts.Core.StageCatalogRules.MainEnemy(Scripts.Core.Manager.StageParser.GetStageNumber((Scripts.Core.eStage)s.OfflineStage), Scripts.Core.Manager.StageParser.GetWaveNumber((Scripts.Core.eStage)s.OfflineStage));
                    gold = BalanceMath.WithRemainder(checked(reward.Gold * plan.estimatedKillCount), s.OfflineRubyGold, ref s.GoldRemainder);
                    exp = BalanceMath.WithRemainder(checked(reward.Experience * plan.estimatedKillCount), s.OfflineRubyExp, ref s.ExpRemainder);
                    LocalProgression.Credit(s, eCurrency.Gold, gold);
                    BalanceMath.GainExperience(ref s.AccountLevel, ref s.Experience, exp);
                    QuestEconomy.Count(s, eQuestObjectiveType.OfflineClaim, 0, 1);
                    settled = new OfflineRewardClaimResult(claim, plan, gold, exp, 0,
                        s.AccountLevel, s.Experience, s.Kills);
                }
                s.Modules["last-offline-claim"] = claim;
                s.LastActiveUtc = to;
                s.OfflineRubyGold = s.RubyGoldLevel; s.OfflineRubyExp = s.RubyExpLevel;
                return true;
            }, plan.HasReward ? claim : null);
            if (ok) result = settled;
            return ok;
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

        public static RewardStatus TryGrantAdBonus(OfflineRewardClaimResult result)
        {
            if (!IsCurrentAccount(result)) return RewardStatus.Stale;
            if (string.IsNullOrEmpty(result.ClaimId) || result.GoldGained < 0 || result.ExperienceGained < 0 ||
                result.GoldGained > long.MaxValue / 2 || result.ExperienceGained > long.MaxValue / 2 ||
                (result.GoldGained == 0 && result.ExperienceGained == 0) ||
                !LocalProgression.State.Claims.Contains(result.ClaimId)) return RewardStatus.InvalidReward;
            string claim = "offline-ad:" + result.ClaimId;
            if (LocalProgression.State.Claims.Contains(claim)) return RewardStatus.Success;
            var failure = RewardStatus.SaveFailed;
            bool saved = LocalProgression.Execute("offline-ad-bonus", s => {
                if (!IsCurrentAccount(result)) { failure = RewardStatus.Stale; return false; }
                if (!s.Claims.Contains(result.ClaimId)) { failure = RewardStatus.InvalidReward; return false; }
                long balance = s.Wallet.TryGetValue(eCurrency.Gold, out var value) ? value : 0;
                if (result.GoldGained > long.MaxValue - balance) { failure = RewardStatus.InvalidReward; return false; }
                LocalProgression.Credit(s, eCurrency.Gold, result.GoldGained);
                BalanceMath.GainExperience(ref s.AccountLevel, ref s.Experience, result.ExperienceGained);
                return true;
            }, claim);
            return saved ? RewardStatus.Success : failure;
        }
    }
}
