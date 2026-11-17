using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using KingdomIdle.Balance;
using Newtonsoft.Json;
using Scripts.Core;
using Scripts.Core.Manager;
using Scripts.Core.Utils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>실제 오버레이 프리팹의 시간·입력·정리 경계를 격리 PlayMode에서 검사한다.</summary>
    [InitializeOnLoad]
    public static class GameplayLoadingAcceptance
    {
        private const string ActiveKey = "GameplayLoadingAcceptance.Active";
        private const string ScenesKey = "GameplayLoadingAcceptance.Scenes";
        private const string OutputKey = "GameplayLoadingAcceptance.Output";
        private const BindingFlags Members = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        private static readonly Stack<IEnumerator> Routines = new();
        private static readonly List<object> Checks = new(), Timeline = new(), Captures = new();
        private static CustomYieldInstruction _wait;
        private static Fixture _fixture;
        private static string _output, _failure;
        private static float _started;
        [Serializable] private sealed class Scenes { public SceneSetup[] items; }

        static GameplayLoadingAcceptance() => EditorApplication.playModeStateChanged += OnPlayModeChanged;

        public static void Start(string outputDirectory)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit PlayMode before the isolated loading acceptance run.");
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save the edited scenes before the loading acceptance run.");
            string output = Path.GetFullPath(outputDirectory ?? throw new ArgumentNullException(nameof(outputDirectory)));
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (output.Equals(project, StringComparison.OrdinalIgnoreCase) || output.StartsWith(project + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Loading QA output must be outside the project.");
            Directory.CreateDirectory(output);
            SessionState.SetString(OutputKey, output);
            SessionState.SetString(ScenesKey, JsonUtility.ToJson(new Scenes { items = EditorSceneManager.GetSceneManagerSetup() }));
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool(ActiveKey, true);
            EditorApplication.isPlaying = true;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                EditorApplication.delayCall += () =>
                {
                    _output = SessionState.GetString(OutputKey, "");
                    Checks.Clear(); Timeline.Clear(); Captures.Clear(); _failure = null;
                    _started = Time.realtimeSinceStartup;
                    Routines.Push(Run());
                    EditorApplication.update += Tick;
                };
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                if (Routines.Count > 0) Finish("PlayMode was stopped before loading acceptance completed.");
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(ActiveKey, false);
                var previous = JsonUtility.FromJson<Scenes>(SessionState.GetString(ScenesKey, "{}"));
                if (previous?.items?.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(previous.items);
            }
        }

        private static void Tick()
        {
            try
            {
                _fixture?.Refresh();
                if (_wait != null && _wait.keepWaiting) return;
                _wait = null;
                while (Routines.Count > 0)
                {
                    var routine = Routines.Peek();
                    if (!routine.MoveNext()) { (routine as IDisposable)?.Dispose(); Routines.Pop(); continue; }
                    if (routine.Current is CustomYieldInstruction wait) { _wait = wait; return; }
                    if (routine.Current is IEnumerator nested) { Routines.Push(nested); continue; }
                    return;
                }
                Finish(null);
            }
            catch (Exception error) { Finish(error.ToString()); }
        }

        private static void Finish(string failure)
        {
            EditorApplication.update -= Tick;
            if (failure != null) _failure = failure;
            while (Routines.Count > 0)
            {
                try { (Routines.Pop() as IDisposable)?.Dispose(); }
                catch (Exception error) { _failure = (_failure ?? "") + "\nCleanup: " + error; }
            }
            _wait = null;
            WriteReport();
            if (Application.isPlaying) EditorApplication.isPlaying = false;
        }

        private static IEnumerator Run()
        {
            if (!Application.isPlaying || LocalProgression.IsReady || NetworkManager.Instance != null || UserManager.Instance != null ||
                UIManager.Instance != null || StageManager.Instance != null || EventSystem.current != null)
                throw new InvalidOperationException("Run in empty-scene PlayMode without an account, managers, or EventSystem.");
            float previousScale = Time.timeScale;
            bool previousBackground = Application.runInBackground;
            string previousAccount = LocalProgression.AccountKey;
            long previousGeneration = LocalProgression.AccountGeneration;
            GameplayLoading.Reset();
            try
            {
                Application.runInBackground = true;
                _fixture = new Fixture();
                var gameView = EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));
                gameView.Show(); gameView.Focus();
                File.WriteAllText(Path.Combine(_output, "ui-fixture-ready.txt"),
                    "Actual catalog loading prefabs in isolated Editor Game View; UI fixture only. No login, server request, profile, or rewards.");
                yield return Capture("00-before");

                bool requestPending = true;
                var background = new GameObject("BackgroundAction", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
                background.transform.SetParent(_fixture.Host.LayerOverlays, false);
                int backgroundClicks = 0;
                background.GetComponent<UnityEngine.UI.Button>().onClick.AddListener(() => backgroundClicks++);
                EventSystem.current.SetSelectedGameObject(background);
                var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
                ExecuteEvents.Execute(background, pointer, ExecuteEvents.pointerDownHandler);
                var longPressObject = new GameObject("TowerTouch", typeof(RectTransform), typeof(UnityEngine.UI.Button), typeof(UILongPressButton));
                longPressObject.transform.SetParent(_fixture.Host.LayerOverlays, false);
                var longPress = longPressObject.GetComponent<UILongPressButton>();
                int towerTaps = 0, towerHolds = 0;
                longPress.Tapped += () => towerTaps++;
                longPress.LongPressed += () => towerHolds++;
                longPress.OnPointerDown(pointer);
                var input = _fixture.Host.BeginRewardRequest("ui-fixture-reward", () => requestPending);
                Check(GameplayLoading.IsInputBlocked, "reward click owns input in the same frame");
                Check(!_fixture.Circle.indicator.gameObject.activeSelf, "immediate input block does not flash the delayed spinner");
                ExecuteEvents.Execute(background, pointer, ExecuteEvents.pointerUpHandler);
                ExecuteEvents.Execute(background, pointer, ExecuteEvents.pointerClickHandler);
                ExecuteEvents.Execute(background, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
                Check(backgroundClicks == 0 && EventSystem.current.currentSelectedGameObject == null,
                    "a pointer already pressed and keyboard submit cannot trigger the covered action");
                longPress.OnPointerUp(pointer);
                Check(towerTaps == 0, "a tower touch started before the reward cannot open a popup on release");
                int inputFrame = Time.frameCount;
                yield return new WaitUntil(() => Time.frameCount > inputFrame);
                Check(_fixture.RaycastIsBlocked(), "the next rendered frame routes pointers to the transparent blocker");
                Check(Time.timeScale == previousScale, "input ownership does not stop or rescale battle time");
                requestPending = false;
                int shown = 0;
                Action closeFirst = null, closeSecond = null;
                GameObject Result(Action closed)
                {
                    shown++;
                    if (shown == 1) closeFirst = closed; else closeSecond = closed;
                    var result = new GameObject("QueuedReward", typeof(RectTransform));
                    result.transform.SetParent(_fixture.Host.LayerOverlays, false);
                    ModalBackHandler.Bind(result, closed);
                    return result;
                }
                using var backgroundWait = GameplayLoading.Begin("ui-fixture-background-stage");
                _fixture.Host.CompleteRewardRequest(input, Result);
                _fixture.Refresh();
                Check(shown == 1, "unrelated background stage work does not delay a ready reward result");
                backgroundWait.Dispose();
                var next = _fixture.Host.BeginRewardRequest("ui-fixture-next-reward", () => false);
                _fixture.Host.CompleteRewardRequest(next, Result);
                Check(shown == 1 && GameplayLoading.IsInputBlocked, "overlapping results queue without replacing the first result");
                int unrelatedCloses = 0;
                var unrelated = new GameObject("AutomaticDungeonResult", typeof(RectTransform));
                unrelated.transform.SetParent(_fixture.Host.LayerOverlays, false);
                ModalBackHandler.Bind(unrelated, () => unrelatedCloses++);
                _fixture.Host.RequestBack();
                closeFirst();
                Check(shown == 2 && GameplayLoading.IsInputBlocked, "closing a result hands input ownership directly to the next result");
                Check(unrelatedCloses == 0, "back closes the owned reward even when a dungeon modal arrived above it");
                Object.DestroyImmediate(unrelated);
                closeSecond(); closeSecond();
                Check(!GameplayLoading.IsInputBlocked && !_fixture.RaycastIsBlocked(), "last result and duplicate close release input exactly once");
                longPress.longPressSeconds = 0f;
                longPress.OnPointerDown(pointer);
                using (var holdBlock = _fixture.Host.BeginRewardRequest("ui-fixture-tower-hold", () => true))
                {
                    typeof(UILongPressButton).GetMethod("Update", Members).Invoke(longPress, null);
                    longPress.OnPointerUp(pointer);
                    longPress.OnPointerDown(pointer);
                }
                _fixture.Refresh();
                longPress.OnPointerUp(pointer);
                Check(towerHolds == 0 && towerTaps == 0, "covered tower hold and new covered press cannot change AUTO or leave a deferred tap");
                longPress.OnPointerDown(pointer); longPress.OnPointerUp(pointer);
                Check(towerTaps == 1, "a new tower touch works after reward input ownership ends");
                var missing = _fixture.Host.BeginRewardRequest("ui-fixture-missing-result", () => false);
                _fixture.Host.CompleteRewardRequest(missing, _ => null);
                Check(!GameplayLoading.IsInputBlocked, "missing result prefab cannot retain input ownership");
                var broken = _fixture.Host.BeginRewardRequest("ui-fixture-broken-result", () => false);
                _fixture.Host.CompleteRewardRequest(broken, _ => throw new InvalidOperationException("Expected reward renderer fixture failure"));
                Check(!GameplayLoading.IsInputBlocked, "a result callback exception cannot retain input ownership");
                var stale = _fixture.Host.BeginRewardRequest("ui-fixture-old-result", () => false);
                _fixture.Host.CompleteRewardRequest(stale, Result);
                GameplayLoading.Reset(); _fixture.Refresh();
                Check(!GameplayLoading.IsInputBlocked && !_fixture.RaycastIsBlocked(), "account reset discards the old result and its input ownership");
                Object.DestroyImmediate(background);
                Object.DestroyImmediate(longPressObject);

                using (var fast = GameplayLoading.Begin("ui-fixture-fast"))
                {
                    Mark("fast-start");
                    yield return new WaitForSecondsRealtime(.1f);
                    _fixture.Refresh();
                    Check(fast.ElapsedSeconds < GameplayLoading.DelaySeconds && !_fixture.CircleVisible, "fast work does not display loading");
                }
                _fixture.Refresh(); Check(!_fixture.Host.IsLoadingVisible, "fast completion leaves no input blocker");

                using (var delayed = GameplayLoading.Begin("ui-fixture-delayed-success"))
                {
                    Mark("delayed-success-start");
                    yield return new WaitForSecondsRealtime(.35f);
                    _fixture.Refresh();
                    Check(_fixture.CircleVisible && !_fixture.SceneVisible, "0.3 seconds of real wait shows only circular loading");
                    Check(_fixture.RaycastIsBlocked(), "visible circular loading intercepts pointer input");
                    yield return Capture("01-delayed-wait");
                }
                _fixture.Refresh(); Check(!_fixture.Host.IsLoadingVisible && !_fixture.RaycastIsBlocked(), "success releases visual and pointer blocker");
                Mark("delayed-success-finished");
                yield return Capture("02-after-success");

                using (var failed = GameplayLoading.Begin("ui-fixture-delayed-failure"))
                {
                    Mark("delayed-failure-start");
                    yield return new WaitForSecondsRealtime(.35f);
                    _fixture.Refresh(); Check(_fixture.CircleVisible, "failure case was visibly waiting");
                    yield return Capture("03-before-failure-release");
                }
                _fixture.Refresh(); Check(!_fixture.Host.IsLoadingVisible, "failed scope cleanup releases loading");
                Mark("delayed-failure-finished");
                yield return Capture("04-after-failure-release");

                using (var paused = GameplayLoading.Begin("ui-fixture-normal-presentation"))
                {
                    yield return new WaitForSecondsRealtime(.1f);
                    float beforePause = paused.ElapsedSeconds;
                    paused.Pause(); Mark("normal-presentation-start");
                    yield return new WaitForSecondsRealtime(.6f);
                    _fixture.Refresh(); Check(!_fixture.CircleVisible && Mathf.Abs(paused.ElapsedSeconds - beforePause) < .02f, "paused normal presentation adds no loading time");
                    paused.Resume(); Mark("actual-wait-after-presentation");
                    yield return new WaitForSecondsRealtime(.1f);
                    _fixture.Refresh(); Check(!_fixture.CircleVisible, "resumed wait stays hidden below the delay");
                    yield return new WaitForSecondsRealtime(.15f);
                    _fixture.Refresh(); Check(_fixture.CircleVisible, "resumed actual wait displays after accumulated 0.3 seconds");
                }

                using (var held = GameplayLoading.Begin("ui-fixture-not-before", Time.realtimeSinceStartup + .5f))
                {
                    Mark("not-before-start");
                    yield return new WaitForSecondsRealtime(.35f);
                    _fixture.Refresh(); Check(!_fixture.CircleVisible && held.ElapsedSeconds == 0f, "notBefore excludes scheduled presentation hold");
                    yield return new WaitForSecondsRealtime(.5f);
                    _fixture.Refresh(); Check(_fixture.CircleVisible, "notBefore wait becomes visible after hold plus 0.3 seconds");
                }

                using (var first = GameplayLoading.Begin("ui-fixture-first-owner"))
                using (var second = GameplayLoading.Begin("ui-fixture-second-owner"))
                {
                    yield return new WaitForSecondsRealtime(.35f);
                    first.Dispose(); first.Dispose(); _fixture.Refresh();
                    Check(_fixture.CircleVisible, "one completion and duplicate Dispose cannot close another owner's loading");
                    second.Dispose(); _fixture.Refresh(); Check(!_fixture.Host.IsLoadingVisible, "last owner completion releases loading");
                }

                using (var pending = GameplayLoading.Begin("ui-fixture-scene-priority"))
                {
                    yield return new WaitForSecondsRealtime(.35f);
                    _fixture.Host.SetLoading(true, "Loading..."); _fixture.Host.SetLoadingProgress(.42f); _fixture.Refresh();
                    Check(_fixture.SceneVisible && !_fixture.CircleVisible && Mathf.Approximately(_fixture.Scene.progressBar.value, .42f),
                        "scene progress remains visible and takes precedence over circular loading");
                    Mark("scene-progress-priority"); yield return Capture("05-scene-progress-priority");
                    _fixture.Host.SetLoading(false); _fixture.Refresh();
                    Check(!_fixture.SceneVisible && _fixture.CircleVisible, "remaining operation resumes circular loading after scene overlay closes");
                    yield return Capture("06-operation-after-scene");
                }

                using (var unscaled = GameplayLoading.Begin("ui-fixture-unscaled"))
                {
                    Time.timeScale = 0f;
                    yield return new WaitForSecondsRealtime(.35f);
                    _fixture.Refresh(); Check(_fixture.CircleVisible, "loading appears while timeScale is zero");
                    Sprite before = _fixture.Circle.indicator.sprite;
                    yield return new WaitForSecondsRealtime(.15f);
                    Check(before != _fixture.Circle.indicator.sprite, "spinner changes frames while timeScale is zero");
                    Mark("timeScale-zero-spinner"); yield return Capture("07-timescale-zero");
                }
                Time.timeScale = previousScale;
                _fixture.Refresh(); Check(!_fixture.Host.IsLoadingVisible, "unscaled completion releases loading");

                var obsolete = GameplayLoading.Begin("ui-fixture-obsolete-account");
                yield return new WaitForSecondsRealtime(.35f);
                GameplayLoading.Reset(); _fixture.Refresh();
                Check(obsolete.IsDisposed && !_fixture.Host.IsLoadingVisible, "account Reset disposes pending display ownership");
                using (var current = GameplayLoading.Begin("ui-fixture-new-owner"))
                {
                    yield return new WaitForSecondsRealtime(.35f);
                    obsolete.Dispose(); _fixture.Refresh();
                    Check(_fixture.CircleVisible, "late obsolete Dispose cannot release a new owner's loading");
                }
                GameplayLoading.Reset(); _fixture.Refresh();
                Check(!_fixture.Host.IsLoadingVisible && !_fixture.RaycastIsBlocked(), "final cleanup leaves no overlay or input blocker");
                yield return Capture("08-final-clear");
            }
            finally
            {
                GameplayLoading.Reset();
                _fixture?.Dispose(); _fixture = null;
                Time.timeScale = previousScale;
                Application.runInBackground = previousBackground;
                Check(!LocalProgression.IsReady && LocalProgression.AccountKey == previousAccount &&
                    LocalProgression.AccountGeneration == previousGeneration && NetworkManager.Instance == null && UserManager.Instance == null &&
                    UIManager.Instance == null && StageManager.Instance == null, "fixture cleanup preserves unopened account, network, and scene managers");
            }
        }

        private static IEnumerator Capture(string name)
        {
            _fixture.Refresh(); Canvas.ForceUpdateCanvases();
            yield return null; yield return null;
            string file = Path.Combine(_output, name + ".png");
            if (File.Exists(file)) File.Delete(file);
            ScreenCapture.CaptureScreenshot(file);
            float deadline = Time.realtimeSinceStartup + 5f;
            while (!File.Exists(file) && Time.realtimeSinceStartup < deadline) yield return null;
            Check(File.Exists(file), "Game View screenshot saved: " + name);
            Captures.Add(new { path = file, classification = "UI fixture; actual catalog prefabs, no game account or live service request", width = Screen.width, height = Screen.height,
                elapsedRealtime = Time.realtimeSinceStartup - _started, frame = Time.frameCount });
            Mark("capture:" + name);
        }

        private static void Check(bool passed, string name)
        {
            Checks.Add(new { name, passed, elapsedRealtime = Time.realtimeSinceStartup - _started, frame = Time.frameCount });
            if (!passed) throw new InvalidOperationException("Gameplay loading acceptance: " + name);
        }

        private static void Mark(string name)
        {
            Timeline.Add(new { name, elapsedRealtime = Time.realtimeSinceStartup - _started, frame = Time.frameCount,
                sceneVisible = _fixture?.SceneVisible ?? false, circularVisible = _fixture?.CircleVisible ?? false, timeScale = Time.timeScale });
            WriteReport();
        }

        private static void WriteReport()
        {
            if (string.IsNullOrEmpty(_output)) return;
            File.WriteAllText(Path.Combine(_output, "ui-loading-acceptance.json"), JsonConvert.SerializeObject(new
            {
                passed = _failure == null && Routines.Count == 0,
                status = _failure != null ? "failed" : Routines.Count == 0 ? "complete" : "running",
                failure = _failure,
                classification = "Isolated Editor Game View UI fixture using actual catalog loading prefabs. Simulated scope completion only; service transport integration is tested separately.",
                unity = Application.unityVersion,
                delaySeconds = GameplayLoading.DelaySeconds,
                checks = Checks, timeline = Timeline, captures = Captures
            }, Formatting.Indented));
        }

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject _owner, _canvas, _camera, _events;
            private readonly MethodInfo _refresh = typeof(UIManager).GetMethod("RefreshLoading", Members);
            private readonly EventSystem _eventSystem;
            public readonly UIManager Host;
            public readonly LoadingOverlayView Scene;
            public readonly GameplayLoadingOverlayView Circle;
            public bool SceneVisible => Scene.gameObject.activeInHierarchy;
            public bool CircleVisible => Circle.gameObject.activeInHierarchy;

            public Fixture()
            {
                var catalog = AssetDatabase.LoadAssetAtPath<UIViewCatalog>("Assets/UGUI/UIViewCatalog.asset");
                if (catalog == null || catalog.overlayLoading == null || catalog.overlayGameplayLoading == null)
                    throw new InvalidOperationException("Both scene and gameplay loading prefabs must be serialized in UIViewCatalog.");
                try
                {
                _owner = new GameObject("GameplayLoadingAcceptance_Host"); _owner.SetActive(false);
                Host = _owner.AddComponent<UIManager>();
                typeof(UIManager).GetProperty("Instance", Members).SetValue(null, Host);
                typeof(UIManager).GetField("catalog", Members).SetValue(Host, catalog);
                _canvas = new GameObject("GameplayLoadingAcceptance_Canvas", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
                _canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = _canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
                scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080, 1920); scaler.matchWidthOrHeight = .5f;
                typeof(UIManager).GetField("layerOverlays", Members).SetValue(Host, (RectTransform)_canvas.transform);
                Scene = Object.Instantiate(catalog.overlayLoading, _canvas.transform, false).GetComponent<LoadingOverlayView>();
                Circle = Object.Instantiate(catalog.overlayGameplayLoading, _canvas.transform, false).GetComponent<GameplayLoadingOverlayView>();
                foreach (var rect in new[] { (RectTransform)Scene.transform, (RectTransform)Circle.transform })
                { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
                Scene.gameObject.SetActive(false); Circle.gameObject.SetActive(false);
                typeof(UIManager).GetField("_loading", Members).SetValue(Host, Scene);
                typeof(UIManager).GetField("_operationLoading", Members).SetValue(Host, Circle);
                _camera = new GameObject("GameplayLoadingAcceptance_Camera", typeof(Camera));
                var camera = _camera.GetComponent<Camera>(); camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.18f, .24f, .27f); camera.transform.position = new Vector3(0, 0, -10);
                _events = new GameObject("GameplayLoadingAcceptance_EventSystem", typeof(EventSystem));
                _eventSystem = _events.GetComponent<EventSystem>();
                Check(Circle.indicator != null && Circle.frames != null && Circle.frames.Length == 8, "circular prefab serializes the indicator and eight frames");
                Refresh();
                }
                catch { Dispose(); throw; }
            }

            public void Refresh() => _refresh.Invoke(Host, null);

            public bool RaycastIsBlocked()
            {
                Canvas.ForceUpdateCanvases();
                var hits = new List<RaycastResult>();
                _eventSystem.RaycastAll(new PointerEventData(_eventSystem) { position = new Vector2(Screen.width * .5f, Screen.height * .5f) }, hits);
                return hits.Count > 0 && (hits[0].gameObject.transform.IsChildOf(Circle.transform) || hits[0].gameObject.transform.IsChildOf(Scene.transform));
            }

            public void Dispose()
            {
                if (Host != null) Host.SetLoading(false);
                Object.DestroyImmediate(_canvas); Object.DestroyImmediate(_camera); Object.DestroyImmediate(_events);
                typeof(UIManager).GetProperty("Instance", Members).SetValue(null, null);
                Object.DestroyImmediate(_owner);
            }
        }
    }
}
