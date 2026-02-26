using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Scripts.Core.inteface;
using Scripts.Core.Utils;

namespace Scripts.Monster.MonsterNode
{
    using Scripts.Core;
    using Scripts.Core.inteface;
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
            IDamageable target = _res[0].GetComponent<IDamageable>();
            _monster.SetTarget(target);
            _monster.ChangeMonsterAction(eMonsterAction.Walk);
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
