using System;
using System.Collections.Generic;
using System.Linq;
using KingdomIdle.Rewards;

namespace KingdomIdle.Mailbox
{
    [Serializable]
    public sealed class MailMessage
    {
        public string Id, Title, Sender, Body;
        public long CreatedUtc, ExpiresUtc, ClaimedAtUtc;
        public List<RewardGrant> Rewards = new();
        public bool Claimed => ClaimedAtUtc != 0;
        public MailMessage Copy() => new()
        {
            Id = Id, Title = Title, Sender = Sender, Body = Body,
            CreatedUtc = CreatedUtc, ExpiresUtc = ExpiresUtc, ClaimedAtUtc = ClaimedAtUtc,
            Rewards = RewardGrants.Copy(Rewards)
        };
        // Legacy files still contain local mail while the live mailbox comes from the server.
        internal static bool IsValid(MailMessage mail) => mail != null && !string.IsNullOrWhiteSpace(mail.Id) &&
            !string.IsNullOrWhiteSpace(mail.Title) && mail.CreatedUtc > 0 && mail.ExpiresUtc > mail.CreatedUtc &&
            mail.ExpiresUtc <= 253402268399L && mail.ClaimedAtUtc >= 0 && mail.ClaimedAtUtc <= 253402268399L &&
            mail.Rewards != null && mail.Rewards.Count > 0 && mail.Rewards.All(RewardGrants.IsValid);
    }

    public sealed class LocalMailboxSnapshot
    {
        public long AccountGeneration { get; internal set; }
        public IReadOnlyList<MailMessage> Messages { get; internal set; }
    }
}
