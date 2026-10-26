using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace KingdomIdle.UGUI.Editor
{
    public static class AdMobTestSceneBuilder
    {
        public const string ScenePath = "Assets/UGUI/Scenes/AdMobTest.unity";

        [MenuItem("KingdomIdle/Ads/Open Test Scene")]
        public static void OpenTestScene()
        {
            EnsureCanRun();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        [MenuItem("KingdomIdle/Ads/Build Android Test APK")]
        public static void BuildAndroidTestApk()
        {
            EnsureCanRun();
            TitleLobbyDeviceBuild.BuildAdMobTest();
        }

        private static void EnsureCanRun()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("먼저 Play 모드를 종료해 주세요.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("현재 씬의 변경 사항을 먼저 저장해 주세요.");
        }
    }
}
