using UnityEngine;
using KingdomIdle.Balance;
using KingdomIdle.Combat;

public sealed class IronWill : ActiveSkill
{
    private readonly float _cooldown, _duration;
    private readonly decimal _shieldFraction;
    public override string DisplayName => "강철의 의지";
    public override float Cooldown => _cooldown;
    public override bool IsSelfTriggered => true;
    public IronWill(Player player, float cooldown, float healPercent, float duration) : base(player)
    {
        _cooldown = cooldown; _duration = duration;
        // Preserve the previous total defensive budget: 4% x 5s becomes a 20% shield.
        _shieldFraction = (decimal)(healPercent * duration);
    }
    public override bool CanExecute() => _player.isActiveAndEnabled && !_player.IsDead && _player.HPRatio > 0;
    public override float Execute()
    {
        _player.PlaySkillAnimation("IronWill", .65f);
        _player.GrantShield(BalanceMath.Floor(_player.playerStatus.MaxHP * _shieldFraction), _duration);
        foreach (var monster in CombatMotion.Monsters)
            if (monster != null && Vector2.Distance(monster.transform.position, _player.transform.position) <= 2.2f)
                monster.TryTaunt(_player);
        _nextAvailableTime = Time.time + _cooldown;
        return .65f;
    }
}
