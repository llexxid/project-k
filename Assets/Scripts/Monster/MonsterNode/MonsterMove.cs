using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Scripts.Monster.MonsterNode 
{
    public class MonsterMove : Node
    {
        public override NodeState Evaluate()
        {
            throw new System.NotImplementedException();
        }

        // Start is called before the first frame update
        void Start()
        {
            Transform monTrans = _monster.gameObject.transform;
            Core.inteface.IDamageable _target = _monster.Target;
            Vector3 targetPos = Vector3.zero;
            //������ target�� ���� ���
            if (_target != null)
            {
                //Todo : ī�޶��� ��ǥ�� ����
                targetPos = _target.targetPos;
            }
                
            Vector3 myPos = monTrans.position;
            Vector3 dir = (targetPos - myPos).normalized;

        }

        // Update is called once per frame
        void Update()
        {

        }
    }

}

