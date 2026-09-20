using KingdomIdle.Combat;
using Scripts.Core;
using UnityEngine;

public partial class Player
{
    public int LifeGeneration { get; private set; }
    public int BasicAttackSerial { get; private set; }
    public long ShieldHP => Time.time < _shieldExpires ? _shieldHP : 0;
    private long _shieldHP;
    private float _shieldExpires, _pendingImpactAt;
    private float _vfxFootY = -.5f, _vfxHeadY = .5f;
    public Vector3 VfxFootPosition => transform.TransformPoint(Vector3.up * _vfxFootY);
    public Vector3 VfxHeadPosition => transform.TransformPoint(Vector3.up * _vfxHeadY);
    public void SetVfxBodyAnchors(float foot, float head) { _vfxFootY = foot; _vfxHeadY = head; }
    private void OnEnable() { if (!CombatMotion.Players.Contains(this)) CombatMotion.Players.Add(this); }
    private void OnDisable() { CombatMotion.Players.Remove(this); CancelCombat(); }

    private void LateUpdate()
    {
        if (_isDead || IsInAttackAnimation || playerOrder.IsAbort) return;
        Vector2 feet = VfxFootPosition;
        Vector2 delta = CombatMotion.Clamp(feet + CombatMotion.Separate(transform, CombatMotion.PlayerRadius, 1.2f)) - feet;
        transform.position += new Vector3(delta.x, delta.y, 0);
    }

    public bool IsInMeleeReach(Vector3 target, float range, float lane = CombatMotion.MeleeLane) =>
        CombatMotion.InFront(VfxFootPosition, target, Mathf.Sign(transform.localScale.x), range, lane);

    public void GrantShield(long amount, float duration)
    {
        _shieldHP = System.Math.Max(ShieldHP, amount);
        _shieldExpires = Time.time + duration;
        CombatStatusVisuals.Ensure(this);
    }

    public float BasicImpactNormalized(string eventName)
    {
        if (_am != null && _am.runtimeAnimatorController != null)
            foreach (var clip in _am.runtimeAnimatorController.animationClips)
                if (clip.name == "Attack_Anim")
                    foreach (var cue in clip.events)
                        if (cue.functionName == eventName || (eventName == "OnSkillHit" && cue.functionName == "OnAttackHit"))
                            return Mathf.Clamp01(cue.time / Mathf.Max(.01f, clip.length));
        return .55f;
    }

    private void CancelCombat()
    {
        _hasPendingSkillDamage = false; _pendingSkillTargets.Clear(); _pendingGenerations.Clear();
        _hasPendingVFX = false; _pendingVFXPositions.Clear(); _pendingVFXTargets.Clear();
        _attackAnimEndTime = 0; _pendingAnimRecovery = false; _shieldHP = 0;
        ResetTarget(_currentTarget);
        LifeGeneration++;
    }
}
