using System;
using System.Linq;
using Newtonsoft.Json;
using Protocol.Message.Dto;
using Scripts.Core.Manager;

namespace KingdomIdle.Mailbox
{
    internal static class MailboxProtocol
    {
        private const long MaxUtc = 253402268399L;
        private static void Require(bool condition) { if (!condition) throw new JsonSerializationException("Invalid mailbox response."); }

        internal static void ValidateList(OnGetMailListResponseDTO r)
        {
            Require(r != null && r.StartIndex >= 0 && r.TotalCount >= 0 && r.TotalCount <= 100 &&
                r.ServerTimeUtc > 0 && r.ServerTimeUtc <= MaxUtc && r.Mails != null && r.Mails.Count <= 50);
            Require(r.TotalCount == 0 || !string.IsNullOrWhiteSpace(r.MailboxId));
            foreach (var m in r.Mails)
            {
                Require(m != null && m.MailId > 0 && Enum.IsDefined(typeof(MailCategory), m.Category) &&
                    m.TitleKey != null && m.BodyKey != null && m.SentUtc > 0 && m.ExpiresUtc > m.SentUtc &&
                    m.ExpiresUtc <= MaxUtc && m.Rewards != null && m.Rewards.Count >= 1 && m.Rewards.Count <= 10);
                foreach (var reward in m.Rewards)
                {
                    Require(reward != null && reward.Amount > 0);
                    ValidateKind(reward.Kind, reward.CurrencyType, reward.DefinitionId);
                    Require(reward.Kind != RewardKind.Equipment || reward.Amount <= 100);
                }
            }
        }

        internal static void ValidateClaim(OnClaimMailRewardResponseDTO r)
        {
            Require(r != null && !string.IsNullOrWhiteSpace(r.MailboxId) && r.ClaimedMailIds != null &&
                r.ClaimedMailIds.Count >= 1 && r.ClaimedMailIds.Count <= 50 && r.ClaimedMailIds.All(id => id > 0) &&
                r.ClaimedMailIds.Distinct().Count() == r.ClaimedMailIds.Count && r.RemainingCount >= 0 && r.RemainingCount <= 100 &&
                r.Rewards != null && r.Rewards.Count > 0 && r.Currency != null);
            var c = r.Currency;
            Require(c.Gold >= 0 && c.AncientCoin >= 0 && c.ArcaneKnowledge >= 0 && c.ClassFragment >= 0 &&
                c.Ruby >= 0 && c.EquipmentStone >= 0 && c.KingdomSupply >= 0 && c.TrainingTome >= 0);
            NetworkManager.ValidateEquipment(r.Equipment);
            foreach (var reward in r.Rewards)
            {
                Require(reward != null && reward.Amount > 0 && reward.Order >= 0 && reward.GrantedEquipmentStone >= 0);
                ValidateKind(reward.Kind, reward.CurrencyType, reward.DefinitionId);
                Require(Enum.IsDefined(typeof(EquipmentDisposition), reward.Disposition));
                if (reward.Kind == RewardKind.Currency) Require(reward.Disposition == EquipmentDisposition.None);
                else Require(reward.Disposition != EquipmentDisposition.None &&
                    (reward.Disposition == EquipmentDisposition.AutoDismantled || reward.ItemCode.HasValue));
            }
        }

        private static void ValidateKind(RewardKind kind, Protocol.Message.Data.eCurrency? currency, int? definition)
        {
            Require(kind == RewardKind.Currency
                ? currency.HasValue && Enum.IsDefined(typeof(Protocol.Message.Data.eCurrency), currency.Value) && definition == null
                : kind == RewardKind.Equipment && definition > 0 && currency == null);
        }
    }
}
