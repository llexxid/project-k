using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace KingdomIdle.EditorTools
{
    public static class MimicPalettePreparation
    {
        private const string Source = "Assets/ExternalAssets/MainAssetBundles & Tilesets/Mimic_Animation_Pack/Sprites/";
        private const string Working = "Assets/_Project/Art/Sprites/Mimics/HeresyMimic/";
        private const string Prefab = "Assets/_Project/Prefabs/Monster/Mimics/HeresyMimic.prefab";
        private static readonly string[] Sheets =
        {
            "Idle", "Walk", "Jump", "Attackk_Bite", "Attack_Tongue",
            "Turn", "Hit", "Death", "Reveal", "Hide"
        };
        // Exact source colors: mouth, wood shadows, steel, wood, tongue, steel light, tongue light, tooth shadows/highlights.
        private static readonly Dictionary<uint, uint> Palette = new()
        {
            [0x2E1B2B] = 0x222224, [0x5F2525] = 0x4D2F24, [0x644F4F] = 0x494849,
            [0x7B7460] = 0x373D3F, [0x964234] = 0x8F4F2B, [0x9A4E66] = 0x8F3A47,
            [0xA6AA9D] = 0x9BACB1, [0xD46E76] = 0xDD6C71, [0xE79F79] = 0xBF9862,
            [0xF6CBA7] = 0xE8D8AF, [0xFFE6D1] = 0xFAF1D3
        };

        [MenuItem("KingdomIdle/Combat/Apply Mimic Palette")]
        public static void Apply()
        {
            PrepareTextures();
            var root = PrefabUtility.LoadPrefabContents(Prefab);
            try
            {
                var body = root.GetComponentInChildren<SpriteRenderer>();
                if (body == null) throw new InvalidOperationException("Mimic body is missing.");
                if (body.color != Color.white)
                {
                    body.color = Color.white;
                    PrefabUtility.SaveAsPrefabAsset(root, Prefab);
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        internal static void PrepareTextures()
        {
            foreach (string sheet in Sheets)
            {
                Texture2D source = null, current = null;
                try
                {
                    source = Decode(Source + sheet + ".png");
                    Color32[] pixels = source.GetPixels32();
                    for (int i = 0; i < pixels.Length; i++) pixels[i] = Map(pixels[i]);
                    string path = Working + sheet + ".png";
                    current = Decode(path);
                    if (source.width == current.width && source.height == current.height &&
                        pixels.SequenceEqual(current.GetPixels32())) continue;
                    source.SetPixels32(pixels); source.Apply(false, false);
                    File.WriteAllBytes(path, source.EncodeToPNG());
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                }
                finally
                {
                    if (current != null) UnityEngine.Object.DestroyImmediate(current);
                    if (source != null) UnityEngine.Object.DestroyImmediate(source);
                }
            }
        }

        public static Dictionary<string, object> Validate()
        {
            var cases = new List<string>(); var failures = new List<string>();
            void Check(bool ok, string name) { cases.Add(name); if (!ok) failures.Add(name); }
            foreach (string sheet in Sheets)
            {
                Texture2D source = null, working = null;
                try
                {
                    source = Decode(Source + sheet + ".png"); working = Decode(Working + sheet + ".png");
                    Check(source.width == working.width && source.height == working.height, sheet + " dimensions preserved");
                    var original = source.GetPixels32(); var actual = working.GetPixels32();
                    Check(original.Length == actual.Length && original.Select(Map).SequenceEqual(actual), sheet + " exact palette and alpha");
                    var importer = (TextureImporter)AssetImporter.GetAtPath(Working + sheet + ".png");
                    Check(importer != null && importer.spritePixelsPerUnit == 32 && importer.filterMode == FilterMode.Point &&
                        !importer.isReadable && !importer.mipmapEnabled, sheet + " point PPU32 non-readable without mipmaps");
                    var sprites = AssetDatabase.LoadAllAssetsAtPath(Working + sheet + ".png").OfType<Sprite>().ToArray();
                    Check(sprites.Length == source.width / 102 && sprites.All(s => s.rect.width == 102 && s.rect.height == 102), sheet + " frame grid preserved");
                }
                catch (Exception e) { Check(false, sheet + ": " + e.Message); }
                finally
                {
                    if (working != null) UnityEngine.Object.DestroyImmediate(working);
                    if (source != null) UnityEngine.Object.DestroyImmediate(source);
                }
            }
            var tongue = AssetDatabase.LoadAllAssetsAtPath(Working + "Attack_Tongue.png").OfType<Sprite>().OrderBy(s => s.name).ToArray();
            float[] feet = { 51.5f, 51, 51, 51, 56, 80.5f, 86.5f, 79, 71.5f, 62, 52, 50.5f };
            Check(tongue.Length == 12, "Twelve tongue poses retained");
            for (int i = 0; i < Mathf.Min(tongue.Length, feet.Length); i++)
                Check(Mathf.Abs(tongue[i].pivot.x - feet[i]) < .001f && Mathf.Abs(tongue[i].pivot.y) < .001f,
                    "Tongue foot pivot " + i);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
            var renderer = prefab != null ? prefab.GetComponentInChildren<SpriteRenderer>() : null;
            Check(renderer != null && renderer.color == Color.white, "Palette is not tinted brown a second time");
            Check(prefab != null && prefab.transform.localScale == Vector3.one * .9f && renderer != null &&
                renderer.transform.localScale == new Vector3(1.1875f, 1.1875f, 1), "Existing root and visual scale preserved");
            return new Dictionary<string, object> { ["passed"] = failures.Count == 0, ["count"] = cases.Count, ["cases"] = cases, ["failures"] = failures };
        }

        private static Color32 Map(Color32 pixel)
        {
            if (pixel.a == 0) return pixel;
            uint key = (uint)(pixel.r << 16 | pixel.g << 8 | pixel.b);
            if (!Palette.TryGetValue(key, out uint value))
                throw new InvalidOperationException("Unexpected source palette color: " + key.ToString("X6"));
            return new Color32((byte)(value >> 16), (byte)((value >> 8) & 255), (byte)(value & 255), pixel.a);
        }

        private static Texture2D Decode(string path)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!texture.LoadImage(File.ReadAllBytes(path), false)) throw new InvalidOperationException("Cannot decode " + path);
                return texture;
            }
            catch { UnityEngine.Object.DestroyImmediate(texture); throw; }
        }
    }
}
