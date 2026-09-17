namespace Scripts.Monster.MonsterNode
{
    public class MonsterMove : MonsterNode
    {
        public MonsterMove(Monster monster) : base(monster) { }
        public override NodeState Evaluate()
        {
            _monster.ApproachTarget();
            return NodeState.Running;
        }
    }
}
