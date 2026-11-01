#if UNITY_EDITOR || LOBBY_DEVICE_QA
using System;
using System.Collections.Generic;
using System.Linq;
using KingdomIdle.UGUI;

namespace KingdomIdle.Balance
{
    public static class ShopAcceptance
    {
        public static List<string> Run()
        {
            var checks = new List<string>();
            void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); checks.Add(message); }
            Check(ShopCatalog.Offers.Count == 11 && Enum.GetValues(typeof(ShopCategory)).Length == 3,
                "Eleven authored offers use three shop categories");
            Check(ShopCatalog.Offers.Select(x => x.Id).Distinct().Count() == ShopCatalog.Offers.Count, "Stable offer IDs are unique");
            Check(ShopCatalog.Offers.Count(x => x.Category == ShopCategory.Recommended) == 3 &&
                ShopCatalog.Offers.Count(x => x.Category == ShopCategory.Currency) == 6 &&
                ShopCatalog.Offers.Count(x => x.Category == ShopCategory.Rewarded) == 2,
                "Recommended, currency and daily pages retain their authored offers");
            Check(ShopCatalog.Offers.Count(x => x.Payment == ShopPayment.RewardedAd && x.DailyLimit == 1) == 2, "Equipment and mage daily placements are independent");
            Check(ShopCatalog.Find("rewarded.daily_equipment").EquipmentTickets == 2 && ShopCatalog.Find("rewarded.daily_mage").MageTickets == 2, "Each daily ad grants two corresponding tickets in the design");
            Check(ShopCatalog.Find("ki.remove_ads.permanent").PreviewWon == 3900 && ShopCatalog.Find("ki.remove_ads.permanent").PermanentAdRemoval, "Permanent ad removal preview is 3900 KRW");
            var coinPacks = ShopCatalog.Offers.Where(x => x.Id.StartsWith("ki.coins.", StringComparison.Ordinal)).OrderBy(x => x.PreviewWon).ToArray();
            Check(coinPacks.Length == 4 && coinPacks[0].Coins == 1000 && coinPacks[0].PreviewWon == 1000,
                "Base ancient coin preview is 1000 coins for 1000 KRW");
            Check(coinPacks.Zip(coinPacks.Skip(1), (a, b) => (decimal)b.Coins / b.PreviewWon > (decimal)a.Coins / a.PreviewWon).All(x => x),
                "Larger ancient coin packs offer progressively better value");
            var packages = ShopCatalog.Offers.Where(x => x.Id.StartsWith("ki.package.", StringComparison.Ordinal)).ToArray();
            Check(packages.Length == 2 && packages.All(x => x.Coins >= x.PreviewWon && x.EquipmentTickets > 0 && x.MageTickets > 0),
                "Mixed packages retain both ticket types and better value than bare base coins");
            Check(ShopCatalog.Find("ki.gold.30m").AncientCoinCost == 100 && ShopCatalog.Find("ki.gold.120m").AncientCoinCost == 300,
                "Thirty-minute and two-hour gold previews retain their authored coin costs");
            var income = new ProgressionState { OfflineStage = 0x200010001, OfflineKpm = 3.25m, RubyGoldLevel = 1 };
            Check(ShopCatalog.GoldPerMinute(income) == 33.15m && ShopCatalog.PreviewGold(ShopCatalog.Find("ki.gold.30m"), income) == 994, "Shop gold uses fractional current income and floors only the final grant preview");
            Check(ShopCatalog.GoldPerMinute(new ProgressionState()) == 30, "Unmeasured income has the existing quest baseline");
            long beforeGrowth = ShopCatalog.PreviewGold(ShopCatalog.Find("ki.gold.120m"), income);
            income.RubyGoldLevel = 10;
            Check(ShopCatalog.PreviewGold(ShopCatalog.Find("ki.gold.120m"), income) > beforeGrowth,
                "Gold previews follow permanent gold growth");
            Check(ShopCatalog.PreviewGold(ShopCatalog.Find("ki.coins.1000"), income) == 0 && ShopCatalog.PreviewGold(null, income) == 0,
                "Non-gold and missing offers cannot preview a gold grant");
            Check(ShopCatalog.Offers.All(x => !string.IsNullOrWhiteSpace(ShopCatalog.PreviewMessage(x.Id))),
                "Every authored offer has an unfinished-feature explanation");
            using (LocalProgression.BeginTestSession())
            {
                LocalProgression.OpenTestAccount("shop-" + Guid.NewGuid().ToString("N"));
                LocalProgression.Execute("shop-test-setup", state => {
                    state.OfflineStage = 0x200010001; state.OfflineKpm = 30;
                    state.Wallet[eCurrency.AncientCoin] = 10000; return true;
                });
                long revision = LocalProgression.State.Revision;
                string before = Newtonsoft.Json.JsonConvert.SerializeObject(LocalProgression.State);
                foreach (var offer in ShopCatalog.Offers) ShopPopupController.Preview(offer.Id);
                Check(before == Newtonsoft.Json.JsonConvert.SerializeObject(LocalProgression.State) && revision == LocalProgression.State.Revision, "Every purchase, currency exchange and ad preview leaves the whole account unchanged");
            }
            return checks;
        }
    }
}
#endif
