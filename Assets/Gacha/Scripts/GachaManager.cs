using System.Collections.Generic;
using UnityEngine;
using Scripts.Core;
using KingdomIdle.MageTower;
using KingdomIdle.KingdomArmy;
using PlayFab.CloudScriptModels;
using Newtonsoft.Json;
using Scripts.Server.DTO;
using Scripts.Core.Manager;

namespace KingdomIdle.Gacha
{
    using ItemCode = Scripts.Server.DTO.ItemCode;

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
            EconomyBridge.TryGetAmount(table.costCurrency, out long cur);
            return cur >= table.costAmount;
        }

        public int GetTotalCost(GachaTableSO table, int count) =>
            table != null ? table.costAmount * count : 0;

        public bool CanPullMulti(GachaTableSO table, int count)
        {
            if (table == null || !table.isImplemented || count <= 0) return false;
            EconomyBridge.TryGetAmount(table.costCurrency, out long cur);
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
            EconomyBridge.TryGetAmount(table.costCurrency, out long cur);
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

                case eGachaRewardType.Equipment:
                    DistributeEquipmentReward(reward);
                    break;
            }
        }

        /// <summary>
        /// 장비 보상을 첫 번째 플레이어의 인벤토리에 추가한다.
        /// 모든 플레이어의 인벤토리가 공유 UI에서 표시되므로 첫 번째 플레이어에 넣는다.
        /// </summary>
        private void DistributeEquipmentReward(GachaRewardEntry reward)
        {
            if (reward.equipmentData == null)
            {
                Debug.LogWarning("[GachaManager] Equipment 보상이지만 equipmentData가 null입니다.");
                return;
            }

            // KingdomArmyManager에서 플레이어 목록 가져오기
            var armyMgr = KingdomArmyManager.Instance;
            if (armyMgr == null)
            {
                Debug.LogWarning("[GachaManager] KingdomArmyManager가 없어 장비를 지급할 수 없습니다.");
                return;
            }

            var players = armyMgr.GetPlayers();
            if (players == null || players.Count == 0)
            {
                Debug.LogWarning("[GachaManager] 플레이어가 없어 장비를 지급할 수 없습니다.");
                return;
            }

            // 장비의 직업 제한에 맞는 플레이어를 우선 선택, 없으면 첫 번째 플레이어
            Player targetPlayer = players[0];
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                if (p?.equipmentManager == null) continue;

                var changeJob = p.GetComponent<ChangeJob>();
                if (changeJob == null) continue;

                // 현재 직업 이름으로 장비 호환 여부 확인
                // equipmentManager에 현재 직업이 세팅되어 있으면 해당 플레이어 우선
                if (reward.equipmentData.IsAllowedForJob(p.playerStatus?.JobName ?? ""))
                {
                    targetPlayer = p;
                    break;
                }
            }

            // EquipmentInstance 생성 후 인벤토리에 추가
            var instance = new EquipmentInstance(reward.equipmentData);
            targetPlayer.equipmentManager.Inventory.Add(instance);
            targetPlayer.equipmentManager.OnItemDropped?.Invoke(instance);

            Debug.Log($"[GachaManager] 장비 지급: {reward.equipmentData.equipmentName} ({reward.equipmentData.rarity}) → {targetPlayer.name}");
        }

		private void Update()
		{
		}

        private void OnGachaError(PlayFab.PlayFabError error)
        {
            Debug.Log(error.ErrorMessage);
        }

		private void OnSuccess(ExecuteFunctionResult result)
		{
			Debug.Log("Success");
		}

		private void OnGachaSuccess(ExecuteFunctionResult result)
        {
			//For Debugging
			string json = JsonConvert.SerializeObject(result.FunctionResult);
			OnGachaSkillArcaneKnowledgeResponseDTO responsedto = JsonConvert.DeserializeObject<OnGachaSkillArcaneKnowledgeResponseDTO>(json);

		}
	}
}
