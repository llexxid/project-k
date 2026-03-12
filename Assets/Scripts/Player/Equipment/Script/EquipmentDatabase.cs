using UnityEngine;
using System.Collections.Generic;

// ================================================================
//  장비 등록 방법 (코드 수정 없이 에디터에서만 작업)
// ================================================================
//
//  ── STEP 1. EquipmentData 에셋 생성 ───────────────────────────
//  Project 창 우클릭 → Create > ScriptableObjects > EquipmentData
//  Inspector 필드:
//    · Equipment Name        : 장비 고유 이름 (예: "낡은 검")
//    · Slot                  : Weapon
//    · Rarity                : Normal / Rare / Epic
//    · Icon                  : Equipment/Sprite 폴더 스프라이트 연결
//                              normal.png / normal2.png / normal3.png
//                              rare.png / rare2.png / epic.png
//    · Bonus Atk / Max HP    : 기본 스탯 보너스
//    · Max Enhancement Level : 최대 강화 레벨
//    · Atk/HP Growth Per Lvl : 강화당 성장률
//    · Base Enhance Cost     : 강화 기본 비용(골드)
//
//  ── STEP 2. EquipmentDatabase 에셋에 등록 ─────────────────────
//  EquipmentDatabase.asset Inspector > Equipment List → + → EquipmentData 드래그
//  (Normal 3개 / Rare 2개 / Epic 1개 총 6개 등록)
//
//  ── STEP 3. Player 인스펙터 연결 ──────────────────────────────
//  Player 인스펙터 > Equipment Database 필드에 이 에셋 연결
//  Player 인스펙터 > Equipment Drop Table 필드에 EquipmentDropTableSO 연결
// ================================================================

[CreateAssetMenu(fileName = "NewEquipmentDatabase", menuName = "ScriptableObjects/EquipmentDatabase")]
public class EquipmentDatabase : ScriptableObject
{
    [Tooltip("게임에 등록된 모든 장비 데이터 목록 (Normal 3 / Rare 2 / Epic 1)")]
    public List<EquipmentData> equipmentList = new List<EquipmentData>();

    private Dictionary<string, EquipmentData> _dict;

    public void Initialize()
    {
        _dict = new Dictionary<string, EquipmentData>();
        foreach (var data in equipmentList)
        {
            if (data == null) continue;
            if (!_dict.ContainsKey(data.equipmentName))
                _dict.Add(data.equipmentName, data);
        }
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
}
