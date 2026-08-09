using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>
    /// bootstrap.unity 재배선: UITK_UIRoot 비활성화 ↔ UGUI_UIRoot 배치, GameManager 브릿지 교체.
    /// UITK 타입은 이름 문자열로 다뤄서(리플렉션) Phase 8에서 UITK 스크립트가 삭제된 뒤에도 컴파일된다.
    /// </summary>
    internal static class BootstrapRewireGen
    {
        private const string BootstrapPath = "Assets/_Project/Scenes/buildScenes/bootstrap.unity";
        private const string UitkRootName = "UITK_UIRoot";
        private const string UguiRootName = "UGUI_UIRoot";

        internal static void SwitchToUgui()
        {
            var scene = OpenBootstrap();

            // 1) UITK 루트 비활성화 (컴포넌트는 남겨둠 — Switch back 복원용; 최종 삭제는 RemoveUitkRoot)
            var uitkRoot = FindRoot(scene, UitkRootName);
            if (uitkRoot != null && uitkRoot.activeSelf)
            {
                uitkRoot.SetActive(false);
                Debug.Log("[UguiGen] UITK_UIRoot 비활성화");
            }

            // 2) GameManager 브릿지 교체
            var gameManager = FindRoot(scene, "GameManager");
            if (gameManager != null)
            {
                RemoveComponentByName(gameManager, "UITKSceneRoutingBridge");
                RemoveComponentByName(gameManager, "UITKLoadingOverlayBridge");

                if (gameManager.GetComponent<SceneRoutingBridge>() == null)
                    gameManager.AddComponent<SceneRoutingBridge>();
                if (gameManager.GetComponent<LoadingOverlayBridge>() == null)
                    gameManager.AddComponent<LoadingOverlayBridge>();
            }
            else
            {
                Debug.LogWarning("[UguiGen] GameManager 오브젝트를 찾지 못했습니다 — 브릿지 교체 생략");
            }

            // 3) UGUI 루트 배치
            if (FindRoot(scene, UguiRootName) == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabGenUtil.PrefabRoot}/UGUI_UIRoot.prefab");
                if (prefab == null)
                {
                    Debug.LogError("[UguiGen] UGUI_UIRoot.prefab이 없습니다. Generate All을 먼저 실행하세요.");
                }
                else
                {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                    instance.name = UguiRootName;
                    Debug.Log("[UguiGen] UGUI_UIRoot 배치");
                }
            }

            SaveBootstrap(scene);
            Debug.Log("[UguiGen] Switch to UGUI 완료");
        }

        internal static void SwitchBackToUitk()
        {
            var scene = OpenBootstrap();

            // 1) UGUI 루트 제거
            var uguiRoot = FindRoot(scene, UguiRootName);
            if (uguiRoot != null)
            {
                UnityEngine.Object.DestroyImmediate(uguiRoot);
                Debug.Log("[UguiGen] UGUI_UIRoot 제거");
            }

            // 2) GameManager 브릿지 원복
            var gameManager = FindRoot(scene, "GameManager");
            if (gameManager != null)
            {
                var routing = gameManager.GetComponent<SceneRoutingBridge>();
                if (routing != null) UnityEngine.Object.DestroyImmediate(routing);
                var loading = gameManager.GetComponent<LoadingOverlayBridge>();
                if (loading != null) UnityEngine.Object.DestroyImmediate(loading);

                AddComponentByName(gameManager, "KingdomIdle.UIToolkit.UITKSceneRoutingBridge");
                AddComponentByName(gameManager, "KingdomIdle.UIToolkit.UITKLoadingOverlayBridge");
            }

            // 3) UITK 루트 재활성화
            var uitkRoot = FindRoot(scene, UitkRootName);
            if (uitkRoot != null && !uitkRoot.activeSelf)
            {
                uitkRoot.SetActive(true);
                Debug.Log("[UguiGen] UITK_UIRoot 재활성화");
            }

            SaveBootstrap(scene);
            Debug.Log("[UguiGen] Switch back to UITK 완료");
        }

        /// <summary>최종 정리(Phase 8): 비활성 UITK_UIRoot 오브젝트를 씬에서 완전히 제거.</summary>
        internal static void RemoveUitkRoot()
        {
            var scene = OpenBootstrap();

            var uitkRoot = FindRoot(scene, UitkRootName);
            if (uitkRoot != null)
            {
                UnityEngine.Object.DestroyImmediate(uitkRoot);
                Debug.Log("[UguiGen] UITK_UIRoot 완전 제거");
                SaveBootstrap(scene);
            }
            else
            {
                Debug.Log("[UguiGen] UITK_UIRoot가 이미 없습니다.");
            }
        }

        // ── 유틸 ──

        private static Scene OpenBootstrap()
        {
            var active = SceneManager.GetActiveScene();
            if (active.path == BootstrapPath) return active;
            return EditorSceneManager.OpenScene(BootstrapPath, OpenSceneMode.Single);
        }

        private static void SaveBootstrap(Scene scene)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == name) return root;
            }
            return null;
        }

        private static void RemoveComponentByName(GameObject go, string typeName)
        {
            var comps = go.GetComponents<Component>();
            foreach (var c in comps)
            {
                if (c == null) continue;
                if (c.GetType().Name == typeName)
                {
                    UnityEngine.Object.DestroyImmediate(c);
                    Debug.Log($"[UguiGen] {go.name}에서 {typeName} 제거");
                    return;
                }
            }
        }

        private static void AddComponentByName(GameObject go, string fullTypeName)
        {
            var type = Type.GetType($"{fullTypeName}, Assembly-CSharp");
            if (type == null)
            {
                Debug.LogWarning($"[UguiGen] 타입을 찾을 수 없습니다: {fullTypeName} (이미 삭제되었을 수 있음)");
                return;
            }

            if (go.GetComponent(type) == null)
                go.AddComponent(type);
        }
    }
}
