#if UNITY_EDITOR || LOBBY_DEVICE_QA
using System;
using System.Collections.Generic;
using System.Linq;
using KingdomIdle.OfflineRewards;
using KingdomIdle.UGUI;

namespace KingdomIdle.Balance
{
    public static class ShopAcceptance
    {
        public static List<string> Run()
        {
            var checks = new List<string>();
            void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); checks.Add(message); }
            Check(ShopCatalog.Offers.Select(x => x.Id).Distinct().Count() == ShopCatalog.Offers.Count, "Stable offer IDs are unique");
            Check(ShopCatalog.Offers.Count(x => x.Payment == ShopPayment.RewardedAd && x.DailyLimit == 1) == 2, "Equipment and mage daily placements are independent");
            Check(ShopCatalog.Find("rewarded.daily_equipment").EquipmentTickets == 2 && ShopCatalog.Find("rewarded.daily_mage").MageTickets == 2, "Each daily ad grants two corresponding tickets in the design");
            Check(ShopCatalog.Find("ki.remove_ads.permanent").PreviewWon == 3900 && ShopCatalog.Find("ki.remove_ads.permanent").PermanentAdRemoval, "Permanent ad removal preview is 3900 KRW");
            var income = new ProgressionState { OfflineStage = 0x200010001, OfflineKpm = 3.25m, RubyGoldLevel = 1 };
            Check(ShopCatalog.GoldPerMinute(income) == 33.15m && ShopCatalog.PreviewGold(ShopCatalog.Find("ki.gold.30m"), income) == 994, "Shop gold uses fractional current income and floors only the final grant preview");
            Check(ShopCatalog.GoldPerMinute(new ProgressionState()) == 30, "Unmeasured income has the existing quest baseline");
            var cap = OfflineRewardCalculator.CreatePlan(TimeSpan.FromHours(50), 0x200010001, 30);
            Check(cap.appliedOfflineSeconds == 21600 && cap.estimatedKillCount == 6480, "Offline income stops at six hours / 6480 kills");
            Check(!OfflineRewardCalculator.CreatePlan(TimeSpan.FromSeconds(-1), 0x200010001, 30).HasReward, "Negative elapsed time cannot award rewards");
            using (LocalProgression.BeginTestSession())
            {
                LocalProgression.TestUtcNow = 1800000000;
                LocalProgression.OpenTestAccount("shop-" + Guid.NewGuid().ToString("N"));
                LocalProgression.Execute("shop-test-setup", state => {
                    state.OfflineStage = 0x200010001; state.OfflineKpm = 30; state.LastActiveUtc = LocalProgression.UtcNow - 8 * 3600;
                    state.OfflineRubyGold = 0; state.OfflineRubyExp = 0; state.Wallet[eCurrency.AncientCoin] = 10000; return true;
                });
                long revision = LocalProgression.State.Revision;
                string before = Newtonsoft.Json.JsonConvert.SerializeObject(LocalProgression.State);
                foreach (var offer in ShopCatalog.Offers) ShopPopupController.Preview(offer.Id);
                ShopPopupController.Preview(ShopCatalog.OfflineDoublePlacement);
                Check(before == Newtonsoft.Json.JsonConvert.SerializeObject(LocalProgression.State) && revision == LocalProgression.State.Revision, "Every purchase, currency exchange and ad preview leaves the whole account unchanged");
                long gold = LocalProgression.Balance(eCurrency.Gold);
                Check(OfflineRewardManager.Claim(), "Return reward commits");
                long awarded = LocalProgression.Balance(eCurrency.Gold) - gold;
                Check(awarded == 64800 && LocalProgression.State.LastActiveUtc == LocalProgression.UtcNow, "Six-hour claim credits the safe-stage amount and advances its watermark");
                Check(OfflineRewardManager.Claim() && LocalProgression.Balance(eCurrency.Gold) == gold + awarded, "Repeated same-second claim never duplicates gold");
                LocalProgression.TestUtcNow -= 100;
                Check(OfflineRewardManager.Claim() && LocalProgression.Balance(eCurrency.Gold) == gold + awarded && LocalProgression.State.LastActiveUtc == 1800000000, "Backward clock neither awards nor rewinds the watermark");
                LocalProgression.TestUtcNow = 1800003600;
                long watermark = LocalProgression.State.LastActiveUtc;
                Check(!LocalProgression.TestFailedCommit(OfflineRewardManager.Claim) && LocalProgression.State.LastActiveUtc == watermark && LocalProgression.Balance(eCurrency.Gold) == gold + awarded, "Failed durable claim preserves interval and wallet for retry");
                Check(OfflineRewardManager.Claim() && LocalProgression.Balance(eCurrency.Gold) == gold + awarded + 10800, "Retry after storage failure credits exactly one pending hour");
            }
            return checks;
        }
    }
}
#endif
