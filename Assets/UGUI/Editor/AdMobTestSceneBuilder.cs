using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace KingdomIdle.UGUI.Editor
{
    public static class AdMobTestSceneBuilder
    {
        public const string ScenePath = "Assets/UGUI/Scenes/AdMobTest.unity";

        [MenuItem("KingdomIdle/Ads/Open Test Scene")]
        public static void OpenTestScene() => CreateScene();

        [MenuItem("KingdomIdle/Ads/Build Android Test APK")]
        public static void BuildAndroidTestApk()
        {
            if (!File.Exists(ScenePath)) CreateScene();
            TitleLobbyDeviceBuild.BuildAdMobTest();
        }

        public static void CreateScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("광고 테스트 씬을 열기 전에 Play 모드를 종료해 주세요.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("현재 씬의 변경 사항을 먼저 저장해 주세요.");

            if (File.Exists(ScenePath))
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                return;
            }

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/UGUI/Art/Font/Galmuri11 SDF.asset");
            if (font == null) throw new InvalidOperationException("기존 한글 폰트를 찾지 못했습니다.");
            PrefabGenUtil.EnsureFolder("Assets/UGUI/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            camera.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 1);
            camera.cullingMask = 0;
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            var root = new GameObject("AdMobTestCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            root.layer = 5;
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            var controller = root.AddComponent<AdMobTestController>();
            var safeArea = Rect(root.transform, "SafeArea", Vector2.zero, Vector2.zero);
            Stretch(safeArea);
            safeArea.gameObject.AddComponent<SafeAreaFitter>();
            controller.showButton = Button(safeArea, "ShowButton", "광고 보기", font,
                new Vector2(600, 140), Vector2.zero, out var buttonLabel);
            controller.buttonLabel = buttonLabel;

            var popup = Rect(root.transform, "CompletionPopup", Vector2.zero, Vector2.zero);
            Stretch(popup);
            popup.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(0, 0, 0, 0.65f);
            var panel = Rect(popup, "Panel", new Vector2(780, 420), Vector2.zero);
            panel.gameObject.AddComponent<UnityEngine.UI.Image>().color = Color.white;
            controller.popupLabel = Label(panel, "Message", "광고 시청이 완료되었습니다.", font,
                new Vector2(680, 160), new Vector2(0, 60));
            controller.closeButton = Button(panel, "CloseButton", "닫기", font,
                new Vector2(420, 110), new Vector2(0, -110), out _);
            controller.popup = popup.gameObject;
            popup.gameObject.SetActive(false);

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("광고 테스트 씬을 저장하지 못했습니다.");
            Selection.activeGameObject = root;
        }

        static UnityEngine.UI.Button Button(Transform parent, string name, string text, TMP_FontAsset font,
            Vector2 size, Vector2 position, out TMP_Text label)
        {
            var rect = Rect(parent, name, size, position);
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = new Color(0.85f, 0.85f, 0.85f, 1);
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            label = Label(rect, "Label", text, font, size - new Vector2(32, 16), Vector2.zero);
            return button;
        }

        static TMP_Text Label(Transform parent, string name, string text, TMP_FontAsset font,
            Vector2 size, Vector2 position)
        {
            var label = Rect(parent, name, size, position).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.text = text;
            label.fontSize = 42;
            label.color = Color.black;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            label.richText = false;
            return label;
        }

        static RectTransform Rect(Transform parent, string name, Vector2 size, Vector2 position)
        {
            var target = new GameObject(name, typeof(RectTransform)) { layer = 5 };
            var rect = (RectTransform)target.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
