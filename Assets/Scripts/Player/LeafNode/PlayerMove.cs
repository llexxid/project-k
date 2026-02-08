using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMove : MonoBehaviour
{
    public PlayerDetection detection; // Inspector에서 PlayerDetection 연결 필요
    public float moveSpeed = 5f;
    public float stopDistance = 1.0f; // 적 앞에서 멈출 거리 (공격 사거리)
    public Animator animator;

    // 행동 트리에서 호출할 함수 (반환값 NodeState로 변경)
    public NodeState Move()
    {
        // 1. 타겟이 없으면 실패 (적이 사라짐)
        if (detection.currentTarget == null)
            return NodeState.Failure;
        else if (detection.currentTarget != null)
        {
            Vector2 direction = (Vector2)detection.currentTarget.position - (Vector2)transform.position;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            animator.SetBool("isMove", true);
        }

        // 2. 거리 계산
        float distance = Vector2.Distance(transform.position, detection.currentTarget.position);

        // 3. 공격 사거리 내에 도착했으면 Success 반환 -> 다음 Attack 노드 실행됨
        if (distance <= stopDistance)
        {
            animator.SetBool("isMove", false);
            return NodeState.Success;
        }

        // 4. 아직 이동 중이면 Running 반환 (계속 이동)
        transform.position = Vector2.MoveTowards(transform.position, detection.currentTarget.position, moveSpeed * Time.deltaTime);
        return NodeState.Running;
    }

    // 행동 트리 전용 노드 클래스
    public class MoveNode : Node
    {
        private PlayerMove _move;
        public MoveNode(PlayerMove move) { _move = move; }

        public override NodeState Evaluate() => _move.Move();
    }
}