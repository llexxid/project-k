using Scripts.Core;
using WalletModel = Scripts.Wallets.Wallet;

namespace KingdomIdle.UGUI
{
    /// <summary>Reads the current user's wallet without scanning scene objects or invoking unrelated getters.</summary>
    public static class WalletLocator
    {
        public static object FindAnyWallet() => EconomyBridge.CurrentWallet;

        public static bool TryGetAmount(object walletObj, eCurrency currency, out long amount)
        {
            amount = 0;
            return walletObj is WalletModel wallet && wallet.TryGetAmount(currency, out amount);
        }
    }
}
