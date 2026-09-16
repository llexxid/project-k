using KingdomIdle.Combat;
using Scripts.Core;
using Scripts.Monster;
public class PlayerDetection
{
    public float detectionRadius = 8f;
    private readonly Player _player;
    private Monster _acquired;
    private int _generation;
    public PlayerDetection(Player player) { _player = player; }
    public bool Detect()
    {
        if (ReferenceEquals(_player.currentTarget, _acquired) && _acquired != null && _acquired.isActiveAndEnabled &&
            _acquired.MonAction != eMonsterAction.Dead && _acquired.AllocGen == _generation) return true;
        _player.ResetTarget(_player.currentTarget);
        _acquired = CombatMotion.NearestEnemy(_player.transform.position);
        if (_acquired == null) return false;
        _generation = _acquired.AllocGen;
        _player.SetTarget(_acquired);
        return true;
    }
    public class DetectionNode : Node
    {
        private readonly PlayerDetection _detection;
        public DetectionNode(PlayerDetection detection) { _detection = detection; }
        public override NodeState Evaluate() => _detection.Detect() ? NodeState.Success : NodeState.Failure;
    }
}
