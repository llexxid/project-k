#if LOBBY_DEVICE_QA && DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using KingdomIdle.Balance;
using KingdomIdle.MageTower;
using Newtonsoft.Json;
using Scripts.Core;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KingdomIdle.UGUI
{
    /// <summary>Opt-in development APK only. Real inputs are driven by adb; this captures actual runtime state.</summary>
    public sealed class SettingsDeviceProbe : MonoBehaviour
    {
        [Serializable] sealed class Command { public string id, action, value; }
        string _directory;
        SFXEntity _loop;
        bool _fixture;
        float _nextBurst;
        readonly float[] _samples = new float[512];
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install() => DontDestroyOnLoad(new GameObject("SettingsDeviceProbe", typeof(SettingsDeviceProbe)));

        IEnumerator Start()
        {
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var files = activity.Call<AndroidJavaObject>("getFilesDir")) _directory = files.Call<string>("getAbsolutePath");
            string path = Path.Combine(_directory, "settings-command.json");
            var tick = new WaitForSecondsRealtime(.1f);
            while (true)
            {
                if (File.Exists(path))
                {
                    Command command = null;
                    try
                    {
                        command = JsonConvert.DeserializeObject<Command>(File.ReadAllText(path)); File.Delete(path);
                        object result = null;
                        switch (command.action)
                        {
                            case "state": break;
                            case "open": UIManager.Instance.OpenSettings(); break;
                            case "acceptance": result = SettingsAcceptance.Run(); break;
                            case "damage": Damage(ulong.Parse(command.value)); break;
                            case "fixture": _fixture = command.value == "on"; break;
                            case "shake":
                                var camera = Camera.main; var shaker = camera.GetComponent<CameraShaker>() ?? camera.gameObject.AddComponent<CameraShaker>();
                                shaker.Shake(20, .5f); break;
                            case "effect":
                                if (_loop != null) { _loop.StopSFX(); SFXManager.Instance.DestroySFX(_loop); _loop = null; }
                                if (command.value == "on") SFXManager.Instance.GetSFX(eSFXType.Slash_Attack_SFX, Vector3.zero, Quaternion.identity, s => { _loop = s; s.PlaySFXLoop(); });
                                break;
                            case "amount":
                                result = LocalProgression.Execute("qa-format-amount", s => { s.Wallet[eCurrency.Gold] = long.Parse(command.value); return true; }); break;
                            case "loot":
                                var data = EquipmentManager.Instance.GetByRarity(eEquipmentRarity.Normal).First();
                                var item = new EquipmentSave { Id = Guid.NewGuid().ToString("N"), Code = data.itemCode };
                                bool ok = LocalProgression.Execute("qa-field-loot", s => EquipmentManager.Grant(s, item, true));
                                if (ok) { EquipmentManager.Instance.RestoreEquipment(); UIManager.Instance.NotifyFieldEquipment(item.Code); }
                                result = new { ok, item.Id, count = LocalProgression.State.Equipment.Count, pending = LocalProgression.State.PendingEquipment.Count }; break;
                            default: throw new ArgumentException("Unknown settings QA command.");
                        }
                        Canvas.ForceUpdateCanvases();
                        Write(command.id, new { result, state = Snapshot() });
                    }
                    catch (Exception ex) { Write(command?.id ?? "error", new { error = ex.ToString() }); }
                }
                yield return tick;
            }
        }
        void Update()
        {
            if (!_fixture || Time.unscaledTime < _nextBurst) return;
            _nextBurst = Time.unscaledTime + .35f;
            var camera = Camera.main; if (camera == null) return;
            var manager = FindFirstObjectByType<DamageTextManager>(); if (manager == null) return;
            for (int i = 0; i < 35; i++)
                manager.ShowWorldDamage(camera.ScreenToWorldPoint(new Vector3(Screen.width * (.16f + i % 7 * .11f), Screen.height * (.3f + i / 7 * .08f), 10)), 123456789012345UL + (ulong)i);
        }
        void Damage(ulong amount)
        {
            var manager = FindFirstObjectByType<DamageTextManager>();
            manager.ShowWorldDamage(Camera.main.ScreenToWorldPoint(new Vector3(Screen.width * .5f, Screen.height * .65f, 10)), amount);
        }
        object Snapshot()
        {
            var view = FindFirstObjectByType<SettingsModalView>();
            var manager = FindFirstObjectByType<DamageTextManager>();
            var camera = Camera.main;
            return new
            {
                width = Screen.width, height = Screen.height, dpi = Screen.dpi,
                style = NumberNotation.Style.ToString(), revision = NumberNotation.Revision,
                volume = AudioListener.volume, master = GameAudioSettings.Master, music = GameAudioSettings.Music, effects = GameAudioSettings.Effects, muted = GameAudioSettings.Muted,
                powerSave = GamePresentationSettings.PowerSave, lowSpec = GamePresentationSettings.LowSpec, keepAwake = GamePresentationSettings.KeepAwake, sleepTimeout = Screen.sleepTimeout,
                hideItem = GamePresentationSettings.HideItemNotifications, shake = GamePresentationSettings.ScreenShake,
                damage = PlayerPrefs.GetInt(UIManager.PrefKeyDamageText, 1) == 1, fps = Application.targetFrameRate,
                camera = camera == null ? null : new[] { camera.transform.localPosition.x, camera.transform.localPosition.y, camera.transform.localPosition.z },
                settingsOpen = view != null, panel = view == null ? null : Bounds(view.panel), viewport = view == null ? null : Bounds(view.scroll.viewport),
                scroll = view == null ? 1 : view.scroll.verticalNormalizedPosition,
                toggles = view == null ? null : view.GetComponentsInChildren<Toggle>().Select(t => new { t.name, t.isOn, bounds = Bounds((RectTransform)t.transform) }).ToArray(),
                sliders = view == null ? null : view.GetComponentsInChildren<Slider>().Select(s => new { s.name, s.value, bounds = Bounds((RectTransform)s.transform) }).ToArray(),
                sources = FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Select(s => new { s.name, s.volume, s.isPlaying, clip = s.clip?.name, s.time, peak = Peak(s), effect = s.GetComponent<SFXEntity>() != null }).ToArray(),
                labels = view == null ? null : view.GetComponentsInChildren<TMP_Text>().Select(t => new { t.name, t.text, t.isTextTruncated, width = t.preferredWidth, height = t.preferredHeight, logicalWidth = t.rectTransform.rect.width, logicalHeight = t.rectTransform.rect.height, bounds = Bounds(t.rectTransform) }).ToArray(),
                hits = manager == null || manager.layer == null ? null : manager.layer.GetComponentsInChildren<TMP_Text>().Select(t => new { t.text, t.isTextTruncated }).ToArray(),
                mainGold = FindFirstObjectByType<MainScreenView>()?.lblGold?.text,
                gold = LocalProgression.Balance(eCurrency.Gold), equipment = LocalProgression.State.Equipment.Count,
                toast = FindFirstObjectByType<ToastView>()?.label?.text,
            };
        }
        float Peak(AudioSource source)
        {
            if (!source.isPlaying) return 0;
            source.GetOutputData(_samples, 0); float peak = 0;
            foreach (float sample in _samples) peak = Mathf.Max(peak, Mathf.Abs(sample)); return peak;
        }
        static object Bounds(RectTransform rt)
        {
            var corners = new Vector3[4]; rt.GetWorldCorners(corners);
            var canvas = rt.GetComponentInParent<Canvas>(); var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var points = corners.Select(p => RectTransformUtility.WorldToScreenPoint(camera, p)).ToArray();
            float left = points.Min(p => p.x), right = points.Max(p => p.x), bottom = points.Min(p => p.y), top = points.Max(p => p.y);
            return new { x = left, y = Screen.height - top, width = right - left, height = top - bottom };
        }
        void Write(string id, object value) => File.WriteAllText(Path.Combine(_directory, "settings-" + id + ".json"), JsonConvert.SerializeObject(value, Formatting.Indented));
    }
}
#endif
