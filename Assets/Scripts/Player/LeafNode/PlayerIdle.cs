using Scripts.Core;
using System.Collections.Generic;
using UnityEngine;

public class PlayerIdle
{
    private readonly Player _player;

    public PlayerIdle(Player player) { _player = player; }

    public NodeState Idle()
    {
        if (_player != null)
            _player.SetAnimation(ePlayerAction.Idle);
        return NodeState.Success;
    }

    public class IdleNode : Node
    {
        private PlayerIdle _idle;
        public IdleNode(PlayerIdle idle) { _idle = idle; }
        public override NodeState Evaluate() => _idle.Idle();
    }
}
