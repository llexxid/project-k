using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewEquipmentDatabase", menuName = "ScriptableObjects/EquipmentDatabase")]
public class EquipmentDatabase : ScriptableObject
{
    [Tooltip("게임에 등록된 모든 장비 데이터 목록 (직업별 Normal 3 / Rare 2 / Epic 1)")]
    public List<EquipmentData> equipmentList = new List<EquipmentData>();

    private Dictionary<string, EquipmentData> _dict;
    private Dictionary<int, EquipmentData> _codeDict;

    public void Initialize()
    {
        _dict     = new Dictionary<string, EquipmentData>();
        _codeDict = new Dictionary<int, EquipmentData>();

        foreach (var data in equipmentList)
        {
            if (data == null) continue;

            if (!_dict.ContainsKey(data.equipmentName))
                _dict.Add(data.equipmentName, data);

            // 같은 itemCode가 다른 에셋에 중복되면 경고, 첫 번째 등록 유지
            int code = data.itemCode;
            if (!_codeDict.ContainsKey(code))
                _codeDict.Add(code, data);
        }
    }

    /// <summary>서버에서 받은 itemCode로 장비 데이터를 가져온다. 없으면 null 반환.</summary>
    public EquipmentData GetEquipmentByCode(int itemCode)
    {
        if (_codeDict == null) Initialize();
        _codeDict.TryGetValue(itemCode, out EquipmentData result);
        return result;
    }

    /// <summary>이름으로 장비 데이터를 가져온다.</summary>
    public EquipmentData GetEquipment(string equipmentName)
    {
        if (_dict == null) Initialize();
        _dict.TryGetValue(equipmentName, out EquipmentData result);
        return result;
    }

    /// <summary>특정 등급의 장비 목록을 반환한다. 드롭 / 합성 결과 선택에 사용.</summary>
    public List<EquipmentData> GetEquipmentsByRarity(eEquipmentRarity rarity)
    {
        if (_dict == null) Initialize();
        var result = new List<EquipmentData>();
        foreach (var data in equipmentList)
        {
            if (data != null && data.rarity == rarity)
                result.Add(data);
        }
        return result;
    }

    /// <summary>
    /// 특정 직업이 사용 가능한 Equipment 중 지정 등급만 반환한다.
    /// 드롭 시 직업 필터에 사용된다.
    /// allowedJobs가 비어있는 장비는 모든 직업 공용으로 포함된다.
    /// </summary>
    public List<EquipmentData> GetEquipmentsByJob(string jobName, eEquipmentRarity rarity)
    {
        if (_dict == null) Initialize();
        var result = new List<EquipmentData>();
        foreach (var data in equipmentList)
        {
            if (data != null && data.rarity == rarity && data.IsAllowedForJob(jobName))
                result.Add(data);
        }
        return result;
    }

    /// <summary>
    /// 특정 직업이 사용 가능한 모든 등급의 Equipment 목록을 반환한다.
    /// </summary>
    public List<EquipmentData> GetAllEquipmentsByJob(string jobName)
    {
        if (_dict == null) Initialize();
        var result = new List<EquipmentData>();
        foreach (var data in equipmentList)
        {
            if (data != null && data.IsAllowedForJob(jobName))
                result.Add(data);
        }
        return result;
    }
}
