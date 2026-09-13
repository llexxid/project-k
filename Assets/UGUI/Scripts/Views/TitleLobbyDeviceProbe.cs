#if LOBBY_DEVICE_QA && DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using KingdomIdle.UI;
using Scripts.Core;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace KingdomIdle.UGUI
{
    /// <summary>Local file-controlled instrumentation compiled only into the opt-in device QA build.</summary>
    public sealed class TitleLobbyDeviceProbe : MonoBehaviour
    {
        [Serializable] sealed class Command
        {
            public string id;
            public string action;
            public string value;
            public float seconds = 30;
        }

        string _directory;
        string _commandPath;
        TitleScreenView _view;
        bool _measuring;
        int _frames;
        float _endAt;
        string _measurementId;
        readonly double[] _frameMs = new double[18000];
        readonly long[] _gc = new long[18000];
        readonly long[] _cpu = new long[18000];
        readonly long[] _drawCalls = new long[18000];
        ProfilerRecorder _gcRecorder, _cpuRecorder, _drawRecorder;
        long _startMemory;
        long _startProcessCpu;
        float _startRealtime;
        ProfilerRecorder _lobbyRecorder, _damageRecorder, _verticesRecorder;
        readonly long[] _lobbyTime = new long[18000], _damageTime = new long[18000], _vertices = new long[18000];
        GameObject _fixture;
        DamageTextManager _damageManager;
        float _nextBurst;
        ulong _hitSequence;
        readonly float[] _audioSamples = new float[512];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            DontDestroyOnLoad(new GameObject("LobbyDeviceProbe", typeof(TitleLobbyDeviceProbe)));
        }

        IEnumerator Start()
        {
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var files = activity.Call<AndroidJavaObject>("getFilesDir"))
                _directory = files.Call<string>("getAbsolutePath");
            _commandPath = Path.Combine(_directory, "lobby-command.json");
            float deadline = Time.realtimeSinceStartup + 90;
            while (_view == null && Time.realtimeSinceStartup < deadline)
            {
                _view = FindFirstObjectByType<TitleScreenView>();
                yield return new WaitForSecondsRealtime(.5f);
            }
            if (_view == null)
            {
                Write("startup-failure", new { scene = SceneManager.GetActiveScene().name, error = "Title screen did not appear within 90 seconds." });
                yield break;
            }
            yield return new WaitForSecondsRealtime(2);
            Snapshot("ready");
            var tick = new WaitForSecondsRealtime(.25f);
            while (true)
            {
                if (!_measuring && File.Exists(_commandPath))
                {
                    string identifier = "command-error";
                    try
                    {
                        var command = JsonConvert.DeserializeObject<Command>(File.ReadAllText(_commandPath));
                        File.Delete(_commandPath);
                        if (command == null || string.IsNullOrEmpty(command.id))
                            throw new ArgumentException("A complete local QA command is required.");
                        identifier = command.id;
                        Execute(command);
                    }
                    catch (Exception error) { Write(identifier, new { error = error.ToString() }); }
                }
                yield return tick;
            }
        }

        void Execute(Command command)
        {
            switch (command.action)
            {
                case "state": Snapshot(command.id); break;
                case "language": _view.presentation.SetLanguage(command.value == "en", true); Snapshot(command.id); break;
                case "motion": _view.presentation.SetAmbientMotion(command.value == "on", true); Snapshot(command.id); break;
                case "settings": UIManager.Instance.OpenSettings(); Snapshot(command.id); break;
                case "damage-check":
                    var damage = FindFirstObjectByType<DamageTextManager>();
                    var point = Camera.main.ScreenToWorldPoint(new Vector3(Screen.width * .5f, Screen.height * .5f, 10));
                    damage.ShowWorldDamage(point, ulong.MaxValue);
                    damage.ShowWorldDamage(point + Vector3.up, 0);
                    Write(command.id, new { lowSpec = GamePresentationSettings.LowSpec, values = damage.layer.GetComponentsInChildren<TMPro.TMP_Text>().Select(t => t.text).ToArray() });
                    break;
                case "fixture":
                    SetDamageFixture(command.value == "on"); Snapshot(command.id); break;
                case "music":
                    Scripts.Core.SFXManager.Instance.PlayBGM(command.value == "combat" ? eSFXType.BGM : eSFXType.TITLE);
                    Snapshot(command.id); break;
                case "music-end":
                    var source = Scripts.Core.SFXManager.Instance.CurrentMusicSource;
                    if (source != null && source.clip != null) source.time = Mathf.Max(0, source.clip.length - 3);
                    Snapshot(command.id); break;
                case "counters":
                    var handles = new List<ProfilerRecorderHandle>(); ProfilerRecorderHandle.GetAvailable(handles);
                    Write(command.id, handles.Select(ProfilerRecorderHandle.GetDescription).Where(d => d.Name.Contains("Canvas") || d.Name.Contains("UI.") || d.Name.Contains("KingdomIdle") || d.Name.Contains("WaitForTarget")).Select(d => new { d.Name, category = d.Category.Name, units = d.UnitType.ToString() }));
                    break;
                case "power":
                    PlayerPrefs.SetInt(UIManager.PrefKeyPowerSave, command.value == "on" ? 1 : 0);
                    GamePresentationSettings.Apply();
                    _view.presentation.SetAmbientMotion(_view.presentation.AmbientMotion);
                    Snapshot(command.id); break;
                case "reopen":
                    UIManager.Instance.ReplaceScreen(UIScreenId.Title);
                    _view = FindFirstObjectByType<TitleScreenView>();
                    Snapshot(command.id); break;
                case "measure":
                    _measurementId = command.id;
                    _frames = 0;
                    _startMemory = Profiler.GetTotalAllocatedMemoryLong();
                    _gcRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
                    _cpuRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread");
                    _drawRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
                    _lobbyRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "KingdomIdle.LobbyAnimation");
                    _damageRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "KingdomIdle.DamagePresentation");
                    _verticesRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Vertices Count");
                    _startProcessCpu = ProcessCpuMs();
                    _startRealtime = Time.realtimeSinceStartup;
                    _endAt = Time.realtimeSinceStartup + Mathf.Clamp(command.seconds, 1, 240);
                    _measuring = true;
                    break;
                default: throw new ArgumentException("Unknown local QA action: " + command.action);
            }
        }

        void Update()
        {
            if (_fixture != null && Time.unscaledTime >= _nextBurst)
            {
                _nextBurst = Time.unscaledTime + .35f;
                var camera = Camera.main;
                for (int i = 0; i < 35; i++)
                {
                    float x = Screen.width * (.16f + (i % 7) * .11f), y = Screen.height * (.25f + (i / 7) * .10f);
                    var point = camera.ScreenToWorldPoint(new Vector3(x, y, 10));
                    _damageManager.ShowWorldDamage(point, 10000 + (++_hitSequence % 90000));
                }
            }
            if (!_measuring) return;
            if (_frames < _frameMs.Length)
            {
                _frameMs[_frames] = Time.unscaledDeltaTime * 1000.0;
                _gc[_frames] = _gcRecorder.LastValue;
                _cpu[_frames] = _cpuRecorder.LastValue;
                _drawCalls[_frames] = _drawRecorder.LastValue;
                _lobbyTime[_frames] = _lobbyRecorder.LastValue;
                _damageTime[_frames] = _damageRecorder.LastValue;
                _vertices[_frames] = _verticesRecorder.LastValue;
                _frames++;
            }
            if (Time.realtimeSinceStartup < _endAt) return;
            _measuring = false;
            long endMemory = Profiler.GetTotalAllocatedMemoryLong();
            bool gcValid = _gcRecorder.Valid, cpuValid = _cpuRecorder.Valid, drawValid = _drawRecorder.Valid;
            bool lobbyValid = _lobbyRecorder.Valid, damageValid = _damageRecorder.Valid;
            long processCpuMs = ProcessCpuMs() - _startProcessCpu;
            float elapsedSeconds = Time.realtimeSinceStartup - _startRealtime;
            _gcRecorder.Dispose(); _cpuRecorder.Dispose(); _drawRecorder.Dispose();
            _lobbyRecorder.Dispose(); _damageRecorder.Dispose(); _verticesRecorder.Dispose();
            var times = _frameMs.Take(_frames).Skip(5).OrderBy(t => t).ToArray();
            var allocations = _gc.Take(_frames).Skip(5).ToArray();
            Write(_measurementId, new
            {
                frames = times.Length,
                fps = 1000 / times.Average(),
                frameMs = new { average = times.Average(), p50 = times[times.Length / 2], p95 = times[(int)(times.Length * .95)], p99 = times[(int)(times.Length * .99)], max = times.Max(), over33ms = times.Count(t => t > 33.5), over50ms = times.Count(t => t > 50) },
                gc = new { valid = gcValid, bytes = allocations.Sum(), perFrame = allocations.Average(), max = allocations.Max(), nonzeroFrames = allocations.Count(x => x > 0) },
                cpu = new { valid = cpuValid, mainThreadMs = _cpu.Take(_frames).Skip(5).Average() / 1000000.0 },
                processCpu = new { milliseconds = processCpuMs, elapsedSeconds, corePercent = processCpuMs / (elapsedSeconds * 10.0) },
                presentation = new { lobbyValid, damageValid, lobbyMicrosecondsPerFrame = _lobbyTime.Take(_frames).Skip(5).Average() / 1000.0, damageMicrosecondsPerFrame = _damageTime.Take(_frames).Skip(5).Average() / 1000.0, verticesPerFrame = _vertices.Take(_frames).Skip(5).Average() },
                draw = new { valid = drawValid, average = _drawCalls.Take(_frames).Skip(5).Average(), max = _drawCalls.Take(_frames).Skip(5).Max() },
                unityMemory = new { start = _startMemory, end = endMemory, difference = endMemory - _startMemory },
                motion = _view != null && _view.presentation.AmbientMotion, powerSave = GamePresentationSettings.PowerSave,
                lowSpec = GamePresentationSettings.LowSpec, damageFixture = _fixture != null, generatedHits = _hitSequence,
                width = Screen.width, height = Screen.height, targetFps = Application.targetFrameRate,
                note = "Development ARM64 IL2CPP; main-thread time includes frame pacing waits. Command polling and result serialization are suspended during sampling."
            });
        }

        static long ProcessCpuMs()
        {
            using var process = new AndroidJavaClass("android.os.Process");
            return process.CallStatic<long>("getElapsedCpuTime");
        }

        void SetDamageFixture(bool enabled)
        {
            if (_fixture != null) { Destroy(_fixture); _fixture = null; }
            _view.gameObject.SetActive(!enabled);
            if (!enabled) return;
            // Shipped main-screen and damage prefabs, without starting account, reward or combat simulation services.
            _fixture = Instantiate(UIManager.Instance.Catalog.screenMain, UIManager.Instance.LayerScreens, false);
            _fixture.name = "DeviceDamageFixture";
            var view = _fixture.GetComponent<MainScreenView>();
            if (view.btnMenuSettings != null) view.btnMenuSettings.onClick.AddListener(UIManager.Instance.OpenSettings);
            _damageManager = FindFirstObjectByType<DamageTextManager>();
            if (_damageManager == null) throw new InvalidOperationException("Shipped DamageTextManager missing.");
            _hitSequence = 0;
            _nextBurst = Time.unscaledTime + .5f;
        }

        void Snapshot(string id)
        {
            Canvas.ForceUpdateCanvases();
            _view.presentation.RefreshLayout();
            Canvas.ForceUpdateCanvases();
            var presentation = _view.presentation;
            var canvas = _view.GetComponentInParent<Canvas>().rootCanvas;
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            object RectInfo(RectTransform rect)
            {
                var corners = new Vector3[4]; rect.GetWorldCorners(corners);
                var points = corners.Select(p => RectTransformUtility.WorldToScreenPoint(camera, p)).ToArray();
                return new { name = rect.name, x = points.Min(p => p.x), y = Screen.height - points.Max(p => p.y), width = points.Max(p => p.x) - points.Min(p => p.x), height = points.Max(p => p.y) - points.Min(p => p.y) };
            }
            var images = _view.GetComponentsInChildren<Image>().Where(i => i.sprite != null);
            var settings = FindFirstObjectByType<SettingsModalView>();
            var controls = new List<object> { RectInfo((RectTransform)_view.btnLogin.transform), RectInfo((RectTransform)presentation.languageButton.transform), RectInfo(presentation.logoRoot), RectInfo(_view.pressHint.rectTransform) };
            if (_view.popupLogin.activeSelf)
            {
                controls.Add(RectInfo(_view.popupLoginBox));
                foreach (var provider in new[] { _view.btnLoginGuest, _view.btnLoginGoogle })
                    if (provider.gameObject.activeInHierarchy) controls.Add(RectInfo((RectTransform)provider.transform));
            }
            if (presentation.languagePopup != null && presentation.languagePopup.activeSelf)
            {
                controls.Add(RectInfo(presentation.languagePanel)); controls.Add(RectInfo((RectTransform)presentation.koreanButton.transform)); controls.Add(RectInfo((RectTransform)presentation.englishButton.transform));
            }
            if (settings != null)
            {
                controls.Add(RectInfo(settings.panel));
                controls.Add(RectInfo((RectTransform)settings.tglLowSpec.transform.parent));
                controls.Add(RectInfo((RectTransform)settings.tglDamageText.transform.parent));
                controls.Add(RectInfo((RectTransform)settings.btnSaveClose.transform));
            }
            var music = Scripts.Core.SFXManager.Instance != null ? Scripts.Core.SFXManager.Instance.CurrentMusicSource : null;
            AudioListener.GetOutputData(_audioSamples, 0);
            double audioPower = 0;
            for (int i = 0; i < _audioSamples.Length; i++) audioPower += _audioSamples[i] * _audioSamples[i];
            var textures = images.Select(i => i.sprite.texture).Distinct().Select(t => new { t.name, t.width, t.height, format = t.format.ToString(), t.isReadable, bytes = Profiler.GetRuntimeMemorySizeLong(t) });
            Write(id, new
            {
                scene = SceneManager.GetActiveScene().name,
                device = SystemInfo.deviceModel, os = SystemInfo.operatingSystem,
                gpu = SystemInfo.graphicsDeviceName, api = SystemInfo.graphicsDeviceType.ToString(), memoryMB = SystemInfo.systemMemorySize,
                screen = new { width = Screen.width, height = Screen.height, dpi = Screen.dpi, safeArea = new { Screen.safeArea.x, Screen.safeArea.y, Screen.safeArea.width, Screen.safeArea.height }, orientation = Screen.orientation.ToString() },
                sceneArt = new { movingLayers = presentation.layers.Select(layer => layer.target.name).ToArray(), crystalIdleGlow = presentation.crystalGlow != null,
                    towerAttackExists = _view.GetComponentsInChildren<Transform>(true).Any(t => t.name.Contains("Laser") || t.name.Contains("MagicBolt")),
                    cloudLightning = presentation.stormFlash != null, cloudIntensity = presentation.stormFlash != null ? presentation.stormFlash.Intensity : 0,
                    cloudActive = presentation.stormFlash != null && presentation.stormFlash.gameObject.activeSelf,
                    effectsButtonExists = _view.GetComponentsInChildren<Transform>(true).Any(t => t.name == "AmbientMotion") },
                language = presentation.English ? "en" : "ko", motion = presentation.AmbientMotion,
                powerSave = GamePresentationSettings.PowerSave, targetFps = Application.targetFrameRate,
                lowSpec = GamePresentationSettings.LowSpec, languagePopup = presentation.languagePopup != null && presentation.languagePopup.activeSelf,
                audioOutputRms = Math.Sqrt(audioPower / _audioSamples.Length),
                currentLanguage = presentation.currentLanguage != null ? presentation.currentLanguage.text : "",
                lowSpecToggle = settings != null ? settings.tglLowSpec.isOn : (bool?)null,
                music = music != null && music.clip != null ? new { clip = music.clip.name, music.isPlaying, music.time, music.volume, length = music.clip.length, loadType = music.clip.loadType.ToString(), audioListenerVolume = AudioListener.volume } : null,
                popup = _view.popupLogin.activeSelf, hint = _view.pressHint.text,
                controls,
                swordAngle = presentation.layers.First(l => l.target.name == "Sword").target.localEulerAngles.z,
                textures,
                unityMemory = Profiler.GetTotalAllocatedMemoryLong(), monoUsed = Profiler.GetMonoUsedSizeLong(), monoHeap = Profiler.GetMonoHeapSizeLong(),
                activeGraphics = _view.GetComponentsInChildren<Graphic>().Length
            });
        }

        void Write(string id, object value)
        {
            string name = string.Concat(id.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_'));
            File.WriteAllText(Path.Combine(_directory, "lobby-" + name + ".json"), JsonConvert.SerializeObject(value, Formatting.Indented));
            Debug.Log("[Lobby Device QA] " + name);
        }

        void OnDestroy()
        {
            _gcRecorder.Dispose(); _cpuRecorder.Dispose(); _drawRecorder.Dispose();
            _lobbyRecorder.Dispose(); _damageRecorder.Dispose(); _verticesRecorder.Dispose();
        }
    }
}
#endif
