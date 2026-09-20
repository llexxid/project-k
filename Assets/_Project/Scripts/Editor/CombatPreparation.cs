using System;
using System.IO;
using System.Linq;
using KingdomIdle.Combat;
using Scripts.Core;
using Scripts.Monster;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace KingdomIdle.EditorTools
{
    public static class CombatPreparation
    {
        public static void BuildDevice()
        {
            var importMode=EditorSettings.refreshImportMode;
            int workers=EditorUserSettings.desiredImportWorkerCount;
            try
            {
            EditorSettings.refreshImportMode=AssetDatabase.RefreshImportMode.InProcess;
            EditorUserSettings.desiredImportWorkerCount=0;AssetDatabase.ForceToDesiredWorkerCount();
            AssetDatabase.ReleaseCachedFileHandles();
            Prepare();
            Scripts.Core.Parser.StageDataGenerator.Generate();
            KingdomIdle.UGUI.Editor.MagePolishPreparation.Prepare();
            KingdomIdle.UGUI.Editor.CombatUiPreparation.Build();
            KingdomIdle.UGUI.Editor.MagePolishPreparation.BakeAuthoredGlyphs();
            KingdomIdle.UGUI.Editor.TitleLobbyDeviceBuild.Build();
            }
            finally { EditorSettings.refreshImportMode=importMode;EditorUserSettings.desiredImportWorkerCount=workers; }
        }
        [MenuItem("KingdomIdle/Combat/Prepare Combat Assets")]
        public static void Prepare()
        {
            Job("Spearman", 1.12f, 1.2f, 1.2f);
            Job("Knight", .94f, 1.65f, 1.5f);
            Job("Elite_Knight", 1.04f, 1.75f, 1.6f);
            Job("Mage", 3.2f, 1.6f, 1.15f);
            Job("Elite_Mage", 3.3f, 1.65f, 1.2f);
            PrepareBomb();
            PrepareAimedSpear();
            PrepareShamanTotem("Goblins", "GoblinShaman", "Goblin Shaman Sprite Sheet");
            PrepareShamanTotem("Orcs", "OrcShaman", "Orc Shaman Sprite Sheet");
            PrepareAttackClips();
            PrepareAudio();
            foreach (string path in Directory.GetFiles("Assets/_Project/Prefabs/Monster", "*.prefab", SearchOption.AllDirectories))
                PrepareMonster(path.Replace('\\','/'));
            var lancer = PrefabUtility.LoadPrefabContents("Assets/_Project/Prefabs/RoyalGuard/Lancer.prefab");
            try
            {
                var circle = lancer.GetComponent<CircleCollider2D>();
                if (circle != null) { circle.radius = .26f; circle.offset = new Vector2(0, -.36f); circle.isTrigger = true; }
                PrefabUtility.SaveAsPrefabAsset(lancer,"Assets/_Project/Prefabs/RoyalGuard/Lancer.prefab");
            }
            finally { PrefabUtility.UnloadPrefabContents(lancer); }
            AssetDatabase.SaveAssets();
            Debug.Log("[CombatPreparation] Jobs, footprints, attack cues and projectile references prepared.");
        }

        private static void Job(string name, float range, float interval, float multiplier)
        {
            var data = AssetDatabase.LoadAssetAtPath<JobData>($"Assets/_Project/Scripts/Player/Job/SO/{name}.asset");
            data.movSpeed = 2;
            data.basicAttack.range = range; data.basicAttack.cooldown = interval; data.basicAttack.damageMultiplier = multiplier;
            data.basicAttack.halfWidth = range * .5f; data.basicAttack.halfHeight = .28f;
            EditorUtility.SetDirty(data);
        }

        private static void PrepareAttackClips()
        {
            var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/_Project/Art/Animations/RoyalGuard/EliteKnight/Attack_Anim.anim");
            var binding=AnimationUtility.GetObjectReferenceCurveBindings(clip).First(b=>b.propertyName=="m_Sprite");
            var original=AnimationUtility.GetObjectReferenceCurve(clip,binding);
            // The source is a multi-hit combo. Keep its first deliberate slash and recovery only.
            // Asset GUID remains stable; reruns reuse the already trimmed sequence.
            if(original.Length>10)
            {
                float[] times={0,.11f,.22f,.34f,.48f,.57f,.63f,.69f,.79f,.94f};
                var frames=Enumerable.Range(0,10).Select(i=>new ObjectReferenceKeyframe { time=times[i],value=original[i==9?original.Length-1:i].value }).ToArray();
                AnimationUtility.SetObjectReferenceCurve(clip,binding,frames);
                var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.stopTime=1;settings.loopTime=false;AnimationUtility.SetAnimationClipSettings(clip,settings);
            }
            AnimationUtility.SetAnimationEvents(clip,new[]{new AnimationEvent{functionName="OnAttackHit",time=.63f}});
            EditorUtility.SetDirty(clip);
        }

        private static void PrepareAudio()
        {
            const string folder="Assets/_Project/Audio/Combat";
            AssetDatabase.Refresh();
            foreach(var path in Directory.GetFiles(folder,"*.wav"))
            {
                var importer=(AudioImporter)AssetImporter.GetAtPath(path.Replace('\\','/'));
                importer.forceToMono=true;importer.loadInBackground=false;
                var settings=importer.defaultSampleSettings;settings.preloadAudioData=true;settings.loadType=AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat=AudioCompressionFormat.PCM;settings.sampleRateSetting=AudioSampleRateSetting.OverrideSampleRate;settings.sampleRateOverride=22050;
                importer.defaultSampleSettings=settings;importer.SaveAndReimport();
            }
            const string palettePath="Assets/_Project/Resources/CombatAudioPalette.asset";
            var palette=AssetDatabase.LoadAssetAtPath<CombatAudioPalette>(palettePath);
            if(palette==null) { Directory.CreateDirectory("Assets/_Project/Resources");AssetDatabase.Refresh();palette=ScriptableObject.CreateInstance<CombatAudioPalette>();AssetDatabase.CreateAsset(palette,palettePath); }
            AudioClip Load(string name)=>AssetDatabase.LoadAssetAtPath<AudioClip>(folder+"/"+name+".wav");
            palette.steel=Load("Steel");palette.heavy=Load("Heavy");palette.thrust=Load("Thrust");palette.throwing=Load("Throw");palette.magic=Load("Magic");palette.whip=Load("Whip");EditorUtility.SetDirty(palette);
        }

        private static void PrepareBomb()
        {
            const string texture = "Assets/_Project/Art/VFX/Projectiles/GoblinBomb.png";
            AssetDatabase.ImportAsset(texture,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(texture);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.spritePixelsPerUnit=26;importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;importer.isReadable=false;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.npotScale=TextureImporterNPOTScale.None;importer.SaveAndReimport();
            // Keep the new working projectile on its owning monster's Point / ASTC 4x4 atlas.
            const string atlasPath="Assets/_Project/Art/Atlases/Prepared/Goblins/Atlas_GoblinBomber.spriteatlasv2";
            var atlas=new UnityEditor.U2D.SpriteAtlasAsset();
            atlas.Add(new UnityEngine.Object[] {
                AssetDatabase.LoadMainAssetAtPath("Assets/_Project/Art/Sprites/Goblins/GoblinBomber/Goblin Bomber Sprite Sheet.png"),
                AssetDatabase.LoadMainAssetAtPath(texture)
            });
            UnityEditor.U2D.SpriteAtlasAsset.Save(atlas,atlasPath);
            AssetDatabase.ImportAsset(atlasPath,ImportAssetOptions.ForceSynchronousImport);
            const string folder="Assets/_Project/Prefabs/VFX/Prepared/Goblins/GoblinBomber/Projectiles";
            Directory.CreateDirectory(folder);AssetDatabase.Refresh();
            var root=new GameObject("GoblinBomb_Flight");
            try { var renderer=root.AddComponent<SpriteRenderer>();renderer.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(texture);renderer.sortingLayerName="CombatVFX";renderer.sortingOrder=8;PrefabUtility.SaveAsPrefabAsset(root,folder+"/GoblinBomb_Flight.prefab"); }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void PrepareAimedSpear()
        {
            const string folder="Assets/_Project/Prefabs/VFX/Prepared/Orcs/OrcHunter/Projectiles";
            // The source sheet already pitches the spear from +45 to -45 degrees.
            // Runtime trajectory rotation needs its horizontal frame, not a second pitch animation.
            var sprite=AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/Sprites/Orcs/OrcHunter/Spear.png")
                .OfType<Sprite>().First(s=>s.name=="Flight_002");
            var root=new GameObject("Spear_AimedFlight");
            try {
                var renderer=root.AddComponent<SpriteRenderer>();renderer.sprite=sprite;
                renderer.sortingLayerName="CombatVFX";renderer.sortingOrder=8;
                PrefabUtility.SaveAsPrefabAsset(root,folder+"/Spear_AimedFlight.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void PrepareMonster(string path)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var mon = root.GetComponent<Monster>(); if (mon == null) return;
                var animator = root.GetComponentInChildren<Animator>(); if (animator == null) return;
                if (animator.GetComponent<MonsterAttackCue>() == null) animator.gameObject.AddComponent<MonsterAttackCue>();
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                string name = root.name;
                // Absolute authored scale: repeated preparation must not keep shrinking them.
                if (path.Contains("/Mimics/")) root.transform.localScale = Vector3.one * .9f;
                string projectile = name switch
                {
                    "OrcHunter" => "Orcs/OrcHunter/Projectiles/Spear_AimedFlight",
                    "MON_BANDIT_ARCHER" => "RoyalGuard/Projectiles/Projectiles/Arrow_Idle",
                    "GoblinGunner" => "Goblins/GoblinGunner/Projectiles/projectile_Idle",
                    "GoblinKing" => "Goblins/GoblinKing/Projectiles/CoinBagSpriteSheet_Flight",
                    "GoblinBomber" => "Goblins/GoblinBomber/Projectiles/GoblinBomb_Flight",
                    "OrcKid" => "Orcs/OrcKid/Projectiles/Meat_Idle",
                    "OrcWarlock" => "Bandit/BanditMage/Projectiles/BanditMageSpriteSheet_ProjectileFlight",
                    _ => null
                };
                float reach = name.Contains("Taskmaster") ? 1.35f : name.Contains("Brute") ? 1.18f : name.Contains("King") || name == "BANDIT_KING" ? 1.15f : .83f;
                bool shaman = name == "GoblinShaman" || name == "OrcShaman";
                if (projectile != null || shaman) reach = 3.1f;
                var serialized = new SerializedObject(mon);
                serialized.FindProperty("_attackRadius").floatValue = reach;
                serialized.FindProperty("_attackSound").enumValueIndex=(int)(name.Contains("Taskmaster")?CombatSoundCue.Whip:
                    name.Contains("Shaman") || name.Contains("Warlock")?CombatSoundCue.Magic:projectile!=null?CombatSoundCue.Throw:
                    name.Contains("Brute") || name.Contains("King")?CombatSoundCue.Heavy:name.Contains("Spear")?CombatSoundCue.Thrust:CombatSoundCue.Steel);
                serialized.FindProperty("_projectilePrefab").objectReferenceValue = projectile != null ?
                    AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/VFX/Prepared/" + projectile + ".prefab") : null;
                serialized.FindProperty("_totemPrefab").objectReferenceValue = shaman ?
                    AssetDatabase.LoadAssetAtPath<ShamanTotemStrike>($"Assets/_Project/Prefabs/VFX/Prepared/{(name == "GoblinShaman" ? "Goblins" : "Orcs")}/{name}/Totems/TotemStrike.prefab") : null;
                float impact = name == "OrcHunter" ? 4f / 7f : name == "GoblinBomber" ? .4f : name == "GoblinShaman" ? 3f / 6f : name == "OrcShaman" ? 2f / 5f : .5f;
                serialized.FindProperty("_impactNormalized").floatValue = impact;
                serialized.FindProperty("_projectileSpeed").floatValue = name == "OrcHunter" ? 5f : 4.3f;
                serialized.FindProperty("_projectileArc").floatValue = name == "GoblinKing" || name == "GoblinBomber" || name == "OrcKid" ? .4f : name == "OrcHunter" ? .08f : 0;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                var controller = animator.runtimeAnimatorController as AnimatorController;
                if (controller != null)
                {
                    var state = controller.layers[0].stateMachine.states.Select(s=>s.state).FirstOrDefault(s=>s.name.Contains("Attack"));
                    if (state != null)
                    {
                        state.name = "Attack";
                        if (name == "OrcHunter")
                            state.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/_Project/Art/Animations/Orcs/OrcHunter/Prepared/OrcHunterSpriteSheet/Attack2.anim");
                        if (name == "GoblinBomber")
                            state.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/_Project/Art/Animations/Goblins/GoblinBomber/Prepared/GoblinBomberSpriteSheet/Attack2.anim");
                        if (state.motion is AnimationClip clip && AssetDatabase.GetAssetPath(clip).StartsWith("Assets/_Project/", StringComparison.Ordinal))
                        {
                            var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = false;
                            AnimationUtility.SetAnimationClipSettings(clip,settings);
                            var cues = AnimationUtility.GetAnimationEvents(clip).Where(c=>c.functionName != "CombatImpact").ToList();
                            cues.Add(new AnimationEvent { functionName="CombatImpact", time=clip.length*impact });
                            AnimationUtility.SetAnimationEvents(clip,cues.ToArray());
                            EditorUtility.SetDirty(clip);
                            var soRef = serialized.FindProperty("_AnimationClipSO").objectReferenceValue;
                            if (soRef != null)
                            {
                                var animations = new SerializedObject(soRef); var entries = animations.FindProperty("_datas");
                                for(int i=0;i<entries.arraySize;i++)
                                {
                                    var entry=entries.GetArrayElementAtIndex(i);
                                    if(entry.FindPropertyRelative("actionType").intValue==(int)eMonsterAction.Attack)
                                        entry.FindPropertyRelative("clip").objectReferenceValue=clip;
                                }
                                animations.ApplyModifiedPropertiesWithoutUndo();
                            }
                        }
                        EditorUtility.SetDirty(controller);
                    }
                }
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void PrepareShamanTotem(string race, string name, string sheet)
        {
            var sprites = AssetDatabase.LoadAllAssetsAtPath($"Assets/_Project/Art/Sprites/{race}/{name}/{sheet}.png").OfType<Sprite>().ToArray();
            var root = new GameObject("TotemStrike", typeof(SpriteRenderer), typeof(ShamanTotemStrike));
            try
            {
                var renderer = root.GetComponent<SpriteRenderer>();
                renderer.sortingLayerName = "CombatVFX"; renderer.sortingOrder = CombatVfxOrder.Impact;
                var serialized = new SerializedObject(root.GetComponent<ShamanTotemStrike>());
                foreach (var pair in new[] { ("emerge", "TotemAppear"), ("idle", "TotemIdle"), ("crumble", "TotemDead") })
                {
                    var frames = sprites.Where(s => s.name.StartsWith(pair.Item2 + "_", StringComparison.Ordinal)).OrderBy(s => s.name).ToArray();
                    if (frames.Length == 0) throw new InvalidOperationException("Missing authored totem frames: " + name + " / " + pair.Item2);
                    var property = serialized.FindProperty(pair.Item1); property.arraySize = frames.Length;
                    for (int i = 0; i < frames.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
                    if (pair.Item1 == "emerge") renderer.sprite = frames[0];
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, $"Assets/_Project/Prefabs/VFX/Prepared/{race}/{name}/Totems/TotemStrike.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
