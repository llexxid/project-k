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

        internal static void ValidateList(OnGetMailListResponseDTO dto)
        {
            Require(dto != null && dto.StartIndex >= 0 && dto.StartIndex <= 100 && dto.TotalCount >= 0 && dto.TotalCount <= 100 &&
                dto.ServerTimeUtc > 0 && dto.ServerTimeUtc <= MaxUtc && dto.Mails != null && dto.Mails.Count <= 50);
            Require(dto.TotalCount == 0 || !string.IsNullOrWhiteSpace(dto.MailboxId));
            foreach (var m in dto.Mails)
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

        internal static void ValidateClaim(OnClaimMailRewardResponseDTO dto)
        {
            Require(dto != null && !string.IsNullOrWhiteSpace(dto.MailboxId) && dto.ClaimedMailIds != null &&
                dto.ClaimedMailIds.Count >= 1 && dto.ClaimedMailIds.Count <= 50 && dto.ClaimedMailIds.All(id => id > 0) &&
                dto.ClaimedMailIds.Distinct().Count() == dto.ClaimedMailIds.Count && dto.RemainingCount >= 0 && dto.RemainingCount <= 100 &&
                dto.Rewards != null && dto.Rewards.Count > 0 && dto.Currency != null);
            var c = dto.Currency;
            Require(c.Gold >= 0 && c.AncientCoin >= 0 && c.ArcaneKnowledge >= 0 && c.ClassFragment >= 0 &&
                c.EquipmentTicket >= 0 && c.MageTicket >= 0 && c.Ruby >= 0 && c.EquipmentStone >= 0 && c.KingdomSupply >= 0 && c.TrainingTome >= 0);
            Require(dto.Equipment != null);
            NetworkManager.ValidateEquipment(dto.Equipment);
            foreach (var reward in dto.Rewards)
            {
                Require(reward != null && reward.Amount > 0 && reward.Order >= 0 && reward.GrantedEquipmentStone >= 0);
                ValidateKind(reward.Kind, reward.CurrencyType, reward.DefinitionId);
                Require(Enum.IsDefined(typeof(EquipmentDisposition), reward.Disposition));
                if (reward.ItemCode.HasValue)
                    Require((reward.ItemCode.Value.Code >> 56) == 0 && reward.ItemCode.Value.GetItemCode() == (ulong)(reward.DefinitionId ?? 0));
                if (reward.Kind == RewardKind.Currency) Require(reward.Disposition == EquipmentDisposition.None && !reward.ItemCode.HasValue && reward.GrantedEquipmentStone == 0);
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
