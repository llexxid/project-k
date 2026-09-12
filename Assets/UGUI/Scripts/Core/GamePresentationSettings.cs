using System;
using UnityEngine;

namespace KingdomIdle.UGUI
{
    public static class GamePresentationSettings
    {
        public static bool HideItemNotifications { get; private set; }
        public static bool PowerSave { get; private set; }
        public const string LowSpecKey = "settings_lowSpec";
        public static bool LowSpec { get; private set; }
        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            Changed = null;
            LowSpec = false;
            PowerSave = false;
            HideItemNotifications = false;
        }

        public static void SetLowSpec(bool enabled, bool save = true)
        {
            PlayerPrefs.SetInt(LowSpecKey, enabled ? 1 : 0);
            // Retain compatibility with the first illustrated title revision.
            PlayerPrefs.SetInt("title_ambientMotion", enabled ? 0 : 1);
            Apply();
            if (save) PlayerPrefs.Save();
        }

        public static void Apply()
        {
            bool oldLowSpec = LowSpec, oldPowerSave = PowerSave;
            if (!PlayerPrefs.HasKey(LowSpecKey))
                PlayerPrefs.SetInt(LowSpecKey, PlayerPrefs.GetInt("title_ambientMotion", 1) == 0 ? 1 : 0);
            LowSpec = PlayerPrefs.GetInt(LowSpecKey, 0) == 1;
            PowerSave = PlayerPrefs.GetInt(UIManager.PrefKeyPowerSave, 0) == 1;
            HideItemNotifications = PlayerPrefs.GetInt(UIManager.PrefKeyHideItem, 0) == 1;
            // Low-spec keeps the normal input/render cadence. The existing optional battery saver is separate.
            Application.targetFrameRate = PowerSave ? 30 : 60;
            if (oldLowSpec != LowSpec || oldPowerSave != PowerSave) Changed?.Invoke();
        }
    }
}
