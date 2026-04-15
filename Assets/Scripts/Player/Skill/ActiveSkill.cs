using Scripts.Core.inteface;
using UnityEngine;

/// <summary>
/// 모든 액티브 스킬의 추상 기반 클래스.
/// </summary>
public abstract class ActiveSkill
{
    protected readonly Player _player;
    protected float _nextAvailableTime;

    public abstract string DisplayName { get; }
    public abstract float Cooldown { get; }

    public float CooldownRemaining => Mathf.Max(0f, _nextAvailableTime - Time.time);
    public bool IsReady => Time.time >= _nextAvailableTime;

    /// <summary>자기 트리거 스킬이면 true (BT 밖에서 Tick으로 발동).</summary>
    public virtual bool IsSelfTriggered => false;

    /// <summary>스킬이 현재 활성 상태(애니메이션 재생 중 등)이면 true.</summary>
    public virtual bool IsActive => false;

    protected ActiveSkill(Player player) { _player = player; }

    /// <summary>발동 조건 충족 여부.</summary>
    public abstract bool CanExecute();

    /// <summary>스킬 실행. 애니메이션 잠금 시간(초) 반환.</summary>
    public abstract float Execute();

    /// <summary>매 프레임 호출 — 지속 효과 처리.</summary>
    public virtual void Tick() { }

    /// <summary>공격 애니메이션 길이(초).</summary>
    protected float GetAttackAnimLength()
        => _player.GetClipLength("Attack_Anim", 0.4f);

    // ── 데미지 전달 프록시 ──
    public class DamageProxy : IAttackable, IRewardable
    {
        public ulong damage { get; }
        public Vector3 attackerPos => _owner != null ? _owner.transform.position : Vector3.zero;
        public GameObject gameobj { get; }
        private readonly Player _owner;

        public bool Attack(IDamageable target) => false;
        public void GiveReward(int gold, int ancientCoin) => _owner?.GiveReward(gold, ancientCoin);

        public DamageProxy(ulong damage, Player owner)
        {
            this.damage = damage;
            this.gameobj = owner != null ? owner.gameObject : null;
            _owner = owner;
        }
    }
}
