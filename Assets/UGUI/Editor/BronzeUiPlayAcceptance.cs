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
        private const string ModeKey = "BronzeUiPlayAcceptance.Mode";
        [Serializable] private sealed class Scenes { public SceneSetup[] items; }

        static BronzeUiPlayAcceptance() => EditorApplication.playModeStateChanged += OnState;

        [MenuItem("KingdomIdle/UGUI/청동 원정대/격리 퀘스트 입력 검사")]
        public static void Run() => Start("quest");

        [MenuItem("KingdomIdle/UGUI/청동 원정대/격리 오프라인 광고 지급 오류 검사")]
        public static void RunOfflineFailureRecovery() => Start("offline-failure");

        [MenuItem("KingdomIdle/UGUI/청동 원정대/격리 퀘스트 지급 오류 검사")]
        public static void RunQuestFailureRecovery() => Start("quest-failure");

        [MenuItem("KingdomIdle/UGUI/청동 원정대/격리 장비·상점 통신 복구 검사")]
        public static void RunCommerceFailureRecovery() => Start("commerce-failure");

        public static void RunAllRequestFailureRecovery() => Start("all-request-failure");

        private static void Start(string mode)
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
            SessionState.SetString(ModeKey, mode);
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
                    bool offlineFailure = SessionState.GetString(ModeKey, "quest") == "offline-failure";
                    bool questFailure = SessionState.GetString(ModeKey, "quest") == "quest-failure";
                    bool allRequests = SessionState.GetString(ModeKey, "quest") == "all-request-failure";
                    bool commerceFailure = allRequests || SessionState.GetString(ModeKey, "quest") == "commerce-failure";
                    string filePrefix = commerceFailure ? "commerce-failure-recovery-play" : offlineFailure ? "offline-failure-recovery-play" : questFailure ? "quest-failure-recovery-play" : "quest-play";
                    try
                    {
                        if (LocalProgression.IsReady)
                            throw new InvalidOperationException("The empty test scene unexpectedly opened a profile.");
                        var report = commerceFailure ? RunCommerceChecks() : offlineFailure ? OfflineRewardAdAcceptance.RunFailureRecovery() :
                            questFailure ? QuestTabAcceptance.RunClaimFailureRecovery() : QuestTabAcceptance.Run();
                        File.WriteAllText(Path.Combine(SessionState.GetString(OutputKey, ""), filePrefix + "-acceptance.json"),
                            JsonConvert.SerializeObject(report, Formatting.Indented));
                        Debug.Log((commerceFailure ? "BRONZE COMMERCE FAILURE RECOVERY PLAY ACCEPTANCE PASSED " : offlineFailure ? "BRONZE OFFLINE FAILURE RECOVERY PLAY ACCEPTANCE PASSED " :
                            questFailure ? "BRONZE QUEST FAILURE RECOVERY PLAY ACCEPTANCE PASSED " : "BRONZE QUEST PLAY ACCEPTANCE PASSED ") + report["passed"]);
                        if (allRequests)
                        {
                            filePrefix = "quest-failure-recovery-play";
                            var questReport = QuestTabAcceptance.RunClaimFailureRecovery();
                            File.WriteAllText(Path.Combine(SessionState.GetString(OutputKey, ""), filePrefix + "-acceptance.json"),
                                JsonConvert.SerializeObject(questReport, Formatting.Indented));
                            Debug.Log("BRONZE QUEST FAILURE RECOVERY PLAY ACCEPTANCE PASSED " + questReport["passed"]);
                        }
                    }
                    catch (Exception error)
                    {
                        SessionState.SetBool(Failed, true);
                        File.WriteAllText(Path.Combine(SessionState.GetString(OutputKey, ""), filePrefix + "-failure.txt"), error.ToString());
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

        private static System.Collections.Generic.Dictionary<string, object> RunCommerceChecks()
        {
            var equipment = Newtonsoft.Json.Linq.JObject.FromObject(EquipmentEconomyAcceptance.RunFailureRecovery());
            var account = Newtonsoft.Json.Linq.JObject.FromObject(ShopAdRewardAcceptance.RunAccountLifecycle());
            var shop = ShopAdRewardAcceptance.RunServerFailureRecovery();
            return new System.Collections.Generic.Dictionary<string, object>
            {
                { "passed", (int)equipment["count"] + (int)account["count"] + (int)shop["passed"] },
                { "equipment", equipment }, { "shopAccount", account }, { "shopFailure", shop }
            };
        }
    }
}
