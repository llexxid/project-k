using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Direction;
using KingdomIdle.Balance;
using KingdomIdle.UI;
using Newtonsoft.Json;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>빈 PlayMode 씬에서 안내 미리보기·입력·중단을 검증한다. 서버 진행 저장·실습 경제는 검증하지 않는다.</summary>
    [InitializeOnLoad]
    public static class GameDirectAcceptance
    {
        private const string RunKey = "GameDirectAcceptance.Run";
        private const string SceneKey = "GameDirectAcceptance.Scene";
        private static string Output => Environment.GetEnvironmentVariable("TUTORIAL_PREVIEW_QA_OUTPUT") ?? "C:/GitHub/QA/TutorialPreview-20261104";
        private static readonly List<string> Checks = new();
        private static readonly List<object> Captures = new();

        /// <summary>도메인 재로드 뒤 테스트 시작 요청을 복원하고, PlayMode 종료 뒤 원래 편집 씬으로 돌아간다.</summary>
        static GameDirectAcceptance()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(RunKey, false)) RunAsync().Forget();
                if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(RunKey, false))
                {
                    SessionState.SetBool(RunKey, false);
                    string path = SessionState.GetString(SceneKey, "");
                    if (!string.IsNullOrEmpty(path)) EditorApplication.delayCall += () => EditorSceneManager.OpenScene(path);
                }
            };
        }

        /// <summary>미저장 편집이 없는 단일 씬에서 검사를 시작한다. 기존 저장 계정을 강제 지정하거나 덮어쓰지 않는다.</summary>
        [MenuItem("KingdomIdle/Direction/Validate isolated guide acceptance")]
        public static void Begin() => throw new InvalidOperationException("로컬 진행 저장 기반 검증은 중단되었습니다. 서버 계약은 KingdomIdle.Balance.ServerTutorialAcceptance.Run(), UI는 Validate isolated guide preview로 검증하세요.");

        [MenuItem("KingdomIdle/Direction/Validate isolated guide preview")]
        public static void BeginPreview()
        {
            if (EditorApplication.isPlaying || SceneManager.sceneCount != 1 || SceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("저장된 단일 씬의 편집 모드에서 실행하세요.");
            SessionState.SetString(SceneKey, SceneManager.GetActiveScene().path);
            SessionState.SetBool(RunKey, true);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // GameView가 렌더링되어야 GraphicRaycaster와 TMP 레이아웃의 실제 픽셀 입력을 검사할 수 있다.
            var gameView = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            EditorWindow.GetWindow(gameView).Show();
            EditorApplication.isPlaying = true;
        }

        /// <summary>실패한 검사는 라벨과 예외를 기록하고 즉시 중단한다. 성공한 검사의 수를 결과에 남긴다.</summary>
        private static void Check(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException("GameDirect acceptance: " + label);
            Checks.Add(label);
        }

        /// <summary>조건을 최대 8초 동안 프레임 단위로 기다린다. 실패가 무한 대기로 가려지지 않도록 제한한다.</summary>
        private static async UniTask Until(Func<bool> condition, string label)
        {
            float deadline = Time.realtimeSinceStartup + 8;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > deadline) throw new TimeoutException(label);
                await UniTask.NextFrame();
            }
        }

        /// <summary>화면·레이아웃 변경이 LateUpdate와 Canvas 재생성을 거치도록 두 프레임을 기다린다.</summary>
        private static async UniTask Settle()
        { await UniTask.NextFrame(); await UniTask.NextFrame(); Canvas.ForceUpdateCanvases(); }

        /// <summary>실제 EventSystem 레이캐스트의 첫 결과에 포인터 클릭을 보낸다. Button.onClick 직접 호출과 구분된다.</summary>
        private static GameObject Click(RectTransform target) => Click(target, out _);

        private static GameObject Click(RectTransform target, out string diagnostic)
        {
            var world = target.TransformPoint(target.rect.center);
            Vector2 point = RectTransformUtility.WorldToScreenPoint(null, world);
            var data = new PointerEventData(EventSystem.current) { position = point, button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(data, hits);
            var canvas = target.GetComponentInParent<Canvas>()?.rootCanvas;
            diagnostic = JsonConvert.SerializeObject(new
            {
                target = target.name, targetParent = target.parent?.name, rect = target.rect.ToString("F2"),
                world = world.ToString("F2"), point = point.ToString("F2"), screen = $"{Screen.width}x{Screen.height}",
                canvas = canvas?.name, renderMode = canvas?.renderMode.ToString(), camera = canvas?.worldCamera?.name,
                cameraPoint = RectTransformUtility.WorldToScreenPoint(canvas != null ? canvas.worldCamera : null, world).ToString("F2"),
                hits = hits.Take(8).Select(x => new { name = x.gameObject.name, parent = x.gameObject.transform.parent?.name,
                    module = x.module?.name, x.depth, x.sortingLayer, x.sortingOrder, x.distance }).ToArray()
            });
            if (hits.Count == 0) throw new InvalidOperationException("레이캐스트 결과 없음: " + diagnostic);
            var hit = hits[0].gameObject;
            ExecuteEvents.ExecuteHierarchy(hit, data, ExecuteEvents.pointerClickHandler);
            return hit;
        }

        /// <summary>공통 실행과 실 프리팹을 검사하고 모든 임시 객체·계정 컨텍스트를 finally에서 정리한다.</summary>
        private static async UniTask RunAsync()
        {
            Checks.Clear(); Captures.Clear();
            Directory.CreateDirectory(Output);
            string failure = null;
            GameObject root = null, managerRoot = null, events = null;
            IDisposable session = null;
            bool background = Application.runInBackground;
            string account = "game-direct-" + Guid.NewGuid().ToString("N");
            try
            {
                if (LocalProgression.IsReady || Scripts.Core.Manager.NetworkManager.Instance != null) throw new InvalidOperationException("운영 계정이나 네트워크 관리자가 없는 빈 PlayMode 씬이 필요합니다.");
                Application.runInBackground = true;
                session = LocalProgression.BeginTestSession();
                LocalProgression.OpenTestAccount(account);
                var menu = AssetDatabase.LoadAssetAtPath<GameDirectSequenceSO>($"{GameDirectPreparation.DataRoot}/{GameDirectPreparation.MenusId}.asset");
                var rebirth = AssetDatabase.LoadAssetAtPath<GameDirectSequenceSO>($"{GameDirectPreparation.DataRoot}/{GameDirectPreparation.ReincarnationId}.asset");
                Check(menu != null && menu.TryValidate(out _) && menu.steps.Length == 4, "Four-step authored menu definition");
                Check(rebirth != null && rebirth.TryValidate(out _) && rebirth.steps.Length == 1, "Single-step reincarnation definition");
                root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UGUI/Prefabs/UGUI_UIRoot.prefab"));
                var ui = root.GetComponent<UIManager>();
                events = new GameObject("GuideAcceptance_EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                managerRoot = new GameObject("GuideAcceptance_Manager", typeof(GameDirectManager));
                var manager = managerRoot.GetComponent<GameDirectManager>();
                ui.ReplaceScreen(UIScreenId.Main);
                await Settle();
                Check(ui.CanShowFeatureGuide, "Main screen ready without overlay: " + JsonConvert.SerializeObject(new {
                    ui.ActiveScreenId, ui.HasBlockingPanel, ui.HasActiveTabPanel, modal = ModalBackHandler.HasOpenModal,
                    popups = ui.LayerPopups.Cast<Transform>().Where(x => x.gameObject.activeInHierarchy).Select(x => x.name).ToArray(),
                    overlays = ui.LayerOverlays.Cast<Transform>().Where(x => x.gameObject.activeInHierarchy).Select(x => x.name).ToArray(),
                    modals = Object.FindObjectsByType<ModalBackHandler>(FindObjectsSortMode.None).Select(x => x.name).ToArray() }));
                foreach (GameDirectTarget target in new[] { GameDirectTarget.Development, GameDirectTarget.KingdomArmy, GameDirectTarget.Dungeon, GameDirectTarget.Gacha, GameDirectTarget.Reincarnation })
                    Check(ui.GuideTargets.TryGet(target, out _), "Registered live target: " + target);

                if (SessionState.GetBool("GameDirectAcceptance.InteractiveOnly", false))
                {
                    SessionState.SetBool("GameDirectAcceptance.InteractiveOnly", false);
                    throw new InvalidOperationException("이전 로컬 실습 검증은 중단되었습니다. KingdomIdle.Balance.ServerTutorialAcceptance.Run()으로 서버 계약을 검증하세요.");
                }
                await ExercisePlayerAsync(menu);
                long revision = LocalProgression.State.Revision;
                float scale = Time.timeScale;
                Check(manager.RequestPlay(menu, true), "Preview accepted");
                Check(!manager.RequestPlay(menu, true), "Duplicate active ID rejected");
                await Until(() => ui.ActiveFeatureGuide != null && ui.ActiveFeatureGuide.IsVisible, "Preview did not appear");
                await Settle();
                var view = ui.ActiveFeatureGuide;
                ui.GuideTargets.TryGet(GameDirectTarget.Development, out var development);
                bool panelBeforeClick = ui.HasActiveTabPanel;
                var blocker = view.inputGate != null ? view.inputGate.GetComponent<UnityEngine.UI.Image>() : null;
                var previewHit = Click(development, out string clickDiagnostic);
                Check(previewHit.name == "InputBlocker" && !ui.HasActiveTabPanel,
                    $"Bright target hole blocks the underlying menu: hit={previewHit.name}, panelBefore={panelBeforeClick}, panelAfter={ui.HasActiveTabPanel}, " +
                    $"visible={view.IsVisible}, blocks={view.inputGroup.blocksRaycasts}, guideStep={GameDirectInteraction.Step?.Id}, armed={GameDirectInteraction.Armed}, " +
                    $"blockerActive={blocker?.isActiveAndEnabled}, blockerRaycast={blocker?.raycastTarget}, blockerCull={blocker?.canvasRenderer.cull}, blockerDepth={blocker?.depth}, blockerAlpha={blocker?.color.a}, " +
                    $"highlight={view.HighlightRect:F2}, card={view.card.rect:F2}, cardPosition={view.card.anchoredPosition:F2}, root={((RectTransform)view.transform).rect:F2}, click={clickDiagnostic}");
                for (int i = 0; i < 4; i++)
                {
                    Check(view.titleLabel.text == menu.steps[i].title && view.progressLabel.text == $"{i + 1} / 4", "Correct preview step " + (i + 1));
                    int expected = i + 2;
                    Click(view.nextButton.transform as RectTransform);
                    view.Confirm(); // 같은 프레임의 두 번째 응답은 다음 단계를 건너뛰면 안 된다.
                    if (i < 3) await Until(() => view.IsVisible && view.progressLabel.text == $"{expected} / 4", "Preview step did not advance");
                    await Settle();
                }
                await Until(() => manager.ActiveSequenceId == null, "Preview did not finish");
                Check(!view.IsVisible && !view.inputGroup.blocksRaycasts, "Preview completion releases input");
                Check(LocalProgression.State.Revision == revision, "Preview leaves account revision unchanged");
                Check(Time.timeScale == scale, "Guide does not change battle timeScale");

                Check(manager.RequestPlay(menu, true) && manager.RequestPlay(rebirth, true), "Two distinct previews queued");
                Check(manager.PendingCount == 1 && !manager.RequestPlay(rebirth, true), "FIFO queue deduplicates waiting ID");
                await Until(() => view.IsVisible, "Queued preview did not appear");
                view.Skip();
                await Until(() => manager.ActiveSequenceId == rebirth.sequenceId && view.IsVisible, "FIFO did not start next sequence");
                Check(view.titleLabel.text == "환생", "FIFO advances to second sequence after skip");
                manager.CancelCurrent();
                await Until(() => manager.ActiveSequenceId == null, "FIFO cancellation did not complete");

                Check(manager.RequestPlay(menu, true), "Interruption preview accepted");
                await Until(() => view.IsVisible, "Interruption preview did not appear");
                var popup = new GameObject("GuideAcceptance_InterruptingPopup", typeof(RectTransform));
                popup.transform.SetParent(ui.LayerPopups, false);
                await Settle();
                Check(!view.IsVisible && !view.inputGroup.blocksRaycasts, "Popup interruption releases input");
                Object.Destroy(popup);
                await Until(() => view.IsVisible, "Popup interruption did not resume");
                Check(view.progressLabel.text == "1 / 4", "Popup resumes same unconfirmed step");
                ui.SetLoading(true, "검증 로딩");
                await Settle();
                Check(!view.IsVisible, "Loading hides guide");
                ui.SetLoading(false);
                await Until(() => view.IsVisible, "Loading did not resume");
                ui.GuideTargets.TryGet(GameDirectTarget.Development, out development);
                development.gameObject.SetActive(false);
                await Settle();
                Check(!view.IsVisible && !view.inputGroup.blocksRaycasts, "Hidden target cannot leave a blocker");
                development.gameObject.SetActive(true);
                await Until(() => view.IsVisible, "Target restoration did not resume");
                Object.Destroy(development.gameObject);
                await Settle();
                Check(!view.IsVisible && !view.inputGroup.blocksRaycasts, "Destroyed target releases input");
                ui.ReplaceScreen(UIScreenId.Main);
                await Settle();
                await Until(() => view.IsVisible, "New main screen target did not rebind");
                Check(view.progressLabel.text == "1 / 4", "Screen replacement rebinds without skipping a step");
                Check(manager.RequestPlay(rebirth, true), "Second request queued before account change");
                LocalProgression.OpenTestAccount(account + "-second");
                await Until(() => manager.ActiveSequenceId == null, "Old account request did not cancel");
                Check(manager.PendingCount == 0 && !view.IsVisible, "Account change clears queue and overlay");
                await ExerciseLowSpecAsync(manager, ui, menu);
                await CaptureLayoutsAsync(root, ui, manager, menu, rebirth);
                await ValidateReferencesAsync(ui.Catalog.overlayFeatureGuide);
                Check(menu.steps[0].title == "육성" && menu.steps.Length == 4, "Runtime did not mutate authored sequence");
                await InteractiveGuideAcceptance.RunPreviewAsync(ui, Check);
            }
            catch (Exception error) { failure = error.ToString(); Debug.LogException(error); }
            finally
            {
                if (managerRoot != null) Object.Destroy(managerRoot);
                if (root != null) Object.Destroy(root);
                if (events != null) Object.Destroy(events);
                await Settle();
                session?.Dispose();
                Application.runInBackground = background;
                File.WriteAllText(Output + "/acceptance.json", JsonConvert.SerializeObject(new
                { passed = failure == null, unity = Application.unityVersion, checks = Checks, captures = Captures, failure,
                    scope = "Preview only: isolated PlayMode, production UGUI prefabs and synthetic raycast input. Server progress, gameplay rewards, real combat and Android are not tested." }, Formatting.Indented));
                Debug.Log("[GameDirectPreviewAcceptance] " + (failure == null ? "PASSED " : "FAILED ") + Checks.Count);
                EditorApplication.isPlaying = false;
            }
        }

        /// <summary>저사양에서 장식만 멈추고 안내·터치가 유지되는지 확인한다. 임시 설정은 원래 키 존재 여부까지 복원한다.</summary>
        private static async UniTask ExerciseLowSpecAsync(GameDirectManager manager, UIManager ui, GameDirectSequenceSO menu)
        {
            string[] keys = { GamePresentationSettings.LowSpecKey, "title_ambientMotion" };
            bool[] existed = keys.Select(PlayerPrefs.HasKey).ToArray();
            int[] values = keys.Select(key => PlayerPrefs.GetInt(key)).ToArray();
            try
            {
                GamePresentationSettings.SetLowSpec(true, false);
                Check(manager.RequestPlay(menu, true), "Low-spec preview accepted");
                await Until(() => ui.ActiveFeatureGuide.IsVisible, "Low-spec preview did not show");
                var view = ui.ActiveFeatureGuide;
                await Settle();
                Check(view.highlight.localScale == Vector3.one && view.inputGroup.blocksRaycasts, "Low-spec stops highlight pulse and retains input");
                Click(view.nextButton.transform as RectTransform);
                await Until(() => view.progressLabel.text == "2 / 4", "Low-spec button did not advance");
                Check(view.IsVisible, "Low-spec description and next step remain visible");
                manager.enabled = false;
                await Until(() => manager.ActiveSequenceId == null, "Disabled manager did not cancel");
                Check(!view.IsVisible && !view.inputGroup.blocksRaycasts, "Manager disable restores input");
                manager.enabled = true;
            }
            finally
            {
                for (int i = 0; i < keys.Length; i++)
                    if (existed[i]) PlayerPrefs.SetInt(keys[i], values[i]); else PlayerPrefs.DeleteKey(keys[i]);
                GamePresentationSettings.Apply();
            }
        }

        /// <summary>MonoBehaviour 없이도 재생기가 취소·가림·응답 중복·폐기를 처리하는지 메모리 연결부로 검사한다.</summary>
        private static async UniTask ExercisePlayerAsync(GameDirectSequenceSO definition)
        {
            var surface = new TestSurface();
            using var player = new FeatureGuidePlayer(surface);
            using var token = new CancellationTokenSource();
            var running = player.PlayAsync(new GameDirectStep(definition.steps[0], 1, 4), token.Token);
            Check(!surface.IsVisible, "Pure C# player waits for target readiness");
            surface.Ready = true;
            await Until(() => surface.IsVisible, "Fake surface did not show");
            surface.Ready = false;
            surface.Emit(GameDirectResult.Confirmed);
            await Settle();
            Check(!surface.IsVisible, "Response under blocking UI is ignored");
            surface.Ready = true;
            await Until(() => surface.IsVisible, "Fake surface did not resume");
            token.Cancel();
            bool cancelled = false;
            try { await running; } catch (OperationCanceledException) { cancelled = true; }
            Check(cancelled && !surface.IsVisible && surface.Subscribers == 0, "Cancellation propagates and unsubscribes surface");
            var second = player.PlayAsync(new GameDirectStep(definition.steps[1], 2, 4), CancellationToken.None);
            surface.Emit(GameDirectResult.Skipped);
            surface.Emit(GameDirectResult.Confirmed);
            Check(await second == GameDirectResult.Skipped, "First response wins within one frame");
            player.Dispose();
            Check(surface.Disposed, "Player owns surface disposal");
        }

        /// <summary>프리팹의 직렬화 객체 참조가 끊기지 않았는지 실제 SerializedObject로 검사한다.</summary>
        private static async UniTask ValidateReferencesAsync(GameObject prefab)
        {
            int checkedReferences = 0;
            foreach (var component in prefab.GetComponentsInChildren<Component>(true))
            {
                Check(component != null, "No missing script: " + checkedReferences);
                var serialized = new SerializedObject(component);
                var property = serialized.GetIterator();
                while (property.Next(true))
                    if (property.propertyType == SerializedPropertyType.ObjectReference)
                    {
                        if (property.objectReferenceInstanceIDValue != 0 && property.objectReferenceValue == null)
                            throw new InvalidOperationException("Broken reference: " + property.propertyPath);
                        checkedReferences++;
                    }
            }
            Check(checkedReferences > 0, "Prefab serialized references checked: " + checkedReferences);
            await UniTask.CompletedTask;
        }

        /// <summary>실제 UGUI 프리팹을 세 비율의 RenderTexture로 렌더한다. 실기기/전투 화면으로 기록하지 않는다.</summary>
        private static async UniTask CaptureLayoutsAsync(GameObject root, UIManager ui, GameDirectManager manager, GameDirectSequenceSO menu, GameDirectSequenceSO rebirth)
        {
            var canvas = root.GetComponent<Canvas>();
            RenderMode oldMode = canvas.renderMode;
            Camera oldCamera = canvas.worldCamera;
            var cameraRoot = new GameObject("GuideAcceptance_CaptureCamera", typeof(Camera));
            var camera = cameraRoot.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.06f, .12f, .08f);
            camera.orthographic = true; cameraRoot.transform.position = new Vector3(0, 0, -10);
            var sizes = new[] { new Vector2Int(360, 800), new Vector2Int(540, 960), new Vector2Int(900, 1200) };
            try
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
                foreach (var size in sizes)
                {
                    var texture = new RenderTexture(size.x, size.y, 24);
                    camera.targetTexture = texture;
                    try
                    {
                        await Settle();
                        await SaveCaptureAsync(camera, size, "before", ui, false);
                        manager.RequestPlay(menu, true);
                        await Until(() => ui.ActiveFeatureGuide.IsVisible, "Capture menu did not show");
                        await Settle();
                        for (int i = 0; i < 4; i++)
                        {
                            await SaveCaptureAsync(camera, size, "menu-" + (i + 1), ui, true);
                            ui.ActiveFeatureGuide.Confirm();
                            if (i < 3) await Until(() => ui.ActiveFeatureGuide.IsVisible && ui.ActiveFeatureGuide.progressLabel.text == $"{i + 2} / 4", "Capture step did not advance");
                            await Settle();
                        }
                        await Until(() => manager.ActiveSequenceId == null, "Capture menu did not finish");
                        manager.RequestPlay(rebirth, true);
                        await Until(() => ui.ActiveFeatureGuide.IsVisible, "Capture reincarnation did not show");
                        await Settle();
                        await SaveCaptureAsync(camera, size, "reincarnation", ui, true);
                        manager.CancelCurrent();
                        await Until(() => manager.ActiveSequenceId == null, "Capture cancellation did not finish");
                    }
                    finally { camera.targetTexture = null; texture.Release(); Object.Destroy(texture); }
                }
            }
            finally { canvas.renderMode = oldMode; canvas.worldCamera = oldCamera; Object.Destroy(cameraRoot); }
        }

        /// <summary>현재 렌더를 PNG로 기록하고 카드·글자 잘림과 렌더 배치 수를 수집한다.</summary>
        private static async UniTask SaveCaptureAsync(Camera camera, Vector2Int size, string name, UIManager ui, bool guide)
        {
            await Settle();
            camera.Render();
            var previous = RenderTexture.active;
            var image = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
            try
            {
                RenderTexture.active = camera.targetTexture;
                image.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0); image.Apply();
                string path = $"{Output}/{name}-{size.x}x{size.y}.png";
                File.WriteAllBytes(path, image.EncodeToPNG());
                if (guide)
                {
                    var view = ui.ActiveFeatureGuide;
                    var corners = new Vector3[4]; view.card.GetWorldCorners(corners);
                    Rect bounds = ((RectTransform)view.transform).rect;
                    var low = view.transform.InverseTransformPoint(corners[0]);
                    var high = view.transform.InverseTransformPoint(corners[2]);
                    Check(low.x >= bounds.xMin && low.y >= bounds.yMin && high.x <= bounds.xMax && high.y <= bounds.yMax, "Card inside safe area: " + name + size);
                    Check(!view.descriptionLabel.isTextOverflowing, "Description fits: " + name + size);
                }
                Captures.Add(new { path, width = size.x, height = size.y, guide, batches = UnityEditor.UnityStats.batches,
                    drawCalls = UnityEditor.UnityStats.drawCalls, unityMemory = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong(),
                    context = "Isolated prefab render, not combat or physical device" });
            }
            finally { RenderTexture.active = previous; Object.Destroy(image); }
        }

        /// <summary>프레임 준비와 응답만 제공하는 테스트 연결부다. Unity 오브젝트나 저장소를 갖지 않는다.</summary>
        private sealed class TestSurface : IFeatureGuideSurface
        {
            private Action<GameDirectResult> _responded;
            public bool Ready, Disposed;
            public bool IsVisible { get; private set; }
            public int Subscribers => _responded?.GetInvocationList().Length ?? 0;
            public event Action<GameDirectResult> Responded { add => _responded += value; remove => _responded -= value; }
            /// <summary>테스트가 지정한 화면 준비 상태를 반환한다.</summary>
            public bool IsReady(GameDirectTarget target) => Ready && !Disposed;
            /// <summary>실제 View 대신 표시 여부만 기록한다.</summary>
            public void Show(GameDirectStep step) => IsVisible = true;
            /// <summary>입력 해제를 검증할 수 있도록 표시 상태를 지운다.</summary>
            public void Hide() => IsVisible = false;
            /// <summary>한 프레임의 사용자 응답을 동기 전달한다.</summary>
            public void Emit(GameDirectResult result) => _responded?.Invoke(result);
            /// <summary>소유자가 종료를 호출했는지 기록하고 표시를 해제한다.</summary>
            public void Dispose() { Disposed = true; Hide(); }
        }
    }
}
