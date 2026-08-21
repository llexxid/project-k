using System;
using System.Collections.Generic;

namespace KingdomIdle.UGUI
{
    /// <summary>캐릭터와 파티의 V1 전투력을 계산한다.</summary>
    public static class CombatPowerCalculator
    {
        private const int PartySize = 3;

        /// <summary>V1 전투력 = 공격력 x 5 + 최대 체력.</summary>
        public static long CalculateCharacterPowerV1(int attack, int maxHp)
        {
            return (long)attack * 5L + maxHp;
        }

        /// <summary>캐릭터가 아직 생성되지 않았으면 0을 반환한다.</summary>
        public static long CalculateCharacterPowerV1(Player player)
        {
            var status = player != null ? player.playerStatus : null;
            return status == null ? 0L : CalculateCharacterPowerV1(status.Atk, status.MaxHP);
        }

        /// <summary>연결된 앞의 세 캐릭터 전투력을 합산한다.</summary>
        public static long CalculatePartyPowerV1(IReadOnlyList<Player> players)
        {
            if (players == null) return 0L;

            long total = 0L;
            int count = Math.Min(PartySize, players.Count);
            for (int i = 0; i < count; i++)
                total += CalculateCharacterPowerV1(players[i]);

            return total;
        }
    }
}
