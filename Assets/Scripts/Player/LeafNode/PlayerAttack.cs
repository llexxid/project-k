using Scripts.Core;
using Scripts.Core.inteface;
using Scripts.Monster;
using System.Collections.Generic;
using UnityEngine;

public class PlayerAttack
{
    [SerializeField] private PlayerDetection _detection;
    public float attackRate;
    private float _nextAttackTime = 0f;

    public SkillManager skillManager;
    public SkillDatabase skillDatabase; // 스킬 데이터 참조용
    public VFXManager vfxManager;
    public IAttackable attackable;

    public float attackRadius = 3f;
    private List<Collider2D> _hitResults = new List<Collider2D>();
    // skillName -> next usable absolute time (Time.time)
    private Dictionary<string, float> _skillCooldowns = new Dictionary<string, float>();

    [SerializeField]
    public Player player;

    LayerMask enemyLayer = 1 << 6;

    // 애니메이션 종료 시점에 사용할 대상 목록
    private List<IDamageable> _pendingTargets = new List<IDamageable>();

    public PlayerAttack(Player player)
    {
        this.player = player;
        this.skillDatabase = player.skillDatabase;
        this.skillManager = player.skillManager;
        // 즉시 사용 가능하도록 초기화
        _skillCooldowns["Wind_Lance"] = 0f;
    }

    public NodeState Attack()
    {
        // 일반 공격(attackRate) 쿨타임 체크
        if (Time.time < _nextAttackTime) return NodeState.Failure;
        _nextAttackTime = Time.time + attackRate;

        // 스킬 데이터 조회
        string skillName = "Wind_Lance";
        SkillData data = skillDatabase?.GetSkill(skillName);

        // 현재 스킬의 다음 사용 가능 시간 조회 (없으면 즉시 사용 가능)
        _skillCooldowns.TryGetValue(skillName, out float nextAvailable);

        // 탐지 필터 설정
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(enemyLayer);
        filter.useLayerMask = true;
        filter.useTriggers = true;

        // 플레이어 주변의 적 탐지 (쿨타임과 상관없이 항상 탐지)
        int hitCount = Physics2D.OverlapCircle(player.transform.position, attackRadius, filter, _hitResults);

        // pending 리스트 초기화
        _pendingTargets.Clear();

        // VFX 재생 여부 플래그
        bool vfxPlayed = false;

        // 스킬 사용 가능하면 광역 스킬 적용 (한 번만 VFX 재생)
        if (data != null && Time.time >= nextAvailable)
        {
            // 스킬 사용 처리: 쿨다운 갱신, 매니저 호출
            _skillCooldowns[skillName] = Time.time + data.cooldown;
            skillManager?.ActivateSkill(skillName);

            // 탐지된 모든 적을 수집 (데미지 적용은 애니메이션 끝에서 일괄 적용)
            for (int i = 0; i < hitCount; i++)
            {
                // 각 적이 IDamageable인지 확인 후 pending 리스트에 추가
                if (_hitResults[i].TryGetComponent<IDamageable>(out var targetEnemy))
                {
                    // VFX는 스킬 사용 시 한 번만 재생
                    if (!vfxPlayed)
                    {
                        // VFX는 스킬 사용 시 한 번만 재생
                        VFXManager.Instance?.GetVFX(eVFXType.Wind_Lance, targetEnemy.targetPos, player.transform.rotation, (vfx) => { vfx?.ActiveEffect(200); });

                        // 플래그 설정하여 VFX가 이미 재생되었음을 표시
                        vfxPlayed = true;
                    }

                    // 데미지 대상에 추가 (실제 TakeDamage는 애니메이션 종료 시점에 실행)
                    _pendingTargets.Add(targetEnemy);
                }
            }

            // 애니메이션 재생 및 애니메이션 종료 시점에 데미지 적용
            player.PlayAttackAndApplyDamage(ApplyPendingTargets);
        }
        else
        {
            // 쿨타임 중이면 일반 공격만 수행 (첫 번째 적에만 적용)
            for (int i = 0; i < hitCount; i++)
            {
                // 첫 번째 적을 찾으면 루프 종료 (일반 공격은 한 명에게만 적용)
                if (_hitResults[i].TryGetComponent<IDamageable>(out var targetEnemy))
                {
                    // _pendingTargets 리스트에 첫 번째 적 추가
                    _pendingTargets.Add(targetEnemy);

                    break;
                }
            }

            // 일반 공격 애니메이션 재생 및 첫 번째 적에게 데미지 적용
            player.PlayAttackAndApplyDamage(ApplyPendingTargets);
        }
        return NodeState.Success;
    }

    // 애니메이션 종료 시점에 호출되는 메서드: pending 리스트에 있는 모든 대상에게 데미지 적용
    private void ApplyPendingTargets()
    {
        //Debug.Log("애니메이션 종료, pending 대상에게 데미지 적용 시도");

        if (_pendingTargets == null || _pendingTargets.Count == 0) return;

        for (int i = 0; i < _pendingTargets.Count; i++)
        {
            var target = _pendingTargets[i];

            if (target != null)
            {
                target.TakeDamage(player);

                if (target.TakeDamage(player)) Debug.Log("적 데미지 적용 성공");
            }
            else
            {
                Debug.Log("대상이 null입니다.");
            }
        }
        _pendingTargets.Clear();
    }

    public class AttackNode : Node
    {
        private PlayerAttack _attack;
        public AttackNode(PlayerAttack attack) { _attack = attack; }
        public override NodeState Evaluate() => _attack.Attack();
    }
}