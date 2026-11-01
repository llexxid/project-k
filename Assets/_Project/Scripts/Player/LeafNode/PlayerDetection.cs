using KingdomIdle.Combat;
using Scripts.Core;
using Scripts.Monster;
using UnityEngine;
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
            _acquired.MonAction != eMonsterAction.Dead && _acquired.AllocGen == _generation)
        {
            var reachable = FindCloserMeleeTarget();
            if (reachable != null)
            {
                _acquired = reachable;
                _generation = reachable.AllocGen;
                _player.SetTarget(reachable);
            }
            return true;
        }
        _player.ResetTarget(_player.currentTarget);
        _acquired = CombatMotion.NearestEnemy(_player.VfxFootPosition);
        if (_acquired == null) return false;
        _generation = _acquired.AllocGen;
        _player.SetTarget(_acquired);
        return true;
    }

    private Monster FindCloserMeleeTarget()
    {
        if (_player.IsInAttackAnimation || _player.skillSystem.IsRanged) return null;
        Vector2 origin = _player.VfxFootPosition, delta = (Vector2)_acquired.FootPosition - origin;
        float facing = Mathf.Abs(delta.x) < .001f ? Mathf.Sign(_player.transform.localScale.x) : Mathf.Sign(delta.x);
        float range = _player.skillSystem.AttackRange;
        if (CombatMotion.InFront(origin, _acquired.FootPosition, facing, range)) return null;

        // A reachable enemy in front must not be ignored while walking into its body.
        // Include enemies too close to hit: PlayerMove already restores melee spacing.
        Monster best = null;
        float distance = delta.sqrMagnitude;
        foreach (var candidate in CombatMotion.Monsters)
        {
            if (candidate == null || candidate == _acquired || !candidate.isActiveAndEnabled ||
                candidate.MonAction == eMonsterAction.Dead) continue;
            Vector2 offset = (Vector2)candidate.FootPosition - origin;
            if (offset.x * facing <= 0 || Mathf.Abs(offset.x) > range ||
                Mathf.Abs(offset.y) > CombatMotion.MeleeLane || offset.sqrMagnitude >= distance) continue;
            best = candidate;
            distance = offset.sqrMagnitude;
        }
        return best;
    }
    public class DetectionNode : Node
    {
        private readonly PlayerDetection _detection;
        public DetectionNode(PlayerDetection detection) { _detection = detection; }
        public override NodeState Evaluate() => _detection.Detect() ? NodeState.Success : NodeState.Failure;
    }
}
