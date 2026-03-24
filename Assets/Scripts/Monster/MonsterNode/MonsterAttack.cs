using Scripts.Core;
using Scripts.Core.Utils;
using Scripts.Monster.State;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Scripts.Monster.MonsterNode
{
    public class MonsterAttack : MonsterNode
    {
        float attackLatency;
        public MonsterAttack(Monster mon) : base(mon)
        {
            attackLatency = mon.GetAnimationLength(eMonsterAction.Attack);
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

            AttackProcess();
			return NodeState.Success;
		}
        private void AttackProcess()
        {
			//CustomLogger.Log($"_monster.LastAttackTime + attackLatency : {_monster.LastAttackTime + attackLatency} | Time : {Time.time}");
			if ((_monster.LastAttackTime + attackLatency) < Time.time)
            {
                //CustomLogger.Log("공격 성공 했음");
                bool IsAlive = _monster.Attack(_monster.Target);
				_monster.ChangeState(new MonsterAttackState(_monster));
            }
		}
        // 공격범위에 있다면 공격을 한다.
        //Player가 공격 범위에 있는가
        public override NodeState Evaluate()
        {
            return IsInAttackRange();
        }
    }
}

