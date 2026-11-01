using KingdomIdle.Ads;
using KingdomIdle.Rewards;
using Scripts.Core.Manager;

namespace KingdomIdle.Balance
{
    /// <summary>광고 시작일·계정을 고정하고 일일 보상과 수령 기록을 함께 저장한다.</summary>
    public static class ShopAdRewardService
    {
        public sealed class Claim
        {
            public string OfferId { get; }
            public string Day { get; }
            public bool Earned { get; internal set; }
            internal readonly string AccountKey, ClaimId;
            internal readonly long AccountGeneration, NetworkGeneration, Amount;
            internal readonly eCurrency Currency;

            internal Claim(ShopOffer offer, eCurrency currency)
            {
                OfferId = offer.Id; Day = Today;
                ClaimId = Key(OfferId, Day);
                Currency = currency;
                Amount = currency == eCurrency.EquipmentTicket ? offer.EquipmentTickets : offer.MageTickets;
                AccountKey = LocalProgression.AccountKey;
                AccountGeneration = LocalProgression.AccountGeneration;
                NetworkGeneration = NetworkManager.Instance?.AccountGeneration ?? -1;
            }
        }

        public static string Today => QuestPeriod.At(RewardGrants.Now).Day;
        static string Key(string id, string day) => "shop-ad:" + id + ":" + day;
        public static bool IsClaimed(string id, string day) =>
            LocalProgression.TryGetCommittedState(out var state) && state.Claims.Contains(Key(id, day));

        public static bool TryGetCurrency(string id, out eCurrency currency)
        {
            currency = id == "rewarded.daily_equipment" ? eCurrency.EquipmentTicket : eCurrency.MageTicket;
            return id == "rewarded.daily_equipment" || id == "rewarded.daily_mage";
        }

        public static RewardStatus Begin(string id, out Claim claim)
        {
            claim = null;
            var offer = ShopCatalog.Find(id);
            if (offer == null || offer.Payment != ShopPayment.RewardedAd || offer.DailyLimit != 1 ||
                !TryGetCurrency(id, out var currency)) return RewardStatus.InvalidReward;
            try { LocalProgression.Ensure(); }
            catch { return RewardStatus.NotReady; }
            var request = new Claim(offer, currency);
            if (!IsCurrentAccount(request)) return RewardStatus.NotReady;
            if (request.Amount <= 0) return RewardStatus.InvalidReward;
            if (IsClaimed(id, request.Day)) return RewardStatus.NothingAvailable;
            claim = request;
            return RewardStatus.Success;
        }

        public static bool IsCurrentAccount(Claim claim)
        {
            if (claim == null || !LocalProgression.IsReady) return false;
            try { LocalProgression.Ensure(); }
            catch { return false; }
            if (claim.AccountKey != LocalProgression.AccountKey ||
                claim.AccountGeneration != LocalProgression.AccountGeneration ||
                claim.NetworkGeneration != (NetworkManager.Instance?.AccountGeneration ?? -1)) return false;
            return NetworkManager.Instance != null && NetworkManager.Instance.IsGameDataReady;
        }

        public static RewardResult FinishAd(Claim claim, RewardedAdResult result)
        {
            if (!IsCurrentAccount(claim)) return new RewardResult(RewardStatus.Stale);
            if (result != RewardedAdResult.Completed) return new RewardResult(RewardStatus.NothingAvailable);
            claim.Earned = true;
            return TryGrant(claim);
        }

        public static RewardResult TryGrant(Claim claim)
        {
            if (!IsCurrentAccount(claim)) return new RewardResult(RewardStatus.Stale);
            if (!claim.Earned) return new RewardResult(RewardStatus.InvalidReward);
            if (IsClaimed(claim.OfferId, claim.Day)) return new RewardResult(RewardStatus.NothingAvailable);
            var reward = new RewardGrant(claim.Currency, claim.Amount);
            var status = RewardStatus.SaveFailed;
            bool saved = LocalProgression.Execute("shop-ad-reward", state =>
            {
                if (!IsCurrentAccount(claim)) { status = RewardStatus.Stale; return false; }
                if (!RewardGrants.TryApply(state, new[] { reward }, out _))
                { status = RewardStatus.InvalidReward; return false; }
                return true;
            }, claim.ClaimId);
            return new RewardResult(saved ? RewardStatus.Success : status, saved ? new[] { reward } : null);
        }

#if UNITY_EDITOR
        internal static RewardStatus ResetTodayForTesting()
        {
            if (!LocalProgression.IsReady) return RewardStatus.NotReady;
            try { LocalProgression.Ensure(); }
            catch { return RewardStatus.NotReady; }
            var account = new Claim(ShopCatalog.Find("rewarded.daily_equipment"), eCurrency.EquipmentTicket);
            if (!IsCurrentAccount(account)) return RewardStatus.NotReady;
            var failure = RewardStatus.SaveFailed;
            bool saved = LocalProgression.Execute("test-shop-ad-reset", state =>
            {
                if (!IsCurrentAccount(account)) { failure = RewardStatus.Stale; return false; }
                state.Claims.Remove(Key("rewarded.daily_equipment", account.Day));
                state.Claims.Remove(Key("rewarded.daily_mage", account.Day));
                return true;
            });
            return saved ? RewardStatus.Success : failure;
        }
#endif

    }
}
