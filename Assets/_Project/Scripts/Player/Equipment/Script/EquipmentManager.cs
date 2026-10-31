using System;
using System.Collections.Generic;
using System.Linq;
using KingdomIdle.Balance;
using PlayFab;
using Scripts.Core;
using Scripts.Core.Manager;
using UnityEngine;
using Dto = Protocol.Message.Dto;

public enum GetEffect { None, Gacha, Popup }
/// <summary>
/// 장비 매니저. 장비 상태의 원본은 서버다. 로그인·사냥·클리어·가챠·장비 API 응답의 Equipment(EquipmentInventoryDTO)를
/// UserManager.ApplyEquipment가 플레이어 상태(ProgressionState.Equipment·PendingEquipment·LegacyEquipment·AutoDismantleMask)에
/// 그대로 쓰고, RestoreEquipment가 그 상태를 런타임 인스턴스로 복원한다.
/// 잠금·장착·해제·강화·분해·자동 분해·수령은 로컬에서 계산하지 않고 서버 API를 호출한 뒤 응답의 Equipment를 적용한다.
/// 각 조작은 요청이 나가면 Pending을 돌려주고 OnActionCompleted로 결과를 알린다(StatEnhanceManager 패턴). 로그 태그 [Equipment].
/// </summary>
public class EquipmentManager : MonoBehaviour
{
    public static EquipmentManager Instance;
    public event Action<EquipmentInstance> OnItemDropped, OnEnhanced;
    public event Action<Player, eEquipmentSlot, EquipmentInstance> OnEquipped, OnUnequipped;
    [SerializeField] private EquipmentDatabase _database;
    [SerializeField] private EquipmentDropTableSO _dropTable;
    private EquipmentInventory _inventory = new();
    private readonly Dictionary<int, Player> _players = new();
    public EquipmentInventory Inventory => _inventory;
    public const int Capacity = 300, PendingCapacity = 100;
    public int AvailableSlots => Math.Max(0, Capacity - LocalProgression.State.Equipment.Count);
    /// <summary>서버 자동 분해 등급 마스크(bit0 일반, bit1 레어, bit2 에픽). 로그인·장비 응답으로 갱신된다.</summary>
    public int AutoDismantleMask => LocalProgression.State.AutoDismantleMask;

    /// <summary>
    /// 조작 결과. Pending은 서버 요청이 나갔고 OnActionCompleted로 결과가 온다는 뜻이다. 나머지는 요청 전 안내용 가드다.
    /// Success·ChanceFailed·SaveFailed는 옛 로컬 처리 호출자(QA 스크립트) 호환용으로 남긴다.
    /// </summary>
    public enum EnhancementResult { Success, ChanceFailed, NotEnoughMaterials, MaxLevel, InvalidItem, SaveFailed, Pending, Busy, NetworkNotReady, NotAllowed, AlreadyEquipped, NotEquipped, NoFreeCopy, Nothing }
    public enum ActionKind { Lock, Equip, Unequip, Enhance, Dismantle, AutoDismantle, ClaimPending, ClaimReserve, Resync }
    /// <summary>완료 이벤트 인자. ItemId는 응답 적용 뒤 UI가 다시 보여 줄 인스턴스(장착·강화·잠금 대상)다.</summary>
    public sealed class ActionInfo
    {
        public ActionKind Kind;
        public bool Ok;
        public string ItemId;
        public int CharacterIndex = -1;
        public long GrantedEquipmentStone, SpentEquipmentStone;
        public int ErrorCode;
        public string Failure;
        public string LogLine;
    }

    public bool IsBusy { get; private set; }
    /// <summary>마지막 서버 거절 또는 적용 실패. Business 오류면 CustomData에 서버 ErrorResponseDTO가 들어 있다.</summary>
    public PlayFabError LastFailure { get; private set; }
    public event Action<bool> OnBusyChanged;
    public event Action<ActionInfo> OnActionCompleted;

    private void Awake() { if (Instance != null && Instance != this) { Destroy(gameObject); return; } Instance = this; }
    private void OnDestroy() { if (Instance == this) Instance = null; }
    public void RegisterPlayer(Player player) { if (player != null) _players[player.PlayerIndex] = player; }
    public void ClearPlayer() => _players.Clear();
    public IEnumerable<string> GetCurrentJobNames() => _players.Values.Where(p => p != null).Select(p => p.playerStatus.JobName);
    public List<EquipmentData> GetByRarity(eEquipmentRarity rarity) => _database != null ? _database.GetEquipmentsByRarity(rarity) : new List<EquipmentData>();
    public List<EquipmentData> GetRewardPool(eEquipmentRarity rarity) => GetByRarity(rarity).FindAll(item => item.CanBeRewarded);
    public EquipmentData GetData(int code) => _database?.GetEquipmentByCode(code);

    /// <summary>플레이어 상태(서버에서 받은 장비 인벤토리)를 런타임 인스턴스로 복원한다. 같은 Id의 인스턴스는 재사용해 열린 UI가 깨지지 않게 한다.</summary>
    public void RestoreEquipment()
    {
        var previous = _inventory.Items.ToDictionary(x => x.instanceId);
        var next = new EquipmentInventory();
        foreach (var saved in LocalProgression.State.Equipment)
        {
            var data = GetData(saved.Code);
            if (data == null) { Debug.LogError("[Equipment] Preserved unknown item code: " + saved.Code); continue; }
            if (!previous.TryGetValue(saved.Id, out var item)) item = new EquipmentInstance(data, saved.Id);
            item.enhancementLevel = saved.Level; item.equipmentPlayerIndex = saved.Player; item.IsLocked = saved.Locked;
            item.expiresUtc = saved.ExpiresUtc; item.stonesSpent = saved.EnhancementStonesSpent;
            next.Add(item);
        }
        _inventory = next;
        foreach (var pair in _players)
            pair.Value?.PlayerEquipmentManager?.RestoreSelection(_inventory.Items.FirstOrDefault(x => x.equipmentPlayerIndex == pair.Key));
    }

    // ── 서버 조작 ─────────────────────────────────────────────────────
    // 모든 조작은 같은 골격이다: 안내용 가드 → IsBusy → NetworkManager.Call → 응답 Equipment를 플레이어 상태에 적용 → RestoreEquipment → OnActionCompleted.
    // 서버가 받지 않는 값은 보내지 않고, 잔액·보유·직업 검사는 안내용이다. 최종 판정은 서버다.

    private EnhancementResult Gate(out NetworkManager network)
    {
        network = NetworkManager.Instance;
        if (IsBusy) return EnhancementResult.Busy;
        if (network == null || !network.IsGameDataReady) return EnhancementResult.NetworkNotReady;
        return EnhancementResult.Pending;
    }

    private void Begin()
    {
        IsBusy = true; LastFailure = null;
        OnBusyChanged?.Invoke(true);
    }

    private void Complete(ActionInfo info)
    {
        IsBusy = false;
        OnBusyChanged?.Invoke(false);
        Notify(info);
    }

    // 한 구독자의 예외가 다른 구독자와 상태 갱신을 막지 않게 한다.
    private void Notify(ActionInfo info)
    {
        var handlers = OnActionCompleted;
        if (handlers == null) return;
        foreach (Action<ActionInfo> handler in handlers.GetInvocationList())
            try { handler(info); } catch (Exception exception) { Debug.LogException(exception); }
    }

    public static string ActionName(ActionKind kind) => kind switch
    {
        ActionKind.Lock => "잠금", ActionKind.Equip => "장착", ActionKind.Unequip => "해제", ActionKind.Enhance => "강화",
        ActionKind.Dismantle => "분해", ActionKind.AutoDismantle => "자동 분해 설정", ActionKind.ClaimPending => "대기함 수령",
        ActionKind.ClaimReserve => "추가 보관 수령", _ => "장비 조회"
    };

    /// <summary>요청 전 가드 결과를 사용자 문구로 바꾼다. Pending은 호출자가 "요청 중"으로 처리한다.</summary>
    public static string Describe(EnhancementResult result) => result switch
    {
        EnhancementResult.Pending => "요청 중입니다.",
        EnhancementResult.Busy => "이전 장비 요청을 처리 중입니다. 잠시 후 다시 시도해 주세요.",
        EnhancementResult.NetworkNotReady => "서버 로그인이 필요합니다.",
        EnhancementResult.InvalidItem => "장비를 찾을 수 없습니다. 목록을 다시 확인해 주세요.",
        EnhancementResult.NotAllowed => "이 병사의 직업으로는 장착할 수 없습니다.",
        EnhancementResult.AlreadyEquipped => "이미 장착 중인 장비입니다.",
        EnhancementResult.NotEquipped => "장착 중인 장비가 없습니다.",
        EnhancementResult.NoFreeCopy => "장착할 여분의 장비가 없습니다.",
        EnhancementResult.MaxLevel => "이미 최대 강화 레벨입니다.",
        EnhancementResult.NotEnoughMaterials => "강화석이 부족합니다.",
        EnhancementResult.Nothing => "처리할 장비가 없습니다.",
        _ => "장비 요청을 처리하지 못했습니다."
    };

    /// <summary>완료 결과를 사용자 문구로 바꾼다.</summary>
    public static string Describe(ActionInfo info)
    {
        if (info == null) return "";
        if (!info.Ok) return string.IsNullOrEmpty(info.Failure) ? ActionName(info.Kind) + "에 실패했습니다." : info.Failure;
        return info.Kind switch
        {
            ActionKind.Lock => "잠금 상태를 바꿨습니다.",
            ActionKind.Equip => "장착했습니다.",
            ActionKind.Unequip => "해제했습니다.",
            ActionKind.Enhance => $"강화 성공! 강화석 {KingdomIdle.UGUI.NumberNotation.Format(info.SpentEquipmentStone)}개 사용",
            ActionKind.Dismantle => $"강화석 {KingdomIdle.UGUI.NumberNotation.Format(info.GrantedEquipmentStone)}개 획득",
            ActionKind.AutoDismantle => "자동 분해 설정을 저장했습니다.",
            ActionKind.ClaimPending => "대기함 장비를 가방에 넣었습니다.",
            ActionKind.ClaimReserve => "추가 보관 장비를 가방에 넣었습니다.",
            _ => "장비 목록을 갱신했습니다."
        };
    }

    /// <summary>응답의 Equipment(와 강화석 잔액)를 플레이어 상태에 쓰고 인스턴스를 복원한다. 저장 실패는 ApplyFailed로 알린다.</summary>
    private void ApplyServerInventory(ActionInfo info, string op, string function, Dto.EquipmentInventoryDTO inventory, long? stone, string detail, Func<EquipmentInstance> resolve)
    {
        bool saved;
        try
        {
            saved = LocalProgression.Execute("server-equipment-" + op, s =>
            {
                UserManager.ApplyEquipment(s, inventory);
                if (stone.HasValue) s.Wallet[eCurrency.EquipmentStone] = Math.Max(0, stone.Value);
                return true;
            });
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            saved = false;
        }
        var state = LocalProgression.State;
        info.LogLine = $"[Equipment] {op} {detail} | server Inventory={UserManager.DescribeInventory(inventory)} Stone={(stone.HasValue ? stone.Value.ToString() : "-")} Mask={inventory?.AutoDismantleRarityMask ?? -1}" +
            $" | state Inventory={UserManager.DescribeInventory(state)} Stone={LocalProgression.Balance(eCurrency.EquipmentStone)} Mask={state.AutoDismantleMask} saved={saved}";
        Debug.Log(info.LogLine);
        if (!saved)
        {
            Direction.GameDirectInteraction.ReportSaveFailure();
            LastFailure = NetworkManager.CreateError("ApplyFailed", "서버 장비 결과를 저장하지 못했습니다: " + LocalProgression.LastError, function);
            info.Ok = false;
            info.Failure = "서버 결과를 저장하지 못했습니다. 다시 로그인하면 서버 값으로 맞춰집니다.";
            Complete(info);
            return;
        }
        RestoreEquipment();
        info.Ok = true;
        var target = resolve?.Invoke();
        info.ItemId = target?.instanceId ?? info.ItemId;
        if (info.Kind == ActionKind.Equip && target != null && _players.TryGetValue(info.CharacterIndex, out var equippedPlayer)) OnEquipped?.Invoke(equippedPlayer, eEquipmentSlot.Weapon, target);
        if (info.Kind == ActionKind.Unequip && _players.TryGetValue(info.CharacterIndex, out var unequippedPlayer)) OnUnequipped?.Invoke(unequippedPlayer, eEquipmentSlot.Weapon, target);
        if (info.Kind == ActionKind.Enhance && target != null) OnEnhanced?.Invoke(target);
        if (info.Kind == ActionKind.Dismantle || info.Kind == ActionKind.ClaimPending || info.Kind == ActionKind.ClaimReserve) OnItemDropped?.Invoke(null);
        Complete(info);
        QuestManager.Instance?.RequestRefresh("equipment");
    }

    /// <summary>서버 거절·전송 실패. 거절(Business)은 상태를 건드리지 않고 문구만 돌려준다. 응답 유실(Timeout/Transport)은 재전송하지 않고 서버 상태를 다시 받는다.</summary>
    private void RejectServerResult(ActionInfo info, string op, PlayFabError error)
    {
        LastFailure = error;
        int code = NetworkManager.TryGetBusinessError(error, out var business) ? (int)business.ErrorCode : 0;
        string kind = NetworkManager.GetFailureKind(error);
        info.LogLine = $"[Equipment] {op} rejected code={code} kind={kind} message={error?.ErrorMessage}";
        Debug.LogWarning(info.LogLine);
        info.Ok = false;
        info.ErrorCode = code;
        IsBusy = false;
        OnBusyChanged?.Invoke(false);
        if (code == 0 && (kind == "Timeout" || kind == "Transport") && UserManager.Instance != null)
        {
            UserManager.Instance.RefreshFromServer(ok =>
            {
                info.Failure = ok ? "서버 응답이 늦어 장비 상태를 서버에서 다시 불러왔습니다. 목록을 확인해 주세요." : NetworkManager.DescribeFailure(error, ActionName(info.Kind));
                Notify(info);
            });
            return;
        }
        info.Failure = NetworkManager.DescribeFailure(error, ActionName(info.Kind));
        Notify(info);
    }

    private bool Owns(EquipmentInstance item) => item?.baseData != null && _inventory.Items.Contains(item);

    private EquipmentInstance FindInStack(int code, int level, long expires, Func<EquipmentInstance, bool> prefer)
    {
        EquipmentInstance fallback = null;
        foreach (var item in _inventory.Items)
        {
            if (item.baseData == null || item.baseData.itemCode != code || item.enhancementLevel != level || item.expiresUtc != expires) continue;
            if (prefer == null || prefer(item)) return item;
            fallback ??= item;
        }
        return fallback;
    }

    // 잠금: 같은 묶음(정의·강화·만료)의 주 보관함·대기함·예비 전부에 적용된다. 서버가 요청의 IsLocked를 그대로 쓴다.
    public bool SetLocked(EquipmentInstance item, bool locked) => SetLockedEx(item, locked) == EnhancementResult.Pending;
    public EnhancementResult SetLockedEx(EquipmentInstance item, bool locked)
    {
        if (!Owns(item)) return EnhancementResult.InvalidItem;
        var gate = Gate(out var network); if (gate != EnhancementResult.Pending) return gate;
        var info = new ActionInfo { Kind = ActionKind.Lock, ItemId = item.instanceId, CharacterIndex = item.equipmentPlayerIndex ?? -1 };
        int code = item.baseData.itemCode, level = item.enhancementLevel; long expires = item.expiresUtc; string id = item.instanceId;
        var request = new Dto.OnSetEquipmentLockRequestDTO { ItemCode = item.ToItemCode(), IsLocked = locked };
        Begin();
        network.Call<Dto.OnSetEquipmentLockRequestDTO, Dto.OnSetEquipmentLockResponseDTO>(request,
            r => ApplyServerInventory(info, "lock", "OnSetEquipmentLock", r.Equipment, null, $"code=0x{request.ItemCode.Code:X} locked={locked}",
                () => _inventory.Items.FirstOrDefault(x => x.instanceId == id) ?? FindInStack(code, level, expires, null)),
            e => RejectServerResult(info, "lock", e));
        return EnhancementResult.Pending;
    }

    // 장착: 서버가 저장된 직업으로 호환을 검사하고(1018), 미장착 사본이 없으면 11, 그 캐릭터의 기존 장비는 자동 교체한다.
    public bool TryEquip(int playerIndex, EquipmentInstance item) => TryEquipEx(playerIndex, item) == EnhancementResult.Pending;
    public EnhancementResult TryEquipEx(int playerIndex, EquipmentInstance item)
    {
        if (!Owns(item) || !_players.TryGetValue(playerIndex, out var player)) return EnhancementResult.InvalidItem;
        // 안내용 사전 검사: 로컬 직업과 서버가 저장한 직업 둘 다 허용해야 한다(직업 변경은 아직 로컬이라 서로 다를 수 있다).
        if (!item.baseData.IsAllowedForJob(player.playerStatus.JobName)) return EnhancementResult.NotAllowed;
        string serverJob = UserManager.Instance?.GetServerJobName(playerIndex);
        if (!string.IsNullOrEmpty(serverJob) && !item.baseData.IsAllowedForJob(serverJob)) return EnhancementResult.NotAllowed;
        if (item.equipmentPlayerIndex == playerIndex) return EnhancementResult.AlreadyEquipped;
        bool freeCopy = false;
        foreach (var copy in _inventory.Items)
        {
            if (!copy.SameStack(item)) continue;
            if (copy.equipmentPlayerIndex == playerIndex) return EnhancementResult.AlreadyEquipped;
            freeCopy |= !copy.equipmentPlayerIndex.HasValue;
        }
        if (!freeCopy) return EnhancementResult.NoFreeCopy;
        var gate = Gate(out var network); if (gate != EnhancementResult.Pending) return gate;
        var info = new ActionInfo { Kind = ActionKind.Equip, ItemId = item.instanceId, CharacterIndex = playerIndex };
        int code = item.baseData.itemCode, level = item.enhancementLevel; long expires = item.expiresUtc;
        var request = new Dto.OnEquipEquipmentRequestDTO { ItemCode = item.ToItemCode(), CharacterIndex = playerIndex };
        Begin();
        network.Call<Dto.OnEquipEquipmentRequestDTO, Dto.OnEquipEquipmentResponseDTO>(request,
            r => ApplyServerInventory(info, "equip", "OnEquipEquipment", r.Equipment, null, $"code=0x{request.ItemCode.Code:X} char={playerIndex}",
                () => FindInStack(code, level, expires, x => x.equipmentPlayerIndex == playerIndex)),
            e => RejectServerResult(info, "equip", e));
        return EnhancementResult.Pending;
    }

    // 해제: 그 캐릭터가 장착한 묶음의 ItemCode를 보낸다. 장착 중이 아니면 서버가 1018이다.
    public void Unequip(int playerIndex) => UnequipEx(playerIndex);
    public EnhancementResult UnequipEx(int playerIndex)
    {
        if (!_players.TryGetValue(playerIndex, out var player)) return EnhancementResult.InvalidItem;
        var item = player.PlayerEquipmentManager?.GetSlotEquipment(eEquipmentSlot.Weapon);
        if (item == null || item.baseData == null) return EnhancementResult.NotEquipped;
        var gate = Gate(out var network); if (gate != EnhancementResult.Pending) return gate;
        var info = new ActionInfo { Kind = ActionKind.Unequip, ItemId = item.instanceId, CharacterIndex = playerIndex };
        int code = item.baseData.itemCode, level = item.enhancementLevel; long expires = item.expiresUtc;
        var request = new Dto.OnUnequipEquipmentRequestDTO { ItemCode = item.ToItemCode(), CharacterIndex = playerIndex };
        Begin();
        network.Call<Dto.OnUnequipEquipmentRequestDTO, Dto.OnUnequipEquipmentResponseDTO>(request,
            r => ApplyServerInventory(info, "unequip", "OnUnequipEquipment", r.Equipment, null, $"code=0x{request.ItemCode.Code:X} char={playerIndex}",
                () => FindInStack(code, level, expires, x => !x.equipmentPlayerIndex.HasValue)),
            e => RejectServerResult(info, "unequip", e));
        return EnhancementResult.Pending;
    }

    // 강화: 비용·상한·성공 여부는 서버가 정한다(일반 5·레어 10·에픽 15). 응답의 ItemCode는 한 단계 오른 묶음이다.
    public bool CanEnhance(EquipmentInstance item) => Owns(item) && !item.IsMaxLevel() && LocalProgression.Balance(eCurrency.EquipmentStone) >= EquipmentEconomy.EnhanceCost(item);
    public bool TryEnhance(EquipmentInstance item) => TryEnhanceDetailed(item) == EnhancementResult.Pending;
    public EnhancementResult TryEnhanceDetailed(EquipmentInstance item)
    {
        if (!Owns(item)) return EnhancementResult.InvalidItem;
        if (item.IsMaxLevel()) return EnhancementResult.MaxLevel;
        if (LocalProgression.Balance(eCurrency.EquipmentStone) < EquipmentEconomy.EnhanceCost(item)) return EnhancementResult.NotEnoughMaterials;
        var gate = Gate(out var network); if (gate != EnhancementResult.Pending) return gate;
        var info = new ActionInfo { Kind = ActionKind.Enhance, ItemId = item.instanceId, CharacterIndex = item.equipmentPlayerIndex ?? -1 };
        int code = item.baseData.itemCode; long expires = item.expiresUtc; int? character = item.equipmentPlayerIndex;
        var request = new Dto.OnEnchantEquipmentRequestDTO { ItemCode = item.ToItemCode(), CharacterIndex = character };
        Begin();
        network.Call<Dto.OnEnchantEquipmentRequestDTO, Dto.OnEnchantEquipmentResponseDTO>(request,
            r =>
            {
                info.SpentEquipmentStone = r.SpentEquipmentStone;
                int upgraded = (int)r.ItemCode.GetItemEnchantCount();
                ApplyServerInventory(info, "enhance", "OnEnchantEquipment", r.Equipment, r.CurrentEquipmentStone,
                    $"code=0x{request.ItemCode.Code:X} char={(character.HasValue ? character.Value.ToString() : "-")} spent={r.SpentEquipmentStone} result=0x{r.ItemCode.Code:X}",
                    () => FindInStack(code, upgraded, expires, x => x.equipmentPlayerIndex == character));
            },
            e => RejectServerResult(info, "enhance", e));
        return EnhancementResult.Pending;
    }

    // 분해: 미리보기(EquipmentEconomy.Preview)가 만든 요청을 그대로 보낸다. 환급액은 응답 GrantedEquipmentStone이 확정한다.
    public bool Dismantle(EquipmentInstance item) => DismantleEx(item?.baseData == null ? null : EquipmentEconomy.Preview(_ => true, item.instanceId)) == EnhancementResult.Pending;
    public bool Dismantle(EquipmentEconomy.DismantlePlan plan) => DismantleEx(plan) == EnhancementResult.Pending;
    public EnhancementResult DismantleEx(EquipmentEconomy.DismantlePlan plan)
    {
        if (plan == null || plan.Count == 0 || plan.Items == null || plan.ReserveStacks == null) return EnhancementResult.Nothing;
        var gate = Gate(out var network); if (gate != EnhancementResult.Pending) return gate;
        var info = new ActionInfo { Kind = ActionKind.Dismantle };
        var request = new Dto.OnDismantleEquipmentRequestDTO { Mode = plan.Mode, Items = new List<Dto.DismantleItemDTO>(plan.Items), ReserveStacks = new List<Dto.DismantleReserveDTO>(plan.ReserveStacks) };
        long previewStones = plan.Stones; long count = plan.Count;
        Begin();
        network.Call<Dto.OnDismantleEquipmentRequestDTO, Dto.OnDismantleEquipmentResponseDTO>(request,
            r =>
            {
                info.GrantedEquipmentStone = r.GrantedEquipmentStone;
                ApplyServerInventory(info, "dismantle", "OnDismantleEquipment", r.Equipment, r.CurrentEquipmentStone,
                    $"mode={request.Mode} items={request.Items.Count} reserves={request.ReserveStacks.Count} count={count} preview={previewStones} granted={r.GrantedEquipmentStone}", null);
            },
            e => RejectServerResult(info, "dismantle", e));
        return EnhancementResult.Pending;
    }

    // 자동 분해: 설정만 저장한다. 이후 서버가 지급하는 장비에만 적용되고 기존 보유분은 분해하지 않는다.
    public EnhancementResult SetAutoDismantleEx(int mask)
    {
        if (mask < 0 || mask > 7) return EnhancementResult.InvalidItem;
        var gate = Gate(out var network); if (gate != EnhancementResult.Pending) return gate;
        var info = new ActionInfo { Kind = ActionKind.AutoDismantle };
        Begin();
        network.Call<Dto.OnSetAutoDismantleRequestDTO, Dto.OnSetAutoDismantleResponseDTO>(new Dto.OnSetAutoDismantleRequestDTO { RarityMask = mask },
            r => ApplyServerInventory(info, "auto-dismantle", "OnSetAutoDismantle", r.Equipment, null, $"mask={mask}", null),
            e => RejectServerResult(info, "auto-dismantle", e));
        return EnhancementResult.Pending;
    }

    // 대기함 수령: 대기함 항목 1개를 주 보관함으로 옮긴다(ItemCode의 수량 비트 = 옮길 수). 가방이 가득이면 서버가 1020이다.
    public bool ClaimPending(string id) => ClaimPendingEx(id) == EnhancementResult.Pending;
    public EnhancementResult ClaimPendingEx(string id)
    {
        var saved = LocalProgression.State.PendingEquipment.Find(x => x.Id == id);
        if (saved == null || GetData(saved.Code) == null) return EnhancementResult.InvalidItem;
        var gate = Gate(out var network); if (gate != EnhancementResult.Pending) return gate;
        var info = new ActionInfo { Kind = ActionKind.ClaimPending };
        int code = saved.Code, level = saved.Level; long expires = saved.ExpiresUtc;
        var request = new Dto.OnClaimPendingEquipmentRequestDTO { ItemCode = EquipmentInstance.MakeItemCode(code, level, expires, 1) };
        Begin();
        network.Call<Dto.OnClaimPendingEquipmentRequestDTO, Dto.OnClaimPendingEquipmentResponseDTO>(request,
            r => ApplyServerInventory(info, "claim-pending", "OnClaimPendingEquipment", r.Equipment, null, $"code=0x{request.ItemCode.Code:X}",
                () => FindInStack(code, level, expires, x => !x.equipmentPlayerIndex.HasValue)),
            e => RejectServerResult(info, "claim-pending", e));
        return EnhancementResult.Pending;
    }

    // 추가 보관(예비) 수령: 예비 묶음에서 1개를 주 보관함으로 옮긴다.
    public bool ClaimLegacy(int code, int level) => ClaimLegacyEx(code, level) == EnhancementResult.Pending;
    public EnhancementResult ClaimLegacyEx(int code, int level)
    {
        var stack = LocalProgression.State.LegacyEquipment.Find(x => x.Code == code && x.Level == level);
        if (stack == null || stack.Count <= 0 || GetData(code) == null) return EnhancementResult.InvalidItem;
        var gate = Gate(out var network); if (gate != EnhancementResult.Pending) return gate;
        var info = new ActionInfo { Kind = ActionKind.ClaimReserve };
        var request = new Dto.OnClaimReserveEquipmentRequestDTO { ItemCode = EquipmentInstance.MakeItemCode(code, level, 0, 1) };
        Begin();
        network.Call<Dto.OnClaimReserveEquipmentRequestDTO, Dto.OnClaimReserveEquipmentResponseDTO>(request,
            r => ApplyServerInventory(info, "claim-reserve", "OnClaimReserveEquipment", r.Equipment, null, $"code=0x{request.ItemCode.Code:X}",
                () => FindInStack(code, level, 0, x => !x.equipmentPlayerIndex.HasValue)),
            e => RejectServerResult(info, "claim-reserve", e));
        return EnhancementResult.Pending;
    }

    /// <summary>서버 장비 인벤토리를 다시 받아 적용한다. 응답 유실·저장 실패 뒤 복구용이다.</summary>
    public EnhancementResult Resync()
    {
        var gate = Gate(out var network); if (gate != EnhancementResult.Pending) return gate;
        var info = new ActionInfo { Kind = ActionKind.Resync };
        Begin();
        network.Call<Dto.OnGetEquipmentInventoryRequestDTO, Dto.OnGetEquipmentInventoryResponseDTO>(new Dto.OnGetEquipmentInventoryRequestDTO(),
            r => ApplyServerInventory(info, "resync", "OnGetEquipmentInventory", r.Equipment, null, "", null),
            e => RejectServerResult(info, "resync", e));
        return EnhancementResult.Pending;
    }

    // ── 로컬 전용(에디터 QA 스크립트 호환) ──────────────────────────────
    // 운영 경로는 서버가 지급·이관한다. 아래는 로그인 없는 격리 계정의 인수 검사만 쓴다. 서버 전환으로 결과는 무효화되었다.
    public static bool Grant(ProgressionState state, EquipmentSave item, bool allowPending, bool recordQuestProgress = true)
    {
        if (state.Equipment.Any(x => x.Id == item.Id) || state.PendingEquipment.Any(x => x.Id == item.Id)) return false;
        if (EquipmentEconomy.ShouldAutoDismantle(state, item)) LocalProgression.Credit(state, eCurrency.EquipmentStone, EquipmentEconomy.Yield(item));
        else if (state.Equipment.Count < Capacity) state.Equipment.Add(item);
        else if (allowPending && state.PendingEquipment.Count < PendingCapacity)
        { item.ExpiresUtc = 0; state.PendingEquipment.Add(item); }
        else if (allowPending && !item.Locked && !item.Player.HasValue)
        {
            var stack = state.LegacyEquipment.Find(x => x.Code == item.Code && x.Level == item.Level);
            if (stack == null) state.LegacyEquipment.Add(new LegacyEquipmentStack { Code = item.Code, Level = item.Level, Count = 1 });
            else stack.Count = checked(stack.Count + 1);
        }
        else return false;
        return true;
    }
    public static void ImportLegacy(ProgressionState state, int code, int level, int amount)
    {
        if (amount < 0 || level < 0 || level > 15) throw new ArgumentOutOfRangeException();
        int direct = Math.Min(amount, Math.Max(0, Capacity - state.Equipment.Count));
        for (int n = 0; n < direct; n++)
            state.Equipment.Add(new EquipmentSave { Id = Guid.NewGuid().ToString("N"), Code = code, Level = level });
        int remainder = amount - direct;
        if (remainder > 0)
        {
            var stack = state.LegacyEquipment.Find(x => x.Code == code && x.Level == level);
            if (stack == null) state.LegacyEquipment.Add(new LegacyEquipmentStack { Code = code, Level = level, Count = remainder });
            else stack.Count = checked(stack.Count + remainder);
        }
    }
    public static bool TakeLegacy(ProgressionState state, int code, int level)
    {
        var stack = state.LegacyEquipment.Find(x => x.Code == code && x.Level == level);
        if (stack == null || stack.Count <= 0 || state.Equipment.Count >= Capacity) return false;
        state.Equipment.Add(new EquipmentSave { Id = Guid.NewGuid().ToString("N"), Code = code, Level = level });
        if (--stack.Count == 0) state.LegacyEquipment.Remove(stack);
        return true;
    }
    public EquipmentSave RollFieldDrop(double probability)
    {
        if (UnityEngine.Random.value >= probability) return null;
        float roll = UnityEngine.Random.value;
        var items = GetRewardPool(roll < .80f ? eEquipmentRarity.Normal : roll < .98f ? eEquipmentRarity.Rare : eEquipmentRarity.Epic);
        if (items.Count == 0) return null;
        return new EquipmentSave { Id = Guid.NewGuid().ToString("N"), Code = items[UnityEngine.Random.Range(0, items.Count)].itemCode };
    }
}
