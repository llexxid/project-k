using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Coin : MonoBehaviour
{
    public eCurrency type = eCurrency.Gold;
    public int value = 100;

    public int Value
    {
        get { return value; }
        set { this.value = value; }
    }
}
