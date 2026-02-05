using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Coin : MonoBehaviour
{
    [SerializeField]
    private int value = 100;

    public int Value
    {
        get { return value; }
        set { this.value = value; }
    }

    void Start()
    {

    }

    void Update()
    {

    }
}
