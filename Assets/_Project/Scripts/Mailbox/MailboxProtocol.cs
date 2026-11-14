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
        private const long OverflowIdBase = 1L << 48;
        private const long MaxExactId = (1L << 53) - 1;
        private static void Require(bool condition) { if (!condition) throw new JsonSerializationException("Invalid mailbox response."); }

        internal static void ValidateList(OnGetMailListResponseDTO dto)
        {
            bool v2 = EquipmentManager.IsInstanceInventory;
            int maximum = v2 ? int.MaxValue : 100;
            Require(dto != null && dto.StartIndex >= 0 && dto.StartIndex <= maximum && dto.TotalCount >= 0 && dto.TotalCount <= maximum &&
                dto.ServerTimeUtc > 0 && dto.ServerTimeUtc <= MaxUtc && dto.Mails != null && dto.Mails.Count <= 50);
            Require(dto.TotalCount == 0 || !string.IsNullOrWhiteSpace(dto.MailboxId));
            if (v2)
                Require(dto.MailboxId?.Length == 32 && dto.LatestRegularMailId is long regular && regular >= 0 &&
                    regular < OverflowIdBase && dto.LatestOverflowMailId is long overflow &&
                    (overflow == 0 || overflow > OverflowIdBase && overflow <= MaxExactId) &&
                    (dto.RegularMailboxId == null || dto.RegularMailboxId.Length == 32) && (regular == 0 || dto.RegularMailboxId != null) &&
                    (dto.TotalCount == 0) == (regular == 0 && overflow == 0));
            else Require(!dto.LatestRegularMailId.HasValue && !dto.LatestOverflowMailId.HasValue && dto.RegularMailboxId == null);
            foreach (var m in dto.Mails)
            {
                Require(m != null && m.MailId > 0 && Enum.IsDefined(typeof(MailCategory), m.Category) &&
                    m.SentUtc > 0 && m.SentUtc <= MaxUtc &&
                    (m.IsEquipmentOverflow ? v2 && m.ExpiresUtc == 0 && m.MailId > OverflowIdBase && m.MailId <= dto.LatestOverflowMailId :
                        m.ExpiresUtc > m.SentUtc && (!v2 || m.MailId <= dto.LatestRegularMailId)) &&
                    m.ExpiresUtc <= MaxUtc && m.Rewards != null && m.Rewards.Count >= 1 && m.Rewards.Count <= 10);
                Require(m.TextMode == MailTextMode.Key
                    ? !string.IsNullOrWhiteSpace(m.TitleKey) && m.TitleKey.Length <= 100 &&
                      !string.IsNullOrWhiteSpace(m.BodyKey) && m.BodyKey.Length <= 100 &&
                      m.Title == null && m.Body == null && m.ResolvedLocale == null
                    : m.TextMode == MailTextMode.Text && m.TitleKey == null && m.BodyKey == null &&
                      !string.IsNullOrWhiteSpace(m.Title) && System.Text.Encoding.UTF8.GetByteCount(m.Title) <= 256 &&
                      !string.IsNullOrWhiteSpace(m.Body) && System.Text.Encoding.UTF8.GetByteCount(m.Body) <= 2048 && ValidLocale(m.ResolvedLocale));
                foreach (var reward in m.Rewards)
                {
                    Require(reward != null && reward.Amount > 0);
                    ValidateKind(reward.Kind, reward.CurrencyType, reward.DefinitionId);
                    Require(reward.Kind != RewardKind.Equipment || reward.Amount <= 100);
                }
                if (m.IsEquipmentOverflow)
                    Require(m.Rewards.All(reward => reward.Kind == RewardKind.Equipment) && RequiredEquipmentSlots(m) <= 10);
            }
        }

        internal static void ValidateClaim(OnClaimMailRewardResponseDTO dto)
        {
            Require(dto != null && !string.IsNullOrWhiteSpace(dto.MailboxId) && dto.ClaimedMailIds != null &&
                dto.ClaimedMailIds.Count >= 1 && dto.ClaimedMailIds.Count <= 50 && dto.ClaimedMailIds.All(id => id > 0) &&
                dto.ClaimedMailIds.Distinct().Count() == dto.ClaimedMailIds.Count && dto.RemainingCount >= 0 &&
                (dto.Equipment?.SchemaVersion == 2 || dto.RemainingCount <= 100) &&
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

        internal static bool IsAvailable(MailDTO mail, long now) => mail.IsEquipmentOverflow || mail.ExpiresUtc > now;

        internal static int RequiredEquipmentSlots(MailDTO mail) => mail.IsEquipmentOverflow
            ? (int)mail.Rewards.Sum(reward => reward.Amount)
            : 0;

        private static bool ValidLocale(string locale) => locale != null && locale.Length >= 2 && locale.Length <= 24 &&
            System.Text.RegularExpressions.Regex.IsMatch(locale, "^[A-Za-z][A-Za-z0-9]*(?:-[A-Za-z0-9]+)*\\z");

        private static void ValidateKind(RewardKind kind, Protocol.Message.Data.eCurrency? currency, int? definition)
        {
            Require(kind == RewardKind.Currency
                ? currency.HasValue && Enum.IsDefined(typeof(Protocol.Message.Data.eCurrency), currency.Value) && definition == null
                : kind == RewardKind.Equipment && definition > 0 && currency == null);
        }
    }
}
