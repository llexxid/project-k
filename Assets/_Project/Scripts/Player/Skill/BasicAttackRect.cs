using KingdomIdle.Balance;
using KingdomIdle.Combat;
using Scripts.Core;
using Scripts.Core.inteface;
using Scripts.Monster;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 전방 직사각형 범위 기본공격 (Elite_Knight).
/// </summary>
public sealed class BasicAttackRect : ActiveSkill
{
    private readonly float _range;
    private readonly float _halfWidth;
    private readonly float _halfHeight;
    private readonly float _cooldown;
    private readonly float _damageMultiplier;

    private readonly List<IDamageable> _targets = new List<IDamageable>();

    public override string DisplayName => "기본공격";
    public override float Cooldown => _cooldown;
    public float Range => _range;

    public BasicAttackRect(Player player, float range, float halfWidth, float halfHeight, float cooldown, float damageMultiplier = 1f)
        : base(player)
    {
        _range = range;
        _halfWidth = halfWidth;
        _halfHeight = halfHeight;
        _cooldown = cooldown;
        _damageMultiplier = damageMultiplier;
    }

    public override bool CanExecute()
    {
        var target = _player.currentTarget;
        if (target == null) return false;

        var mono = target as MonoBehaviour;
        if (mono == null || !mono.gameObject.activeInHierarchy) return false;

        var mon = mono.GetComponentInParent<Monster>();
        if (mon != null && mon.MonAction == eMonsterAction.Dead) return false;

        return _player.IsInMeleeReach(target.targetPos, _range);
    }

    public override float Execute()
    {
        long baseAtk = _player.playerStatus?.Atk ?? 0;
        long damage = BalanceMath.Damage(baseAtk, (decimal)_damageMultiplier);
        _targets.Clear();
        // Combat uses feet, while tall monsters' colliders sit above their feet.
        // Prioritize the selected victim, then up to two neighbours in the same lane.
        if (_player.currentTarget is Monster primary && primary.MonAction != eMonsterAction.Dead &&
            _player.IsInMeleeReach(primary.transform.position, _range)) _targets.Add(primary);
        foreach (var mon in CombatMotion.Monsters)
        {
            if (_targets.Count >= 3) break;
            if (mon == null || mon.MonAction == eMonsterAction.Dead || _targets.Contains(mon)) continue;
            if (_player.IsInMeleeReach(mon.transform.position, _range, CombatMotion.MeleeLane)) _targets.Add(mon);
        }

        if (_targets.Count > 0)
            _player.SetPendingSkillDamage(_targets, damage);
        
        Player.AttackAnimationTiming timing =
            _player.PlayBasicSkillAnimation(ScaledCooldown(_cooldown));

        _nextAvailableTime =
            Time.time + timing.EffectiveInterval;

        return timing.AnimationDuration;
        /*
        float animLen = GetAttackAnimLength();
        _player.SetAnimation(ePlayerAction.Attack);

        _nextAvailableTime = Time.time + animLen + ScaledCooldown(_cooldown);
        return animLen;
        */
    }
}
