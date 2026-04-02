using System;
using System.Collections.Generic;
using UnityEngine;
using Scripts.Core;

/// <summary>
/// 모든 캐릭터에 일괄 적용되는 글로벌 스탯 강화 시스템.
/// 골드를 소모하여 공격력/체력/치명타확률/치명타데미지/경험치획득량을 강화한다.
/// </summary>
public class StatEnhanceManager : MonoBehaviour
{
    public static StatEnhanceManager Instance { get; private set; }

    private const string PrefKey = "StatEnhance";

    // ── 강화 종류 ──
    public enum EnhanceType
    {
        Attack,
        MaxHP,
        CritRate,
        CritDamage,
        ExpGain
    }

    // ── 강화 레벨 저장소 ──
    private Dictionary<EnhanceType, int> _levels = new();

    // ── 레벨당 증가율 (모두 %) ──
    private static readonly Dictionary<EnhanceType, float> BonusPerLevel = new()
    {
        { EnhanceType.Attack,     0.1f },
        { EnhanceType.MaxHP,      0.1f },
        { EnhanceType.CritRate,   0.1f }, // 더미
        { EnhanceType.CritDamage, 0.1f }, // 더미
        { EnhanceType.ExpGain,    0.1f }  // 더미
    };

    // ── 비용 기본값 + 증가율 (방치형: 기본비용 × 1.15^레벨) ──
    private static readonly Dictionary<EnhanceType, int> BaseCost = new()
    {
        { EnhanceType.Attack,     50 },
        { EnhanceType.MaxHP,      50 },
        { EnhanceType.CritRate,   80 },
        { EnhanceType.CritDamage, 80 },
        { EnhanceType.ExpGain,    100 }
    };
    private const float CostGrowthRate = 1.15f;

    public event Action OnEnhanced;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        Load();
    }

    // ── 레벨 조회 ──
    public int GetLevel(EnhanceType type)
    {
        return _levels.TryGetValue(type, out int lv) ? lv : 0;
    }

    // ── 보너스 수치 조회 ──
    public float GetBonus(EnhanceType type)
    {
        return GetLevel(type) * BonusPerLevel[type];
    }

    // % 비율로 반환 (0.02 × level 형태)
    public float GetBonusAtkRate() => GetBonus(EnhanceType.Attack) / 100f;
    public float GetBonusMaxHPRate() => GetBonus(EnhanceType.MaxHP) / 100f;

    // ── 비용 계산: baseCost × 1.15^level (방치형 지수 증가) ──
    public int GetSingleCost(EnhanceType type, int level)
    {
        int baseCost = BaseCost.TryGetValue(type, out int bc) ? bc : 50;
        return Mathf.RoundToInt(baseCost * Mathf.Pow(CostGrowthRate, level));
    }

    public int GetCost(EnhanceType type, int count = 1)
    {
        int level = GetLevel(type);
        int total = 0;
        for (int i = 0; i < count; i++)
            total += GetSingleCost(type, level + i);
        return total;
    }

    // ── 강화 실행 ──
    public bool TryEnhance(EnhanceType type, int count = 1)
    {
        int cost = GetCost(type, count);
        if (!EconomyBridge.TryGetAmount(eCurrency.Gold, out int gold) || gold < cost)
            return false;

        EconomyBridge.Add(eCurrency.Gold, -cost);

        if (!_levels.ContainsKey(type)) _levels[type] = 0;
        _levels[type] += count;

        ApplyToAllPlayers();
        Save();
        OnEnhanced?.Invoke();
        return true;
    }

    // ── 모든 플레이어에 강화 보너스 적용 ──
    public void ApplyToAllPlayers()
    {
        var um = UserManager.Instance;
        if (um == null) return;

        // UserManager의 _user 필드에서 플레이어 목록 가져오기
        var userField = typeof(UserManager).GetField("_user",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        if (userField == null) return;

        var user = userField.GetValue(um) as Scripts.Users.User;
        if (user == null || user._players == null) return;

        float atkRate = GetBonusAtkRate();
        float hpRate = GetBonusMaxHPRate();

        foreach (var player in user._players)
        {
            if (player == null || player.playerStatus == null) continue;
            player.playerStatus.SetEnhanceBonus(atkRate, hpRate);
        }
    }

    // ── 표시용 문자열 ──
    public string GetBonusText(EnhanceType type)
    {
        float bonus = GetBonus(type);
        float rounded = Mathf.Round(bonus * 10f) / 10f;
        if (rounded == Mathf.Floor(rounded))
            return $"+{(int)rounded}%";
        return $"+{rounded:F1}%";
    }

    public static string GetTypeName(EnhanceType type)
    {
        switch (type)
        {
            case EnhanceType.Attack:     return "공격력 강화";
            case EnhanceType.MaxHP:      return "체력 강화";
            case EnhanceType.CritRate:   return "치명타 확률 강화";
            case EnhanceType.CritDamage: return "치명타 데미지 강화";
            case EnhanceType.ExpGain:    return "경험치 획득량 강화";
            default: return "";
        }
    }

    // ── 해당 스탯이 플레이어에 실제 구현되어 있는지 여부 ──
    public static bool IsStatImplemented(EnhanceType type)
    {
        return type == EnhanceType.Attack || type == EnhanceType.MaxHP;
    }

    // ── 저장/로드 ──
    [Serializable]
    private class SaveData
    {
        public List<int> keys = new();
        public List<int> vals = new();
    }

    private void Save()
    {
        var d = new SaveData();
        foreach (var kv in _levels)
        {
            d.keys.Add((int)kv.Key);
            d.vals.Add(kv.Value);
        }
        PlayerPrefs.SetString(PrefKey, JsonUtility.ToJson(d));
        PlayerPrefs.Save();
    }

    private void Load()
    {
        string raw = PlayerPrefs.GetString(PrefKey, "");
        if (string.IsNullOrEmpty(raw)) return;
        var d = JsonUtility.FromJson<SaveData>(raw);
        if (d == null) return;
        int len = Mathf.Min(d.keys.Count, d.vals.Count);
        for (int i = 0; i < len; i++)
            _levels[(EnhanceType)d.keys[i]] = d.vals[i];
    }

    // ── 서버 동기화용 데이터 조회 ──
    public Dictionary<EnhanceType, int> GetAllLevels()
    {
        return new Dictionary<EnhanceType, int>(_levels);
    }

    public void LoadFromServer(Dictionary<EnhanceType, int> serverData)
    {
        _levels = new Dictionary<EnhanceType, int>(serverData);
        ApplyToAllPlayers();
        Save();
        OnEnhanced?.Invoke();
    }
}
