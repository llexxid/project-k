using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerDetection : MonoBehaviour, ISignal
{
    public eSignal signalType;

    public void Detect()
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
        Detect();
    }
}
