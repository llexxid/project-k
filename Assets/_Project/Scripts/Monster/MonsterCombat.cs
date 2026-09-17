using KingdomIdle.Combat;
using Scripts.Core;
using Scripts.Monster.State;
using UnityEngine;

namespace Scripts.Monster
{
    public partial class Monster
    {
        [Header("Combat motion")]
        [SerializeField, Range(.05f, .95f)] private float _impactNormalized = .5f;
        [SerializeField] private GameObject _projectilePrefab;
        [SerializeField, Min(1)] private float _projectileSpeed = 4.5f;
        [SerializeField, Min(0)] private float _projectileArc = .12f;
        [SerializeField] private float _projectileAngle;
        [SerializeField] private CombatSoundCue _attackSound=CombatSoundCue.Heavy;
        private Player _attackTarget, _tauntOwner;
        private int _attackTargetGeneration, _tauntGeneration;
        private float _impactAt, _attackEnds;
        private bool _pendingImpact;
        private MonsterMoveState _moveState;
        private MonsterAttackState _attackState;
        private MonsterIdleState _idleState;
        public bool IsRangedAttack => _projectilePrefab != null;
        public float BodyRadius => Mathf.Clamp(_bodyHeight * .34f, .19f, .5f);
        public float AttackInterval => Mathf.Max(.7f, (float)_stat._atkSpeed);
        public bool IsAttackLocked => Time.time < _attackEnds;
        public bool HasTaunt => _tauntOwner != null && _tauntOwner.isActiveAndEnabled && !_tauntOwner.IsDead && _tauntOwner.LifeGeneration == _tauntGeneration;
        public Player TauntOwner => HasTaunt ? _tauntOwner : null;

        private void OnEnable() { if (!CombatMotion.Monsters.Contains(this)) CombatMotion.Monsters.Add(this); }
        private void OnDisable() { CombatMotion.Monsters.Remove(this); ResetCombat(); }

        private void ResetCombat()
        {
            _tauntOwner = null;
            SetTarget(null);
            CancelAttack();
            _knockbackVelocity = Vector2.zero;
            _lastAttackTime = Time.time - AttackInterval + .25f;
            transform.rotation = Quaternion.identity; _facingDir = 1;
        }

        public bool TryTaunt(Player knight)
        {
            if (HasTaunt || knight == null || knight.IsDead || _monAction == eMonsterAction.Dead) return false;
            _tauntOwner = knight; _tauntGeneration = knight.LifeGeneration;
            CancelAttack(); SetTarget(knight);
            CombatDiagnostics.Record("taunt",this,knight);
            return true;
        }

        public void AcquireTarget()
        {
            if (HasTaunt) { SetTarget(_tauntOwner); return; }
            _tauntOwner = null;
            if (Target is Player current && current.isActiveAndEnabled && !current.IsDead) return;
            SetTarget(null);
            Player best = null; float nearest = float.MaxValue;
            foreach (var candidate in CombatMotion.Players)
            {
                if (candidate == null || candidate.IsDead) continue;
                float distance = (candidate.transform.position - transform.position).sqrMagnitude;
                if (distance < nearest) { nearest = distance; best = candidate; }
            }
            SetTarget(best);
        }

        public bool CanReachTarget()
        {
            AcquireTarget();
            if (Target is not Player player || player.IsDead) return false;
            Vector2 delta = player.transform.position - transform.position;
            if (IsRangedAttack) return delta.sqrMagnitude <= AttackRadius * AttackRadius;
            return CombatMotion.InFront(transform.position, player.transform.position, delta.x < 0 ? -1 : 1,
                AttackRadius, CombatMotion.MeleeLane + (IsBalanceBoss ? .12f : 0));
        }

        public void ApproachTarget()
        {
            if (IsAttackLocked) return;
            if (Target is not Player player || player.IsDead) { SetIdle(); return; }
            Vector2 current = transform.position, target = player.transform.position;
            Vector2 destination = IsRangedAttack ? target : CombatMotion.Approach(current, target, AttackRadius, Mathf.Abs(GetInstanceID()));
            if (_monAction != eMonsterAction.Walk) ChangeState(_moveState ??= new MonsterMoveState(this));
            _am.speed = 1;
            SetFlip(target.x - current.x);
            Vector2 next = Vector2.MoveTowards(current, destination, (float)GetSpeed() * Time.deltaTime);
            transform.position = new Vector3(next.x, next.y, transform.position.z);
        }

        public bool TryBeginAttack()
        {
            if (_monAI.IsAbort || IsAttackLocked || !CanReachTarget()) return false;
            if (Time.time < _lastAttackTime + AttackInterval) { SetIdle(); return false; }
            _attackTarget = Target as Player;
            _attackTargetGeneration = _attackTarget.LifeGeneration;
            SetFlip(_attackTarget.transform.position.x - transform.position.x);
            _lastAttackTime = Time.time;
            CombatDiagnostics.Record("monster-windup",this,_attackTarget,AttackInterval);
            float duration = Mathf.Clamp(AttackInterval * .6f, .55f, 1.05f);
            _attackEnds = Time.time + duration;
            _impactAt = Time.time + duration * _impactNormalized;
            _pendingImpact = true;
            ChangeState(_attackState ??= new MonsterAttackState(this));
            _am.speed = Mathf.Max(.05f, GetAnimationLength(eMonsterAction.Attack) / duration);
            _am.Play("Attack", 0, 0);
            return true;
        }

        private void TickCombat()
        {
            if (_monAction == eMonsterAction.Dead) { CancelAttack(); return; }
            if (_monAI == null || _monAI.IsAbort) return;
            // Event timing is authoritative; this covers culled animators/missing events.
            if (_pendingImpact && Time.time >= _impactAt + .04f) ResolveAttackImpact();
            if (_attackEnds > 0 && Time.time >= _attackEnds) { _attackEnds = 0; SetIdle(); }
        }

        public void ResolveAttackImpact()
        {
            if (!_pendingImpact || _monAction == eMonsterAction.Dead || _monAI.IsAbort) return;
            _pendingImpact = false;
            if (_attackTarget == null || !_attackTarget.isActiveAndEnabled || _attackTarget.IsDead || _attackTarget.LifeGeneration != _attackTargetGeneration) return;
            if (IsRangedAttack)
            {
                Vector3 muzzle = transform.position + new Vector3(FacingDir * .25f, Mathf.Max(.3f, _bodyHeight * .6f), 0);
                MonsterProjectile.Launch(_projectilePrefab, this, _attackTarget, muzzle, _projectileSpeed, _projectileArc, _projectileAngle);
                CombatDiagnostics.Record("monster-release",this,_attackTarget,AttackInterval);
                CombatAudio.Play(_attackSound,KingdomIdle.UGUI.SoundChannel.Monsters,transform.position);
            }
            else if (CombatMotion.InFront(transform.position, _attackTarget.transform.position, FacingDir, AttackRadius + .1f,
                CombatMotion.MeleeImpactLane + (IsBalanceBoss ? .12f : 0)))
            {
                CombatDiagnostics.Record("monster-melee-hit",this,_attackTarget,AttackInterval);
                CombatAudio.Play(_attackSound,KingdomIdle.UGUI.SoundChannel.Monsters,transform.position);
                _attackTarget.TakeDamage(this);
            }
            else CombatDiagnostics.Record("monster-melee-miss",this,_attackTarget,AttackInterval);
        }

        private void CancelAttack()
        {
            _pendingImpact = false; _attackTarget = null; _attackEnds = 0;
            if (_am != null) _am.speed = 1;
        }

        private void SetIdle()
        {
            if (_monAction == eMonsterAction.Dead || _am == null) return;
            _am.speed = 1;
            if (_monAction != eMonsterAction.Idle) ChangeState(_idleState ??= new MonsterIdleState(this));
        }

        private void LateUpdate()
        {
            if (_monAction == eMonsterAction.Dead || IsAttackLocked) return;
            Vector2 position = (Vector2)transform.position + CombatMotion.Separate(transform, BodyRadius, .8f);
            position = CombatMotion.Clamp(position);
            transform.position = new Vector3(position.x, position.y, transform.position.z);
        }
    }
}
