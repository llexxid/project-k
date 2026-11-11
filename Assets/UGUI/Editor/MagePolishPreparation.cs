using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using KingdomIdle.MageTower;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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

        private const string UiFontPath = "Assets/UGUI/Art/Font/Galmuri11 SDF.asset";

        [MenuItem("KingdomIdle/UGUI/Bake Authored Korean Glyphs")]
        public static void BakeAuthoredGlyphs()
        {
            var font = LoadUiFont();
            var characters = CollectAuthoredCharacters();
            var needed = MissingCharacters(font, characters);
            if (needed.Length > 0)
            {
                font.TryAddCharacters(needed, out _);
                EditorUtility.SetDirty(font);
                AssetDatabase.SaveAssets();
            }
            var missing = MissingCharacters(font, characters);
            RequireAuthoredHangul(missing);
            Debug.Log($"[UI glyph bake] requested={needed.Length}, characters={font.characterTable.Count}, pages={font.atlasTextureCount}, unsupported={missing.Length}");
        }

        internal static void ValidateAuthoredGlyphs()
        {
            var font = LoadUiFont();
            var characters = CollectAuthoredCharacters();
            RequireAuthoredHangul(MissingCharacters(font, characters));
            Debug.Log($"[UI glyph validation] authored={characters.Length}, characters={font.characterTable.Count}, pages={font.atlasTextureCount}, missingHangul=0");
        }

        private static TMP_FontAsset LoadUiFont()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(UiFontPath);
            if (font == null) throw new InvalidOperationException("UI font missing: " + UiFontPath);
            return font;
        }

        internal static uint[] CollectAuthoredCharacters()
        {
            var characters = new HashSet<uint>();
            AddText(characters, "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz %+-.,:;/()[]◆×·→›");
            foreach (var directory in new[] { "Assets/UGUI/Scripts", "Assets/UGUI/Editor", "Assets/MageTower",
                "Assets/_Project/Scripts/MageTower", "Assets/_Project/Scripts/Editor", "Assets/_Project/Scripts/Core/Balance" })
                foreach (var file in Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
                    foreach (char character in File.ReadAllText(file))
                        if (character >= '가' && character <= '힣') characters.Add(character);

            var registry = AssetDatabase.LoadAssetAtPath<MageTowerSkillRegistrySO>("Assets/MageTower/SO/MageTowerSkillList.asset");
            if (registry == null) throw new InvalidOperationException("Mage roster is missing while collecting UI glyphs.");
            foreach (var skill in registry.skills)
                AddText(characters, skill.nameKor + skill.description + skill.bloomName + skill.bloomDescription);

            foreach (var directory in new[] { "Assets/_Project/Resources/Balance", "Assets/_Project/Resources/PatchNotes" })
            {
                if (!Directory.Exists(directory)) continue;
                foreach (var file in Directory.EnumerateFiles(directory, "*.json", SearchOption.AllDirectories))
                {
                    try { characters.UnionWith(CollectJsonCharacters(File.ReadAllText(file))); }
                    catch (JsonException error)
                    {
                        throw new InvalidOperationException("Cannot collect UI glyphs from " + file + ": " + error.Message, error);
                    }
                }
            }
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/UGUI/Prefabs" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) throw new InvalidOperationException("Cannot read authored UI text from " + path);
                foreach (var label in prefab.GetComponentsInChildren<TMP_Text>(true)) AddText(characters, label.text);
            }
            return characters.OrderBy(value => value).ToArray();
        }

        internal static uint[] CollectJsonCharacters(string text)
        {
            var json = JToken.Parse(text);
            IEnumerable<JToken> values = json is JContainer container ? container.Descendants() : new[] { json };
            var characters = new HashSet<uint>();
            foreach (var value in values)
                if (value.Type == JTokenType.String) AddText(characters, (string)value);
            return characters.OrderBy(value => value).ToArray();
        }

        private static void AddText(HashSet<uint> characters, string text)
        {
            if (text == null) return;
            for (int i = 0; i < text.Length; i++)
            {
                uint unicode = (uint)char.ConvertToUtf32(text, i);
                if (!char.IsControl(text, i)) characters.Add(unicode);
                if (unicode > char.MaxValue) i++;
            }
        }

        private static uint[] MissingCharacters(TMP_FontAsset font, uint[] characters) =>
            characters.Where(value => !font.HasCharacter((int)value)).ToArray();

        internal static void RequireAuthoredHangul(uint[] missing)
        {
            var hangul = missing.Where(value => value >= 0xAC00 && value <= 0xD7A3).ToArray();
            if (hangul.Length == 0) return;
            string codes = string.Join(", ", hangul.Select(value => "U+" + value.ToString("X4")));
            throw new InvalidOperationException("Authored UI Hangul is not baked: " + codes +
                ". Run KingdomIdle/UGUI/Bake Authored Korean Glyphs before building.");
        }
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
            string report=$"skills={registry.skills.Count} baseIcons={registry.skills.Count} bloomIcons={registry.skills.Count} prefabs={prefabs.Length} components={components} missingReferences=0 retiredDependencies=0 externalMageDependencies=0 Unity={Application.unityVersion} version={PlayerSettings.bundleVersion}";
            MageSkillAssetPreparation.WriteAtomically("Docs/ArtPreparation/Validation/MageIntegration/unity-polish-validation.txt",System.Text.Encoding.UTF8.GetBytes(report));
            Debug.Log(report);
        }
        public static void BuildDevice() { Prepare(); TitleLobbyDeviceBuild.Build(); }
    }
}
