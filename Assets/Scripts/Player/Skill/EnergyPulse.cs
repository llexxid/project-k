using Scripts.Core;
using Scripts.Core.inteface;
using Scripts.Monster;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 에너지 파동 (Elite_Mage).
/// 기본공격 쿨다운 중에만 발동 가능.
/// CastEnergyPulse 애니메이션 + VFX 동시 재생.
/// 재생 중에는 기본공격 차단 (IsActive).
/// </summary>
public sealed class EnergyPulse : ActiveSkill
{
    private readonly float _triggerRange;
    private readonly float _cooldown;
    private readonly float _knockbackForce;
    private readonly ActiveSkill _basicAttackRef;

    private readonly LayerMask _enemyLayer = GameLayers.EnemyMask;
    private readonly List<Collider2D> _hitResults = new List<Collider2D>();

    private EnergyPulseVFX _vfxInstance;
    private bool _isPlaying;
    private float _playEndTime;

    public override string DisplayName => "에너지 파동";
    public override float Cooldown => _cooldown;
    public override bool IsActive => _isPlaying;

    public EnergyPulse(Player player, ActiveSkill basicAttack,
                       float triggerRange, float cooldown, float knockbackForce)
        : base(player)
    {
        _basicAttackRef = basicAttack;
        _triggerRange = triggerRange;
        _cooldown = cooldown;
        _knockbackForce = knockbackForce;
    }

    public override bool CanExecute()
    {
        // 기본공격이 쿨다운 중이 아니면 발동 불가
        if (_basicAttackRef != null && _basicAttackRef.IsReady) return false;

        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(_enemyLayer);
        filter.useLayerMask = true;
        filter.useTriggers = true;

        int count = Physics2D.OverlapCircle(
            _player.transform.position, _triggerRange, filter, _hitResults);

        for (int i = 0; i < count; i++)
        {
            var mon = _hitResults[i].GetComponentInParent<Monster>();
            if (mon != null && mon.MonAction != eMonsterAction.Dead) return true;
        }
        return false;
    }

    public override float Execute()
    {
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(_enemyLayer);
        filter.useLayerMask = true;
        filter.useTriggers = true;

        int hitCount = Physics2D.OverlapCircle(
            _player.transform.position, _triggerRange, filter, _hitResults);

        int baseAtk = _player.playerStatus?.Atk ?? 0;
        int skillDamage = baseAtk * 2;
        var proxy = new DamageProxy((ulong)skillDamage, _player);

        for (int i = 0; i < hitCount; i++)
        {
            var mon = _hitResults[i].GetComponentInParent<Monster>();
            if (mon == null || mon.MonAction == eMonsterAction.Dead) continue;

            var damageable = _hitResults[i].GetComponentInParent<IDamageable>();
            damageable?.TakeDamage(proxy);

            Vector2 dir = ((Vector2)mon.transform.position - (Vector2)_player.transform.position).normalized;
            mon.ApplyKnockback(dir, _knockbackForce);
        }

        // VFX + 캐스팅 애니메이션 동시 재생
        SpawnVFX();

        string animName = "CastEnergyPulse";
        float animLen = _player.GetClipLength(animName, 0.6f);
        _player.PlaySkillAnimation(animName, animLen);

        _isPlaying = true;
        _playEndTime = Time.time + animLen;

        _nextAvailableTime = Time.time + animLen + _cooldown;
        return animLen;
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
