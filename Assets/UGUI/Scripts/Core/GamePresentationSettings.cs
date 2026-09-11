using UnityEngine;

namespace KingdomIdle.UGUI
{
    public static class GamePresentationSettings
    {
        public static bool HideItemNotifications { get; private set; }
        public static bool PowerSave { get; private set; }
        public static void Apply()
        {
            PowerSave = PlayerPrefs.GetInt(UIManager.PrefKeyPowerSave, 0) == 1;
            HideItemNotifications = PlayerPrefs.GetInt(UIManager.PrefKeyHideItem, 0) == 1;
            // Keep combat simulation unchanged; reduce only presentation frame frequency.
            Application.targetFrameRate = PowerSave ? 30 : 60;
        }
    }
}
