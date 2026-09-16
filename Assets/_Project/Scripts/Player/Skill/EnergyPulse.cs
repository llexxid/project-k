using KingdomIdle.Balance;
using KingdomIdle.Combat;
using Scripts.Core;
using Scripts.Core.inteface;
using Scripts.Monster;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 에너지 파동 (Elite_Mage).
/// 가까운 적을 밀쳐내는 방어용 파동. 보스에는 피해만 적용한다.
/// CastEnergyPulse 애니메이션 + VFX 동시 재생.
/// 재생 중에는 기본공격 차단 (IsActive).
/// </summary>
public sealed class EnergyPulse : ActiveSkill
{
    private readonly float _triggerRange;
    private readonly float _cooldown;
    private readonly float _knockbackForce;
    private readonly float _damageMultiplier;
    private readonly List<Monster> _targets = new(6);

    private EnergyPulseVFX _vfxInstance;
    private bool _isPlaying;
    private float _playEndTime;

    public override string DisplayName => "에너지 파동";
    public override float Cooldown => _cooldown;
    public override bool IsActive => _isPlaying;

    public EnergyPulse(Player player, ActiveSkill basicAttack,
                       float triggerRange, float cooldown, float knockbackForce, float damageMultiplier = 2f)
        : base(player)
    {
        _triggerRange = triggerRange;
        _cooldown = cooldown;
        _knockbackForce = knockbackForce;
        _damageMultiplier = damageMultiplier;
    }

    public override bool CanExecute()
    {
        foreach (var mon in CombatMotion.Monsters)
            if (InRange(mon)) return true;
        return false;
    }

    private bool InRange(Monster mon) => mon != null && mon.isActiveAndEnabled && mon.MonAction != eMonsterAction.Dead &&
        ((Vector2)(mon.transform.position-_player.transform.position)).sqrMagnitude <= _triggerRange*_triggerRange;

    public override float Execute()
    {
        // Snapshot the foot-space registry before hits can return monsters to their pool.
        _targets.Clear();
        foreach (var mon in CombatMotion.Monsters)
            if (InRange(mon)) { _targets.Add(mon); if (_targets.Count == 6) break; }

        long baseAtk = _player.playerStatus?.Atk ?? 0;
        long skillDamage = BalanceMath.Damage(baseAtk, (decimal)_damageMultiplier);
        var proxy = new DamageProxy((ulong)skillDamage, _player);

        foreach (var mon in _targets)
        {
            CombatDiagnostics.Record(mon.IsBalanceBoss ? "pulse-boss-immune" : "pulse-control",_player,mon);
            if (!mon.TakeDamage(proxy)) continue;

            Vector2 dir = ((Vector2)mon.transform.position - (Vector2)_player.transform.position).normalized;
            mon.ApplyKnockback(dir, _knockbackForce);
            if (!mon.IsBalanceBoss) MonsterCCState.Apply(mon, CrowdControlKind.Stun, 1.25f, 0);
        }

        // VFX + 캐스팅 애니메이션 동시 재생
        SpawnVFX();

        string animName = "CastEnergyPulse";
        // 애니메이션이 마지막 프레임까지 완전히 재생되도록 약간의 버퍼를 둔다.
        // (버퍼가 없으면 _pendingAnimRecovery 가 끝 프레임을 Attack_Anim 로 덮어쓴다)
        float protectLen = 0.7f;
        _player.PlaySkillAnimation(animName, protectLen);

        _isPlaying = true;
        _playEndTime = Time.time + protectLen;

        _nextAvailableTime = Time.time + _cooldown;
        return protectLen;
    }

    public override void Tick()
    {
        if (_isPlaying && Time.time >= _playEndTime)
            _isPlaying = false;
    }

    private void SpawnVFX()
    {
        var prefab = _player.EnergyPulseVFXPrefab;
        if (prefab == null) return;

        if (_vfxInstance == null)
        {
            var obj = Object.Instantiate(prefab.gameObject);
            _vfxInstance = obj.GetComponent<EnergyPulseVFX>();
            _vfxInstance.Init();
        }

        _vfxInstance.Play(_player.transform.position);
    }
}
