using KingdomIdle.Balance;
using Scripts.Core;
using Scripts.Core.inteface;
using Scripts.Monster;
using UnityEngine;

/// <summary>
/// 집중 사격 (Elite_Archer).
/// 기본공격과 동일 조건 → 애니메이션 후 3연속 타격.
/// 쿨다운은 모든 타격 완료 후 시작.
/// </summary>
public sealed class ChargeShot : ActiveSkill
{
    private readonly float _range;
    private readonly float _cooldown;
    private readonly int _hitCount;
    private readonly float _damageMultiplier;

    private enum Phase { Idle, Animating, Hitting }
    private Phase _phase = Phase.Idle;
    private float _animEndTime;
    private int _hitsRemaining;
    private float _nextHitTime;
    private IDamageable _target;
    private int _generation;
    public override bool IsActive => _phase != Phase.Idle;
    private long _hitDamage;

    private const float HIT_INTERVAL = 0.15f;

    public override string DisplayName => "집중 사격";
    public override float Cooldown => _cooldown;

    public ChargeShot(Player player, float range, float cooldown, int hitCount, float damageMultiplier = 1f) : base(player)
    {
        _range = range;
        _cooldown = cooldown;
        _hitCount = hitCount;
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

        float dist = Vector2.Distance(_player.transform.position, target.targetPos);
        return dist <= _range;
    }

    public override float Execute()
    {
        _target = _player.currentTarget;
        _generation = (_target as MonoBehaviour)?.GetComponentInParent<Monster>()?.AllocGen ?? -1;
        long baseAtk = _player.playerStatus?.Atk ?? 0;
        _hitDamage = BalanceMath.Damage(baseAtk, (decimal)_damageMultiplier);
        _hitsRemaining = _hitCount;

        string animName = "Tripple_Shot_Anim";
        float animLen = 0.5f;
        _player.PlaySkillAnimation(animName, animLen);

        _phase = Phase.Animating;
        _animEndTime = Time.time + animLen;

        // 모든 타격 완료 전까지 쿨다운 시작하지 않음
        _nextAvailableTime = Time.time + _cooldown;

        return 0.8f;
    }

    public override void Tick()
    {
        if (_phase == Phase.Idle) return;

        if (_phase == Phase.Animating && Time.time >= _animEndTime)
        {
            _phase = Phase.Hitting;
            _nextHitTime = _animEndTime;
        }

        while (_phase == Phase.Hitting && Time.time >= _nextHitTime && _hitsRemaining > 0)
        {
            ApplyOneHit();
            _hitsRemaining--;
            _nextHitTime += HIT_INTERVAL;

            if (_hitsRemaining <= 0)
            {
                _phase = Phase.Idle;

            }
        }
    }

    private void ApplyOneHit()
    {
        if (_target == null) return;

        var mono = _target as MonoBehaviour;
        if (mono == null || !mono.gameObject.activeInHierarchy) return;

        var mon = mono.GetComponentInParent<Monster>();
        if (mon != null && (mon.MonAction == eMonsterAction.Dead || mon.AllocGen != _generation)) return;

        var proxy = new DamageProxy((ulong)_hitDamage, _player);
        _target.TakeDamage(proxy);
    }
}
