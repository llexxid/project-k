using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using KingdomIdle.UI;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>
    /// 생성된 UGUI 프리팹을 실제로 렌더링해 PNG로 저장하는 진단 도구.
    /// (플레이 없이 UI 외형을 확인하기 위한 용도 — Unity를 -batchmode 로 실행하되
    ///  -nographics 는 빼야 렌더링이 된다.)
    /// </summary>
    internal static class UguiPreviewCapture
    {
        private const int W = 1080;
        private const int H = 1920;
        private static readonly string OutDir = Path.Combine(Path.GetTempPath(), "ugui_preview");

        internal static void CaptureAll()
        {
            Directory.CreateDirectory(OutDir);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 렌더용 카메라
            var camGo = new GameObject("PreviewCam");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.15f, 0.35f, 0.15f, 1f);   // 게임 배경 대용(초원 느낌)
            cam.transform.position = new Vector3(0, 0, -100);

            var catalog = AssetDatabase.LoadAssetAtPath<UIViewCatalog>(PrefabGenUtil.CatalogPath);
            if (catalog == null)
            {
                Debug.LogError("[Preview] 카탈로그 없음");
                return;
            }

            var rootPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabGenUtil.PrefabRoot}/UGUI_UIRoot.prefab");
            var rootGo = (GameObject)PrefabUtility.InstantiatePrefab(rootPrefab, scene);

            var canvas = rootGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 10f;

            // UIManager가 Awake를 못 도니 레이어를 직접 찾는다
            var layers = new Dictionary<string, RectTransform>();
            foreach (var rt in rootGo.GetComponentsInChildren<RectTransform>(true))
                layers[rt.name] = rt;

            var shots = new (string name, GameObject prefab, string layer)[]
            {
                ("01_title",       catalog.screenTitle,      "LayerScreens"),
                ("02_main",        catalog.screenMain,       "LayerScreens"),
                ("03_kingdomarmy", catalog.panelKingdomArmy, "LayerPanels"),
                ("04_settings",    catalog.overlaySettings,  "LayerOverlays"),
                ("05_gacharesult", catalog.popupGachaResult, "LayerOverlays"),
            };

            foreach (var s in shots)
            {
                if (s.prefab == null) continue;
                if (!layers.TryGetValue(s.layer, out var parent)) continue;

                var inst = (GameObject)PrefabUtility.InstantiatePrefab(s.prefab, scene);
                inst.transform.SetParent(parent, false);
                var irt = (RectTransform)inst.transform;
                irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one;
                irt.offsetMin = Vector2.zero; irt.offsetMax = Vector2.zero;
                inst.SetActive(true);

                // 메인 화면일 땐 하단바/상단바가 보이도록 그대로 두고 캡처
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(irt);
                Canvas.ForceUpdateCanvases();

                Render(cam, Path.Combine(OutDir, s.name + ".png"));

                Object.DestroyImmediate(inst);
            }

            Debug.Log($"[Preview] 캡처 완료: {OutDir}");
        }

        private static void Render(Camera cam, string path)
        {
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();

            File.WriteAllBytes(path, tex.EncodeToPNG());

            RenderTexture.active = null;
            cam.targetTexture = null;
            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(rt);
        }
    }
}
