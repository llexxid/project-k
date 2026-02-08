using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerIdle : MonoBehaviour
{
    public Animator animator;

    // 대기 행동 로직
    public NodeState Idle()
    {
        // 여기에 대기 애니메이션을 재생하거나, 체력을 회복하는 등의 로직 추가
        animator.SetBool("isMoving", false);

        // 대기는 항상 성공(수행 가능)한 상태로 간주
        return NodeState.Success;
    }

    // [추가] 행동 트리 전용 노드 클래스
    public class IdleNode : Node
    {
        private PlayerIdle _idle;
        public IdleNode(PlayerIdle idle) { _idle = idle; }

        public override NodeState Evaluate() => _idle.Idle();
    }
}