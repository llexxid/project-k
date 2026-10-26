using System;
using System.Collections.Generic;
using System.Linq;
using KingdomIdle.Balance;
using KingdomIdle.Rewards;

namespace KingdomIdle.Mailbox
{
    /// <summary>우편 보관·만료·수령을 기존 로컬 거래로 처리한다.</summary>
    public sealed class MailboxService
    {
        // 우편 행이 한국시간으로 표시할 수 있는 날짜까지만 저장한다.
        private static readonly long MaxMailUtc = new DateTimeOffset(9999, 12, 31, 23, 59, 59, TimeSpan.FromHours(9)).ToUnixTimeSeconds();
        public MailboxSnapshot GetSnapshot()
        {
            if (!LocalProgression.TryGetCommittedState(out var state)) return null;
            long now = RewardGrants.Now;
            return new MailboxSnapshot { AccountGeneration = LocalProgression.AccountGeneration,
                Messages = state.Mail.Where(m => m.ExpiresUtc > now).Select(m => m.Copy()).ToArray() };
        }

        public bool Refresh()
        {
            if (!LocalProgression.TryGetCommittedState(out var state)) return false;
            long now = RewardGrants.Now;
            if (!state.Mail.Any(m => m.ExpiresUtc <= now)) return true;
            return LocalProgression.Execute("mail-expire", draft => { draft.Mail.RemoveAll(m => m.ExpiresUtc <= now); draft.RewardsRevision++; return true; });
        }

        /// <summary>호출자가 고유 ID와 유효기간을 지정한다. 삭제된 우편도 같은 ID로 재발급하지 않는다.</summary>
        public RewardResult TryAddMail(MailMessage mail)
        {
            if (!LocalProgression.TryGetCommittedState(out var state)) return new(RewardStatus.NotReady);
            if (!IsValid(mail) || mail.Claimed || mail.ExpiresUtc <= RewardGrants.Now) return new(RewardStatus.InvalidReward);
            string key = "mail-issued:" + mail.Id;
            if (state.Claims.Contains(key)) return new(RewardStatus.Stale);
            long account = LocalProgression.AccountGeneration;
            bool saved = LocalProgression.Execute("mail-add", draft =>
            {
                if (account != LocalProgression.AccountGeneration) return false;
                draft.Mail.Add(mail.Copy()); draft.RewardsRevision++; return true;
            }, key);
            return new(saved ? RewardStatus.Success : RewardStatus.SaveFailed);
        }

        public RewardResult TryClaim(MailboxSnapshot snapshot, string id) => Claim(snapshot, new[] { id });
        public RewardResult TryClaimAll(MailboxSnapshot snapshot) => Claim(snapshot,
            snapshot?.Messages.Where(m => !m.Claimed).Select(m => m.Id).ToArray());

        private RewardResult Claim(MailboxSnapshot snapshot, string[] ids)
        {
            if (!LocalProgression.IsReady) return new(RewardStatus.NotReady);
            if (snapshot == null || snapshot.AccountGeneration != LocalProgression.AccountGeneration) return new(RewardStatus.Stale);
            if (ids == null || ids.Length == 0) return new(RewardStatus.NothingAvailable);
            // 만료 삭제는 지급 취소에 의해 되돌아가지 않는 독립 거래다.
            if (!Refresh()) return new(RewardStatus.SaveFailed);
            long now = RewardGrants.Now;
            var failure = RewardStatus.SaveFailed;
            List<RewardGrant> granted = null;
            bool saved = LocalProgression.Execute("mail-claim", draft =>
            {
                if (snapshot.AccountGeneration != LocalProgression.AccountGeneration || ids.Distinct().Count() != ids.Length)
                { failure = RewardStatus.Stale; return false; }
                var targets = new List<MailMessage>();
                foreach (string id in ids)
                {
                    if (!snapshot.Messages.Any(m => m.Id == id && !m.Claimed))
                    { failure = RewardStatus.Stale; return false; }
                    var mail = draft.Mail.Find(m => m.Id == id);
                    if (mail == null || mail.Claimed || mail.ExpiresUtc <= now)
                    { failure = RewardStatus.Stale; return false; }
                    targets.Add(mail);
                }
                if (!RewardGrants.TryApply(draft, targets.SelectMany(m => m.Rewards), out granted))
                { failure = RewardStatus.InvalidReward; return false; }
                foreach (var mail in targets) mail.ClaimedAtUtc = now;
                draft.RewardsRevision++;
                return true;
            });
            return new(saved ? RewardStatus.Success : failure, saved ? granted : null);
        }

        public RewardResult DeleteClaimed(MailboxSnapshot snapshot)
        {
            if (!LocalProgression.IsReady) return new(RewardStatus.NotReady);
            if (snapshot == null || snapshot.AccountGeneration != LocalProgression.AccountGeneration) return new(RewardStatus.Stale);
            var ids = snapshot.Messages.Where(m => m.Claimed).Select(m => m.Id).ToHashSet();
            if (ids.Count == 0) return new(RewardStatus.NothingAvailable);
            var failure = RewardStatus.SaveFailed;
            bool saved = LocalProgression.Execute("mail-delete-claimed", draft =>
            {
                if (snapshot.AccountGeneration != LocalProgression.AccountGeneration ||
                    ids.Any(id => !draft.Mail.Any(m => m.Id == id && m.Claimed)))
                { failure = RewardStatus.Stale; return false; }
                draft.Mail.RemoveAll(m => ids.Contains(m.Id));
                draft.RewardsRevision++;
                return true;
            });
            return new(saved ? RewardStatus.Success : failure);
        }

        internal static bool IsValid(MailMessage mail) => mail != null && !string.IsNullOrWhiteSpace(mail.Id) &&
            !string.IsNullOrWhiteSpace(mail.Title) && mail.CreatedUtc > 0 && mail.ExpiresUtc > mail.CreatedUtc &&
            mail.ExpiresUtc <= MaxMailUtc && mail.ClaimedAtUtc >= 0 && mail.ClaimedAtUtc <= MaxMailUtc &&
            mail.Rewards != null && mail.Rewards.Count > 0 && mail.Rewards.All(RewardGrants.IsValid);
    }
}
