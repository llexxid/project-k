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

        // Ž���� �����ϸ� ������ �� �ִ��� ������. 
        // �̷��� �Ǹ� ���� -> Ž�� -> �̵�.
        // ���� Ž���� �����ߴ�? success
        // �׸���, ������  �������� ����������.
        private bool DetectChracter()
        {
            //���Ͱ� �ѹ� Target�� �ƴٸ� �ش� ���͸� ���� �̵�.
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
