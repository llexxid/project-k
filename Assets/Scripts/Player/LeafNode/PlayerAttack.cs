using System.Collections.Generic;
using UnityEngine;

public class PlayerAttack : MonoBehaviour
{
    [SerializeField] private PlayerDetection _detection;
    public float attackRate = 0.5f;
    private float _nextAttackTime = 0f;
    public Animator animator;
    public SkillManager skillManager;

    // 광역 공격 설정을 위한 변수
    public float attackRadius = 3f;
    public LayerMask enemyLayer;
    private List<Collider2D> _hitResults = new List<Collider2D>();

    public NodeState Attack()
    {
        if (Time.time < _nextAttackTime) return NodeState.Failure;

        animator.SetBool("isAttack", true);
        _nextAttackTime = Time.time + attackRate;

        // OverlapCircle 리스트 오버로드 사용
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(enemyLayer);
        int hitCount = Physics2D.OverlapCircle(transform.position, attackRadius, filter, _hitResults);

        for (int i = 0; i < hitCount; i++)
        {
            // Attack 메서드 내 루프
            if (_hitResults[i].TryGetComponent<Enemy>(out Enemy enemy))
            {
                skillManager.ActivateSkill("Strong Slash", transform.position);
                enemy.TakeDamage(10); // 적의 hp를 직접 깎는 대신 메서드 호출
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