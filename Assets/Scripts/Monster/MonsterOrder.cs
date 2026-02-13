using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MonsterOrder
{
    private Node _rootNode;

    public void Init()
    {
        // Sequence와 Selector를 조합해서 어떻게 Monster들이 동작하는지 추적
        Sequence MonsterSeq = new Sequence(
            {
            
        }
            );


        _rootNode = new Sequence()
    }
}
