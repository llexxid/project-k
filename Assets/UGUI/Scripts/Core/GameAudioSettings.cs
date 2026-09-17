using System;
using UnityEngine;

namespace KingdomIdle.UGUI
{
    public enum SoundChannel { General, RoyalGuard, Monsters, MageTower, Interface }
    /// <summary>Device preferences, independent of account/progression authority.</summary>
    public static class GameAudioSettings
    {
        public const string MusicKey = "settings_musicVolume", EffectsKey = "settings_effectsVolume";
        public const string RoyalGuardKey="settings_guardVolume", MonstersKey="settings_monsterVolume", SpellsKey="settings_spellVolume", InterfaceKey="settings_uiVolume";
        public static float RoyalGuard { get; private set; } = 1;
        public static float Monsters { get; private set; } = 1;
        public static float Spells { get; private set; } = 1;
        public static float Interface { get; private set; } = 1;
        public static float Music { get; private set; } = 1;
        public static float Effects { get; private set; } = 1;
        public static float Master { get; private set; } = 1;
        public static bool Muted { get; private set; }
        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { Music = Effects = Master = RoyalGuard = Monsters = Spells = Interface = 1; Muted = false; Changed = null; }
        public static float Gain(SoundChannel channel) => Effects * (channel switch {
            SoundChannel.RoyalGuard => RoyalGuard, SoundChannel.Monsters => Monsters,
            SoundChannel.MageTower => Spells, SoundChannel.Interface => Interface, _ => 1 });

        internal static float ReadVolume(string key)
        {
            float value = PlayerPrefs.GetFloat(key, 1);
            return float.IsNaN(value) || float.IsInfinity(value) ? 1 : Mathf.Clamp01(value);
        }
        public static void Apply()
        {
            Master = ReadVolume(UIManager.PrefKeyVolume);
            Music = ReadVolume(MusicKey); Effects = ReadVolume(EffectsKey);
            RoyalGuard=ReadVolume(RoyalGuardKey);Monsters=ReadVolume(MonstersKey);Spells=ReadVolume(SpellsKey);Interface=ReadVolume(InterfaceKey);
            Muted = PlayerPrefs.GetInt(UIManager.PrefKeyMute, 0) == 1;
            AudioListener.volume = Muted ? 0 : Master;
            Changed?.Invoke();
        }
    }
}
