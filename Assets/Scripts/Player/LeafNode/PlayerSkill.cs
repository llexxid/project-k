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
    private readonly SkillSharedState _sharedState;

    // 적 레이어 마스크
    private readonly LayerMask _enemyLayer = GameLayers.EnemyMask;

    // 다음 사용 가능 시각 (Time.time 기준)
    private float _nextAvailableTime = 0f;

    // Physics2D 결과 버퍼 (GC 최소화)
    private readonly List<Collider2D> _hitResults = new List<Collider2D>();

    // Animation Event 전달용 타격 대상 버퍼 (GC 최소화)
    private readonly List<IDamageable> _pendingTargets = new List<IDamageable>();


    public PlayerSkill(Player player, SkillData skillData, PlayerDetection detection, SkillSharedState sharedState)
    {
        _player      = player;
        _skillData   = skillData;
        _detection   = detection;
        _sharedState = sharedState;
    }

    /// <summary>
    /// 같은 플레이어의 모든 스킬 인스턴스가 공유하는 잠금 상태.
    /// 어느 스킬이 발동하면 나머지 스킬도 해당 쿨타임 동안 대기한다.
    /// </summary>
    public class SkillSharedState
    {
        public float nextAvailableTime;
    }

    /// <summary>
    /// BT에서 매 프레임 호출.
    /// 쿨타임이 남았거나 범위 내 적이 없으면 Failure, 발동 성공 시 Success.
    /// </summary>
    public NodeState Execute()
    {
        // [1] 쿨타임 체크 (개별 + 공유)
        if (Time.time < _nextAvailableTime) 
            return NodeState.Failure;
        if (_sharedState != null && Time.time < _sharedState.nextAvailableTime) 
            return NodeState.Failure;

        // [2] 범위 내 적 탐지
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(_enemyLayer);
        filter.useLayerMask = true;
        filter.useTriggers  = true;

        float radius = _player.playerOrder?._attack?.attackRadius ?? 3f;
        int hitCount = Physics2D.OverlapCircle(
            _player.transform.position, radius, filter, _hitResults);

        if (hitCount == 0) 
            return NodeState.Failure;

        // [2-a] 플레이어 정면 방향 계산 (스케일 X 부호 기준)
        float facingSign = _player.transform.localScale.x >= 0f ? 1f : -1f;
        Vector2 facingDir = new Vector2(facingSign, 0f);

        // [2-b] 방향성 스킬이면 정면 원뿔 밖의 적 제거
        if (_skillData.attackAngle < 360f)
            hitCount = FilterByAngle(hitCount, facingDir, _skillData.attackAngle * 0.5f);

        if (hitCount == 0) 
            return NodeState.Failure;

        // 탐지된 적이 전부 Dead 상태면 스킬 발동하지 않음
        bool hasAliveTarget = false;
        for (int i = 0; i < hitCount; i++)
        {
            // 자식 콜라이더에서도 부모로 올라가 Monster를 탐색
            var m = _hitResults[i].GetComponentInParent<Monster>();
            bool isDead = m != null && m.MonAction == eMonsterAction.Dead;
            if (!isDead) { hasAliveTarget = true; break; }
        }
        if (!hasAliveTarget) 
            return NodeState.Failure;

        // [3] 쿨타임 소모 (강화 레벨 반영)
        var enhancer = SkillEnhanceManager.Instance;
        float finalCooldown = enhancer != null
            ? enhancer.Runtime.GetFinalCooldown(_skillData)
            : _skillData.cooldown;
        _nextAvailableTime = Time.time + finalCooldown;
        // 공유 잠금은 애니메이션 길이만큼만 → 다음 스킬이 자연스럽게 이어서 발동
        if (_sharedState != null) _sharedState.nextAvailableTime = Time.time + 0.8f;

        // [4] 데미지 계산: 기본 Atk × 강화된 스킬 계수
        int   baseAtk      = _player.playerStatus?.Atk ?? 0;
        float finalDamage  = enhancer != null
            ? enhancer.Runtime.GetFinalDamage(_skillData)
            : _skillData.damage;
        int   skillDamage  = Mathf.RoundToInt(baseAtk * finalDamage);

        Debug.Log($"[스킬] {_skillData.skillName} | Atk:{baseAtk} × {finalDamage:F2}(Lv.{enhancer?.Runtime.GetLevel(_skillData.skillName) ?? 0}) = {skillDamage} | CD:{finalCooldown:F2}s");

        // [5] VFX 정보를 Player에 등록 (Animation Event OnSkillVFXStart에서 재생)
        // 위치는 지금 시점(Execute)에 확정해서 저장 — 이벤트 발화 시 플레이어가 이동해도 위치가 흔들리지 않는다
        if (System.Enum.TryParse(_skillData.skillName, out eVFXType vfxType))
        {
            if (_skillData.vfxOnTarget)
            {
                // 탐지된 모든 살아있는 적에게 VFX 등록
                bool first = true;
                for (int i = 0; i < hitCount; i++)
                {
                    var m = _hitResults[i].GetComponentInParent<Monster>();
                    if (m == null || m.MonAction == eMonsterAction.Dead) continue;

                    Vector3 enemyPos = m.transform.position;
                    if (first)
                    {
                        _player.SetPendingSkillVFX(vfxType, enemyPos, facingDir.x, _skillData.vfxDuration, _skillData.flipVFX);
                        first = false;
                    }
                    else
                    {
                        _player.AddPendingSkillVFXTarget(enemyPos);
                    }
                }
            }
            else
            {
                Vector3 vfxPos = _player.transform.position + (Vector3)(facingDir * _skillData.vfxForwardOffset);
                _player.SetPendingSkillVFX(vfxType, vfxPos, facingDir.x,
                    _skillData.vfxDuration, _skillData.flipVFX);
            }
        }

        // [6] 데미지 적용
        // animationStateName이 설정된 스킬 → Animation Event(OnSkillHit)에서 적용
        // animationStateName이 없는 스킬  → 즉시 적용 (VFX 스킬 등 전용 애니메이션 없는 경우)
        if (!string.IsNullOrEmpty(_skillData.animationStateName))
        {
            _pendingTargets.Clear();
            for (int i = 0; i < hitCount; i++)
            {
                // 이미 Dead 상태인 몬스터는 건너뜀
                Monster mon = _hitResults[i].GetComponentInParent<Monster>(); // GetComponent → GetComponentInParent

                if (mon.MonAction == eMonsterAction.Dead)
                {
					continue;
				}

                bool isAlive = mon.TakeDamage(_player);
                if (!isAlive)
                {
                    // 몬스터 사망 시 Idle로 전환 (Attack은 Trigger라 자동 리셋됨)
                    Debug.Log($"[스킬 사망] {_skillData.skillName} attacker : {_player.gameobj.GetInstanceID()} | monster : {mon.gameobj.GetInstanceID()}");
                    _player.SetAnimation(ePlayerAction.Idle);
                    //_player.currentTarget = null;
                    break;
                }
            }
        }

        // [7] 공격 애니메이션 재생
        _player.PlaySkillAnimation(_skillData.animationStateName);
        Debug.Log("SKill Node Success");
		return NodeState.Success;
    }

    /// <summary>
    /// OverlapCircle 결과에서 플레이어 정면 원뿔 범위 밖의 적을 제거한다.
    /// 유효한 적 수를 반환한다.
    /// </summary>
    private int FilterByAngle(int hitCount, Vector2 facingDir, float halfAngle)
    {
        int validCount = 0;
        for (int i = 0; i < hitCount; i++)
        {
            Vector2 toEnemy = ((Vector2)_hitResults[i].transform.position - (Vector2)_player.transform.position);
            // 플레이어와 겹쳐있는 경우는 항상 통과
            if (toEnemy == Vector2.zero || Vector2.Angle(facingDir, toEnemy) <= halfAngle)
            {
                _hitResults[validCount] = _hitResults[i];
                validCount++;
            }
        }
        return validCount;
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

    public class DamageProxy : IAttackable
    {
        public ulong damage { get; }
        public Vector3 attackerPos => Vector3.zero;

        public GameObject gameobj { get; }

        public bool Attack(IDamageable target) => false;
        public DamageProxy(ulong damage, GameObject owner) { this.damage = damage; this.gameobj = owner; }
    }
}
