using System;
using System.IO;
using KingdomIdle.Balance;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>실계정 없이 실제 프리팹 입력 경로를 검사하고 원래 편집 씬으로 돌아간다.</summary>
    [InitializeOnLoad]
    public static class BronzeUiPlayAcceptance
    {
        private const string Active = "BronzeUiPlayAcceptance.Active";
        private const string Previous = "BronzeUiPlayAcceptance.PreviousScenes";
        private const string OutputKey = "BronzeUiPlayAcceptance.Output";
        private const string Failed = "BronzeUiPlayAcceptance.Failed";
        [Serializable] private sealed class Scenes { public SceneSetup[] items; }

        static BronzeUiPlayAcceptance() => EditorApplication.playModeStateChanged += OnState;

        [MenuItem("KingdomIdle/UGUI/청동 원정대/격리 퀘스트 입력 검사")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before the isolated acceptance run.");
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save the edited scenes before the isolated acceptance run.");
            string output = Environment.GetEnvironmentVariable("BRONZE_UI_QA_OUTPUT");
            if (string.IsNullOrWhiteSpace(output)) output = Path.Combine(Path.GetTempPath(), "KingdomIdle-BronzeUiQA");
            output = Path.GetFullPath(output);
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (output.StartsWith(project, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(output.TrimEnd(Path.DirectorySeparatorChar), project.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("QA output must be outside the project.");
            Directory.CreateDirectory(output);
            SessionState.SetString(OutputKey, output);
            SessionState.SetString(Previous, JsonUtility.ToJson(new Scenes { items = EditorSceneManager.GetSceneManagerSetup() }));
            SessionState.SetBool(Failed, false);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool(Active, true);
            EditorApplication.isPlaying = true;
        }

        private static void OnState(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Active, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                EditorApplication.delayCall += () =>
                {
                    try
                    {
                        if (LocalProgression.IsReady)
                            throw new InvalidOperationException("The empty test scene unexpectedly opened a profile.");
                        var report = QuestTabAcceptance.Run();
                        File.WriteAllText(Path.Combine(SessionState.GetString(OutputKey, ""), "quest-play-acceptance.json"),
                            JsonConvert.SerializeObject(report, Formatting.Indented));
                        Debug.Log("BRONZE QUEST PLAY ACCEPTANCE PASSED " + report["passed"]);
                    }
                    catch (Exception error)
                    {
                        SessionState.SetBool(Failed, true);
                        File.WriteAllText(Path.Combine(SessionState.GetString(OutputKey, ""), "quest-play-failure.txt"), error.ToString());
                        Debug.LogException(error);
                    }
                    finally { EditorApplication.isPlaying = false; }
                };
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(Active, false);
                var scenes = JsonUtility.FromJson<Scenes>(SessionState.GetString(Previous, "{}"));
                if (scenes?.items != null && scenes.items.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(scenes.items);
                if (Application.isBatchMode) EditorApplication.Exit(SessionState.GetBool(Failed, true) ? 1 : 0);
            }
        }
    }
}
