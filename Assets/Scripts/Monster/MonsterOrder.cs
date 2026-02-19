using Scripts.Monster.MonsterNode;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Scripts.Monster
{
    public class MonsterOrder : MonoBehaviour
    {
        private Node _rootNode;
        [SerializeField]
        Monster monster;
        public void Init()
        {

            List<Node> nodes = new List<Node>();

            nodes.Add(new MonsterAttack(monster));
            nodes.Add(new MonsterDetect(monster));
            nodes.Add(new MonsterMove(monster));

            _rootNode = new Selector(
                    nodes
                );
        }
    }
}

