using System.Collections.Generic;
using UnityEngine;

public class PlayerStatus
{
    // ── 직업 기본 스탯 (ApplyJob에서 설정) ───────────────────────
    private int   _baseMaxHP    = 100;
    private int   _baseAtk      = 1;
    private int   _baseMovSpeed = 5;
    private float _baseAtkSpeed = 1f;

    // ── [장비 시스템] 장비 보너스 스탯 (EquipmentManager.RecalculateStats에서 설정) ──
    private int _equipAtk   = 0;
    private int _equipMaxHP = 0;
    // ── [장비 시스템 끝] ─────────────────────────────────────────

    // ── 현재 체력 (별도 관리, 직업/장비와 독립적으로 증감) ───────
    public int HP { get; set; } = 100;

    // ── 최종 스탯 프로퍼티 (기본 + 장비 보너스 합산) ─────────────
    // [장비 시스템] 기존 단순 auto-property에서 합산 계산으로 변경
    public int   MaxHP    => _baseMaxHP    + _equipMaxHP;
    public int   Atk      => _baseAtk      + _equipAtk;
    public int   MovSpeed => _baseMovSpeed;          // 현재 장비 보너스 없음
    public float AtkSpeed => _baseAtkSpeed;          // 현재 장비 보너스 없음
    // [장비 시스템 끝]

    public string JobName { get; set; } = "Warrior";

    /// <summary>전직 완료 시 발행되는 이벤트. 인자는 새 직업 이름.</summary>
    public System.Action<string> OnJobChanged;

    /// <summary>
    /// JobData의 스탯을 기본 스탯에 적용한다.
    /// 전직 시 HP는 MaxHP로 풀회복된다.
    /// </summary>
    public void ApplyJob(JobData data)
    {
        if (data == null)
        {
            Debug.LogWarning("[PlayerStatus] ApplyJob: JobData가 null입니다.");
            return;
        }

        // [장비 시스템] auto-property 직접 대입 → 기본 스탯 필드에 저장하도록 변경
        _baseMaxHP    = data.maxHP;
        HP            = data.maxHP;   // 전직 시 HP 풀회복
        _baseAtk      = data.atk;
        _baseMovSpeed = data.movSpeed;
        _baseAtkSpeed = data.atkSpeed;
        // [장비 시스템 끝]

        JobName = data.jobName;
        OnJobChanged?.Invoke(JobName);
    }

    // ── [장비 시스템] 장비 보너스 적용 메서드 ────────────────────
    /// <summary>
    /// EquipmentManager.RecalculateStats()에서 호출.
    /// 현재 착용 장비의 합산 보너스를 갱신한다.
    /// </summary>
    public void SetEquipmentBonus(int bonusAtk, int bonusMaxHP)
    {
        _equipAtk   = bonusAtk;
        _equipMaxHP = bonusMaxHP;
    }
    // ── [장비 시스템 끝] ─────────────────────────────────────────
}
