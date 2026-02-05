using System.Collections;
using System.Collections.Generic;
using Scripts.Core.inteface;
using Scripts.Core;
using UnityEngine;

public class Monster : MonoBehaviour, IPoolable
{
#if UNITY_EDITOR
    public bool IsActive { get; set; }
#endif
    public void OnAlloc()
    {
        return;
    }

    public void OnRelease()
    {
        return;
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
