using System;
using System.Collections.Generic;
using UnityEngine;
using Scripts.Core;
using Scripts.Core.Manager;
using Scripts.Server.DTO;
using Scripts.Wallets;
using KingdomIdle.MageTower;
using KingdomIdle.KingdomArmy;
using PlayFab.CloudScriptModels;
using Newtonsoft.Json;

namespace KingdomIdle.Gacha
{
    using ItemCode = Scripts.Server.DTO.ItemCode;
    using SkillCode = Scripts.Server.DTO.SkillCode;

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
        /// ClassFragment / ArcaneKnowledge 통화로 뽑는 테이블은 서버 가챠로 라우팅된다.
        /// 그 외(Gold 등 테스트용)는 클라이언트 가중 롤.
        /// </summary>
        private static bool IsServerBacked(GachaTableSO table) =>
            table != null &&
            (table.costCurrency == eCurrency.ClassFragment ||
             table.costCurrency == eCurrency.ArcaneKnowledge);

        /// <summary>
        /// 다중 뽑기(비동기). 서버 응답을 받아 콜백으로 결과 전달.
        /// 서버 에러 시 차감된 재화를 롤백한다.
        /// </summary>
        public void TryPull(GachaTableSO table, int count,
                            Action<List<GachaRewardEntry>> onSuccess,
                            Action<string> onError)
        {
            if (table == null || !table.isImplemented || count <= 0)
            {
                onError?.Invoke("invalid");
                return;
            }

            int totalCost = table.costAmount * count;
            EconomyBridge.TryGetAmount(table.costCurrency, out long cur);
            if (cur < totalCost)
            {
                onError?.Invoke("재화가 부족합니다.");
                return;
            }

            // 낙관적 차감: 서버 오류 시 롤백
            EconomyBridge.Add(table.costCurrency, -totalCost);

            if (IsServerBacked(table))
            {
                RequestServerPull(table, count, totalCost, onSuccess, onError);
            }
            else
            {
                var results = RollClient(table, count);
                if (results == null || results.Count == 0)
                {
                    EconomyBridge.Add(table.costCurrency, totalCost); // 롤백
                    onError?.Invoke("뽑기에 실패했습니다.");
                    return;
                }
                onSuccess?.Invoke(results);
            }
        }

        // ===== 서버 가챠 =====

        private void RequestServerPull(GachaTableSO table, int count, int refundAmount,
                                       Action<List<GachaRewardEntry>> onSuccess,
                                       Action<string> onError)
        {
            var net = NetworkManager.Instance;
            if (net == null)
            {
                EconomyBridge.Add(table.costCurrency, refundAmount);
                onError?.Invoke("네트워크가 초기화되지 않았습니다.");
                return;
            }

            if (table.costCurrency == eCurrency.ClassFragment)
            {
                net.OnGachaEquipmentClick(count,
                    result => HandleEquipmentResponse(result, onSuccess, onError, table, refundAmount),
                    error  => HandleServerError(error, onError, table, refundAmount));
            }
            else if (table.costCurrency == eCurrency.ArcaneKnowledge)
            {
                net.OnGachaSkillClick(count,
                    result => HandleSkillResponse(result, onSuccess, onError, table, refundAmount),
                    error  => HandleServerError(error, onError, table, refundAmount));
            }
            else
            {
                EconomyBridge.Add(table.costCurrency, refundAmount);
                onError?.Invoke("지원하지 않는 재화입니다.");
            }
        }

        private void HandleEquipmentResponse(ExecuteFunctionResult result,
                                             Action<List<GachaRewardEntry>> onSuccess,
                                             Action<string> onError,
                                             GachaTableSO table, int refundAmount)
        {
            OnGachaEquipmentClassFragmentResponseDTO dto;
            try
            {
                string json = JsonConvert.SerializeObject(result.FunctionResult);
                dto = JsonConvert.DeserializeObject<OnGachaEquipmentClassFragmentResponseDTO>(json);
            }
            catch (Exception ex)
            {
                EconomyBridge.Add(table.costCurrency, refundAmount);
                onError?.Invoke($"응답 파싱 실패: {ex.Message}");
                return;
            }

            if (dto?.GachaList == null || dto.GachaList.Count == 0)
            {
                EconomyBridge.Add(table.costCurrency, refundAmount);
                onError?.Invoke("서버에서 보상을 받지 못했습니다.");
                return;
            }

            var equipDB = KingdomArmyManager.Instance?.EquipDB;
            if (equipDB == null)
            {
                Debug.LogWarning("[GachaManager] EquipmentDatabase 없음 - 보상 지급 불가");
                onError?.Invoke("장비 데이터베이스가 없습니다.");
                return;
            }

            var results = new List<GachaRewardEntry>(dto.GachaList.Count);
            foreach (var itemCode in dto.GachaList)
            {
                EquipmentData data = equipDB.GetEquipmentByCode((int)itemCode.GetItemCode());
                if (data == null)
                {
                    Debug.LogWarning($"[GachaManager] itemCode {itemCode.GetItemCode()} 에 해당하는 장비 없음");
                    continue;
                }

                var entry = new GachaRewardEntry
                {
                    rewardType    = eGachaRewardType.Equipment,
                    equipmentData = data,
                    nameKor       = data.equipmentName,
                    icon          = data.icon,
                    amount        = 1,
                };
                DistributeEquipmentReward(entry);
                results.Add(entry);
            }

            if (results.Count == 0)
            {
                onError?.Invoke("유효한 보상이 없습니다.");
                return;
            }

            onSuccess?.Invoke(results);
        }

        private void HandleSkillResponse(ExecuteFunctionResult result,
                                         Action<List<GachaRewardEntry>> onSuccess,
                                         Action<string> onError,
                                         GachaTableSO table, int refundAmount)
        {
            OnGachaSkillArcaneKnowledgeResponseDTO dto;
            try
            {
                string json = JsonConvert.SerializeObject(result.FunctionResult);
                dto = JsonConvert.DeserializeObject<OnGachaSkillArcaneKnowledgeResponseDTO>(json);
            }
            catch (Exception ex)
            {
                EconomyBridge.Add(table.costCurrency, refundAmount);
                onError?.Invoke($"응답 파싱 실패: {ex.Message}");
                return;
            }

            if (dto?.GachaList == null || dto.GachaList.Count == 0)
            {
                EconomyBridge.Add(table.costCurrency, refundAmount);
                onError?.Invoke("서버에서 보상을 받지 못했습니다.");
                return;
            }

            var mtMgr = MageTowerManager.Instance;
            var results = new List<GachaRewardEntry>(dto.GachaList.Count);

            foreach (var skillCode in dto.GachaList)
            {
                int skillId = (int)skillCode.GetSkillId();
                var so = mtMgr != null ? mtMgr.GetSkillById(skillId) : null;

                var entry = new GachaRewardEntry
                {
                    rewardType = eGachaRewardType.Skill,
                    skillId    = skillId,
                    amount     = 1,
                    nameKor    = so != null ? so.nameKor : $"Skill #{skillId}",
                    icon       = so != null ? so.icon : null,
                };

                if (mtMgr != null)
                    mtMgr.AddFragments(skillId, 1);

                results.Add(entry);
            }

            if (results.Count == 0)
            {
                onError?.Invoke("유효한 보상이 없습니다.");
                return;
            }

            onSuccess?.Invoke(results);
        }

        private void HandleServerError(PlayFab.PlayFabError error,
                                       Action<string> onError,
                                       GachaTableSO table, int refundAmount)
        {
            EconomyBridge.Add(table.costCurrency, refundAmount);
            string msg = error != null ? error.ErrorMessage : "알 수 없는 서버 오류";
            Debug.LogWarning($"[GachaManager] 서버 가챠 실패: {msg}");
            onError?.Invoke(msg);
        }

        // ===== 클라이언트 롤(테스트/비서버 통화용) =====

        private List<GachaRewardEntry> RollClient(GachaTableSO table, int count)
        {
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
            return results;
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
        /// 장비 보상을 직업이 호환되는 플레이어(없으면 첫 번째)의 인벤토리에 추가한다.
        /// </summary>
        private void DistributeEquipmentReward(GachaRewardEntry reward)
        {
            if (reward.equipmentData == null)
            {
                Debug.LogWarning("[GachaManager] Equipment 보상이지만 equipmentData가 null입니다.");
                return;
            }

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

            Player targetPlayer = players[0];
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                if (p?.equipmentManager == null) continue;

                var changeJob = p.GetComponent<ChangeJob>();
                if (changeJob == null) continue;

                if (reward.equipmentData.IsAllowedForJob(p.playerStatus?.JobName ?? ""))
                {
                    targetPlayer = p;
                    break;
                }
            }

            var instance = new EquipmentInstance(reward.equipmentData);
            targetPlayer.equipmentManager.Inventory.Add(instance);
            targetPlayer.equipmentManager.OnItemDropped?.Invoke(instance);

            Debug.Log($"[GachaManager] 장비 지급: {reward.equipmentData.equipmentName} ({reward.equipmentData.rarity}) → {targetPlayer.name}");
        }
    }
}
