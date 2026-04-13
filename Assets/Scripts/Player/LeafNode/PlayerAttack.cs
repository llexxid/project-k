using Scripts.Core;
using Scripts.Core.inteface;
using Scripts.Core.Utils;
using Scripts.Monster;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어의 일반 공격 로직을 담당하는 클래스.
/// 스킬 공격은 PlayerSkill LeafNode가 별도로 처리한다.
/// </summary>
public class PlayerAttack
{
    private PlayerDetection _detection;

    // 공격 애니메이션 한 사이클 길이 (초). attackRate의 최솟값 기준으로도 사용
    private const float ANIMATION_DURATION = 0.4f;

    // 일반 공격 간격 (초). 이 값이 작을수록 공격 속도가 빠름
    public float attackRate = ANIMATION_DURATION;
    // 다음 공격이 가능한 시각 (Time.time 기준). 현재 시간이 이 값 이상일 때만 공격 가능
    private float _nextAttackTime = 0f;

    // 공격이 닿는 범위 (반지름, 단위: 유니티 단위)
    public float attackRadius = 2f;  // 모바일 기준 공격 판정 반경
    // Physics2D.OverlapCircle 결과를 재사용하기 위한 버퍼 (GC 최소화)
    private List<Collider2D> _hitResults = new List<Collider2D>();

    public Player player;

    // 적 레이어 마스크 (GameLayers 상수 사용, 하드코딩 제거)
    LayerMask enemyLayer = GameLayers.EnemyMask;

    /// <summary>
    /// 생성자: Player로부터 필요한 참조를 가져온다.
    /// </summary>
    public PlayerAttack(Player player, PlayerDetection detection = null)
    {
        this.player     = player;
        this._detection = detection;

        // attackRate가 0 이하면 애니메이션 길이로 자동 보정
        if (attackRate <= 0f)
        {
            attackRate = ANIMATION_DURATION;
        }
    }

    /// <summary>
    /// 행동 트리에서 매 프레임 호출되는 일반 공격 판정 함수.
    /// 쿨타임이 끝났고 범위 내 적이 있을 때 첫 번째 적에게 단일 데미지를 적용한다.
    /// 스킬이 이미 Success를 반환했으면 이 노드는 호출되지 않는다(Selector 구조).
    /// </summary>
    public NodeState Attack()
    {
        // [1] 패링 중에는 공격 애니메이션이 패링을 덮어쓰지 않도록 차단
        if (player.IsParrying) return NodeState.Failure;

        // [2] 일반 공격 쿨타임 체크
        if (Time.time < _nextAttackTime)
        {
            // 쿨타임 중이라도 공격 시퀀스 자체는 성공으로 유지 (스폰 위치 복귀 방지)
            return NodeState.Success;
        }

        // [3] 현재 타겟이 이미 있고 공격 범위 내에 있는지 우선 확인 (공격 반응 속도 향상)
        if (player.currentTarget != null && !player.currentTarget.gameobj.GetComponent<Monster>().MonAction.Equals(eMonsterAction.Dead))
        {
            float distToTarget = Vector2.Distance(player.transform.position, player.currentTarget.targetPos);
            if (distToTarget <= attackRadius)
            {
                // 즉시 공격 시작
                _nextAttackTime = Time.time + attackRate;
                player.PlayAttackAnimation();
                return NodeState.Success;
            }
        }

        // [4] 적 탐지 필터 설정 (기본 로직 유지)
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(enemyLayer);
        filter.useLayerMask = true;
        filter.useTriggers = true;

        // [4] 범위 내 적 탐지 — 적이 없으면 공격하지 않음
        int hitCount = Physics2D.OverlapCircle(
            player.transform.position, attackRadius, filter, _hitResults);

        if (hitCount == 0) return NodeState.Failure;

        // [4-a] 탐지된 적이 전부 Dead 상태면 공격하지 않음
        bool hasAliveTarget = false;
        for (int i = 0; i < hitCount; i++)
        {
            if (!IsTargetDead(_hitResults[i])) { hasAliveTarget = true; break; }
        }
        if (!hasAliveTarget)
        {
			CustomLogger.Log("Player가 공격범위 내에 대상이 없습니다..");
			return NodeState.Failure;
		}

        // [5] 쿨타임 소모
        _nextAttackTime = Time.time + attackRate;

        // [6] 공격 애니메이션 재생
        player.PlayAttackAnimation();

        return NodeState.Success;
    }


    /// <summary>
    /// Animation Event(OnAttackHit)에서 호출.
    /// 현재 범위 내 첫 번째 살아있는 적에게 즉시 데미지를 적용한다.
    /// </summary>
    public void DealDamage()
    {
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(enemyLayer);
        filter.useLayerMask = true;
        filter.useTriggers  = true;

        int hitCount = Physics2D.OverlapCircle(
            player.transform.position, attackRadius, filter, _hitResults);

        if (hitCount == 0) return;

        int baseAtk = player.playerStatus?.Atk ?? 0;

        for (int i = 0; i < hitCount; i++)
        {
            bool hasDamageable = _hitResults[i].TryGetComponent<IDamageable>(out var target);

            if (!hasDamageable) continue;
            if (IsTargetDead(_hitResults[i])) continue;
            ApplyDamage(target, (ulong)baseAtk);
        }
    }

    /// <summary>
    /// 지정한 대상에게 계산된 데미지를 적용하고 사망 여부를 처리한다.
    /// </summary>
    private bool ApplyDamage(IDamageable target, ulong damage)
    {
        if (target == null) return false;

        bool isAlive;
        isAlive = target.TakeDamage(new PlayerSkill.DamageProxy(damage, player.gameObject, player));

        if (!isAlive)
        {
            _nextAttackTime = 0f; // 다음 적 즉시 공격 가능하도록 쿨타임 초기화
            if (player != null) player.SetAnimation(ePlayerAction.Idle);
            if (player != null) player.currentTarget = null;
            if (_detection != null) _detection.currentTarget = null;
        }
        return isAlive;
    }

    private bool IsTargetDead(Collider2D col)
    {
        // 콜라이더가 자식 오브젝트에 있어도 부모로 올라가 Monster를 찾음
        var mon = col.GetComponentInParent<Monster>();
        return mon != null && mon.MonAction == eMonsterAction.Dead;
    }

    /// <summary>
    /// 행동 트리에서 PlayerAttack.Attack()을 노드로 감싸는 래퍼 클래스.
    /// </summary>
    public class AttackNode : Node
    {
        private PlayerAttack _attack;
        public AttackNode(PlayerAttack attack) { _attack = attack; }
        public override NodeState Evaluate() => _attack.Attack();
    }
}
