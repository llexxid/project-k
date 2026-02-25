using Scripts.Core;
using Scripts.Core.inteface;
using Scripts.Monster;
using System.Collections.Generic;
using UnityEngine;

public class PlayerAttack
{
    [SerializeField] private PlayerDetection _detection;
    public float attackRate;
    private float _nextAttackTime = 1f;

    public SkillManager skillManager;
    public SkillDatabase skillDatabase; // 스킬 데이터 참조용
    public VFXManager vfxManager;
    public IAttackable attackable;

    public float attackRadius = 3f;
    private List<Collider2D> _hitResults = new List<Collider2D>();
    private Dictionary<string, float> _skillCooldowns = new Dictionary<string, float>();

    [SerializeField]
    public Player player;

    int enemyLayer = 1 << 6;

    public PlayerAttack(Player player)
    {
        this.player = player;
        this.skillDatabase = player.skillDatabase; // 삭제 예정
        this.skillManager = player.skillManager; // 삭제 예정
        _skillCooldowns.Add("Wind_Lance", 1f);
    }

    public NodeState Attack()
    {
        // 스킬 쿨타임 체크 ("WindLance")
        string skillName = "Wind_Lance";
        SkillData data;
        data = skillDatabase.GetSkill(skillName);
        skillManager.ActivateSkill(skillName);

        // Debug.Log("Attack");

        // 1. 일반 공격 쿨타임 체크
        _nextAttackTime = Time.time + attackRate;

        // 2. 공격 범위 내 적 탐지
        ContactFilter2D filter = new ContactFilter2D();

        // 레이어 마스크 설정 (적 레이어)
        filter.SetLayerMask(enemyLayer);

        // 트리거 콜라이더 포함 여부 설정
        filter.useLayerMask = true;
        filter.useTriggers = true;

        // 리스트를 재사용하여 가비지 발생을 최소화하는 방식
        int hitCount = Physics2D.OverlapCircle(player.transform.position, attackRadius, filter, _hitResults);

        // 쿨타임 갱신
        _skillCooldowns[skillName] = Time.time + data.cooldown;

        for (int i = 0; i < hitCount; i++)
        {
            if (_hitResults[i].TryGetComponent<IDamageable>(out var targetEnemy))
            {
                // Debug.Log(targetEnemy);

                if (_skillCooldowns[skillName] >= data.cooldown)
                {
                    // Debug.Log("123");

                    // VFX 및 쿨타임 갱신
                    VFXManager.Instance.GetVFX(eVFXType.Wind_Lance, targetEnemy.targetPos, player.transform.rotation, (vfx) => { vfx.ActiveEffect(200); });

                    // Debug.Log(data);

                    // 3. 공격 적용
                    targetEnemy.TakeDamage(attackable);
                }
            }
        }
        return NodeState.Success;
    }

    public class AttackNode : Node
    {
        private PlayerAttack _attack;
        public AttackNode(PlayerAttack attack) { _attack = attack; }
        public override NodeState Evaluate() => _attack.Attack();
    }
}