using Scripts.Core;
using Scripts.Monster;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 투사체 기본공격 (Mage · Elite_Mage).
/// 직선 발사 → 몬스터 접촉 시 소멸 + 소범위 AoE.
/// </summary>
public sealed class BasicAttackProjectile : ActiveSkill
{
    private readonly float _range;
    private readonly float _cooldown;
    private readonly float _aoeRadius;
    private readonly Queue<MageProjectile> _pool = new Queue<MageProjectile>();

    public override string DisplayName => "기본공격";
    public override float Cooldown => _cooldown;
    public float Range => _range;

    public BasicAttackProjectile(Player player, float range, float cooldown, float aoeRadius)
        : base(player)
    {
        _range = range;
        _cooldown = cooldown;
        _aoeRadius = aoeRadius;
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
        Vector2 dir = ((Vector2)target.targetPos - (Vector2)_player.transform.position).normalized;

        int baseAtk = _player.playerStatus?.Atk ?? 0;
        MageProjectile proj = GetProjectile();
        if (proj != null)
        {
            proj.transform.position = _player.transform.position;
            proj.Fire(_player, dir, speed: 8f, damage: baseAtk, _aoeRadius, _range);
        }

        float animLen = GetAttackAnimLength();
        _player.SetAnimation(ePlayerAction.Attack);

        _nextAvailableTime = Time.time + animLen + _cooldown;
        return animLen;
    }

    private MageProjectile GetProjectile()
    {
        while (_pool.Count > 0)
        {
            var p = _pool.Dequeue();
            if (p != null)
            {
                p.gameObject.SetActive(true);
                return p;
            }
        }

        var prefab = _player.MageProjectilePrefab;
        if (prefab == null)
        {
            Debug.LogWarning("[BasicAttackProjectile] MageProjectile 프리팹이 Player에 할당되지 않았습니다.");
            return null;
        }

        var obj = Object.Instantiate(prefab.gameObject);
        var mp = obj.GetComponent<MageProjectile>();
        mp.Init(ReturnToPool);
        return mp;
    }

    private void ReturnToPool(MageProjectile p)
    {
        if (p == null) return;
        p.gameObject.SetActive(false);
        _pool.Enqueue(p);
    }
}
