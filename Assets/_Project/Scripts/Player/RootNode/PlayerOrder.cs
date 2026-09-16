using System.Collections.Generic;
using UnityEngine;

public class PlayerOrder
{
    public Node _rootNode;

    private PlayerDetection _detection;
    public PlayerMove _move;
    private PlayerIdle _idle;

    public void Init(Player player)
    {
        _detection = new PlayerDetection(player);
        _move = new PlayerMove(player);
        _idle = new PlayerIdle(player);

        var skillNode = new SkillSystem.SkillSystemNode(player.skillSystem);

        _rootNode = new Selector(new List<Node>
        {
            new Sequence(new List<Node>
            {
                new PlayerDetection.DetectionNode(_detection),
                new PlayerMove.MoveNode(_move),
                skillNode
            }),
            new PlayerIdle.IdleNode(_idle)
        });

        ApplyRanges(player.skillSystem);
        SyncMoveSpeed(player.playerStatus);
    }

    /// <summary>스킬 사거리에 따라 탐지/정지 거리 갱신.</summary>
    public void ApplyRanges(SkillSystem system)
    {
        if (system == null) return;
        float range = system.AttackRange;
        if (range > 0f && _move != null)
            _move.stopDistance = range;
        if (range > 0f && _detection != null)
            _detection.detectionRadius = range + 1.5f;
    }

    /// <summary>PlayerStatus.MovSpeed → PlayerMove.moveSpeed 동기화.</summary>
    public void SyncMoveSpeed(PlayerStatus status)
    {
        if (status == null || _move == null) return;
        _move.moveSpeed = status.MovSpeed;
    }

    private bool _isAbort;
    public bool IsAbort => _isAbort;

    public void InterruptBT()
    {
        _isAbort = true;
    }

    public void RecoveryBT()
    {
        _isAbort = false;
    }

    public void ExecuteNode()
    {
        if (_isAbort) return;
        _rootNode.Evaluate();
    }
}
