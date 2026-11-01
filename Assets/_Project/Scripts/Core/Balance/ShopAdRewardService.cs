using System;
using KingdomIdle.Ads;
using KingdomIdle.Rewards;
using PlayFab;
using Scripts.Core;
using Scripts.Core.Manager;
using UnityEngine;
using Dto = Protocol.Message.Dto;

namespace KingdomIdle.Balance
{
    /// <summary>
    /// 상점 일일 광고 보상(2026-11-01 서버 연결). 광고를 다 보면 OnClaimAdReward(AdType=ShopDaily)를 보내고 응답의 재화 절대값을 플레이어 상태에 쓴다.
    /// 상품별 KST 하루 1회 판정과 지급은 서버가 한다. 광고 시청 여부는 서버가 클라이언트 보고를 믿는다.
    /// 오늘 받은 상품 기록(AdRewardStateDTO)은 플레이어 상태 파일이 아니라 메모리에 두고 로그인·OnGetUserState·수령 응답이 교체한다.
    /// </summary>
    public static class ShopAdRewardService
    {
        public sealed class Claim
        {
            public string OfferId { get; }
            public string Day { get; }
            public bool Earned { get; internal set; }
            internal readonly string AccountKey;
            internal readonly long AccountGeneration, NetworkGeneration, Amount;
            internal readonly eCurrency Currency;

            internal Claim(ShopOffer offer, eCurrency currency)
            {
                OfferId = offer.Id; Day = Today;
                Currency = currency;
                Amount = currency == eCurrency.EquipmentTicket ? offer.EquipmentTickets : offer.MageTickets;
                AccountKey = LocalProgression.AccountKey;
                AccountGeneration = LocalProgression.AccountGeneration;
                NetworkGeneration = NetworkManager.Instance?.AccountGeneration ?? -1;
            }
        }

        static Dto.AdRewardStateDTO _server;
        // 서버 시각 − 로컬 시각(로그인 응답의 ServerTimeUtc 기준). 오늘(KST)은 이 추정 서버 시각으로 본다.
        static long _serverOffset;
        public static int Revision { get; private set; }
        public static bool IsBusy { get; private set; }
        /// <summary>수령 요청의 결과. 메시지가 있으면 결과 상태 대신 그 문구를 보여 준다.</summary>
        public static event Action<Claim, RewardResult, string> ClaimCompleted;

        /// <summary>추정 서버 시각 기준 오늘(KST). 서버는 관측한 최대 시각으로 판정하므로 자정 부근에서는 다를 수 있다. 표시·안내용이다.</summary>
        public static string Today => QuestPeriod.At(LocalProgression.UtcNow + _serverOffset).Day;

        /// <summary>로그인·OnGetUserState 응답의 수령 기록을 받는다(UserManager가 호출).</summary>
        public static void Receive(Dto.AdRewardStateDTO state, long serverTimeUtc, string source)
        {
            if (state == null) return;
            if (serverTimeUtc > 0) _serverOffset = serverTimeUtc - LocalProgression.UtcNow;
            _server = state;
            Revision++;
            Debug.Log($"[AdReward] receive source={source} ads={Describe(state)} serverTime={serverTimeUtc}");
        }

        public static string Describe() => Describe(_server);
        public static string Describe(Dto.AdRewardStateDTO s) => s == null ? "-" :
            $"{(string.IsNullOrEmpty(s.Day) ? "none" : s.Day)}/{s.ClaimedOfferIds?.Count ?? 0}";

        /// <summary>서버 수령 기록으로 그날 받았는지 본다(안내용). 최종 판정은 서버다.</summary>
        public static bool IsClaimed(string id, string day) =>
            _server != null && _server.Day == day && _server.ClaimedOfferIds != null && _server.ClaimedOfferIds.Contains(id);

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
            if (!IsCurrentAccount(request) || IsBusy) return RewardStatus.NotReady;
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

        /// <summary>
        /// 광고를 다 본 클레임을 서버에 보낸다. 결과는 ClaimCompleted로 온다(Pending).
        /// 서버는 같은 날 같은 상품을 다시 받아도 AlreadyClaimed로 지급하지 않으므로, 저장·전송 실패 뒤 "보상 다시 받기"는 광고 없이 이 함수를 다시 부른다.
        /// </summary>
        public static RewardResult TryGrant(Claim claim)
        {
            if (!IsCurrentAccount(claim)) return new RewardResult(RewardStatus.Stale);
            if (!claim.Earned) return new RewardResult(RewardStatus.InvalidReward);
            var network = NetworkManager.Instance;
            if (IsBusy || network == null || !network.IsGameDataReady) return new RewardResult(RewardStatus.NotReady);
            IsBusy = true;
            network.Call<Dto.OnClaimAdRewardRequestDTO, Dto.OnClaimAdRewardResponseDTO>(
                new Dto.OnClaimAdRewardRequestDTO { AdType = Protocol.Message.Data.eAdType.ShopDaily, OfferId = claim.OfferId },
                response => ApplyServerResult(claim, response),
                error => RejectServerResult(claim, error));
            return new RewardResult(RewardStatus.Pending);
        }

        static void ApplyServerResult(Claim claim, Dto.OnClaimAdRewardResponseDTO r)
        {
            if (!IsCurrentAccount(claim)) { IsBusy = false; ClaimCompleted?.Invoke(claim, new RewardResult(RewardStatus.Stale), null); return; }
            bool saved = LocalProgression.Execute("server-ad-reward", s => { UserManager.ApplyAdReward(s, r); return true; });
            _server = r.AdRewards;
            Revision++;
            IsBusy = false;
            long granted = Granted(r, claim.Currency);
            Debug.Log($"[AdReward] claim offer={claim.OfferId} type={r.AdType} already={r.AlreadyClaimed} granted={granted} ads={Describe(r.AdRewards)}" +
                $" | server EquipmentTicket={r.Currency.EquipmentTicket} MageTicket={r.Currency.MageTicket}" +
                $" | state EquipmentTicket={LocalProgression.Balance(eCurrency.EquipmentTicket)} MageTicket={LocalProgression.Balance(eCurrency.MageTicket)} | saved={saved}");
            if (!saved)
            {
                global::Direction.GameDirectInteraction.ReportSaveFailure();
                ClaimCompleted?.Invoke(claim, new RewardResult(RewardStatus.SaveFailed), null);
                return;
            }
            if (r.AlreadyClaimed)
            {
                // 오늘 이미 받았다(응답 유실 뒤 재요청 포함). 보유 수량은 서버 값으로 맞췄다.
                ClaimCompleted?.Invoke(claim, new RewardResult(RewardStatus.NothingAvailable), "오늘 이미 받은 보상입니다.");
                return;
            }
            var grants = new System.Collections.Generic.List<RewardGrant>();
            foreach (var g in r.GrantedCurrencies)
            {
                if (g != null && g.Amount > 0) grants.Add(new RewardGrant((eCurrency)(int)g.CurrencyType, g.Amount));
            }
            ClaimCompleted?.Invoke(claim, new RewardResult(RewardStatus.Success, grants.ToArray()), null);
        }

        /// <summary>광고 응답의 지급 목록에서 한 재화의 지급량 합.</summary>
        internal static long Granted(Dto.OnClaimAdRewardResponseDTO r, eCurrency currency)
        {
            long sum = 0;
            if (r?.GrantedCurrencies == null) return 0;
            foreach (var g in r.GrantedCurrencies)
            {
                if (g != null && (int)g.CurrencyType == (int)currency) sum += g.Amount;
            }
            return sum;
        }

        /// <summary>광고 응답(오프라인 2배 포함)이 담아 온 오늘 수령 기록을 받는다.</summary>
        internal static void ReceiveClaimRecord(Dto.AdRewardStateDTO state)
        {
            if (state == null) return;
            _server = state;
            Revision++;
        }

        static void RejectServerResult(Claim claim, PlayFabError error)
        {
            string kind = NetworkManager.GetFailureKind(error);
            int code = NetworkManager.TryGetBusinessError(error, out var business) ? (int)business.ErrorCode : 0;
            Debug.LogWarning($"[AdReward] claim offer={claim.OfferId} rejected code={code} kind={kind} message={error?.ErrorMessage}");
            if (kind == "Timeout" || kind == "Transport")
            {
                // 응답 유실: 서버가 지급했을 수 있다. 재전송 대신 상태를 다시 받는다. 받은 것으로 나오면 끝, 아니면 클레임을 남겨 "보상 다시 받기"로 다시 보낸다.
                var users = UserManager.Instance;
                if (users == null)
                {
                    IsBusy = false;
                    ClaimCompleted?.Invoke(claim, new RewardResult(RewardStatus.SaveFailed), NetworkManager.DescribeFailure(error, "광고 보상"));
                    return;
                }
                users.RefreshFromServer(ok =>
                {
                    IsBusy = false;
                    if (ok && IsClaimed(claim.OfferId, Today))
                        ClaimCompleted?.Invoke(claim, new RewardResult(RewardStatus.NothingAvailable), "보상을 서버에서 다시 불러왔습니다.");
                    else
                        ClaimCompleted?.Invoke(claim, new RewardResult(RewardStatus.SaveFailed), NetworkManager.DescribeFailure(error, "광고 보상"));
                });
                return;
            }
            // 거절(Business 등): 서버는 저장하지 않았다. 클레임은 버린다.
            IsBusy = false;
            ClaimCompleted?.Invoke(claim, new RewardResult(RewardStatus.InvalidReward), NetworkManager.DescribeFailure(error, "광고 보상"));
        }

#if UNITY_EDITOR
        // 에디터 QA ShopAdRewardAcceptance가 컴파일되도록 남긴 스텁. 수령 기록은 서버가 가지므로 로컬에서 초기화하지 않는다(그 QA는 무효).
        internal static RewardStatus ResetTodayForTesting() => RewardStatus.NotReady;
#endif
    }
}
