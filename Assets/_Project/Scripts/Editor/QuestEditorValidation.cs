using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using KingdomIdle.Balance;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace KingdomIdle.Editor
{
    /// <summary>열려 있는 사용자 씬을 바꾸지 않고 카탈로그·거래·프리팹 연결을 검증하는 Editor 진입점이다.</summary>
    [InitializeOnLoad]
    public static class QuestEditorValidation
    {
        private const string RequestPath = "Library/QuestValidation.request";
        private const string OutputDirectory = "AI/validation/quests-20260917";

        static QuestEditorValidation() { EditorApplication.update += CheckRequest; }

        /// <summary>고정된 인수 검사만 요청 파일로 실행한다. 임의 코드나 메서드는 입력받지 않는다.</summary>
        private static void CheckRequest()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode ||
                !File.Exists(RequestPath)) return;
            if (File.ReadAllText(RequestPath).Trim() == "refresh")
            {
                // 다음 editor update 또는 domain reload에서 새로 컴파일된 검사만 실행한다.
                File.WriteAllText(RequestPath, "run");
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                return;
            }
            File.Delete(RequestPath);
            Run();
        }

        /// <summary>격리 계정으로 인수 검사와 직렬화 참조 확인 후 결과를 기록한다.</summary>
        [MenuItem("KingdomIdle/Validation/Quest System")]
        public static void Run()
        {
            Directory.CreateDirectory(OutputDirectory);
            var report = new Dictionary<string, object> { ["unity"] = Application.unityVersion,
                ["utc"] = DateTimeOffset.UtcNow.ToString("O"), ["mode"] = "Unity Editor isolated accounts" };
            try
            {
                // 원본 Excel을 읽되 런타임 카탈로그를 다시 쓰지 않는다.
                string generated = QuestCatalogImporter.BuildJson(QuestCatalogImporter.SourcePath);
                string current = File.ReadAllText("Assets/_Project/Resources/Balance/catalog.json");
                if (!JToken.DeepEquals(JObject.Parse(generated), JObject.Parse(current)))
                    throw new InvalidOperationException("Excel and shipped quest catalog differ.");
                report["catalogWorkbookEquivalent"] = true;
                // QA 전용 검사 클래스를 고정 이름으로만 찾는다. 플레이어 일반 빌드에는 포함되지 않는다.
                foreach (string name in new[] { "QuestAcceptance", "QuestTimeAcceptance", "QuestUiAcceptance" })
                {
                    Type type = typeof(QuestEconomy).Assembly.GetType("KingdomIdle.Balance." + name, true);
                    report[name] = type.GetMethod("Run", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
                }
                // 기존 경제 회귀 검사도 계정 복구 범위 안에서 실행한다. 기존 40회 내구 저장 측정치를 함께 남긴다.
                using (LocalProgression.BeginTestSession()) report["BalanceAcceptance"] = BalanceAcceptance.Run();
                report["serializedReferences"] = CheckReferences();
                report["passed"] = true;
                Debug.Log("QUEST VALIDATION PASSED");
            }
            catch (Exception error)
            {
                Exception cause = error is TargetInvocationException invocation && invocation.InnerException != null ? invocation.InnerException : error;
                report["passed"] = false;
                report["error"] = cause.ToString();
                Debug.LogException(cause);
            }
            File.WriteAllText(OutputDirectory + "/editor-validation.json", JsonConvert.SerializeObject(report, Formatting.Indented));
        }

        /// <summary>씬을 preview로 열어 사용자 작업 씬과 저장 상태를 보존한다.</summary>
        private static int CheckReferences()
        {
            int count = 0;
            var scene = EditorSceneManager.OpenPreviewScene("Assets/_Project/Scenes/buildScenes/bootstrap.unity");
            try
            {
                GameObject[] roots = scene.GetRootGameObjects();
                if (roots.SelectMany(x => x.GetComponentsInChildren<QuestManager>(true)).Count() != 1)
                    throw new InvalidOperationException("Bootstrap must contain one QuestManager.");
                foreach (GameObject root in roots) count += CheckObject(root);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
            foreach (string path in new[] { "Assets/_Project/Prefabs/QuestUI/GuideQuestPannel.prefab", "Assets/UGUI/Prefabs/UGUI_UIRoot.prefab" })
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) throw new InvalidOperationException("Missing prefab: " + path);
                count += CheckObject(prefab);
            }
            return count;
        }

        /// <summary>선택한 직렬화 그래프의 missing script 및 끊긴 객체 참조를 검사한다.</summary>
        private static int CheckObject(GameObject root)
        {
            int count = 0;
            foreach (Component component in root.GetComponentsInChildren<Component>(true))
            {
                if (component == null) throw new InvalidOperationException("Missing script under " + root.name);
                var serialized = new SerializedObject(component);
                var property = serialized.GetIterator();
                while (property.NextVisible(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                    count++;
                    if (property.objectReferenceValue == null && property.objectReferenceInstanceIDValue != 0)
                        throw new InvalidOperationException("Broken reference: " + root.name + "/" + property.propertyPath);
                }
            }
            return count;
        }
    }
}
