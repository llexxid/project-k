using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using KingdomIdle.UI;
using Object = UnityEngine.Object;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>Runs the shipped title prefab/controller in the real title scene, without signing in.</summary>
    [InitializeOnLoad]
    public static class TitleLobbyValidation
    {
        const string ActiveKey = "LobbyValidation.Active";
        static bool GuestOnly => SessionState.GetBool("LobbyValidation.GuestOnly", false);
        static string Output => GuestOnly ? "Recordings/LobbyGuestRestore/Editor" : "Recordings/LobbyRevision5/Editor";
        static readonly (string name, int w, int h, bool english, Vector4 inset)[] Cases =
        {
            ("01_phone_ko", 1080, 1920, false, Vector4.zero),
            ("02_tall_safe_ko", 1080, 2400, false, new Vector4(0, 90, 0, 80)),
            ("03_small_en", 720, 1280, true, Vector4.zero),
            ("04_tablet_ko", 1536, 2048, false, new Vector4(0, 48, 0, 40)),
            ("05_tablet_en", 1600, 2560, true, Vector4.zero),
            ("06_landscape_en", 1920, 1080, true, Vector4.zero)
        };
        static Camera _camera;
        static Canvas _canvas;
        static RectTransform _safe;
        static TitleScreenView _view;
        static RenderTexture _render;
        static int _index, _frames;
        static double _deadline;
        static readonly List<string> Results = new List<string>();

        static TitleLobbyValidation()
        {
            EditorApplication.playModeStateChanged += PlayState;
            if (SessionState.GetBool(ActiveKey, false)) EditorApplication.update += Tick;
        }

        public static void RunGuestLogin()
        {
            SessionState.SetBool("LobbyValidation.GuestOnly", true);
            Run();
        }

        public static void Run()
        {
            Directory.CreateDirectory(Output);
            string failure = Path.Combine(Output, "FAILURE.txt");
            if (File.Exists(failure)) File.Delete(failure);
            TitleLobbyBuilder.Rebuild();
            if (!GuestOnly) KingdomIdle.EditorTools.Optimization.OptAtlases.CreateInBuildAtlases();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/buildScenes/title.unity");
            PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UGUI/Prefabs/UGUI_UIRoot.prefab"));
            if (Object.FindFirstObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            SessionState.SetBool(ActiveKey, true);
            EditorApplication.isPlaying = true;
        }

        static void PlayState(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                _index = 0; _frames = 0; Results.Clear();
                _deadline = EditorApplication.timeSinceStartup + 180;
                _camera = Camera.main;
                UIManager.Instance.ReplaceScreen(UIScreenId.Title);
                _canvas = UIManager.Instance.GetComponent<Canvas>();
                _canvas.renderMode = RenderMode.ScreenSpaceCamera;
                _canvas.worldCamera = _camera; _canvas.planeDistance = 10;
                var scaler = _canvas.GetComponent<CanvasScaler>(); scaler.enabled = false;
                _safe = (RectTransform)_canvas.transform.Find("SafeArea");
                _safe.GetComponent<SafeAreaFitter>().enabled = false;
                _view = Object.FindFirstObjectByType<TitleScreenView>();
                _view.presentation.SendMessage("OnApplicationFocus", true);
                _view.presentation.SetAmbientMotion(false);
                EditorApplication.update -= Tick; EditorApplication.update += Tick;
                Configure();
            }
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(ActiveKey, false);
                EditorApplication.update -= Tick;
                // Keep the owned editor available for the subsequent Android build and debugging.
            }
        }

        static void Configure()
        {
            var c = Cases[_index];
            if (_render != null) { _camera.targetTexture = null; _render.Release(); Object.DestroyImmediate(_render); }
            _render = new RenderTexture(c.w, c.h, 24, RenderTextureFormat.ARGB32);
            _render.Create(); _camera.targetTexture = _render;
            _camera.aspect = (float)c.w / c.h;
            // Match the shipped width-based 1080px CanvasScaler at the actual target resolution.
            _canvas.scaleFactor = c.w / 1080f;
            _safe.anchorMin = new Vector2(c.inset.x / c.w, c.inset.w / c.h);
            _safe.anchorMax = new Vector2(1 - c.inset.z / c.w, 1 - c.inset.y / c.h);
            _safe.offsetMin = _safe.offsetMax = Vector2.zero;
            _view.presentation.SetLanguage(c.english);
            _view.popupLogin.SetActive(false);
            Canvas.ForceUpdateCanvases();
            _view.presentation.RefreshLayout();
            _frames = 0;
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying || _view == null) return;
            try
            {
                if (EditorApplication.timeSinceStartup > _deadline) throw new TimeoutException("Lobby validation timed out.");
                if (++_frames < 10) return;
                if (_index < Cases.Length)
                {
                    var c = Cases[_index];
                    _view.presentation.RefreshLayout();
                    _view.presentation.SetAmbientMotion(true);
                    _view.presentation.SamplePose(2.4f);
                    Canvas.ForceUpdateCanvases();
                    Capture(c.name);
                    CheckBounds(c.name);
                    if (_index == 0 && !GuestOnly) CheckInteractionAndMotion();
                    if (GuestOnly || _index == 2 || _index == 3)
                    {
                        _view.btnLogin.onClick.Invoke();
                        // Sample final popup pose, after its tiny entrance tween.
                        _view.popupLoginBox.localScale = Vector3.one;
                        Canvas.ForceUpdateCanvases();
                        Capture(c.name + "_login");
                        CheckGuestButton(c.name);
                        _view.popupLoginDim.onClick.Invoke();
                    }
                    _index++;
                    if (_index < Cases.Length) { Configure(); return; }
                    if (!GuestOnly) CaptureMotion();
                    Report();
                    EditorApplication.isPlaying = false;
                }
            }
            catch (Exception e)
            {
                File.WriteAllText(Path.Combine(Output, "FAILURE.txt"), e.ToString());
                Debug.LogException(e);
                SessionState.SetBool(ActiveKey, false);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                else EditorApplication.isPlaying = false;
            }
        }

        static void CheckInteractionAndMotion()
        {
            foreach (var button in new[] { _view.btnLogin, _view.presentation.languageButton })
            {
                var pointer = new PointerEventData(EventSystem.current)
                { position = RectTransformUtility.WorldToScreenPoint(_camera, button.transform.position) };
                var hits = new List<RaycastResult>();
                EventSystem.current.RaycastAll(pointer, hits);
                Require(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == button,
                    button.name + " receives the topmost pointer hit.");
            }
            _view.bgClickCatcher.onClick.Invoke();
            Require(_view.popupLogin.activeSelf, "Unauthenticated tap opens sign-in without loading main.");
            _view.popupLoginDim.onClick.Invoke();
            Require(!_view.popupLogin.activeSelf, "Outside tap closes sign-in.");
            Require(_view.btnLoginGuest.gameObject.activeSelf && !_view.btnLoginApple.gameObject.activeSelf, "Development guest is available; unsupported Apple remains hidden.");
            _view.presentation.SamplePose(0);
            var sword = _view.presentation.layers.First(l => l.target.name == "Sword").target;
            var first = sword.localRotation;
            _view.presentation.SamplePose(1.2f);
            Require(Quaternion.Angle(first, sword.localRotation) > .1f, "Sword moves independently.");
            Capture("07_motion_second_pose");
            _view.presentation.SetAmbientMotion(false);
            Require(Quaternion.Angle(sword.localRotation, Quaternion.identity) < .001f, "Motion-off restores neutral pose.");
            // OnEnable must recreate a coroutine that Unity stopped on deactivation.
            _view.presentation.SetAmbientMotion(true);
            _view.gameObject.SetActive(false); _view.gameObject.SetActive(true);
            var field = typeof(TitleLobbyPresentation).GetField("_ambient", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Require(field.GetValue(_view.presentation) != null, "Ambient coroutine restarts after reactivation.");
            _view.presentation.SetAmbientMotion(false);
            var p = _view.presentation;
            Require(!_view.GetComponentsInChildren<Transform>(true).Any(t => t.name == "AmbientMotion"), "Removed title effects button has no hidden hit target.");
            Require(!_view.GetComponentsInChildren<Transform>(true).Any(t => t.name.Contains("Laser") || t.name.Contains("MagicBolt")), "Tower has no attack objects.");
            p.SetAmbientMotion(true);
            p.SamplePose(0); float firstGlow = p.crystalGlow.color.a;
            p.SamplePose(1.2f);
            Require(Mathf.Abs(firstGlow - p.crystalGlow.color.a) > .04f && p.crystalGlow.color.a < .3f, "Centered crystal has a subtle idle light pulse.");
            p.SamplePose(5.62f); Require(p.stormFlash.Intensity > .7f, "First intracloud pulse is visible."); Capture("08_storm_first");
            p.SamplePose(5.90f); Require(p.stormFlash.Intensity == 0, "Cloud is dark between pulses.");
            p.SamplePose(6.08f); Require(p.stormFlash.Intensity > .99f, "Second intracloud pulse is visible."); Capture("09_storm_second");
            p.SamplePose(7); Require(p.stormFlash.Intensity == 0, "Cloud is quiet after its brief burst.");
            p.SamplePose(14.22f); Require(p.stormFlash.Intensity > .7f, "Cloud repeats only after the next long interval.");
            p.SetAmbientMotion(false);
            Require(p.stormFlash.Intensity == 0 && !p.stormFlash.gameObject.activeSelf && !p.crystalGlow.gameObject.activeSelf, "Low-spec setting completely suppresses lightning.");
            p.OpenLanguagePopup();
            Require(p.languagePopup.activeSelf, "Language globe opens a compact popup.");
            p.englishButton.onClick.Invoke();
            Require(p.English && !p.languagePopup.activeSelf, "English selection applies and closes popup.");
            p.OpenLanguagePopup();
            Require(p.currentLanguage.text.Contains("English"), "Language popup identifies current language.");
            p.koreanButton.onClick.Invoke();
            p.SamplePose(1); // warm up before allocation measurement
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 300; i++) p.SamplePose(i / 30f);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Require(allocated == 0, "300 ambient pose updates allocate " + allocated + " managed bytes.");
            _view.presentation.SetLanguage(true);
            Require(_view.pressHint.text == "TAP TO CONTINUE" && p.logo.sprite == p.englishLogo, "English switches logo and interface together.");
            _view.presentation.SetLanguage(false);
        }

        static void CheckGuestButton(string name)
        {
            var button = _view.btnLoginGuest;
            Require(button.gameObject.activeInHierarchy && button.IsInteractable(), name + ": guest entry is visible and enabled.");
            var label = button.GetComponentInChildren<TMP_Text>();
            Require(label.text == (_view.presentation.English ? "Continue as Guest" : "게스트로 시작"), name + ": guest label matches selected language.");
            var pointer = new PointerEventData(EventSystem.current)
            { position = RectTransformUtility.WorldToScreenPoint(_camera, button.transform.position) };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Require(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == button,
                name + ": guest receives the topmost pointer hit (" + (hits.Count > 0 ? hits[0].gameObject.name : "none") + ").");
            var corners = new Vector3[4]; _view.popupLoginBox.GetWorldCorners(corners);
            Require(corners.All(c => _safe.rect.Contains((Vector2)_safe.InverseTransformPoint(c))), name + ": two-provider popup fits the safe area.");
        }

        static void CheckBounds(string name)
        {
            var safeRect = _safe.rect;
            foreach (var rt in new[] { _view.presentation.logoRoot, _view.pressHint.rectTransform, (RectTransform)_view.btnLogin.transform,
                         (RectTransform)_view.presentation.languageButton.transform })
            {
                var corners = new Vector3[4]; rt.GetWorldCorners(corners);
                foreach (var corner in corners)
                {
                    var local = _safe.InverseTransformPoint(corner);
                    Require(local.x >= safeRect.xMin - 2 && local.x <= safeRect.xMax + 2 && local.y >= safeRect.yMin - 2 && local.y <= safeRect.yMax + 2,
                        name + ": " + rt.name + " fits safe area", false);
                }
            }
            Results.Add("PASS " + name + ": title and controls fit safe area.");
            var language = (RectTransform)_view.presentation.languageButton.transform;
            var languagePosition = _safe.InverseTransformPoint(language.position);
            Require(languagePosition.x < safeRect.center.x && languagePosition.y > safeRect.center.y,
                name + ": language control stays at top-left", false);
            if (_safe.rect.width > _safe.rect.height)
            {
                var lc = new Vector3[4]; language.GetWorldCorners(lc);
                var tc = new Vector3[4]; _view.presentation.logoRoot.GetWorldCorners(tc);
                Require(tc.Max(c => _safe.InverseTransformPoint(c).y) < lc.Min(c => _safe.InverseTransformPoint(c).y) - 4,
                    "Landscape title clears the top-left language control.");
            }
            if (_safe.rect.height >= _safe.rect.width)
            {
                var titleCorners = new Vector3[4]; _view.presentation.logoRoot.GetWorldCorners(titleCorners);
                float titleBottom = titleCorners.Min(c => _safe.InverseTransformPoint(c).y);
                float siegeTop = _safe.InverseTransformPoint(_view.presentation.artWorld.TransformPoint(new Vector3(0, 448, 0))).y;
                Require(titleBottom >= siegeTop + 8, name + ": wordmark clears the distant cloud and dragon wings.");
                var hintCorners = new Vector3[4]; _view.pressHint.rectTransform.GetWorldCorners(hintCorners);
                float hintTop = hintCorners.Max(c => _safe.InverseTransformPoint(c).y);
                foreach (string hero in new[] { "Knight", "Archer", "Mage" })
                {
                    var rt = _view.presentation.layers.First(l => l.target.name == hero).target;
                    var corners = new Vector3[4]; rt.GetWorldCorners(corners);
                    float bottom = corners.Min(c => _safe.InverseTransformPoint(c).y);
                    Require(bottom > hintTop + 12, name + ": " + hero + " clears the start prompt", false);
                }
                Results.Add("PASS " + name + ": all three heroes clear the start prompt.");
            }
            var texts = _view.presentation.footer.GetComponentsInChildren<TMP_Text>();
            foreach (var text in texts)
            {
                text.ForceMeshUpdate();
                Require(text.preferredWidth <= text.rectTransform.rect.width + 8, name + ": " + text.name + " text width", false);
            }
        }

        static void Capture(string name)
        {
            Canvas.ForceUpdateCanvases(); _camera.Render();
            var previous = RenderTexture.active; RenderTexture.active = _render;
            var image = new Texture2D(_render.width, _render.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, _render.width, _render.height), 0, 0); image.Apply();
            File.WriteAllBytes(Path.Combine(Output, name + ".png"), image.EncodeToPNG());
            Object.DestroyImmediate(image); RenderTexture.active = previous;
        }

        static void CaptureMotion()
        {
            _index = 2; Configure();
            _view.presentation.SetLanguage(false);
            _view.presentation.SetAmbientMotion(true);
            _view.presentation.RefreshLayout();
            Directory.CreateDirectory(Path.Combine(Output, "MotionFrames"));
            for (int frame = 0; frame < 240; frame++)
            {
                _view.presentation.SamplePose(frame / 30f + .5f);
                Capture("MotionFrames/frame_" + frame.ToString("D4"));
            }
            _view.presentation.SetAmbientMotion(false);
            _index = Cases.Length;
            Results.Add("Captured 240 in-game animation frames at 720x1280, 30 fps, 8 seconds.");
        }

        static void Require(bool condition, string description, bool record = true)
        {
            if (!condition) throw new InvalidOperationException("FAIL " + description);
            if (record) Results.Add("PASS " + description);
        }

        static void Report()
        {
            long bytes = AssetDatabase.GetDependencies(TitleLobbyBuilder.PrefabPath, true)
                .Where(path => path.StartsWith(TitleLobbyBuilder.ArtPath + "/") && path.EndsWith(".png"))
                .Sum(path => new FileInfo(path).Length);
            Results.Add("Shipped source PNG bytes: " + bytes);
            Results.Add("Live title Graphics: " + _view.GetComponentsInChildren<Graphic>(false).Length);
            Results.Add("Moving layers: " + _view.presentation.layers.Length + "; motes: " + _view.presentation.motes.Length);
            var atlas = AssetDatabase.LoadAssetAtPath<UnityEngine.U2D.SpriteAtlas>("Assets/_Project/Art/Atlases/Atlas_UI.spriteatlasv2");
            var atlasSprites = new Sprite[atlas.spriteCount];
            atlas.GetSprites(atlasSprites);
            var textures = atlasSprites.Where(s => s != null).Select(s => s.texture).Distinct();
            long astcBytes = 0;
            foreach (var texture in textures)
            {
                astcBytes += ((texture.width + 5) / 6) * ((texture.height + 5) / 6) * 16L;
                Results.Add("Atlas_UI page: " + texture.width + "x" + texture.height + ", " + texture.format);
            }
            Results.Add("Atlas_UI ASTC6x6 block bytes across all pages (including existing UI): " + astcBytes);
            foreach (var sprite in atlasSprites) if (sprite != null) Object.DestroyImmediate(sprite);
            Results.Add("Runtime scene: Assets/_Project/Scenes/buildScenes/title.unity; real UIManager + TitleScreenController; no sign-in dispatched.");
            File.WriteAllLines(Path.Combine(Output, "validation.txt"), Results);
            Debug.Log("[Lobby Validation] " + string.Join("\n", Results));
        }
    }
}
