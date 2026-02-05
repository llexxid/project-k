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

        // Enum을 List로 변환
        var values = (eCurrency[])System.Enum.GetValues(typeof(eCurrency));
        List<eCurrency> currencies = new List<eCurrency>(values);
    }


}
