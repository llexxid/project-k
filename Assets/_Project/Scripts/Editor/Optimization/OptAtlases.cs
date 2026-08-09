// Mobile art optimization — Sprite Atlas V2 creation (in-build atlases).
// Collapses many separate textures into a few atlas pages -> big UGUI/SpriteRenderer batching win + POT-for-free.
// Atlasing never changes source sprite GUID/fileID, so all AnimationClip / prefab sprite refs survive (late-bound).
//
// In-build atlases only here (Monster/VFX Addressable atlases handled separately to avoid bundle duplication).
//   Atlas_Characters : player job + royal guard character sheets   Point / ASTC 4x4  (pixel-art)
//   Atlas_Equipment  : equipment rarity icon sheets                Point / ASTC 4x4  (pixel-art UI)
//   Atlas_UI         : shipped Layer Lab UI sprites + Circle/RoundedRect  Bilinear / ASTC 6x6
//
// Run:  ... -executeMethod KingdomIdle.EditorTools.Optimization.OptAtlases.CreateInBuildAtlases
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

namespace KingdomIdle.EditorTools.Optimization
{
    public static class OptAtlases
    {
        const string AtlasDir = "Assets/_Project/Art/Atlases";

        [MenuItem("KingdomIdle/Optimize/2) Create In-Build Sprite Atlases")]
        public static void CreateInBuildAtlases()
        {
            if (!Directory.Exists(AtlasDir)) { Directory.CreateDirectory(AtlasDir); AssetDatabase.Refresh(); }
            var log = new System.Text.StringBuilder();

            // --- Atlas_Characters : folders of character sprite sheets (pixel-art, Point) ---
            var charPackables = LoadFolders(new[]
            {
                "Assets/_Project/Scripts/Player/Job",
                "Assets/_Project/Prefabs/Royal_Guard_Lancer",
                "Assets/_Project/Art/Sprites/RoyalGuard/EliteArcher",
            }, log);
            BuildAtlas($"{AtlasDir}/Atlas_Characters.spriteatlasv2", true, FilterMode.Point,
                TextureImporterFormat.ASTC_4x4, padding: 4, tight: false, charPackables, log);

            // --- Atlas_Equipment : equipment rarity icon sheets (pixel-art UI, Point) ---
            var equipPackables = LoadFolders(new[]
            {
                "Assets/_Project/Scripts/Player/Equipment/Sprite",
            }, log);
            BuildAtlas($"{AtlasDir}/Atlas_Equipment.spriteatlasv2", true, FilterMode.Point,
                TextureImporterFormat.ASTC_4x4, padding: 4, tight: false, equipPackables, log);

            // --- Atlas_UI : shipped Layer Lab sprites (via UI dependency scan) + first-party smooth UI ---
            var uiPackables = GatherUiSprites(log);
            BuildAtlas($"{AtlasDir}/Atlas_UI.spriteatlasv2", true, FilterMode.Bilinear,
                TextureImporterFormat.ASTC_6x6, padding: 8, tight: false, uiPackables, log);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Pack for the active (Android) target and report sprite counts.
            SpriteAtlasUtility.PackAllAtlases(EditorUserBuildSettings.activeBuildTarget);
            foreach (var name in new[] { "Atlas_Characters", "Atlas_Equipment", "Atlas_UI" })
            {
                var sa = AssetDatabase.LoadAssetAtPath<SpriteAtlas>($"{AtlasDir}/{name}.spriteatlasv2");
                log.AppendLine(sa != null
                    ? $"[PACKED] {name}: spriteCount={sa.spriteCount}"
                    : $"[PACKED] {name}: <NULL - not created!>");
            }
            Debug.Log(log.ToString());
        }

        static List<Object> LoadFolders(string[] folders, System.Text.StringBuilder log)
        {
            var list = new List<Object>();
            foreach (var f in folders)
            {
                if (!AssetDatabase.IsValidFolder(f)) { log.AppendLine($"  [folder MISSING] {f}"); continue; }
                var o = AssetDatabase.LoadMainAssetAtPath(f);
                if (o != null) { list.Add(o); log.AppendLine($"  +folder {f}"); }
            }
            return list;
        }

        // Collect the Layer Lab sprites actually referenced by shipped UI (prefabs + catalog),
        // plus first-party smooth UI (Circle, RoundedRect). Excludes the standalone Background.
        static List<Object> GatherUiSprites(System.Text.StringBuilder log)
        {
            var roots = new List<string>();
            roots.AddRange(AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/UGUI/Prefabs" })
                .Select(AssetDatabase.GUIDToAssetPath));
            if (File.Exists("Assets/UGUI/UIViewCatalog.asset")) roots.Add("Assets/UGUI/UIViewCatalog.asset");

            var deps = AssetDatabase.GetDependencies(roots.ToArray(), true);
            var ll = deps
                .Select(d => d.Replace('\\', '/'))
                .Where(d => d.Contains("/Layer Lab/")
                            && (d.EndsWith(".png") || d.EndsWith(".Png"))
                            && !d.Contains("Background_04"))
                .Distinct()
                .OrderBy(d => d)
                .ToList();

            var list = new List<Object>();
            foreach (var p in ll)
            {
                var o = AssetDatabase.LoadMainAssetAtPath(p);
                if (o != null) list.Add(o);
            }
            log.AppendLine($"  [Atlas_UI] Layer Lab shipped sprites referenced by UI: {ll.Count}");

            // first-party smooth UI shapes (Bilinear)
            foreach (var p in new[] { "Assets/UGUI/Sprites/Circle.png", "Assets/UGUI/Sprites/RoundedRect.png" })
            {
                var o = AssetDatabase.LoadMainAssetAtPath(p);
                if (o != null) { list.Add(o); log.AppendLine($"  [Atlas_UI] +{p}"); }
            }
            return list;
        }

        static void BuildAtlas(string path, bool includeInBuild, FilterMode filter,
            TextureImporterFormat androidFmt, int padding, bool tight, List<Object> packables,
            System.Text.StringBuilder log)
        {
            if (packables == null || packables.Count == 0)
            {
                log.AppendLine($"[SKIP] {path} — no packables");
                return;
            }
            if (File.Exists(path)) AssetDatabase.DeleteAsset(path);

            var asset = new SpriteAtlasAsset();
            asset.SetIncludeInBuild(includeInBuild);
            asset.SetIsVariant(false);
            asset.Add(packables.ToArray());
            SpriteAtlasAsset.Save(asset, path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

            var imp = AssetImporter.GetAtPath(path) as SpriteAtlasImporter;
            if (imp == null) { log.AppendLine($"[ERR] importer null for {path}"); return; }

            imp.packingSettings = new SpriteAtlasPackingSettings
            {
                padding = padding,
                blockOffset = 1,
                enableRotation = false,
                enableTightPacking = tight,
            };
            imp.textureSettings = new SpriteAtlasTextureSettings
            {
                filterMode = filter,
                generateMipMaps = false,
                readable = false,
                sRGB = true,
            };
            imp.SetPlatformSettings(new TextureImporterPlatformSettings
            {
                name = "Android",
                overridden = true,
                format = androidFmt,
                maxTextureSize = 2048,
                textureCompression = TextureImporterCompression.Compressed,
                compressionQuality = 100,
            });
            imp.SaveAndReimport();
            log.AppendLine($"[BUILT] {Path.GetFileName(path)} includeInBuild={includeInBuild} filter={filter} fmt={androidFmt} pad={padding} tight={tight} packables={packables.Count}");
        }
    }
}
