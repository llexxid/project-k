using System;
using System.Collections.Generic;
using UnityEngine;
using Scripts.Core;
using Scripts.Core.Manager;
using Scripts.Server.DTO;
using Newtonsoft.Json;

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

    /// <summary>
    /// 강화 시도 결과. UI 가 구체적인 실패 원인을 사용자에게 보여주기 위해 분리.
    /// 기존 bool TryEnhance 는 Success 만 true 로 치환해 호환성 유지.
    /// </summary>
    public enum EnhanceResult
    {
        Success,
        NotEnoughGold,
        NetworkNotReady,
        ManagerNotReady,
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
    // 구현된 스탯(공격력/체력)은 서버 세션을 통해 PlayFab CloudScript로 동기화된다.
    // 낙관적(optimistic)으로 로컬 차감/레벨 반영 후, 서버 오류 시 자동 롤백한다.
    /// <summary>
    /// 기존 bool 반환 API (호환성 유지). Success 만 true 로 변환한다.
    /// 새 코드는 가능하면 <see cref="TryEnhanceEx"/> 를 써서 실패 원인을 구체적으로 다룰 것.
    /// </summary>
    public bool TryEnhance(EnhanceType type, int count = 1)
        => TryEnhanceEx(type, count) == EnhanceResult.Success;

    /// <summary>
    /// 강화 시도. 실패 시 구체적 사유(EnhanceResult) 반환.
    /// - NotEnoughGold: 골드 부족
    /// - NetworkNotReady: 서버 동기화가 필요한 스탯인데 세션 미준비
    /// - Success: 낙관적 로컬 차감/레벨 반영 완료 (서버 동기화는 비동기 진행)
    /// </summary>
    public EnhanceResult TryEnhanceEx(EnhanceType type, int count = 1)
    {
        int cost = GetCost(type, count);
        if (!EconomyBridge.TryGetAmount(eCurrency.Gold, out long gold) || gold < cost)
            return EnhanceResult.NotEnoughGold;

        // 공격력/체력 강화는 서버가 실제 권한을 가진다.
        // 네트워크 세션이 준비되지 않았다면 로컬 진행 자체를 막는다.
        // (이전엔 false 만 반환해 UI 가 "골드 부족"으로 잘못 안내하는 버그가 있었다.)
        if (IsServerBacked(type) && !IsNetworkReady())
        {
            Debug.LogWarning("[StatEnhanceManager] 네트워크 세션이 준비되지 않아 강화를 진행할 수 없습니다.");
            return EnhanceResult.NetworkNotReady;
        }

        // 낙관적 로컬 차감 + 레벨 반영
        EconomyBridge.Add(eCurrency.Gold, -cost);

        if (!_levels.ContainsKey(type)) _levels[type] = 0;
        _levels[type] += count;

        ApplyToAllPlayers();
        Save();
        OnEnhanced?.Invoke();

        // 서버 동기화 (공격력 / 체력)
        if (IsServerBacked(type))
        {
            TrySyncServer(type, count, cost);
        }

        return EnhanceResult.Success;
    }

    // ── 서버 동기화 ────────────────────────────────────────────────
    private static bool IsServerBacked(EnhanceType type)
    {
        // 서버에 대응되는 CloudScript (OnEnChantATK / OnEnChantHP) 가 존재하는 타입만 서버로 보낸다.
        return type == EnhanceType.Attack || type == EnhanceType.MaxHP;
    }

    private static bool IsNetworkReady()
    {
        var net = NetworkManager.Instance;
        if (net == null) return false;
        string sid = net.GetSessionID();
        return !string.IsNullOrEmpty(sid);
    }

    private void TrySyncServer(EnhanceType type, int count, int refundCost)
    {
        var net = NetworkManager.Instance;
        if (net == null)
        {
            RollbackEnhance(type, count, refundCost, "NetworkManager 없음");
            return;
        }

        Action<PlayFab.CloudScriptModels.ExecuteFunctionResult> onSuccess = (result) =>
        {
            // 서버가 현재 레벨을 내려주면 로컬과 비교하여 보정한다.
            if (result == null || result.FunctionResult == null) return;
            try
            {
                string json = JsonConvert.SerializeObject(result.FunctionResult);
                var dto = JsonConvert.DeserializeObject<OnEnchantResponseDTO>(json);
                if (dto == null) return;

                int serverLevel = (int)dto.CurrentLevel;
                int localLevel = _levels.TryGetValue(type, out int lv) ? lv : 0;
                if (serverLevel != localLevel)
                {
                    Debug.LogWarning($"[StatEnhanceManager] {type} 서버 레벨({serverLevel}) ↔ 로컬({localLevel}) 불일치 — 서버 기준으로 보정");
                    _levels[type] = serverLevel;
                    ApplyToAllPlayers();
                    Save();
                    OnEnhanced?.Invoke();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[StatEnhanceManager] 강화 응답 파싱 실패: {ex.Message}");
            }
        };

        Action<PlayFab.PlayFabError> onError = (error) =>
        {
            string msg = error != null ? error.ErrorMessage : "알 수 없는 서버 오류";
            RollbackEnhance(type, count, refundCost, msg);
        };

        if (type == EnhanceType.Attack)
        {
            net.OnEnchantATK(count, onSuccess, onError);
        }
        else if (type == EnhanceType.MaxHP)
        {
            net.OnEnchantHp(count, onSuccess, onError);
        }
    }

    private void RollbackEnhance(EnhanceType type, int count, int refundCost, string reason)
    {
        Debug.LogWarning($"[StatEnhanceManager] 강화 롤백 ({type}, count={count}, refund={refundCost}): {reason}");

        EconomyBridge.Add(eCurrency.Gold, refundCost);

        if (_levels.TryGetValue(type, out int lv))
        {
            _levels[type] = Mathf.Max(0, lv - count);
        }

        ApplyToAllPlayers();
        Save();
        OnEnhanced?.Invoke();
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
