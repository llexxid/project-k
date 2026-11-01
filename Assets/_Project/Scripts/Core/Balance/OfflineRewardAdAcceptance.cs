#if UNITY_EDITOR || LOBBY_DEVICE_QA
using System;
using System.Collections.Generic;
using System.Reflection;
using KingdomIdle.OfflineRewards;
using KingdomIdle.Rewards;
using Newtonsoft.Json;
using UnityEngine;

namespace KingdomIdle.Balance
{
    public static class OfflineRewardAdAcceptance
    {
        public static Dictionary<string, object> Run()
        {
            using var scope = LocalProgression.BeginTestSession();
            var checks = new List<string>();
            void Check(bool condition, string label)
            {
                if (!condition) throw new InvalidOperationException("OFFLINE AD ASSERT: " + label);
                checks.Add(label);
            }
            string account = "offline-ad-" + Guid.NewGuid().ToString("N");
            LocalProgression.TestUtcNow = 1793100000;
            LocalProgression.OpenTestAccount(account);
            Check(LocalProgression.Execute("offline-ad-fixture", s => {
                s.LastActiveUtc = LocalProgression.UtcNow - 600;
                s.OfflineStage = 0x200010001;
                s.OfflineKpm = 3.25m;
                s.OfflineRubyGold = 2;
                s.OfflineRubyExp = 3;
                s.Kills = 77;
                s.Wallet[eCurrency.Gold] = 123;
                return true;
            }), "격리 계정의 오프라인 구간 준비");
            Check(OfflineRewardManager.Claim(out var reward) && reward != null &&
                reward.GoldGained > 0 && reward.ExperienceGained > 0 &&
                LocalProgression.Balance(eCurrency.Gold) == 123 + reward.GoldGained &&
                LocalProgression.State.Claims.Contains(reward.ClaimId), "기본 보상과 정산 ID가 팝업 전에 함께 저장됨");
            Check(!typeof(OfflineRewardClaimResult).GetProperty(nameof(reward.ExperienceGained)).CanWrite &&
                !typeof(OfflineRewardClaimResult).GetProperty(nameof(reward.GoldGained)).CanWrite,
                "최초 정산 골드와 경험치는 읽기 전용");
            Check(OfflineRewardManager.Claim(out var empty) && empty == null,
                "같은 방치 시간을 다시 정산해도 추가 기본 보상 없음");
            Check(LocalProgression.Execute("offline-ad-growth-change", s => {
                s.OfflineKpm = 30;
                s.RubyGoldLevel = s.RubyExpLevel = 20;
                return true;
            }), "광고 시청 중 성장 조건 변경");
            long gold = LocalProgression.Balance(eCurrency.Gold);
            int level = LocalProgression.State.AccountLevel;
            long exp = LocalProgression.State.Experience;
            BalanceMath.GainExperience(ref level, ref exp, reward.ExperienceGained);
            string counters = JsonConvert.SerializeObject(LocalProgression.State.Counters);
            string before = JsonConvert.SerializeObject(LocalProgression.State);
            RewardStatus failed = RewardStatus.Success;
            Check(!LocalProgression.TestFailedCommit(() => {
                failed = OfflineRewardManager.TryGrantAdBonus(reward);
                return failed == RewardStatus.Success;
            }) && failed == RewardStatus.SaveFailed && JsonConvert.SerializeObject(LocalProgression.State) == before,
                "추가 지급 저장 실패는 지갑·경험치·수령 기록을 모두 보존함");
            Check(OfflineRewardManager.TryGrantAdBonus(reward) == RewardStatus.Success &&
                LocalProgression.Balance(eCurrency.Gold) == gold + reward.GoldGained &&
                LocalProgression.State.AccountLevel == level && LocalProgression.State.Experience == exp,
                "실패 후 같은 정산으로 재시도하면 변경된 성장 조건 대신 원본 금액 한 벌만 지급됨");
            Check(LocalProgression.State.Kills == 77 && JsonConvert.SerializeObject(LocalProgression.State.Counters) == counters,
                "광고 보너스는 처치 수와 오프라인 수령 퀘스트 횟수를 추가하지 않음");
            long revision = LocalProgression.State.Revision;
            Check(OfflineRewardManager.TryGrantAdBonus(reward) == RewardStatus.Success &&
                LocalProgression.State.Revision == revision && LocalProgression.Balance(eCurrency.Gold) == gold + reward.GoldGained,
                "중복 완료 요청은 성공 결과만 반환하고 재지급하지 않음");
            var unissued = new OfflineRewardClaimResult("unissued", reward.Plan, 999, 999, 0, level, exp, 77);
            Check(OfflineRewardManager.TryGrantAdBonus(unissued) == RewardStatus.InvalidReward && LocalProgression.State.Revision == revision,
                "발급하지 않은 기본 정산의 추가 지급 거절");

            var owner = new GameObject("OfflineRewardAdAcceptance");
            try
            {
                var manager = owner.AddComponent<OfflineRewardManager>();
                const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
                void Set(string name, bool value) => typeof(OfflineRewardManager).GetField(name, flags).SetValue(manager, value);
                bool Get(string name) => (bool)typeof(OfflineRewardManager).GetField(name, flags).GetValue(manager);
                Set("_ready", true);
                Set("_suspended", true);
                Set("_adSuspended", true);
                LocalProgression.TestUtcNow += 60;
                before = JsonConvert.SerializeObject(LocalProgression.State);
                Check(!LocalProgression.TestFailedCommit(() => {
                    manager.SendMessage("OnApplicationPause", false);
                    return !Get("_suspended");
                }) && Get("_resumePending") && Get("_adSuspended") &&
                    JsonConvert.SerializeObject(LocalProgression.State) == before,
                    "광고 복귀 체크포인트 실패 시 복귀 원인과 재시도 상태 보존");
                typeof(OfflineRewardManager).GetMethod("Resume", flags).Invoke(manager, null);
                Check(!Get("_suspended") && !Get("_adSuspended") &&
                    LocalProgression.State.LastActiveUtc == LocalProgression.UtcNow &&
                    LocalProgression.Balance(eCurrency.Gold) == gold + reward.GoldGained &&
                    JsonConvert.SerializeObject(LocalProgression.State.Counters) == counters,
                    "SDK 종료 후의 광고 복귀도 새 보상 정산 없이 활동 시각만 저장함");
            }
            finally { UnityEngine.Object.DestroyImmediate(owner); }
            LocalProgression.OpenTestAccount(account + "-other");
            revision = LocalProgression.State.Revision;
            Check(!OfflineRewardManager.IsCurrentAccount(reward) &&
                OfflineRewardManager.TryGrantAdBonus(reward) == RewardStatus.Stale && LocalProgression.State.Revision == revision,
                "다른 계정에서는 이전 광고 보상 거절");
            LocalProgression.OpenTestAccount(account);
            Check(LocalProgression.Balance(eCurrency.Gold) == gold + reward.GoldGained &&
                LocalProgression.State.Claims.Contains("offline-ad:" + reward.ClaimId) &&
                OfflineRewardManager.TryGrantAdBonus(reward) == RewardStatus.Stale,
                "다시 연 계정은 저장된 보상과 중복 방지 기록을 복원하고 이전 실행 토큰은 거절");
            return new Dictionary<string, object> { { "passed", checks.Count }, { "checks", checks } };
        }
    }
}
#endif
