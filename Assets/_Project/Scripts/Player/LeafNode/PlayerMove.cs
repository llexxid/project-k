using KingdomIdle.Combat;
using Scripts.Core;
using UnityEngine;

public class PlayerMove
{
    public float moveSpeed = 2f, stopDistance = 1f;
    public Player player;
    private int _seenAttack;
    private float _retreatUntil, _retreatStarted;
    public bool IsRetreating => Time.time < _retreatUntil;
    public PlayerMove(Player owner) { player = owner; _seenAttack = owner.BasicAttackSerial; }
    public NodeState Move()
    {
        if (player.currentTarget == null) { EndRetreat(); return NodeState.Failure; }
        if (player.IsInAttackAnimation) return NodeState.Running;
        Vector2 current = player.VfxFootPosition, target = player.currentTarget.targetPos;
        Vector2 delta = target - current;
        bool ranged = player.skillSystem.IsRanged;
        if (ranged)
        {
            var threat = CombatMotion.NearestEnemy(current, 1.2f);
            if (_seenAttack != player.BasicAttackSerial)
            {
                _seenAttack = player.BasicAttackSerial;
                if (threat != null) {
                    EndRetreat(); _retreatStarted=Time.time;_retreatUntil=Time.time+.7f;
                    CombatDiagnostics.Record("retreat-start",player,threat);
                }
            }
            if (IsRetreating && threat != null)
            {
                Vector2 away = current - (Vector2)threat.FootPosition;
                if (away.sqrMagnitude < .001f) away = player.PlayerIndex == 1 ? Vector2.left : Vector2.right;
                Vector2 destination = CombatMotion.Clamp(current + away.normalized * .85f);
                if ((destination - current).sqrMagnitude > .005f)
                {
                    Walk(destination, target.x - current.x);
                    return NodeState.Running;
                }
            }
            // A retreat starts only after a fresh attack, never just because an enemy is close.
            EndRetreat();
        }
        Face(delta.x);
        bool inRange = ranged ? delta.sqrMagnitude <= stopDistance * stopDistance :
            CombatMotion.InFront(current, target, Mathf.Sign(player.transform.localScale.x), stopDistance);
        if (inRange) return NodeState.Success;
        Walk(ranged ? target : CombatMotion.Approach(current, target, stopDistance, player.PlayerIndex), delta.x);
        return NodeState.Running;
    }
    private void EndRetreat()
    {
        if (_retreatUntil>0) CombatDiagnostics.Record("retreat-end",player,null,Time.time-_retreatStarted);
        _retreatUntil=0;
    }
    private void Face(float direction)
    {
        if (Mathf.Abs(direction) < .001f) return;
        Vector3 scale = player.transform.localScale;
        scale.x = Mathf.Abs(scale.x) * Mathf.Sign(direction);
        player.transform.localScale = scale;
    }
    private void Walk(Vector2 destination, float facing)
    {
        Face(facing); player.SetAnimation(ePlayerAction.Walk);
        CombatMotion.MoveTowards(player, destination, moveSpeed * Time.deltaTime, CombatMotion.PlayerRadius);
    }
    public class MoveNode : Node
    {
        private readonly PlayerMove _move;
        public MoveNode(PlayerMove move) { _move = move; }
        public override NodeState Evaluate() => _move.Move();
    }
}
