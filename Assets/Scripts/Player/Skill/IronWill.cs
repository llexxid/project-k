using UnityEngine;

/// <summary>
/// 강철 의지 (Elite_Knight).
/// HP 50% 미만 → 자동 발동 → 지속시간 동안 최대HP의 n%/초 회복.
/// 쿨다운은 효과 종료 후 시작.
/// </summary>
public sealed class IronWill : ActiveSkill
{
    private readonly float _cooldown;
    private readonly float _healPercent;   // 초당 회복 비율 (0.1 = 10%)
    private readonly float _duration;      // 회복 지속시간(초)

    private enum Phase { Idle, Animating, Healing }
    private Phase _phase = Phase.Idle;
    private float _animEndTime;
    private float _healEndTime;

    public override string DisplayName => "강철 의지";
    public override float Cooldown => _cooldown;
    public override bool IsSelfTriggered => true;

    public IronWill(Player player, float cooldown, float healPercent, float duration) : base(player)
    {
        _cooldown = cooldown;
        _healPercent = healPercent;
        _duration = duration;
    }

    public override bool CanExecute()
    {
        return _player.HPRatio < 0.5f;
    }

    public override float Execute()
    {
        string animName = "IronWill";
        float animLen = _player.GetClipLength(animName, 0.5f);
        _player.PlaySkillAnimation(animName, animLen);

        _phase = Phase.Animating;
        _animEndTime = Time.time + animLen;
        _healEndTime = Time.time + animLen + _duration;

        // 효과 종료 전까지 재사용 불가 (쿨다운은 Tick에서 설정)
        _nextAvailableTime = float.MaxValue;

        return animLen;
    }

    public override void Tick()
    {
        if (_phase == Phase.Idle) return;

        if (_phase == Phase.Animating && Time.time >= _animEndTime)
            _phase = Phase.Healing;

        if (_phase == Phase.Healing)
        {
            if (Time.time >= _healEndTime)
            {
                _phase = Phase.Idle;
                _nextAvailableTime = Time.time + _cooldown;
                return;
            }

            int maxHP = _player.playerStatus?.MaxHP ?? 1;
            int healAmount = Mathf.RoundToInt(maxHP * _healPercent * Time.deltaTime);
            if (healAmount > 0) _player.Heal(healAmount);
        }
    }
}
