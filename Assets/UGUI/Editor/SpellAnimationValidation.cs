using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KingdomIdle.Balance;
using KingdomIdle.Combat;
using KingdomIdle.MageTower;
using Newtonsoft.Json;
using Scripts.Core;
using Scripts.Core.Manager;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>Focused real Play Mode checks for the restored chain and heavier meteor.</summary>
    [InitializeOnLoad]
    public static class SpellAnimationValidation
    {
        const string Active = "SpellAnimationValidation.Active";
        const string Output = "Recordings/SpellAnimationRevision/EditorPlay";
        static readonly List<object> checks = new();
        static readonly List<string> errors = new();
        static IEnumerator routine;
        static double deadline;
        static SpellAnimationValidation() => EditorApplication.playModeStateChanged += State;

        public static void Prepare()
        {
            MageSkillAssetPreparation.Build();
            MagePolishPreparation.BakeAuthoredGlyphs();
            KingdomIdle.EditorTools.Optimization.OptAtlases.CreateInBuildAtlases();
            MagePolishPreparation.Validate();
            AssetDatabase.SaveAssets();
        }
        public static void BuildDevice() => TitleLobbyDeviceBuild.Build();
        public static void Run()
        {
            Prepare();
            Directory.CreateDirectory(Output);
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
                // ReadPixels can stall the batch editor. A fixed game clock keeps the
                // capture itself from skipping animation poses; device QA uses real time.
                Time.captureDeltaTime = 1f / 60f;
                LocalProgression.OpenTestAccount("spell-animation-editor-20260920");
                LocalProgression.Execute("spell-animation-fixture", s => {
                    s.Modules["imported"] = "Isolated animation QA";
                    s.Modules["inventory-imported"] = s.Modules["mage-imported"] = "1";
                    s.AttackLevel = 0; s.HealthLevel = 136; s.MainStage = 0x20003000A;
                    s.ActiveDungeon = null; s.ActiveBattleId = null;
                    string[] jobs = { "Elite_Knight", "Spearman", "Elite_Mage" };
                    for (int i = 0; i < 3; i++) { s.Jobs[i] = jobs[i]; s.UnlockedJobs[i] = new HashSet<string> { "Spearman", jobs[i] }; }
                    for (int i = 0; i < 10; i++) if (i != 6) s.MageSkills[i] = new MageSave { Enhance = 0, Awaken = 0, Fragments = 55 };
                    for (int i = 0; i < 5; i++) s.MageSlots[i] = -1;
                    return true;
                });
                Application.logMessageReceived += Log;
                routine = Exercise(); deadline = EditorApplication.timeSinceStartup + 180;
                EditorApplication.update += Tick;
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(Active, false);
                Time.captureDeltaTime = 0;
                EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
                if (Application.isBatchMode) EditorApplication.Exit(errors.Count == 0 ? 0 : 1);
            }
        }
        static void Log(string text, string trace, LogType type)
        { if (type == LogType.Exception || type == LogType.Assert) errors.Add(text + "\n" + trace); }
        static void Tick()
        {
            if (!EditorApplication.isPlaying || routine == null) return;
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Spell Play Mode timed out");
                if (routine.MoveNext()) return;
                Finish();
            }
            catch (Exception e) { errors.Add(e.ToString()); Finish(); }
        }
        static void Finish()
        {
            routine = null;
            File.WriteAllText(Output + "/report.json", JsonConvert.SerializeObject(new { checks, errors, actualPlayMode = true, isolatedAccount = true, fixedCaptureStep = 1f / 60f }, Formatting.Indented));
            EditorApplication.isPlaying = false;
        }
        static IEnumerator Exercise()
        {
            var boot = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("bootstrap");
            while (!boot.isDone) yield return null;
            while (LoadManager.Instance == null || Object.FindFirstObjectByType<TitleScreenView>() == null) yield return null;
            LoadManager.Instance.LoadAsyncScene(eSceneType.main);
            while (StageManager.Instance == null || UserManager.Instance?.GetPlayers()?.Count != 3) yield return null;
            var stage = StageManager.Instance; var mage = MageTowerManager.Instance;
            while (stage.CurrentRunState != eStageRunState.Running) yield return null;
            mage.SetAutoEnabled(false); StatEnhanceManager.Instance.ApplyToAllPlayers();
            foreach (var variant in new[] { (0, 0, false), (0, 4, false), (0, 8, false), (0, 10, false), (0, 10, true), (2, 0, false), (8, 0, false), (8, 0, false) })
            {
                int id = variant.Item1, awaken = variant.Item2; bool bloom = variant.Item3;
                for (int i = 0; i < 5; i++) mage.Unequip(i);
                LocalProgression.Execute("spell-case", s => { s.MageSkills[id] = new MageSave { Enhance = 0, Awaken = awaken, BloomEnabled = bloom }; return true; });
                mage.NotifyCommitted(); mage.Equip(0, id);
                stage.BeginStage((eStage)0x20003000A);
                float ready = Time.time + 2;
                while (Time.time < ready || stage.CurrentRunState != eStageRunState.Running || mage.IsOnCooldown(0)) yield return null;
                MageSkillDiagnostics.Events.Clear();
                if (!mage.CastSkill(0)) throw new Exception("Cast rejected " + variant);
                float start = Time.time; bool captured = false;
                var frames = new HashSet<string>();
                float firstDamage = -1;
                while (mage.IsCasting(0))
                {
                    foreach (var effect in Object.FindObjectsByType<PooledSpellVfx>(FindObjectsSortMode.None))
                        if (effect.name.StartsWith(id == 8 ? "Meteor(" : id == 2 ? "FireTornado(" : "Lightning("))
                            foreach (var renderer in effect.GetComponentsInChildren<SpriteRenderer>())
                                if (renderer.sprite != null) frames.Add(renderer.sprite.name);
                    var damage = MageSkillDiagnostics.Events.FirstOrDefault(e => e.skill == id && e.kind == "damage");
                    if (damage != null && firstDamage < 0) firstDamage = damage.time - start;
                    if (!captured && Time.time-start > (id==8 ? 1.2f : bloom ? 2.05f : .22f))
                    { Capture($"spell-{id}-a{awaken}-b{bloom}-{checks.Count}"); captured = true; }
                    yield return null;
                }
                var bolts = MageSkillDiagnostics.Events.Where(e => e.kind == "bolt").ToArray();
                checks.Add(new { id, awaken, bloom, bolts=bolts.Length, boltTimes=bolts.Select(b=>b.time-start).ToArray(), distinctFrames=frames.Count, firstDamageSeconds=firstDamage, duration=Time.time-start });
                if (id==0 && !bloom)
                {
                    if (bolts.Length != 3 + awaken/4) throw new Exception("Wrong restored strike count");
                    for (int i=1;i<bolts.Length;i++) if (Mathf.Abs(bolts[i].time-bolts[i-1].time-2f/12f)>.08f) throw new Exception("Original lightning cadence changed");
                    if (frames.Any(f=>!f.StartsWith("LightningOriginal_"))) throw new Exception("Unexpected non-bloom lightning art");
                }
                if (id==0 && bloom && bolts.Length!=0) throw new Exception("Normal chain leaked into bloom");
                if (id==8 && (firstDamage<2.4f || firstDamage>2.8f || frames.Count<20)) throw new Exception("Meteor impact timing/animation failed");
                if (firstDamage<0) throw new Exception("No actual spell damage " + variant);
            }
        }
        static void Capture(string name)
        {
            var cam = Camera.main; if (cam==null) throw new Exception("No battle camera");
            var rt=new RenderTexture(720,1544,24); rt.Create();
            var target=cam.targetTexture; float aspect=cam.aspect; var active=RenderTexture.active;
            cam.targetTexture=rt;cam.aspect=720f/1544;cam.Render();RenderTexture.active=rt;
            var image=new Texture2D(720,1544,TextureFormat.RGBA32,false);
            image.ReadPixels(new Rect(0,0,720,1544),0,0);image.Apply();
            File.WriteAllBytes(Output+"/"+name+".png",image.EncodeToPNG());
            RenderTexture.active=active;cam.targetTexture=target;cam.aspect=aspect;
            Object.DestroyImmediate(image);rt.Release();Object.DestroyImmediate(rt);
        }
    }
}
