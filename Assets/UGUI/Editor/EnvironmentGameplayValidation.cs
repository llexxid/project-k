using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using KingdomIdle.Balance;
using KingdomIdle.Combat;
using KingdomIdle.MageTower;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Scripts.Core;
using Scripts.Core.Manager;
using Scripts.Monster;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>Real bootstrap/main/dungeon Play Mode, isolated local account, actual actors and spell casts.</summary>
    [InitializeOnLoad]
    public static class EnvironmentGameplayValidation
    {
        const string Active = "EnvironmentGameplayValidation.Active";
        static readonly string[] Pools = { "Stage01_ForestDirt", "Stage02_ForestGrass", "Stage03_DeepForest", "GoldDungeon", "RubyWasteland" };
        static readonly string[] Modes = { "Main", "Boss", "Special" };
        static readonly (int width, int height)[] Ratios = { (540, 960), (432, 1008), (720, 960) };
        static readonly Stack<IEnumerator> routines = new();
        static readonly List<string> errors = new();
        static readonly JArray checks = new(), skillChecks = new(), stageChecks = new(), hitFlashChecks = new();
        static JObject hitFlashShaderAudit;
        static IDisposable testSession;
        static double deadline, started;
        static string account;
        static int changedExistingSnapshots;
        static object initialChangedListeners, initialAccountListeners;
        static readonly BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;
        static GameObject temporaryEnvironment;
        static Renderer[] originalBackground;
        static bool[] originalBackgroundEnabled;
        static string Output => SessionState.GetString(Active + ".Output", "");

        static EnvironmentGameplayValidation() => EditorApplication.playModeStateChanged += State;
        public static void PrepareAndRun()
        {
            EnvironmentPolishValidation.PrepareAndCaptureAfter();
            Run();
        }
        public static void Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Start gameplay validation from Edit Mode.");
            if (EditorSettings.enterPlayModeOptionsEnabled && (EditorSettings.enterPlayModeOptions & EnterPlayModeOptions.DisableDomainReload) != 0)
                throw new InvalidOperationException("Isolated environment validation requires the existing normal domain-reload Play Mode configuration.");
            for (int i = 0; i < SceneManager.sceneCount; i++) if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open editor scenes before starting isolated validation.");
            string directory = Path.Combine(Environment.GetEnvironmentVariable("ENVIRONMENT_POLISH_OUTPUT") ??
                "Docs/ArtPreparation/Validation/EnvironmentPolish20261001/revision2", "gameplay");
            if (File.Exists(Path.Combine(directory, "report.json")))
                directory += "-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            Directory.CreateDirectory(directory);
            SessionState.SetString(Active + ".Output", directory);
            BackupExistingProfiles();
            SessionState.SetString(Active + ".Scenes", JsonConvert.SerializeObject(EditorSceneManager.GetSceneManagerSetup()));
            SessionState.SetBool(Active + ".Failed", false);
            SessionState.SetBool(Active + ".Completing", false);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool(Active, true);
            EditorApplication.isPlaying = true;
        }

        static void State(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Active, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                try
                {
                    errors.Clear(); checks.Clear(); skillChecks.Clear(); stageChecks.Clear(); hitFlashChecks.Clear(); routines.Clear();
                    hitFlashShaderAudit = AuditHitFlashShader();
                    if (!(bool)hitFlashShaderAudit["valid"]) errors.Add("Hit-flash shader graph is not configured for transparent, depth-write-disabled sprite rendering.");
                    started = EditorApplication.timeSinceStartup; deadline = started + 900;
                    changedExistingSnapshots = 0;
                    initialChangedListeners = typeof(LocalProgression).GetField("Changed", StaticPrivate)?.GetValue(null);
                    initialAccountListeners = typeof(LocalProgression).GetField("AccountChanged", StaticPrivate)?.GetValue(null);
                    SessionState.SetString(Active + ".PreviousAccount", LocalProgression.AccountKey ?? "");
                    testSession = LocalProgression.BeginTestSession();
                    LocalProgression.OpenTestAccount("environment-" + Guid.NewGuid().ToString("N"));
                    account = LocalProgression.AccountKey;
                    if (!account.StartsWith("balance-qa-environment-", StringComparison.Ordinal) || !LocalProgression.IsLocalAuthority)
                        throw new InvalidOperationException("An isolated local QA account is required.");
                    bool seeded = LocalProgression.Execute("environment-visual-fixture", s =>
                    {
                        s.Modules["imported"] = "Isolated environment Play Mode fixture";
                        s.Modules["inventory-imported"] = s.Modules["mage-imported"] = "1";
                        s.AttackLevel = 0; s.HealthLevel = 136; s.MainStage = 0x20003000A;
                        s.ActiveDungeon = null; s.ActiveBattleId = null;
                        s.GoldTickets = s.RubyTickets = 2; s.TicketDay = LocalProgression.KstDay;
                        string[] jobs = { "Elite_Knight", "Spearman", "Elite_Mage" };
                        for (int i = 0; i < 3; i++) { s.Jobs[i] = jobs[i]; s.UnlockedJobs[i] = new HashSet<string> { "Spearman", jobs[i] }; }
                        for (int c = 1; c <= 3; c++) for (int w = 1; w <= 11; w++) s.MainClears.Add(0x200000000L | ((long)c << 16) | (uint)w);
                        foreach (int id in new[] { 0, 9 }) s.MageSkills[id] = new MageSave { Enhance = 0, Awaken = 0, BloomEnabled = false };
                        for (int slot = 0; slot < 5; slot++) s.MageSlots[slot] = -1;
                        return true;
                    });
                    if (!seeded) throw new InvalidOperationException(LocalProgression.LastError);
                    Time.captureDeltaTime = 1f / 60f;
                    Application.logMessageReceived += Log;
                    routines.Push(Exercise()); EditorApplication.update += Tick;
                }
                catch (Exception e) { errors.Add(e.ToString()); Finish(); }
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
                try { CompleteAfterPlayMode(); }
                catch (Exception e)
                {
                    SessionState.SetBool(Active + ".Failed", true);
                    File.WriteAllText(Path.Combine(Output, "restoration-failure.txt"), e.ToString());
                    Debug.LogException(e);
                }
                SessionState.SetBool(Active, false);
                var setups = JsonConvert.DeserializeObject<SceneSetup[]>(SessionState.GetString(Active + ".Scenes", "[]"));
                if (setups != null && setups.Length > 0 && setups.All(s => !string.IsNullOrEmpty(s.path)))
                    EditorSceneManager.RestoreSceneManagerSetup(setups);
                if (Application.isBatchMode) EditorApplication.Exit(SessionState.GetBool(Active + ".Failed", false) ? 1 : 0);
            }
        }
        static void Log(string message, string trace, LogType type)
        { if (type == LogType.Exception || type == LogType.Assert) errors.Add(message + "\n" + trace); }
        static void Tick()
        {
            if (!EditorApplication.isPlaying || routines.Count == 0) return;
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Environment Play Mode exceeded 900 seconds.");
                while (routines.Count > 0)
                {
                    IEnumerator routine = routines.Peek();
                    if (!routine.MoveNext()) { routines.Pop(); continue; }
                    if (routine.Current is IEnumerator nested) { routines.Push(nested); continue; }
                    return;
                }
                Finish();
            }
            catch (Exception e) { errors.Add(e.ToString()); Finish(); }
        }
        static void Finish()
        {
            routines.Clear(); EditorApplication.update -= Tick;
            Time.timeScale = 0;
            try { RestoreBackground(); }
            catch (Exception e) { errors.Add(e.ToString()); }
            // Keep the QA account bound while OnApplicationQuit/OnDestroy and scene unload run.
            // Restoring here lets live Changed listeners reopen/migrate local-guest and quit saves it.
            bool valid = errors.Count == 0 && checks.Count == 90 && skillChecks.Count >= 2 && hitFlashChecks.Count == 3 &&
                hitFlashChecks.All(c => (bool)c["valid"]) &&
                checks.All(c => (bool)c["sorting"]["valid"] && (bool)c["opaquePixelOcclusion"]["valid"] && c["aspectOcclusion"].All(a => (bool)a["valid"]));
            File.WriteAllText(Path.Combine(Output, "report-pending-exit.json"), JsonConvert.SerializeObject(new
            {
                valid, actualPlayMode = true, account,
                authenticationProviderInvoked = false, cloudWriteInvoked = false,
                hudIncluded = false, uiProjectionValidated = false,
                scope = "Actual bootstrap/main/dungeon scene, actors and casts, captured as world-only camera views. Each saved environment is temporarily substituted for controlled visual comparison; assets are never saved. Camera aspect ratios are validated; HUD, safe area and physical-device responsiveness are not claimed.",
                durationSeconds = EditorApplication.timeSinceStartup - started, errors, stageChecks, checks, skillChecks, hitFlashShaderAudit, hitFlashChecks
            }, Formatting.Indented));
            SessionState.SetBool(Active + ".Completing", true);
            EditorApplication.isPlaying = false;
        }

        static void CompleteAfterPlayMode()
        {
            string pending = Path.Combine(Output, "report-pending-exit.json");
            JObject report = File.Exists(pending) ? JObject.Parse(File.ReadAllText(pending)) : new JObject { ["valid"] = false, ["errors"] = new JArray("Play Mode stopped before completion.") };
            try
            {
                // The original delegates belong to the pre-test domain context; discard only QA-added
                // listeners after all QA scene objects have gone. Dispose cannot awaken destroyed UI.
                typeof(LocalProgression).GetField("Changed", StaticPrivate)?.SetValue(null, initialChangedListeners);
                typeof(LocalProgression).GetField("AccountChanged", StaticPrivate)?.SetValue(null, initialAccountListeners);
                testSession?.Dispose();
            }
            catch (Exception e) { errors.Add("Post-Play-Mode restoration: " + e); }
            finally { testSession = null; Time.captureDeltaTime = 0; Time.timeScale = 1; }
            string previous = SessionState.GetString(Active + ".PreviousAccount", "");
            bool restored = (LocalProgression.AccountKey ?? "") == previous;
            if (!restored) errors.Add("The pre-test account context was not restored after scene shutdown.");
            var baseline = JArray.Parse(File.ReadAllText(SessionState.GetString(Active + ".ProfileBaseline", "")));
            var differences = new JArray();
            foreach (JToken item in baseline)
            {
                string source = (string)item["source"], backup = (string)item["backup"];
                if (!File.Exists(source) || !File.ReadAllBytes(source).SequenceEqual(File.ReadAllBytes(backup)))
                    differences.Add(Path.GetFileName(source));
            }
            changedExistingSnapshots = differences.Count;
            if (changedExistingSnapshots != 0) errors.Add("Existing local progression files changed after complete Play Mode shutdown: " + changedExistingSnapshots);
            report["errors"] = new JArray(((JArray)report["errors"]).Values<string>().Concat(errors).Distinct());
            report["valid"] = (bool)report["valid"] && restored && changedExistingSnapshots == 0 && report["errors"].Count() == 0;
            report["changedExistingSnapshots"] = changedExistingSnapshots;
            report["changedExistingFileNames"] = differences;
            report["protectedExistingFileCount"] = baseline.Count;
            report["accountRestoredAfterRun"] = restored;
            report["preservationCheckedAfterPlayModeExit"] = true;
            report["profileFilesAutomaticallyRestored"] = false;
            File.WriteAllText(Path.Combine(Output, "report.json"), report.ToString(Formatting.Indented));
            SessionState.SetBool(Active + ".Failed", !(bool)report["valid"]);
            Debug.Log("ENVIRONMENT GAMEPLAY " + ((bool)report["valid"] ? "PASSED" : "FAILED") + ": " + Path.GetFullPath(Output));
        }

        static void BackupExistingProfiles()
        {
            string privateRoot = Environment.GetEnvironmentVariable("ENVIRONMENT_QA_PRIVATE_OUTPUT");
            if (string.IsNullOrEmpty(privateRoot)) throw new InvalidOperationException("Set ENVIRONMENT_QA_PRIVATE_OUTPUT to a private work directory before running profile-preserving validation.");
            string backupDirectory = Path.Combine(privateRoot, "before-run-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(backupDirectory);
            string directory = Path.Combine(Application.persistentDataPath, "progression-local-v1");
            using var hash = SHA256.Create();
            var baseline = new JArray();
            foreach (string source in Directory.Exists(directory) ? Directory.GetFiles(directory) : Array.Empty<string>())
            {
                string backup = Path.Combine(backupDirectory, Path.GetFileName(source));
                byte[] bytes = File.ReadAllBytes(source);
                File.WriteAllBytes(backup, bytes);
                baseline.Add(new JObject { ["source"] = source, ["backup"] = backup, ["sha256"] = Convert.ToBase64String(hash.ComputeHash(bytes)) });
            }
            string manifest = Path.Combine(backupDirectory, "baseline.json");
            File.WriteAllText(manifest, baseline.ToString(Formatting.Indented));
            SessionState.SetString(Active + ".ProfileBaseline", manifest);
        }
        static IEnumerator Exercise()
        {
            var boot = SceneManager.LoadSceneAsync("bootstrap");
            while (!boot.isDone) yield return null;
            while (LoadManager.Instance == null || Object.FindFirstObjectByType<TitleScreenView>() == null) yield return null;
            LoadManager.Instance.LoadAsyncScene(eSceneType.main);
            while (StageManager.Instance == null || UserManager.Instance?.GetPlayers()?.Count != 3) yield return null;
            yield return Ready(null);
            MageTowerManager.Instance.SetAutoEnabled(false);
            StatEnhanceManager.Instance.ApplyToAllPlayers();
            JObject[] entries = JArray.Parse(File.ReadAllText("Docs/ArtPreparation/Manifests/environment-presets.json")).Cast<JObject>()
                .OrderBy(e => Array.IndexOf(Pools, (string)e["pool"]))
                .ThenBy(e => Array.IndexOf(Modes, (string)e["mode"]))
                .ThenBy(e => (string)e["name"], StringComparer.Ordinal).ToArray();
            if (entries.Length != 90) throw new InvalidOperationException("Expected all 90 saved environment presets.");
            for (int pool = 0; pool < Pools.Length; pool++)
            {
                Time.timeScale = 1;
                StageManager stage = StageManager.Instance;
                if (stage.CurrentDefinition.Type != eStageType.Main)
                { stage.ReturnToMainStage(); yield return Ready(null); }
                stage = StageManager.Instance;
                stage.SetBossAutoChallenge(false);
                eStage baseStage = (eStage)(0x200000000L | ((long)Mathf.Min(pool + 1, 3) << 16) | 10);
                stage.BeginStage(baseStage); yield return Ready(baseStage);
                if (pool >= 3)
                {
                    eStage dungeon = pool == 3 ? eStage.GoldDungeon1_1 : eStage.RubyDungeon1_1;
                    if (!stage.TryEnterDungeon(dungeon)) throw new InvalidOperationException("Isolated dungeon entry rejected: " + dungeon);
                    yield return Ready(dungeon);
                }
                string previousMode = null;
                foreach (JObject entry in entries.Where(e => (string)e["pool"] == Pools[pool]))
                {
                    string mode = (string)entry["mode"];
                    if (pool < 3 && previousMode != mode)
                    {
                        RestoreBackground(); Time.timeScale = 1;
                        eStage target = (eStage)(0x200000000L | ((long)(pool + 1) << 16) | (mode == "Boss" ? 11L : 10L));
                        StageManager.Instance.SetBossAutoChallenge(mode == "Boss");
                        StageManager.Instance.BeginStage(target); yield return Ready(target);
                    }
                    previousMode = mode;
                    Time.timeScale = 0;
                    UseBackground(entry);
                    int number = Array.IndexOf(entries, entry) + 1;
                    if (number == 1) yield return CheckHitFlash();
                    Renderer[] foreground = Foreground();
                    JObject sorting = Ordering(foreground, temporaryEnvironment.GetComponentsInChildren<Renderer>());
                    JObject pixels = PixelOcclusion(foreground, temporaryEnvironment.GetComponentsInChildren<Renderer>(), 540, 960);
                    var captures = new JArray(); var aspectOcclusion = new JArray();
                    if (((string)entry["name"]).EndsWith("_01", StringComparison.Ordinal))
                        foreach (var ratio in Ratios)
                        {
                            string filename = $"{number:00}_{entry["name"]}_live-world_{ratio.width}x{ratio.height}.png";
                            Capture(filename, ratio.width, ratio.height); captures.Add(filename);
                            if (ratio.width != 540) aspectOcclusion.Add(PixelOcclusion(foreground, temporaryEnvironment.GetComponentsInChildren<Renderer>(), ratio.width, ratio.height));
                        }
                    checks.Add(new JObject
                    {
                        ["number"] = number, ["name"] = entry["name"], ["pool"] = entry["pool"], ["mode"] = mode,
                        ["actualStage"] = StageManager.Instance.CurrentStage.ToString(), ["actualBossWave"] = StageManager.Instance.IsBossWave,
                        ["playerCount"] = CombatMotion.Players.Count(p => p != null && !p.IsDead),
                        ["enemyCount"] = CombatMotion.Monsters.Count(m => m != null && m.MonAction != eMonsterAction.Dead),
                        ["sorting"] = sorting, ["opaquePixelOcclusion"] = pixels, ["captures"] = captures, ["aspectOcclusion"] = aspectOcclusion,
                        ["actorFootprints"] = ActorFootprints()
                    });
                }
                RestoreBackground(); Time.timeScale = 1;
                if (pool == 2) yield return CheckSpell(0, entries.First(e => (string)e["pool"] == Pools[pool]));
                if (pool == 3) yield return CheckSpell(9, entries.First(e => (string)e["pool"] == Pools[pool]));
                Debug.Log("ENVIRONMENT LIVE: " + checks.Count + "/90 actual preset/actor occlusion checks");
            }
        }
        static IEnumerator Ready(eStage? expected)
        {
            double until = EditorApplication.timeSinceStartup + 65;
            while (StageManager.Instance == null || StageManager.Instance.CurrentRunState != eStageRunState.Running ||
                expected.HasValue && StageManager.Instance.CurrentStage != expected.Value ||
                !CombatMotion.Monsters.Any(m => m != null && m.MonAction != eMonsterAction.Dead))
            {
                if (EditorApplication.timeSinceStartup > until) throw new TimeoutException("Battle did not become ready: " + expected);
                yield return null;
            }
            float start = Time.time;
            Vector3[] before = CombatMotion.Players.Where(p => p != null).Select(p => p.transform.position).ToArray();
            while (Time.time - start < .8f || !CombatMotion.Monsters.Any(m => m != null && m.MonAction != eMonsterAction.Dead))
            { if (EditorApplication.timeSinceStartup > until) throw new TimeoutException("No live enemy after settle."); yield return null; }
            stageChecks.Add(new JObject { ["stage"] = StageManager.Instance.CurrentStage.ToString(),
                ["state"] = StageManager.Instance.CurrentRunState.ToString(), ["simulatedSeconds"] = Time.time - start,
                ["playerCount"] = CombatMotion.Players.Count(p => p != null && !p.IsDead),
                ["enemyCount"] = CombatMotion.Monsters.Count(m => m != null && m.MonAction != eMonsterAction.Dead),
                ["actorMotionObserved"] = CombatMotion.Players.Where(p => p != null).Select((p, i) => i < before.Length && Vector3.Distance(p.transform.position, before[i]) > .001f).Any(x => x) });
        }
        static IEnumerator CheckSpell(int id, JObject entry)
        {
            var mage = MageTowerManager.Instance;
            for (int slot = 0; slot < 5; slot++) mage.Unequip(slot);
            mage.SetBloomEnabled(id, false); mage.Equip(0, id);
            while (mage.IsOnCooldown(0) || !CombatMotion.Monsters.Any(m => m != null && m.MonAction != eMonsterAction.Dead)) yield return null;
            MageSkillDiagnostics.Events.Clear();
            if (!mage.CastSkill(0)) throw new InvalidOperationException("Real skill cast rejected: " + id);
            float start = Time.time; bool captured = false;
            while (mage.IsCasting(0))
            {
                var effects = Object.FindObjectsByType<PooledSpellVfx>(FindObjectsSortMode.None).Where(v => v.isActiveAndEnabled).ToArray();
                if (!captured && Time.time - start > .08f && effects.Any(v => v.GetComponentsInChildren<SpriteRenderer>().Any(r => r.enabled && r.sprite != null && r.color.a > .1f)))
                {
                    Time.timeScale = 0; UseBackground(entry);
                    Renderer[] foreground = Foreground();
                    JObject ordering = Ordering(foreground, temporaryEnvironment.GetComponentsInChildren<Renderer>());
                    JObject pixels = PixelOcclusion(foreground, temporaryEnvironment.GetComponentsInChildren<Renderer>(), 540, 960);
                    string name = $"skill-{id}-actual-cast.png"; Capture(name, 540, 960);
                    skillChecks.Add(new JObject { ["skill"] = id, ["bloom"] = false, ["image"] = name,
                        ["activeEffects"] = JArray.FromObject(effects.Select(e => e.name)), ["sorting"] = ordering, ["opaquePixelOcclusion"] = pixels });
                    if (!(bool)ordering["valid"] || !(bool)pixels["valid"]) errors.Add("Skill background occlusion failed: " + id);
                    RestoreBackground(); Time.timeScale = 1; captured = true;
                }
                yield return null;
            }
            if (!captured) throw new InvalidOperationException("No actual active effect captured for skill " + id);
            JObject check = (JObject)skillChecks.Last;
            check["events"] = JArray.FromObject(MageSkillDiagnostics.Events);
            check["damageEventCount"] = MageSkillDiagnostics.Events.Count(e => e.kind == "damage");
            if (id == 0 && MageSkillDiagnostics.Events.Count(e => e.kind == "bolt") != 3)
                throw new InvalidOperationException("Non-bloom lightning did not produce exactly 3 real strikes.");
        }

        static JObject AuditHitFlashShader()
        {
            const string path = "Assets/_Project/Shader/Monster/MonsterHitEffect.shadergraph";
            var objects = new List<JObject>();
            using (var reader = new JsonTextReader(new StringReader(File.ReadAllText(path))) { SupportMultipleContent = true })
                while (reader.Read()) objects.Add(JObject.Load(reader));
            JObject target = objects.Single(o => (string)o["m_Type"] == "UnityEditor.Rendering.Universal.ShaderGraph.UniversalTarget");
            string subTargetId = (string)target["m_ActiveSubTarget"]["m_Id"];
            string subTarget = (string)objects.Single(o => (string)o["m_ObjectId"] == subTargetId)["m_Type"];
            bool valid = (int)target["m_SurfaceType"] == 1 && (int)target["m_ZWriteControl"] == 2 &&
                !(bool)target["m_Sort3DAs2DCompatible"] && subTarget.EndsWith(".UniversalSpriteUnlitSubTarget", StringComparison.Ordinal);
            return new JObject
            {
                ["valid"] = valid, ["source"] = path, ["subTarget"] = subTarget,
                ["surfaceType"] = target["m_SurfaceType"], ["zWriteControl"] = target["m_ZWriteControl"],
                ["sort3DAs2DCompatible"] = target["m_Sort3DAs2DCompatible"],
                ["method"] = "Static graph-source contract: Universal Sprite Unlit, SurfaceType Transparent(1), ZWriteControl ForceDisabled(2), Sort3DAs2DCompatible false. This is separate source evidence; an unexposed material property is not treated as proof of pass depth state. Live queue and dual-matte pixel comparisons remain required."
            };
        }
        static long CurrentHp(Monster monster)
        {
            var stat = (Monster.MonsterStat)typeof(Monster).GetField("_stat", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(monster);
            return stat._hp + stat._extraHp;
        }
        static float FlashAmount(SpriteRenderer renderer)
        {
            var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block);
            return block.GetFloat(Shader.PropertyToID("_FlashAmount"));
        }
        static IEnumerator CheckHitFlash()
        {
            // Apply controlled damage to an already spawned actor through the normal combat path.
            // Only the initial baseline is reset; restoration must come from the real coroutine.
            Monster target = CombatMotion.Monsters.Where(m => m != null && m.MonAction != eMonsterAction.Dead &&
                m.GetComponent<MonsterHitFlash>() != null && m.GetComponentsInChildren<SpriteRenderer>().Any(r => r.enabled && r.sprite != null &&
                    r.sharedMaterial != null && r.sharedMaterial.shader.name == "Shader Graphs/MonsterHitEffect"))
                .OrderByDescending(CurrentHp).FirstOrDefault();
            if (target == null || CurrentHp(target) <= 1) throw new InvalidOperationException("No live hit-flash enemy available for deterministic real-hit coverage.");
            var owner = CombatMotion.Players.First(p => p != null && !p.IsDead);
            var flash = target.GetComponent<MonsterHitFlash>();
            SpriteRenderer[] bodies = target.GetComponentsInChildren<SpriteRenderer>().Where(r => r.enabled && r.sprite != null &&
                r.sharedMaterial != null && r.sharedMaterial.shader.name == "Shader Graphs/MonsterHitEffect").ToArray();
            Material[] materials = bodies.Select(r => r.sharedMaterial).ToArray();
            long hpBefore = CurrentHp(target);
            flash.ResetFlash();
            CaptureHitFlashPhase("baseline", target, bodies, materials, hpBefore, hpBefore, false);
            bool survived = target.TakeDamage(new ActiveSkill.DamageProxy(1, owner));
            long hpAfter = CurrentHp(target);
            if (!survived || hpAfter != hpBefore - 1 || !bodies.Any(r => FlashAmount(r) > .4f))
                throw new InvalidOperationException("The normal TakeDamage path did not apply exactly 1 HP and activate the real hit-flash property block.");
            CaptureHitFlashPhase("active", target, bodies, materials, hpBefore, hpAfter, true);
            float start = Time.time;
            double until = EditorApplication.timeSinceStartup + 10;
            Time.timeScale = 1;
            while (Time.time - start < .1f || bodies.Any(r => r != null && FlashAmount(r) > .0001f))
            {
                if (target == null || target.MonAction == eMonsterAction.Dead) throw new InvalidOperationException("Hit-flash target died before restoration could be observed.");
                if (EditorApplication.timeSinceStartup > until) throw new TimeoutException("Real hit-flash coroutine did not restore the body to normal.");
                yield return null;
            }
            Time.timeScale = 0;
            CaptureHitFlashPhase("restored", target, bodies, materials, hpBefore, CurrentHp(target), false);
            ((JObject)hitFlashChecks.Last)["elapsedSimulationSeconds"] = Time.time - start;
        }
        static void CaptureHitFlashPhase(string phase, Monster target, SpriteRenderer[] bodies, Material[] materials,
            long hpBefore, long hpNow, bool expectActive)
        {
            Renderer[] background = temporaryEnvironment.GetComponentsInChildren<Renderer>();
            JObject ordering = Ordering(Foreground(), background);
            JObject pixels = PixelOcclusion(Foreground(), background, 540, 960);
            JObject targetPixels = PixelOcclusion(bodies, background, 540, 960);
            float[] amounts = bodies.Select(FlashAmount).ToArray();
            bool sameMaterials = bodies.Select((r, i) => r.sharedMaterial == materials[i]).All(v => v);
            bool correctAmount = expectActive ? amounts.All(a => a > .4f) : amounts.All(a => a <= .0001f);
            string filename = "hit-flash-" + phase + "-actual-world.png";
            Capture(filename, 540, 960);
            bool valid = correctAmount && sameMaterials && (bool)ordering["valid"] && (bool)pixels["valid"] && (bool)targetPixels["valid"];
            hitFlashChecks.Add(new JObject
            {
                ["valid"] = valid, ["phase"] = phase, ["target"] = target.name, ["targetInstanceId"] = target.GetInstanceID(),
                ["actualStage"] = StageManager.Instance.CurrentStage.ToString(), ["image"] = filename,
                ["hpBeforeControlledHit"] = hpBefore, ["hpNow"] = hpNow, ["flashAmounts"] = JArray.FromObject(amounts),
                ["sameActualMaterials"] = sameMaterials, ["sorting"] = ordering, ["opaquePixelOcclusion"] = pixels,
                ["targetOnlyOpaquePixelOcclusion"] = targetPixels,
                ["method"] = "Actual spawned enemy and player in the isolated battle. Initial ResetFlash baseline, then Monster.TakeDamage(ActiveSkill.DamageProxy(1, livePlayer)); active state read from each real renderer MaterialPropertyBlock. Normal restoration is observed after the actual coroutine runs, without a reset or material replacement."
            });
            if (!valid) errors.Add("Deterministic hit-flash background coverage failed: " + phase);
        }

        static void UseBackground(JObject entry)
        {
            if (temporaryEnvironment != null) Object.DestroyImmediate(temporaryEnvironment);
            if (originalBackground == null)
            {
                var controller = Object.FindFirstObjectByType<StageBackgroundController>();
                if (controller == null) throw new InvalidOperationException("No live stage background controller.");
                originalBackground = controller.GetComponentsInChildren<Renderer>(true);
                originalBackgroundEnabled = originalBackground.Select(r => r.enabled).ToArray();
                foreach (Renderer r in originalBackground) r.enabled = false;
            }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>((string)entry["path"]);
            temporaryEnvironment = Object.Instantiate(prefab);
            temporaryEnvironment.name = "EnvironmentGameplayProbe_" + entry["name"];
            foreach (Tilemap map in temporaryEnvironment.GetComponentsInChildren<Tilemap>()) map.RefreshAllTiles();
        }
        static void RestoreBackground()
        {
            if (temporaryEnvironment != null) Object.DestroyImmediate(temporaryEnvironment);
            temporaryEnvironment = null;
            if (originalBackground != null)
                for (int i = 0; i < originalBackground.Length; i++) if (originalBackground[i] != null) originalBackground[i].enabled = originalBackgroundEnabled[i];
            originalBackground = null; originalBackgroundEnabled = null;
        }
        static Renderer[] Foreground() => CombatMotion.Players.Where(p => p != null && !p.IsDead).SelectMany(p => p.GetComponentsInChildren<SpriteRenderer>()).Cast<Renderer>()
            .Concat(CombatMotion.Monsters.Where(m => m != null && m.MonAction != eMonsterAction.Dead).SelectMany(m => m.GetComponentsInChildren<SpriteRenderer>()))
            .Concat(Object.FindObjectsByType<PooledSpellVfx>(FindObjectsSortMode.None).Where(v => v.isActiveAndEnabled).SelectMany(v => v.GetComponentsInChildren<SpriteRenderer>()))
            .Where(r => r.enabled && r.gameObject.activeInHierarchy).Distinct().ToArray();
        static JArray ActorFootprints() => JArray.FromObject(CombatMotion.Players.Where(p => p != null && !p.IsDead)
            .Select(p => new { kind = "player", p.name, x = p.VfxFootPosition.x, y = p.VfxFootPosition.y, radius = CombatMotion.PlayerRadius })
            .Concat(CombatMotion.Monsters.Where(m => m != null && m.MonAction != eMonsterAction.Dead)
                .Select(m => new { kind = "enemy", m.name, x = m.FootPosition.x, y = m.FootPosition.y, radius = m.BodyRadius })));

        static JObject RendererInfo(Renderer renderer)
        {
            SortingGroup group = renderer.GetComponentsInParent<SortingGroup>().LastOrDefault(g => g.enabled);
            int layer = group != null ? group.sortingLayerID : renderer.sortingLayerID;
            return new JObject { ["name"] = renderer.name, ["layer"] = SortingLayer.IDToName(layer), ["layerValue"] = SortingLayer.GetLayerValueFromID(layer),
                ["order"] = group != null ? group.sortingOrder : renderer.sortingOrder,
                ["outerSortingGroup"] = group != null ? group.name : null,
                ["materials"] = JArray.FromObject(renderer.sharedMaterials.Where(m => m != null).Select(m => new
                    { m.name, shader = m.shader.name, queue = m.renderQueue, hasDepthWriteProperty = m.HasProperty("_ZWrite"),
                        zWrite = m.HasProperty("_ZWrite") ? (float?)m.GetFloat("_ZWrite") : null })) };
        }
        static JObject Ordering(Renderer[] foreground, Renderer[] background)
        {
            var fg = foreground.Select(RendererInfo).ToArray(); var bg = background.Select(RendererInfo).ToArray();
            var issues = new JArray();
            foreach (JObject back in bg) foreach (JObject front in fg)
            {
                bool lower = (int)back["layerValue"] < (int)front["layerValue"] ||
                    (int)back["layerValue"] == (int)front["layerValue"] && (int)back["order"] < (int)front["order"];
                if (!lower) issues.Add(back["name"] + " sorts at/above " + front["name"]);
                foreach (JToken bm in back["materials"])
                {
                    if (bm["zWrite"].Type != JTokenType.Null && (float)bm["zWrite"] != 0) issues.Add(back["name"] + " writes depth");
                    foreach (JToken fm in front["materials"])
                        if ((int)bm["queue"] > (int)fm["queue"]) issues.Add(back["name"] + " material queue exceeds foreground " + front["name"]);
                }
            }
            return new JObject { ["valid"] = fg.Length >= 3 && bg.Length == 3 && issues.Count == 0,
                ["background"] = new JArray(bg), ["foreground"] = new JArray(fg), ["issues"] = issues };
        }

        static JObject PixelOcclusion(Renderer[] foreground, Renderer[] background, int width, int height)
        {
            if (Time.timeScale != 0) throw new InvalidOperationException("Pixel comparison requires a frozen live frame.");
            int sampledFrame = Time.frameCount;
            Camera camera = Camera.main;
            Renderer[] all = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            bool[] enabled = all.Select(r => r.enabled).ToArray();
            Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            bool[] canvasEnabled = canvases.Select(c => c.enabled).ToArray();
            Color previousColor = camera.backgroundColor; CameraClearFlags previousFlags = camera.clearFlags;
            try
            {
                var keep = new HashSet<Renderer>(foreground.Concat(background));
                foreach (Renderer renderer in all) renderer.enabled &= keep.Contains(renderer);
                foreach (Canvas canvas in canvases) canvas.enabled = false;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                Color32[] combined = ReadFrame(width, height, null);
                foreach (Renderer renderer in background) renderer.enabled = false;
                Color32[] blackForeground = ReadFrame(width, height, null);
                camera.backgroundColor = Color.white;
                Color32[] whiteForeground = ReadFrame(width, height, null);
                int opaque = 0, changed = 0;
                for (int i = 0; i < combined.Length; i++)
                {
                    Color32 a = blackForeground[i], b = combined[i], white = whiteForeground[i];
                    // A pixel unaffected by switching the matte from black to white is opaque
                    // foreground coverage. This does not depend on URP preserving RT alpha.
                    if (Math.Abs(a.r - white.r) > 2 || Math.Abs(a.g - white.g) > 2 || Math.Abs(a.b - white.b) > 2) continue;
                    opaque++;
                    if (Math.Abs(a.r - b.r) > 3 || Math.Abs(a.g - b.g) > 3 || Math.Abs(a.b - b.b) > 3) changed++;
                }
                bool usableMask = opaque >= 100 && opaque < width * height * .65f;
                return new JObject { ["valid"] = usableMask && changed == 0 && sampledFrame == Time.frameCount, ["width"] = width, ["height"] = height,
                    ["sampledFrame"] = sampledFrame, ["sameFrame"] = sampledFrame == Time.frameCount,
                    ["opaqueForegroundPixels"] = opaque, ["backgroundChangedOpaquePixels"] = changed,
                    ["method"] = "Same frozen live frame, actual actor/effect renderers. Opaque foreground mask is the RGB-invariant intersection of black-matte and white-matte renders (all RGB deltas<=2); compare covered pixels with the full backdrop render at RGB tolerance3. Independent of RT alpha; canvases/unrelated world renderers transiently excluded." };
            }
            finally
            {
                for (int i = 0; i < all.Length; i++) if (all[i] != null) all[i].enabled = enabled[i];
                for (int i = 0; i < canvases.Length; i++) if (canvases[i] != null) canvases[i].enabled = canvasEnabled[i];
                camera.backgroundColor = previousColor; camera.clearFlags = previousFlags;
            }
        }
        static void Capture(string name, int width, int height)
        {
            // HP-bar bindings are projected in LateUpdate for the real Screen and Overlay canvas.
            // A one-off RenderTexture aspect change cannot validate their responsive positions.
            // Capture actual live world actors/VFX only, keeping every canvas configuration intact.
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)
                .Select(c => (canvas: c, enabled: c.enabled)).ToArray();
            try
            {
                foreach (var item in canvases) item.canvas.enabled = false;
                ReadFrame(width, height, Path.Combine(Output, name));
            }
            finally
            {
                foreach (var item in canvases) if (item.canvas != null) item.canvas.enabled = item.enabled;
                Canvas.ForceUpdateCanvases();
            }
        }
        static Color32[] ReadFrame(int width, int height, string path)
        {
            Camera camera = Camera.main;
            if (camera == null) throw new InvalidOperationException("No actual game camera.");
            RenderTexture active = RenderTexture.active, previous = camera.targetTexture;
            float aspect = camera.aspect;
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32); Texture2D pixels = null;
            try
            {
                target.Create(); camera.targetTexture = target; camera.aspect = (float)width / height;
                Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
                pixels = new Texture2D(width, height, TextureFormat.RGBA32, false);
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply();
                if (path != null) File.WriteAllBytes(path, pixels.EncodeToPNG());
                return pixels.GetPixels32();
            }
            finally
            {
                camera.targetTexture = previous; camera.aspect = aspect; RenderTexture.active = active;
                if (pixels != null) Object.DestroyImmediate(pixels);
                target.Release(); Object.DestroyImmediate(target);
            }
        }
    }
}
