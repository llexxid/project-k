using System.Collections;
using System.Collections.Generic;
using Scripts.Core;
using UnityEngine;

public class ObjectPoolTest : MonoBehaviour
{
    [SerializeField]
    Monster _prefab;
    [SerializeField]
    Transform parent;
    ObjectPool<Monster> monsterPool;

    Monster mon;
    private void Awake()
    {
        monsterPool = new ObjectPool<Monster>();
        monsterPool.Init(50, parent ,_prefab);
    }

    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            mon = monsterPool.Alloc(Vector3.zero, Quaternion.identity);
            mon.gameObject.SetActive(true);

        }
        if (Input.GetKeyDown(KeyCode.R))
        {
            monsterPool.Release(mon);
        }

        if (Input.GetKeyDown(KeyCode.E))
        {
            monsterPool.Release(null);
        }
    }
}
