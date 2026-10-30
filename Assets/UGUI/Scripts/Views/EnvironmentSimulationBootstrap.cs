#if LOBBY_DEVICE_QA && DEVELOPMENT_BUILD && ENVIRONMENT_SIMULATION_QA
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using KingdomIdle.Balance;
using KingdomIdle.Combat;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Scripts.Monster;
using System.Reflection;
using Scripts.Core;
using Scripts.Core.Manager;
using Scripts.Core.SO;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace KingdomIdle.UGUI
{
    /// <summary>Separate-package, local-only gameplay simulation. Never invokes an authentication provider.</summary>
    public sealed class EnvironmentSimulationBootstrap : MonoBehaviour
    {
        public const string Package = "com.isolatedyouth.idlekingdomrpg.environmentqa";
        public static readonly string AccountId = "environment-device-" + Guid.NewGuid().ToString("N");
        public static bool Ready { get; private set; }
        public static bool RequestInFlight { get; private set; }
        public static string StartupError { get; private set; }
        public static void EnsureIsolatedAccount()
        {
            RequirePackage();
            LocalProgression.OpenTestAccount(AccountId);
            RequireIsolation();
        }
        static void RequirePackage()
        {
            if (!Debug.isDebugBuild || Application.identifier != Package || !LocalProgression.IsLocalAuthority)
                throw new InvalidOperationException("Environment simulation requires its separate development package and local authority.");
        }
        public static void RequireIsolation()
        {
            RequirePackage();
            if (LocalProgression.AccountKey != "balance-qa-" + AccountId ||
                !string.IsNullOrEmpty(NetworkManager.Instance?.GetSessionID()) ||
                PlayFab.PlayFabClientAPI.IsClientLoggedIn())
                throw new InvalidOperationException("Environment simulation must use only its unique unauthenticated local profile.");
        }
        public static object Evidence() => new { isolatedPackage = Application.identifier == Package,
            uniqueLocalAccount = LocalProgression.AccountKey == "balance-qa-" + AccountId,
            localAuthority = LocalProgression.IsLocalAuthority, ready = Ready, startupError = StartupError,
            bootstrapInvokesAuthenticationProvider = false, authenticatedCloudSession = PlayFab.PlayFabClientAPI.IsClientLoggedIn(),
            hasNetworkSession = !string.IsNullOrEmpty(NetworkManager.Instance?.GetSessionID()) };
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            RequirePackage();
            Object.DontDestroyOnLoad(new GameObject("EnvironmentSimulationBootstrap", typeof(EnvironmentSimulationBootstrap)));
        }
        async void Start()
        {
            try
            {
                float deadline = Time.realtimeSinceStartup + 90;
                while (LoadManager.Instance == null || Object.FindFirstObjectByType<TitleScreenView>() == null)
                {
                    if (Time.realtimeSinceStartup > deadline) throw new TimeoutException("Simulation bootstrap/title was not ready.");
                    await UniTask.Yield();
                }
                RequireIsolation();
                // The normal title/provider path remains unchanged in every other build.
                LoadManager.Instance.LoadAsyncScene(eSceneType.main);
                while (StageManager.Instance == null || UserManager.Instance?.GetPlayers()?.Count != 3 ||
                       StageManager.Instance.CurrentRunState != eStageRunState.Running)
                {
                    if (Time.realtimeSinceStartup > deadline) throw new TimeoutException("Simulation battle did not become ready.");
                    await UniTask.Yield();
                }
                RequireIsolation(); Ready = true;
                WriteStartup();
            }
            catch (Exception e) { StartupError = e.ToString(); WriteStartup(); Debug.LogException(e); }
        }
        static void WriteStartup() => File.WriteAllText(Path.Combine(Application.persistentDataPath, "environment-simulation-ready.json"),
            JsonConvert.SerializeObject(Evidence(), Formatting.Indented));

        public static async void SelectPreset(string presetId, Action<object> completed, Action<Exception> failed)
        {
            if (RequestInFlight) { failed(new InvalidOperationException("A simulation preset request is already pending.")); return; }
            RequestInFlight = true;
            StageDatabaseSO selection = null;
            float previousScale = Time.timeScale;
            object result = null; Exception failure = null;
            try
            {
                RequireIsolation();
                if (!Ready) throw new InvalidOperationException("Simulation is not ready.");
                var source = Resources.Load<StageDatabaseSO>("StageDatabaseSO");
                var choices = source.EnvironmentPresets.Where(p => p.PresetId == presetId).ToArray();
                if (choices.Length != 1) throw new ArgumentException("Unknown or ambiguous catalog preset.");
                var definition = StageManager.Instance.CurrentDefinition;
                string stagePool = definition.Encounter.EnvironmentPoolId;
                if (choices[0].PoolId.Split('/')[0] != stagePool.Split('/')[0])
                    throw new InvalidOperationException("Enter the preset's actual region/dungeon before selecting it.");
                selection = ScriptableObject.CreateInstance<StageDatabaseSO>();
                selection.SetCatalog("isolated-device-selection", new List<StageEnvironmentPreset> { new StageEnvironmentPreset(stagePool, choices[0].PresetId, choices[0].Weight) },
                    new List<CatalogMonsterInfo>(), 0, default);
                Time.timeScale = 0;
                var background = Object.FindFirstObjectByType<StageBackgroundController>();
                bool adopted = await background.ApplyAsync(definition, selection);
                if (!adopted || background.CurrentPresetId != presetId) throw new InvalidOperationException("Preset request was superseded.");
                RequireIsolation();
                result = new { adopted, preset = presetId, catalogPool = choices[0].PoolId, stagePool, controlledModeSubstitution = choices[0].PoolId != stagePool, selectionMethod = "Real Addressables/controller path with one catalog entry in a temporary in-memory database; no source asset edits" };
            }
            catch (Exception e) { failure = e; }
            finally
            {
                if (selection != null) Object.Destroy(selection);
                Time.timeScale = previousScale; RequestInFlight = false;
            }
            // Report only after the live clock and request guard have been restored.
            if (failure != null) failed(failure);
            else { try { completed(result); } catch (Exception e) { failed(e); } }
        }

        public static object InspectPixels(Renderer[] onlyForeground = null)
        {
            RequireIsolation();
            var background = Object.FindFirstObjectByType<StageBackgroundController>();
            if (!Ready || background == null || Camera.main == null) throw new InvalidOperationException("No live simulation battle.");
            var foreground = CombatMotion.Players.Where(p => p != null && !p.IsDead).SelectMany(p => p.GetComponentsInChildren<SpriteRenderer>()).Cast<Renderer>()
                .Concat(CombatMotion.Monsters.Where(m => m != null && m.MonAction != eMonsterAction.Dead).SelectMany(m => m.GetComponentsInChildren<SpriteRenderer>()))
                .Concat(Object.FindObjectsByType<PooledSpellVfx>(FindObjectsSortMode.None).Where(v => v.isActiveAndEnabled).SelectMany(v => v.GetComponentsInChildren<SpriteRenderer>()))
                .Where(r => r.enabled && r.gameObject.activeInHierarchy).Distinct().ToArray();
            if (onlyForeground != null) foreground = onlyForeground;
            var scenery = background.GetComponentsInChildren<Renderer>().Where(r => r.enabled && r.gameObject.activeInHierarchy).ToArray();
            var all = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            bool[] enabled = all.Select(r => r.enabled).ToArray();
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            bool[] canvasEnabled = canvases.Select(c => c.enabled).ToArray();
            Camera camera = Camera.main;
            Color previousColor = camera.backgroundColor; CameraClearFlags previousFlags = camera.clearFlags;
            float previousScale = Time.timeScale;
            int sampledFrame = Time.frameCount, width = Math.Min(540, Screen.width), height = Mathf.RoundToInt((float)width * Screen.height / Screen.width);
            try
            {
                Time.timeScale = 0;
                var keep = new HashSet<Renderer>(foreground.Concat(scenery));
                foreach (Renderer renderer in all) renderer.enabled &= keep.Contains(renderer);
                foreach (Canvas canvas in canvases) canvas.enabled = false;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                Color32[] combined = ReadFrame(camera, width, height);
                foreach (Renderer renderer in scenery) renderer.enabled = false;
                Color32[] black = ReadFrame(camera, width, height);
                camera.backgroundColor = Color.white;
                Color32[] white = ReadFrame(camera, width, height);
                int opaque = 0, changed = 0;
                for (int i = 0; i < combined.Length; i++)
                {
                    Color32 a = black[i], b = combined[i], w = white[i];
                    if (Math.Abs(a.r - w.r) > 2 || Math.Abs(a.g - w.g) > 2 || Math.Abs(a.b - w.b) > 2) continue;
                    opaque++;
                    if (Math.Abs(a.r - b.r) > 3 || Math.Abs(a.g - b.g) > 3 || Math.Abs(a.b - b.b) > 3) changed++;
                }
                bool sorting = scenery.Length == 3 && foreground.Length >= (onlyForeground == null ? 3 : 1) && scenery.All(b => foreground.All(f => DrawsBefore(b, f)));
                return new { valid = sorting && opaque >= 100 && opaque < width * height * .65f && changed == 0 && sampledFrame == Time.frameCount,
                    sorting, renderWidth = width, renderHeight = height, deviceWidth = Screen.width, deviceHeight = Screen.height,
                    sampledFrame, sameFrame = sampledFrame == Time.frameCount, opaqueForegroundPixels = opaque, backgroundChangedOpaquePixels = changed,
                    bodyAndEffectRenderers = foreground.Length, backgroundRenderers = scenery.Length, background.CurrentPresetId,
                    actorFeet = CombatMotion.Players.Where(p => p != null && !p.IsDead).Select(p => new { kind = "player", x = p.VfxFootPosition.x, y = p.VfxFootPosition.y, radius = CombatMotion.PlayerRadius })
                        .Concat(CombatMotion.Monsters.Where(m => m != null && m.MonAction != eMonsterAction.Dead).Select(m => new { kind = "enemy", x = m.FootPosition.x, y = m.FootPosition.y, radius = m.BodyRadius })).ToArray(),
                    method = "Actual phone GPU, same frozen live frame. Foreground opaque mask from RGB-invariant black/white matte renders; compare full backdrop at RGB tolerance 3. Render resolution capped to 540-wide at the actual phone aspect; no display override." };
            }
            finally
            {
                for (int i = 0; i < all.Length; i++) if (all[i] != null) all[i].enabled = enabled[i];
                for (int i = 0; i < canvases.Length; i++) if (canvases[i] != null) canvases[i].enabled = canvasEnabled[i];
                camera.backgroundColor = previousColor; camera.clearFlags = previousFlags; Time.timeScale = previousScale;
            }
        }
        static Monster flashTarget;
        static SpriteRenderer[] flashBodies;
        static Material[] flashMaterials;
        static long flashHpBefore;
        static long CurrentHp(Monster target)
        {
            var stat = (Monster.MonsterStat)typeof(Monster).GetField("_stat", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
            return stat._hp + stat._extraHp;
        }
        static float FlashAmount(SpriteRenderer renderer)
        {
            var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block);
            return block.GetFloat(Shader.PropertyToID("_FlashAmount"));
        }
        public static object HitFlash(string phase)
        {
            RequireIsolation(); Time.timeScale = 0;
            if (phase == "baseline")
            {
                flashTarget = CombatMotion.Monsters.Where(m => m != null && m.MonAction != eMonsterAction.Dead && CurrentHp(m) > 1 &&
                    m.GetComponent<MonsterHitFlash>() != null && m.GetComponentsInChildren<SpriteRenderer>().Any(r => r.enabled && r.sprite != null &&
                        r.sharedMaterial != null && r.sharedMaterial.shader.name == "Shader Graphs/MonsterHitEffect"))
                    .OrderByDescending(CurrentHp).FirstOrDefault();
                if (flashTarget == null) throw new InvalidOperationException("No live hit-flash monster is available.");
                flashBodies = flashTarget.GetComponentsInChildren<SpriteRenderer>().Where(r => r.enabled && r.sprite != null &&
                    r.sharedMaterial != null && r.sharedMaterial.shader.name == "Shader Graphs/MonsterHitEffect").ToArray();
                flashMaterials = flashBodies.Select(r => r.sharedMaterial).ToArray();
                flashHpBefore = CurrentHp(flashTarget);
                flashTarget.GetComponent<MonsterHitFlash>().ResetFlash();
            }
            else if (phase != "active" && phase != "restored") throw new ArgumentException("Unknown hit-flash phase.");
            if (flashTarget == null || flashTarget.MonAction == eMonsterAction.Dead) throw new InvalidOperationException("The controlled hit-flash target is no longer alive.");
            if (phase == "active")
            {
                var owner = CombatMotion.Players.First(p => p != null && !p.IsDead);
                bool survived = flashTarget.TakeDamage(new ActiveSkill.DamageProxy(1, owner));
                if (!survived || CurrentHp(flashTarget) != flashHpBefore - 1)
                    throw new InvalidOperationException("Real TakeDamage did not apply exactly 1 HP to the controlled enemy.");
            }
            float[] amounts = flashBodies.Select(FlashAmount).ToArray();
            bool sameMaterials = flashBodies.Select((r, i) => r.sharedMaterial == flashMaterials[i]).All(v => v);
            bool expectedAmount = phase == "active" ? amounts.All(a => a > .4f) : amounts.All(a => a <= .0001f);
            var pixels = JObject.FromObject(InspectPixels());
            var targetPixels = JObject.FromObject(InspectPixels(flashBodies));
            return new { valid = expectedAmount && sameMaterials && (bool)pixels["valid"] && (bool)targetPixels["valid"],
                phase, target = flashTarget.Type.ToString(), hpBefore = flashHpBefore, hpNow = CurrentHp(flashTarget),
                flashAmounts = amounts, sameActualMaterials = sameMaterials, actualShaders = flashBodies.Select(r => r.sharedMaterial.shader.name).ToArray(),
                pixels, targetPixels, method = "Real spawned enemy TakeDamage(1, live player); no material replacement. Initial baseline reset only; restored phase follows the real coroutine in normal time." };
        }

        static bool DrawsBefore(Renderer back, Renderer front)
        {
            var bg = back.GetComponentsInParent<SortingGroup>().LastOrDefault(g => g.enabled);
            var fg = front.GetComponentsInParent<SortingGroup>().LastOrDefault(g => g.enabled);
            int b = SortingLayer.GetLayerValueFromID(bg != null ? bg.sortingLayerID : back.sortingLayerID);
            int f = SortingLayer.GetLayerValueFromID(fg != null ? fg.sortingLayerID : front.sortingLayerID);
            int bo = bg != null ? bg.sortingOrder : back.sortingOrder, fo = fg != null ? fg.sortingOrder : front.sortingOrder;
            return (b < f || b == f && bo < fo) && back.sharedMaterials.Where(m => m != null).All(m =>
                (!m.HasProperty("_ZWrite") || m.GetFloat("_ZWrite") == 0) && front.sharedMaterials.Where(n => n != null).All(n => m.renderQueue <= n.renderQueue));
        }
        static Color32[] ReadFrame(Camera camera, int width, int height)
        {
            RenderTexture active = RenderTexture.active, previous = camera.targetTexture;
            float aspect = camera.aspect;
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32); Texture2D pixels = null;
            try
            {
                target.Create(); camera.targetTexture = target; camera.aspect = (float)width / height;
                camera.Render(); RenderTexture.active = target;
                pixels = new Texture2D(width, height, TextureFormat.RGBA32, false);
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply();
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
#endif
