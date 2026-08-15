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

    private readonly List<Collider2D> _hitResults = new List<Collider2D>();
    private readonly List<IDamageable> _targets = new List<IDamageable>();
    private readonly LayerMask _enemyLayer = GameLayers.EnemyMask;

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

        float dist = Vector2.Distance(_player.transform.position, target.targetPos);
        return dist <= _range;
    }

    public override float Execute()
    {
        float facing = _player.transform.localScale.x >= 0f ? 1f : -1f;
        Vector2 center = (Vector2)_player.transform.position + new Vector2(facing * _halfWidth, 0f);

        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(_enemyLayer);
        filter.useLayerMask = true;
        filter.useTriggers = true;

        int hitCount = Physics2D.OverlapBox(
            center, new Vector2(_halfWidth * 2f, _halfHeight * 2f), 0f, filter, _hitResults);

        int baseAtk = _player.playerStatus?.Atk ?? 0;
        int damage = Mathf.RoundToInt(baseAtk * _damageMultiplier);
        _targets.Clear();

        for (int i = 0; i < hitCount; i++)
        {
            var mon = _hitResults[i].GetComponentInParent<Monster>();
            if (mon == null || mon.MonAction == eMonsterAction.Dead) continue;

            var d = _hitResults[i].GetComponentInParent<IDamageable>();
            if (d != null && !_targets.Contains(d))
                _targets.Add(d);
        }

        if (_targets.Count > 0)
            _player.SetPendingSkillDamage(_targets, damage);

        float animLen = GetAttackAnimLength();
        _player.SetAnimation(ePlayerAction.Attack);

        _nextAvailableTime = Time.time + animLen + ScaledCooldown(_cooldown);
        return animLen;
    }
}
