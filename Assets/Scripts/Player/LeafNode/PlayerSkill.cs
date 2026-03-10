using Scripts.Core;
using Scripts.Core.inteface;
using Scripts.Monster;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 스킬 하나를 BT LeafNode로 관리하는 클래스.
/// 쿨타임 체크 → 범위 내 적 탐지 → 데미지 적용 → VFX 재생을 담당한다.
/// PlayerOrder.RebuildSkillTree()에서 보유 스킬 수만큼 동적으로 생성된다.
/// </summary>
public class PlayerSkill
{
    private readonly Player _player;
    private readonly SkillData _skillData;
    private readonly PlayerDetection _detection;

    // 적 레이어 마스크
    private readonly LayerMask _enemyLayer = GameLayers.EnemyMask;

    // 다음 사용 가능 시각 (Time.time 기준)
    private float _nextAvailableTime = 0f;

    // Physics2D 결과 버퍼 (GC 최소화)
    private readonly List<Collider2D> _hitResults = new List<Collider2D>();

    public PlayerSkill(Player player, SkillData skillData, PlayerDetection detection)
    {
        _player    = player;
        _skillData = skillData;
        _detection = detection;
    }

    /// <summary>
    /// BT에서 매 프레임 호출.
    /// 쿨타임이 남았거나 범위 내 적이 없으면 Failure, 발동 성공 시 Success.
    /// </summary>
    public NodeState Execute()
    {
        // [1] 쿨타임 체크
        if (Time.time < _nextAvailableTime) return NodeState.Failure;

        // [2] 범위 내 적 탐지
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(_enemyLayer);
        filter.useLayerMask = true;
        filter.useTriggers  = true;

        float radius = _player.playerOrder?._attack?.attackRadius ?? 3f;
        int hitCount = Physics2D.OverlapCircle(
            _player.transform.position, radius, filter, _hitResults);

        if (hitCount == 0) return NodeState.Failure;

        // [3] 쿨타임 소모
        _nextAvailableTime = Time.time + _skillData.cooldown;

        // [4] 데미지 계산: 기본 Atk × 스킬 계수
        int baseAtk     = _player.playerStatus?.Atk ?? 0;
        int skillDamage = Mathf.RoundToInt(baseAtk * _skillData.damage);

        Debug.Log($"[스킬] {_skillData.skillName} | Atk:{baseAtk} × {_skillData.damage} = {skillDamage}");

        // [5] VFX 재생 (eVFXType이 스킬 이름과 일치하는 경우)
        bool vfxPlayed = false;

        // [6] 범위 내 적에게 데미지 적용 (광역)
        for (int i = 0; i < hitCount; i++)
        {
            if (_hitResults[i].TryGetComponent<IDamageable>(out var target))
            {
                // 이미 Dead 상태인 몬스터는 건너뜀
                if (_hitResults[i].TryGetComponent<Monster>(out var mon)
                    && mon.MonAction == eMonsterAction.Dead) continue;

                // VFX는 첫 번째 적 위치에서만 한 번 재생
                if (!vfxPlayed)
                {
                    TryPlayVFX(target);
                    vfxPlayed = true;
                }

                bool isAlive = target.TakeDamage(new DamageProxy(skillDamage));
                if (!isAlive)
                {
                    // 몬스터 사망 시 Idle로 전환 (Attack은 Trigger라 자동 리셋됨)
                    _player._playerAction = ePlayerAction.Idle;
                    if (_detection != null) _detection.currentTarget = null;
                    _player.currentTarget = null;
                    break;
                }
            }
        }

        // [7] 공격 애니메이션 재생
        _player.PlayAttackAnimation();

        return NodeState.Success;
    }

    /// <summary>
    /// 스킬 이름 기반으로 VFX 재생을 시도한다.
    /// eVFXType 열거형에 해당 이름이 없으면 조용히 스킵한다.
    /// </summary>
    private void TryPlayVFX(IDamageable target)
    {
        if (!System.Enum.TryParse(_skillData.skillName, out eVFXType vfxType)) return;

        VFXManager.Instance?.GetVFX(vfxType, target.targetPos,
            _player.transform.rotation,
            (vfx) => { vfx?.ActiveEffect(200); });
    }

    // ─────────────────────────────────────────────────────────
    // BT 래퍼 노드
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// PlayerOrder에서 BT에 등록할 때 사용하는 래퍼 노드.
    /// </summary>
    public class SkillNode : Node
    {
        private readonly PlayerSkill _skill;
        public SkillNode(PlayerSkill skill) { _skill = skill; }
        public override NodeState Evaluate() => _skill.Execute();
    }

    // ─────────────────────────────────────────────────────────
    // 데미지 래퍼
    // ─────────────────────────────────────────────────────────

    private class DamageProxy : IAttackable
    {
        public ulong damage { get; }
        public Vector3 attackerPos => Vector3.zero;
        public bool Attack(IDamageable target) => false;
        public DamageProxy(int dmg) { damage = (ulong)dmg; }
    }
}
