namespace Scripts.Monster.MonsterNode
{
    public class MonsterAttack : MonsterNode
    {
        public MonsterAttack(Monster monster) : base(monster) { }
        public override NodeState Evaluate()
        {
            if (_monster.IsAttackLocked) return NodeState.Running;
            if (!_monster.CanReachTarget()) return NodeState.Failure;
            _monster.TryBeginAttack();
            return NodeState.Success;
        }
    }
}
