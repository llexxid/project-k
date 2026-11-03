using System;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace KingdomIdle.UGUI
{
    // This control manages the OS display permission. Campaign subscriptions are separate.
    internal sealed class DeviceNotificationPermission
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        const string PermissionName = "android.permission.POST_NOTIFICATIONS";
        const string DecisionKey = "device.notificationPermissionDecided";
        PermissionCallbacks _request;

        static int SdkVersion
        {
            get
            {
                using var version = new AndroidJavaClass("android.os.Build$VERSION");
                return version.GetStatic<int>("SDK_INT");
            }
        }

        static bool CanAsk()
        {
            return SdkVersion >= 33 && !Permission.HasUserAuthorizedPermission(PermissionName) &&
                (!PlayerPrefs.HasKey(DecisionKey) || Permission.ShouldShowRequestPermissionRationale(PermissionName));
        }
#endif

        internal string ButtonText(bool english, out bool available)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            available = _request == null;
            if (!available) return english ? "Checking notification permission" : "휴대폰 알림 권한 확인 중";
            try
            {
                if (CanAsk()) return english ? "Show game notifications on this phone (optional)\nRequest permission"
                    : "휴대폰에 게임 알림 표시 (선택)\n알림 권한 요청";
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

        internal void Manage(bool english, Action refresh)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (_request != null) return;
            try
            {
                if (CanAsk())
                {
                    _request = new PermissionCallbacks();
                    _request.PermissionGranted += _ => Finish(true);
                    _request.PermissionDenied += _ => Finish(true);
                    _request.PermissionRequestDismissed += _ => Finish(false);
                    refresh();
                    Permission.RequestUserPermission(PermissionName, _request);
                    return;
                }
                OpenSystemSettings();
            }
            catch (AndroidJavaException e)
            {
                _request = null;
                refresh();
                Debug.LogWarning("Unable to open notification settings: " + e.Message);
                UIManager.Instance?.ShowToast(english ? "Manage notifications in your phone's app settings."
                    : "휴대폰 설정의 앱 알림에서 변경할 수 있습니다.");
            }

            void Finish(bool decided)
            {
                if (decided)
                {
                    PlayerPrefs.SetInt(DecisionKey, 1);
                    PlayerPrefs.Save();
                }
                _request = null;
                refresh();
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
