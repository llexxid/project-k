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
        // 실패한 전체 스캔의 부정 결과 스로틀 — 방치형 재화 틱마다 FindObjectsByType가
        // 반복 실행되는 것을 막는다. 지갑을 찾으면 호출측이 캐시하므로 이 경로는 더 안 탄다.
        private const float FailedScanCooldown = 1f;
        private static float _nextScanTime;

        public static object FindAnyWallet()
        {
            var gm = GameManager.Instance;
            var w = TryFindWalletModel(gm, 3);
            if (w != null) return w;

            // 직전 전체 스캔이 실패했으면 잠시 재스캔을 보류 (프레임/틱 폭주 방지)
            if (Time.unscaledTime < _nextScanTime) return null;
            _nextScanTime = Time.unscaledTime + FailedScanCooldown;

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
                if (!ShouldRecurse(v)) continue;

                var w = TryFindWalletModel(v, depth - 1);
                if (w != null) return w;
            }

            for (int i = 0; i < props.Length; i++)
            {
                var p = props[i];
                if (!p.CanRead) continue;
                if (p.GetIndexParameters().Length != 0) continue;
                // UnityEngine 타입 프로퍼티 getter는 호출하지 않는다.
                //   .material 등은 머티리얼 인스턴스를 생성하고, Material.color(TMP는 _Color 없음)는
                //   프레임마다 콘솔 경고를 폭주시킨다. 지갑은 순수 C# 객체라 이들을 건너뛰어도 무방.
                if (typeof(UnityEngine.Object).IsAssignableFrom(p.PropertyType)) continue;

                object v;
                try { v = p.GetValue(obj, null); } catch { continue; }
                if (!ShouldRecurse(v)) continue;

                var w = TryFindWalletModel(v, depth - 1);
                if (w != null) return w;
            }

            return null;
        }

        /// <summary>재귀 탐색 대상 판정 — null/문자열/값타입/UnityEngine 객체는 제외.</summary>
        private static bool ShouldRecurse(object v)
        {
            if (v == null) return false;
            if (v is string) return false;
            if (v.GetType().IsValueType) return false;
            // UnityEngine.Object(Material/Component/GameObject/Texture 등)로는 들어가지 않는다.
            // 지갑을 보유한 MonoBehaviour는 FindObjectsByType로 이미 개별 스캔되므로 손실 없음.
            if (v is UnityEngine.Object) return false;
            return true;
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
