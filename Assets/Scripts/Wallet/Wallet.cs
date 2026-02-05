using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Wallet : MonoBehaviour
{
    private Player player;
    private Coin coin;

    [SerializeField]
    private int totalCoins;

    void Start()
    {
        player = new Player();
        coin = new Coin();
    }

    

    void Update()
    {
        
    }
}
