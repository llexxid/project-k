using System;
using System.Collections.Generic;
using KingdomIdle.Balance;
using Scripts.Core;
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
    /// <summary>실제 강화 요청을 검증하고 소비·레벨·실습 영수증을 함께 저장한다. 실패는 초안을 폐기하며 입력/UI 정리는 안내 Player가 맡는다.</summary>
    public EnhanceResult TryEnhanceEx(EnhanceType type, int count = 1)
    {
        if (!Direction.GameDirectInteraction.CanPerform(type == EnhanceType.Attack ? Direction.GuideAction.AttackOnce : Direction.GuideAction.None, count)) return EnhanceResult.Busy;
        if (!IsStatImplemented(type) || count == 0 || count < -1 || count > BalanceMath.GoldCap) return EnhanceResult.InvalidRequest;
        if (IsEnhancing) return EnhanceResult.Busy;
        if (GetLevel(type) >= BalanceMath.GoldCap) return EnhanceResult.MaxLevel;
        int actual = GetPurchaseCount(type, count);
        if (actual <= 0) return EnhanceResult.NotEnoughGold;
        int before = GetLevel(type);
        long cost = BalanceMath.GoldTotal(before, actual);
        if (LocalProgression.Balance(eCurrency.Gold) < cost) return EnhanceResult.NotEnoughGold;
        IsEnhancing = true;
        bool success;
        try
        {
            success = LocalProgression.Execute("gold-enhance", s =>
            {
                if ((type == EnhanceType.Attack ? s.AttackLevel : s.HealthLevel) != before || !LocalProgression.Spend(s, eCurrency.Gold, cost)) return false;
                if (type == EnhanceType.Attack) s.AttackLevel += actual; else s.HealthLevel += actual;
                if (type == EnhanceType.Attack) Direction.GameDirectInteraction.RecordSuccess(s, Direction.GuideAction.AttackOnce);
                return true;
            });
        }
        finally { IsEnhancing = false; }
        if (!success) Direction.GameDirectInteraction.ReportSaveFailure();
        if (success) { ApplyToAllPlayers(); OnEnhanced?.Invoke(); }
        OnEnhanceCompleted?.Invoke(type, success);
        return success ? EnhanceResult.Success : EnhanceResult.SaveFailed;
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
