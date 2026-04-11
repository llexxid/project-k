using System.Collections.Generic;
using UnityEngine;

public class PlayerStatus
{
    // ── 직업 기본 스탯 (ApplyJob에서 설정) ───────────────────────
    private int   _baseMaxHP    = 100;
    private int   _baseAtk      = 1;
    private int   _baseMovSpeed = 5;
    private float _baseAtkSpeed = 1f;

    // ── 장비 보너스 스탯 ────────────────────────────────────────────
    private int _equipAtk   = 0;
    private int _equipMaxHP = 0;

    // ── 패시브 스킬 보너스 스탯 ────────────────────────────────────────────────
    private int _passiveAtk    = 0;   // 오라/자기 강화로 누적된 공격력 보너스
    private int _passiveMaxHP  = 0;   // 자기 강화 패시브로 누적된 최대 HP 보너스

    // ── 글로벌 강화 보너스 (StatEnhanceManager에서 설정, % 증가) ──
    private float _enhanceAtkRate   = 0f;
    private float _enhanceMaxHPRate = 0f;

    // ── 현재 체력 (별도 관리, 직업/장비와 독립적으로 증감) ───────
    public int HP { get; set; } = 100;

    // ── 최종 스탯 프로퍼티 (기본 + 장비 보너스 합산) ─────────────
    public int   MaxHP    => Mathf.RoundToInt((_baseMaxHP + _equipMaxHP + _passiveMaxHP) * (1f + _enhanceMaxHPRate));
    public int   Atk      => Mathf.RoundToInt((_baseAtk + _equipAtk + _passiveAtk) * (1f + _enhanceAtkRate));
    public int   MovSpeed => _baseMovSpeed;          // 현재 장비 보너스 없음
    public float AtkSpeed => _baseAtkSpeed;          // 현재 장비 보너스 없음

    public string JobName { get; set; } = "Warrior";

    /// <summary>전직 완료 시 발행되는 이벤트. 인자는 새 직업 이름.</summary>
    public System.Action<string> OnJobChanged;

    /// <summary>
    /// JobData의 스탯을 기본 스탯에 적용한다.
    /// 전직 시 HP는 MaxHP로 풀회복된다.
    /// </summary>
    public void ApplyJob(JobData data)
    {
        if (data == null) return;

        _baseMaxHP    = data.maxHP;
        HP            = data.maxHP;   // 전직 시 HP 풀회복
        _baseAtk      = data.atk;
        _baseMovSpeed = data.movSpeed;
        _baseAtkSpeed = data.atkSpeed;

        JobName = data.jobName;
        OnJobChanged?.Invoke(JobName);
    }

    /// <summary>
    /// EquipmentManager.RecalculateStats()에서 호출.
    /// 현재 착용 장비의 합산 보너스를 갱신한다.
    /// </summary>
    public void SetEquipmentBonus(int bonusAtk, int bonusMaxHP)
    {
        _equipAtk   = bonusAtk;
        _equipMaxHP = bonusMaxHP;
    }

    /// <summary>
    /// 패시브 스킬 재계산 전 호출. 누적된 모든 패시브 보너스를 0으로 초기화한다.
    /// </summary>
    public void ResetPassiveBonus()
    {
        _passiveAtk   = 0;
        _passiveMaxHP = 0;
    }

    /// <summary>
    /// 다른 플레이어의 오라 패시브로부터 공격력 보너스를 누적한다.
    /// </summary>
    public void AddPassiveBonus(int bonusAtk) => _passiveAtk += bonusAtk;

    /// <summary>
    /// 자기 강화 패시브 스킬의 공격력·최대 HP 보너스를 본인에게 누적한다.
    /// </summary>
    public void AddPassiveSelfBonus(int bonusAtk, int bonusMaxHP)
    {
        _passiveAtk   += bonusAtk;
        _passiveMaxHP += bonusMaxHP;
    }

    // ── 글로벌 강화 보너스 (StatEnhanceManager 연동, % 단위) ──
    public void SetEnhanceBonus(float atkRate, float maxHPRate)
    {
        _enhanceAtkRate   = atkRate;
        _enhanceMaxHPRate = maxHPRate;
    }
}
