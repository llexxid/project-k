using System.Collections.Generic;
using UnityEngine;
using Scripts.Core;
using KingdomIdle.MageTower;

namespace KingdomIdle.Gacha
{
    public class GachaManager : MonoBehaviour
    {
        public static GachaManager Instance { get; private set; }

        [SerializeField] private List<GachaTableSO> gachaTables = new List<GachaTableSO>();

        public IReadOnlyList<GachaTableSO> GetAllTables() => gachaTables;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public bool CanPull(GachaTableSO table)
        {
            if (table == null || !table.isImplemented) return false;
            EconomyBridge.TryGetAmount(table.costCurrency, out int cur);
            return cur >= table.costAmount;
        }

        public int GetTotalCost(GachaTableSO table, int count) =>
            table != null ? table.costAmount * count : 0;

        public bool CanPullMulti(GachaTableSO table, int count)
        {
            if (table == null || !table.isImplemented || count <= 0) return false;
            EconomyBridge.TryGetAmount(table.costCurrency, out int cur);
            return cur >= table.costAmount * count;
        }

        /// <summary>
        /// 다중 뽑기. 성공 시 보상 리스트 반환, 실패 시 null.
        /// 서버 마이그레이션 시 이 메서드 내부를 API 호출로 교체.
        /// </summary>
        public List<GachaRewardEntry> TryPull(GachaTableSO table, int count = 1)
        {
            if (table == null || !table.isImplemented || count <= 0) return null;

            int totalCost = table.costAmount * count;
            EconomyBridge.TryGetAmount(table.costCurrency, out int cur);
            if (cur < totalCost) return null;

            EconomyBridge.Add(table.costCurrency, -totalCost);

            var results = new List<GachaRewardEntry>(count);
            for (int i = 0; i < count; i++)
            {
                var reward = table.Roll();
                if (reward != null)
                {
                    DistributeReward(reward);
                    results.Add(reward);
                }
            }

            return results.Count > 0 ? results : null;
        }

        private void DistributeReward(GachaRewardEntry reward)
        {
            switch (reward.rewardType)
            {
                case eGachaRewardType.Currency:
                    EconomyBridge.Add(reward.currency, reward.amount);
                    break;

                case eGachaRewardType.Skill:
                    var mtMgr = MageTowerManager.Instance;
                    if (mtMgr != null)
                        mtMgr.AddFragments(reward.skillId, reward.amount);
                    break;
            }
        }
    }
}
