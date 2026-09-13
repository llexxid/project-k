using System;
using System.Collections.Generic;
using KingdomIdle.Balance;
using Scripts.Users;
using UnityEngine;

namespace Scripts.Wallets
{
    internal class Wallet
    {
        public static event Action<eCurrency, long> OnAnyChanged;
        private static readonly Dictionary<eCurrency, long> Published = new();
        private static bool _subscribed;
        public Wallet()
        {
            if (_subscribed) return;
            _subscribed = true; LocalProgression.Changed += Publish;
        }
        private static void Publish()
        {
            foreach (eCurrency currency in Enum.GetValues(typeof(eCurrency)))
            {
                long value = LocalProgression.Balance(currency);
                if (Published.TryGetValue(currency, out long previous) && previous == value) continue;
                Published[currency] = value;
                if (OnAnyChanged == null) continue;
                foreach (Action<eCurrency, long> observer in OnAnyChanged.GetInvocationList())
                    try { observer(currency, value); } catch (Exception ex) { Debug.LogException(ex); }
            }
        }
        public void SetOwner(User user, long gold, long ancient, long supply, long arcane, long fragments) { Publish(); }
        public int TotalCoins { get; set; }
        public void SetCoin(eCurrency currency, long amount)
        {
            // Only explicit development fixtures use this legacy setter. Remote responses are gated by their callers.
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            LocalProgression.Execute("set-wallet", s => { s.Wallet[currency] = amount; return true; });
        }
        public void AddCoins(eCurrency currency, long amount)
        {
            if (amount == 0) return;
            LocalProgression.Execute("wallet-delta", s => {
                if (amount < 0) return LocalProgression.Spend(s, currency, checked(-amount));
                LocalProgression.Credit(s, currency, amount); return true;
            });
        }
        public bool TryGetAmount(eCurrency currency, out long amount) { amount = LocalProgression.Balance(currency); return true; }
        public bool CanAfford(eCurrency currency, long amount) => amount >= 0 && LocalProgression.Balance(currency) >= amount;
        public bool TrySpendCoins(eCurrency currency, long amount) => amount >= 0 && LocalProgression.Execute("wallet-spend", s => LocalProgression.Spend(s, currency, amount));
    }
}
