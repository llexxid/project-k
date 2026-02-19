using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Scripts.Monster.MonsterNode
{
    public class MonsterAttack : Node
    {
        public override NodeState Evaluate()
        {
            //Attack¿« Success¡∂∞«

            //NodeState.Running
            return NodeState.Failure;
        }
    }
}

