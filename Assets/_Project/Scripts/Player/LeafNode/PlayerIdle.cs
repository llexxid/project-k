using Scripts.Core;
using System.Collections.Generic;
using UnityEngine;

public class PlayerIdle
{
    private readonly Player _player;
    private readonly Vector2 _spawnPos;
    private const float returnThreshold = 1f;
    private const float returnSpeed = 3f;

    public PlayerIdle(Player player) 
    { 
        _player = player; 
        _spawnPos = player.transform.position;
    }

    public NodeState Idle()
{
    if (_player == null) return NodeState.Success;

    // 전투 중(타겟이 있음)엔 복귀 이동 안 함
    if (_player.currentTarget != null)
    {
        _player.SetAnimation(ePlayerAction.Idle);
        return NodeState.Success;
    }

    float dist = Vector2.Distance(_player.transform.position, _spawnPos);

    if (dist > returnThreshold)
    {
        // 스폰 위치로 이동
        _player.SetAnimation(ePlayerAction.Walk);

        // 좌우 플립
        Vector3 scale = _player.transform.localScale;

        // 현재 플레이어의 x좌표와 스폰 위치의 x좌표 차이 계산
        float dirX = _spawnPos.x - _player.transform.position.x;

        // x축 방향에 따라 scale.x를 조정하여 좌우 반전
        scale.x = Mathf.Abs(scale.x) * Mathf.Sign(dirX);

        // scale.x에 계산된 값을 적용하여 플레이어의 방향을 스폰 위치로 향하게 함
        _player.transform.localScale = scale;

        // 플레이어를 스폰 위치로 이동
        _player.transform.position = Vector2.MoveTowards(_player.transform.position, _spawnPos, returnSpeed * Time.deltaTime);
    }
    else
    {
        _player.SetAnimation(ePlayerAction.Idle);
    }
    return NodeState.Success;
}

    public class IdleNode : Node
    {
        private PlayerIdle _idle;
        public IdleNode(PlayerIdle idle) { _idle = idle; }
        public override NodeState Evaluate() => _idle.Idle();
    }
}
