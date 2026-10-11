using System;

namespace Scripts.Server
{
    /// <summary>Session-scoped sequence; sending and failure never advance it.</summary>
    public sealed class HuntRewardSequence
    {
        public string SessionID { get; private set; }
        public long Next { get; private set; }
        public bool IsPending { get; private set; }
        public bool NeedsRecovery { get; private set; }
        private long _sent;

        public void Reset() { SessionID = null; Next = 0; _sent = 0; IsPending = false; NeedsRecovery = false; }
        public void SetSession(string session, Func<int> seed)
        {
            if (string.IsNullOrWhiteSpace(session)) throw new ArgumentException("Empty session.");
            if (SessionID == session) return;
            int first = seed();
            if (first < 1000 || first > 50000) throw new ArgumentOutOfRangeException(nameof(seed));
            Reset(); SessionID = session; Next = first;
        }
        public long Begin()
        {
            if (SessionID == null || IsPending || NeedsRecovery || Next == long.MaxValue)
                throw new InvalidOperationException("Hunt sequence is not ready.");
            IsPending = true; _sent = Next; return _sent;
        }
        public void ValidateNext(long next)
        {
            if (!IsPending || next != checked(_sent + 1))
                throw new InvalidOperationException("Unexpected NextRewardSequence.");
        }
        public void Acknowledge(long next)
        { ValidateNext(next); Next = next; IsPending = false; }
        // Recovery awaits the S06 contract. Keep the sequence and never invent another seed.
        public void Fail() { IsPending = false; NeedsRecovery = true; }
    }
}
