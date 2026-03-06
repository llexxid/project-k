using Scripts.Core;
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
        // 1. 타겟이 없으면 실패 (적이 사라짐)
        if (player.currentTarget == null)
        {
            // 이동 중단 → Walk 애니메이션 끄기
            player._playerAction = ePlayerAction.Idle;
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

        // 3. 공격 사거리 내에 도착했으면 Walk 끄고 Success → 다음 Attack 노드 실행
        if (distance <= stopDistance)
        {
            player._playerAction = ePlayerAction.Idle;
            return NodeState.Success;
        }

        // 4. 아직 이동 중 → Walk 애니메이션 켜기
        player._playerAction = ePlayerAction.Walk;
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