using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using KingdomIdle.Combat;
using KingdomIdle.Gacha;
using KingdomIdle.MageTower;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Rebuilds the approved mage catalog into existing SOs and reusable, visual-only prefabs.</summary>
public static class MageSkillAssetPreparation
{
    const string Art = "Assets/_Project/Art";
    const string Prefabs = "Assets/_Project/Prefabs/VFX/MageTower";
    const string AnimationRoot = Art + "/Animations/VFX/MageTower";
    const string Source = Art + "/VFX/PixelArtRPGVFX/Textures/";
    const string Generated = "AI/comfyui/mage-skills/20260915";
    const string Polished = Art + "/VFX/MageTower/";
    const string Refined = RefinedMageSpritePreparation.Folder;

    readonly struct Layer
    {
        public readonly string Texture;
        public readonly Vector2 Size, Offset;
        public readonly Color Color;
        public readonly int StaticFrame;
        public readonly bool Loop;
        public readonly float Fps;
        public Layer(string texture, float width, float height, Color color, float x = 0, float y = 0, int frame = -1, bool loop = false, float fps = 12)
        { Texture = texture; Size = new Vector2(width, height); Offset = new Vector2(x, y); Color = color; StaticFrame = frame; Loop = loop; Fps = fps; }
    }

    [MenuItem("KingdomIdle/MageTower/Rebuild Approved Skill Assets")]
    public static void Build()
    {
        EnsureFolder(Prefabs); EnsureFolder(AnimationRoot); EnsureFolder(Art + "/Icons/MageTower");
        PrepareDerivedSheets();
        RefinedMageSpritePreparation.Prepare();
        const string cloudTexture = Art + "/VFX/MageTower/LightningCloud.png";
        AssetDatabase.ImportAsset(cloudTexture, ImportAssetOptions.ForceSynchronousImport);
        var cloudImporter = (TextureImporter)AssetImporter.GetAtPath(cloudTexture);
        cloudImporter.textureType = TextureImporterType.Sprite;
        cloudImporter.spriteImportMode = SpriteImportMode.Single;
        cloudImporter.spritePixelsPerUnit = 32;
        cloudImporter.filterMode = FilterMode.Point;
        cloudImporter.mipmapEnabled = false;
        cloudImporter.isReadable = false;
        cloudImporter.npotScale = TextureImporterNPOTScale.None;
        cloudImporter.textureCompression = TextureImporterCompression.Uncompressed;
        cloudImporter.maxTextureSize = 64;
        cloudImporter.SaveAndReimport();
        Color white = new Color(.9f, .9f, .9f), violet = new Color(.85f, .5f, .95f), frost = new Color(.8f, .92f, .94f);
        Color fire = new Color(.9f, .8f, .7f), sage = new Color(.7f, .8f, .65f);
        // Preserve the user's first ThunderEffects art, proportions and 12fps poses.
        var lightning = Vfx("Lightning",
            new Layer(Refined+"LightningOriginal.png",70f/64f*1.5f,149f/64f*1.5f,Color.white));
        var ice = Vfx("IceSpike", new Layer(Source+"Ice/IceSpike.png", 1.8f, 1.8f, frost, y:.6f));
        var tornado = Vfx("FireTornado", new Layer(Refined+"FireVortexCrown.png", 2.1f, 3.25f, Color.white, loop:true, fps:24));
        var arcane = Vfx("ArcaneVolley", new Layer(Refined+"StarfallWarm.png", 2.1f, 2.1f, Color.white, loop:true));
        var arcaneHit = Vfx("StarfallPulse", new Layer(Refined+"StarfallPulse.png", 1.1f, .715f, Color.white, fps:20));
        var venom = Vfx("VenomMist",
            new Layer(Art+"/VFX/PoisonEffect/Animation/Sprites/Poison_Effect_05-1.png",3.3f,3f,new Color(1,1,1,.96f),frame:0),
            new Layer(Art+"/VFX/PoisonEffect/Animation/Sprites/Poison_Effect_05-2.png",3f,3f,new Color(1,1,1,.88f),y:.2f,loop:true));
        var stone = Vfx("StoneSeal",
            new Layer(Source+"Earth/EarthRock.png",1.8f,1.8f,new Color(.84f,.79f,.7f),x:-.42f,y:.34f),
            new Layer(Source+"Earth/EarthRock.png",2.3f,2.3f,new Color(.91f,.86f,.77f),y:.48f),
            new Layer(Source+"Earth/EarthRock.png",1.6f,1.6f,new Color(.84f,.79f,.7f),x:.44f,y:.28f));
        var sanctuary = BuildSanctuary();
        var heal = Vfx("SanctuaryHeal",new Layer(Refined+"HealingFeet.png",1.5f,1f,Color.white));
        var meteor = Vfx("Meteor",new Layer(Refined+"MeteorFlight.png",6f,6.5f,Color.white,fps:32));
        EditMeteor(meteor);
        var crater = Vfx("MeteorCrater",
            new Layer(Refined+"MeteorGround.png",2.6f,1.18f,Color.white,frame:0),
            new Layer(Refined+"MeteorImpact.png",3.5f,2f,Color.white,fps:18),
            new Layer(Refined+"MeteorEmbers.png",2.6f,1.18f,Color.white,loop:true,fps:16));
        var rift = Vfx("VoidRift",new Layer(Refined+"VoidRing.png",4.6f,4.6f,Color.white,loop:true));
        var collapse = Vfx("VoidCollapse",new Layer(Polished+"VoidCollapseMuted.png",2.4f,2.4f,new Color(1,1,1,.86f)));
        var telegraph = Vfx("GroundTelegraph",new Layer(Refined+"StoneGlyph.png",2.5f,1.5f,Color.white,frame:0));
        var cloud = Vfx("LightningBloomCloud",
            new Layer(Refined+"StormCloud.png",3.5f,2f,Color.white,frame:0),
            new Layer(Polished+"CloudSparksViolet.png",2f,2f,Color.white,loop:true));
        var thunder = Vfx("LightningBloomStrike",
            new Layer(Refined+"ThunderBolt.png",4f,4.6f,Color.white,y:1.65f),
            new Layer(Refined+"ThunderImpact.png",4f,4f,Color.white));
        var glacier = Vfx("IceBloomCrystal",new Layer(Refined+"Glacier.png",3.5f,4f,Color.white,fps:14));
        var iceCast = Vfx("IceBloomWarning",new Layer(Source+"Ice/IceSlam.png",2.8f,1.8f,new Color(.58f,.73f,.83f)));

        string[] keys={"Lightning","IceSpike","FireTornado","ArcaneVolley","VenomMist","StoneSeal","Retired","Sanctuary","Meteor","VoidRift"};
        string[] names={"라이트닝","얼음 송곳","화염 회오리","유성우","맹독 늪","암석 봉인","","회복의 성역","운석 낙하","공허 균열"};
        string[] descriptions={
            "지정 지점 주변에 낙뢰를 빠르게 3번 내립니다. 각성 4·8에서 같은 패턴의 낙뢰가 1번씩 늘어납니다.",
            "얼음 송곳을 차례로 솟아올려 적을 고르게 공격합니다.",
            "불꽃 회오리가 적을 쫓으며 주변에 지속 피해를 줍니다.",
            "성역보다 조금 넓은 범위의 무작위 지점에 붉은 별빛을 떨어뜨립니다. 적을 추적하지 않으며, 착탄할 때 작은 파동 안의 적들에게 피해를 줍니다.",
            "맹독 늪을 펼쳐 범위 안의 적에게 지속 피해를 주고 이동 속도를 25% 낮춥니다.",
            "바위를 연속으로 솟아올려 주변 적을 공격하고 1.4초간 기절시킵니다.",
            "",
            "성역을 펼쳐 범위 안에서 체력 비율이 가장 낮은 왕국군을 반복해서 회복합니다.",
            "운석을 떨어뜨려 주변 적을 공격합니다. 착탄 지점의 잔열이 두 번 더 피해를 줍니다.",
            "적이 모인 곳의 허공에 원형 균열을 엽니다. 균열보다 50% 넓은 범위의 적을 끌어당깁니다. 균열 안에서 지속 피해를 주고, 닫힐 때 폭발합니다. 보스는 끌어당기지 못합니다."};
        float[] powers={120,100,40,28,36,110,90,45,260,42}, cooldowns={10,12,15,10,14,16,12,18,16,20};
        int[] hits={3,4,10,18,6,2,2,6,1,5}, caps={3,1,3,6,5,4,5,3,6,6};
        float[] radii={.55f,.35f,.85f,.55f,1.4f,1.05f,.55f,2.6f,1.55f,1.3f}, ticks={2f/12f,.18f,.5f,.12f,.75f,.45f,.4f,.7f,.8f,.6f};
        GameObject[] visuals={lightning,ice,tornado,arcane,venom,stone,null,sanctuary,meteor,rift};
        var registry=AssetDatabase.LoadAssetAtPath<MageTowerSkillRegistrySO>("Assets/MageTower/SO/MageTowerSkillList.asset");
        if(registry==null)throw new InvalidOperationException("Existing mage registry is missing.");
        var skills=new List<MageTowerSkillSO>();
        for(int id=0;id<keys.Length;id++)
        {
            if (!MageSkillRules.IsAvailable(id)) continue;
            string folder="Assets/MageTower/SO/"+keys[id];EnsureFolder(folder);
            string path=folder+"/MageTowerSkill_"+keys[id]+".asset";
            var skill=AssetDatabase.LoadAssetAtPath<MageTowerSkillSO>(path);
            if(skill==null){skill=ScriptableObject.CreateInstance<MageTowerSkillSO>();AssetDatabase.CreateAsset(skill,path);}
            skill.id=id;skill.nameEng=keys[id];skill.nameKor=names[id];skill.spellKind=(MageSpellKind)id;
            skill.description=descriptions[id];
            skill.basePower=powers[id];skill.baseCooldown=cooldowns[id];skill.baseHits=hits[id];skill.radius=radii[id];skill.tickInterval=ticks[id];skill.duration=hits[id]*ticks[id];skill.maxTargets=caps[id];
            skill.maxEnhanceLevel=100;skill.maxAwakeningLevel=10;skill.prefab=visuals[id];
            skill.castingPrefab=id==5?telegraph:null;
            skill.secondaryPrefab=id==3?arcaneHit:id==7?heal:id==8?crater:id==9?collapse:null;
            skill.controlDuration=id==4?1.2f:id==5?1.4f:id==9?.85f:0;
            skill.slowFraction=id==4?.25f:id==9?.2f:0;
            skill.secondaryPowerRatio=id==8?35f/260f:id==9?180f/42f:0;
            skill.bloomName=id==0?"천벌":id==1?"만년빙정":id==3?"메테오":"미정";
            skill.bloomDescription=id==0?"뇌운을 모아 큰 범위에 거대한 벼락을 내립니다. 피해 1000% · 준비 2초 · 재사용 대기시간 2배.":id==1?"여러 적: 송곳 8개를 두 번 생성합니다. 각각 피해 50%.\n적 하나: 거대 빙정으로 피해 650%를 줍니다. 기절은 부여하지 않습니다.":id==3?"거대한 운석이 1.5초 동안 낙하해 피해 2000%를 줍니다.\n붉은 균열 장판이 3.5초 동안 0.5초마다 피해 200%를 주고, 장판 안의 적을 60% 감속합니다.\n재사용 대기시간 1.6배 · 드래그로 착탄 지점 지정 가능.":skill.IsHealing?"회복량 +15%. 전용 효과는 준비 중입니다.":"피해량 +15%. 전용 효과는 준비 중입니다.";
            skill.bloomCooldownMultiplier=id==0?2:id==3?1.6f:1;skill.bloomPowerMultiplier=id==0?10:id==1?6.5f:id==3?20:1.15f;
            skill.bloomAreaPowerMultiplier=id==3?2:.5f;skill.bloomControlDuration=0;skill.bloomMaxTargets=10;skill.bloomRadius=id==3?1.75f:2.1f;
            skill.scatterRadius=3;skill.bloomDuration=3.5f;skill.bloomGroundRadius=1.25f;skill.bloomTickInterval=.5f;skill.bloomSlowFraction=.6f;
            skill.bloomPrefab=id==0?thunder:id==1?glacier:id==3?meteor:null;skill.bloomCastingPrefab=id==0?cloud:id==1?iceCast:null;
            skill.bloomSecondaryPrefab=id==3?crater:null;
            skill.icon=InstallIcon(skill,keys[id],id);
            skill.bloomIcon=InstallBloomIcon(keys[id]);
            string[] sounds={"Lightning_SFX","Ice_Spike_SFX","Fire_Tornado_SFX","Charge_Shot_SFX","Water_Splash_SFX","Ice_Block_SFX","Slash_Attack_SFX","Parrying_SFX","Fire_Tornado_SFX","Charge_Shot_SFX"};
            skill.sfxName=sounds[id];
            EditorUtility.SetDirty(skill);skills.Add(skill);
        }
        registry.skills=skills;EditorUtility.SetDirty(registry);
        if(!MageSkillRules.ValidateRoster(skills))throw new InvalidOperationException("Mage catalog validation failed.");
        foreach(string guid in AssetDatabase.FindAssets("t:GachaTableSO",new[]{"Assets/Gacha"}))
        {
            var table=AssetDatabase.LoadAssetAtPath<GachaTableSO>(AssetDatabase.GUIDToAssetPath(guid));if(table.gachaType!=eGachaType.Skill)continue;
            table.costCurrency=eCurrency.AncientCoin;table.costAmount=50;table.isImplemented=true;
            table.description="스킬 8종 · 총 50%, 각 동일 확률 · 중복은 각성 파편 30개\n각성 10에서 스킬 개화를 켜고 끌 수 있습니다.";
            table.rewards=skills.Select(skill=>new GachaRewardEntry{nameKor=skill.nameKor,icon=skill.icon,rewardType=eGachaRewardType.Skill,skillId=skill.id,amount=1,weight=50f/MageSkillRules.SkillCount}).ToList();
            foreach(var pair in new[]{(10,30f),(20,15f),(50,5f)})table.rewards.Add(new GachaRewardEntry{nameKor="비전 지식 "+pair.Item1+"개",rewardType=eGachaRewardType.Currency,currency=eCurrency.ArcaneKnowledge,amount=pair.Item1,weight=pair.Item2});
            EditorUtility.SetDirty(table);
        }
        AssetDatabase.SaveAssets();Debug.Log("Mage catalog: 8 skills, 3 distinct blooms, 5 provisional blooms; existing IDs and icon GUIDs preserved.");
    }

    static void CopyChanged(string source,string target)
    {
        WriteAtomically(target,File.ReadAllBytes(source));
    }
    public static void WriteAtomically(string target,byte[] bytes)
    {
        if(File.Exists(target) && File.ReadAllBytes(target).SequenceEqual(bytes))return;
        AssetDatabase.ReleaseCachedFileHandles();
        // Windows cannot truncate an imported texture while Unity maps the old file.
        // Replace its directory entry atomically, leaving the meta/GUID untouched.
        string staging="Library/MageIcon-"+Guid.NewGuid().ToString("N")+".tmp";
        try
        {
            File.WriteAllBytes(staging,bytes);
            if(File.Exists(target))
            {
                try { File.Replace(staging,target,null); }
                catch(IOException) when(File.Exists(staging))
                {
                    // ReplaceFile can fail while merging metadata on a mapped file.
                    // Rename the new entry instead; never truncate the imported asset.
#if UNITY_EDITOR_WIN
                    if(!MoveFileEx(Path.GetFullPath(staging),Path.GetFullPath(target),1u|8u))
                        throw new System.ComponentModel.Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error());
#else
                    throw;
#endif
                }
            }
            else File.Move(staging,target);
        }
        finally { if(File.Exists(staging))File.Delete(staging); }
    }

#if UNITY_EDITOR_WIN
    [System.Runtime.InteropServices.DllImport("kernel32.dll",EntryPoint="MoveFileExW",CharSet=System.Runtime.InteropServices.CharSet.Unicode,SetLastError=true)]
    [return:System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    static extern bool MoveFileEx(string source,string destination,uint flags);
#endif

    static Sprite InstallIcon(MageTowerSkillSO skill,string key,int id)
    {
        string target=Art+"/Icons/MageTower/"+key+".png";
        if(!File.Exists(target)&&skill.icon!=null)
        {
            string previous=AssetDatabase.GetAssetPath(skill.icon);
            if(previous.StartsWith("Assets/Generated/ComfyUI/MageTower/",StringComparison.Ordinal))
            {string error=AssetDatabase.MoveAsset(previous,target);if(!string.IsNullOrEmpty(error))throw new IOException(error);}
        }
        string source=id==0?Generated+"/pilot-finish/core-resize/0.png":Generated+"/icons/"+id.ToString("00")+"-"+key+"/icon48.png";
        if(id==3) source="AI/comfyui/mage-skills/20260916-combat/Starfall-v1/icon48.png";
        if(key=="VenomMist" || key=="StoneSeal" || key=="Sanctuary") source="AI/comfyui/mage-skills/20260918-playability/v2/"+key+(target.Contains("_Bloom")?"_Bloom":"")+".png";
        if(id==0 || id==3) source="AI/comfyui/mage-vfx/revision6/icons/"+key+".png";
        CopyChanged(source,target);AssetDatabase.ImportAsset(target,ImportAssetOptions.ForceSynchronousImport);
        var importer=(TextureImporter)AssetImporter.GetAtPath(target);
        importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.spritePixelsPerUnit=48;
        importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;importer.isReadable=false;importer.npotScale=TextureImporterNPOTScale.None;importer.textureCompression=TextureImporterCompression.Uncompressed;
        importer.maxTextureSize=64;importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(target);
    }

    public static void RefreshSanctuary() => BuildSanctuary();

    static GameObject BuildSanctuary()
    {
        var prefab = Vfx("Sanctuary",
            new Layer(Refined+"SanctuaryGround.png",5.5f,3.5f,Color.white,loop:true,fps:12/.7f),
            new Layer(Refined+"SanctuaryCrest.png",2f,2f,Color.white,y:.32f,frame:0));
        string path=AssetDatabase.GetAssetPath(prefab);
        var root=PrefabUtility.LoadPrefabContents(path);
        try
        {
            var sustain=root.AddComponent<SanctuarySustainVfx>();
            sustain.crest=root.transform.GetChild(1);
            var life=root.GetComponent<PooledSpellVfx>(); life.fadeIn=.32f; life.fadeOut=.32f; life.startScale=1;
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        return prefab;
    }

    static void EditMeteor(GameObject prefab)
    {
        string path=AssetDatabase.GetAssetPath(prefab);
        var root=PrefabUtility.LoadPrefabContents(path);
        try
        {
            root.GetComponent<PooledSpellVfx>().fadeOut=0;
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static GameObject Vfx(string key,params Layer[] layers)
    {
        var root=new GameObject(key);var lifetime=root.AddComponent<PooledSpellVfx>();lifetime.lifetime=1;lifetime.fadeOut=.15f;
        bool field = key=="VenomMist" || key=="Sanctuary" || key=="VoidRift" || key=="FireTornado";
        if (field || key=="LightningBloomCloud" || key=="GroundTelegraph")
        { lifetime.fadeIn=key=="LightningBloomCloud"?.3f:.12f; lifetime.startScale=.84f; lifetime.fadeOut=.22f; }
        try
        {
            for(int i=0;i<layers.Length;i++)
            {
                var layer=layers[i];
                AssetDatabase.ImportAsset(layer.Texture,ImportAssetOptions.ForceSynchronousImport);
                var importer=(TextureImporter)AssetImporter.GetAtPath(layer.Texture);
                if(importer.filterMode!=FilterMode.Point||importer.mipmapEnabled||importer.isReadable)
                {importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;importer.isReadable=false;importer.SaveAndReimport();}
                var sprites=AssetDatabase.LoadAllAssetsAtPath(layer.Texture).OfType<Sprite>().OrderBy(s=>FrameNumber(s.name)).ToArray();
                if(sprites.Length==0)throw new InvalidOperationException("No sliced sprites: "+layer.Texture);
                var child=new GameObject("Layer"+i);child.transform.SetParent(root.transform,false);child.transform.localPosition=layer.Offset;
                var renderer=child.AddComponent<SpriteRenderer>();renderer.sprite=sprites[Math.Max(0,layer.StaticFrame)];renderer.color=layer.Color;
                CombatVfxOrder.Apply(renderer,key,i);
                renderer.sharedMaterial=AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
                var size=renderer.sprite.bounds.size;child.transform.localScale=new Vector3(layer.Size.x/size.x,layer.Size.y/size.y,1);
                if(layer.StaticFrame>=0)continue;
                string clipPath=AnimationRoot+"/"+key+"_"+i+".anim";
                var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                if(clip==null){clip=new AnimationClip();AssetDatabase.CreateAsset(clip,clipPath);}clip.frameRate=layer.Fps;
                var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=layer.Loop;AnimationUtility.SetAnimationClipSettings(clip,settings);
                var keys=new ObjectReferenceKeyframe[sprites.Length+1];for(int frame=0;frame<keys.Length;frame++)keys[frame]=new ObjectReferenceKeyframe{time=frame/layer.Fps,value=frame<sprites.Length?sprites[frame]:layer.Loop?sprites[0]:null};
                AnimationUtility.SetObjectReferenceCurve(clip,EditorCurveBinding.PPtrCurve("",typeof(SpriteRenderer),"m_Sprite"),keys);AnimationUtility.SetAnimationEvents(clip,Array.Empty<AnimationEvent>());EditorUtility.SetDirty(clip);
                string controllerPath=AnimationRoot+"/"+key+"_"+i+".controller";
                var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
                if(controller==null)controller=AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                var machine=controller.layers[0].stateMachine;var state=machine.defaultState??machine.AddState("Play");state.motion=clip;machine.defaultState=state;EditorUtility.SetDirty(controller);
                child.AddComponent<Animator>().runtimeAnimatorController=controller;
            }
            return PrefabUtility.SaveAsPrefabAsset(root,Prefabs+"/"+key+".prefab");
        }
        finally { Object.DestroyImmediate(root); }
    }
    static int FrameNumber(string name)
    {
        var match=System.Text.RegularExpressions.Regex.Match(name,@"(\d+)$");
        return match.Success && int.TryParse(match.Value,out int value)?value:0;
    }
    static Sprite InstallBloomIcon(string key)
    {
        string source="AI/comfyui/mage-skills/20260916/"+key+"/icon48.png";
        if(key=="ArcaneVolley") source="AI/comfyui/mage-skills/20260916-combat/StarfallBloom-v1/icon48.png";
        string target=Art+"/Icons/MageTower/"+key+"_Bloom.png";
        if(key=="VenomMist" || key=="StoneSeal" || key=="Sanctuary") source="AI/comfyui/mage-skills/20260918-playability/v2/"+key+(target.Contains("_Bloom")?"_Bloom":"")+".png";
        source="AI/comfyui/mage-vfx/revision6/icons/"+key+"_Bloom.png";
        if(!File.Exists(source))throw new FileNotFoundException("Bloom icon missing",source);
        CopyChanged(source,target);AssetDatabase.ImportAsset(target,ImportAssetOptions.ForceSynchronousImport);
        var importer=(TextureImporter)AssetImporter.GetAtPath(target);
        importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.spritePixelsPerUnit=48;
        importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;importer.isReadable=false;importer.npotScale=TextureImporterNPOTScale.None;
        importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=64;importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(target);
    }
    static void PrepareDerivedSheets()
    {
        var factories=new SpriteDataProviderFactories();factories.Init();
        foreach(string path in Directory.GetFiles(Polished,"*.png"))
        {
            string normalized=path.Replace('\\','/');if(normalized.EndsWith("LightningCloud.png"))continue;
            AssetDatabase.ImportAsset(normalized,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(normalized);
            bool single=normalized.EndsWith("MeteorScorch.png") || normalized.EndsWith("MeteorBody64.png");
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=single?SpriteImportMode.Single:SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit=32;importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;importer.isReadable=false;
            importer.npotScale=TextureImporterNPOTScale.None;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.alphaIsTransparency=true;
            if(!single)
            {
                var provider=factories.GetSpriteEditorDataProviderFromObject(importer);provider.InitSpriteEditorDataProvider();
                var previous=provider.GetSpriteRects();var rects=new SpriteRect[6];string key=Path.GetFileNameWithoutExtension(path);
                for(int i=0;i<6;i++)rects[i]=new SpriteRect{name=key+"_"+i,rect=new Rect(0,(5-i)*64,64,64),alignment=SpriteAlignment.Center,pivot=new Vector2(.5f,.5f),spriteID=i<previous.Length?previous[i].spriteID:GUID.Generate()};
                provider.SetSpriteRects(rects);
                var names=provider.GetDataProvider<ISpriteNameFileIdDataProvider>();names.SetNameFileIdPairs(rects.Select(r=>new SpriteNameFileIdPair(r.name,r.spriteID)));
                provider.Apply();
            }
            importer.SaveAndReimport();
        }
    }
    static void EnsureFolder(string folder)
    {
        if(AssetDatabase.IsValidFolder(folder))return;string parent=Path.GetDirectoryName(folder).Replace('\\','/');EnsureFolder(parent);AssetDatabase.CreateFolder(parent,Path.GetFileName(folder));
    }
}
