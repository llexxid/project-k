using Cysharp.Threading.Tasks;
using Scripts.Core;
using Scripts.Core.inteface;
using Scripts.Monster;
using System;
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

    public PlayerAttack(Player player, PlayerDetection detection = null)
    {
        this.player = player;
        this._detection = detection;
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
        if (_pendingTargets == null || _pendingTargets.Count == 0)
        {
            Debug.Log("적이 없습니다. 데미지 적용 실패 - 공격 대상 초기화");
            return;
        }

        // 우선 사용 가능한 공격자 확보: Player가 IAttackable이면 사용, 아니면 attackable 필드 사용
        IAttackable attacker = player as IAttackable ?? attackable;

        if (attacker == null)
        {
            Debug.LogWarning("공격자(attacker) 참조가 없습니다. 데미지 적용을 건너뜁니다.");
            _pendingTargets.Clear();
            return;
        }


        for (int i = 0; i < _pendingTargets.Count; i++)
        {
            var target = _pendingTargets[i];

            if (target == null)
            {
                Debug.Log("대상이 null 또는 이미 파괴됨 - 다음 대상로 진행");
                continue;
            }
            else
            {
                Debug.Log("대상에게 데미지 적용 시도: " + target);
            }

            // TakeDamage: true = 살아있음, false = 사망 (Player.TakeDamage 구현에 따름)
            bool isAliveAfterHit = target.TakeDamage(player);

            if (isAliveAfterHit)
            {
                Debug.Log("적 데미지 적용 완료 (대상은 아직 살아있음)");
            }
            else
            {
                Debug.Log("Monster Is Dead!! → 공격 중단 및 Idle 전환");

                // pending 리스트 전체 초기화
                _pendingTargets.Clear();

                // 다음 공격 가능 시간을 최댓값으로 설정 → Attack()이 Failure를 반환하여 공격 완전 중단
                _nextAttackTime = float.MaxValue;

                // 플레이어 행동 트리를 Idle로 전환하여 대기 상태로 돌아감
                player?.TurnOnAnimation(ePlayerAction.Idle);

                // 플레이어와 Detection의 현재 타겟 초기화
                if (player != null)
                    player.currentTarget = null;
                if (_detection != null)
                    _detection.currentTarget = null;

                // 루프 종료
                break;
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