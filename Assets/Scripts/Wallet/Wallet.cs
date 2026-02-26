using Scripts.Users;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Scripts.Wallets
{
    public class Wallet
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