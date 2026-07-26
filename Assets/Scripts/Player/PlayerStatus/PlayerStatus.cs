using UnityEngine;

public class PlayerStatus
{
    private int   _baseMaxHP    = 100;
    private int   _baseAtk      = 1;
    private int   _baseMovSpeed = 5;

    private int _equipAtk   = 0;
    private int _equipMaxHP = 0;

    private int _passiveAtk   = 0;
    private int _passiveMaxHP = 0;

    // 오라 버프 승수 (곱연산)
    private float _buffAtkMultiplier   = 1f;
    private float _buffMaxHPMultiplier = 1f;

    private float _enhanceAtkRate   = 0f;
    private float _enhanceMaxHPRate = 0f;

    public int HP { get; set; } = 100;

    public int MaxHP    => Mathf.RoundToInt((_baseMaxHP + _equipMaxHP + _passiveMaxHP) * _buffMaxHPMultiplier * (1f + _enhanceMaxHPRate));
    public int Atk      => Mathf.RoundToInt((_baseAtk + _equipAtk + _passiveAtk) * _buffAtkMultiplier * (1f + _enhanceAtkRate));
    public int MovSpeed => _baseMovSpeed;

    public string JobName { get; set; } = "Warrior";

    public System.Action<string> OnJobChanged;
    /// <summary>Atk / MaxHP / MovSpeed 재계산이 필요한 변경이 발생할 때 호출.</summary>
    public System.Action OnStatsChanged;

    public void ApplyJob(JobData data)
    {
        if (data == null) return;

        _baseMaxHP    = data.maxHP;
        _baseAtk      = data.atk;
        _baseMovSpeed = data.movSpeed;

        JobName = data.jobName;
        OnJobChanged?.Invoke(JobName);

        HP = MaxHP;
        OnStatsChanged?.Invoke();
    }

    public void SetEquipmentBonus(int bonusAtk, int bonusMaxHP)
    {
        _equipAtk   = bonusAtk;
        _equipMaxHP = bonusMaxHP;
        OnStatsChanged?.Invoke();
    }

    public void ResetPassiveBonus()
    {
        _passiveAtk   = 0;
        _passiveMaxHP = 0;
        _buffAtkMultiplier   = 1f;
        _buffMaxHPMultiplier = 1f;
        OnStatsChanged?.Invoke();
    }

    public void AddPassiveBonus(int bonusAtk)
    {
        _passiveAtk += bonusAtk;
        OnStatsChanged?.Invoke();
    }

    public void AddPassiveSelfBonus(int bonusAtk, int bonusMaxHP)
    {
        _passiveAtk   += bonusAtk;
        _passiveMaxHP += bonusMaxHP;
        OnStatsChanged?.Invoke();
    }

    public void ApplyBuffMultiplier(float atkMult, float hpMult)
    {
        _buffAtkMultiplier   *= atkMult;
        _buffMaxHPMultiplier *= hpMult;
        OnStatsChanged?.Invoke();
    }

    public void SetEnhanceBonus(float atkRate, float maxHPRate)
    {
        _enhanceAtkRate   = atkRate;
        _enhanceMaxHPRate = maxHPRate;
        OnStatsChanged?.Invoke();
    }

    // ── 스탯 구성요소 접근자 (상세 스탯 방정식 UI / 팀 공용) ──
    // 최종값 = (base + equip + passive) × buffMultiplier × (1 + enhanceRate)
    public int   BaseAtk            => _baseAtk;
    public int   EquipAtk           => _equipAtk;
    public int   PassiveAtk         => _passiveAtk;
    public float BuffAtkMultiplier  => _buffAtkMultiplier;
    public float EnhanceAtkRate     => _enhanceAtkRate;

    public int   BaseMaxHP          => _baseMaxHP;
    public int   EquipMaxHP         => _equipMaxHP;
    public int   PassiveMaxHP       => _passiveMaxHP;
    public float BuffMaxHPMultiplier => _buffMaxHPMultiplier;
    public float EnhanceMaxHPRate   => _enhanceMaxHPRate;

    public int   BaseMovSpeed       => _baseMovSpeed;

    /// <summary>스탯 방정식 표시용 구성요소 묶음. 최종 = (Base+Equip+Passive) × BuffMult × (1+EnhanceRate).</summary>
    public struct StatBreakdown
    {
        public int   Base;         // 전직 기본
        public int   Equip;        // 장비
        public int   Passive;      // 패시브(가산)
        public float BuffMult;     // 오라 승수 (곱)
        public float EnhanceRate;  // 강화 비율 (예: 0.24 = +24%)
        public int   Final;        // 최종값

        public int   AdditiveSum => Base + Equip + Passive;
        public bool  HasBuff     => Mathf.Abs(BuffMult - 1f) > 0.0001f;
        public bool  HasEnhance  => Mathf.Abs(EnhanceRate) > 0.0001f;
    }

    public StatBreakdown AtkBreakdown() => new StatBreakdown
    {
        Base = _baseAtk, Equip = _equipAtk, Passive = _passiveAtk,
        BuffMult = _buffAtkMultiplier, EnhanceRate = _enhanceAtkRate, Final = Atk
    };

    public StatBreakdown MaxHPBreakdown() => new StatBreakdown
    {
        Base = _baseMaxHP, Equip = _equipMaxHP, Passive = _passiveMaxHP,
        BuffMult = _buffMaxHPMultiplier, EnhanceRate = _enhanceMaxHPRate, Final = MaxHP
    };
}
