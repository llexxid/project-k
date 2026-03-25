using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Scripts.Core.inteface;
using Scripts.Core.Utils;

namespace Scripts.Monster.MonsterNode
{
    using Scripts.Core;
    using Scripts.Core.inteface;
	using Scripts.Monster.State;

	public class MonsterDetect : MonsterNode
    {
        private List<Collider2D> _res;
        private int playerLayer;
        public MonsterDetect(Monster mon) : base(mon)
        {

        }

        private bool DetectChracter()
        {
            if (_monster.Target != null)
            {
                CustomLogger.Log($"탐색을 이미 끝냈음!\n");
                return false;
            }

            float radius = _monster.DectectRadius;
            ContactFilter2D filter = new ContactFilter2D();
            //LayerMask 
            //PlayerMask
            playerLayer = 1 << 7;
            filter.SetLayerMask(playerLayer);
            filter.useTriggers = true;

            _res = new List<Collider2D>();
            int around = Physics2D.OverlapCircle(_monster.attackerPos, radius, filter, _res);
			if (around == 0)
			{
				CustomLogger.Log($"탐색범위 밖인 경우");
				return false;
			}

            bool IsDectedDeadPlayer = DetectedDeadPlayer(_res);
			if (IsDectedDeadPlayer)
            {
                return false;
            }

			return true;
        }

        private bool DetectedDeadPlayer(List<Collider2D> coliders)
        {
			//탐색한 대상 중, 하나라도 살아있다면 ㄱㅊ.
            //근데 죽었다면?
			foreach (Collider2D detected in coliders)
			{
				Player p = detected.GetComponent<Player>();
				if (p.CurrentAction != ePlayerAction.Dead)
				{
					_monster.SetTarget(p);
					_monster.ChangeState(new MonsterMoveState(_monster));
                    return false;
				}
			}
            return true;
		}

        public override NodeState Evaluate()
        {
            if (DetectChracter())
            {
                CustomLogger.Log("Dectect Enemy!");
                return NodeState.Success;
            }
            else
            {
                return NodeState.Failure;
            }
        }
    }
}
