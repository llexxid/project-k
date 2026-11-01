using System;
using System.Collections.Generic;
using System.Linq;
using KingdomIdle.Balance;
using KingdomIdle.MageTower;
using PlayFab;
using Protocol.Message.Dto;
using Scripts.Core;
using Scripts.Core.Manager;
using UnityEngine;

namespace KingdomIdle.Gacha
{
    public class GachaManager : MonoBehaviour
    {
        public static GachaManager Instance { get; private set; }
        [SerializeField] private List<GachaTableSO> gachaTables;
        public bool IsPulling { get; private set; }
        public event Action<bool> OnPullStateChanged;
        /// <summary>마지막 서버 거절·적용 실패. 성공하면 null.</summary>
        public PlayFabError LastFailure { get; private set; }
        public const int EpicPityLimit = 40;
        public int EpicPityCount => LocalProgression.State.EquipmentPity;
        public int EpicPityRemaining => EpicPityLimit - EpicPityCount;
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

        /// <summary>
        /// 서버 가챠(OnGachaEquipmentClassFragment / OnGachaSkillArcaneKnowledge)를 요청하고, 응답에 들어온 플레이어 데이터
        /// (재화·천장·장비 인벤토리·마탑 상태)를 플레이어 상태에 그대로 쓴다. 클라이언트는 굴리지도 차감하지도 않는다.
        /// 결과 팝업은 서버 Rewards로만 만든다. 로컬 검사는 안내용이고, 성공 콜백은 저장이 끝난 뒤 부른다.
        /// </summary>
        public void TryPull(GachaTableSO table, int count, Action<List<GachaRewardEntry>> onSuccess, Action<string> onError)
        {
            var guideAction = table != null && table.gachaType == eGachaType.Equipment ? Direction.GuideAction.EquipmentPullOnce : Direction.GuideAction.SkillPullOnce;
            if (!Direction.GameDirectInteraction.CanPerform(guideAction, count)) { onError?.Invoke("현재 안내의 지정된 실습만 진행할 수 있습니다."); return; }
            if (!CanPullMulti(table, count)) { onError?.Invoke(PullFailure(table, count)); return; }
            var network = NetworkManager.Instance;
            if (network == null || !network.IsGameDataReady) { onError?.Invoke("서버 로그인이 필요합니다."); return; }
            long coinsBefore = LocalProgression.Balance(eCurrency.AncientCoin);
            IsPulling = true; LastFailure = null; OnPullStateChanged?.Invoke(true);
            if (table.gachaType == eGachaType.Equipment)
            {
                network.Call<OnGachaEquipmentClassFragmentRequestDTO, OnGachaEquipmentClassFragmentResponseDTO>(new OnGachaEquipmentClassFragmentRequestDTO { Count = count },
                    response => ApplyServerResult(table, count, guideAction, coinsBefore, response.Rewards, response.Currency, response.GachaState, response.Equipment, null, onSuccess, onError),
                    error => RejectServerResult(table, count, error, onError));
            }
            else
            {
                network.Call<OnGachaSkillArcaneKnowledgeRequestDTO, OnGachaSkillArcaneKnowledgeResponseDTO>(new OnGachaSkillArcaneKnowledgeRequestDTO { Count = count },
                    response => ApplyServerResult(table, count, guideAction, coinsBefore, response.Rewards, response.Currency, null, null, response.MageTowerState, onSuccess, onError),
                    error => RejectServerResult(table, count, error, onError));
            }
        }

        /// <summary>응답의 플레이어 데이터를 한 트랜잭션으로 쓴다. 안내 영수증은 로컬 기능이라 같은 트랜잭션에 남긴다. 퀘스트(GachaUse)는 서버가 집계한다.</summary>
        private void ApplyServerResult(GachaTableSO table, int count, Direction.GuideAction guideAction, long coinsBefore, List<GachaRewardDTO> rewards,
            CurrencyQueryDTO currency, GachaStateDTO gacha, EquipmentInventoryDTO inventory, MageTowerStateDTO mage,
            Action<List<GachaRewardEntry>> onSuccess, Action<string> onError)
        {
            bool equipment = table.gachaType == eGachaType.Equipment;
            bool saved;
            try
            {
                saved = LocalProgression.Execute("server-gacha-" + table.gachaType, s =>
                {
                    UserManager.ApplyCurrency(s, currency);
                    if (equipment)
                    {
                        UserManager.ApplyGachaState(s, gacha);
                        UserManager.ApplyEquipment(s, inventory);
                    }
                    else UserManager.ApplyMageTower(s, mage);
                    Direction.GameDirectInteraction.RecordSuccess(s, guideAction);
                    return true;
                });
            }
            finally { IsPulling = false; OnPullStateChanged?.Invoke(false); }
            var entries = BuildEntries(rewards);
            var state = LocalProgression.State;
            Debug.Log($"[Gacha] {table.gachaType} count={count} coins {coinsBefore}->{currency?.AncientCoin} pity={(equipment ? (gacha?.EquipmentPity ?? -1) : state.EquipmentPity)} rewards={DescribeRewards(rewards)} entries={entries.Count}" +
                $" | server Inventory={(equipment ? UserManager.DescribeInventory(inventory) : "-")} Skills={(equipment ? -1 : mage?.Skills?.Count ?? -1)}" +
                $" | state AncientCoin={LocalProgression.Balance(eCurrency.AncientCoin)} ClassFragment={LocalProgression.Balance(eCurrency.ClassFragment)} ArcaneKnowledge={LocalProgression.Balance(eCurrency.ArcaneKnowledge)} Inventory={UserManager.DescribeInventory(state)} Skills={state.MageSkills.Count} Pity={state.EquipmentPity} saved={saved}");
            if (!saved)
            {
                Direction.GameDirectInteraction.ReportSaveFailure();
                LastFailure = NetworkManager.CreateError("ApplyFailed", "서버 가챠 결과를 저장하지 못했습니다: " + LocalProgression.LastError);
                onError?.Invoke("서버 결과를 저장하지 못했습니다. 다시 로그인하면 서버 값으로 맞춰집니다.");
                return;
            }
            EquipmentManager.Instance?.RestoreEquipment();
            MageTowerManager.Instance?.NotifyCommitted();
            QuestManager.Instance?.RequestRefresh("gacha");
            onSuccess?.Invoke(entries);
        }

        private void RejectServerResult(GachaTableSO table, int count, PlayFabError error, Action<string> onError)
        {
            IsPulling = false; LastFailure = error; OnPullStateChanged?.Invoke(false);
            int code = NetworkManager.TryGetBusinessError(error, out var business) ? (int)business.ErrorCode : 0;
            string kind = NetworkManager.GetFailureKind(error);
            Debug.LogWarning($"[Gacha] {table.gachaType} count={count} rejected code={code} kind={kind} message={error?.ErrorMessage}");
            if (code == 0 && (kind == "Timeout" || kind == "Transport"))
            {
                // 응답 유실: 서버가 이미 처리했을 수 있으므로 재전송하지 않고 플레이어 데이터를 다시 받는다(서버 계약).
                var users = UserManager.Instance;
                if (users != null)
                {
                    users.RefreshFromServer(ok => onError?.Invoke(ok ? "서버 응답이 늦어 결과를 서버에서 다시 불러왔습니다. 보유 목록을 확인해 주세요." : NetworkManager.DescribeFailure(error, "뽑기")));
                    return;
                }
            }
            onError?.Invoke(NetworkManager.DescribeFailure(error, "뽑기"));
        }

        /// <summary>서버 Rewards를 결과 팝업 항목으로 바꾼다. 장비는 ItemCode에서 정의 코드를 읽고, 중복 스킬은 파편 수(30)를 amount로 둔다.</summary>
        private static List<GachaRewardEntry> BuildEntries(List<GachaRewardDTO> rewards)
        {
            var entries = new List<GachaRewardEntry>();
            if (rewards == null) return entries;
            var equipmentManager = EquipmentManager.Instance;
            var mage = MageTowerManager.Instance;
            foreach (var reward in rewards)
            {
                if (reward == null) continue;
                switch (reward.Kind)
                {
                    case RewardKind.Equipment:
                    {
                        int code = reward.ItemCode.HasValue ? (int)reward.ItemCode.Value.GetItemCode() : reward.DefinitionId ?? 0;
                        var data = equipmentManager?.GetData(code);
                        if (reward.Disposition == EquipmentDisposition.AutoDismantled)
                            entries.Add(new GachaRewardEntry { nameKor = "자동 분해 · 강화석", icon = data?.icon, rewardType = eGachaRewardType.Currency, currency = eCurrency.EquipmentStone, amount = (int)Math.Min(int.MaxValue, Math.Max(0, reward.GrantedEquipmentStone)) });
                        else if (data != null)
                            entries.Add(new GachaRewardEntry { nameKor = data.equipmentName, icon = data.icon, rewardType = eGachaRewardType.Equipment, equipmentData = data, amount = 1 });
                        else
                            Debug.LogWarning("[Gacha] 클라이언트 카탈로그에 없는 장비 코드 " + code + "는 결과 팝업에서 건너뛴다.");
                        break;
                    }
                    case RewardKind.Skill:
                    {
                        int id = reward.DefinitionId ?? -1;
                        var so = mage?.GetSkillById(id);
                        string name = so != null ? so.nameKor : "스킬 #" + id;
                        entries.Add(new GachaRewardEntry
                        {
                            nameKor = reward.IsDuplicate ? name + " 파편" : name, icon = so?.icon, rewardType = eGachaRewardType.Skill, skillId = id,
                            amount = reward.IsDuplicate ? Math.Max(1, reward.GrantedFragments) : 1
                        });
                        break;
                    }
                    default:
                    {
                        eCurrency type = reward.CurrencyType.HasValue ? (eCurrency)(int)reward.CurrencyType.Value : eCurrency.ArcaneKnowledge;
                        int amount = (int)Math.Min(int.MaxValue, Math.Max(0, reward.Amount));
                        string name = type == eCurrency.ClassFragment ? "전직 파편 " + amount + "개" : type == eCurrency.ArcaneKnowledge ? "비전 지식 " + amount + "개" : type + " " + amount;
                        entries.Add(new GachaRewardEntry { nameKor = name, rewardType = eGachaRewardType.Currency, currency = type, amount = amount });
                        break;
                    }
                }
            }
            return entries;
        }

        private static string DescribeRewards(List<GachaRewardDTO> rewards)
        {
            if (rewards == null) return "-";
            int equipment = 0, pending = 0, reserve = 0, dismantled = 0, fragments = 0, skills = 0, duplicates = 0, knowledge = 0;
            foreach (var reward in rewards)
            {
                if (reward == null) continue;
                if (reward.Kind == RewardKind.Equipment)
                {
                    if (reward.Disposition == EquipmentDisposition.Pending) pending++;
                    else if (reward.Disposition == EquipmentDisposition.Reserve) reserve++;
                    else if (reward.Disposition == EquipmentDisposition.AutoDismantled) dismantled++;
                    else equipment++;
                }
                else if (reward.Kind == RewardKind.Skill) { skills++; if (reward.IsDuplicate) duplicates++; }
                else if (reward.CurrencyType == Protocol.Message.Data.eCurrency.ClassFragment) fragments += (int)reward.Amount;
                else knowledge += (int)reward.Amount;
            }
            return $"equipment{equipment}+pending{pending}+reserve{reserve}+dismantled{dismantled},fragments{fragments},skills{skills}(dup{duplicates}),knowledge{knowledge}";
        }
    }
}
