using Scripts.Core.inteface;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Scripts.Monster.MonsterNode 
{
    public class MonsterMove : MonsterNode
    {
        Vector3 centerPos;

        public MonsterMove(Monster mon) : base(mon)
        {

        }

        private void MoveToCharacter()
        {
            Transform monTrans = _monster.gameObject.transform;
            Core.inteface.IDamageable _target = _monster.Target;
            Vector3 targetPos = Vector3.zero;
            //주위에 target이 없는 경우
            if (_target != null)
            {
                //Todo : 카메라의 좌표로 변경
                targetPos = _target.targetPos;
            }
                
            Vector3 myPos = monTrans.position;
            Vector3 dir = (targetPos - myPos).normalized;

            monTrans.Translate(dir * _monster.GetSpeed() * Time.deltaTime);
        }

        public override NodeState Evaluate()
        {
            MoveToCharacter();
            return NodeState.Success;
        }   
        

        //화면 중앙으로 움직여야함.

    }

}

