using Scripts.Core;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Scripts.Core.Utils;
public class MonsterSpawnerTest : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        Vector3 pos1 = new Vector3(-5, 0, 0);
        Vector3 pos2 = new Vector3(5, 0, 0);

        MonsterSpawner.Instance.SpawnMonsterForTest(eMonsterType.MON_ORC, pos1, Quaternion.identity,
            (mon) =>
            {
                Debug.Log("MonsterSpawn!");
            }

            );
    }

    // Update is called once per frame
    void Update()
    {

    }
}
