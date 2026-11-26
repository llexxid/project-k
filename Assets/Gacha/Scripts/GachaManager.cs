using System;
using System.Collections.Generic;
using System.Linq;
using KingdomIdle.Balance;
using KingdomIdle.MageTower;
using PlayFab;
using Protocol.Message.Dto;
using Scripts.Core;
using Scripts.Core.Manager;
using Scripts.Core.Utils;
using UnityEngine;

namespace KingdomIdle.Gacha
{
    public class GachaManager : MonoBehaviour
    {
        public static GachaManager Instance { get; private set; }
        [SerializeField] private List<GachaTableSO> gachaTables;
        private bool _isPulling;
        private NetworkManager _pullNetwork;
        private long _pullNetworkGeneration, _pullAccountGeneration;
        private int _pullGeneration;
        private GameplayLoading.Scope _loading;
        public bool IsPulling { get => _isPulling && IsCurrentPull(_pullGeneration); private set => _isPulling = value; }
        private bool IsCurrentPull(int generation) => generation == _pullGeneration && _pullNetwork != null &&
            _pullNetwork == NetworkManager.Instance && _pullNetworkGeneration == _pullNetwork.AccountGeneration &&
            _pullAccountGeneration == LocalProgression.AccountGeneration;
        public event Action<bool> OnPullStateChanged;
        /// <summary>마지막 서버 거절·적용 실패. 성공하면 null.</summary>
        public PlayFabError LastFailure { get; private set; }
        public const int EpicPityLimit = 40;
        public int EpicPityCount => LocalProgression.State.EquipmentPity;
        public int EpicPityRemaining => EpicPityLimit - EpicPityCount;
        public IReadOnlyList<GachaTableSO> GetAllTables() => gachaTables.Where(t => t != null && t.isImplemented && (t.gachaType == eGachaType.Equipment || t.gachaType == eGachaType.Skill) && t.costCurrency == eCurrency.AncientCoin).ToList();
        private void Awake() { if (Instance != null && Instance != this) { Destroy(gameObject); return; } Instance = this; DontDestroyOnLoad(gameObject); }
        private void OnDestroy() { EndLoading(_loading); if (Instance == this) Instance = null; }
        private void EndLoading(GameplayLoading.Scope loading)
        {
            loading?.Dispose();
            if (ReferenceEquals(_loading, loading)) _loading = null;
        }
        private const string PriceIncreaseMessage = "네트워크가 불안정해 뽑기를 진행하지 못했습니다. 다시 시도해 주세요.";

        private static long PullBalance(eCurrency currency, bool confirmed) => confirmed
            ? LocalProgression.Balance(currency) : EconomyBatchProgression.GetDisplayBalance(currency);

        public int GetTicketCost(GachaTableSO table, int count) => TicketCost(table, count, false);
        private static int TicketCost(GachaTableSO table, int count, bool confirmed) => table != null && (count == 1 || count == 10)
            ? (int)Math.Min(count, PullBalance(table.gachaType == eGachaType.Skill ? eCurrency.MageTicket : eCurrency.EquipmentTicket, confirmed)) : 0;
        public int GetTotalCost(GachaTableSO table, int count) => TotalCost(table, count, false);
        private static int TotalCost(GachaTableSO table, int count, bool confirmed) => table != null && (count == 1 || count == 10)
            ? 50 * (count - TicketCost(table, count, confirmed)) : -1;
        public bool CanPull(GachaTableSO table) => CanPullMulti(table, 1);
        public bool IsEquipmentInventoryFull(GachaTableSO table) => table != null && table.gachaType == eGachaType.Equipment &&
            EquipmentManager.IsInstanceInventory && LocalProgression.State.Equipment.Count >= EquipmentManager.InventoryCapacity;
        public bool IsPendingDraw(GachaTableSO table, int count) => table?.gachaType == eGachaType.Equipment &&
            LocalProgression.State.PendingEquipmentDrawId != null && LocalProgression.State.PendingEquipmentDrawCount == count;
        public bool CanPullMulti(GachaTableSO table, int count) => !IsPulling && CheckPull(table, count) == null;
        public string PullFailure(GachaTableSO table, int count) => IsPulling ? "이미 뽑기가 진행 중입니다." :
            CheckPull(table, count) ?? "뽑기 데이터를 준비 중입니다. 잠시 후 다시 시도해 주세요.";

        private string CheckPull(GachaTableSO table, int count, int? maxCoinCost = null)
        {
            if (table == null || !table.isImplemented || (count != 1 && count != 10) || table.costCurrency != eCurrency.AncientCoin ||
                (table.gachaType != eGachaType.Equipment && table.gachaType != eGachaType.Skill)) return "뽑기 구성을 확인할 수 없습니다.";
            if (table.gachaType == eGachaType.Equipment && LocalProgression.State.PendingEquipmentDrawId != null)
                return IsPendingDraw(table, count) && EquipmentManager.Instance != null ? null : "이전 뽑기 결과를 먼저 확인하세요.";

            bool confirmed = maxCoinCost.HasValue;
            int cost = TotalCost(table, count, confirmed);
            if (confirmed && cost > maxCoinCost.Value) return PriceIncreaseMessage;
            if (IsEquipmentInventoryFull(table)) return "장비 보관함이 가득 찼습니다. 빈 칸을 만든 후 다시 뽑아 주세요.";
            long coins = PullBalance(eCurrency.AncientCoin, confirmed);
            if (coins < cost) return $"고대주화가 {cost - coins:N0}개 부족합니다.";
            if (table.gachaType == eGachaType.Equipment && EquipmentManager.Instance == null)
                return "장비 정보를 준비 중입니다. 잠시 후 다시 시도해 주세요.";
            return null;
        }

        /// <summary>
        /// 서버 가챠(OnGachaEquipmentClassFragment / OnGachaSkillArcaneKnowledge)를 요청하고, 응답에 들어온 플레이어 데이터
        /// (재화·천장·장비 인벤토리·마탑 상태)를 플레이어 상태에 그대로 쓴다. 클라이언트는 굴리지도 차감하지도 않는다.
        /// 결과 팝업은 서버 Rewards로만 만든다. 로컬 검사는 안내용이고, 성공 콜백은 저장이 끝난 뒤 부른다.
        /// </summary>
        public void TryPull(GachaTableSO table, int count, Action<List<GachaRewardEntry>> onSuccess, Action<string> onError)
        {
            var guideAction = table != null && table.gachaType == eGachaType.Equipment ? Direction.GuideAction.EquipmentPullOnce : Direction.GuideAction.SkillPullOnce;
            if (!IsPendingDraw(table,count) && !Direction.GameDirectInteraction.CanPerform(guideAction, count)) { onError?.Invoke("현재 안내의 지정된 실습만 진행할 수 있습니다."); return; }
            if (!CanPullMulti(table, count)) { onError?.Invoke(PullFailure(table, count)); return; }
            var network = NetworkManager.Instance;
            if (network == null || !network.IsGameDataReady) { onError?.Invoke("서버 로그인이 필요합니다."); return; }
            string guideReceipt = Direction.GameDirectInteraction.CaptureReceipt(guideAction, count);
            int maxCoinCost = GetTotalCost(table, count);
            long coinsBefore = 0;
            string ConfirmPull()
            {
                coinsBefore = LocalProgression.Balance(eCurrency.AncientCoin);
                return CheckPull(table, count, maxCoinCost);
            }
            _pullNetwork = network;
            _pullNetworkGeneration = network.AccountGeneration;
            _pullAccountGeneration = LocalProgression.AccountGeneration;
            int generation = ++_pullGeneration;
            EndLoading(_loading);
            IsPulling = true; LastFailure = null; OnPullStateChanged?.Invoke(true);
            _loading = GameplayLoading.Begin("gacha");
            if (table.gachaType == eGachaType.Equipment)
            {
                var request = new OnGachaEquipmentClassFragmentRequestDTO { Count = count };
                string PrepareEquipment(OnGachaEquipmentClassFragmentRequestDTO pending)
                {
                    string failure = ConfirmPull();
                    if (failure != null) return failure;
                    if (!EquipmentManager.IsInstanceInventory && LocalProgression.State.PendingEquipmentDrawId == null) return null;
                    if (LocalProgression.State.PendingEquipmentDrawId == null && !LocalProgression.Execute("equipment-draw-intent", state =>
                    {
                        state.PendingEquipmentDrawId = Guid.NewGuid().ToString("N");
                        state.PendingEquipmentDrawSequence = state.NextEquipmentDrawSequence;
                        state.PendingEquipmentDrawCount = count;
                        return true;
                    })) return "뽑기 요청을 저장하지 못했습니다.";
                    var saved = LocalProgression.State;
                    pending.RequestId = saved.PendingEquipmentDrawId;
                    pending.ExpectedDrawSequence = saved.PendingEquipmentDrawSequence;
                    return null;
                }
                void Send(bool mayRetry)
                {
                    void Success(OnGachaEquipmentClassFragmentResponseDTO response)
                    {
                        if (!IsCurrentPull(generation)) return;
                        if (request.RequestId != null && (response.GachaState == null || response.GachaState.NextEquipmentDrawSequence <= request.ExpectedDrawSequence))
                        {
                            RejectServerResult(table, count, NetworkManager.CreateError("InvalidResponse", "뽑기 처리 순서를 확인할 수 없습니다."), onError);
                            return;
                        }
                        ApplyServerResult(table, count, guideAction, guideReceipt, coinsBefore, response.Rewards, response.Currency,
                            response.GachaState, response.Equipment, null, onSuccess, onError);
                    }
                    void Failure(PlayFabError error)
                    {
                        if (!IsCurrentPull(generation)) return;
                        if (mayRetry && request.RequestId != null && !NetworkManager.TryGetBusinessError(error,out _) &&
                            NetworkManager.GetFailureKind(error) is "Timeout" or "Transport")
                        {
                            var users = UserManager.Instance;
                            if (users != null)
                            {
                                users.RefreshFromServer(ok =>
                                {
                                    if (!IsCurrentPull(generation)) return;
                                    if (ok && !EconomyBatchProgression.HasUnknownResult) Send(false);
                                    else RejectServerResult(table, count, error, onError);
                                });
                                return;
                            }
                        }
                        RejectServerResult(table, count, error, onError);
                    }
                    if (mayRetry) network.CallWithPreflight<OnGachaEquipmentClassFragmentRequestDTO, OnGachaEquipmentClassFragmentResponseDTO>(request, PrepareEquipment, Success, Failure);
                    else network.Call<OnGachaEquipmentClassFragmentRequestDTO, OnGachaEquipmentClassFragmentResponseDTO>(request, Success, Failure);
                }
                Send(true);
            }
            else
            {
                network.CallWithPreflight<OnGachaSkillArcaneKnowledgeRequestDTO, OnGachaSkillArcaneKnowledgeResponseDTO>(new OnGachaSkillArcaneKnowledgeRequestDTO { Count = count }, _ => ConfirmPull(),
                    response => { if (IsCurrentPull(generation)) ApplyServerResult(table, count, guideAction, guideReceipt, coinsBefore, response.Rewards, response.Currency, null, null, response.MageTowerState, onSuccess, onError); },
                    error => { if (IsCurrentPull(generation)) RejectServerResult(table, count, error, onError); });
            }
        }

        /// <summary>응답의 플레이어 데이터를 한 트랜잭션으로 쓴다. 안내 영수증은 로컬 기능이라 같은 트랜잭션에 남긴다. 퀘스트(GachaUse)는 서버가 집계한다.</summary>
        private void ApplyServerResult(GachaTableSO table, int count, Direction.GuideAction guideAction, string guideReceipt, long coinsBefore, List<GachaRewardDTO> rewards,
            CurrencyQueryDTO currency, GachaStateDTO gacha, EquipmentInventoryDTO inventory, MageTowerStateDTO mage,
            Action<List<GachaRewardEntry>> onSuccess, Action<string> onError)
        {
            var loading = _loading;
            bool equipment = table.gachaType == eGachaType.Equipment;
            var loot = KingdomIdle.Rewards.LootNotifications.Capture();
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
                        ClearDraw(s);
                    }
                    else UserManager.ApplyMageTower(s, mage);
                    Direction.GameDirectInteraction.RecordSuccess(s, guideAction, guideReceipt);
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
                EndLoading(loading);
                onError?.Invoke("서버 결과를 저장하지 못했습니다. 다시 로그인하면 서버 값으로 맞춰집니다.");
                return;
            }
            KingdomIdle.Rewards.LootNotifications.PublishGacha(loot, rewards, "gacha:" + GetInstanceID() + ":" + _pullGeneration);
            EquipmentManager.Instance?.RestoreEquipment();
            MageTowerManager.Instance?.NotifyCommitted();
            QuestManager.Instance?.RequestRefresh("gacha");
            if (rewards != null && rewards.Any(reward => reward?.Disposition == EquipmentDisposition.Mail))
                GameManager.Instance?.Mailbox?.RefreshAfterDelivery("equipment-gacha");
            EndLoading(loading);
            onSuccess?.Invoke(entries);
        }

        private static void ClearDraw(ProgressionState state)
        {
            state.PendingEquipmentDrawId = null; state.PendingEquipmentDrawCount = 0; state.PendingEquipmentDrawSequence = 0;
        }

        private void RejectServerResult(GachaTableSO table, int count, PlayFabError error, Action<string> onError)
        {
            _loading ??= GameplayLoading.Begin("gacha");
            var loading = _loading;
            LastFailure = error;
            int generation = _pullGeneration;
            void Complete(string message)
            {
                if (!IsCurrentPull(generation)) return;
                IsPulling = false;
                EndLoading(loading);
                OnPullStateChanged?.Invoke(false);
                onError?.Invoke(message);
            }
            int code = NetworkManager.TryGetBusinessError(error, out var business) ? (int)business.ErrorCode : 0;
            string kind = NetworkManager.GetFailureKind(error);
            Debug.LogWarning($"[Gacha] {table.gachaType} count={count} rejected code={code} kind={kind} message={error?.ErrorMessage}");
            if (kind == "Preflight") { Complete(error.ErrorMessage); return; }
            if (table.gachaType == eGachaType.Equipment && LocalProgression.State.PendingEquipmentDrawId != null)
            {
                if (code == (int)Protocol.Message.Data.eErrorCode.EquipmentDrawConflict ||
                    code == (int)Protocol.Message.Data.eErrorCode.EquipmentDrawSequenceMismatch)
                {
                    var users = UserManager.Instance;
                    if (users != null) users.RefreshFromServer(ok =>
                    {
                        if (!IsCurrentPull(generation)) return;
                        if (ok) LocalProgression.Execute("equipment-draw-resolved", s => { ClearDraw(s); return true; });
                        Complete(ok ? "다른 뽑기 기록이 확인되어 보유 장비와 잔액을 갱신했습니다." : "뽑기 결과를 확인하지 못했습니다. 다시 확인해 주세요.");
                    });
                    else Complete("뽑기 기록을 확인하려면 다시 로그인해 주세요.");
                    return;
                }
                if (code == (int)Protocol.Message.Data.eErrorCode.EquipmentCapacity ||
                    code == (int)Protocol.Message.Data.eErrorCode.eNotEnoughAncientCoin || code == (int)Protocol.Message.Data.eErrorCode.NumericLimit)
                    LocalProgression.Execute("equipment-draw-rejected", s => { ClearDraw(s); return true; });
                else
                {
                    Complete("뽑기 결과를 확인하지 못했습니다. 뽑기 메뉴에서 같은 요청의 결과를 확인하세요."); return;
                }
            }
            if (code == (int)Protocol.Message.Data.eErrorCode.EquipmentCapacity && table.gachaType == eGachaType.Equipment)
            {
                Complete("장비 보관함이 가득 찼습니다. 빈 칸을 만든 후 다시 뽑아 주세요.");
                return;
            }
            bool uncertain = code == 0 && (kind == "Timeout" || kind == "Transport" || kind == "Function" ||
                kind == "InvalidResponse" || kind == "ApplyFailed" ||
                error?.Error == PlayFabErrorCode.CloudScriptAzureFunctionsExecutionTimeLimitExceeded ||
                error?.Error == PlayFabErrorCode.CloudScriptExecutionTimeLimitExceeded || error?.Error == PlayFabErrorCode.ConnectionError);
            if (uncertain)
            {
                // 응답 유실: 서버가 이미 처리했을 수 있으므로 재전송하지 않고 플레이어 데이터를 다시 받는다(서버 계약).
                // 조회가 끝날 때까지 같은 요청의 입력 잠금을 유지한다. 조회 상태로 결과 카드를 추정하지 않는다.
                IsPulling = true;
                var users = UserManager.Instance;
                if (users != null)
                {
                    users.RefreshFromServer(ok =>
                    {
                        if (!IsCurrentPull(generation)) return;
                        if (ok && table.gachaType == eGachaType.Equipment && EquipmentManager.IsInstanceInventory)
                            GameManager.Instance?.Mailbox?.RefreshAfterDelivery("equipment-gacha-recovered");
                        Complete(ok ? "서버 응답이 늦어 보유 상태를 다시 불러왔습니다. 보유 목록을 확인해 주세요." :
                            "뽑기 결과를 확인하지 못했습니다. 다시 로그인해 보유 목록을 확인해 주세요.");
                    });
                    return;
                }
                Complete("뽑기 결과를 확인하지 못했습니다. 다시 로그인해 보유 목록을 확인해 주세요.");
                return;
            }
            Complete(NetworkManager.DescribeFailure(error, "뽑기"));
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
                            entries.Add(new GachaRewardEntry { nameKor = data.equipmentName, icon = data.icon, rewardType = eGachaRewardType.Equipment, equipmentData = data, amount = 1,
                                mailedAmount = reward.Disposition == EquipmentDisposition.Mail ? 1 : 0 });
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
            int equipment = 0, pending = 0, reserve = 0, mailed = 0, dismantled = 0, fragments = 0, skills = 0, duplicates = 0, knowledge = 0;
            foreach (var reward in rewards)
            {
                if (reward == null) continue;
                if (reward.Kind == RewardKind.Equipment)
                {
                    if (reward.Disposition == EquipmentDisposition.Pending) pending++;
                    else if (reward.Disposition == EquipmentDisposition.Reserve) reserve++;
                    else if (reward.Disposition == EquipmentDisposition.Mail) mailed++;
                    else if (reward.Disposition == EquipmentDisposition.AutoDismantled) dismantled++;
                    else equipment++;
                }
                else if (reward.Kind == RewardKind.Skill) { skills++; if (reward.IsDuplicate) duplicates++; }
                else if (reward.CurrencyType == Protocol.Message.Data.eCurrency.ClassFragment) fragments += (int)reward.Amount;
                else knowledge += (int)reward.Amount;
            }
            return $"equipment{equipment}+pending{pending}+reserve{reserve}+mail{mailed}+dismantled{dismantled},fragments{fragments},skills{skills}(dup{duplicates}),knowledge{knowledge}";
        }
    }
}
