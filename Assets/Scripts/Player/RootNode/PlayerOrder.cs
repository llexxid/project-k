using System.Collections.Generic;
using UnityEngine;

public class PlayerOrder : MonoBehaviour
{
    private Node _rootNode;

    [SerializeField] private PlayerDetection _detection;
    [SerializeField] private PlayerMove _move;
    [SerializeField] private PlayerAttack _attack;

    void Start()
    {
        // 트리 조립: Selector(전투 OR 대기)
        _rootNode = new Selector(new List<Node>
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

    void Update()
    {
        _rootNode?.Evaluate();
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