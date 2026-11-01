#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using KingdomIdle.Balance;
using KingdomIdle.Rewards;

namespace KingdomIdle.Mailbox.Testing
{
    /// <summary>우편 보관·만료·수령을 기존 로컬 거래로 처리한다.</summary>
    public sealed class LocalMailboxService
    {
        private static void RequireTestAccount()
        {
            if (!LocalProgression.IsTestAccount) throw new InvalidOperationException("로컬 우편 지급은 격리 테스트 계정에서만 사용할 수 있습니다.");
        }
        public LocalMailboxSnapshot GetSnapshot()
        {
            RequireTestAccount();
            if (!LocalProgression.TryGetCommittedState(out var state)) return null;
            long now = RewardGrants.Now;
            return new LocalMailboxSnapshot { AccountGeneration = LocalProgression.AccountGeneration,
                Messages = state.Mail.Where(m => m.ExpiresUtc > now).Select(m => m.Copy()).ToArray() };
        }

        public bool Refresh()
        {
            RequireTestAccount();
            if (!LocalProgression.TryGetCommittedState(out var state)) return false;
            long now = RewardGrants.Now;
            if (!state.Mail.Any(m => m.ExpiresUtc <= now)) return true;
            return LocalProgression.Execute("mail-expire", draft => { draft.Mail.RemoveAll(m => m.ExpiresUtc <= now); draft.RewardsRevision++; return true; });
        }

        /// <summary>호출자가 고유 ID와 유효기간을 지정한다. 삭제된 우편도 같은 ID로 재발급하지 않는다.</summary>
        public RewardResult TryAddMail(MailMessage mail)
        {
            RequireTestAccount();
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

        public RewardResult TryClaim(LocalMailboxSnapshot snapshot, string id) => Claim(snapshot, new[] { id });
        public RewardResult TryClaimAll(LocalMailboxSnapshot snapshot) => Claim(snapshot,
            snapshot?.Messages.Where(m => !m.Claimed).Select(m => m.Id).ToArray());

        private RewardResult Claim(LocalMailboxSnapshot snapshot, string[] ids)
        {
            RequireTestAccount();
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

        public RewardResult DeleteClaimed(LocalMailboxSnapshot snapshot)
        {
            RequireTestAccount();
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

        internal static bool IsValid(MailMessage mail) => MailMessage.IsValid(mail);
    }
    public sealed class LocalMailboxSnapshot
    {
        public long AccountGeneration { get; internal set; }
        public IReadOnlyList<MailMessage> Messages { get; internal set; }
    }
}

#endif
