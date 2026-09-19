using System;
using System.Collections.Generic;

namespace KingdomIdle.Balance
{
    /// <summary>Identifies one account's reward in one period; an old screen cannot claim a new period.</summary>
    public readonly struct QuestClaimToken : IEquatable<QuestClaimToken>
    {
        public long AccountGeneration { get; }
        public long QuestId { get; }
        public string Period { get; }

        public QuestClaimToken(long accountGeneration, long questId, string period)
        { AccountGeneration = accountGeneration; QuestId = questId; Period = period ?? string.Empty; }

        public bool Equals(QuestClaimToken other) => AccountGeneration == other.AccountGeneration &&
            QuestId == other.QuestId && string.Equals(Period, other.Period, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is QuestClaimToken other && Equals(other);
        public override int GetHashCode()
        {
            unchecked { return ((AccountGeneration.GetHashCode() * 397) ^ QuestId.GetHashCode()) * 397 ^ (Period == null ? 0 : StringComparer.Ordinal.GetHashCode(Period)); }
        }
        public static bool operator ==(QuestClaimToken left, QuestClaimToken right) => left.Equals(right);
        public static bool operator !=(QuestClaimToken left, QuestClaimToken right) => !left.Equals(right);
    }

    public enum QuestClaimStatus
    {
        Success, NotReady, StaleAccount, Locked, Incomplete, AlreadyClaimed, Expired, SaveFailed, UnknownQuest
    }

    public readonly struct QuestClaimResult
    {
        public QuestClaimStatus Status { get; }
        public QuestClaimToken Token { get; }
        public bool Succeeded => Status == QuestClaimStatus.Success;
        public QuestClaimResult(QuestClaimStatus status, QuestClaimToken token) { Status = status; Token = token; }
    }

    public enum QuestRowState { Locked, InProgress, Claimable, Claimed }

    /// <summary>Reward values are copied from the committed state, never a writable wallet reference.</summary>
    public sealed class QuestRewardSnapshot
    {
        public eCurrency Currency { get; }
        public decimal Amount { get; }
        public bool IsDynamic { get; }
        public QuestRewardSnapshot(eCurrency currency, decimal amount, bool isDynamic = false)
        { Currency = currency; Amount = amount; IsDynamic = isDynamic; }
    }

    public sealed class QuestRowSnapshot
    {
        public QuestClaimToken Token { get; }
        public eQuestCategory Category { get; }
        public string Title { get; }
        public string Description { get; }
        public eQuestObjectiveType ObjectiveType { get; }
        public long TargetId { get; }
        public int RequiredCount { get; }
        public long Progress { get; }
        public QuestRowState State { get; }
        public bool IsPending { get; }
        public long ExpiresUtc { get; }
        public eQuestPresentationType PresentationType { get; }
        public IReadOnlyList<QuestRewardSnapshot> Rewards { get; }
        public bool CanClaim => State == QuestRowState.Claimable;
        public bool IsCompleted => State == QuestRowState.Claimable || State == QuestRowState.Claimed;

        public QuestRowSnapshot(QuestClaimToken token, eQuestCategory category, string title, string description,
            eQuestObjectiveType objectiveType, long targetId, int requiredCount, long progress, QuestRowState state,
            bool isPending, long expiresUtc, IEnumerable<QuestRewardSnapshot> rewards,
            eQuestPresentationType presentationType = eQuestPresentationType.None)
        {
            Token = token; Category = category; Title = title ?? string.Empty; Description = description ?? string.Empty;
            ObjectiveType = objectiveType; TargetId = targetId; RequiredCount = requiredCount; Progress = progress;
            State = state; IsPending = isPending; ExpiresUtc = expiresUtc; PresentationType = presentationType;
            Rewards = new List<QuestRewardSnapshot>(rewards ?? Array.Empty<QuestRewardSnapshot>()).AsReadOnly();
        }

        /// <summary>Revision-only commits do not invalidate an unchanged quest row.</summary>
        public bool ContentEquals(QuestRowSnapshot other)
        {
            if (other == null || Token != other.Token || Category != other.Category || Title != other.Title ||
                Description != other.Description || ObjectiveType != other.ObjectiveType || TargetId != other.TargetId ||
                RequiredCount != other.RequiredCount || Progress != other.Progress || State != other.State ||
                IsPending != other.IsPending || ExpiresUtc != other.ExpiresUtc || PresentationType != other.PresentationType ||
                Rewards.Count != other.Rewards.Count) return false;
            for (int i = 0; i < Rewards.Count; i++)
                if (Rewards[i].Currency != other.Rewards[i].Currency || Rewards[i].Amount != other.Rewards[i].Amount ||
                    Rewards[i].IsDynamic != other.Rewards[i].IsDynamic) return false;
            return true;
        }
    }

    public sealed class QuestBoardSnapshot
    {
        public long AccountGeneration { get; }
        public long Revision { get; }
        public eQuestCategory Category { get; }
        public string Period { get; }
        public long ResetUtc { get; }
        public bool IsReady { get; }
        public IReadOnlyList<QuestRowSnapshot> Rows { get; }

        public QuestBoardSnapshot(long accountGeneration, long revision, eQuestCategory category, string period,
            long resetUtc, IEnumerable<QuestRowSnapshot> rows, bool isReady = true)
        {
            AccountGeneration = accountGeneration; Revision = revision; Category = category; Period = period ?? string.Empty;
            ResetUtc = resetUtc; IsReady = isReady;
            Rows = new List<QuestRowSnapshot>(rows ?? Array.Empty<QuestRowSnapshot>()).AsReadOnly();
        }

        public static QuestBoardSnapshot NotReady(eQuestCategory category, long generation = 0) =>
            new(generation, -1, category, string.Empty, 0, Array.Empty<QuestRowSnapshot>(), false);
    }

    /// <summary>Includes removed tokens as well as added/updated tokens, so a view can remove expired rows.</summary>
    public sealed class QuestChangeSet
    {
        public IReadOnlyList<QuestClaimToken> ChangedTokens { get; }
        public IReadOnlyList<eQuestCategory> ChangedCategories { get; }
        public bool AccountChanged { get; }
        public QuestChangeSet(IEnumerable<QuestClaimToken> changedTokens, IEnumerable<eQuestCategory> changedCategories,
            bool accountChanged = false)
        {
            ChangedTokens = new List<QuestClaimToken>(changedTokens ?? Array.Empty<QuestClaimToken>()).AsReadOnly();
            ChangedCategories = new List<eQuestCategory>(changedCategories ?? Array.Empty<eQuestCategory>()).AsReadOnly();
            AccountChanged = accountChanged;
        }
    }
}
