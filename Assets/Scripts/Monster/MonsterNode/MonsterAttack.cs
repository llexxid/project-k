using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Scripts.Monster.MonsterNode
{
    public class MonsterAttack : MonsterNode
    {
        public MonsterAttack(Monster mon) : base(mon)
        {

        }

        private bool IsInAttackRange()
        {
            //타겟이 없으면 Fail!
            if (_monster.Target == null)
            {
                return false;
            }

            Vector3 targetPos = _monster.Target.targetPos;
            //공격 범위 체크
            float distance = Vector3.Distance(_monster.attackerPos, targetPos);
            if (distance < _monster.AttackRadius)
            {
                return false;
            }
            bool IsAlive;
            IsAlive = _monster.Target.TakeDamage(_monster);
            if (!IsAlive)
            {
                _monster.ResetTarget();
                return false;
            }
            return true;
        }
        // 공격범위에 있다면 공격을 한다.
        // 
        //Player가 공격 범위에 있는가
        public override NodeState Evaluate()
        {
            //Attack의 Success조건
            if (IsInAttackRange())
            {
                return NodeState.Success;
            }
            //NodeState.Running
            return NodeState.Failure;
        }
    }
}

