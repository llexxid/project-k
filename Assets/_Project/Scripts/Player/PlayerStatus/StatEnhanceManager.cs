using System;
using System.Collections.Generic;
using KingdomIdle.Balance;
using PlayFab;
using Protocol.Message.Dto;
using Scripts.Core;
using Scripts.Core.Manager;
using UnityEngine;

public class StatEnhanceManager : MonoBehaviour
{
    public static StatEnhanceManager Instance { get; private set; }
    public enum EnhanceType { Attack, MaxHP, CritRate, CritDamage, ExpGain }
    public enum EnhanceResult { Success, NotEnoughGold, NetworkNotReady, ManagerNotReady, Pending, Busy, InvalidRequest, MaxLevel, SaveFailed }
    public event Action OnEnhanced;
    public event Action<EnhanceType, bool> OnEnhanceCompleted;
    public bool IsEnhancing { get; private set; }
    private int _appliedAttack = -1, _appliedHealth = -1, _appliedAccount = -1, _appliedReincarnation = -1;
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        LocalProgression.Changed += HandleProgressionChanged;
    }
    private void OnDestroy() { LocalProgression.Changed -= HandleProgressionChanged; if (Instance == this) Instance = null; }
    private void HandleProgressionChanged()
    {
        var s = LocalProgression.State;
        if (s.AttackLevel == _appliedAttack && s.HealthLevel == _appliedHealth && s.AccountLevel == _appliedAccount && s.ReincarnationLevel == _appliedReincarnation) return;
        ApplyToAllPlayers(); OnEnhanced?.Invoke();
    }
    public int GetLevel(EnhanceType type) => type == EnhanceType.Attack ? LocalProgression.State.AttackLevel : type == EnhanceType.MaxHP ? LocalProgression.State.HealthLevel : 0;
    public float GetBonus(EnhanceType type) => IsStatImplemented(type) ? (float)((BalanceMath.GoldMultiplier(GetLevel(type)) - 1m) * 100m) : 0;
    public float GetBonusAtkRate() => GetBonus(EnhanceType.Attack) / 100f;
    public float GetBonusMaxHPRate() => GetBonus(EnhanceType.MaxHP) / 100f;
    public long GetSingleCost(EnhanceType type, int level) => IsStatImplemented(type) ? BalanceMath.GoldCost(level) ?? -1 : -1;
    public int GetPurchaseCount(EnhanceType type, int requested)
    {
        int remaining = BalanceMath.GoldCap - GetLevel(type);
        return requested == -1 ? BalanceMath.AffordableGoldLevels(GetLevel(type), LocalProgression.Balance(eCurrency.Gold)) : Math.Max(0, Math.Min(requested, remaining));
    }
    public long GetCost(EnhanceType type, int count = 1)
    {
        int actual = GetPurchaseCount(type, count);
        return !IsStatImplemented(type) || actual == 0 ? -1 : BalanceMath.GoldTotal(GetLevel(type), actual);
    }
    public bool TryEnhance(EnhanceType type, int count = 1) => TryEnhanceEx(type, count) == EnhanceResult.Success;
    /// <summary>마지막 서버 거절 또는 적용 실패. Business 오류면 CustomData에 서버 ErrorResponseDTO가 들어 있다.</summary>
    public PlayFabError LastFailure { get; private set; }
    /// <summary>
    /// 강화 요청을 서버(OnEnChantATK/OnEnChantHP)로 보낸다. 비용 차감·단계 상승은 서버가 확정하고 응답의 절대값을 저장한다.
    /// 로컬 검사는 안내용이다. Pending을 돌려준 뒤 OnEnhanceCompleted로 결과를 알린다.
    /// </summary>
    public EnhanceResult TryEnhanceEx(EnhanceType type, int count = 1)
    {
        if (!Direction.GameDirectInteraction.CanPerform(type == EnhanceType.Attack ? Direction.GuideAction.AttackOnce : Direction.GuideAction.None, count)) return EnhanceResult.Busy;
        // 서버는 Count 1/10/-1(MAX)만 받는다(1004 InvalidEnchantCount).
        if (!IsStatImplemented(type) || (count != 1 && count != 10 && count != -1)) return EnhanceResult.InvalidRequest;
        if (IsEnhancing) return EnhanceResult.Busy;
        if (GetLevel(type) >= BalanceMath.GoldCap) return EnhanceResult.MaxLevel;
        var network = NetworkManager.Instance;
        if (network == null || !network.IsGameDataReady) return EnhanceResult.NetworkNotReady;
        if (GetPurchaseCount(type, count) <= 0) return EnhanceResult.NotEnoughGold;
        IsEnhancing = true;
        LastFailure = null;
        if (type == EnhanceType.Attack)
        {
            network.Call<OnEnchantATKRequestDTO, OnEnchantResponseDTO>(new OnEnchantATKRequestDTO { Count = count },
                response => ApplyServerResult(type, count, response), error => RejectServerResult(type, count, error));
        }
        else
        {
            network.Call<OnEnchantHPRequestDTO, OnEnchantResponseDTO>(new OnEnchantHPRequestDTO { Count = count },
                response => ApplyServerResult(type, count, response), error => RejectServerResult(type, count, error));
        }
        return EnhanceResult.Pending;
    }
    /// <summary>서버가 확정한 단계·잔액을 저장한다. 저장이 Changed를 발행하므로 스탯·UI는 HandleProgressionChanged가 갱신한다.</summary>
    private void ApplyServerResult(EnhanceType type, int count, OnEnchantResponseDTO response)
    {
        bool saved;
        try
        {
            saved = LocalProgression.Execute("server-enhance", s =>
            {
                int level = (int)Math.Min(BalanceMath.GoldCap, Math.Max(0, response.CurrentLevel));
                if (type == EnhanceType.Attack) s.AttackLevel = level; else s.HealthLevel = level;
                s.Wallet[eCurrency.Gold] = Math.Max(0, response.CurrentGold);
                if (type == EnhanceType.Attack && response.AppliedCount > 0) Direction.GameDirectInteraction.RecordSuccess(s, Direction.GuideAction.AttackOnce);
                return true;
            });
        }
        finally { IsEnhancing = false; }
        Debug.Log($"[Enhance] {type} count={count} applied={response.AppliedCount} spent={response.SpentGold} level={response.CurrentLevel} gold={response.CurrentGold} saved={saved}");
        if (!saved)
        {
            Direction.GameDirectInteraction.ReportSaveFailure();
            LastFailure = NetworkManager.CreateError("ApplyFailed", "서버 강화 결과를 저장하지 못했습니다: " + LocalProgression.LastError);
        }
        else { ApplyToAllPlayers(); OnEnhanced?.Invoke(); }
        OnEnhanceCompleted?.Invoke(type, saved);
    }
    private void RejectServerResult(EnhanceType type, int count, PlayFabError error)
    {
        IsEnhancing = false;
        LastFailure = error;
        int code = NetworkManager.TryGetBusinessError(error, out var business) ? (int)business.ErrorCode : 0;
        Debug.LogWarning($"[Enhance] {type} count={count} rejected code={code} message={error?.ErrorMessage}");
        OnEnhanceCompleted?.Invoke(type, false);
    }
    public void ApplyToAllPlayers()
    {
        var s = LocalProgression.State;
        _appliedAttack = s.AttackLevel; _appliedHealth = s.HealthLevel; _appliedAccount = s.AccountLevel; _appliedReincarnation = s.ReincarnationLevel;
        var players = UserManager.Instance?.GetPlayers();
        if (players == null) return;
        foreach (var player in players) player?.playerStatus?.SetProgression(s.AttackLevel, s.HealthLevel, s.AccountLevel, s.ReincarnationLevel);
    }
    public string GetBonusText(EnhanceType type) => $"×{BalanceMath.GoldMultiplier(GetLevel(type)):0.####}";
    public static string GetTypeName(EnhanceType type) => type == EnhanceType.Attack ? "공격력 강화" : type == EnhanceType.MaxHP ? "체력 강화" : "미사용 강화";
    public static bool IsStatImplemented(EnhanceType type) => type == EnhanceType.Attack || type == EnhanceType.MaxHP;
    public Dictionary<EnhanceType, int> GetAllLevels() => new() { [EnhanceType.Attack] = GetLevel(EnhanceType.Attack), [EnhanceType.MaxHP] = GetLevel(EnhanceType.MaxHP) };
    public void LoadFromServer(Dictionary<EnhanceType, int> serverData) => Debug.LogWarning("[Progression] Unversioned server enhancement snapshot ignored by local authority.");
}
