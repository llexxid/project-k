using System;
using UnityEngine;

namespace KingdomIdle.UGUI
{
    /// <summary>Device preferences, independent of account/progression authority.</summary>
    public static class GameAudioSettings
    {
        public const string MusicKey = "settings_musicVolume", EffectsKey = "settings_effectsVolume";
        public static float Music { get; private set; } = 1;
        public static float Effects { get; private set; } = 1;
        public static float Master { get; private set; } = 1;
        public static bool Muted { get; private set; }
        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { Music = Effects = Master = 1; Muted = false; Changed = null; }

        internal static float ReadVolume(string key)
        {
            float value = PlayerPrefs.GetFloat(key, 1);
            return float.IsNaN(value) || float.IsInfinity(value) ? 1 : Mathf.Clamp01(value);
        }
        public static void Apply()
        {
            Master = ReadVolume(UIManager.PrefKeyVolume);
            Music = ReadVolume(MusicKey); Effects = ReadVolume(EffectsKey);
            Muted = PlayerPrefs.GetInt(UIManager.PrefKeyMute, 0) == 1;
            AudioListener.volume = Muted ? 0 : Master;
            Changed?.Invoke();
        }
    }
}
