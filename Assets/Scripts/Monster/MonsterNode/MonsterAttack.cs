using Scripts.Core;
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

        private NodeState IsInAttackRange()
        {
            //타겟이 없으면 Fail!
            if (_monster.Target == null)
            {
                return NodeState.Failure;
            }

            Vector3 targetPos = _monster.Target.targetPos;
            //공격 범위 체크
            float distance = Vector3.Distance(_monster.attackerPos, targetPos);
            if (distance > _monster.AttackRadius)
            {
                return NodeState.Failure;
            }
            
            //공격범위 안에 있고, Target이 여전히 있다.
            bool IsAlive;
            IsAlive = _monster.Target.TakeDamage(_monster);
            if (!IsAlive)
            {
                //상대방을 죽였다면, 다음.
                _monster.ChangeMonsterAction(eMonsterAction.Idle);
                _monster.ResetTarget();
                return NodeState.Success;
            }
            //아니라면 전투지속
            _monster.ChangeMonsterAction(eMonsterAction.Attack);
            return NodeState.Running;
        }

        // 공격범위에 있다면 공격을 한다.
        // 
        //Player가 공격 범위에 있는가
        public override NodeState Evaluate()
        {
            return IsInAttackRange();
        }
    }
}

