using Scripts.Core;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMove
{
    // public PlayerDetection detection; // Inspector에서 PlayerDetection 연결 필요
    public float moveSpeed = 5f;
    public float stopDistance = 1.5f; // 모바일 기준 정지 거리 (attackRadius보다 작게)
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

        // 3. 공격 사거리 내에 도착 → Attack 노드에게 제어권 넘김
        //    ⚠ 여기서 _playerAction을 Idle로 설정하면 Attack 애니메이션이 즉시 취소됨
        Debug.Log($"[PlayerMove] 거리: {distance:F2} / 정지거리: {stopDistance}");
        if (distance <= stopDistance)
        {
            // 공격 중이 아닐 때만 Idle로 전환 (Attack 애니메이션 보호)
            if (player._playerAction != ePlayerAction.Attack)
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