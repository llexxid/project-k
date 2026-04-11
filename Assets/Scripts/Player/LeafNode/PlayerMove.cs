using Scripts.Core;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMove
{
    public float moveSpeed = 5f;
    public float stopDistance = 1.5f;
    public Player player;

    public PlayerMove(Player player)
    {
        this.player = player;
    }

    public NodeState Move()
    {
        // 1. 타겟이 없으면 실패
        if (player.currentTarget == null)
        {
            player.SetAnimation(ePlayerAction.Idle);
            return NodeState.Failure;
        }

        // 이동 방향 계산 → 스프라이트 좌우 플립
        Vector2 direction = (Vector2)player.currentTarget.targetPos - (Vector2)player.transform.position;
        if (direction.x != 0f)
        {
            Vector3 scale = player.transform.localScale;
            scale.x = direction.x > 0f ? Mathf.Abs(scale.x) : -Mathf.Abs(scale.x);
            player.transform.localScale = scale;
        }

        // 2. 거리 계산
        float distance = Vector2.Distance(player.transform.position, player.currentTarget.targetPos);

        if (distance <= stopDistance)
        {
            //player.SetAnimation(ePlayerAction.Idle);
            return NodeState.Success;
        }

        // 4. 이동 중
        player.SetAnimation(ePlayerAction.Walk);
        player.transform.position = Vector2.MoveTowards(
        player.transform.position, player.currentTarget.targetPos, moveSpeed * Time.deltaTime);
        return NodeState.Running;
    }

    public class MoveNode : Node
    {
        private PlayerMove _move;
        public MoveNode(PlayerMove move) { _move = move; }
        public override NodeState Evaluate() => _move.Move();
    }
}
