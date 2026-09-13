using UnityEngine;
using KingdomIdle.Balance;

public sealed class IronWill : ActiveSkill
{
    private readonly float _cooldown, _duration, _trigger;
    private readonly decimal _rate;
    private float _start;
    private long _paid;
    private long _maxHp;
    private bool _healing;
    public override string DisplayName => "강철 의지";
    public override float Cooldown => _cooldown;
    public override bool IsSelfTriggered => true;
    public IronWill(Player player, float cooldown, float healPercent, float duration, float triggerHPRatio = .5f) : base(player)
    { _cooldown = cooldown; _duration = duration; _rate = (decimal)healPercent; _trigger = triggerHPRatio; }
    public override bool CanExecute() => !_healing && _player.HPRatio > 0 && _player.HPRatio < _trigger;
    public override float Execute()
    {
        _player.PlaySkillAnimation("IronWill", .5f);
        _start = Time.time + .5f; _paid = 0; _maxHp = _player.playerStatus.MaxHP; _healing = true;
        _nextAvailableTime = _start + _duration + _cooldown; return .5f;
    }
    public override void Tick()
    {
        if (!_healing || Time.time < _start) return;
        float elapsed = Mathf.Min(_duration, Time.time - _start);
        long total = BalanceMath.Floor(_maxHp * _rate * (decimal)elapsed);
        if (total > _paid) _player.Heal(total - _paid);
        _paid = total;
        if (elapsed >= _duration) _healing = false;
    }
}
