using System;
using KingdomIdle.Balance;

public class PlayerStatus
{
    private int _baseMaxHP = 200, _baseAtk = 30, _baseMovSpeed = 3;
    private int _equipAtk, _equipMaxHP, _passiveAtk, _passiveMaxHP;
    private decimal _auraAttack, _auraHealth;
    private int _attackLevel, _healthLevel, _accountLevel = 1, _reincarnationLevel;
    public long HP { get; set; } = 200;
    public long MaxHP => MaxHPBreakdown().Final;
    public long Atk => AtkBreakdown().Final;
    public int MovSpeed => _baseMovSpeed;
    public string JobName { get; set; } = "Spearman";
    public Action<string> OnJobChanged;
    public Action OnStatsChanged;
    public void ApplyJob(JobData data)
    {
        if (data == null) return;
        _baseMaxHP = data.maxHP; _baseAtk = data.atk; _baseMovSpeed = data.movSpeed;
        JobName = data.jobName; HP = Math.Min(HP, MaxHP);
        OnJobChanged?.Invoke(JobName); OnStatsChanged?.Invoke();
    }
    public void SetEquipmentBonus(int attack, int health) { if (_equipAtk == attack && _equipMaxHP == health) return; _equipAtk = attack; _equipMaxHP = health; HP = Math.Min(HP, MaxHP); OnStatsChanged?.Invoke(); }
    public void SetProgression(int attack, int health, int account, int reincarnation)
    {
        if (_attackLevel == attack && _healthLevel == health && _accountLevel == account && _reincarnationLevel == reincarnation) return;
        _attackLevel = attack; _healthLevel = health; _accountLevel = account; _reincarnationLevel = reincarnation;
        HP = Math.Min(HP, MaxHP); OnStatsChanged?.Invoke();
    }
    public void SetAura(decimal attack, decimal health)
    {
        if (_auraAttack == attack && _auraHealth == health) return;
        _auraAttack = attack; _auraHealth = health; HP = Math.Min(HP, MaxHP); OnStatsChanged?.Invoke();
    }
    public void ResetPassiveBonus() { _passiveAtk = _passiveMaxHP = 0; SetAura(0, 0); }
    public void AddPassiveBonus(int attack) => AddPassiveSelfBonus(attack, 0);
    public void AddPassiveSelfBonus(int attack, int health) { _passiveAtk = checked(_passiveAtk + attack); _passiveMaxHP = checked(_passiveMaxHP + health); OnStatsChanged?.Invoke(); }
    public void ApplyBuffMultiplier(float attack, float health) => SetAura(_auraAttack + (decimal)attack - 1m, _auraHealth + (decimal)health - 1m);
    public void SetEnhanceBonus(float attack, float health)
    {
        var s = LocalProgression.State; SetProgression(s.AttackLevel, s.HealthLevel, s.AccountLevel, s.ReincarnationLevel);
    }
    public int BaseAtk => _baseAtk;
    public int EquipAtk => _equipAtk;
    public int PassiveAtk => _passiveAtk;
    public float BuffAtkMultiplier => (float)Group(_auraAttack);
    public float EnhanceAtkRate => (float)(BalanceMath.GoldMultiplier(_attackLevel) - 1m);
    public int BaseMaxHP => _baseMaxHP;
    public int EquipMaxHP => _equipMaxHP;
    public int PassiveMaxHP => _passiveMaxHP;
    public float BuffMaxHPMultiplier => (float)Group(_auraHealth);
    public float EnhanceMaxHPRate => (float)(BalanceMath.GoldMultiplier(_healthLevel) - 1m);
    public int BaseMovSpeed => _baseMovSpeed;
    private decimal Group(decimal aura) => 1m + aura + .002m * (_accountLevel - 1) + .01m * _reincarnationLevel;
    public struct StatBreakdown
    {
        public int Base, Equip, Passive;
        public float BuffMult, EnhanceRate;
        public long Final;
        public decimal AuraRate, AccountRate, ReincarnationRate, GrowthMultiplier;
        public long AdditiveSum => (long)Base + Equip + Passive;
        public bool HasBuff => Math.Abs(BuffMult - 1f) > .0001f;
        public bool HasEnhance => Math.Abs(EnhanceRate) > .0001f;
    }
    private StatBreakdown Breakdown(int basis, int equip, int passive, int level, decimal aura) => new()
    {
        Base = basis, Equip = equip, Passive = passive, AuraRate = aura,
        AccountRate = .002m * (_accountLevel - 1), ReincarnationRate = .01m * _reincarnationLevel,
        GrowthMultiplier = BalanceMath.GoldMultiplier(level), BuffMult = (float)Group(aura),
        EnhanceRate = (float)(BalanceMath.GoldMultiplier(level) - 1m),
        Final = BalanceMath.Stat(basis, equip, passive, level, aura, _accountLevel, _reincarnationLevel)
    };
    public StatBreakdown AtkBreakdown() => Breakdown(_baseAtk, _equipAtk, _passiveAtk, _attackLevel, _auraAttack);
    public StatBreakdown MaxHPBreakdown() => Breakdown(_baseMaxHP, _equipMaxHP, _passiveMaxHP, _healthLevel, _auraHealth);
}
