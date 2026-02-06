using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerAttack : MonoBehaviour
{
    [SerializeField] private PlayerDetection _detection; // 타겟 정보를 가져오기 위해 연결
    public float attackRate = 1.0f; // 공격 속도 (초 단위)
    private float _nextAttackTime = 0f;

    public NodeState Attack()
    {
        // 1. 타겟이 사라졌으면 실패
        if (_detection.currentTarget == null)
            return NodeState.Failure;

        // 2. 쿨타임 체크 (아직 공격할 때가 아니면 실패 처리하여 다시 Move로 넘김)
        if (Time.time < _nextAttackTime)
            return NodeState.Failure;

        // 3. 공격 실행
        Debug.Log($"공격! -> {_detection.currentTarget.name}");
        _nextAttackTime = Time.time + attackRate;

        // 4. 공격 성공 반환
        return NodeState.Success;
    }

    // [추가] 행동 트리 전용 노드 클래스
    public class AttackNode : Node
    {
        private PlayerAttack _attack;
        public AttackNode(PlayerAttack attack) { _attack = attack; }

        public override NodeState Evaluate() => _attack.Attack();
    }
}