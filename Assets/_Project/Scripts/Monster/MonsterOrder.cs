using Scripts.Core.inteface;
using Scripts.Monster.MonsterNode;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Scripts.Monster
{
    public class MonsterOrder
    {
        private Node _rootNode;
        private Monster monster;
		bool _isAbort;
		public bool IsAbort
        {
            get { return _isAbort; }
        }
	    public void InterruptBT()
		{
			_isAbort = true;
		}
		public void RecoveryBT()
		{
			_isAbort = false;
		}
		public MonsterOrder()
        {
            return;
        }

        public void Init(Monster mon)
        {
            monster = mon;
            List<Node> nodes = new List<Node>();

            nodes.Add(new MonsterAttack(mon));
            nodes.Add(new MonsterDetect(mon));
            nodes.Add(new MonsterMove(mon));

            //_rootNode = new Sequence()
            _rootNode = new Selector(
                    nodes
                );

        }

        public void ExecuteNode()
        {
            if (_isAbort == true)
            {
                return;
            }
            NodeState state = _rootNode.Evaluate();
        }
    }
}

