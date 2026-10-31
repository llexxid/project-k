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
