using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using PlayFab.ClientModels;
using UnityEngine;

//아이템 획득 시 효과(그냥 획득 / 팝업창 띄우기 등...)
public enum GetEffect
{
    None,
    Gacha,
    Popup,
    
}
public class EquipmentManager : MonoBehaviour
{
    public static EquipmentManager Instance;
    #region 이벤트
    /// <summary>드롭으로 장비 획득 시 발행. (획득한 인스턴스)</summary>
    public event Action<EquipmentInstance> OnItemDropped;
    /// <summary>강화 성공 시 발행. (강화된 인스턴스)</summary>
    public event Action<EquipmentInstance> OnEnhanced;
    /// <summary>강화 실패 시 발행. (재료만 소모되고 레벨은 유지된 인스턴스)</summary>
    public event Action<EquipmentInstance> OnEnhanceFailed;
    /// <summary>합성 완료 시 발행. (합성 결과 인스턴스)</summary>
    public event Action<EquipmentInstance> OnSynthesized;
    public event Action<Player, eEquipmentSlot, EquipmentInstance> OnEquipped;
    public event Action<Player, eEquipmentSlot, EquipmentInstance> OnUnequipped;


    #endregion

    public EquipmentInventory Inventory => _inventory; //외부접근용 프로퍼티
    [SerializeField]private EquipmentDatabase    _database;
    [SerializeField]private EquipmentDropTableSO _dropTable;

    // 합성에 필요한 동일 장비 개수
    private const int SYNTHESIS_REQUIRED_COUNT = 3;

    // 현재 착용 중인 장비 (슬롯 → 인스턴스)
    private readonly Dictionary<PlayerEquipmentManager, Player> _ownerByEquipmentManager = new();
    private EquipmentInventory   _inventory;
    private PlayerStatus         _playerStatus;

    private string _currentJobName = ""; //현재 직업명

    private readonly Dictionary<int, PlayerEquipmentManager> _playerEquipments = new();
    private readonly Dictionary<int, Player> _players = new();

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        Init();
    }

    private void Init()
    {
        _inventory = new();
    }

    public void RegisterPlayer(Player player)
    {
        if (player?.PlayerEquipmentManager == null) return;
        
        _players[player.PlayerIndex] = player;
        _playerEquipments[player.PlayerIndex] = player.PlayerEquipmentManager;
        _ownerByEquipmentManager[player.PlayerEquipmentManager] = player;

        player.PlayerEquipmentManager.OnEquipped -= HandleEquipped;
        player.PlayerEquipmentManager.OnEquipped += HandleEquipped;

        player.PlayerEquipmentManager.OnUnequipped -= HandleUnequipped;
        player.PlayerEquipmentManager.OnUnequipped += HandleUnequipped;
    }

    public void ClearPlayer()
    {
        foreach (var player in _players.Values)
        {
            player.PlayerEquipmentManager.OnEquipped -= HandleEquipped;
            player.PlayerEquipmentManager.OnUnequipped -= HandleUnequipped;
        }
        _players.Clear();
        _playerEquipments.Clear();
        _ownerByEquipmentManager.Clear();
    }
    private void HandleEquipped(PlayerEquipmentManager source, eEquipmentSlot slot, EquipmentInstance item)
    {
        _ownerByEquipmentManager.TryGetValue(source, out var player);
        OnEquipped?.Invoke(player, slot, item);
    }

    private void HandleUnequipped(PlayerEquipmentManager source, eEquipmentSlot slot, EquipmentInstance item)
    {
        _ownerByEquipmentManager.TryGetValue(source, out var player);
        OnUnequipped?.Invoke(player, slot, item);
    }
    public IEnumerable<string> GetCurrentJobNames()
    {
        return _players.Values
            .Select(p => p?.playerStatus?.JobName)
            .Where(job => !string.IsNullOrEmpty(job));
    }
    public void GetEquipment(EquipmentInstance instance, GetEffect effect)
    {
        _inventory.Add(instance);
        if (effect != GetEffect.None)
        {
            OnItemDropped?.Invoke(instance);
        }
    }

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
            .Count(material => material != instance && material.baseData == instance.baseData && !material.IsEquipped);

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

        HashSet<EquipmentInstance> materials = _inventory.Items.
            Where(material => instance.baseData == material.baseData && !material.IsEquipped && material != instance).
            Take(materialCount).
            ToHashSet();
        
        if (materials.Count < materialCount)
        {
            Debug.LogWarning($"장비 강화에 필요한 개수가 선정되지 못했습니다 (장비 소모량 : {materialCount}, 현재 보유량 : {materials.Count})");
            return false;
        }

        if (!_inventory.RemoveAll(materials))
        {
            Debug.Log("삭제 입력과 실제 삭제가 다름");
        }
        // 성공 확률 판정
        bool success = UnityEngine.Random.value <= successRate;

        if (success)
        {
            instance.enhancementLevel++;

            // 착용 중인 장비라면 스탯 즉시 재계산
            if (instance.equipmentPlayerIndex != null)
            {
                int playerIdx = (int)instance.equipmentPlayerIndex;
                if (_playerEquipments.TryGetValue(playerIdx, out PlayerEquipmentManager manager))
                {
                    manager.RecalculateStats();
                }
            }

            OnEnhanced?.Invoke(instance);
        }
        else
        {
            OnEnhanceFailed?.Invoke(instance);
        }

        return success;
    }

    /// <summary>
    /// 몬스터 처치 시 장비 드롭 여부를 판정하고, 드롭되면 인벤토리에 추가한다.
    /// 현재 직업에 허용된 장비만 드롭된다. 창병(Spearman)처럼 허용 장비가 없으면 드롭 안 됨.
    /// Player.GiveReward()에서 호출된다.
    /// </summary>
    public void TryDropEquipment()
    {
        if (_dropTable == null || _database == null) return;

        var jobNames = GetCurrentJobNames().ToList();
        eEquipmentRarity? rarity = _dropTable.RollDrop();
        if (rarity == null) return;

        // 현재 직업에 허용된 등급 장비 목록에서 랜덤 선택
        List<EquipmentData> candidates = new();

        foreach (var job in jobNames)
        {
            List<EquipmentData> candidate = string.IsNullOrEmpty(job)
                ? _database.GetEquipmentsByRarity(rarity.Value)
                : _database.GetEquipmentsByJob(job, rarity.Value);

            candidates.AddRange(candidate);
        }

        candidates = candidates.Distinct().ToList();

        if (candidates.Count == 0)
        {
            return;
        }

        EquipmentData picked   = candidates[UnityEngine.Random.Range(0, candidates.Count)];
        EquipmentInstance item = new EquipmentInstance(picked);

        _inventory.Add(item);
        OnItemDropped?.Invoke(item);
    }
    /// <summary>
    /// *** 기획에서 제외!!! 미사용 코드임***
    /// 동일한 장비 3개를 합성해 다음 등급 장비 1개를 획득한다.
    ///   Normal × 3  →  Rare 1개 (랜덤)
    ///   Rare   × 3  →  Epic 1개
    ///   Epic   × 3  →  합성 불가
    ///
    /// materials: 동일 EquipmentData를 가진 인스턴스 3개 (인벤토리에 존재해야 함)
    /// 성공 시 결과 인스턴스 반환, 실패 시 null 반환.
    /// </summary>
    public EquipmentInstance TrySynthesize(List<EquipmentInstance> equipments)
    {
     // ── 재료 유효성 검사 ──────────────────────────────────────
     if (equipments == null || equipments.Count < SYNTHESIS_REQUIRED_COUNT) return null;

     EquipmentData baseData = equipments[0].baseData;
     if (baseData.rarity + 1 == eEquipmentRarity.MaxRarity) return null;
     
     foreach (var equipment in equipments)
     {
         if (equipment.IsEquipped)
         {
             Debug.LogWarning("장착중인 장비는 합성할 수 없습니다");
             return null;
         }
         if (equipment.baseData != baseData)
         {
             Debug.LogWarning("합성하려는 장비의 종류가 다릅니다");
             return null;
         }
     }

     // ── 재료 소모 ─────────────────────────────────────────────
     foreach (var equipment in equipments.Take(SYNTHESIS_REQUIRED_COUNT))
     {
         _inventory.Remove(equipment);
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
}
