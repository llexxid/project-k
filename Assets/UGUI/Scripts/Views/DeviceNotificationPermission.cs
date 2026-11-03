using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace KingdomIdle.UGUI
{
    // This control manages the OS display permission. Campaign subscriptions are separate.
    internal static class DeviceNotificationPermission
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        const string PermissionName = "android.permission.POST_NOTIFICATIONS";
        // Keep the existing key so an earlier permission decision also suppresses the first-launch prompt.
        const string PromptHandledKey = "device.notificationPermissionDecided";

        static int SdkVersion
        {
            get
            {
                using var version = new AndroidJavaClass("android.os.Build$VERSION");
                return version.GetStatic<int>("SDK_INT");
            }
        }

        internal static void RequestOnFirstLaunch()
        {
            if (PlayerPrefs.HasKey(PromptHandledKey)) return;
            try
            {
                if (SdkVersion < 33) return;
                bool alreadyAllowed = Permission.HasUserAuthorizedPermission(PermissionName);
                bool shouldPrompt = !alreadyAllowed && !Permission.ShouldShowRequestPermissionRationale(PermissionName);
                // Android can pause or kill the activity while the dialog is open.
                // Store the attempt before handing off, including when the user dismisses it.
                PlayerPrefs.SetInt(PromptHandledKey, 1);
                PlayerPrefs.Save();
                if (shouldPrompt) Permission.RequestUserPermission(PermissionName);
            }
            catch (AndroidJavaException e)
            {
                Debug.LogWarning("Unable to request notification permission: " + e.Message);
            }
        }
#endif

        internal static string ButtonText(bool english, out bool available)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            available = true;
            try
            {
                using var manager = AndroidApplication.currentActivity.Call<AndroidJavaObject>("getSystemService", "notification");
                bool enabled = manager.Call<bool>("areNotificationsEnabled");
                if (english) return enabled ? "Notifications allowed · Open device settings" : "Notifications off · Open device settings";
                return enabled ? "휴대폰 알림 허용됨 · 기기 설정 열기" : "휴대폰 알림 꺼짐 · 기기 설정 열기";
            }
            catch (AndroidJavaException)
            {
                return english ? "Notifications · Open device settings" : "휴대폰 알림 · 기기 설정 열기";
            }
#else
            available = false;
            return english ? "Manage phone notifications on your Android device" : "휴대폰 알림은 Android 기기에서 설정할 수 있습니다";
#endif
        }

        internal static void Manage(bool english)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                OpenSystemSettings();
            }
            catch (AndroidJavaException e)
            {
                Debug.LogWarning("Unable to open notification settings: " + e.Message);
                UIManager.Instance?.ShowToast(english ? "Manage notifications in your phone's app settings."
                    : "휴대폰 설정의 앱 알림에서 변경할 수 있습니다.");
            }

#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        static void OpenSystemSettings()
        {
            var activity = AndroidApplication.currentActivity;
            string package = activity.Call<string>("getPackageName");
            if (SdkVersion >= 26)
            {
                using var intent = new AndroidJavaObject("android.content.Intent", "android.settings.APP_NOTIFICATION_SETTINGS");
                using var withPackage = intent.Call<AndroidJavaObject>("putExtra", "android.provider.extra.APP_PACKAGE", package);
                try { activity.Call("startActivity", intent); return; }
                catch (AndroidJavaException) { /* Use the app details screen on devices without this activity. */ }
            }
            using var uri = new AndroidJavaClass("android.net.Uri");
            using var packageUri = uri.CallStatic<AndroidJavaObject>("parse", "package:" + package);
            using var details = new AndroidJavaObject("android.content.Intent", "android.settings.APPLICATION_DETAILS_SETTINGS", packageUri);
            activity.Call("startActivity", details);
        }
#endif
    }
}
