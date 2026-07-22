using System;
using System.Reflection;
using UnityEngine;
using Scripts.Wallets;
using Scripts.Users;

namespace Scripts.Core
{
    // 재화(지갑) 연동용 브릿지
    public static class EconomyBridge
    {
        private static User _cachedUser;

        /// <summary>
        /// 재화가 변경될 때마다 (currency, 변경 후 잔액) 로 발생한다.
        /// UI 측에서 HUD 재화 라벨 등을 즉시 갱신하는 데 사용한다.
        /// 내부적으로 Wallet.OnAnyChanged 를 래핑해 노출한다.
        /// </summary>
        public static event Action<eCurrency, long> OnAmountChanged
        {
            add    { Wallet.OnAnyChanged += value; }
            remove { Wallet.OnAnyChanged -= value; }
        }

        public static bool TryGetAmount(eCurrency currency, out long amount)
        {
            amount = 0;
            var u = EnsureUser();
            if (u == null) return false;

            // User 내부 Wallet을 직접 찾기(필드 접근)
            var w = TryGetWallet(u);
            if (w == null) return false;
            return w.TryGetAmount(currency, out amount);
        }

        public static void Add(eCurrency currency, long amount)
        {
            if (amount == 0) return;
            var u = EnsureUser();
            if (u == null) return;

            // User 공개 메서드로 추가(내부에서 Wallet.AddCoins)
            u.GainCoin(currency, amount);
        }

        public static void AddGold(int amount) => Add(eCurrency.Gold, amount);
        public static void AddAncientCoin(int amount) => Add(eCurrency.AncientCoin, amount);

        private static User EnsureUser()
        {
            if (_cachedUser != null) return _cachedUser;

            var um = UserManager.Instance;
            if (um == null) return null;

            // UserManager._user (private) 접근
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var f = typeof(UserManager).GetField("_user", flags);
            if (f == null) return null;

            try
            {
                _cachedUser = f.GetValue(um) as User;
            }
            catch
            {
                _cachedUser = null;
            }

            return _cachedUser;
        }

        private static Wallet TryGetWallet(User u)
        {
            if (u == null) return null;

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var f = typeof(User).GetField("_wallet", flags);
            if (f == null) return null;

            try
            {
                return f.GetValue(u) as Wallet;
            }
            catch
            {
                return null;
            }
        }
    }
}