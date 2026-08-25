using Scripts.Core;
using Scripts.Core.inteface;
using Scripts.Monster;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Mage 기본공격 투사체 (MagicMissile 프리팹에 부착).
/// 직선 이동 → 몬스터 접촉 시 소범위 AoE 피해 → 소멸 애니메이션 후 풀 반환.
/// 기존 MagicMissile 프리팹 구성: SpriteRenderer + CircleCollider2D(trigger) + Animator.
/// </summary>
public class MageProjectile : MonoBehaviour
{
    private Player _owner;
    private Vector2 _direction;
    private float _speed;
    private int _damage;
    private float _aoeRadius;
    private float _lifetime;
    private float _expireTime;
    private bool _alive;

    private float _collisionRadius = 0.2f;
    private float _returnTime = -1f;

    private Animator _animator;
    private static readonly int _isHitHash = Animator.StringToHash("IsHit");

    private System.Action<MageProjectile> _onReturn;

    private readonly LayerMask _enemyLayer = GameLayers.EnemyMask;
    private readonly List<Collider2D> _aoeResults = new List<Collider2D>();
    private readonly List<Collider2D> _checkResults = new List<Collider2D>();

    private IDamageable _target;
    private MonoBehaviour _targetBehaviour;
    private Monster _targetMonster;

    private Vector2 _lastKnownTargetPosition;
    private int _targetAllocGeneration;
    private const float ImpactDistance = 0.03f;

    /// <summary>풀 생성 시 1회 호출.</summary>
    public void Init(System.Action<MageProjectile> onReturn)
    {
        _onReturn = onReturn;
        _animator = GetComponent<Animator>();

        var col = GetComponent<CircleCollider2D>();
        if (col != null)
        {
            col.isTrigger = true;
            _collisionRadius = col.radius;
        }
    }

    public void FireToTarget(
        Player owner,
        IDamageable target,
        float speed,
        int damage,
        float aoeRadius,
        float lifetime)
    {
        _alive = true;
        _returnTime = -1f;

        _speed = speed;
        _damage = damage;
        _aoeRadius = aoeRadius;
        _expireTime = Time.time + lifetime;
        _lifetime = lifetime;

        _owner = owner;
        _target = target;
        _targetBehaviour = target as MonoBehaviour;
        _targetMonster =
            _targetBehaviour?.GetComponentInParent<Monster>();
        
        //죽은 몬스터가 타겟이었을때, 해당 몬스터가 재사용되면 해당 몬스터에게 날아가는 버그 방지
        if (_targetMonster != null)
            _targetAllocGeneration = _targetMonster.AllocGen; 

        _lastKnownTargetPosition =
            target?.targetPos ?? transform.position;

        // 애니메이터 리셋 → 비행 상태로
        if (_animator != null)
        {
            _animator.ResetTrigger(_isHitHash);
            _animator.Play("MagicMissile", 0, 0f);
        }
        
        // 이동 방향으로 스프라이트 회전 (기본 스프라이트가 오른쪽을 향한다고 가정)
        float angle = Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
        // 속도, 피해량, 수명, 애니메이터 초기화...
    }
    /// <summary>투사체 발사.</summary>
    public void Fire(Player owner, Vector2 direction, float speed, int damage, float aoeRadius, float lifetime)
    {
        _owner = owner;
        _direction = direction.normalized;
        _speed = speed;
        _damage = damage;
        _aoeRadius = aoeRadius;
        _lifetime = lifetime;
        _expireTime = Time.time + lifetime;
        _alive = true;
        _returnTime = -1f;

        // 애니메이터 리셋 → 비행 상태로
        if (_animator != null)
        {
            _animator.ResetTrigger(_isHitHash);
            _animator.Play("MagicMissile", 0, 0f);
        }

        // 이동 방향으로 스프라이트 회전 (기본 스프라이트가 오른쪽을 향한다고 가정)
        float angle = Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void Update()
    {
        // 소멸 애니메이션 대기 후 풀 반환
        if (_returnTime > 0f)
        {
            if (Time.time >= _returnTime)
            {
                _returnTime = -1f;
                DoReturnToPool();
            }
            return;
        }
        if (!_alive) return;
        
        if (CanTrackCurrentTarget())
        {
            _lastKnownTargetPosition =
                _target.targetPos;
        }
        else
        {
            // 마지막 위치를 목적지로 확정하고 풀 재사용된 타겟을 다시 읽지 않는다.
            ClearTargetReference();
        }
        //이동
        Vector2 currentPosition = transform.position;
        
        Vector2 nextPosition = Vector2.MoveTowards(
            currentPosition,
            _lastKnownTargetPosition,
            _speed * Time.deltaTime);
        
        transform.position = nextPosition;

        Vector2 movement = nextPosition - currentPosition;
        UpdateRotation(movement);
        
        //목표탐색
        float arrivalRadius =
            Mathf.Max(_collisionRadius, 0.05f);

        Vector2 remaining =
            _lastKnownTargetPosition - nextPosition;

        if (remaining.sqrMagnitude <=
            ImpactDistance * ImpactDistance)
        {
            // 폭발 중심을 목적지에 정확히 맞춘다.
            transform.position = _lastKnownTargetPosition;

            Explode();
            return;
        }

        // 수명 초과 → 소멸
        if (Time.time >= _expireTime)
            DoReturnToPool();
        
        /* 매 프레임 OverlapCircle은 무거울것 같아서 변경
        // 충돌 판정 (매 프레임 OverlapCircle)
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(_enemyLayer);
        filter.useLayerMask = true;
        filter.useTriggers = true;

        int count = Physics2D.OverlapCircle(transform.position, _collisionRadius, filter, _checkResults);
        for (int i = 0; i < count; i++)
        {
            var mon = _checkResults[i].GetComponentInParent<Monster>();
            if (mon != null && mon.MonAction != eMonsterAction.Dead)
            {
                Explode();
                return;
            }
        }*/

    }
    
    private bool CanTrackCurrentTarget()
    {
        if (_targetBehaviour == null)
            return false;

        if (!_targetBehaviour.gameObject.activeInHierarchy)
            return false;

        if (_targetMonster != null)
        {
            //목표 몬스터의 AllocGen과 저장된 AllocGen이 다르면 타겟을 추적하지 않음
            if (_targetMonster.AllocGen !=
                _targetAllocGeneration)
            {
                return false;
            }

            if (_targetMonster.MonAction ==
                eMonsterAction.Dead)
            {
                return false;
            }
        }

        return true;
    }
    private void UpdateRotation(Vector2 movement)
    {
        if (movement.sqrMagnitude < 0.0001f)
            return;

        float angle =
            Mathf.Atan2(movement.y, movement.x)
            * Mathf.Rad2Deg;

        transform.rotation =
            Quaternion.Euler(0f, 0f, angle);
    }
    private void ClearTargetReference()
    {
        _target = null;
        _targetBehaviour = null;
        _targetMonster = null;
        _targetAllocGeneration = 0;
    }
    private void Explode()
    {
        _alive = false;

        // AoE 피해
        var proxy = new ActiveSkill.DamageProxy((ulong)_damage, _owner);

        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(_enemyLayer);
        filter.useLayerMask = true;
        filter.useTriggers = true;

        int count = Physics2D.OverlapCircle(transform.position, _aoeRadius, filter, _aoeResults);
        for (int i = 0; i < count; i++)
        {
            var m = _aoeResults[i].GetComponentInParent<Monster>();
            if (m == null || m.MonAction == eMonsterAction.Dead) continue;

            var d = _aoeResults[i].GetComponentInParent<IDamageable>();
            d?.TakeDamage(proxy);
        }

        // 소멸 애니메이션 재생 → 끝나면 풀 반환
        if (_animator != null)
        {
            _animator.SetTrigger(_isHitHash);
            float dissipateLen = GetDissipateClipLength();
            _returnTime = Time.time + dissipateLen;
        }
        else
        {
            DoReturnToPool();
        }
    }

    private float GetDissipateClipLength()
    {
        if (_animator == null || _animator.runtimeAnimatorController == null)
            return 0.3f;

        foreach (var clip in _animator.runtimeAnimatorController.animationClips)
        {
            if (string.Equals(clip.name, "MagicMissile_Dissipate", System.StringComparison.OrdinalIgnoreCase))
                return clip.length;
        }
        return 0.3f;
    }

    private void DoReturnToPool()
    {
        _alive = false;
        _returnTime = -1f;
        _onReturn?.Invoke(this);
    }
}
