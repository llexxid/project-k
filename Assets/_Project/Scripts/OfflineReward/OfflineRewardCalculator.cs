using System;
using System.Collections.Generic;
using Scripts.Core;

namespace KingdomIdle.OfflineRewards
{
    /// <summary>
    /// 오프라인 시간으로 예상 처치 수와 몬스터 타입별 처치 분포를 계산한다.
    /// 일반 보상은 서버 사냥 보상표가 계산하고, 미래 희귀 보상은 독립 확률 판정 API를 사용할 수 있다.
    /// </summary>
    public static class OfflineRewardCalculator
    {
        public const int MaxOfflineSeconds = 8 * 60 * 60;
        public const int KillsPerMinute = 40;

        /// <summary>최대 8시간·분당 40킬 정책으로 서버에 전송할 오프라인보상 요청 계획 생성</summary>
        public static OfflineRewardPlan CreatePlan(
            TimeSpan offlineDuration,
            StageDefinition stageDefinition)
        {
            //미접속시간 계산 후 몬스터 예상 처치수 계산
            //actualSeconds는 실제 미접속 시간, appliedSeconds는 실제 오프라인 보상시간 
            long actualSeconds = Math.Max(0L, (long)offlineDuration.TotalSeconds);
            long appliedSeconds = Math.Min(actualSeconds, MaxOfflineSeconds);
            int killCount = (int)Math.Floor(
                appliedSeconds * KillsPerMinute / 60d);

            var plan = new OfflineRewardPlan
            {
                actualOfflineSeconds = actualSeconds,
                appliedOfflineSeconds = appliedSeconds,
                estimatedKillCount = killCount
            };

            if (stageDefinition == null || killCount <= 0)
                return plan;
            
            List<WeightedMonster> monsters = CollectMonsterWeights(stageDefinition);
            if (monsters.Count == 0)
                return plan;

            int totalWeight = 0;
            foreach (WeightedMonster monster in monsters)
                totalWeight += monster.Weight;

            int allocated = 0;
            
            /*
             * 총 처치수를 몬스터별로 정확하게 분배
             * ex. Orc Weight 3, ArmoredOrc Weight 1로 총 Weight = 4일때,
             * 오프라인 시간동안 몬스터를 10마리 처치했다고 하면 Orc = 6.6666... 처치, ArmoredOrc = 3.3333...마리 처치
             * 이러면 정확한 분배를 할 수 없기 때문에 각 소수점 내림한 정수와 소수점 나머지들을 저장한 후 큰 순서대로 하나씩 지급
             * 6 + 3 = 9마리 분배되어 1마리를 추가 분배해야하고, Orc = 0.6666.... / ArmoredOrc = 3.3333...이므로 Orc에 1마리 추가
             * => Orc = 7마리 / ArmoredOrc = 3마리 처치보상 지급
             */ 
            foreach (WeightedMonster monster in monsters)
            {
                double exact = killCount * (double)monster.Weight / totalWeight;
                monster.AllocatedCount = (int)Math.Floor(exact);
                monster.Remainder = exact - monster.AllocatedCount;
                allocated += monster.AllocatedCount;
            }
            //각 몬스터마다 소수점을 비교해 내림차순 정렬
            monsters.Sort(CompareRemainder);
            for (int i = 0; i < killCount - allocated; i++) //소수점 큰 몬스터마다 1마리씩 추가
                monsters[i % monsters.Count].AllocatedCount++;

            monsters.Sort((left, right) =>
                ((ulong)left.MonsterType).CompareTo((ulong)right.MonsterType));
            foreach (WeightedMonster monster in monsters)
            {
                if (monster.AllocatedCount > 0)
                {
                    //계산이 완료된 값들을 OfflineRewardPlan타입으로 정리해서 전달
                    plan.hunts.Add(new OfflineHuntEntry(
                        monster.MonsterType,
                        monster.AllocatedCount));
                }
            }

            return plan;
        }

        /// <summary>
        /// 희귀 아이템처럼 확률이 필요한 보상을 encounterCount회 독립 판정해 최종 수량을 반환한다.
        /// TeamProject DropTableSO.GetDroppedItems(int, float)의 아이템 분기와 같은 확장 지점이다.
        /// </summary>
        public static long RollProbabilisticRewardAmount(
            int encounterCount,
            float baseChance,
            float dropRate,
            int minAmount,
            int maxAmount,
            Random random = null)
        {
            if (encounterCount <= 0)
                return 0L;

            random ??= new Random();
            double finalChance = Math.Clamp(
                baseChance * (1d + dropRate),
                0d,
                1d);
            bool invalidRange = minAmount > maxAmount;
            long totalAmount = 0L;

            for (int i = 0; i < encounterCount; i++)
            {
                if (random.NextDouble() >= finalChance)
                    continue;

                totalAmount += invalidRange
                    ? 1
                    : random.Next(minAmount, maxAmount + 1);
            }

            return totalAmount;
        }

        /// <summary>
        /// StageDefinition의 몬스터 타입별 등장비중을 계산
        /// </summary>
        private static List<WeightedMonster> CollectMonsterWeights(
            StageDefinition stageDefinition)
        {
            var monsters = new List<WeightedMonster>();
            foreach (StageMonsterEntry entry in stageDefinition.MonsterEntries)
            {
                if (entry.Count <= 0)
                    continue;

                WeightedMonster existing = monsters.Find(candidate =>
                    candidate.MonsterType == entry.MonsterType);
                if (existing != null)
                {
                    existing.Weight += entry.Count;
                    continue;
                }

                monsters.Add(new WeightedMonster(entry.MonsterType, entry.Count));
            }

            return monsters;
        }

        private static int CompareRemainder(
            WeightedMonster left,
            WeightedMonster right)
        {
            int remainderOrder = right.Remainder.CompareTo(left.Remainder);
            return remainderOrder != 0
                ? remainderOrder
                : ((ulong)left.MonsterType).CompareTo((ulong)right.MonsterType);
        }

        private sealed class WeightedMonster
        {
            public eMonsterType MonsterType { get; }
            public int Weight { get; set; }
            public int AllocatedCount { get; set; } // MonsterType 몬스터의 처치 수(소수점 내림) 
            public double Remainder { get; set; } //내림한 소수

            public WeightedMonster(eMonsterType monsterType, int weight)
            {
                MonsterType = monsterType;
                Weight = weight;
            }
        }
    }
}
