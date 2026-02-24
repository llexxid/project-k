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
        // Debug.Log("Attack");
        // 1. 일반 공격 쿨타임 체크

        //animator.SetBool("isAttack", true);
        _nextAttackTime = Time.time + attackRate;

        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(enemyLayer);
        filter.useLayerMask = true;
        filter.useTriggers = true;
        int hitCount = Physics2D.OverlapCircle(player.transform.position, attackRadius, filter, _hitResults);

        for (int i = 0; i < hitCount; i++)
        {
            if (_hitResults[i].TryGetComponent<IDamageable>(out var targetEnemy))
            {
                // 2. 스킬 쿨타임 체크 ("WindLance")
                string skillName = "Wind_Lance";
                
                    SkillData data = skillDatabase.GetSkill(skillName);
                    skillManager.ActivateSkill(skillName, player.transform.position);

                    //Debug.Log(targetEnemy);

                    // VFX 및 쿨타임 갱신
                    VFXManager.Instance.GetVFX(eVFXType.Wind_Lance, targetEnemy.targetPos, player.transform.rotation, (vfx) => { vfx.ActiveEffect(200);});

                    Debug.Log(data);

                    _skillCooldowns[skillName] = Time.time + data.cooldown;
                //  
                targetEnemy.TakeDamage(attackable);
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