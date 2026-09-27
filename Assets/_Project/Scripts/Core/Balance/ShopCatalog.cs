using System;
using System.Collections.Generic;

namespace KingdomIdle.Balance
{
    public enum ShopCategory { Recommended, Currency, Rewarded }
    public enum ShopPayment { Store, AncientCoin, RewardedAd }

    /// <summary>Display-only offers. IDs survive SDK/server integration; prices are KRW design previews.</summary>
    public sealed class ShopOffer
    {
        public readonly string Id, Title, Detail;
        public readonly ShopCategory Category;
        public readonly ShopPayment Payment;
        public readonly int PreviewWon, AncientCoinCost, Coins, EquipmentTickets, MageTickets, GoldMinutes;
        public readonly bool PermanentAdRemoval;
        public readonly int DailyLimit;

        public ShopOffer(string id, string title, string detail, ShopCategory category, ShopPayment payment,
            int previewWon = 0, int coins = 0, int equipmentTickets = 0, int mageTickets = 0,
            int coinCost = 0, int goldMinutes = 0, bool permanentAdRemoval = false, int dailyLimit = 0)
        {
            Id = id; Title = title; Detail = detail; Category = category; Payment = payment;
            PreviewWon = previewWon; Coins = coins; EquipmentTickets = equipmentTickets; MageTickets = mageTickets;
            AncientCoinCost = coinCost; GoldMinutes = goldMinutes; PermanentAdRemoval = permanentAdRemoval; DailyLimit = dailyLimit;
        }
    }

    public static class ShopCatalog
    {
        public const string OfflineDoublePlacement = "rewarded.offline_double";
        // This is deliberately not a purchase service. Never mint rewards from preview metadata.
        public static IReadOnlyList<ShopOffer> Offers { get; } = Array.AsReadOnly(new[] {
            new ShopOffer("ki.remove_ads.permanent", "광고 제거", "무기한 · 모든 광고 제거\n광고 보상도 영상 없이 수령 · 일일 횟수는 유지", ShopCategory.Recommended, ShopPayment.Store, 3900, permanentAdRemoval: true),
            new ShopOffer("ki.package.supply_small", "작은 보급 패키지", "고대주화 1,500개 + 장비 뽑기권 10장 + 마탑 뽑기권 10장", ShopCategory.Recommended, ShopPayment.Store, 1500, 1500, 10, 10),
            new ShopOffer("ki.package.supply_medium", "든든한 보급 패키지", "고대주화 5,000개 + 장비 뽑기권 20장 + 마탑 뽑기권 20장", ShopCategory.Recommended, ShopPayment.Store, 3900, 5000, 20, 20),
            new ShopOffer("ki.coins.1000", "고대주화 1,000개", "기본 주화 상품", ShopCategory.Currency, ShopPayment.Store, 1000, 1000),
            new ShopOffer("ki.coins.3300", "고대주화 3,300개", "기본 3,000개 + 추가 300개", ShopCategory.Currency, ShopPayment.Store, 3000, 3300),
            new ShopOffer("ki.coins.5750", "고대주화 5,750개", "기본 5,000개 + 추가 750개", ShopCategory.Currency, ShopPayment.Store, 5000, 5750),
            new ShopOffer("ki.coins.12000", "고대주화 12,000개", "기본 10,000개 + 추가 2,000개", ShopCategory.Currency, ShopPayment.Store, 10000, 12000),
            new ShopOffer("ki.gold.30m", "골드 보급 · 30분", "현재 기준 골드 수입의 30분치", ShopCategory.Currency, ShopPayment.AncientCoin, coinCost: 100, goldMinutes: 30),
            new ShopOffer("ki.gold.120m", "골드 보급 · 2시간", "현재 기준 골드 수입의 2시간치", ShopCategory.Currency, ShopPayment.AncientCoin, coinCost: 300, goldMinutes: 120),
            new ShopOffer("rewarded.daily_equipment", "매일 장비 뽑기권", "광고 1회로 장비 뽑기권 2장", ShopCategory.Rewarded, ShopPayment.RewardedAd, equipmentTickets: 2, dailyLimit: 1),
            new ShopOffer("rewarded.daily_mage", "매일 마탑 뽑기권", "광고 1회로 마탑 뽑기권 2장", ShopCategory.Rewarded, ShopPayment.RewardedAd, mageTickets: 2, dailyLimit: 1)
        });

        public static ShopOffer Find(string id)
        {
            foreach (var offer in Offers) if (offer.Id == id) return offer;
            return null;
        }

        /// <summary>The same safe-wave/KPM/ruby income baseline as dynamic quest gold, before offline efficiency.</summary>
        public static decimal GoldPerMinute(ProgressionState state) => state == null ? 0 : QuestEconomy.DynamicGold(state) / 2m;
        public static long PreviewGold(ShopOffer offer, ProgressionState state)
        {
            if (offer == null || offer.GoldMinutes <= 0) return 0;
            return (long)Math.Min(long.MaxValue, decimal.Floor(GoldPerMinute(state) * offer.GoldMinutes));
        }

        public static string PreviewMessage(string id)
        {
            if (id == OfflineDoublePlacement) return "광고 보상은 준비 중입니다. 기본 보상은 이미 받았습니다.";
            var offer = Find(id);
            if (offer == null) return "준비 중인 상품입니다.";
            return offer.Payment == ShopPayment.RewardedAd ? "광고 보상은 아직 준비 중입니다." :
                offer.Payment == ShopPayment.AncientCoin ? "골드 교환은 아직 준비 중입니다." : "상품 구매는 아직 준비 중입니다.";
        }
    }
}
