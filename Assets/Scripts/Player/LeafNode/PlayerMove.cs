using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMove
{
    // public PlayerDetection detection; // Inspector에서 PlayerDetection 연결 필요
    public float moveSpeed = 5f;
    public float stopDistance = 1.0f; // 적 앞에서 멈출 거리 (공격 사거리)
    public Player player; 

    public PlayerMove(Player player)
    {
        this.player = player;
    }

    // 행동 트리에서 호출할 함수 (반환값 NodeState로 변경)
    public NodeState Move()
    {
        //Debug.Log("Player Moving...");

        // 1. 타겟이 없으면 실패 (적이 사라짐)
        if (player.currentTarget == null)
        {
            Debug.Log("타겟 없음");
            return NodeState.Failure;
        }
        else
        {
            // 이동 방향 계산 → 스프라이트 좌우 플립
            Vector2 direction = (Vector2)player.currentTarget.targetPos - (Vector2)player.transform.position;
            if (direction.x != 0f)
            {
                Vector3 scale = player.transform.localScale;
                scale.x = direction.x > 0f ? Mathf.Abs(scale.x) : -Mathf.Abs(scale.x);
                player.transform.localScale = scale;
            }
        }

        // 2. 거리 계산
        float distance = Vector2.Distance(player.transform.position, player.currentTarget.targetPos);

        // 3. 공격 사거리 내에 도착했으면 Success 반환 -> 다음 Attack 노드 실행됨
        if (distance <= stopDistance)
        {
            return NodeState.Success;
        }

        // 4. 아직 이동 중이면 Running 반환 (계속 이동)
        player.transform.position = Vector2.MoveTowards(player.transform.position, player.currentTarget.targetPos, moveSpeed * Time.deltaTime);
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