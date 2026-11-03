using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using Direction;
using KingdomIdle.Balance;
using KingdomIdle.UI;
using Newtonsoft.Json;
using PlayFab;
using Protocol.Message.Dto;
using Scripts.Core.Manager;
using Scripts.Server.Auth;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace KingdomIdle.UGUI.Editor
{
    [InitializeOnLoad]
    public static class NewPlayerGuideAcceptance
    {
        const string RunKey = "NewPlayerGuideAcceptance.Run";
        const string BatchKey = "NewPlayerGuideAcceptance.Batch";
        const string SceneKey = "NewPlayerGuideAcceptance.Scene";
        const string ExitKey = "NewPlayerGuideAcceptance.Exit";
        static readonly string[] Ids = { "guide_development", "guide_kingdom_army", "guide_dungeon", "guide_gacha", "guide_mage_tower" };
        static readonly List<string> Checks = new();
        static readonly HashSet<string> Snapshots = new();
        static string Output => Environment.GetEnvironmentVariable("TUTORIAL_QA_OUTPUT") ?? "C:/GitHub/QA/TutorialStartup-20261103";

        static NewPlayerGuideAcceptance()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (!SessionState.GetBool(RunKey, false)) return;
                if (state == PlayModeStateChange.EnteredPlayMode) RunAsync().Forget();
                if (state != PlayModeStateChange.EnteredEditMode) return;
                SessionState.SetBool(RunKey, false);
                string scene = SessionState.GetString(SceneKey, "");
                if (!string.IsNullOrEmpty(scene)) EditorSceneManager.OpenScene(scene);
                if (SessionState.GetBool(BatchKey, false)) EditorApplication.Exit(SessionState.GetInt(ExitKey, 1));
            };
        }

        [MenuItem("KingdomIdle/Direction/Validate new player startup")]
        public static void Begin() => Start(false);
        public static void BeginBatch() => Start(true);

        static void Start(bool batch)
        {
            if (EditorApplication.isPlaying || SceneManager.sceneCount != 1 || SceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("저장된 단일 편집 씬에서 실행하세요.");
            SessionState.SetString(SceneKey, SceneManager.GetActiveScene().path);
            SessionState.SetBool(BatchKey, batch);
            SessionState.SetBool(RunKey, true);
            SessionState.SetInt(ExitKey, 1);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (!batch) EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Show();
            EditorApplication.isPlaying = true;
        }

        static void Check(bool value, string label)
        {
            if (!value) throw new InvalidOperationException(label);
            Checks.Add(label);
        }

        static async UniTask Until(Func<bool> condition, string label)
        {
            float deadline = Time.realtimeSinceStartup + 10;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > deadline) throw new TimeoutException(label);
                await UniTask.NextFrame();
            }
        }

        static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        static void Call(object target, string method, params object[] args) =>
            target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);

        static void Login(NetworkManager network, string account, bool ready)
        {
            network.BeginAuthentication();
            // A null SDK context rejects every cloud call locally, including signup probes below.
            network.SetAuthenticatedAccount("balance-qa-" + account, "isolated-acceptance", null);
            LocalProgression.OpenTestAccount(account);
            Snapshots.Add(LocalProgression.SnapshotPath);
            if (ready) Ready(network);
        }

        static void Ready(NetworkManager network)
        {
            network.SetGameSession("isolated-acceptance");
            network.CompleteLogin();
        }

        static GameDirectManager Manager(GameDirectSequenceSO[] definitions, TestPlayer player = null)
        {
            var manager = new GameObject("StartupAcceptance_Manager", typeof(GameDirectManager)).GetComponent<GameDirectManager>();
            Set(manager, "sequences", definitions);
            if (player != null)
            {
                ((IGameDirectPlayer)typeof(GameDirectManager).GetField("_player", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager)).Dispose();
                Set(manager, "_player", player);
            }
            return manager;
        }

        static async UniTask Stop(GameDirectManager manager)
        {
            manager.enabled = false;
            await Until(() => manager.ActiveSequenceId == null, "Cancelled guide did not stop");
            Object.Destroy(manager.gameObject);
            await UniTask.NextFrame();
        }

        static GameDirectProgress Read(GameDirectProgressStore store, string id)
        {
            if (!store.TryLoad(id, LocalProgression.AccountGeneration, out var progress)) throw new InvalidOperationException("Cannot read " + id);
            return progress;
        }

        static async UniTask RunAsync()
        {
            Checks.Clear(); Snapshots.Clear();
            string failure = null;
            string run = "startup-" + Guid.NewGuid().ToString("N");
            IDisposable session = null;
            GameDirectManager manager = null;
            GameObject networkRoot = null, uiRoot = null, events = null, enhancement = null;
            bool background = Application.runInBackground;
            try
            {
                Directory.CreateDirectory(Output);
                if (LocalProgression.IsReady || NetworkManager.Instance != null || UIManager.Instance != null)
                    throw new InvalidOperationException("운영 계정이나 게임 관리자가 없는 빈 PlayMode 씬이 필요합니다.");
                Application.runInBackground = true;
                session = LocalProgression.BeginTestSession();
                networkRoot = new GameObject("StartupAcceptance_Network", typeof(NetworkManager));
                var network = networkRoot.GetComponent<NetworkManager>();
                var store = new GameDirectProgressStore();
                var definitions = Ids.Select(id => AssetDatabase.LoadAssetAtPath<GameDirectSequenceSO>("Assets/_Project/Data/Direction/" + id + ".asset")).ToArray();
                Check(definitions.All(x => x != null && x.TryValidate(out _)), "Five production guide assets validate");
                var player = new TestPlayer();
                manager = Manager(definitions, player);

                Login(network, run + "-existing", true);
                manager.RequestNewPlayerGuides();
                Check(player.Shown.Count == 0 && manager.PendingCount == 0, "Existing account without enrollment does not start");
                Check(!store.TryEnrollNewPlayer("") && !store.TryEnrollNewPlayer("another-account"), "Enrollment rejects missing and mismatched identity");
                var auth = new Authentication(network);
                Call(auth, "InitializeNewAccount");
                Check(!store.IsNewPlayerEnrolled(LocalProgression.AccountGeneration), "Non-Google signup does not enroll");

                Login(network, run + "-new", false);
                Set(auth, "_googleLogin", true);
                Call(auth, "InitializeNewAccount");
                Check(store.IsNewPlayerEnrolled(LocalProgression.AccountGeneration), "Google signup durably enrolls before cloud initialization");
                Check(NetworkManager.GetFailureKind(network.LastFailure) == "NotAuthenticated", "Null SDK context prevents signup network request");
                manager.RequestNewPlayerGuides();
                Check(player.Shown.Count == 0, "Enrollment alone cannot start before login Ready");
                Login(network, run + "-new", true);
                Check(store.IsNewPlayerEnrolled(LocalProgression.AccountGeneration), "Enrollment survives exit before first display");
                manager.RequestNewPlayerGuides();
                await Until(() => player.Current != null, "First guide did not start");
                Check(manager.ActiveSequenceId == Ids[0] && player.Current.Id == definitions[0].steps[0].id && manager.PendingCount == 4, "New login requests the five guides in order");
                manager.RequestNewPlayerGuides();
                Check(player.Shown.Count == 1 && manager.PendingCount == 4, "Duplicate main entry within one login is ignored");
                player.Confirm();
                await Until(() => player.Current?.Id == definitions[0].steps[1].id, "First confirmation did not advance");
                Check(Read(store, Ids[0]).ConfirmedSteps.SequenceEqual(new[] { definitions[0].steps[0].id }), "Only the confirmed step is saved");
                await Stop(manager); manager = null;
                Login(network, run + "-new", true);
                player = new TestPlayer(); manager = Manager(definitions, player);
                manager.RequestNewPlayerGuides();
                await Until(() => player.Current != null, "Reconnected guide did not start");
                Check(player.Current.Id == definitions[0].steps[1].id && !Read(store, Ids[0]).Skipped, "Recreated manager resumes first unconfirmed step after disk reload");
                long stale = LocalProgression.AccountGeneration;
                Login(network, run + "-other", true);
                await Until(() => manager.ActiveSequenceId == null, "Account switch did not cancel");
                manager.RequestNewPlayerGuides();
                Check(manager.PendingCount == 0 && player.Current == null && !store.IsNewPlayerEnrolled(stale), "Account switch clears pending guides and rejects stale enrollment generation");

                Login(network, run + "-new", true);
                // Economy is outside this test: pre-confirm only the two server reward steps.
                Check(store.TrySave(Ids[3], LocalProgression.AccountGeneration, new GameDirectProgress {
                    ConfirmedSteps = definitions[3].steps.Where(x => x.grantPracticeCoins).Select(x => x.id).ToList()
                }), "Server reward steps isolated from local startup test");
                manager.RequestNewPlayerGuides();
                int limit = definitions.Sum(x => x.steps.Length) + 1;
                while (manager.ActiveSequenceId != null && limit-- > 0)
                {
                    await Until(() => player.Current != null || manager.ActiveSequenceId == null, "Guide player stalled");
                    player.Confirm();
                    await UniTask.NextFrame();
                }
                Check(Ids.All(id => Read(store, id).Completed), "All five guide completion states persist");
                await Stop(manager); manager = null;
                Login(network, run + "-new", true);
                player = new TestPlayer(); manager = Manager(definitions, player);
                manager.RequestNewPlayerGuides();
                Check(player.Shown.Count == 0 && manager.PendingCount == 0, "Completed account remains silent after new login and manager recreation");

                Login(network, run + "-failure", false);
                bool enrolled = LocalProgression.TestFailedCommit(() => { Call(auth, "InitializeNewAccount"); return store.IsNewPlayerEnrolled(LocalProgression.AccountGeneration); });
                Check(!enrolled && NetworkManager.GetFailureKind(network.LastFailure) == "ApplyFailed", "Enrollment write failure stops signup before cloud call");
                LocalProgression.OpenTestAccount(run + "-failure");
                Check(!store.IsNewPlayerEnrolled(LocalProgression.AccountGeneration), "Failed enrollment does not appear after disk reload");
                var uninitialized = new PlayFabError { CustomData = new ErrorResponseDTO { ErrorCode = Protocol.Message.Data.eErrorCode.PlayerNotInitialized } };
                Call(auth, "OnLoginFailed", uninitialized);
                Check(store.IsNewPlayerEnrolled(LocalProgression.AccountGeneration), "Google PlayerNotInitialized recovery enrolls before retrying signup");
                Login(network, run + "-custom-recovery", false);
                Set(auth, "_googleLogin", false);
                Call(auth, "OnLoginFailed", uninitialized);
                Check(!store.IsNewPlayerEnrolled(LocalProgression.AccountGeneration), "Non-Google recovery does not enroll");

                await Stop(manager); manager = null;
                Login(network, run + "-ui", true);
                Check(store.TryEnrollNewPlayer(network.PlayFabId), "UI smoke account enrolled");
                uiRoot = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UGUI/Prefabs/UGUI_UIRoot.prefab"));
                events = new GameObject("StartupAcceptance_EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                enhancement = new GameObject("StartupAcceptance_Enhancement", typeof(StatEnhanceManager));
                var ui = uiRoot.GetComponent<UIManager>();
                ui.ReplaceScreen(UIScreenId.Main);
                manager = Manager(definitions);
                manager.RequestNewPlayerGuides();
                await Until(() => ui.ActiveFeatureGuide != null && ui.ActiveFeatureGuide.IsVisible, "Production guide overlay did not appear");
                await UniTask.NextFrame(); Canvas.ForceUpdateCanvases();
                Check(ui.ActiveFeatureGuide.titleLabel.text == definitions[0].steps[0].title, "Production UGUI overlay automatically displays first guide");
                Check(ui.GuideTargets.TryGet(GameDirectTarget.Development, out var target), "Development target exists");
                Click(target);
                await Until(() => GameDirectInteraction.Step?.Id == definitions[0].steps[1].id && ui.ActiveFeatureGuide.IsVisible, "Development click did not reach gold explanation");
                Check(Read(store, Ids[0]).ConfirmedSteps.Contains(definitions[0].steps[0].id), "Real UI click saves first step");
                await Stop(manager); manager = null;
                ui.FinishFeatureGuide();
                Login(network, run + "-ui", true);
                manager = Manager(definitions);
                manager.RequestNewPlayerGuides();
                await Until(() => ui.ActiveFeatureGuide != null && ui.ActiveFeatureGuide.IsVisible && GameDirectInteraction.Step?.Id == definitions[0].steps[1].id, "Production UI resume did not restore gold explanation");
                Check(ui.ActiveFeatureGuide.titleLabel.text == definitions[0].steps[1].title, "Production UI restores the unconfirmed step and its panel after reconnect");
            }
            catch (Exception error) { failure = error.ToString(); Debug.LogException(error); }
            finally
            {
                if (manager != null) await Stop(manager);
                if (uiRoot != null) Object.Destroy(uiRoot);
                if (events != null) Object.Destroy(events);
                if (enhancement != null) Object.Destroy(enhancement);
                if (networkRoot != null) Object.Destroy(networkRoot);
                await UniTask.NextFrame(); await UniTask.NextFrame();
                session?.Dispose();
                foreach (string snapshot in Snapshots)
                    foreach (string suffix in new[] { "", ".bak", ".tmp" })
                        if (File.Exists(snapshot + suffix)) File.Delete(snapshot + suffix);
                Application.runInBackground = background;
                File.WriteAllText(Path.Combine(Output, "startup-acceptance.json"), JsonConvert.SerializeObject(new {
                    passed = failure == null, unity = Application.unityVersion, checks = Checks, failure,
                    scope = "Isolated PlayMode, fake authentication context, real disk reopen and production guide assets. UI smoke uses production UGUI and raycast input. No Google SDK, server economy, Android process kill, or device validation."
                }, Formatting.Indented));
                SessionState.SetInt(ExitKey, failure == null ? 0 : 1);
                Debug.Log("[NewPlayerGuideAcceptance] " + (failure == null ? "PASSED " : "FAILED ") + Checks.Count);
                EditorApplication.isPlaying = false;
            }
        }

        static void Click(RectTransform target)
        {
            var data = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(null, target.TransformPoint(target.rect.center)), button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(data, hits);
            if (hits.Count == 0 || !ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, data, ExecuteEvents.pointerClickHandler))
                throw new InvalidOperationException("Guide target did not receive a raycast click");
        }

        sealed class TestPlayer : IGameDirectPlayer
        {
            public readonly List<string> Shown = new();
            public GameDirectStep Current { get; private set; }
            UniTaskCompletionSource<GameDirectResult> _response;
            public async UniTask<GameDirectResult> PlayAsync(GameDirectStep step, CancellationToken token)
            {
                Current = step; Shown.Add(step.Id);
                var response = _response = new UniTaskCompletionSource<GameDirectResult>();
                try { return await response.Task.AttachExternalCancellation(token); }
                finally { if (_response == response) { _response = null; Current = null; } }
            }
            public void Confirm() => _response?.TrySetResult(GameDirectResult.Confirmed);
            public void Dispose() { _response?.TrySetCanceled(); Current = null; }
        }
    }
}
