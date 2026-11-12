#if UNITY_EDITOR || LOBBY_DEVICE_QA
using System;
using System.Collections.Generic;
using KingdomIdle.Ads;
using KingdomIdle.Rewards;
using Newtonsoft.Json;

namespace KingdomIdle.Balance
{
    public static class ShopAdRewardAcceptance
    {
#if UNITY_EDITOR
        /// <summary>서버 오류·기수령 응답을 격리해서 적용한다. 로그인·광고 SDK·HTTP는 호출하지 않는다.</summary>
        public static Dictionary<string, object> RunServerFailureRecovery()
        {
            using var scope = LocalProgression.BeginTestSession();
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
            var service = typeof(ShopAdRewardService);
            var busy = service.GetField("<IsBusy>k__BackingField", flags);
            var saved = new Dictionary<System.Reflection.FieldInfo, object>();
            foreach (string name in new[] { "_server", "_serverOffset", "<Revision>k__BackingField", "<IsBusy>k__BackingField" })
            {
                var field = service.GetField(name, flags);
                saved[field] = field.GetValue(null);
            }
            var previousNetwork = Scripts.Core.Manager.NetworkManager.Instance;
            var previousUsers = Scripts.Core.UserManager.Instance;
            var owner = new UnityEngine.GameObject("ShopAdServerFailureAcceptance") { hideFlags = UnityEngine.HideFlags.HideAndDontSave };
            owner.SetActive(false);
            var checks = new List<string>();
            void Check(bool value, string label)
            { if (!value) throw new InvalidOperationException("SHOP AD SERVER ASSERT: " + label); checks.Add(label); }
            RewardResult completed = null;
            ShopAdRewardService.Claim completedClaim = null;
            void OnCompleted(ShopAdRewardService.Claim claim, RewardResult result, string message)
            { completedClaim = claim; completed = result; }
            ShopAdRewardService.ClaimCompleted += OnCompleted;
            try
            {
                var network = owner.AddComponent<Scripts.Core.Manager.NetworkManager>();
                typeof(Scripts.Core.Manager.NetworkManager).GetProperty("LoginState").SetValue(network, Scripts.Core.Manager.GameLoginState.Ready);
                Scripts.Core.Manager.NetworkManager.Instance = network;
                // 복구 조회를 보낼 수 없는 경우도 Earned 권한을 유지해야 한다. 실제 조회 경로는 외부 dependency fixture에서 검사한다.
                Scripts.Core.UserManager.Instance = null;
                LocalProgression.OpenTestAccount("shop-ad-server-" + Guid.NewGuid().ToString("N"));
                ShopAdRewardService.Receive(new Protocol.Message.Dto.AdRewardStateDTO
                { Day = ShopAdRewardService.Today, ClaimedOfferIds = new List<string>() }, 0, "fixture");
                var claim = new ShopAdRewardService.Claim(ShopCatalog.Find("rewarded.daily_equipment"), eCurrency.EquipmentTicket) { Earned = true };
                string original = JsonConvert.SerializeObject(LocalProgression.State);
                var reject = service.GetMethod("RejectServerResult", flags);
                foreach (var failure in new[] { ("Business", 1014), ("Business", 1017), ("InvalidResponse", 0),
                    ("Function", 0), ("ApplyFailed", 0), ("Transport", 0), ("Timeout", 0), ("", 0) })
                {
                    var error = Scripts.Core.Manager.NetworkManager.CreateError(failure.Item1, "fixture", null,
                        failure.Item2 == 0 ? null : new Protocol.Message.Dto.ErrorResponseDTO
                        { ErrorCode = (Protocol.Message.Data.eErrorCode)failure.Item2, Message = "fixture" });
                    completed = null; busy.SetValue(null, true);
                    reject.Invoke(null, new object[] { claim, error });
                    Check(completed?.Status == RewardStatus.SaveFailed && ReferenceEquals(completedClaim, claim) &&
                        claim.Earned && !ShopAdRewardService.IsBusy, "uncertain result retains the same earned claim: " + failure);
                }
                var invalid = Scripts.Core.Manager.NetworkManager.CreateError("Business", "fixture", null,
                    new Protocol.Message.Dto.ErrorResponseDTO { ErrorCode = (Protocol.Message.Data.eErrorCode)1050, Message = "fixture" });
                reject.Invoke(null, new object[] { claim, invalid });
                Check(completed.Status == RewardStatus.InvalidReward, "confirmed invalid ad request remains invalid");
                Check(JsonConvert.SerializeObject(LocalProgression.State) == original, "rejected requests never grant local currency");

                var response = new Protocol.Message.Dto.OnClaimAdRewardResponseDTO
                {
                    AdType = Protocol.Message.Data.eAdType.ShopDaily, OfferId = claim.OfferId, Level = 1,
                    Currency = new Protocol.Message.Dto.CurrencyQueryDTO { EquipmentTicket = 2 },
                    AdRewards = new Protocol.Message.Dto.AdRewardStateDTO
                    { Day = ShopAdRewardService.Today, ClaimedOfferIds = new List<string> { claim.OfferId } },
                    GrantedCurrencies = new List<Protocol.Message.Dto.CurrencyAmountDTO>
                    { new() { CurrencyType = Protocol.Message.Data.eCurrency.EquipmentTicket, Amount = 2 } }
                };
                var apply = service.GetMethod("ApplyServerResult", flags);
                apply.Invoke(null, new object[] { claim, response });
                Check(completed.Succeeded && LocalProgression.Balance(eCurrency.EquipmentTicket) == 2,
                    "retry success applies the authoritative ticket balance");
                response.AlreadyClaimed = true; response.GrantedCurrencies.Clear();
                apply.Invoke(null, new object[] { claim, response });
                Check(completed.Status == RewardStatus.NothingAvailable && LocalProgression.Balance(eCurrency.EquipmentTicket) == 2,
                    "replayed server receipt does not add a second local reward");
                Check(ShopAdRewardService.Begin(claim.OfferId, out _) == RewardStatus.NothingAvailable,
                    "confirmed receipt blocks another advertisement for the same day");
                LocalProgression.OpenTestAccount("shop-ad-server-other-" + Guid.NewGuid().ToString("N"));
                Check(ShopAdRewardService.FinishAd(claim, RewardedAdResult.Completed).Status == RewardStatus.Stale,
                    "account changes still reject the old earned claim");
                Check(typeof(Scripts.Core.Manager.NetworkManager).GetField("_context", System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic).GetValue(network) == null, "fixture stayed unauthenticated without HTTP");
                return new Dictionary<string, object> { { "passed", checks.Count }, { "checks", checks } };
            }
            finally
            {
                ShopAdRewardService.ClaimCompleted -= OnCompleted;
                foreach (var field in saved) field.Key.SetValue(null, field.Value);
                Scripts.Core.Manager.NetworkManager.Instance = previousNetwork;
                Scripts.Core.UserManager.Instance = previousUsers;
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        public static Dictionary<string, object> RunReset()
        {
            using var scope = LocalProgression.BeginTestSession();
            var checks = new List<string>();
            void Check(bool ok, string text)
            { if (!ok) throw new InvalidOperationException("SHOP AD RESET ASSERT: " + text); checks.Add(text); }
            const string equipment = "rewarded.daily_equipment", mage = "rewarded.daily_mage";
            string account = "shop-ad-reset-" + Guid.NewGuid().ToString("N");
            LocalProgression.TestUtcNow = new DateTimeOffset(2026, 10, 30, 12, 0, 0, TimeSpan.FromHours(9)).ToUnixTimeSeconds();
            LocalProgression.OpenTestAccount(account);
            foreach (var id in new[] { equipment, mage })
            {
                Check(ShopAdRewardService.Begin(id, out var claim) == RewardStatus.Success &&
                    ShopAdRewardService.FinishAd(claim, RewardedAdResult.Completed).Succeeded, "Seed claimed offer: " + id);
            }
            LocalProgression.Execute("test-reset-history", state => {
                state.Claims.Add("shop-ad:" + equipment + ":2026-10-29");
                state.Claims.Add("offline-ad:test-reset-preserve");
                return true;
            });
            string original = JsonConvert.SerializeObject(LocalProgression.State);
            Check(!LocalProgression.TestFailedCommit(() => ShopAdRewardService.ResetTodayForTesting() == RewardStatus.Success) &&
                JsonConvert.SerializeObject(LocalProgression.State) == original, "Failed reset save preserves all state");
            Check(ShopAdRewardService.ResetTodayForTesting() == RewardStatus.Success &&
                !ShopAdRewardService.IsClaimed(equipment, ShopAdRewardService.Today) &&
                !ShopAdRewardService.IsClaimed(mage, ShopAdRewardService.Today), "Reset removes both current-day claims");
            Check(LocalProgression.Balance(eCurrency.EquipmentTicket) == 2 && LocalProgression.Balance(eCurrency.MageTicket) == 2 &&
                LocalProgression.State.Claims.Contains("shop-ad:" + equipment + ":2026-10-29") &&
                LocalProgression.State.Claims.Contains("offline-ad:test-reset-preserve"), "Reset preserves tickets, previous days and offline claims");
            LocalProgression.OpenTestAccount(account);
            Check(!ShopAdRewardService.IsClaimed(equipment, ShopAdRewardService.Today) &&
                !ShopAdRewardService.IsClaimed(mage, ShopAdRewardService.Today), "Reset survives reopening the account");
            Check(ShopAdRewardService.Begin(equipment, out var again) == RewardStatus.Success &&
                ShopAdRewardService.FinishAd(again, RewardedAdResult.Completed).Succeeded &&
                LocalProgression.Balance(eCurrency.EquipmentTicket) == 4 && LocalProgression.Balance(eCurrency.MageTicket) == 2,
                "Watching again adds two tickets without clearing existing balances");
            return new Dictionary<string, object> { { "passed", checks.Count }, { "checks", checks } };
        }
#endif

        public static Dictionary<string, object> Run()
        {
            using var scope = LocalProgression.BeginTestSession();
            var checks = new List<string>();
            void Check(bool ok, string text)
            { if (!ok) throw new InvalidOperationException("SHOP AD ASSERT: " + text); checks.Add(text); }
            const string equipment = "rewarded.daily_equipment", mage = "rewarded.daily_mage";
            string account = "shop-ad-" + Guid.NewGuid().ToString("N");
            long beforeMidnight = new DateTimeOffset(2026, 10, 30, 23, 59, 0, TimeSpan.FromHours(9)).ToUnixTimeSeconds();
            LocalProgression.TestUtcNow = beforeMidnight;
            LocalProgression.OpenTestAccount(account);
            string Snapshot() => JsonConvert.SerializeObject(LocalProgression.State);
            var original = Snapshot();
            foreach (string invalid in new[] { "missing", "ki.coins.1000", "ki.gold.30m", ShopCatalog.OfflineDoublePlacement })
                Check(ShopAdRewardService.Begin(invalid, out _) == RewardStatus.InvalidReward, "Reject non-daily offer: " + invalid);
            Check(ShopAdRewardService.Begin(equipment, out var first) == RewardStatus.Success && first.Day == "2026-10-30" &&
                ShopAdRewardService.TryGrant(first).Status == RewardStatus.InvalidReward && Snapshot() == original,
                "Starting an ad grants nothing; payout requires completion");
            foreach (var outcome in new[] { RewardedAdResult.Skipped, RewardedAdResult.Failed, RewardedAdResult.Busy })
                Check(ShopAdRewardService.FinishAd(first, outcome).Status == RewardStatus.NothingAvailable &&
                    !first.Earned && Snapshot() == original, "No reward or daily usage on " + outcome);
            Check(ShopAdRewardService.Begin(equipment, out var duplicate) == RewardStatus.Success,
                "A second request captured before the first save still shares its durable daily key");
            RewardResult failure = null;
            Check(!LocalProgression.TestFailedCommit(() => {
                failure = ShopAdRewardService.FinishAd(first, RewardedAdResult.Completed);
                return failure.Succeeded;
            }) && failure.Status == RewardStatus.SaveFailed && first.Earned && Snapshot() == original,
                "Failed save keeps tickets and daily ledger unchanged, retaining earned entitlement");
            LocalProgression.TestUtcNow = beforeMidnight + 120;
            Check(ShopAdRewardService.TryGrant(first).Succeeded && LocalProgression.Balance(eCurrency.EquipmentTicket) == 2 &&
                ShopAdRewardService.IsClaimed(equipment, "2026-10-30") && !ShopAdRewardService.IsClaimed(equipment, "2026-10-31"),
                "Retry after midnight grants two tickets to the ad start date without another ad");
            long revision = LocalProgression.State.Revision;
            Check(ShopAdRewardService.TryGrant(first).Status == RewardStatus.NothingAvailable &&
                ShopAdRewardService.FinishAd(duplicate, RewardedAdResult.Completed).Status == RewardStatus.NothingAvailable &&
                LocalProgression.State.Revision == revision && LocalProgression.Balance(eCurrency.EquipmentTicket) == 2,
                "Repeated callbacks and concurrent snapshots cannot duplicate a daily reward");
            Check(ShopAdRewardService.Begin(equipment, out var nextDay) == RewardStatus.Success &&
                ShopAdRewardService.FinishAd(nextDay, RewardedAdResult.Completed).Succeeded &&
                LocalProgression.Balance(eCurrency.EquipmentTicket) == 4,
                "The new day remains independently claimable after yesterday's delayed reward");
            Check(ShopAdRewardService.Begin(equipment, out _) == RewardStatus.NothingAvailable,
                "Already claimed offer rejects a new ad before playback");
            Check(ShopAdRewardService.Begin(mage, out var mageClaim) == RewardStatus.Success &&
                ShopAdRewardService.FinishAd(mageClaim, RewardedAdResult.Completed).Succeeded &&
                LocalProgression.Balance(eCurrency.MageTicket) == 2 && LocalProgression.Balance(eCurrency.EquipmentTicket) == 4,
                "Mage and equipment rewards have separate balances and daily limits");
            Check(LocalProgression.Balance(eCurrency.AncientCoin) == 0 && LocalProgression.State.Kills == 0,
                "Ad rewards do not spend coins, perform a draw, or award kills");
            LocalProgression.TestUtcNow = beforeMidnight;
            Check(ShopAdRewardService.Today == "2026-10-31" && ShopAdRewardService.Begin(mage, out _) == RewardStatus.NothingAvailable,
                "Moving the clock backwards cannot reopen a committed daily period");
            LocalProgression.TestUtcNow = beforeMidnight + 86460;
            Check(ShopAdRewardService.Begin(mage, out var stale) == RewardStatus.Success, "Capture next-day account ownership");
            LocalProgression.OpenTestAccount(account + "-other");
            original = Snapshot();
            Check(ShopAdRewardService.FinishAd(stale, RewardedAdResult.Completed).Status == RewardStatus.Stale && Snapshot() == original,
                "Account switch rejects late ad completion without touching the other account");
            LocalProgression.OpenTestAccount(account);
            Check(LocalProgression.Balance(eCurrency.EquipmentTicket) == 4 && LocalProgression.Balance(eCurrency.MageTicket) == 2 &&
                ShopAdRewardService.IsClaimed(equipment, "2026-10-31") && !ShopAdRewardService.IsCurrentAccount(stale),
                "Reopening preserves tickets and daily ledger, but invalidates old in-memory requests");
            LocalProgression.Execute("shop-ad-overflow-fixture", s => { s.Wallet[eCurrency.MageTicket] = long.MaxValue; return true; });
            ShopAdRewardService.Begin(mage, out var overflow);
            original = Snapshot();
            Check(ShopAdRewardService.FinishAd(overflow, RewardedAdResult.Completed).Status == RewardStatus.InvalidReward && Snapshot() == original,
                "Ticket overflow rejects the whole transaction including daily usage");
            Check(RewardGrants.IsValid(new RewardGrant(eCurrency.EquipmentTicket, 2)) &&
                RewardGrants.IsValid(new RewardGrant(eCurrency.MageTicket, 2)) &&
                !RewardGrants.IsValid(new RewardGrant((eCurrency)10, 2)), "Shared reward validation supports exactly the new ticket IDs");
            return new Dictionary<string, object> { { "passed", checks.Count }, { "checks", checks } };
        }
    }
}
#endif
