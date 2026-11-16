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
        private static System.Collections.IEnumerator _lootLifetime;
        private static readonly System.Collections.Generic.Stack<System.Collections.IEnumerator> Routines = new();
        private static CustomYieldInstruction _wait;
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

        [MenuItem("KingdomIdle/UGUI/청동 원정대/격리 획득 알림 검사")]
        public static void RunLootFeed() => Start("loot-feed");

        [MenuItem("KingdomIdle/UGUI/청동 원정대/격리 UI 시뮬레이션 검사")]
        public static void RunUiSimulation() => Start("ui-simulation");

        [MenuItem("KingdomIdle/UGUI/청동 원정대/격리 획득 알림 Game View 미리보기")]
        public static void RunUiPreview() => Start("ui-preview");

        // 실제 프리팹도 갱신한 뒤 획득 알림 관련 검사만 실행한다.
        public static void RunCompactLootFeed()
        {
            LootFeedPreparation.Prepare();
            Start("compact-loot");
        }

        public static void PrepareLootFeed()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before preparing Loot Feed prefabs.");
            F.Init();
            F.Catalog = AssetDatabase.LoadAssetAtPath<UIViewCatalog>(PrefabGenUtil.CatalogPath);
            if (F.Catalog == null) throw new InvalidOperationException("UI catalog is missing.");
            SettingsRevisionBuilder.Generate();
            LootFeedPreparation.Prepare();
            MagePolishPreparation.BakeAuthoredGlyphs();
            AssetDatabase.SaveAssets();
            Debug.Log("LOOT FEED PREFABS AND GLYPHS PREPARED");
        }

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
            if (mode == "ui-preview") File.Delete(Path.Combine(output, "preview-stop.txt"));
            SessionState.SetString(OutputKey, output);
            SessionState.SetString(ModeKey, mode);
            SessionState.SetString(Previous, JsonUtility.ToJson(new Scenes { items = EditorSceneManager.GetSceneManagerSetup() }));
            SessionState.SetBool(Failed, false);
            if (mode == "ui-simulation")
            {
                SaveSimulationReport("common-ui", BronzeUiAcceptance.Run());
                SaveSimulationReport("equipment-grid", BronzeEquipmentGridAcceptance.Run());
            }
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
                    bool loot = SessionState.GetString(ModeKey, "quest") == "loot-feed";
                    bool simulation = SessionState.GetString(ModeKey, "quest") == "ui-simulation";
                    bool preview = SessionState.GetString(ModeKey, "quest") == "ui-preview";
                    bool compactLoot = SessionState.GetString(ModeKey, "quest") == "compact-loot";
                    bool awaitingLifetime = false;
                    string filePrefix = compactLoot ? "compact-loot" : preview ? "ui-preview" : simulation ? "ui-simulation" : loot ? "loot-feed-play" : commerceFailure ? "commerce-failure-recovery-play" : offlineFailure ? "offline-failure-recovery-play" : questFailure ? "quest-failure-recovery-play" : "quest-play";
                    try
                    {
                        if (LocalProgression.IsReady)
                            throw new InvalidOperationException("The empty test scene unexpectedly opened a profile.");
                        if (compactLoot)
                        {
                            SaveSimulationReport("loot-feed-regression", RunLootChecks());
                            SaveSimulationReport("loot-feed-simulation", LootFeedAcceptance.RunSimulation(SessionState.GetString(OutputKey, "")));
                            _lootLifetime = LootFeedAcceptance.VerifyUnscaledLifetime(report => SaveSimulationReport("loot-feed-lifetime", report));
                            Routines.Push(_lootLifetime);
                            EditorApplication.update += TickLootLifetime;
                            awaitingLifetime = true;
                            return;
                        }
                        if (preview)
                        {
                            _lootLifetime = LootFeedAcceptance.Preview(SessionState.GetString(OutputKey, ""));
                            Routines.Push(_lootLifetime);
                            EditorApplication.update += TickLootLifetime;
                            awaitingLifetime = true;
                            return;
                        }
                        if (simulation)
                        {
                            RunUiSimulationChecks();
                            _lootLifetime = RunUiSimulationTiming();
                            Routines.Push(_lootLifetime);
                            EditorApplication.update += TickLootLifetime;
                            awaitingLifetime = true;
                            return;
                        }
                        var report = loot ? RunLootChecks() : commerceFailure ? RunCommerceChecks() : offlineFailure ? OfflineRewardAdAcceptance.RunFailureRecovery() :
                            questFailure ? QuestTabAcceptance.RunClaimFailureRecovery() : QuestTabAcceptance.Run();
                        File.WriteAllText(Path.Combine(SessionState.GetString(OutputKey, ""), filePrefix + "-acceptance.json"),
                            JsonConvert.SerializeObject(report, Formatting.Indented));
                        Debug.Log((loot ? "LOOT FEED PLAY ACCEPTANCE PASSED " : commerceFailure ? "BRONZE COMMERCE FAILURE RECOVERY PLAY ACCEPTANCE PASSED " : offlineFailure ? "BRONZE OFFLINE FAILURE RECOVERY PLAY ACCEPTANCE PASSED " :
                            questFailure ? "BRONZE QUEST FAILURE RECOVERY PLAY ACCEPTANCE PASSED " : "BRONZE QUEST PLAY ACCEPTANCE PASSED ") + report["passed"]);
                        if (loot)
                        {
                            _lootLifetime = LootFeedAcceptance.VerifyUnscaledLifetime(lifetime =>
                            {
                                File.WriteAllText(Path.Combine(SessionState.GetString(OutputKey, ""), "loot-feed-lifetime.json"),
                                    JsonConvert.SerializeObject(lifetime, Formatting.Indented));
                                Debug.Log("LOOT FEED UNSCALED LIFETIME PASSED " + lifetime["passed"]);
                            });
                            Routines.Push(_lootLifetime);
                            EditorApplication.update += TickLootLifetime;
                            awaitingLifetime = true;
                        }
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
                    finally { if (!awaitingLifetime) EditorApplication.isPlaying = false; }
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

        private static void TickLootLifetime()
        {
            try
            {
                if (_wait != null && _wait.keepWaiting) return;
                _wait = null;
                while (Routines.Count > 0)
                {
                    var routine = Routines.Peek();
                    if (!routine.MoveNext()) { (routine as IDisposable)?.Dispose(); Routines.Pop(); continue; }
                    if (routine.Current is CustomYieldInstruction wait) { _wait = wait; return; }
                    if (routine.Current is System.Collections.IEnumerator nested) { Routines.Push(nested); continue; }
                    return;
                }
            }
            catch (Exception error)
            {
                SessionState.SetBool(Failed, true);
                File.WriteAllText(Path.Combine(SessionState.GetString(OutputKey, ""), "loot-feed-lifetime-failure.txt"), error.ToString());
                Debug.LogException(error);
            }
            EditorApplication.update -= TickLootLifetime;
            while (Routines.Count > 0) (Routines.Pop() as IDisposable)?.Dispose();
            _lootLifetime = null;
            _wait = null;
            EditorApplication.isPlaying = false;
        }

        private static void RunUiSimulationChecks()
        {
            string output = SessionState.GetString(OutputKey, "");
            SaveSimulationReport("async-equipment-ui", AsyncEquipmentUiAcceptance.Run());
            SaveSimulationReport("equipment-cell-presentation", EquipmentCellPresentationAcceptance.Run());
            SaveSimulationReport("loot-feed-simulation", LootFeedAcceptance.RunSimulation(output));
            SaveSimulationReport("equipment-gallery", EquipmentCellPresentationAcceptance.Snapshot(Path.Combine(output, "equipment-cells.png")));
            SaveSimulationReport("loot-feed-regression", RunLootChecks());
            SaveSimulationReport("quest-cell-regression", QuestTabAcceptance.Run());
        }

        private static System.Collections.IEnumerator RunUiSimulationTiming()
        {
            yield return SettingsInteractionAcceptance.Run(report => SaveSimulationReport("settings-interaction", report));
            yield return LootFeedAcceptance.VerifyUnscaledLifetime(report => SaveSimulationReport("loot-feed-lifetime", report));
        }

        private static void SaveSimulationReport(string name, object report)
        {
            File.WriteAllText(Path.Combine(SessionState.GetString(OutputKey, ""), name + ".json"),
                JsonConvert.SerializeObject(report, Formatting.Indented));
            var json = Newtonsoft.Json.Linq.JObject.FromObject(report);
            int failures = (json["failures"] as Newtonsoft.Json.Linq.JArray)?.Count ?? 0;
            if (failures > 0) SessionState.SetBool(Failed, true);
            Debug.Log($"UI SIMULATION {name}: passed={json["passed"]}, failures={failures}");
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

        private static System.Collections.Generic.Dictionary<string, object> RunLootChecks()
        {
            var rewards = KingdomIdle.Rewards.LootNotificationsAcceptance.Run();
            var settings = LootNotificationSettingsAcceptance.Run();
            var view = LootFeedAcceptance.Run();
            return new System.Collections.Generic.Dictionary<string, object>
            {
                { "passed", (int)rewards["passed"] + (int)settings["passed"] + (int)view["passed"] },
                { "rewards", rewards }, { "settings", settings }, { "view", view }
            };
        }
    }
}
