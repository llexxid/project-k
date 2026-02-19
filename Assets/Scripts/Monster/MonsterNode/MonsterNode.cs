using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Scripts.Monster;


namespace Scripts.Monster.MonsterNode
{
    public class MonsterNode : Node
    {
        protected Monster _monster;
        public MonsterNode(Monster mon)
        {
            _monster = mon;
        }
        public override NodeState Evaluate()
        {
            return NodeState.Success;
        }
    }
}

