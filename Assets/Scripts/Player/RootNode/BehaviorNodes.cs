using System.Collections.Generic;

// 상태 정의
public enum NodeState { Running, Success, Failure }

// 기본 노드 (추상 클래스)
public abstract class Node
{
    public abstract NodeState Evaluate();
}

// [Sequence] 모든 자식이 성공해야 성공 (AND)
public class Sequence : Node
{
    private List<Node> nodes = new List<Node>();
    public Sequence(List<Node> nodes) { this.nodes = nodes; }

    public override NodeState Evaluate()
    {
        foreach (var node in nodes)
        {
            switch (node.Evaluate())
            {
                case NodeState.Running: return NodeState.Running;
                case NodeState.Failure: return NodeState.Failure;
                case NodeState.Success: continue;
            }
        }
        return NodeState.Success;
    }
}

// [Selector] 자식 중 하나라도 성공하면 성공 (OR) - (구 PlayerSelector)
public class Selector : Node
{
    private List<Node> nodes = new List<Node>();
    public Selector(List<Node> nodes) { this.nodes = nodes; }

    public override NodeState Evaluate()
    {
        foreach (var node in nodes)
        {
            switch (node.Evaluate())
            {
                case NodeState.Running: return NodeState.Running;
                case NodeState.Success: return NodeState.Success;
                case NodeState.Failure: continue;
            }
        }
        return NodeState.Failure;
    }
}