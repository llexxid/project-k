using System;
using System.IO;
using System.Linq;
using KingdomIdle.Combat;
using KingdomIdle.MageTower;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace KingdomIdle.UGUI.Editor
{
    public static class CombatPresentationPreparation
    {
        const string Art = "Assets/_Project/Art/VFX/CombatStatus";
        public const string Output = "Recordings/CombatPresentation/Editor";
        public static void Prepare()
        {
            Directory.CreateDirectory(Output);
            Directory.CreateDirectory(Art);
            Directory.CreateDirectory("Assets/_Project/Resources");
            AssetDatabase.Refresh();
            Copy("Assets/_Project/Art/Archive/Divine/VFX/Art/Astra_StunStars.png", Art + "/StunStars32.png");
            Copy("Assets/ExternalAssets/StateEffect/EffectMaterials/Sprites/Effect_Rage_Overhead1.png", Art + "/Taunt18.png");
            string ringSource = "Assets/_Project/Art/VFX/PixelArtRPGVFX/Textures/Holy/HolyBlessing.png";
            var ring = new Texture2D(2,2,TextureFormat.RGBA32,false);
            try
            {
                ring.LoadImage(File.ReadAllBytes(ringSource));
                var pixels = ring.GetPixels32();
                // Preserve the existing pixels/alpha/detail; neutral lightness permits accurate status colors.
                for (int i=0;i<pixels.Length;i++) { var p=pixels[i]; byte value=Math.Max(p.r,Math.Max(p.g,p.b)); pixels[i]=new Color32(value,value,value,p.a); }
                ring.SetPixels32(pixels);ring.Apply();
                MageSkillAssetPreparation.WriteAtomically(Art+"/FootRing64.png",ring.EncodeToPNG());
            }
            finally { UnityEngine.Object.DestroyImmediate(ring); }
            AssetDatabase.ImportAsset(Art+"/FootRing64.png",ImportAssetOptions.ForceSynchronousImport);
            Slice(Art+"/FootRing64.png",64,64,true,64);
            Slice(Art+"/Taunt18.png",18,18,false,32);
            var config = AssetDatabase.LoadAssetAtPath<CombatStatusArt>("Assets/_Project/Resources/CombatStatusArt.asset");
            if(config==null){config=ScriptableObject.CreateInstance<CombatStatusArt>();AssetDatabase.CreateAsset(config,"Assets/_Project/Resources/CombatStatusArt.asset");}
            config.stun=Sprites(Art+"/StunStars32.png"); config.footRing=Sprites(Art+"/FootRing64.png");
            config.taunt=Sprites(Art+"/Taunt18.png").Last(); EditorUtility.SetDirty(config);
            foreach(string id in new[]{"Spearman","Knight","Elite_Knight","Mage","Elite_Mage"})
            {
                var job=AssetDatabase.LoadAssetAtPath<JobData>("Assets/_Project/Scripts/Player/Job/SO/"+id+".asset");
                var sprite=job.jobSprite; var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
                try
                {
                    texture.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite)));
                    int bottom=int.MaxValue,top=int.MinValue;
                    // Central body strip excludes extended spears, shields and side-mounted weapons.
                    int half=Mathf.RoundToInt(sprite.pixelsPerUnit*.20f), center=Mathf.RoundToInt(sprite.pivot.x);
                    for(int x=Math.Max(0,center-half);x<Math.Min(sprite.rect.width,center+half);x++)
                    for(int y=0;y<sprite.rect.height;y++)
                        if(texture.GetPixel((int)sprite.rect.x+x,(int)sprite.rect.y+y).a>.5f){bottom=Math.Min(bottom,y);top=Math.Max(top,y+1);}
                    if(top<=bottom)throw new Exception("Missing idle body: "+id);
                    job.vfxFootY=(bottom-sprite.pivot.y)/sprite.pixelsPerUnit;
                    job.vfxHeadY=(top-sprite.pivot.y)/sprite.pixelsPerUnit;EditorUtility.SetDirty(job);
                }
                finally{UnityEngine.Object.DestroyImmediate(texture);}
            }
            MageSkillAssetPreparation.Build();
            KingdomIdle.EditorTools.Optimization.OptAtlases.CreateInBuildAtlases();
            PlayerSettings.bundleVersion="0.12.1";
            AssetDatabase.SaveAssets(); Validate();
        }
        static void Copy(string from,string to) { if(!File.Exists(to) && !AssetDatabase.CopyAsset(from,to))throw new IOException(to); }
        static Sprite[] Sprites(string path)=>AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s=>int.Parse(System.Text.RegularExpressions.Regex.Match(s.name,@"\d+$").Value)).ToArray();
        static void Slice(string path,int width,int height,bool vertical,int ppu)
        {
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit=ppu;importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;importer.isReadable=false;importer.npotScale=TextureImporterNPOTScale.None;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=512;
            var factories=new SpriteDataProviderFactories();factories.Init();var provider=factories.GetSpriteEditorDataProviderFromObject(importer);provider.InitSpriteEditorDataProvider();
            var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(path);int count=vertical?tex.height/height:tex.width/width;
            var old=provider.GetSpriteRects();
            provider.SetSpriteRects(Enumerable.Range(0,count).Select(i=>new SpriteRect{name=Path.GetFileNameWithoutExtension(path)+"_"+i,rect=new Rect(vertical?0:i*width,vertical?tex.height-(i+1)*height:0,width,height),pivot=new Vector2(.5f,.5f),alignment=SpriteAlignment.Center,spriteID=i<old.Length?old[i].spriteID:GUID.Generate()}).ToArray());
            provider.Apply();importer.SaveAndReimport();
        }
        public static void Validate()
        {
            Directory.CreateDirectory(Output);
            var rows=AssetDatabase.LoadAssetAtPath<MageTowerSkillRegistrySO>("Assets/MageTower/SO/MageTowerSkillList.asset").skills
                .SelectMany(s=>new[]{s.prefab,s.secondaryPrefab,s.castingPrefab,s.bloomPrefab,s.bloomCastingPrefab}).Where(p=>p!=null).Distinct()
                .SelectMany(p=>p.GetComponentsInChildren<SpriteRenderer>(true).Select((r,i)=>new{prefab=p.name,child=r.name,layer=r.sortingLayerName,order=r.sortingOrder,ground=CombatVfxOrder.IsGround(p.name,i)})).ToArray();
            if(rows.Any(r=>r.ground?(r.layer!="Default"||r.order>=2):(r.layer!="CombatVFX"||r.order<CombatVfxOrder.Impact)))throw new Exception("Spell layering invalid");
            var art=AssetDatabase.LoadAssetAtPath<CombatStatusArt>("Assets/_Project/Resources/CombatStatusArt.asset");
            if(art==null||art.stun.Length==0||art.footRing.Length==0||art.taunt==null)throw new Exception("Missing status art");
            var anchors=new[]{"Spearman","Knight","Elite_Knight","Mage","Elite_Mage"}.Select(id=>AssetDatabase.LoadAssetAtPath<JobData>("Assets/_Project/Scripts/Player/Job/SO/"+id+".asset")).Select(j=>new{j.jobName,j.vfxFootY,j.vfxHeadY}).ToArray();
            File.WriteAllText(Output+"/layers.json",JsonConvert.SerializeObject(new{passed=true,rows,anchors,stunFrames=art.stun.Length,ringFrames=art.footRing.Length},Formatting.Indented));
            Debug.Log("COMBAT PRESENTATION REFERENCES PASSED: "+rows.Length+" spell renderers");
        }
        public static void BuildDevice(){Prepare();TitleLobbyDeviceBuild.Build();}
        public static void BuildFinal(){Validate();TitleLobbyDeviceBuild.BuildForManualTesting();}
    }
}
