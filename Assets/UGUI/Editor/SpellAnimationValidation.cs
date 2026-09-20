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
using Scripts.Monster;
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
        static string Output => Environment.GetEnvironmentVariable("SPELL_ANIMATION_OUTPUT") ?? "Recordings/BloomRevision/EditorPlay";
        static bool MeteorOnly => Environment.GetEnvironmentVariable("SPELL_ANIMATION_SKILL") == "8";
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
            if (!MeteorOnly) Prepare();
            Directory.CreateDirectory(Output);
            SessionState.SetInt(Active + ".Shake", PlayerPrefs.GetInt("settings_screenShake", 1));
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
                routine = Exercise(); deadline = EditorApplication.timeSinceStartup + 420;
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
            PlayerPrefs.SetInt("settings_screenShake", SessionState.GetInt(Active + ".Shake", 1)); GamePresentationSettings.Apply();
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
            File.WriteAllText(Output+"/acceptance.json", JsonConvert.SerializeObject(MageSkillAcceptance.Run(), Formatting.Indented));
            LocalProgression.Execute("spell-health", s=> { s.HealthLevel=136; s.AttackLevel=0; return true; });
            foreach (var skill in mage.GetAllSkills())
            {
                LocalProgression.Execute("all-blooms",s=>{s.MageSkills[skill.id]=new MageSave{Awaken=10};return true;});
                float cd=mage.GetEffectiveCooldown(skill.id);
                if(!mage.SetBloomEnabled(skill.id,true) || !mage.IsBloomEnabled(skill.id) || skill.DisplayIcon(true)==skill.DisplayIcon(false) ||
                    Mathf.Abs(mage.GetEffectiveCooldown(skill.id)-cd*skill.bloomCooldownMultiplier)>.001f) throw new Exception("Bloom toggle mismatch "+skill.id);
                if(!mage.SetBloomEnabled(skill.id,false) || mage.IsBloomEnabled(skill.id))throw new Exception("Bloom off mismatch "+skill.id);
            }
            foreach (var variant in new[] { (0,0,false,true), (0,8,false,true), (0,10,true,true), (1,10,true,true),
                (3,0,false,true), (3,10,true,true), (3,10,true,false), (0,0,false,false), (0,10,true,false),
                (2,10,true,true), (4,10,true,true), (5,10,true,true), (7,10,true,true), (9,10,true,true) })
            {
                if (MeteorOnly && (variant.Item1 != 3 || !variant.Item3)) continue;
                int id = variant.Item1, awaken = variant.Item2; bool bloom = variant.Item3;
                bool shakeEnabled = variant.Item4, meteor = id==3 && bloom;
                PlayerPrefs.SetInt("settings_screenShake",shakeEnabled?1:0);GamePresentationSettings.Apply();
                for (int i = 0; i < 5; i++) mage.Unequip(i);
                LocalProgression.Execute("spell-case", s => { s.MageSkills[id] = new MageSave { Enhance = 0, Awaken = awaken, BloomEnabled = bloom }; return true; });
                mage.NotifyCommitted(); mage.Equip(0, id);
                stage.BeginStage((eStage)0x20003000A);
                float ready = Time.time + 2;
                while (Time.time < ready || stage.CurrentRunState != eStageRunState.Running || mage.IsOnCooldown(0)) yield return null;
                if(id==7){var p=UserManager.Instance.GetPlayers()[0];p.TakeDamage(new ActiveSkill.DamageProxy((ulong)(p.playerStatus.MaxHP/2),p));}
                if(id==3 && !bloom && mage.CastSkillAt(0,MageTowerTargeting.BattleCenter()))throw new Exception("Random starfall consumed drag");
                MageSkillDiagnostics.Events.Clear();
                if (!mage.CastSkill(0)) throw new Exception("Cast rejected " + variant);
                float start = Time.time; bool captured = false;
                var frames = new HashSet<string>();
                float firstDamage = -1;
                float peakOffset=0,peakSlow=0,afterLeavingSlow=-1;
                var actors=CombatMotion.Monsters.Where(m=>m!=null&&m.MonAction!=eMonsterAction.Dead).Take(3).ToArray();
                var names=actors.Select(m=>m.name).ToArray();
                var begin=MageSkillDiagnostics.Events.First(e=>e.kind=="begin");
                var center=new Vector3(begin.x,begin.y,0);
                if(meteor && actors.Length>0) MonsterCCState.Apply(actors[0],CrowdControlKind.Slow,4,.25f,slowStyle:SlowVisualKind.Venom);
                if(meteor && shakeEnabled)
                {
                    float timer=mage.GetCooldownRemaining(0);
                    mage.SetBloomEnabled(id,false);
                    if(Mathf.Abs(timer-mage.GetCooldownRemaining(0))>.001f || !mage.IsCasting(0))throw new Exception("Mode switch reset active cast/cooldown");
                }
                while (mage.IsCasting(0))
                {
                    if(id==3 && !bloom && Time.time-start<.5f)
                    {
                        var launch=MageSkillDiagnostics.Events.FirstOrDefault(e=>e.kind=="star-launch");
                        if(launch!=null)for(int i=0;i<actors.Length;i++)
                        {actors[i].name="StarfallProbe"+i; PlaceDurable(actors[i],new Vector3(launch.x+(i==2?1.2f:(i==0?-.18f:.18f)),launch.y,0));}
                    }
                    if(meteor && actors.Length>0)
                    {
                        PlaceDurable(actors[0],center+Vector3.right*(Time.time-start<2.6f?0:2.5f));
                        float slow=actors[0].GetComponent<MonsterCCState>()?.SlowFraction??0;
                        if(Time.time-start<2.6f)peakSlow=Mathf.Max(peakSlow,slow);
                        if(Time.time-start>2.85f && Time.time-start<3.2f)afterLeavingSlow=slow;
                    }
                    var shaker=Camera.main?.GetComponent<CameraShaker>();
                    if(shaker!=null)peakOffset=Mathf.Max(peakOffset,shaker.DiagnosticOffset.magnitude);
                    foreach (var effect in Object.FindObjectsByType<PooledSpellVfx>(FindObjectsSortMode.None))
                        if (effect.name.StartsWith(meteor ? "Meteor(" : id == 3 ? "ArcaneVolley(" : id == 2 ? "FireTornado(" : "Lightning("))
                            foreach (var renderer in effect.GetComponentsInChildren<SpriteRenderer>())
                                if (renderer.sprite != null) frames.Add(renderer.sprite.name);
                    var damage = MageSkillDiagnostics.Events.FirstOrDefault(e => e.skill == id && e.kind == (id==7?"heal":"damage"));
                    if (damage != null && firstDamage < 0) firstDamage = damage.time - start;
                    if (!captured && Time.time-start > (meteor ? 1.2f : bloom ? 2.05f : .8f))
                    { Capture($"spell-{id}-a{awaken}-b{bloom}-{checks.Count}"); captured = true; }
                    yield return null;
                }
                for(int i=0;i<actors.Length;i++)if(actors[i]!=null){actors[i].name=names[i];actors[i].RestartBehaviourTree();}
                var bolts = MageSkillDiagnostics.Events.Where(e => e.kind == "bolt").ToArray();
                int shakes=MageSkillDiagnostics.Events.Count(e=>e.kind=="shake");
                checks.Add(new { id, awaken, bloom, shakeEnabled, shakes, peakOffset,peakSlow,afterLeavingSlow, bolts=bolts.Length, boltTimes=bolts.Select(b=>b.time-start).ToArray(), distinctFrames=frames.Count, firstDamageSeconds=firstDamage, duration=Time.time-start, events=MageSkillDiagnostics.Events.ToArray() });
                if (id==0 && !bloom)
                {
                    if (bolts.Length != 3 + awaken/4) throw new Exception("Wrong restored strike count");
                    for (int i=1;i<bolts.Length;i++) if (Mathf.Abs(bolts[i].time-bolts[i-1].time-2f/12f)>.08f) throw new Exception("Original lightning cadence changed");
                    if (frames.Any(f=>!f.StartsWith("LightningOriginal_"))) throw new Exception("Unexpected non-bloom lightning art");
                }
                if (id==0 && bloom && bolts.Length!=0) throw new Exception("Normal chain leaked into bloom");
                if((id==0 || meteor) && shakes!=(shakeEnabled?(id==0&&!bloom?bolts.Length:1):0))throw new Exception("Impact shake count/settings failed");
                if((id==0 || meteor) && (shakeEnabled?peakOffset<=0:peakOffset>.0001f))throw new Exception("Camera did not obey shake setting");
                if(meteor)
                {
                    if(firstDamage<1.5f || firstDamage>1.85f || frames.Count<40)throw new Exception("Meteor impact timing/animation failed");
                    var contact=MageSkillDiagnostics.Events.First(e=>e.kind=="meteor-contact");var end=MageSkillDiagnostics.Events.First(e=>e.kind=="meteor-ground-end");
                    if(Mathf.Abs(end.time-contact.time-3.5f)>.05f || Mathf.Abs(peakSlow-.6f)>.001f || Mathf.Abs(afterLeavingSlow-.25f)>.001f)throw new Exception("Ground lifetime or enter/leave slow failed");
                    Capture("meteor-ground-complete-"+checks.Count);
                }
                if(id==3 && !bloom)
                {
                    var impacts=MageSkillDiagnostics.Events.Where(e=>e.kind=="star-impact").ToArray();
                    var launches=MageSkillDiagnostics.Events.Where(e=>e.kind=="star-launch").ToArray();
                    if(impacts.Length!=MageSkillRules.HitCount(mage.GetSkillById(id),awaken) || launches.Length!=impacts.Length)throw new Exception("Random stars lost impacts");
                    var hits=MageSkillDiagnostics.Events.Where(e=>e.kind=="damage" && Mathf.Abs(e.time-impacts[0].time)<.001f).Select(e=>e.target).ToArray();
                    if(!hits.Contains("StarfallProbe0")||!hits.Contains("StarfallProbe1")||hits.Contains("StarfallProbe2"))throw new Exception("Localized area hit bounds failed");
                    if(launches.Any(e=>!impacts.Any(p=>p.x==e.x&&p.y==e.y)))throw new Exception("Stars tracked targets after launch");
                }
                if (firstDamage<0) throw new Exception("No actual spell damage " + variant);
            }
        }
        static void PlaceDurable(Monster monster,Vector3 point)
        {
            if(monster==null || monster.MonAction==eMonsterAction.Dead)return;
            monster.InterruptBehaviourTree();
            monster.transform.position+=point-monster.FootPosition;
            var field=typeof(Monster).GetField("_stat",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
            var stat=(Monster.MonsterStat)field.GetValue(monster);stat._hp=stat._maxHp=100000000;field.SetValue(monster,stat);
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
