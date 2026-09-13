using System;
using System.Collections.Generic;
using System.Linq;
using KingdomIdle.Balance;
using KingdomIdle.MageTower;
using UnityEngine;

namespace KingdomIdle.Gacha
{
    public class GachaManager : MonoBehaviour
    {
        public static GachaManager Instance { get; private set; }
        [SerializeField] private List<GachaTableSO> gachaTables;
        public bool IsPulling { get; private set; }
        public event Action<bool> OnPullStateChanged;
        public int EpicPityRemaining => 40 - LocalProgression.State.EquipmentPity;
        public IReadOnlyList<GachaTableSO> GetAllTables() => gachaTables.Where(t => t != null && t.isImplemented && (t.gachaType == eGachaType.Equipment || t.gachaType == eGachaType.Skill) && t.costCurrency == eCurrency.AncientCoin).ToList();
        private void Awake() { if (Instance != null && Instance != this) { Destroy(gameObject); return; } Instance = this; DontDestroyOnLoad(gameObject); }
        private void OnDestroy() { if (Instance == this) Instance = null; }
        public int GetTotalCost(GachaTableSO table, int count) => table != null && (count == 1 || count == 10) ? 50 * count : -1;
        public bool CanPull(GachaTableSO table) => CanPullMulti(table, 1);
        public bool CanPullMulti(GachaTableSO table, int count) => table != null && table.isImplemented && !IsPulling &&
            (table.gachaType == eGachaType.Equipment || table.gachaType == eGachaType.Skill) && table.costCurrency == eCurrency.AncientCoin && (count == 1 || count == 10) && LocalProgression.Balance(eCurrency.AncientCoin) >= 50L * count &&
            (table.gachaType != eGachaType.Equipment || (EquipmentManager.Instance != null && EquipmentManager.Instance.AvailableSlots >= count));
        public void TryPull(GachaTableSO table, int count, Action<List<GachaRewardEntry>> onSuccess, Action<string> onError)
        {
            if (!CanPullMulti(table, count)) { onError?.Invoke("주화와 가방 여유 공간을 확인해 주세요."); return; }
            var equipment = EquipmentManager.Instance;
            var mage = MageTowerManager.Instance;
            var normal = equipment?.GetByRarity(eEquipmentRarity.Normal);
            var rare = equipment?.GetByRarity(eEquipmentRarity.Rare);
            var epic = equipment?.GetByRarity(eEquipmentRarity.Epic);
            var skills = mage?.GetAllSkills().Where(x => x != null).ToArray();
            if ((table.gachaType == eGachaType.Equipment && (normal?.Count != 9 || rare?.Count != 6 || epic?.Count != 3)) ||
                (table.gachaType == eGachaType.Skill && skills?.Length != 3)) { onError?.Invoke("뽑기 데이터 구성을 확인할 수 없습니다."); return; }
            IsPulling = true; OnPullStateChanged?.Invoke(true);
            var rewards = new List<GachaRewardEntry>(); bool ok;
            try
            {
                ok = LocalProgression.Execute("gacha-" + table.gachaType, state => {
                    if (table.gachaType == eGachaType.Equipment && state.Equipment.Count + count > EquipmentManager.Capacity) return false;
                    if (!LocalProgression.Spend(state, eCurrency.AncientCoin, 50L * count)) return false;
                    for (int i = 0; i < count; i++)
                    {
                        int roll = UnityEngine.Random.Range(0, 1000000);
                        if (table.gachaType == eGachaType.Equipment)
                        {
                            int tier = BalanceMath.EquipmentRoll(roll,state.EquipmentPity);
                            if (tier < 0)
                            {
                                state.EquipmentPity++;
                                LocalProgression.Credit(state, eCurrency.ClassFragment, 2);
                                rewards.Add(new GachaRewardEntry { nameKor = "전직 파편 2개", rewardType = eGachaRewardType.Currency, currency = eCurrency.ClassFragment, amount = 2 });
                                continue;
                            }
                            var pool = tier == 2 ? epic : tier == 1 ? rare : normal;
                            var item = pool[UnityEngine.Random.Range(0, pool.Count)];
                            state.EquipmentPity = item.rarity == eEquipmentRarity.Epic ? 0 : state.EquipmentPity + 1;
                            if (!EquipmentManager.Grant(state, new EquipmentSave { Id = Guid.NewGuid().ToString("N"), Code = item.itemCode }, false)) return false;
                            rewards.Add(new GachaRewardEntry { nameKor = item.equipmentName, icon = item.icon, rewardType = eGachaRewardType.Equipment, equipmentData = item, amount = 1 });
                        }
                        else if (roll < 500000)
                        {
                            var skill = skills[UnityEngine.Random.Range(0, skills.Length)];
                            MageTowerManager.Grant(state, skill.id);
                            rewards.Add(new GachaRewardEntry { nameKor = skill.nameKor, icon = skill.icon, rewardType = eGachaRewardType.Skill, skillId = skill.id, amount = 1 });
                        }
                        else
                        {
                            int amount = roll < 800000 ? 10 : roll < 950000 ? 20 : 50;
                            LocalProgression.Credit(state, eCurrency.ArcaneKnowledge, amount);
                            rewards.Add(new GachaRewardEntry { nameKor = "비전 지식 " + amount + "개", rewardType = eGachaRewardType.Currency, currency = eCurrency.ArcaneKnowledge, amount = amount });
                        }
                    }
                    QuestEconomy.Count(state,eQuestObjectiveType.GachaUse,table.gachaType == eGachaType.Equipment ? 1 : 2,count);
                    return true;
                });
            }
            finally { IsPulling = false; OnPullStateChanged?.Invoke(false); }
            if (!ok) { onError?.Invoke("결과를 저장하지 못했습니다. 주화는 소비되지 않았습니다."); return; }
            equipment?.RestoreEquipment(); mage?.NotifyCommitted(); onSuccess?.Invoke(rewards);
        }
    }
}
