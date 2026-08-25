using System;
using System.Collections.Generic;

namespace KingdomIdle.UGUI
{
    /// <summary>서버 랭킹 연동 전까지 사용할 고정 더미 랭킹을 만든다.</summary>
    public static class DummyPowerRankingProvider
    {
        private const int DummyCount = 60;
        private const int Seed = 20260811;
        private const int MinPower = 150;
        private const int MaxPowerExclusive = 35211;

        public static IReadOnlyList<PowerRankingEntry> Create(string playerName, long playerPower)
        {
            var random = new Random(Seed);
            var entries = new List<PowerRankingEntry>(DummyCount + 1);

            for (int i = 0; i < DummyCount; i++)
            {
                int number = i + 1;
                entries.Add(new PowerRankingEntry(
                    $"dummy-{number:000}",
                    $"모험가{number:00}",
                    random.Next(MinPower, MaxPowerExclusive),
                    false));
            }

            entries.Add(new PowerRankingEntry(
                "current-player",
                string.IsNullOrWhiteSpace(playerName) ? "Guest" : playerName,
                playerPower,
                true));

            entries.Sort(CompareEntries);
            for (int i = 0; i < entries.Count; i++)
                entries[i].Rank = i + 1;

            return entries;
        }

        private static int CompareEntries(PowerRankingEntry left, PowerRankingEntry right)
        {
            int powerOrder = right.Power.CompareTo(left.Power);
            return powerOrder != 0
                ? powerOrder
                : string.CompareOrdinal(left.Id, right.Id);
        }
    }
}
