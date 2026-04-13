using UnityEngine;

public class PlayerStatus
{
    private int   _baseMaxHP    = 100;
    private int   _baseAtk      = 1;
    private int   _baseMovSpeed = 5;
    private float _baseAtkSpeed = 1f;

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

    public int   MaxHP    => Mathf.RoundToInt((_baseMaxHP + _equipMaxHP + _passiveMaxHP) * _buffMaxHPMultiplier * (1f + _enhanceMaxHPRate));
    public int   Atk      => Mathf.RoundToInt((_baseAtk + _equipAtk + _passiveAtk) * _buffAtkMultiplier * (1f + _enhanceAtkRate));
    public int   MovSpeed => _baseMovSpeed;
    public float AtkSpeed => _baseAtkSpeed;

    public string JobName { get; set; } = "Warrior";

    public System.Action<string> OnJobChanged;

    public void ApplyJob(JobData data)
    {
        if (data == null) return;

        _baseMaxHP    = data.maxHP;
        _baseAtk      = data.atk;
        _baseMovSpeed = data.movSpeed;
        _baseAtkSpeed = data.atkSpeed;

        JobName = data.jobName;
        OnJobChanged?.Invoke(JobName);

        HP = MaxHP;
    }

    public void SetEquipmentBonus(int bonusAtk, int bonusMaxHP)
    {
        _equipAtk   = bonusAtk;
        _equipMaxHP = bonusMaxHP;
    }

    public void ResetPassiveBonus()
    {
        _passiveAtk   = 0;
        _passiveMaxHP = 0;
        _buffAtkMultiplier   = 1f;
        _buffMaxHPMultiplier = 1f;
    }

    public void AddPassiveBonus(int bonusAtk) => _passiveAtk += bonusAtk;

    public void AddPassiveSelfBonus(int bonusAtk, int bonusMaxHP)
    {
        _passiveAtk   += bonusAtk;
        _passiveMaxHP += bonusMaxHP;
    }

    public void ApplyBuffMultiplier(float atkMult, float hpMult)
    {
        _buffAtkMultiplier   *= atkMult;
        _buffMaxHPMultiplier *= hpMult;
    }

    public void SetEnhanceBonus(float atkRate, float maxHPRate)
    {
        _enhanceAtkRate   = atkRate;
        _enhanceMaxHPRate = maxHPRate;
    }
}
