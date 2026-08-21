using System;
using System.Collections.Generic;
using Scripts.Core;
using Scripts.Server.DTO;

namespace KingdomIdle.OfflineRewards
{
    /// <summary>
    /// 한 번의 오프라인 사냥 요청에 필요한 시간, 예상 처치 수, 몬스터별 처치 분포를 보관한다.
    /// 서버 DTO와 저장 형식을 분리해 PlayerPrefs에 보류 요청을 직렬화할 수 있게 한다.
    /// </summary>
    [Serializable]
    public sealed class OfflineRewardPlan
    {
        public long actualOfflineSeconds;
        public long appliedOfflineSeconds;
        public int estimatedKillCount;
        public List<OfflineHuntEntry> hunts = new List<OfflineHuntEntry>();

        public bool HasReward =>
            estimatedKillCount > 0 && hunts != null && hunts.Count > 0;

        /// <summary>오프라인 사냥 보상을 기존 OnHuntReward 요청 DTO로 변환한다.
        /// <br/> OnHuntReward를 그대로 쓰지 않은 이유는 HuntResult의 count가 short(최대 32,767) 타입이기 때문에 오버플로 방지용
        /// </summary>
        public List<HuntResult> CreateHuntResults()
        {
            var results = new List<HuntResult>();
            if (hunts == null)
                return results;

            foreach (OfflineHuntEntry hunt in hunts)
            {
                int remaining = Math.Max(0, hunt.count);
                while (remaining > 0)
                {
                    short batch = (short)Math.Min(short.MaxValue, remaining);
                    results.Add(new HuntResult
                    {
                        MonsterType = (eMonsterType)hunt.monsterTypeValue,
                        Count = batch
                    });
                    remaining -= batch;
                }
            }

            return results;
        }
    }

    /// <summary>Unity가 직렬화할 수 있는 몬스터 타입값과 처치 수</summary>
    [Serializable]
    public struct OfflineHuntEntry
    {
        public ulong monsterTypeValue;
        public int count;

        public OfflineHuntEntry(eMonsterType monsterType, int count)
        {
            monsterTypeValue = (ulong)monsterType;
            this.count = count;
        }
    }
}
