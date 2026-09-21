using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KingdomIdle.MageTower;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

namespace KingdomIdle.EditorTools.Optimization
{
    /// <summary>Scoped, nonvisual cleanup. Does not rebuild skills, prefabs or shared UI atlases.</summary>
    public static class MageSpriteAudit
    {
        const string Registry = "Assets/MageTower/SO/MageTowerSkillList.asset";
        const string Status = "Assets/_Project/Resources/CombatStatusArt.asset";
        const string AtlasRoot = "Assets/_Project/Art/Atlases/";
        const string Report = "Docs/ArtPreparation/Validation/MageSprite20260921/audit.json";

        [MenuItem("KingdomIdle/Optimize/Audit Mage Sprites")]
        public static void Audit() => Run(false);

        [MenuItem("KingdomIdle/Optimize/Remove Unused Mage Sprite Physics")]
        public static void Optimize() => Run(true);

        static void Run(bool optimize)
        {
            var errors = new List<string>();
            var rows = new List<object>();
            var dependencies = AssetDatabase.GetDependencies(new[] { Registry, Status }, true);
            var registry = AssetDatabase.LoadAssetAtPath<MageTowerSkillRegistrySO>(Registry);
            if (registry == null || !MageSkillRules.ValidateRoster(registry.skills)) errors.Add("Invalid live mage roster");
            foreach (var path in dependencies.Where(p => p.EndsWith(".prefab")))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                foreach (var t in prefab.GetComponentsInChildren<Transform>(true))
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) > 0)
                        errors.Add("Missing script: " + path + "/" + t.name);
                if (prefab.GetComponentsInChildren<PolygonCollider2D>(true).Length > 0)
                    errors.Add("Sprite physics consumer requires review: " + path);
            }
            foreach (var path in dependencies.Where(p => p.EndsWith(".anim")))
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                {
                    var keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                    // The approved generator ends one-shot clips with an explicit null sprite.
                    for (int i = 0; i < keys.Length; i++)
                        if (binding.type == typeof(SpriteRenderer) && keys[i].value == null &&
                            (i != keys.Length - 1 || clip.isLooping))
                            errors.Add("Missing animation sprite: " + path);
                }
            }
            if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));

            var atlases = new[] { "Atlas_MageVFX", "Atlas_UIPixel" }
                .Select(n => AssetDatabase.LoadAssetAtPath<SpriteAtlas>(AtlasRoot + n + ".spriteatlasv2")).ToArray();
            int removedPoints = 0, changed = 0;
            foreach (var path in dependencies.Where(p => p.EndsWith(".png") && p.StartsWith("Assets/_Project/Art/")).OrderBy(p => p))
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
                var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s => s.name).ToArray();
                string before = RenderFingerprint(sprites);
                int physicsBefore = PhysicsPoints(sprites);
                bool originalFallback = settings.spriteGenerateFallbackPhysicsShape;
                if (optimize && originalFallback)
                {
                    settings.spriteGenerateFallbackPhysicsShape = false;
                    importer.SetTextureSettings(settings); importer.SaveAndReimport();
                    sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s => s.name).ToArray();
                    if (before != RenderFingerprint(sprites))
                    {
                        settings.spriteGenerateFallbackPhysicsShape = originalFallback;
                        importer.SetTextureSettings(settings); importer.SaveAndReimport();
                        throw new InvalidOperationException("Render data changed; restored importer: " + path);
                    }
                    changed++;
                }
                int physicsAfter = PhysicsPoints(sprites); removedPoints += physicsBefore - physicsAfter;
                bool packed = sprites.Length > 0 && sprites.All(s => atlases.Any(a => a != null && a.CanBindTo(s)));
                if (!packed || importer.isReadable || importer.mipmapEnabled || importer.filterMode != FilterMode.Point)
                    errors.Add("Sprite optimization incomplete: " + path);
                importer.GetSourceTextureWidthAndHeight(out int width, out int height);
                rows.Add(new { path, width, height, sprites = sprites.Length, packed,
                    readable = importer.isReadable, mipmaps = importer.mipmapEnabled, filter = importer.filterMode.ToString(),
                    physicsBefore, physicsAfter, originalFallback, renderDataUnchanged = before == RenderFingerprint(sprites) });
            }
            var atlasRows = new List<object>();
            foreach (var atlas in atlases)
            {
                if (atlas == null) { errors.Add("Missing atlas"); continue; }
                var importer = (SpriteAtlasImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(atlas));
                var platform = importer.GetPlatformSettings("Android");
                var texture = importer.textureSettings;
                if (!importer.includeInBuild || !platform.overridden || platform.format != TextureImporterFormat.ASTC_4x4 ||
                    platform.maxTextureSize > 2048 || texture.readable || texture.generateMipMaps || texture.filterMode != FilterMode.Point)
                    errors.Add("Atlas settings require review: " + atlas.name);
                atlasRows.Add(new { atlas.name, importer.includeInBuild, format = platform.format.ToString(), platform.maxTextureSize,
                    readable = texture.readable, mipmaps = texture.generateMipMaps, filter = texture.filterMode.ToString() });
            }
            Directory.CreateDirectory(Path.GetDirectoryName(Report));
            File.WriteAllText(Report, JsonConvert.SerializeObject(new { unity = Application.unityVersion,
                optimized = optimize, changedTextures = changed, removedPhysicsPoints = removedPoints,
                dependencies = dependencies.Length, textures = rows, atlases = atlasRows, errors,
                deviceTested = false, playModeTested = false }, Formatting.Indented));
            if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));
            Debug.Log($"[MageSpriteAudit] PASS textures={rows.Count}, changed={changed}, removedPhysicsPoints={removedPoints}");
        }

        static int PhysicsPoints(IEnumerable<Sprite> sprites)
            => sprites.Sum(s => Enumerable.Range(0, s.GetPhysicsShapeCount()).Sum(s.GetPhysicsShapePointCount));

        static string RenderFingerprint(IEnumerable<Sprite> sprites)
            => JsonConvert.SerializeObject(sprites.Select(s => {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(s, out string guid, out long id);
                return new { guid, id, s.name, rect = new[] { s.rect.x, s.rect.y, s.rect.width, s.rect.height },
                    pivot = new[] { s.pivot.x, s.pivot.y }, border = new[] { s.border.x, s.border.y, s.border.z, s.border.w },
                    s.pixelsPerUnit, vertices = s.vertices.Select(v => new[] { v.x, v.y }),
                    uv = s.uv.Select(v => new[] { v.x, v.y }), triangles = s.triangles };
            }));
    }
}
