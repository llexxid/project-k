using System.Collections;
using System.Collections.Generic;
using Scripts.Core;
using UnityEngine;

namespace Scripts.Monster.MonsterNode
{
    using Scripts.Core.inteface;
	using Scripts.Core.Utils;
	using Scripts.Monster.State;

	public class MonsterMove : MonsterNode
    {
        Vector3 centerPos;

        public MonsterMove(Monster mon) : base(mon)
        {
           
        }

        private void MoveToCharacter()
        {
            Transform monTrans = _monster.gameObject.transform;
            IDamageable _target = _monster.Target;
            Vector3 targetPos = Vector3.zero;
            //주위에 target이 없는 경우
            if (_target != null)
            {
                //Todo : 카메라의 좌표로 변경
                targetPos = _target.targetPos;
                
            }

            Vector3 myPos = monTrans.position;
            Vector3 dir = (targetPos - myPos).normalized;

            _monster.ChangeState(new MonsterMoveState(_monster));
            _monster.SetFlip(dir.x);
            CustomLogger.Log($"몬스터가 {_monster.GetSpeed()} 속도로 0,0,0을 향해 움직임");
            //rb는 기본적으로 월드좌표 기준.
            //Translate는 로컬 좌표를 기준으로 움직임.
            //Rotate를 하면, 로컬 좌표계도 뒤집히므로, Translate할 때, 내가 바라보는 방향 반대로 가게됨.
            // RB를 쓰거나, Space.World를 하거나, 이미지만을 뒤집어주거나.
            // 횡스크롤인 경우에는, 좌/우 밖에 없기 때문에 facing Dir을 곱해줘도 됨.
            monTrans.Translate(dir * (float)_monster.GetSpeed() * Time.deltaTime, Space.World);
        }

        //기본 
        public override NodeState Evaluate()
        {
            MoveToCharacter();
            return NodeState.Running;
        }
    }
}
