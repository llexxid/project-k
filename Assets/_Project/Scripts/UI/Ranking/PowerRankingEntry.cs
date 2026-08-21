namespace KingdomIdle.UGUI
{
    /// <summary>전투력 랭킹 한 행의 표시 데이터.</summary>
    public sealed class PowerRankingEntry
    {
        public string Id { get; }
        public string DisplayName { get; }
        public long Power { get; }
        public bool IsCurrentPlayer { get; }
        public int Rank { get; internal set; }

        public PowerRankingEntry(string id, string displayName, long power, bool isCurrentPlayer)
        {
            Id = id;
            DisplayName = displayName;
            Power = power;
            IsCurrentPlayer = isCurrentPlayer;
        }
    }
}
