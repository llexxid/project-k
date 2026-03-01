using Scripts.Core;
using Scripts.Core.inteface;
using Scripts.Monster;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어의 공격 로직을 담당하는 클래스.
/// 일반 공격과 스킬 공격을 쿨타임 기반으로 관리하며,
/// 범위 내 적을 탐지하여 즉시 데미지를 적용한다.
/// </summary>
public class PlayerAttack
{
    // 적 탐지를 담당하는 컴포넌트 (타겟 초기화 시 참조)
    [SerializeField] private PlayerDetection _detection;

    // 공격 애니메이션 한 사이클 길이 (초). attackRate의 최솟값 기준으로도 사용
    private const float ANIMATION_DURATION = 0.4f;

    // 일반 공격 간격 (초). 이 값이 작을수록 공격 속도가 빠름
    // 공격 애니메이션 길이(ANIMATION_DURATION)와 맞춰 기본값 설정 → 애니메이션 1사이클 = 공격 1회
    public float attackRate = ANIMATION_DURATION;
    // 다음 공격이 가능한 시각 (Time.time 기준). 현재 시간이 이 값 이상일 때만 공격 가능
    private float _nextAttackTime = 0f;

    // 스킬 활성화 및 데이터 참조용
    public SkillManager skillManager;
    public SkillDatabase skillDatabase;
    public VFXManager vfxManager;

    // 공격이 닿는 범위 (반지름, 단위: 유니티 단위)
    public float attackRadius = 3f;
    // Physics2D.OverlapCircle 결과를 재사용하기 위한 버퍼 (GC 최소화)
    private List<Collider2D> _hitResults = new List<Collider2D>();

    // 스킬별 쿨타임 추적: 스킬 이름 → 다음 사용 가능한 Time.time
    private Dictionary<string, float> _skillCooldowns = new Dictionary<string, float>();

    // 공격 주체인 플레이어 참조
    [SerializeField]
    public Player player;

    // 6번 레이어(Enemy)만 탐지하도록 설정한 레이어 마스크
    LayerMask enemyLayer = 1 << 6;

    /// <summary>
    /// 생성자: Player로부터 필요한 참조를 가져오고 스킬 쿨타임을 초기화한다.
    /// </summary>
    public PlayerAttack(Player player, PlayerDetection detection = null)
    {
        this.player = player;
        this._detection = detection;
        this.skillDatabase = player.skillDatabase;
        this.skillManager = player.skillManager;

        // attackRate가 0 이하면 애니메이션 길이로 자동 보정
        // (외부에서 잘못된 값이 주입되어 매 프레임 공격되는 상황 방지)
        if (attackRate <= 0f)
        {
            Debug.LogWarning($"[PlayerAttack] attackRate가 {attackRate}로 설정되어 있습니다. " +
                             $"애니메이션 길이({ANIMATION_DURATION}초)로 자동 보정합니다.");
            attackRate = ANIMATION_DURATION;
        }

        // Wind_Lance 스킬을 게임 시작 즉시 사용할 수 있도록 쿨타임을 0으로 초기화
        _skillCooldowns["Wind_Lance"] = 0f;
    }

    /// <summary>
    /// 행동 트리에서 매 프레임 호출되는 공격 판정 함수.
    /// 쿨타임 → 범위 탐지 → 스킬/일반 공격 분기 순으로 처리한다.
    /// </summary>
    /// <returns>공격이 실행되면 Success, 쿨타임 중이거나 범위 내 적이 없으면 Failure</returns>
    public NodeState Attack()
    {
        // [1단계] 일반 공격 쿨타임 체크
        // 아직 쿨타임이 남아 있으면 이번 프레임은 공격하지 않음
        if (Time.time < _nextAttackTime) return NodeState.Failure;

        // [2단계] 적 탐지 필터 설정
        // enemyLayer에 속한 Trigger 콜라이더만 탐지
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(enemyLayer);
        filter.useLayerMask = true;
        filter.useTriggers = true;

        // [3단계] 플레이어 중심으로 attackRadius 반경 내 적 탐지
        int hitCount = Physics2D.OverlapCircle(player.transform.position, attackRadius, filter, _hitResults);

        // 범위 내 적이 한 명도 없으면 공격할 필요 없음
        if (hitCount == 0) return NodeState.Failure;

        // [4단계] 쿨타임 소모 (적이 있을 때만 소모하여 낭비 방지)
        _nextAttackTime = Time.time + attackRate;

        // [5단계] PlayerStatus에서 기본 공격력(Atk) 조회
        int baseAtk = player.playerStatus?.Atk ?? 0;

        // [6단계] 스킬 데이터 및 쿨타임 조회
        string skillName = "Wind_Lance";
        SkillData data = skillDatabase?.GetSkill(skillName);
        // 해당 스킬의 다음 사용 가능 시각 (없으면 0f → 즉시 사용 가능)
        _skillCooldowns.TryGetValue(skillName, out float nextSkillAvailable);

        // [7단계] 스킬 쿨타임이 끝났으면 스킬 공격, 아니면 일반 공격 분기
        if (data != null && Time.time >= nextSkillAvailable)
        {
            // ══ 스킬 공격 분기 ══
            // 최종 데미지 = 기본 Atk × 스킬 계수(SkillData.damage)
            float skillCoefficient = data.damage;
            int skillDamage = Mathf.RoundToInt(baseAtk * skillCoefficient);

            Debug.Log($"[스킬 공격] Atk: {baseAtk} × 계수({data.skillName}): {skillCoefficient} = 데미지: {skillDamage}");

            // 스킬 쿨타임 갱신 후 스킬 매니저에 활성화 요청 (이펙트·버프 등 처리)
            _skillCooldowns[skillName] = Time.time + data.cooldown;
            skillManager?.ActivateSkill(skillName);

            // 범위 내 모든 적에게 스킬 데미지 적용 (광역)
            bool vfxPlayed = false;
            for (int i = 0; i < hitCount; i++)
            {
                if (_hitResults[i].TryGetComponent<IDamageable>(out var target))
                {
                    // VFX는 첫 번째 적에게만 한 번 재생하여 중복 방지
                    if (!vfxPlayed)
                    {
                        VFXManager.Instance?.GetVFX(eVFXType.Wind_Lance, target.targetPos,
                            player.transform.rotation, (vfx) => { vfx?.ActiveEffect(200); });
                        vfxPlayed = true;
                    }

                    // 데미지 적용. 적이 사망하면 루프 즉시 중단
                    bool isAlive = ApplyDamage(target, (ulong)skillDamage);
                    if (!isAlive) break;
                }
            }
        }
        else
        {
            // ══ 일반 공격 분기 ══
            // 최종 데미지 = 기본 Atk × 1.0 (계수 없음, 단일 대상)
            float normalCoefficient = 1.0f;
            int normalDamage = Mathf.RoundToInt(baseAtk * normalCoefficient);

            Debug.Log($"[일반 공격] Atk: {baseAtk} × 계수: {normalCoefficient} = 데미지: {normalDamage}");

            // 범위 내 첫 번째 적 한 명에게만 데미지 적용 (단일 타깃)
            for (int i = 0; i < hitCount; i++)
            {
                if (_hitResults[i].TryGetComponent<IDamageable>(out var target))
                {
                    ApplyDamage(target, (ulong)normalDamage);
                    break; // 첫 번째 적 처리 후 탈출
                }
            }
        }

        // [8단계] 데미지와 무관하게 공격 애니메이션 재생 (시각 피드백)
        player.PlayAttackAnimation();

        return NodeState.Success;
    }

    /// <summary>
    /// 지정한 대상에게 계산된 데미지를 적용하고 사망 여부를 처리한다.
    /// </summary>
    /// <param name="target">데미지를 받을 IDamageable 대상</param>
    /// <param name="damage">적용할 최종 데미지 수치</param>
    /// <returns>대상이 살아있으면 true, 사망했으면 false</returns>
    private bool ApplyDamage(IDamageable target, ulong damage)
    {
        if (target == null)
        {
            Debug.Log("대상이 null 또는 이미 파괴됨 - 건너뜀");
            return false;
        }

        // DamageProxy로 데미지 수치를 IAttackable 형태로 포장하여 TakeDamage에 전달
        bool isAlive = target.TakeDamage(new DamageProxy(damage));

        if (!isAlive)
        {
            Debug.Log("Monster Is Dead!! → Idle 전환");

            // 몬스터 사망 후 다음 적을 즉시 공격할 수 있도록 쿨타임 초기화
            _nextAttackTime = 0f;

            // 플레이어를 Idle 상태로 전환하여 진행 중인 공격 모션 중단
            player?.TurnOnAnimation(ePlayerAction.Idle);

            // 현재 타겟 참조를 비워 탐지 루틴이 새 타겟을 찾도록 유도
            if (player != null) player.currentTarget = null;
            if (_detection != null) _detection.currentTarget = null;
        }
        else
        {
            Debug.Log("적 데미지 적용 완료 (대상은 아직 살아있음)");
        }

        return isAlive;
    }

    /// <summary>
    /// ulong 데미지를 IAttackable 인터페이스로 포장하는 내부 래퍼 클래스.
    /// TakeDamage(IAttackable)를 호출할 때 계산된 데미지 수치만 넘기기 위해 사용.
    /// </summary>
    private class DamageProxy : IAttackable
    {
        // 전달할 데미지 수치
        public ulong damage { get; private set; }
        // 공격자 위치(사용 안 함, 인터페이스 구현 요건)
        public Vector3 attackerPos => Vector3.zero;
        // 공격 메서드(사용 안 함, 인터페이스 구현 요건)
        public bool Attack(IDamageable target) => false;

        public DamageProxy(ulong damage)
        {
            this.damage = damage;
        }
    }

    /// <summary>
    /// 행동 트리에서 PlayerAttack.Attack()을 노드로 감싸는 래퍼 클래스.
    /// PlayerOrder에서 이 노드를 트리에 등록하여 매 프레임 Evaluate()가 호출된다.
    /// </summary>
    public class AttackNode : Node
    {
        private PlayerAttack _attack;
        public AttackNode(PlayerAttack attack) { _attack = attack; }
        public override NodeState Evaluate() => _attack.Attack();
    }
}