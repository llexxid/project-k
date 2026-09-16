using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using KingdomIdle.MageTower;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>Applies the current mage presentation and removes retired gameplay references.</summary>
    public static class MagePolishPreparation
    {
        public static void Prepare()
        {
            const string retiredArt="Assets/Generated/ComfyUI/DivineSkill";
            if(AssetDatabase.IsValidFolder(retiredArt))
            {
                string error=AssetDatabase.MoveAsset(retiredArt,"Assets/_Project/Art/Archive/Divine/Illustrations");
                if(!string.IsNullOrEmpty(error))throw new IOException(error);
            }
            MageSkillAssetPreparation.Build();
            MageUiPreparation.Build();
            string path = "Assets/UGUI/Prefabs/Screens/Screen_Main.prefab";
            if (File.Exists(path))
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach (var child in root.GetComponentsInChildren<Transform>(true))
                        if (child != null && child.name == "BtnMenuDivineCollection") UnityEngine.Object.DestroyImmediate(child.gameObject);
                    PrefabUtility.SaveAsPrefabAsset(root,path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            path = "Assets/UGUI/Prefabs/Popups/Panel_MageTowerEquip.prefab";
            if (File.Exists(path))
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach (var label in root.GetComponentsInChildren<TMP_Text>(true))
                        if (label.text.Contains("보유 스킬")) label.text = label.text.Replace("보유 스킬","스킬 목록");
                    PrefabUtility.SaveAsPrefabAsset(root,path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            var catalog=PrefabGenUtil.GetOrCreateCatalog();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            BakeAuthoredGlyphs();
            KingdomIdle.EditorTools.Optimization.OptAtlases.CreateInBuildAtlases();
            AssetDatabase.SaveAssets();
            ExportCatalog();
            Debug.Log("[MagePolish] Assets, UI, authored glyphs and atlases prepared.");
        }

        static void ExportCatalog()
        {
            const string folder=".utmp/catalog-integration/mage-validation";Directory.CreateDirectory(folder);
            var skills=AssetDatabase.LoadAssetAtPath<MageTowerSkillRegistrySO>("Assets/MageTower/SO/MageTowerSkillList.asset").skills.OrderBy(s=>s.id).ToArray();
            File.WriteAllText(folder+"/catalog.json","["+string.Join(",",skills.Select(s=>JsonUtility.ToJson(s,true)))+"]");
            File.WriteAllLines(folder+"/assets.tsv",skills.Select(s=>string.Join("\t",new[]{s.id.ToString(),AssetDatabase.GetAssetPath(s),AssetDatabase.GetAssetPath(s.icon),AssetDatabase.GetAssetPath(s.prefab),AssetDatabase.GetAssetPath(s.bloomPrefab),AssetDatabase.GetAssetPath(s.bloomCastingPrefab),AssetDatabase.GetAssetPath(s.bloomIcon),AssetDatabase.GetAssetPath(s.castingPrefab),AssetDatabase.GetAssetPath(s.secondaryPrefab)})));
        }

        [MenuItem("KingdomIdle/UGUI/Bake Authored Korean Glyphs")]
        public static void BakeAuthoredGlyphs()
        {
            var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/UGUI/Art/Font/Galmuri11 SDF.asset");
            if(font==null) throw new InvalidOperationException("UI font missing.");
            var chars=new HashSet<char>("0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz %+-.,:;/()[]◆×·→");
            // Bake authored Hangul before shipping. Dynamic population remains available for player names.
            foreach(var directory in new[]{"Assets/UGUI/Scripts","Assets/UGUI/Editor","Assets/MageTower","Assets/_Project/Scripts/MageTower","Assets/_Project/Scripts/Editor"})
                foreach(var file in Directory.EnumerateFiles(directory,"*.cs",SearchOption.AllDirectories))
                    foreach(char ch in File.ReadAllText(file)) if(ch>='가'&&ch<='힣') chars.Add(ch);
            foreach(var skill in AssetDatabase.LoadAssetAtPath<MageTowerSkillRegistrySO>("Assets/MageTower/SO/MageTowerSkillList.asset").skills)
                foreach(char ch in skill.nameKor+skill.description+skill.bloomName+skill.bloomDescription) if(!char.IsControl(ch)) chars.Add(ch);
            string needed=new string(chars.OrderBy(c=>c).Where(c=>!font.HasCharacter(c)).ToArray());
            bool success=needed.Length==0 || font.TryAddCharacters(needed,out _);
            string missing=new string(chars.Where(c=>!font.HasCharacter(c)).ToArray());
            // Decorative symbols have a plain-text fallback; authored Korean must never be missing.
            if(missing.Any(c=>c>='가'&&c<='힣')) throw new InvalidOperationException("Missing authored Korean glyphs: "+missing);
            EditorUtility.SetDirty(font);AssetDatabase.SaveAssets();
            Directory.CreateDirectory("Docs/ArtPreparation/Validation/MageIntegration");
            File.WriteAllText("Docs/ArtPreparation/Validation/MageIntegration/glyph-bake.json",JsonUtility.ToJson(new GlyphReport{requested=needed.Length,characters=font.characterTable.Count,pages=font.atlasTextureCount,missing=missing,success=success},true));
        }
        [Serializable] class GlyphReport { public int requested,characters,pages; public string missing; public bool success; }
        public static void Validate()
        {
            var registry=AssetDatabase.LoadAssetAtPath<MageTowerSkillRegistrySO>("Assets/MageTower/SO/MageTowerSkillList.asset");
            if(!MageSkillRules.ValidateRoster(registry.skills))throw new InvalidOperationException("Invalid mage roster");
            int components=0;
            foreach(var skill in registry.skills)
                if(skill.icon==null || skill.bloomIcon==null || skill.icon==skill.bloomIcon || skill.prefab==null)
                    throw new InvalidOperationException("Missing skill presentation: "+skill.id);
            var prefabs=AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/_Project/Prefabs/VFX/MageTower"});
            foreach(var guid in prefabs)
            {
                var root=AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                foreach(var transform in root.GetComponentsInChildren<Transform>(true))
                    if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject)>0)
                        throw new InvalidOperationException("Missing script: "+root.name);
                foreach(var component in root.GetComponentsInChildren<Component>(true))
                {
                    components++;
                    var serialized=new SerializedObject(component);var property=serialized.GetIterator();
                    while(property.NextVisible(true))
                        if(property.propertyType==SerializedPropertyType.ObjectReference && property.objectReferenceValue==null && property.objectReferenceInstanceIDValue!=0)
                            throw new InvalidOperationException("Missing reference: "+root.name+"/"+property.propertyPath);
                }
            }
            var roots=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path)
                .Concat(new[]{"Assets/MageTower/SO/MageTowerSkillList.asset","Assets/UGUI/UIViewCatalog.asset"}).ToArray();
            var dependencies=AssetDatabase.GetDependencies(roots,true);
            var retired=dependencies.Where(p=>p.StartsWith("Assets/DivineSkill/") || p.StartsWith("Assets/_Project/Art/Archive/Divine/")).ToArray();
            if(retired.Length>0)throw new InvalidOperationException("Retired gameplay dependency: "+string.Join(",",retired));
            var external=AssetDatabase.GetDependencies("Assets/MageTower/SO/MageTowerSkillList.asset",true).Where(p=>p.StartsWith("Assets/ExternalAssets/")).ToArray();
            if(external.Length>0)throw new InvalidOperationException("Mage source archive dependency");
            string report=$"skills={registry.skills.Count} baseIcons=10 bloomIcons=10 prefabs={prefabs.Length} components={components} missingReferences=0 retiredDependencies=0 externalMageDependencies=0 Unity={Application.unityVersion} version={PlayerSettings.bundleVersion}";
            File.WriteAllText("Docs/ArtPreparation/Validation/MageIntegration/unity-polish-validation.txt",report);
            Debug.Log(report);
        }
        public static void BuildDevice() { Prepare(); TitleLobbyDeviceBuild.Build(); }
    }
}
