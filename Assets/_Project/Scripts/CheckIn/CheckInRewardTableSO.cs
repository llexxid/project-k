using KingdomIdle.Rewards;
using UnityEngine;

namespace KingdomIdle.CheckIn
{
    [CreateAssetMenu(menuName = "KingdomIdle/CheckIn Reward Table")]
    public sealed class CheckInRewardTableSO : ScriptableObject
    {
        [Tooltip("시안 검증용 수치입니다. 출시 보상은 별도로 확정합니다.")]
        public RewardGrant[] days = new RewardGrant[28];

        public bool IsValid()
        {
            if (days == null || days.Length != 28) return false;
            foreach (var day in days) if (!RewardGrants.IsValid(day)) return false;
            return true;
        }

    }
}
