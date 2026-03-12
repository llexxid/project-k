using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 플레이어가 보유한 장비 인스턴스 목록 관리.
/// EquipmentManager 내부에서 생성·참조한다.
/// </summary>
public class EquipmentInventory
{
    private readonly List<EquipmentInstance> _items = new();

    /// <summary>읽기 전용 장비 목록</summary>
    public IReadOnlyList<EquipmentInstance> Items => _items;

    /// <summary>장비 인스턴스를 인벤토리에 추가한다.</summary>
    public void Add(EquipmentInstance instance)
    {
        _items.Add(instance);
    }

    /// <summary>장비 인스턴스를 인벤토리에서 제거한다. 성공 시 true 반환.</summary>
    public bool Remove(EquipmentInstance instance)
    {
        return _items.Remove(instance);
    }

    /// <summary>
    /// 동일한 EquipmentData를 기반으로 하는 인스턴스 목록을 반환한다.
    /// 합성 시 재료 목록 조회에 사용.
    /// </summary>
    public List<EquipmentInstance> GetSameEquipment(EquipmentData data)
    {
        return _items.Where(i => i.baseData == data).ToList();
    }

    /// <summary>
    /// 특정 등급의 장비 인스턴스 목록을 반환한다.
    /// 합성 가능 여부 확인 등에 사용.
    /// </summary>
    public List<EquipmentInstance> GetByRarity(eEquipmentRarity rarity)
    {
        return _items.Where(i => i.baseData.rarity == rarity).ToList();
    }

    /// <summary>동일 EquipmentData 장비가 인벤토리에 몇 개 있는지 반환한다.</summary>
    public int CountSame(EquipmentData data)
    {
        return _items.Count(i => i.baseData == data);
    }
}
