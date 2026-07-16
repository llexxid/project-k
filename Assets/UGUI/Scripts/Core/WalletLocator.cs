using System;
using System.Reflection;
using UnityEngine;
using Scripts.Core;
using WalletModel = Scripts.Wallets.Wallet;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// 지갑(Wallet) 인스턴스를 리플렉션으로 탐색하고 재화 수량을 읽는 유틸리티.
    /// UITKUIManager.FindAnyWallet / TryFindWalletModel / TryGetAmountFromWallet 이식.
    /// </summary>
    public static class WalletLocator
    {
        public static object FindAnyWallet()
        {
            var gm = GameManager.Instance;
            var w = TryFindWalletModel(gm, 3);
            if (w != null) return w;

            var behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < behaviours.Length; i++)
            {
                var b = behaviours[i];
                w = TryFindWalletModel(b, 2);
                if (w != null) return w;
            }

            for (int i = 0; i < behaviours.Length; i++)
            {
                var b = behaviours[i];
                if (b == null) continue;
                if (IsWalletLikeProvider(b.GetType()))
                    return b;
            }

            return null;
        }

        private static WalletModel TryFindWalletModel(object obj, int depth)
        {
            if (obj == null) return null;
            if (obj is WalletModel w0) return w0;
            if (depth <= 0) return null;

            var t = obj.GetType();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            var fields = t.GetFields(flags);
            for (int i = 0; i < fields.Length; i++)
            {
                var f = fields[i];
                if (!typeof(WalletModel).IsAssignableFrom(f.FieldType)) continue;
                try
                {
                    var v = f.GetValue(obj) as WalletModel;
                    if (v != null) return v;
                }
                catch { }
            }

            var props = t.GetProperties(flags);
            for (int i = 0; i < props.Length; i++)
            {
                var p = props[i];
                if (!p.CanRead) continue;
                if (p.GetIndexParameters().Length != 0) continue;
                if (!typeof(WalletModel).IsAssignableFrom(p.PropertyType)) continue;

                try
                {
                    var v = p.GetValue(obj, null) as WalletModel;
                    if (v != null) return v;
                }
                catch { }
            }

            for (int i = 0; i < fields.Length; i++)
            {
                var f = fields[i];
                object v;
                try { v = f.GetValue(obj); } catch { continue; }
                if (v == null) continue;
                if (v is string) continue;
                if (v.GetType().IsValueType) continue;

                var w = TryFindWalletModel(v, depth - 1);
                if (w != null) return w;
            }

            for (int i = 0; i < props.Length; i++)
            {
                var p = props[i];
                if (!p.CanRead) continue;
                if (p.GetIndexParameters().Length != 0) continue;

                object v;
                try { v = p.GetValue(obj, null); } catch { continue; }
                if (v == null) continue;
                if (v is string) continue;
                if (v.GetType().IsValueType) continue;

                var w = TryFindWalletModel(v, depth - 1);
                if (w != null) return w;
            }

            return null;
        }

        private static bool IsWalletLikeProvider(Type t)
        {
            if (t == null) return false;
            if (t.Name.IndexOf("wallet", StringComparison.OrdinalIgnoreCase) < 0) return false;

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var m = t.GetMethod(
                "TryGetAmount",
                flags,
                null,
                new[] { typeof(eCurrency), typeof(int).MakeByRefType() },
                null
            );
            return m != null && m.ReturnType == typeof(bool);
        }

        public static bool TryGetAmount(object walletObj, eCurrency currency, out long amount)
        {
            amount = 0;
            if (walletObj == null) return false;

            if (walletObj is WalletModel w)
                return w.TryGetAmount(currency, out amount);

            var t = walletObj.GetType();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var m = t.GetMethod(
                "TryGetAmount",
                flags,
                null,
                new[] { typeof(eCurrency), typeof(int).MakeByRefType() },
                null
            );

            if (m == null || m.ReturnType != typeof(bool)) return false;

            object[] args = new object[] { currency, 0 };
            try
            {
                var ok = (bool)m.Invoke(walletObj, args);
                amount = (int)args[1];
                return ok;
            }
            catch
            {
                return false;
            }
        }
    }
}
