using Scripts.Core;
using Scripts.Core.inteface;
using Scripts.Monster;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 단일 대상 기본공격.
/// Spearman · Knight · Archer · Elite_Archer
/// </summary>
public sealed class BasicAttackSingle : ActiveSkill
{
    private readonly float _range;
    private readonly float _cooldown;
    private readonly List<IDamageable> _targets = new List<IDamageable>(1);

    public override string DisplayName => "기본공격";
    public override float Cooldown => _cooldown;
    public float Range => _range;

    public BasicAttackSingle(Player player, float range, float cooldown) : base(player)
    {
        _range = range;
        _cooldown = cooldown;
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
        var target = _player.currentTarget;
        int baseAtk = _player.playerStatus?.Atk ?? 0;

        _targets.Clear();
        _targets.Add(target);
        _player.SetPendingSkillDamage(_targets, baseAtk);

        float animLen = GetAttackAnimLength();
        _player.SetAnimation(ePlayerAction.Attack);

        _nextAvailableTime = Time.time + animLen + _cooldown;
        return animLen;
    }
}
