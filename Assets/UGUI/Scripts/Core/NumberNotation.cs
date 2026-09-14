using System;
using System.Globalization;
using UnityEngine;

namespace KingdomIdle.UGUI
{
    public enum NumberStyle { Standard = 0, Korean = 1, Scientific = 2 }

    /// <summary>Display only. Never use abbreviated text to compare, spend, save or calculate rewards.</summary>
    public static class NumberNotation
    {
        public const string PrefKey = "settings_numberStyle";
        static readonly string[] StandardUnits = { "", "K", "M", "B", "T", "Qa", "Qi", "Sx", "Sp", "Oc" };
        static readonly string[] KoreanUnits = { "", "만", "억", "조", "경", "해", "자", "양" };
        [ThreadStatic] static char[] _buffer;
        public static NumberStyle Style { get; private set; }
        public static int Revision { get; private set; }
        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { Style = NumberStyle.Standard; Revision = 0; Changed = null; }

        public static void Load()
        {
            int saved = PlayerPrefs.GetInt(PrefKey, 0);
            SetStyle(saved >= 0 && saved <= 2 ? (NumberStyle)saved : NumberStyle.Standard, false);
        }

        public static void SetStyle(NumberStyle style, bool save = true)
        {
            if (style < NumberStyle.Standard || style > NumberStyle.Scientific) style = NumberStyle.Standard;
            if (save) { PlayerPrefs.SetInt(PrefKey, (int)style); PlayerPrefs.Save(); }
            if (Style == style) return;
            Style = style; Revision++; Changed?.Invoke();
        }

        public static string Format(long value) => Format((decimal)value);
        public static string Format(ulong value) => Format((decimal)value);
        public static string Format(int value) => Format((decimal)value);
        public static string Format(float value) => Format((double)value);
        public static string Format(double value) => double.IsNaN(value) || double.IsInfinity(value) ? "—" :
            value >= (double)decimal.MaxValue || value <= (double)decimal.MinValue ? value.ToString("0.##e0", CultureInfo.InvariantCulture) : Format((decimal)value);
        public static string Format(decimal value) => Format(value, Style);
        public static string Format(decimal value, NumberStyle style)
        {
            _buffer ??= new char[48];
            int length = Write(value, style, _buffer);
            return new string(_buffer, 0, length);
        }

        public static string Exact(long value) => value.ToString("N0", CultureInfo.InvariantCulture);

        /// <summary>No per-hit allocation; caller owns a buffer of at least 48 characters. Truncates toward zero,
        /// so 999,999 never falsely appears as an affordable 1M. Whole-number groups stay within their tier.</summary>
        public static int Write(decimal value, NumberStyle style, char[] buffer)
        {
            if (buffer == null || buffer.Length < 48) throw new ArgumentException("A 48-character buffer is required.");
            if (value > -.01m && value < .01m) { buffer[0] = '0'; return 1; }
            int n = 0;
            if (value < 0) { buffer[n++] = '-'; value = -value; }
            decimal scaled = value;
            string suffix = "";
            int exponent = 0;
            if (style == NumberStyle.Scientific && value >= 1000)
            {
                while (scaled >= 10) { scaled /= 10; exponent++; }
            }
            else
            {
                var units = style == NumberStyle.Korean ? KoreanUnits : StandardUnits;
                decimal radix = style == NumberStyle.Korean ? 10000 : 1000;
                int tier = 0;
                while (scaled >= radix && tier < units.Length - 1) { scaled /= radix; tier++; }
                suffix = units[tier];
            }
            int places = scaled >= 1000 ? 0 : scaled >= 100 && (suffix.Length > 0 || exponent > 0) ? 1 : 2;
            int scale = places == 0 ? 1 : places == 1 ? 10 : 100;
            long unitsValue = (long)decimal.Floor(scaled * scale);
            long whole = unitsValue / scale;
            int start = n, digits = 0;
            do
            {
                if (digits > 0 && digits % 3 == 0) buffer[n++] = ',';
                buffer[n++] = (char)('0' + whole % 10); whole /= 10; digits++;
            } while (whole > 0);
            Array.Reverse(buffer, start, n - start);
            int fraction = (int)(unitsValue % scale);
            if (fraction != 0)
            {
                buffer[n++] = '.';
                if (places == 2) { buffer[n++] = (char)('0' + fraction / 10); if (fraction % 10 != 0) buffer[n++] = (char)('0' + fraction % 10); }
                else buffer[n++] = (char)('0' + fraction);
            }
            foreach (char c in suffix) buffer[n++] = c;
            if (exponent > 0)
            {
                buffer[n++] = 'e';
                if (exponent >= 10) buffer[n++] = (char)('0' + exponent / 10);
                buffer[n++] = (char)('0' + exponent % 10);
            }
            return n;
        }
    }

}
