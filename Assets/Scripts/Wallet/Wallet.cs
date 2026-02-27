using Scripts.Users;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Scripts.Wallets
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
        private User _user;
        private Dictionary<eCurrency, int> pocket = new Dictionary<eCurrency, int>();

        [SerializeField] private int totalCoins;

        public Wallet(User user, int coins, int ancientCoins)
        {
            _user = user;
            AddCoins(eCurrency.Gold, coins);
            AddCoins(eCurrency.AncientCoin, ancientCoins);
        }

        public int TotalCoins
        {
            get { return totalCoins; }
            set { totalCoins = value; }
        }

        public void AddCoins(eCurrency type, int amount)
        {
            if (pocket.ContainsKey(type))
            {
                pocket[type] += amount;
            }
            else
            {
                pocket.Add(type, amount);
            }
        }

        public bool TryGetAmount(eCurrency type, out int amount)
        {
            return pocket.TryGetValue(type, out amount);
        }
    }
}