using Scripts.Core;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerIdle
{
    private readonly Player _player;

    public PlayerIdle(Player player) { _player = player; }

    /// <summary>
    /// 대기 상태 진입: _playerAction을 Idle로 되돌려 공격 애니메이션을 멈춘다.
    /// 적이 없을 때 BT가 이 노드를 호출하므로, 여기서 리셋하지 않으면
    /// Attack 상태가 영원히 유지되어 공격 애니메이션이 계속 루프된다.
    /// </summary>
    public NodeState Idle()
    {
        if (_player != null)
            _player._playerAction = ePlayerAction.Idle;
        return NodeState.Success;
    }

    public class IdleNode : Node
    {
        private PlayerIdle _idle;
        public IdleNode(PlayerIdle idle) { _idle = idle; }
        public override NodeState Evaluate() => _idle.Idle();
    }
}