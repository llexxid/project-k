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

        // 이동
        transform.Translate(_direction * _speed * Time.deltaTime, Space.World);

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
        }

        // 수명 초과 → 소멸
        if (Time.time >= _expireTime)
            DoReturnToPool();
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
