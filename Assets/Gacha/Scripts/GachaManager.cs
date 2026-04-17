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

        private bool _isPulling;
        public bool IsPulling => _isPulling;

        public event Action<bool> OnPullStateChanged;

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
            return cur >= (long)table.costAmount * count;
        }

        private static bool IsServerBacked(GachaTableSO table) =>
            table != null &&
            (table.gachaType == eGachaType.Equipment ||
             table.gachaType == eGachaType.Skill);

        public void TryPull(GachaTableSO table, int count,
                            Action<List<GachaRewardEntry>> onSuccess,
                            Action<string> onError)
        {
            if (_isPulling)
            {
                onError?.Invoke("이미 뽑기가 진행 중입니다.");
                return;
            }

            if (table == null)
            {
                onError?.Invoke("뽑기 테이블이 유효하지 않습니다.");
                return;
            }

            if (!table.isImplemented)
            {
                onError?.Invoke("미구현 기능입니다.");
                return;
            }

            if (count <= 0)
            {
                onError?.Invoke("뽑기 횟수가 잘못되었습니다.");
                return;
            }

            int totalCost = table.costAmount * count;
            EconomyBridge.TryGetAmount(table.costCurrency, out long cur);
            if (cur < totalCost)
            {
                onError?.Invoke("재화가 부족합니다.");
                return;
            }

            // 서버 가챠는 세션이 확립되어야 가능
            if (IsServerBacked(table) && !IsNetworkReady())
            {
                onError?.Invoke("네트워크 세션이 준비되지 않았습니다.");
                return;
            }

            SetPulling(true);
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
                    FailWithRefund(table, totalCost, "뽑기에 실패했습니다.", onError);
                    return;
                }
                CompleteSuccess(results, onSuccess);
            }
        }

        private void SetPulling(bool v)
        {
            if (_isPulling == v) return;
            _isPulling = v;
            OnPullStateChanged?.Invoke(v);
        }

        private void CompleteSuccess(List<GachaRewardEntry> results,
                                     Action<List<GachaRewardEntry>> onSuccess)
        {
            SetPulling(false);
            onSuccess?.Invoke(results);
        }

        private void FailWithRefund(GachaTableSO table, int refundAmount,
                                    string message, Action<string> onError)
        {
            if (refundAmount > 0 && table != null)
                EconomyBridge.Add(table.costCurrency, refundAmount);
            SetPulling(false);
            onError?.Invoke(message);
        }

        private static bool IsNetworkReady()
        {
            var net = NetworkManager.Instance;
            if (net == null) return false;
            string sid = net.GetSessionID();
            return !string.IsNullOrEmpty(sid);
        }

        private void RequestServerPull(GachaTableSO table, int count, int refundAmount,
                                       Action<List<GachaRewardEntry>> onSuccess,
                                       Action<string> onError)
        {
            var net = NetworkManager.Instance;
            if (net == null)
            {
                FailWithRefund(table, refundAmount, "네트워크가 초기화되지 않았습니다.", onError);
                return;
            }

            switch (table.gachaType)
            {
                case eGachaType.Equipment:
                    net.OnGachaEquipmentClick(count,
                        result => HandleEquipmentResponse(result, onSuccess, onError, table, refundAmount),
                        error  => HandleServerError(error, onError, table, refundAmount));
                    break;
                case eGachaType.Skill:
                    net.OnGachaSkillClick(count,
                        result => HandleSkillResponse(result, onSuccess, onError, table, refundAmount),
                        error  => HandleServerError(error, onError, table, refundAmount));
                    break;
                default:
                    FailWithRefund(table, refundAmount, "지원하지 않는 뽑기 타입입니다.", onError);
                    break;
            }
        }

        private void HandleEquipmentResponse(ExecuteFunctionResult result,
                                             Action<List<GachaRewardEntry>> onSuccess,
                                             Action<string> onError,
                                             GachaTableSO table, int refundAmount)
        {
            if (result == null || result.FunctionResult == null)
            {
                FailWithRefund(table, refundAmount, "서버 응답이 비어있습니다.", onError);
                return;
            }

            OnGachaEquipmentClassFragmentResponseDTO dto;
            try
            {
                string json = JsonConvert.SerializeObject(result.FunctionResult);
                dto = JsonConvert.DeserializeObject<OnGachaEquipmentClassFragmentResponseDTO>(json);
            }
            catch (Exception ex)
            {
                FailWithRefund(table, refundAmount, $"응답 파싱 실패: {ex.Message}", onError);
                return;
            }

            if (dto?.GachaList == null || dto.GachaList.Count == 0)
            {
                FailWithRefund(table, refundAmount, "서버에서 보상을 받지 못했습니다.", onError);
                return;
            }

            var equipDB = KingdomArmyManager.Instance?.EquipDB;
            if (equipDB == null)
            {
                FailWithRefund(table, refundAmount, "장비 데이터베이스가 없습니다.", onError);
                return;
            }

            var results = new List<GachaRewardEntry>(dto.GachaList.Count);
            foreach (var itemCode in dto.GachaList)
            {
                int code = (int)itemCode.GetItemCode();
                EquipmentData data = equipDB.GetEquipmentByCode(code);

                if (data != null)
                {
                    int equipCount = (int)itemCode.GetItemAmount();
                    if (equipCount <= 0) equipCount = 1;

                    var equipEntry = new GachaRewardEntry
                    {
                        rewardType    = eGachaRewardType.Equipment,
                        equipmentData = data,
                        nameKor       = data.equipmentName,
                        icon          = data.icon,
                        amount        = equipCount,
                    };

                    for (int i = 0; i < equipCount; i++)
                        DistributeEquipmentReward(equipEntry);

                    results.Add(equipEntry);
                }
                else
                {
                    int fragmentAmount = (int)itemCode.GetItemAmount();
                    if (fragmentAmount <= 0) fragmentAmount = 1;

                    KingdomArmyManager.Instance.AddFragments(fragmentAmount);

                    results.Add(new GachaRewardEntry
                    {
                        rewardType = eGachaRewardType.Currency,
                        currency   = eCurrency.ClassFragment,
                        amount     = fragmentAmount,
                        nameKor    = "전직 파편",
                    });
                }
            }

            // 서버 응답의 전직파편 총량으로 클라이언트 동기화
            if (dto.ClassFragmentCnt >= 0)
            {
                EconomyBridge.TryGetAmount(eCurrency.ClassFragment, out long curFragment);
                long diff = dto.ClassFragmentCnt - curFragment;
                if (diff != 0)
                    EconomyBridge.Add(eCurrency.ClassFragment, (int)diff);
            }

            if (results.Count == 0)
            {
                SetPulling(false);
                onError?.Invoke("보상 데이터가 올바르지 않습니다. 관리자에게 문의해주세요.");
                return;
            }

            CompleteSuccess(results, onSuccess);
        }

        private void HandleSkillResponse(ExecuteFunctionResult result,
                                         Action<List<GachaRewardEntry>> onSuccess,
                                         Action<string> onError,
                                         GachaTableSO table, int refundAmount)
        {
            if (result == null || result.FunctionResult == null)
            {
                FailWithRefund(table, refundAmount, "서버 응답이 비어있습니다.", onError);
                return;
            }

            OnGachaSkillArcaneKnowledgeResponseDTO dto;
            try
            {
                string json = JsonConvert.SerializeObject(result.FunctionResult);
                dto = JsonConvert.DeserializeObject<OnGachaSkillArcaneKnowledgeResponseDTO>(json);
            }
            catch (Exception ex)
            {
                FailWithRefund(table, refundAmount, $"응답 파싱 실패: {ex.Message}", onError);
                return;
            }

            if (dto?.GachaList == null || dto.GachaList.Count == 0)
            {
                FailWithRefund(table, refundAmount, "서버에서 보상을 받지 못했습니다.", onError);
                return;
            }

            var mtMgr = MageTowerManager.Instance;
            if (mtMgr == null)
            {
                FailWithRefund(table, refundAmount, "마탑 매니저가 없습니다.", onError);
                return;
            }

            var results = new List<GachaRewardEntry>(dto.GachaList.Count);

            foreach (var skillCode in dto.GachaList)
            {
                int skillId = (int)skillCode.GetSkillId();
                var so = mtMgr.GetSkillById(skillId);
                if (so == null) continue;

                int skillCount = (int)skillCode.GetSkillAmount();
                if (skillCount <= 0) skillCount = 1;

                int fragmentAmount = skillCount;
                if (!mtMgr.IsOwned(skillId))
                {
                    mtMgr.Unlock(skillId);
                    fragmentAmount -= 1;
                }
                if (fragmentAmount > 0)
                    mtMgr.AddFragments(skillId, fragmentAmount);

                results.Add(new GachaRewardEntry
                {
                    rewardType = eGachaRewardType.Skill,
                    skillId    = skillId,
                    amount     = skillCount,
                    nameKor    = so.nameKor,
                    icon       = so.icon,
                });
            }

            if (dto.ArcaneKnowledgeCnt >= 0)
            {
                EconomyBridge.TryGetAmount(eCurrency.ArcaneKnowledge, out long curAK);
                long diff = dto.ArcaneKnowledgeCnt - curAK;
                if (diff != 0)
                    EconomyBridge.Add(eCurrency.ArcaneKnowledge, (int)diff);

                if (diff > 0)
                {
                    results.Add(new GachaRewardEntry
                    {
                        rewardType = eGachaRewardType.Currency,
                        currency   = eCurrency.ArcaneKnowledge,
                        amount     = (int)diff,
                        nameKor    = "비전지식",
                    });
                }
            }

            if (results.Count == 0)
            {
                SetPulling(false);
                onError?.Invoke("스킬 데이터가 올바르지 않습니다. 관리자에게 문의해주세요.");
                return;
            }

            CompleteSuccess(results, onSuccess);
        }

        private void HandleServerError(PlayFab.PlayFabError error,
                                       Action<string> onError,
                                       GachaTableSO table, int refundAmount)
        {
            string msg = error != null ? error.ErrorMessage : "알 수 없는 서버 오류";
            FailWithRefund(table, refundAmount, $"서버 오류: {msg}", onError);
        }

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
                    {
                        int amt = reward.amount;
                        if (!mtMgr.IsOwned(reward.skillId))
                        {
                            mtMgr.Unlock(reward.skillId);
                            amt -= 1;
                        }
                        if (amt > 0)
                            mtMgr.AddFragments(reward.skillId, amt);
                    }
                    break;

                case eGachaRewardType.Equipment:
                    DistributeEquipmentReward(reward);
                    break;
            }
        }

        private void DistributeEquipmentReward(GachaRewardEntry reward)
        {
            if (reward.equipmentData == null) return;

            var players = KingdomArmyManager.Instance.GetPlayers();
            if (players == null || players.Count == 0) return;

            Player targetPlayer = players[0];
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                if (p?.equipmentManager == null) continue;
                if (reward.equipmentData.IsAllowedForJob(p.playerStatus?.JobName ?? ""))
                {
                    targetPlayer = p;
                    break;
                }
            }

            var instance = new EquipmentInstance(reward.equipmentData);
            targetPlayer.equipmentManager.Inventory.Add(instance);
            targetPlayer.equipmentManager.OnItemDropped?.Invoke(instance);
        }
    }
}
