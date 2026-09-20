namespace Scripts.Monster.MonsterNode
{
    public class MonsterDetect : MonsterNode
    {
        public MonsterDetect(Monster monster) : base(monster) { }
        public override NodeState Evaluate()
        {
            _monster.AcquireTarget();
            return NodeState.Failure;
        }
    }
}
