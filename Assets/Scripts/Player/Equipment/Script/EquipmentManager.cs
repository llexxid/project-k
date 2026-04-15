using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 장비 시스템 메인 매니저.
/// 장착/해제, 드롭, 강화(재료 소모+확률), 합성, 직업 필터를 담당한다.
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

    // ── [직업 필터] 현재 직업명 ─────────────────────────────────
    private string _currentJobName = "";

    // ── 인벤토리 외부 접근 ────────────────────────────────────────
    public EquipmentInventory Inventory => _inventory;

    // ── UI 연동 이벤트 ────────────────────────────────────────────
    /// <summary>장비 착용 완료 시 발행. (슬롯, 착용된 인스턴스)</summary>
    public System.Action<eEquipmentSlot, EquipmentInstance> OnEquipped;

    /// <summary>장비 해제 완료 시 발행. (슬롯)</summary>
    public System.Action<eEquipmentSlot> OnUnequipped;

    /// <summary>드롭으로 장비 획득 시 발행. (획득한 인스턴스)</summary>
    public System.Action<EquipmentInstance> OnItemDropped;

    /// <summary>강화 성공 시 발행. (강화된 인스턴스)</summary>
    public System.Action<EquipmentInstance> OnEnhanced;

    /// <summary>강화 실패 시 발행. (재료만 소모되고 레벨은 유지된 인스턴스)</summary>
    public System.Action<EquipmentInstance> OnEnhanceFailed;

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

    /// <summary>
    /// 전직 시 ChangeJob.ApplyJobByIndex()에서 호출한다.
    /// 이후 드롭은 이 직업에 허용된 장비만 선택된다.
    /// </summary>
    public void SetCurrentJob(string jobName)
    {
        _currentJobName = jobName ?? "";
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
        if (instance == null) return;

        _equipped[instance.baseData.slot] = instance;
        RecalculateStats();
        OnEquipped?.Invoke(instance.baseData.slot, instance);
    }

    /// <summary>지정 슬롯의 장비를 해제한다.</summary>
    public void Unequip(eEquipmentSlot slot)
    {
        if (!_equipped.ContainsKey(slot)) return;

        string removedName = _equipped[slot].baseData.equipmentName;
        _equipped.Remove(slot);
        RecalculateStats();
        OnUnequipped?.Invoke(slot);
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
    /// 현재 직업에 허용된 장비만 드롭된다. 창병(Spearman)처럼 허용 장비가 없으면 드롭 안 됨.
    /// Player.GiveReward()에서 호출된다.
    /// </summary>
    public void TryDropEquipment()
    {
        if (_dropTable == null || _database == null) return;

        eEquipmentRarity? rarity = _dropTable.RollDrop();
        if (rarity == null) return;

        // 현재 직업에 허용된 등급 장비 목록에서 랜덤 선택
        List<EquipmentData> candidates = string.IsNullOrEmpty(_currentJobName)
            ? _database.GetEquipmentsByRarity(rarity.Value)
            : _database.GetEquipmentsByJob(_currentJobName, rarity.Value);

        if (candidates.Count == 0)
        {
            return;
        }

        EquipmentData picked   = candidates[UnityEngine.Random.Range(0, candidates.Count)];
        EquipmentInstance item = new EquipmentInstance(picked);

        _inventory.Add(item);
        OnItemDropped?.Invoke(item);
    }

    // ═══════════════════════════════════════════════════════════════
    //  강화 (동일 장비 소모 + 확률)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 강화 가능 여부를 확인한다.
    /// 조건: 최대 레벨 미만 AND 인벤토리에 동일 장비가 소모 개수 이상 존재
    /// (강화 대상 장비 자신은 소모 개수에 포함되지 않음)
    /// </summary>
    public bool CanEnhance(EquipmentInstance instance)
    {
        if (instance == null || instance.IsMaxLevel()) return false;

        int materialCount = instance.GetMaterialCount();
        int available = _inventory.Items
            .Where(i => i != instance && i.baseData == instance.baseData)
            .Count();

        return available >= materialCount;
    }

    /// <summary>
    /// 강화를 실행한다.
    /// 동일 장비 N개를 인벤토리에서 소모하고 확률을 굴려 성공 시 레벨+1.
    /// 실패해도 재료는 소모된다.
    /// 성공 시 true, 실패 시 false 반환.
    /// </summary>
    public bool TryEnhance(EquipmentInstance instance)
    {
        if (!CanEnhance(instance)) return false;

        int materialCount = instance.GetMaterialCount();
        float successRate = instance.GetEnhanceSuccessRate();

        // 재료(동일 장비 N개, 자기 자신 제외) 소모
        List<EquipmentInstance> materials = _inventory.Items
            .Where(i => i != instance && i.baseData == instance.baseData)
            .Take(materialCount)
            .ToList();

        foreach (var mat in materials)
        {
            // 착용 중인 재료 장비는 자동 해제 후 제거
            if (_equipped.TryGetValue(mat.baseData.slot, out var eq) && eq == mat)
                Unequip(mat.baseData.slot);
            _inventory.Remove(mat);
        }

        // 성공 확률 판정
        bool success = UnityEngine.Random.value <= successRate;

        if (success)
        {
            instance.enhancementLevel++;

            // 착용 중인 장비라면 스탯 즉시 재계산
            if (_equipped.ContainsValue(instance))
                RecalculateStats();

            OnEnhanced?.Invoke(instance);
        }
        else
        {
            OnEnhanceFailed?.Invoke(instance);
        }

        return success;
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
        if (materials == null || materials.Count < SYNTHESIS_REQUIRED_COUNT) return null;

        EquipmentData baseData = materials[0].baseData;

        if (materials.Any(m => m.baseData != baseData)) return null;

        if (baseData.rarity == eEquipmentRarity.Epic) return null;

        foreach (var mat in materials)
        {
            if (!_inventory.Items.Contains(mat)) return null;
        }

        // ── 재료 소모 ─────────────────────────────────────────────
        foreach (var mat in materials.Take(SYNTHESIS_REQUIRED_COUNT))
        {
            // 착용 중인 장비가 재료로 사용되면 자동 해제
            if (_equipped.TryGetValue(mat.baseData.slot, out var equipped) && equipped == mat)
                Unequip(mat.baseData.slot);

            _inventory.Remove(mat);
        }

        // ── 결과 장비 선택 (다음 등급, 현재 직업 필터 적용) ─────
        eEquipmentRarity nextRarity = (eEquipmentRarity)((int)baseData.rarity + 1);
        List<EquipmentData> candidates = string.IsNullOrEmpty(_currentJobName)
            ? _database.GetEquipmentsByRarity(nextRarity)
            : _database.GetEquipmentsByJob(_currentJobName, nextRarity);

        // 직업 필터 후 후보가 없으면 전체에서 선택 (fallback)
        if (candidates.Count == 0)
            candidates = _database.GetEquipmentsByRarity(nextRarity);

        if (candidates.Count == 0) return null;

        EquipmentData picked     = candidates[UnityEngine.Random.Range(0, candidates.Count)];
        EquipmentInstance result = new EquipmentInstance(picked);
        _inventory.Add(result);

        OnSynthesized?.Invoke(result);
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
