using Scripts.Core;
using Scripts.Monster;
using System.Collections;
using System.Collections.Generic;
using UnityEditor.Build.Pipeline;
using UnityEngine;

public class MonsterOrderPool
{
    private static MonsterOrderPool _Instance;
    public static MonsterOrderPool Instance
    {
        get 
        {
            if (_Instance == null)
            {
                _Instance = new MonsterOrderPool();
                _Instance.Init();
            }
            return _Instance;
        }
    }

    private PureObjectPool<MonsterOrder> _Pool;
    
    private void Init()
    {
        _Pool = new PureObjectPool<MonsterOrder>();
        _Pool.Init(64, () => { return new MonsterOrder(); });
    }

    public MonsterOrder GetMonsterOrder()
    {
        return _Pool.Alloc();
    }
    public void ReleaseMonsterOrder(MonsterOrder order)
    {
        _Pool.Release(order);
    }
}
