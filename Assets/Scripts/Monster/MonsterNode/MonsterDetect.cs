using Scripts.Core.inteface;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Scripts.Monster.MonsterNode
{

    public class MonsterDetect : MonsterNode
    {
        private List<Collider2D> _res;
        const int PlayerLayer = 0x00000040;
        
        public MonsterDetect(Monster mon) : base(mon)
        {

        }

        // 탐지에 성공하면 공격할 수 있는지 봐야함. 
        // 이렇게 되면 공격 -> 탐지 -> 이동.
        // 만약 탐지에 성공했다? success
        // 그리고, 몬스터의  방향으로 움직여야함.
        private bool DetectChracter()
        {
            //몬스터가 한번 Target이 됐다면 해당 몬스터를 향해 이동.
            if (_monster.Target != null)
            {
                return false;
            }

            float radius = _monster.DectectRadius;
            ContactFilter2D filter = new ContactFilter2D();
            //LayerMask 
            //PlayerMask

            filter.SetLayerMask(PlayerLayer);
            filter.useTriggers = true;
            _res = new List<Collider2D>();
            int around = Physics2D.OverlapCircle(_monster.attackerPos, radius, filter, _res);
            if (around == 0)
            {
                return false;
            }
            IDamageable target = _res[0].GetComponent<IDamageable>();
            _monster.SetTarget(target);
            return true;
        }

        public override NodeState Evaluate()
        {
            if (DetectChracter())
            {
                return NodeState.Success;
            }
            else
            {
                return NodeState.Failure;
            }
        }
    }
}
