using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMove : MonoBehaviour, ISignal
{
    public eSignal signalType;

    public void Move()
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
        Move();  
    }
}
