using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KingdomIdle.Balance;
using KingdomIdle.MageTower;
using Newtonsoft.Json;
using Scripts.Core;
using Scripts.Core.Manager;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>Real Editor Play Mode smoke and rendered captures. Uses its own local test profile.</summary>
    [InitializeOnLoad]
    public static class FoundationPlayValidation
    {
        const string Active = "FoundationPlayValidation.Active";
        const string Output = "Recordings/FoundationRevision/EditorPlay";
        static IEnumerator routine;
        static readonly List<string> checks = new(), errors = new();
        static double deadline;
        static FoundationPlayValidation()
        {
            EditorApplication.playModeStateChanged += State;
        }
        public static void Run()
        {
            Directory.CreateDirectory(Output);
            EnvironmentRichnessPreparation.Prepare();
            EquipmentFeaturePreparation.Prepare();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool(Active, true);
            EditorApplication.isPlaying = true;
        }
        static void State(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Active, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                checks.Clear(); errors.Clear();
                LocalProgression.OpenTestAccount("foundation-editor-20260920");
                LocalProgression.Execute("editor-fixture", s => {
                    s.Modules["imported"] = "Editor isolated fixture";
                    s.Modules["inventory-imported"] = s.Modules["mage-imported"] = "1";
                    s.AttackLevel = 55; s.HealthLevel = 136; s.MainStage = 0x20001000A;
                    s.ActiveDungeon = null; s.ActiveBattleId = null;
                    s.Wallet[eCurrency.Gold] = 200000; s.Wallet[eCurrency.Ruby] = 500;
                    string[] jobs = { "Elite_Knight", "Spearman", "Elite_Mage" };
                    for (int i = 0; i < 3; i++) { s.Jobs[i] = jobs[i]; s.UnlockedJobs[i] = new HashSet<string> { "Spearman", jobs[i] }; }
                    for (int i = 0; i < 10; i++) if (i != 6) s.MageSkills[i] = new MageSave { Enhance = 10, Awaken = 4, Fragments = 55 };
                    for (int i = 0; i < 5; i++) s.MageSlots[i] = -1;
                    return true;
                });
                Application.logMessageReceived += Log;
                routine = Exercise(); deadline = EditorApplication.timeSinceStartup + 200;
                EditorApplication.update += Tick;
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(Active, false);
                EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
                if (Application.isBatchMode) EditorApplication.Exit(errors.Count == 0 ? 0 : 1);
            }
        }
        static void Log(string text, string trace, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Assert) errors.Add(text + "\n" + trace);
        }
        static void Tick()
        {
            if (!EditorApplication.isPlaying || routine == null) return;
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Editor play validation timed out");
                if (routine.MoveNext()) return;
                Finish();
            }
            catch (Exception e) { errors.Add(e.ToString()); Finish(); }
        }
        static void Finish()
        {
            routine = null;
            File.WriteAllText(Output + "/report.json", JsonConvert.SerializeObject(new { checks, errors, actualPlayMode = true, loginBypassedForIsolatedEditorFixture = true }, Formatting.Indented));
            EditorApplication.isPlaying = false;
        }
        static IEnumerator Exercise()
        {
            var boot = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("bootstrap");
            while (!boot.isDone) yield return null;
            while (LoadManager.Instance == null || Object.FindFirstObjectByType<TitleScreenView>() == null) yield return null;
            Capture("title"); checks.Add("Title booted in Play Mode");
            LoadManager.Instance.LoadAsyncScene(eSceneType.main);
            while (StageManager.Instance == null || UserManager.Instance?.GetPlayers()?.Count != 3) yield return null;
            var manager = StageManager.Instance;
            while (manager.CurrentRunState != eStageRunState.Running) yield return null;
            MageTowerManager.Instance.SetAutoEnabled(false);
            StatEnhanceManager.Instance.ApplyToAllPlayers();
            foreach (int chapter in new[] { 1, 2, 3, 4 })
            {
                manager.BeginStage((eStage)(0x200000000L | ((long)chapter << 16) | 10));
                float end = Time.unscaledTime + 3;
                while (Time.unscaledTime < end || manager.CurrentRunState != eStageRunState.Running) yield return null;
                var background = Object.FindFirstObjectByType<StageBackgroundController>();
                if (string.IsNullOrEmpty(background?.CurrentPresetId)) throw new Exception("Background missing in chapter " + chapter);
                if (background.GetComponentsInChildren<Renderer>().Length != 3) throw new Exception("Unexpected background renderer growth");
                Capture("battle-stage-" + chapter);
                checks.Add("Chapter " + chapter + ": live actors, 3 background renderers, " + background.CurrentPresetId);
            }
            manager.BeginStage((eStage)0x20003000A);
            float ready = Time.unscaledTime + 3; while (Time.unscaledTime < ready) yield return null;
            foreach (int id in new[] { 8, 1, 5, 9 })
            {
                for (int i = 0; i < 5; i++) MageTowerManager.Instance.Unequip(i);
                MageTowerManager.Instance.Equip(0, id);
                while (MageTowerManager.Instance.IsOnCooldown(0)) yield return null;
                manager.BeginStage((eStage)0x20003000A);
                float targetsReady = Time.unscaledTime + 2;
                while (Time.unscaledTime < targetsReady || manager.CurrentRunState != eStageRunState.Running) yield return null;
                if (!MageTowerManager.Instance.CastSkill(0)) throw new Exception("Editor spell rejected " + id);
                float castAt = Time.time;
                while (Time.time - castAt < (id == 8 ? .65f : .5f)) yield return null;
                Capture("spell-" + id);
                while (MageTowerManager.Instance.IsCasting(0)) yield return null;
                checks.Add("Spell " + id + " entered, rendered, and completed in Play Mode");
            }
            LocalProgression.Execute("editor-dungeon-access", s => { s.GoldTickets = 2; s.TicketDay = LocalProgression.KstDay; for (int c = 1; c <= 3; c++) for (int w = 1; w <= 11; w++) s.MainClears.Add(0x200000000L | ((long)c << 16) | (uint)w); return true; });
            checks.Add("Editor fixture uses isolated account: " + LocalProgression.AccountKey);
            if (errors.Count > 0) throw new Exception("Play Mode exceptions were recorded");
        }

        static void Capture(string name)
        {
            var cam = Camera.main;
            if (cam == null) throw new Exception("No game camera for capture");
            var rt = new RenderTexture(720, 1544, 24, RenderTextureFormat.ARGB32); rt.Create();
            var old = cam.targetTexture; float aspect = cam.aspect;
            cam.targetTexture = rt; cam.aspect = 720f / 1544;
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.isRootCanvas))
                if (canvas.renderMode == RenderMode.ScreenSpaceOverlay) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = cam; canvas.planeDistance = 10; }
            Canvas.ForceUpdateCanvases(); cam.Render();
            var active = RenderTexture.active; RenderTexture.active = rt;
            var image = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); image.Apply();
            File.WriteAllBytes(Output + "/" + name + ".png", image.EncodeToPNG());
            RenderTexture.active = active; cam.targetTexture = old; cam.aspect = aspect;
            Object.DestroyImmediate(image); rt.Release(); Object.DestroyImmediate(rt);
        }
    }
}
