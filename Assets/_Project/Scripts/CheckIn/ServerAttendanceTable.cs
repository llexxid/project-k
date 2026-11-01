using System.Collections.Generic;
using KingdomIdle.Rewards;

namespace KingdomIdle.CheckIn
{
    /// <summary>
    /// 서버 출석판 보상표(AuthenticationFunction/Contents/Data/attendance.json)의 사본이다. n일차 = 이번 달 n번째로 연 칸.
    /// 칸 표시 전용이며 지급량은 서버가 정한다. 서버 테스트(ClientScenarioTests)가 서버 표와 같은지 검사한다.
    /// </summary>
    public static class ServerAttendanceTable
    {
        public const int Days = 28;
        private static RewardGrant C(eCurrency currency, long amount) => new RewardGrant(currency, amount);
        private static RewardGrant E(int definitionId, long amount) => RewardGrant.Equipment(definitionId, amount);

        private static readonly RewardGrant[][] Table =
        {
            /*  1 */ new[] { C(eCurrency.Gold, 5000) },
            /*  2 */ new[] { C(eCurrency.AncientCoin, 50) },
            /*  3 */ new[] { C(eCurrency.ArcaneKnowledge, 100) },
            /*  4 */ new[] { C(eCurrency.Gold, 10000) },
            /*  5 */ new[] { C(eCurrency.ClassFragment, 10) },
            /*  6 */ new[] { C(eCurrency.EquipmentStone, 20) },
            /*  7 */ new[] { C(eCurrency.AncientCoin, 100), E(131076, 1) },
            /*  8 */ new[] { C(eCurrency.Gold, 15000) },
            /*  9 */ new[] { C(eCurrency.AncientCoin, 50) },
            /* 10 */ new[] { C(eCurrency.ArcaneKnowledge, 150) },
            /* 11 */ new[] { C(eCurrency.Gold, 20000) },
            /* 12 */ new[] { C(eCurrency.ClassFragment, 15) },
            /* 13 */ new[] { C(eCurrency.EquipmentStone, 30) },
            /* 14 */ new[] { C(eCurrency.Ruby, 50), E(524289, 1) },
            /* 15 */ new[] { C(eCurrency.Gold, 25000) },
            /* 16 */ new[] { C(eCurrency.AncientCoin, 50) },
            /* 17 */ new[] { C(eCurrency.ArcaneKnowledge, 200) },
            /* 18 */ new[] { C(eCurrency.Gold, 30000) },
            /* 19 */ new[] { C(eCurrency.ClassFragment, 20) },
            /* 20 */ new[] { C(eCurrency.EquipmentStone, 40) },
            /* 21 */ new[] { C(eCurrency.AncientCoin, 150), E(69636, 1) },
            /* 22 */ new[] { C(eCurrency.Gold, 35000) },
            /* 23 */ new[] { C(eCurrency.AncientCoin, 50) },
            /* 24 */ new[] { C(eCurrency.ArcaneKnowledge, 250) },
            /* 25 */ new[] { C(eCurrency.Gold, 40000) },
            /* 26 */ new[] { C(eCurrency.ClassFragment, 25) },
            /* 27 */ new[] { C(eCurrency.EquipmentStone, 50) },
            /* 28 */ new[] { C(eCurrency.AncientCoin, 300), C(eCurrency.Ruby, 100), E(331777, 1) },
        };

        /// <summary>day(1~28)의 보상 사본. 범위 밖이면 빈 목록.</summary>
        public static IReadOnlyList<RewardGrant> RewardsOf(int day)
        {
            if (day < 1 || day > Table.Length) return System.Array.Empty<RewardGrant>();
            return RewardGrants.Copy(Table[day - 1]);
        }

        /// <summary>칸에 대표로 보일 보상: 장비가 있으면 장비, 없으면 첫 보상.</summary>
        public static RewardGrant Representative(int day)
        {
            var rewards = RewardsOf(day);
            foreach (var reward in rewards) if (reward.IsEquipment) return reward;
            return rewards.Count > 0 ? rewards[0] : null;
        }
    }
}
