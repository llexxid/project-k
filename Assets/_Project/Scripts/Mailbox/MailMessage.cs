using System;
using System.Collections.Generic;
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
    }

    public sealed class MailboxSnapshot
    {
        public long AccountGeneration { get; internal set; }
        public IReadOnlyList<MailMessage> Messages { get; internal set; }
    }
}
