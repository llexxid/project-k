using Scripts.Core;
using System.Collections.Generic;
using UnityEngine;

public class PlayerAttack : MonoBehaviour
{
    [SerializeField] private PlayerDetection _detection;
    public float attackRate;
    private float _nextAttackTime = 1f;
    public Animator animator;
    public SkillManager skillManager;
    public SkillDatabase skillDatabase; // 스킬 데이터 참조용
    public VFXManager vfxManager;

    public float attackRadius = 3f;
    public LayerMask enemyLayer;
    private List<Collider2D> _hitResults = new List<Collider2D>();
    private Dictionary<string, float> _skillCooldowns = new Dictionary<string, float>();

    private void Start()
    {
        enemyLayer = LayerMask.GetMask("Enemy");
    }

    public NodeState Attack()
    {
        // 1. 일반 공격 쿨타임 체크
        //if (Time.time < _nextAttackTime) return NodeState.Failure;

        animator.SetBool("isAttack", true);
        _nextAttackTime = Time.time + attackRate;

        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(enemyLayer);
        filter.useLayerMask = true;
        filter.useTriggers = true;
        int hitCount = Physics2D.OverlapCircle(transform.position, attackRadius, filter, _hitResults);

        for (int i = 0; i < hitCount; i++)
        {
            if (_hitResults[i].TryGetComponent<Enemy>(out var targetEnemy))
            {
                // 2. 스킬 쿨타임 체크 ("WindLance")
                string skillName = "Wind_Lance";
                if (!_skillCooldowns.ContainsKey(skillName) || Time.time >= _skillCooldowns[skillName])
                {
                    SkillData data = skillDatabase.GetSkill(skillName);
                    skillManager.ActivateSkill(skillName, transform.position);

                    // VFX 및 쿨타임 갱신
                    vfxManager.GetVFX(eVFXType.Wind_Lance, targetEnemy.transform.position, transform.rotation, (vfx) => { vfx.ActiveEffect(250); });
                    _skillCooldowns[skillName] = Time.time + data.cooldown;
                }

                targetEnemy.TakeDamage(50);
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