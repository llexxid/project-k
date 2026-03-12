using System.Collections.Generic;
using System.Linq;
using Scripts.Users;
using UnityEngine;

/// <summary>
/// 장비 시스템 메인 매니저.
/// 장착/해제, 드롭, 강화, 합성을 모두 담당한다.
/// Player.Awake()에서 new → Init() 순서로 초기화한다.
/// </summary>
public class EquipmentManager
{
    // 합성에 필요한 동일 장비 개수
    private const int SYNTHESIS_REQUIRED_COUNT = 3;

    // 현재 착용 중인 장비 (슬롯 → 인스턴스)
    private readonly Dictionary<eEquipmentSlot, EquipmentInstance> _equipped = new();

    private EquipmentInventory   _inventory;
    private EquipmentDatabase    _database;
    private EquipmentDropTableSO _dropTable;
    private PlayerStatus         _playerStatus;

    // ── 인벤토리 외부 접근 ────────────────────────────────────────
    public EquipmentInventory Inventory => _inventory;

    // ── UI 연동 이벤트 ────────────────────────────────────────────
    /// <summary>장비 착용 완료 시 발행. (슬롯, 착용된 인스턴스)</summary>
    public System.Action<eEquipmentSlot, EquipmentInstance> OnEquipped;

    /// <summary>장비 해제 완료 시 발행. (슬롯)</summary>
    public System.Action<eEquipmentSlot> OnUnequipped;

    /// <summary>드롭으로 장비 획득 시 발행. (획득한 인스턴스)</summary>
    public System.Action<EquipmentInstance> OnItemDropped;

    /// <summary>강화 완료 시 발행. (강화된 인스턴스)</summary>
    public System.Action<EquipmentInstance> OnEnhanced;

    /// <summary>합성 완료 시 발행. (합성 결과 인스턴스)</summary>
    public System.Action<EquipmentInstance> OnSynthesized;

    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Player.Awake()에서 반드시 호출해야 한다.
    /// </summary>
    public void Init(PlayerStatus playerStatus, EquipmentDatabase database, EquipmentDropTableSO dropTable)
    {
        _playerStatus = playerStatus;
        _database     = database;
        _dropTable    = dropTable;
        _inventory    = new EquipmentInventory();
    }

    // ═══════════════════════════════════════════════════════════════
    //  장착 / 해제
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 장비를 착용한다. 같은 슬롯에 장비가 이미 있으면 교체된다.
    /// 착용한 장비는 인벤토리에서 제거하지 않는다 (인벤토리에 남아있는 채로 착용 표시).
    /// </summary>
    public void Equip(EquipmentInstance instance)
    {
        if (instance == null)
        {
            Debug.LogWarning("[EquipmentManager] Equip: null 장비는 착용할 수 없습니다.");
            return;
        }

        _equipped[instance.baseData.slot] = instance;
        RecalculateStats();
        OnEquipped?.Invoke(instance.baseData.slot, instance);

        Debug.Log($"[EquipmentManager] 착용: {instance.baseData.equipmentName} +{instance.enhancementLevel} ({instance.baseData.rarity})");
    }

    /// <summary>지정 슬롯의 장비를 해제한다.</summary>
    public void Unequip(eEquipmentSlot slot)
    {
        if (!_equipped.ContainsKey(slot))
        {
            Debug.LogWarning($"[EquipmentManager] Unequip: {slot} 슬롯에 착용된 장비가 없습니다.");
            return;
        }

        string removedName = _equipped[slot].baseData.equipmentName;
        _equipped.Remove(slot);
        RecalculateStats();
        OnUnequipped?.Invoke(slot);

        Debug.Log($"[EquipmentManager] 해제: {removedName}");
    }

    /// <summary>슬롯에 착용된 인스턴스를 반환한다. 없으면 null.</summary>
    public EquipmentInstance GetEquipped(eEquipmentSlot slot)
    {
        _equipped.TryGetValue(slot, out var result);
        return result;
    }

    /// <summary>해당 슬롯에 장비가 착용되어 있는지 여부</summary>
    public bool IsEquipped(eEquipmentSlot slot) => _equipped.ContainsKey(slot);

    // ═══════════════════════════════════════════════════════════════
    //  드롭
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 몬스터 처치 시 장비 드롭 여부를 판정하고, 드롭되면 인벤토리에 추가한다.
    /// Player.GiveReward()에서 호출된다.
    /// </summary>
    public void TryDropEquipment()
    {
        if (_dropTable == null || _database == null) return;

        eEquipmentRarity? rarity = _dropTable.RollDrop();
        if (rarity == null) return;

        // 해당 등급 장비 목록에서 랜덤 선택
        List<EquipmentData> candidates = _database.GetEquipmentsByRarity(rarity.Value);
        if (candidates.Count == 0)
        {
            Debug.LogWarning($"[EquipmentManager] 드롭 실패: {rarity.Value} 등급 장비가 Database에 없습니다.");
            return;
        }

        EquipmentData picked  = candidates[UnityEngine.Random.Range(0, candidates.Count)];
        EquipmentInstance item = new EquipmentInstance(picked);

        _inventory.Add(item);
        OnItemDropped?.Invoke(item);

        Debug.Log($"[EquipmentManager] 드롭 획득: {picked.equipmentName} ({picked.rarity})");
    }

    // ═══════════════════════════════════════════════════════════════
    //  강화
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 강화 가능 여부를 확인한다.
    /// 조건: 최대 레벨 미만 AND 골드 충분
    /// </summary>
    public bool CanEnhance(EquipmentInstance instance, User user)
    {
        if (instance == null || instance.IsMaxLevel()) return false;
        return user.CanAfford(eCurrency.Gold, instance.GetNextEnhanceCost());
    }

    /// <summary>
    /// 강화를 실행한다.
    /// 골드를 차감하고 강화 레벨을 1 올린다. 성공 시 true 반환.
    /// 착용 중인 장비라면 스탯도 즉시 재계산된다.
    /// </summary>
    public bool TryEnhance(EquipmentInstance instance, User user)
    {
        if (!CanEnhance(instance, user))
        {
            Debug.LogWarning("[EquipmentManager] 강화 실패: 조건 미충족 (레벨 상한 또는 골드 부족)");
            return false;
        }

        int cost = instance.GetNextEnhanceCost();
        if (!user.TrySpendCoin(eCurrency.Gold, cost)) return false;

        instance.enhancementLevel++;

        // 착용 중인 장비라면 스탯 즉시 재계산
        if (_equipped.ContainsValue(instance))
            RecalculateStats();

        OnEnhanced?.Invoke(instance);
        Debug.Log($"[EquipmentManager] 강화 완료: {instance.baseData.equipmentName} → +{instance.enhancementLevel} (비용: {cost}골드)");
        return true;
    }

    // ═══════════════════════════════════════════════════════════════
    //  합성
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 동일한 장비 3개를 합성해 다음 등급 장비 1개를 획득한다.
    ///   Normal × 3  →  Rare 1개 (랜덤)
    ///   Rare   × 3  →  Epic 1개
    ///   Epic   × 3  →  합성 불가
    ///
    /// materials: 동일 EquipmentData를 가진 인스턴스 3개 (인벤토리에 존재해야 함)
    /// 성공 시 결과 인스턴스 반환, 실패 시 null 반환.
    /// </summary>
    public EquipmentInstance TrySynthesize(List<EquipmentInstance> materials)
    {
        // ── 재료 유효성 검사 ──────────────────────────────────────
        if (materials == null || materials.Count < SYNTHESIS_REQUIRED_COUNT)
        {
            Debug.LogWarning($"[EquipmentManager] 합성 실패: 재료가 {SYNTHESIS_REQUIRED_COUNT}개 미만입니다.");
            return null;
        }

        EquipmentData baseData = materials[0].baseData;

        if (materials.Any(m => m.baseData != baseData))
        {
            Debug.LogWarning("[EquipmentManager] 합성 실패: 서로 다른 종류의 장비는 합성할 수 없습니다.");
            return null;
        }

        if (baseData.rarity == eEquipmentRarity.Epic)
        {
            Debug.LogWarning("[EquipmentManager] 합성 실패: Epic 등급은 합성할 수 없습니다.");
            return null;
        }

        foreach (var mat in materials)
        {
            if (!_inventory.Items.Contains(mat))
            {
                Debug.LogWarning("[EquipmentManager] 합성 실패: 인벤토리에 없는 재료가 포함되어 있습니다.");
                return null;
            }
        }

        // ── 재료 소모 ─────────────────────────────────────────────
        foreach (var mat in materials.Take(SYNTHESIS_REQUIRED_COUNT))
        {
            // 착용 중인 장비가 재료로 사용되면 자동 해제
            if (_equipped.TryGetValue(mat.baseData.slot, out var equipped) && equipped == mat)
                Unequip(mat.baseData.slot);

            _inventory.Remove(mat);
        }

        // ── 결과 장비 선택 (다음 등급에서 랜덤) ─────────────────
        eEquipmentRarity nextRarity = (eEquipmentRarity)((int)baseData.rarity + 1);
        List<EquipmentData> candidates = _database.GetEquipmentsByRarity(nextRarity);

        if (candidates.Count == 0)
        {
            Debug.LogWarning($"[EquipmentManager] 합성 실패: {nextRarity} 등급 장비가 Database에 없습니다.");
            return null;
        }

        EquipmentData picked         = candidates[UnityEngine.Random.Range(0, candidates.Count)];
        EquipmentInstance result     = new EquipmentInstance(picked);
        _inventory.Add(result);

        OnSynthesized?.Invoke(result);
        Debug.Log($"[EquipmentManager] 합성 완료: {baseData.equipmentName} ×{SYNTHESIS_REQUIRED_COUNT} → {picked.equipmentName} ({picked.rarity})");
        return result;
    }

    // ═══════════════════════════════════════════════════════════════
    //  스탯 재계산
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 현재 착용 중인 모든 장비의 강화 레벨을 반영한 최종 보너스를 PlayerStatus에 적용한다.
    /// 장착/해제/강화 시 자동 호출된다.
    /// </summary>
    private void RecalculateStats()
    {
        int totalAtk   = 0;
        int totalMaxHP = 0;

        foreach (var instance in _equipped.Values)
        {
            totalAtk   += instance.GetFinalAtk();
            totalMaxHP += instance.GetFinalMaxHP();
        }

        _playerStatus.SetEquipmentBonus(totalAtk, totalMaxHP);
    }
}
