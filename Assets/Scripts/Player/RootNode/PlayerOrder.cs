using System.Collections.Generic;
using UnityEngine;

public class PlayerOrder

{
    public Node _rootNode;

    private PlayerDetection _detection;
    private PlayerMove _move;
    public PlayerAttack _attack;

    public void Init(Player player)
    {

        _detection = new PlayerDetection(player);
        _move = new PlayerMove(player);
        _attack = new PlayerAttack(player, _detection);
        var _idle = new PlayerIdle();

        // 트리 조립: Selector(전투 OR 대기)
        _rootNode = new Selector(new List<Node> // Selector노드 사용
        {
            // 1. 전투 시퀀스 (감지 -> 이동 -> 공격)
            new Sequence(new List<Node>
            {
                new PlayerDetection.DetectionNode(_detection),
                new PlayerMove.MoveNode(_move),
                new PlayerAttack.AttackNode(_attack)
            }),
            
            // 2. 대기 (전투 실패 시 실행) — PlayerIdle.IdleNode 사용
            new PlayerIdle.IdleNode(_idle)
        });
    }
}