#if UNITY_EDITOR || (LOBBY_DEVICE_QA && DEVELOPMENT_BUILD)
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace KingdomIdle.UGUI
{
    /// <summary>Boundary and persistence contract checks, also callable in the isolated device QA build.</summary>
    public static class SettingsAcceptance
    {
        public static Dictionary<string, object> Run()
        {
            var passed = new List<string>();
            void Check(string name, bool ok) { if (!ok) throw new InvalidOperationException(name); passed.Add(name); }
            void Number(decimal value, string standard, string korean, string scientific)
            {
                string[] expected = { standard, korean, scientific };
                for (int i = 0; i < 3; i++) Check($"{value} / {(NumberStyle)i}", NumberNotation.Format(value, (NumberStyle)i) == expected[i]);
            }
            Number(0, "0", "0", "0"); Number(100, "100", "100", "100");
            Number(999, "999", "999", "999"); Number(1000, "1K", "1,000", "1e3");
            Number(9999, "9.99K", "9,999", "9.99e3"); Number(10000, "10K", "1만", "1e4");
            Number(12345, "12.34K", "1.23만", "1.23e4");
            Number(999999, "999.9K", "99.99만", "9.99e5");
            Number(1000000, "1M", "100만", "1e6");
            Number(99999999, "99.99M", "9,999만", "9.99e7");
            Number(100000000, "100M", "1억", "1e8");
            Number(1234567890, "1.23B", "12.34억", "1.23e9");
            Number(1000000000000, "1T", "1조", "1e12");
            Number(long.MaxValue, "9.22Qi", "922.3경", "9.22e18");
            Number(long.MinValue, "-9.22Qi", "-922.3경", "-9.22e18");
            Number(ulong.MaxValue, "18.44Qi", "1,844경", "1.84e19");
            Number(decimal.MaxValue, "79.22Oc", "7.92양", "7.92e28");
            Number(decimal.MinValue, "-79.22Oc", "-7.92양", "-7.92e28");
            Number(-.001m, "0", "0", "0"); Number(1.259m, "1.25", "1.25", "1.25");
            Check("NaN is not visible as a number", NumberNotation.Format(double.NaN) == "—");
            Check("Infinity is not visible as a number", NumberNotation.Format(double.PositiveInfinity) == "—");
            Check("Exact Int64 does not lose precision", NumberNotation.Exact(long.MaxValue) == "9,223,372,036,854,775,807");
            Check("Large float fallback does not overflow", NumberNotation.Format((double)decimal.MaxValue).Contains("e"));
            var culture = CultureInfo.CurrentCulture;
            try { CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR"); Check("Device locale does not change separators", NumberNotation.Format(12345678, NumberStyle.Standard) == "12.34M"); }
            finally { CultureInfo.CurrentCulture = culture; }
            var buffer = new char[48];
            NumberNotation.Write(ulong.MaxValue, NumberStyle.Standard, buffer);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 10000; i++) NumberNotation.Write(ulong.MaxValue, (NumberStyle)(i % 3), buffer);
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Check("10,000 damage formats allocate no managed bytes", bytes == 0);
            bool had = PlayerPrefs.HasKey(NumberNotation.PrefKey);
            int saved = PlayerPrefs.GetInt(NumberNotation.PrefKey);
            var oldStyle = NumberNotation.Style; int notifications = 0;
            Action changed = () => notifications++;
            try
            {
                PlayerPrefs.DeleteKey(NumberNotation.PrefKey); NumberNotation.Load();
                Check("Missing preference selects standard", NumberNotation.Style == NumberStyle.Standard);
                NumberNotation.Changed += changed;
                NumberNotation.SetStyle(NumberStyle.Korean); NumberNotation.SetStyle(NumberStyle.Korean);
                Check("Repeated selection is idempotent", notifications == 1);
                NumberNotation.Load(); Check("Saved selection survives reload", NumberNotation.Style == NumberStyle.Korean);
                PlayerPrefs.SetInt(NumberNotation.PrefKey, 999); NumberNotation.Load();
                Check("Unknown preference safely falls back", NumberNotation.Style == NumberStyle.Standard);
            }
            finally
            {
                NumberNotation.Changed -= changed; NumberNotation.SetStyle(oldStyle, false);
                if (had) PlayerPrefs.SetInt(NumberNotation.PrefKey, saved); else PlayerPrefs.DeleteKey(NumberNotation.PrefKey);
                PlayerPrefs.Save();
            }
            return new Dictionary<string, object> { ["passed"] = passed.Count, ["checks"] = passed, ["damageFormattingBytes10000"] = bytes };
        }
    }
}

#endif
