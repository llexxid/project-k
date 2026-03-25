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
    private float _nextAttackTime = 0f;
    public float attackRadius = 2f;  // 모바일 기준 공격 판정 반경
    // Physics2D.OverlapCircle 결과를 재사용하기 위한 버퍼 (GC 최소화)
    private List<Collider2D> _hitResults = new List<Collider2D>();

    private Player _player;

    // 적 레이어 마스크 (GameLayers 상수 사용, 하드코딩 제거)
    LayerMask enemyLayer = GameLayers.EnemyMask;

    /// <summary>
    /// 생성자: Player로부터 필요한 참조를 가져온다.
    /// </summary>
    public PlayerAttack(Player player, PlayerDetection detection = null)
    {
        _player     = player;
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
        // [1] 일반 공격 쿨타임 체크
        if (Time.time < _nextAttackTime)
        {
            //Debug.Log($"[PlayerAttack] 쿨타임 대기 중 ({(_nextAttackTime - Time.time):F2}초 남음)");
            return NodeState.Failure;
        }

        //Target과 내 공격거리
        Vector3 targetPos = _player.currentTarget.targetPos;
        Vector3 myPos = _player.targetPos;
        //공격 범위에 있지 않음.
        float distance = Vector3.Distance(myPos, targetPos);

		if (distance > attackRadius)
		{
			return NodeState.Failure;
		}

        //공격범위에 있다면
		_nextAttackTime = Time.time + attackRate;
		_player.PlayAttackAnimation();
        ApplyDamage(_player.currentTarget, (ulong)_player.playerStatus.Atk);
		//Debug.Log($"[PlayerAttack] 공격 실행! 다음 공격: {attackRate:F2}초 후");

        return NodeState.Success;
    }


    /// <summary>
    /// Animation Event(OnAttackHit)에서 호출.
    /// 현재 범위 내 첫 번째 살아있는 적에게 즉시 데미지를 적용한다.
    /// </summary>
    public void DealDamage()
    {

        /*
        int baseAtk = player.playerStatus?.Atk ?? 0;
        Debug.Log($"[DealDamage] baseAtk={baseAtk}");

        for (int i = 0; i < hitCount; i++)
        {
            bool hasDamageable = _hitResults[i].TryGetComponent<IDamageable>(out var target);
            Debug.Log($"[DealDamage] [{i}] {_hitResults[i].name} | IDamageable={hasDamageable} | Dead={IsTargetDead(_hitResults[i])}");

            if (!hasDamageable) continue;
            if (IsTargetDead(_hitResults[i])) continue;
            bool isAlive = ApplyDamage(target, (ulong)baseAtk);
            if (!isAlive) break;
            break; // 단일 타깃
        }*/
    }

    /// <summary>
    /// 지정한 대상에게 계산된 데미지를 적용하고 사망 여부를 처리한다.
    /// </summary>
    private bool ApplyDamage(IDamageable target, ulong damage)
    {
        //다른애가 죽였다? 아니 탐지부터 다시하기 때문에, 그럴일 없음.
        if (target == null)
        {
			return false;
		} 

        bool isAlive;

        isAlive = target.TakeDamage(_player);
        if (!isAlive)
        {
			//Debug.Log("Monster Is Dead!! → Idle 전환");
			//_nextAttackTime = 0f; // 다음 적 즉시 공격 가능하도록 쿨타임 초기화
			_player.SetAnimation(ePlayerAction.Idle);
        }
        return isAlive;
    }


    /// <summary>
    /// ulong 데미지를 IAttackable 인터페이스로 포장하는 내부 래퍼 클래스.
    /// </summary>
/*    private class DamageProxy : IAttackable
    {
        public ulong damage { get; private set; }
        public Vector3 attackerPos => Vector3.zero;
        public bool Attack(IDamageable target) => false;
        public DamageProxy(ulong damage) { this.damage = damage; }
    }
*/
    /// <summary>
    /// 행동 트리에서 PlayerAttack.Attack()을 노드로 감싸는 래퍼 클래스.
    /// </summary>
    public class AttackNode : Node
    {
        public PlayerAttack _attack;
        public AttackNode(PlayerAttack attack) { _attack = attack; }
        public override NodeState Evaluate() => _attack.Attack();
    }
}
