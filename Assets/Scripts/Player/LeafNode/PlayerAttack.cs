using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerAttack : MonoBehaviour, ISignal
{
    public eSignal signalType;

    public void Attack()
    {

        Signal();
    }

    public override void Signal()
    {

    }

    void Start()
    {
        
    }

    void Update()
    {
        Attack();
    }
}
