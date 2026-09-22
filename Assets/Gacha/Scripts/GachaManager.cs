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
            (table.gachaType != eGachaType.Equipment || EquipmentManager.Instance != null);
        public string PullFailure(GachaTableSO table, int count)
        {
            if (IsPulling) return "이미 뽑기가 진행 중입니다.";
            if (table == null || !table.isImplemented || (count != 1 && count != 10)) return "뽑기 구성을 확인할 수 없습니다.";
            if (LocalProgression.Balance(eCurrency.AncientCoin) < 50L * count) return $"고대주화가 {50L * count - LocalProgression.Balance(eCurrency.AncientCoin):N0}개 부족합니다.";
            return "뽑기 데이터를 준비 중입니다. 잠시 후 다시 시도해 주세요.";
        }
        /// <summary>기존 확률로 뽑고 소비·보상·실습 영수증을 한 번에 저장한다. 실패 시 오류 콜백만 반환하고 성공 콜백은 확정 저장 후 호출한다.</summary>
        public void TryPull(GachaTableSO table, int count, Action<List<GachaRewardEntry>> onSuccess, Action<string> onError)
        {
            var guideAction = table != null && table.gachaType == eGachaType.Equipment ? Direction.GuideAction.EquipmentPullOnce : Direction.GuideAction.SkillPullOnce;
            if (!Direction.GameDirectInteraction.CanPerform(guideAction, count)) { onError?.Invoke("현재 안내의 지정된 실습만 진행할 수 있습니다."); return; }
            if (!CanPullMulti(table, count)) { onError?.Invoke(PullFailure(table, count)); return; }
            var equipment = EquipmentManager.Instance;
            var mage = MageTowerManager.Instance;
            var normal = equipment?.GetRewardPool(eEquipmentRarity.Normal);
            var rare = equipment?.GetRewardPool(eEquipmentRarity.Rare);
            var epic = equipment?.GetRewardPool(eEquipmentRarity.Epic);
            var skills = mage?.GetAllSkills().Where(x => x != null).ToArray();
            if ((table.gachaType == eGachaType.Equipment && (normal == null || normal.Count == 0 || rare == null || rare.Count == 0 || epic == null || epic.Count == 0)) ||
                (table.gachaType == eGachaType.Skill && !MageSkillRules.ValidateRoster(skills))) { onError?.Invoke("뽑기 데이터 구성을 확인할 수 없습니다."); return; }
            IsPulling = true; OnPullStateChanged?.Invoke(true);
            var rewards = new List<GachaRewardEntry>(); bool ok;
            try
            {
                ok = LocalProgression.Execute("gacha-" + table.gachaType, state => {
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
                            var acquired = new EquipmentSave { Id = Guid.NewGuid().ToString("N"), Code = item.itemCode };
                            bool autoDismantled = EquipmentEconomy.ShouldAutoDismantle(state, acquired);
                            if (!EquipmentManager.Grant(state, acquired, true)) return false;
                            rewards.Add(autoDismantled
                                ? new GachaRewardEntry { nameKor = "자동 분해 · 강화석", icon = item.icon, rewardType = eGachaRewardType.Currency, currency = eCurrency.EquipmentStone, amount = checked((int)EquipmentEconomy.Yield(acquired)) }
                                : new GachaRewardEntry { nameKor = item.equipmentName, icon = item.icon, rewardType = eGachaRewardType.Equipment, equipmentData = item, amount = 1 });
                        }
                        else if (roll < 500000)
                        {
                            var skill = MageSkillRules.SelectSkill(skills, UnityEngine.Random.Range(0, skills.Length));
                            bool duplicate = state.MageSkills.ContainsKey(skill.id);
                            MageTowerManager.Grant(state, skill.id, MageSkillRules.DuplicateFragments);
                            rewards.Add(new GachaRewardEntry { nameKor = duplicate ? $"{skill.nameKor} 파편" : skill.nameKor, icon = skill.icon, rewardType = eGachaRewardType.Skill, skillId = skill.id, amount = duplicate ? MageSkillRules.DuplicateFragments : 1 });
                        }
                        else
                        {
                            int amount = roll < 800000 ? 10 : roll < 950000 ? 20 : 50;
                            LocalProgression.Credit(state, eCurrency.ArcaneKnowledge, amount);
                            rewards.Add(new GachaRewardEntry { nameKor = "비전 지식 " + amount + "개", rewardType = eGachaRewardType.Currency, currency = eCurrency.ArcaneKnowledge, amount = amount });
                        }
                    }
                    QuestEconomy.Count(state,eQuestObjectiveType.GachaUse,table.gachaType == eGachaType.Equipment ? 1 : 2,count);
                    Direction.GameDirectInteraction.RecordSuccess(state, guideAction);
                    return true;
                });
            }
            finally { IsPulling = false; OnPullStateChanged?.Invoke(false); }
            if (!ok) { Direction.GameDirectInteraction.ReportSaveFailure(); onError?.Invoke("결과를 저장하지 못했습니다. 주화는 소비되지 않았습니다."); return; }
            equipment?.RestoreEquipment(); mage?.NotifyCommitted(); onSuccess?.Invoke(rewards);
        }
    }
}
