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
using UnityEditor.AddressableAssets;
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
            var charPackables = new[]
            {
                "Assets/_Project/Art/Sprites/RoyalGuard/Arbalest/Royal Arbalest Sprite Sheet.png",
                "Assets/_Project/Art/Sprites/RoyalGuard/Archer/Royal Archer Sprite Sheet.png",
                "Assets/_Project/Art/Sprites/RoyalGuard/EliteArcher/AnimationSheets/Elite Archer Sprite Sheet.png",
                "Assets/_Project/Art/Sprites/RoyalGuard/EliteKnight/Attack Sprite Sheet.png",
                "Assets/_Project/Art/Sprites/RoyalGuard/EliteKnight/Elite Knight Sprite Sheet.png",
                "Assets/_Project/Art/Sprites/RoyalGuard/EliteMage/Blast Spell Sprite Sheet.png",
                "Assets/_Project/Art/Sprites/RoyalGuard/EliteMage/Elite Mage Sprite Sheet.png",
                "Assets/_Project/Art/Sprites/RoyalGuard/Knight/Royal Knight - Alternate 2 Sprite Sheet.png",
                "Assets/_Project/Art/Sprites/RoyalGuard/Mage/Royal Mage Sprite Sheet.png",
                "Assets/_Project/Art/Sprites/RoyalGuard/Spearman/Royal Spearman Sprite Sheet.png",
                "Assets/_Project/Art/Sprites/RoyalGuard/Lancer/Royal Guard Sprite Sheet.png",
                "Assets/_Project/Art/Sprites/RoyalGuard/EliteArcher/UiSprites/Elite Archer Sprite Sheet.png",
            }.Select(AssetDatabase.LoadMainAssetAtPath).Where(asset => asset != null).ToList();
            BuildAtlas($"{AtlasDir}/Atlas_Characters.spriteatlasv2", true, FilterMode.Point,
                TextureImporterFormat.ASTC_4x4, padding: 4, tight: false, charPackables, log);

            // --- Atlas_Equipment : equipment rarity icon sheets (pixel-art UI, Point) ---
            var equipPackables = LoadFolders(new[]
            {
                "Assets/_Project/Art/Sprites/Equipment/SourceSheets",
            }, log);
            BuildAtlas($"{AtlasDir}/Atlas_Equipment.spriteatlasv2", true, FilterMode.Point,
                TextureImporterFormat.ASTC_4x4, padding: 4, tight: false, equipPackables, log);

            // --- Atlas_UI : shipped Layer Lab sprites (via UI dependency scan) + first-party smooth UI ---
            var uiPackables = GatherUiSprites(log);
            BuildAtlas($"{AtlasDir}/Atlas_UI.spriteatlasv2", true, FilterMode.Bilinear,
                TextureImporterFormat.ASTC_6x6, padding: 8, tight: false, uiPackables, log);

            // --- Atlas_UIPixel : 도트 UI 전용 (Point + ASTC_4x4) ---
            // 생성 아트(초상화/스킬 아이콘/마탑/링)와 PixelArtGUI2 는 픽셀 아트다.
            // Atlas_UI 의 Bilinear/ASTC_6x6 에 섞으면 보간으로 흐려지고 블록 블리딩이 난다
            // (ASTC 는 6x6 이상에서 픽셀 아트를 뭉갠다 — 픽셀 아트는 4x4 가 상한).
            var pixelPackables = GatherPixelUiSprites(log);
            BuildAtlas($"{AtlasDir}/Atlas_UIPixel.spriteatlasv2", true, FilterMode.Point,
                TextureImporterFormat.ASTC_4x4, padding: 4, tight: false, pixelPackables, log);

            var mageSources = AssetDatabase.GetDependencies("Assets/MageTower/SO/MageTowerSkillList.asset", true)
                .Where(p => p.StartsWith("Assets/_Project/Art/VFX/") && p.EndsWith(".png"))
                .Append("Assets/_Project/Art/VFX/PixelArtRPGVFX/Textures/Electricity/ElectricTornado.png")
                .Distinct().Select(AssetDatabase.LoadMainAssetAtPath).Where(a => a != null).ToList();
            BuildAtlas($"{AtlasDir}/Atlas_MageVFX.spriteatlasv2", true, FilterMode.Point,
                TextureImporterFormat.ASTC_4x4, padding: 4, tight: false, mageSources, log);
            ConfigurePreparedAtlases(log);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Pack for the active (Android) target and report sprite counts.
            var activeAtlases = AssetDatabase.FindAssets("t:SpriteAtlas", new[] { AtlasDir })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => (AssetImporter.GetAtPath(p) as SpriteAtlasImporter)?.includeInBuild == true)
                .Select(AssetDatabase.LoadAssetAtPath<SpriteAtlas>).Where(a => a != null).ToArray();
            SpriteAtlasUtility.PackAtlases(activeAtlases, EditorUserBuildSettings.activeBuildTarget);
            foreach (var name in new[] { "Atlas_Characters", "Atlas_Equipment", "Atlas_UI", "Atlas_UIPixel", "Atlas_MageVFX" })
            {
                var sa = AssetDatabase.LoadAssetAtPath<SpriteAtlas>($"{AtlasDir}/{name}.spriteatlasv2");
                log.AppendLine(sa != null
                    ? $"[PACKED] {name}: spriteCount={sa.spriteCount}"
                    : $"[PACKED] {name}: <NULL - not created!>");
            }
            Debug.Log(log.ToString());
        }

        /// <summary>
        /// 도트 UI 전용 아틀라스 수집. 생성 아트 폴더는 통째로(앞으로 추가되는 아트도 자동 포함),
        /// PixelArtGUI2 는 **UI 프리팹이 실제로 참조하는 것만** 넣는다(키트 전체는 수천 장이라 넣으면 안 된다).
        /// </summary>
        static List<Object> GatherPixelUiSprites(System.Text.StringBuilder log)
        {
            var list = new List<Object>();

            // 생성 아트 — 폴더 packable 이라 이후 추가분도 자동으로 아틀라스에 들어간다
            foreach (var f in new[] { "Assets/Generated/ComfyUI/UI", "Assets/Generated/ComfyUI/Portraits", "Assets/_Project/Art/Icons/MageTower", "Assets/UGUI/Art/Gacha" })
            {
                if (!AssetDatabase.IsValidFolder(f)) { log.AppendLine($"  [Atlas_UIPixel] folder MISSING {f}"); continue; }
                var o = AssetDatabase.LoadMainAssetAtPath(f);
                if (o != null) { list.Add(o); log.AppendLine($"  [Atlas_UIPixel] +folder {f}"); }
            }

            // PixelArtGUI2 — UI 프리팹/카탈로그 의존성에 잡힌 것만
            var roots = new List<string>();
            roots.AddRange(AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/UGUI/Prefabs" })
                .Select(AssetDatabase.GUIDToAssetPath));
            if (File.Exists("Assets/UGUI/UIViewCatalog.asset")) roots.Add("Assets/UGUI/UIViewCatalog.asset");

            var pix = AssetDatabase.GetDependencies(roots.ToArray(), true)
                .Select(d => d.Replace('\\', '/'))
                .Where(d => d.Contains("/PixelArtGUI2/") && (d.EndsWith(".png") || d.EndsWith(".Png")))
                .Distinct().OrderBy(d => d).ToList();
            foreach (var p in pix)
            {
                var o = AssetDatabase.LoadMainAssetAtPath(p);
                if (o != null) list.Add(o);
            }
            log.AppendLine($"  [Atlas_UIPixel] PixelArtGUI2 sprites referenced by UI: {pix.Count}");
            return list;
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
                .Where(d => d.StartsWith("Assets/UGUI/Art/LayerLab/")
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
            foreach (var p in new[] { "Assets/UGUI/Sprites/Circle.png", "Assets/UGUI/Sprites/RoundedRect.png", "Assets/UGUI/Sprites/CircleSoft.png" })
            {
                var o = AssetDatabase.LoadMainAssetAtPath(p);
                if (o != null) { list.Add(o); log.AppendLine($"  [Atlas_UI] +{p}"); }
            }
            // Illustrated title art is smooth key art, not a gameplay pixel sprite.
            const string lobbyArt = "Assets/UGUI/Art/Lobby";
            // Pack referenced title art only; removed decoration must not occupy the shipped atlas.
            foreach (var path in deps.Where(path => path.StartsWith(lobbyArt + "/") && path.EndsWith(".png")).Distinct())
            {
                var asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (asset != null) { list.Add(asset); log.AppendLine("  [Atlas_UI] +" + path); }
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
            // Overwrite the atlas asset in place. Deleting it first discards importer
            // settings/GUIDs and can race import workers that still hold the meta file.

            var asset = new SpriteAtlasAsset();
            asset.SetIsVariant(false);
            asset.Add(packables.ToArray());
            SpriteAtlasAsset.Save(asset, path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

            var imp = AssetImporter.GetAtPath(path) as SpriteAtlasImporter;
            if (imp == null) { log.AppendLine($"[ERR] importer null for {path}"); return; }
            imp.includeInBuild = includeInBuild;

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
            if (path.EndsWith("/Atlas_UI.spriteatlasv2"))
                imp.SetPlatformSettings(new TextureImporterPlatformSettings
                {
                    name = "iPhone", overridden = true, maxTextureSize = 2048,
                    format = androidFmt, textureCompression = TextureImporterCompression.Compressed,
                    compressionQuality = 100,
                });
            imp.SaveAndReimport();
            log.AppendLine($"[BUILT] {Path.GetFileName(path)} includeInBuild={includeInBuild} filter={filter} fmt={androidFmt} pad={padding} tight={tight} packables={packables.Count}");
        }

        // Preparation atlases remain editable. Only assets in the integrated stage groups ship.
        static void ConfigurePreparedAtlases(System.Text.StringBuilder log)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var roots = settings.groups.Where(g => g != null && (g.Name == "Stage Monsters" || g.Name == "Stage Environments"))
                .SelectMany(g => g.entries).Select(e => e.AssetPath).Where(p => !string.IsNullOrEmpty(p)).ToArray();
            var dependencies = new HashSet<string>(AssetDatabase.GetDependencies(roots, true));
            int enabled = 0, excluded = 0;
            foreach (string path in Directory.GetFiles(AtlasDir + "/Prepared", "*.spriteatlasv2", SearchOption.AllDirectories).Select(p => p.Replace('\\', '/')))
            {
                var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(path);
                var importer = AssetImporter.GetAtPath(path) as SpriteAtlasImporter;
                if (atlas == null || importer == null) continue;
                bool active = !path.Contains("/RoyalGuard/") && atlas.GetPackables().Any(p => dependencies.Contains(AssetDatabase.GetAssetPath(p)));
                if (importer.includeInBuild != active) { importer.includeInBuild = active; importer.SaveAndReimport(); }
                if (active) enabled++; else excluded++;
            }
            log.AppendLine($"[Prepared atlases] active={enabled}, excluded={excluded}; Royal Guard sheets use Atlas_Characters.");
        }
    }
}
