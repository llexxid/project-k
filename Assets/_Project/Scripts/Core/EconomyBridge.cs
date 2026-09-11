using System;
using Scripts.Wallets;

namespace Scripts.Core
{
    public static class EconomyBridge
    {
        // Resolve the active account each time; a scene/account change must not retain an old wallet.
        internal static Wallet CurrentWallet => UserManager.Instance != null ? UserManager.Instance.CurrentUser?.Wallet : null;

        public static event Action<eCurrency, long> OnAmountChanged
        {
            add { Wallet.OnAnyChanged += value; }
            remove { Wallet.OnAnyChanged -= value; }
        }

        public static bool TryGetAmount(eCurrency currency, out long amount)
        {
            amount = 0;
            var wallet = CurrentWallet;
            return wallet != null && wallet.TryGetAmount(currency, out amount);
        }

        public static void Add(eCurrency currency, long amount)
        {
            if (amount == 0) return;
            CurrentWallet?.AddCoins(currency, amount);
        }

        public static void AddGold(int amount) => Add(eCurrency.Gold, amount);
        public static void AddAncientCoin(int amount) => Add(eCurrency.AncientCoin, amount);
    }
}
