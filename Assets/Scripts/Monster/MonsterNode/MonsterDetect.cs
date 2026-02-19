using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Scripts.Monster.MonsterNode
{
    public class MonsterDetect : Node
    {
        private Monster _monster;
        public MonsterDetect(Monster mon)
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
            Core.inteface.IDamageable target = _res[0].GetComponent<Core.inteface.IDamageable>();
            _monster.SetTarget(target);
            return true;
        }

        public override NodeState Evaluate()
        {
            if () ;
        }
    }
}
