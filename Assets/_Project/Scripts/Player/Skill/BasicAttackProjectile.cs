using Scripts.Core;
using Scripts.Monster;
using System.Collections.Generic;
using Scripts.Core.inteface;
using UnityEngine;

/// <summary>
/// 투사체 기본공격 (Mage · Elite_Mage).
/// 공격 애니메이션 종료 시점에 투사체 발사 → 적 접촉 또는 수명 만료 시 소멸.
/// </summary>
public sealed class BasicAttackProjectile : ActiveSkill
{
    private readonly float _range;
    private readonly float _cooldown;
    private readonly float _aoeRadius;
    private readonly float _projectileSpeed;
    private readonly float _damageMultiplier;
    private IDamageable _pendingTarget;
    private readonly Queue<MageProjectile> _pool = new Queue<MageProjectile>();

    private const float PROJECTILE_LIFETIME = 10f;

    // 애니메이션 종료 후 발사를 위한 상태
    private enum Phase { Idle, WaitingForReleaseEvent }
    private Phase _phase = Phase.Idle;
    private float _fireTime;
    private int _pendingDamage;

    public override string DisplayName => "기본공격";
    public override float Cooldown => _cooldown;
    public float Range => _range;

    public BasicAttackProjectile(Player player, float range, float cooldown, float aoeRadius, float projectileSpeed = 4f, float damageMultiplier = 1f)
        : base(player)
    {
        _range = range;
        _cooldown = cooldown;
        _aoeRadius = aoeRadius;
        _projectileSpeed = projectileSpeed;
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
        _pendingTarget = _player.currentTarget;
        
        int baseAtk = _player.playerStatus?.Atk ?? 0;
        _pendingDamage = Mathf.RoundToInt(baseAtk * _damageMultiplier);
        
         _phase = Phase.WaitingForReleaseEvent;

         Player.AttackAnimationTiming timing =
             _player.PlayBasicSkillAnimation(ScaledCooldown(_cooldown));

         _nextAvailableTime =
             Time.time + timing.EffectiveInterval;

         return timing.AnimationDuration;
    }

    public void OnProjectileRelease()
    {
        if (_phase != Phase.WaitingForReleaseEvent)
            return;

        _phase = Phase.Idle;

        MageProjectile projectile = GetProjectile();

        if (projectile == null)
            return;

        projectile.transform.position =
            _player.transform.position;
        
        projectile.FireToTarget(
            _player,
            _pendingTarget,
            _projectileSpeed,
            _pendingDamage,
            _aoeRadius,
            PROJECTILE_LIFETIME);

        _pendingTarget = null;     
        
        /*Vector2 direction =
            _player.transform.localScale.x >= 0f
                ? Vector2.right
                : Vector2.left;

        IDamageable target = _pendingTarget;

        if (target != null)
        {
            var targetObject = target as MonoBehaviour;

            if (targetObject != null &&
                targetObject.gameObject.activeInHierarchy)
            {
                direction =
                    ((Vector2)target.targetPos -
                     (Vector2)_player.transform.position).normalized;
            }
        }

        MageProjectile projectile = GetProjectile();

        if (projectile == null)
            return;

        projectile.transform.position =
            _player.transform.position;

        projectile.Fire(
            _player,
            direction,
            _projectileSpeed,
            _pendingDamage,
            _aoeRadius,
            PROJECTILE_LIFETIME);
            */
    }
    public override void Tick(){}

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
