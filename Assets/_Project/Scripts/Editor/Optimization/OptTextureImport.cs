// Mobile art optimization — deterministic per-class Android texture import overrides.
// Fidelity-first policy (approved): pixel-art gameplay -> ASTC 4x4 (near-lossless),
// VFX -> ASTC 6x6, smooth UI -> ASTC 8x8, tiny pixel UI -> uncompressed RGBA32.
// Does NOT change filterMode / PPU / mipmaps (already correct) so on-screen look is preserved.
// Only sets the Android platform override + fixes the maxTextureSize clamp on oversized sheets.
//
// Run headless:  Unity.exe -batchmode -quit -projectPath <p> -executeMethod KingdomIdle.EditorTools.Optimization.OptTextureImport.ApplyAll -logFile <log>
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace KingdomIdle.EditorTools.Optimization
{
    public static class OptTextureImport
    {
        // First-party sprite roots to process (relative to project). LL / third-party handled elsewhere (atlas overrides).
        static readonly string[] Roots =
        {
            "Assets/_Project/Art/Sprites",
            "Assets/_Project/Prefabs/Monster/Sprite",
            "Assets/_Project/Prefabs/VFX/Sprite",
            "Assets/_Project/Prefabs/Royal_Guard_Lancer",
            "Assets/_Project/Scripts/Player/Job",
            "Assets/_Project/Scripts/Player/Equipment/Sprite",
            "Assets/MageTower/Prefabs",
            "Assets/UGUI/Sprites",
            "Assets/UGUI/UsingAssets",
        };

        // Files to never touch (app icon etc.)
        static readonly HashSet<string> SkipNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "play_store_512",
        };

        enum Cls { PixelArt, Vfx, SmoothUI, TinyUI }

        [MenuItem("KingdomIdle/Optimize/1) Apply Texture Import Overrides")]
        public static void ApplyAll()
        {
            var pngs = new List<string>();
            foreach (var root in Roots)
            {
                if (!Directory.Exists(root)) continue;
                pngs.AddRange(Directory.GetFiles(root, "*.png", SearchOption.AllDirectories)
                    .Select(p => p.Replace('\\', '/')));
            }
            pngs = pngs.Distinct().OrderBy(p => p).ToList();

            int changed = 0, skipped = 0;
            var log = new System.Text.StringBuilder();
            log.AppendLine($"[OptTextureImport] scanning {pngs.Count} first-party PNGs");

            foreach (var path in pngs)
            {
                string name = Path.GetFileNameWithoutExtension(path);
                if (SkipNames.Contains(name)) { skipped++; log.AppendLine($"SKIP(app-icon) {path}"); continue; }

                var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti == null) { skipped++; continue; }
                if (ti.textureType != TextureImporterType.Sprite) { skipped++; log.AppendLine($"SKIP(not-sprite) {path}"); continue; }

                Cls cls = Classify(path, name);
                TextureImporterFormat fmt;
                bool compressed;
                switch (cls)
                {
                    case Cls.Vfx:      fmt = TextureImporterFormat.ASTC_6x6; compressed = true;  break;
                    case Cls.SmoothUI: fmt = TextureImporterFormat.ASTC_8x8; compressed = true;  break;
                    case Cls.TinyUI:   fmt = TextureImporterFormat.RGBA32;   compressed = false; break;
                    default:           fmt = TextureImporterFormat.ASTC_4x4; compressed = true;  break; // PixelArt
                }

                // Fix the 2048 clamp that silently downscales oversized sheets (restores native pixels for clean atlas packing).
                int maxSize = 2048;
                if (name.Equals("Elite Knight Sprite Sheet", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("Attack Sprite Sheet", StringComparison.OrdinalIgnoreCase))
                    maxSize = 4096;

                var ps = ti.GetPlatformTextureSettings("Android");
                bool needsChange = !ps.overridden
                    || ps.format != fmt
                    || ps.maxTextureSize != maxSize
                    || (compressed && ps.textureCompression != TextureImporterCompression.Compressed)
                    || ps.crunchedCompression;

                ps.overridden = true;
                ps.format = fmt;
                ps.maxTextureSize = maxSize;
                ps.textureCompression = compressed ? TextureImporterCompression.Compressed : TextureImporterCompression.Uncompressed;
                ps.crunchedCompression = false;
                ps.compressionQuality = 100; // best ASTC quality (fidelity-first)

                if (needsChange)
                {
                    ti.SetPlatformTextureSettings(ps);
                    EditorUtility.SetDirty(ti);
                    ti.SaveAndReimport();
                    changed++;
                    log.AppendLine($"SET  {cls,-8} {fmt,-9} max{maxSize}  {path}");
                }
                else
                {
                    log.AppendLine($"ok   {cls,-8} {fmt,-9} max{maxSize}  {path}");
                }
            }

            log.AppendLine($"[OptTextureImport] DONE. changed={changed} skipped={skipped} total={pngs.Count}");
            Debug.Log(log.ToString());
            AssetDatabase.SaveAssets();
        }

        static Cls Classify(string path, string name)
        {
            string p = path.ToLowerInvariant();
            string n = name.ToLowerInvariant();

            // VFX / effects
            if (p.Contains("/prefabs/vfx/") || p.Contains("/magetower/prefabs/")
                || n.Contains("hit effect") || n.Contains("blast spell") || n.Contains("spell projectile")
                || n.Contains("mine explosion") || n.Contains("bear trap") || n == "net")
                return Cls.Vfx;

            // smooth (anti-aliased) UI
            if (n == "circle") return Cls.SmoothUI;

            // tiny pixel UI (<=48px) — compression not worth it, keep crisp
            if (n == "roundedrect" || n.StartsWith("dungeon_")) return Cls.TinyUI;

            // default: pixel-art gameplay (characters/enemies/equipment/backgrounds)
            return Cls.PixelArt;
        }
    }
}
