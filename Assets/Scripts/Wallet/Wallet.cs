using Scripts.Users;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Wallet : MonoBehaviour
{
    private Player player;
    private Coin coin;
    private List<eCurrency> currencies;
    private Dictionary<eCurrency, int> wallet = new Dictionary<eCurrency, int>();

    [SerializeField]
    private int totalCoins;
    private User user;
    private int coin1;
    private int coin2;

    public Wallet(User user, int coin1, int coin2)
    {
        this.user = user;
        this.coin1 = coin1;
        this.coin2 = coin2;
    }

    public int TotalCoins
    {
        get { return totalCoins; }
        set { totalCoins = value; }
    }

    public void AddCoins(eCurrency type, int amount)
    {
        // 이미 있는 재화면 개수만 더하고, 처음이면 새로 추가
        if (wallet.ContainsKey(type))
        {
            wallet[type] += amount;
        }
        else
        {
            wallet.Add(type, amount);
        }

        /*
        foreach (var pair in wallet)
        {
            Debug.Log($"재화: {pair.Key}, 수량: {pair.Value}");
        }
        */
    }

    public bool TryGetAmount(eCurrency type, out int amount)
    {
        return wallet.TryGetValue(type, out amount);
    }

    void Start()
    {
        // Enum을 List로 변환
        var values = (eCurrency[])System.Enum.GetValues(typeof(eCurrency));
        currencies = new List<eCurrency>(values);
    }

    void Update()
    {

    }
}