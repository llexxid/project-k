using System.Collections.Generic;
using UnityEngine;

public class PlayerOrder

{
    public Node _rootNode;

    private PlayerDetection _detection;
    private PlayerMove _move;
    private PlayerAttack _attack;

    public void Init(Player player)
    {

        _detection = new PlayerDetection(player);
        _move = new PlayerMove(player);
        _attack = new PlayerAttack(player, _detection);

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
            
            // 2. 대기 (전투 실패 시 실행)
            new IdleNode()
        });
    }

    // 간단한 대기 노드
    public class IdleNode : Node
    {
        public override NodeState Evaluate()
        {
            return NodeState.Success;
        }
    }
}