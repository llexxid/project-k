using System;
using KingdomIdle.Balance;
using KingdomIdle.UGUI;
using Scripts.Core;
using UnityEngine;
namespace KingdomIdle.OfflineRewards
{
    // One durable transaction advances the interval and credits its exact reward.
    public sealed class OfflineRewardManager : MonoBehaviour
    {
        private bool _ready, _suspended;
        private float _heartbeat;
        private void Update()
        {
            if (!_ready || _suspended) return;
            _heartbeat += Time.unscaledDeltaTime;
            if (_heartbeat >= 15f) { _heartbeat = 0; Checkpoint(); }
        }
        public void TryClaim(eStage stageId, Action onCompleted)
        {
            if (!Claim()) { UIManager.Instance?.ShowToast("보상 저장에 실패했습니다. 저장 공간을 확인한 뒤 다시 접속해 주세요."); return; }
            _ready = true; onCompleted?.Invoke();
        }
        private void OnApplicationPause(bool paused)
        {
            if (!_ready) return;
            if (paused) { Checkpoint(); _suspended = true; }
            else if (_suspended) { _suspended = !Claim(); }
        }
        private void OnApplicationQuit() { if (_ready && !_suspended) Checkpoint(); }
        private void Checkpoint()
        {
            LocalProgression.Execute("activity-checkpoint", s => {
                s.LastActiveUtc = Math.Max(s.LastActiveUtc, LocalProgression.UtcNow);
                s.OfflineRubyGold = s.RubyGoldLevel; s.OfflineRubyExp = s.RubyExpLevel;
                return true;
            });
        }
        public static bool Claim()
        {
            var old = LocalProgression.State;
            long to = LocalProgression.UtcNow, from = old.LastActiveUtc;
            if (to < from) return true; // Do not rewind the watermark when the local clock moves backward.
            var plan = OfflineRewardCalculator.CreatePlan(TimeSpan.FromSeconds(from > 0 ? to - from : 0), old.OfflineStage, old.OfflineKpm);
            string claim = $"offline:{from}:{to}";
            long gold = 0, exp = 0;
            bool ok = LocalProgression.Execute("offline-claim", s => {
                if (s.LastActiveUtc != from) return false;
                if (plan.HasReward)
                {
                    var reward = BalanceMath.MainEnemy((int)((s.OfflineStage >> 16) & 0xFFF), (int)(s.OfflineStage & 0xFFFF));
                    gold = BalanceMath.WithRemainder(checked(reward.Gold * plan.estimatedKillCount), s.OfflineRubyGold, ref s.GoldRemainder);
                    exp = BalanceMath.WithRemainder(checked(reward.Experience * plan.estimatedKillCount), s.OfflineRubyExp, ref s.ExpRemainder);
                    LocalProgression.Credit(s, eCurrency.Gold, gold);
                    BalanceMath.GainExperience(ref s.AccountLevel, ref s.Experience, exp);
                    QuestEconomy.Count(s, eQuestObjectiveType.OfflineClaim, 0, 1);
                }
                s.Modules["last-offline-claim"] = claim;
                s.LastActiveUtc = to;
                s.OfflineRubyGold = s.RubyGoldLevel; s.OfflineRubyExp = s.RubyExpLevel;
                return true;
            });
            if (ok && plan.HasReward)
            {
                var s = LocalProgression.State;
                OfflineRewardPopupController.Show(new OfflineRewardClaimResult(plan, gold, 0, s.AccountLevel, s.Experience, s.Kills) { ExperienceGained = exp });
            }
            return ok;
        }
    }
}
