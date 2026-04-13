using Scripts.Core;
using Scripts.Core.inteface;
using Scripts.Monster;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 에너지 파동 (Elite_Mage).
/// 몬스터가 원형 범위 내 진입 → 원형 AoE 피해 + 넉백.
/// </summary>
public sealed class EnergyPulse : ActiveSkill
{
    private readonly float _triggerRange;
    private readonly float _cooldown;
    private readonly float _knockbackForce;

    private readonly LayerMask _enemyLayer = GameLayers.EnemyMask;
    private readonly List<Collider2D> _hitResults = new List<Collider2D>();

    public override string DisplayName => "에너지 파동";
    public override float Cooldown => _cooldown;

    public EnergyPulse(Player player, float triggerRange, float cooldown, float knockbackForce)
        : base(player)
    {
        _triggerRange = triggerRange;
        _cooldown = cooldown;
        _knockbackForce = knockbackForce;
    }

    public override bool CanExecute()
    {
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(_enemyLayer);
        filter.useLayerMask = true;
        filter.useTriggers = true;

        int count = Physics2D.OverlapCircle(
            _player.transform.position, _triggerRange, filter, _hitResults);

        for (int i = 0; i < count; i++)
        {
            var mon = _hitResults[i].GetComponentInParent<Monster>();
            if (mon != null && mon.MonAction != eMonsterAction.Dead) return true;
        }
        return false;
    }

    public override float Execute()
    {
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(_enemyLayer);
        filter.useLayerMask = true;
        filter.useTriggers = true;

        int hitCount = Physics2D.OverlapCircle(
            _player.transform.position, _triggerRange, filter, _hitResults);

        int baseAtk = _player.playerStatus?.Atk ?? 0;
        int skillDamage = baseAtk * 2;
        var proxy = new DamageProxy((ulong)skillDamage, _player);

        for (int i = 0; i < hitCount; i++)
        {
            var mon = _hitResults[i].GetComponentInParent<Monster>();
            if (mon == null || mon.MonAction == eMonsterAction.Dead) continue;

            var damageable = _hitResults[i].GetComponentInParent<IDamageable>();
            damageable?.TakeDamage(proxy);

            Vector2 dir = ((Vector2)mon.transform.position - (Vector2)_player.transform.position).normalized;
            mon.ApplyKnockback(dir, _knockbackForce);
        }

        string animName = "EnergyPulse";
        float animLen = _player.GetClipLength(animName, 0.4f);
        _player.PlaySkillAnimation(animName, animLen);

        _nextAvailableTime = Time.time + animLen + _cooldown;
        return animLen;
    }
}
