using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>Authored tiles at their original 32 pixels/unit; no runtime decoration objects.</summary>
    public static class EnvironmentRichnessPreparation
    {
        const string Manifest = "Docs/ArtPreparation/Manifests/environment-presets.json";
        static readonly Dictionary<string, Tile> Tiles = new();
        static string family;
        static Tilemap ground, details, scenery;
        static float clear;
        static readonly List<Rect> reservations = new();
        static readonly List<Rect> occupied = new();
        static readonly List<object> props = new();

        public static void Prepare()
        {
            var manifest = JArray.Parse(File.ReadAllText(Manifest));
            foreach (JObject item in manifest)
            {
                string path = (string)item["path"], pool = (string)item["pool"];
                family = (string)item["family"];
                int variant = int.Parse(Path.GetFileNameWithoutExtension(path).Split('_').Last()) - 1;
                // Modes have their own arrangements, not copies with a different random seed.
                if ((string)item["mode"] == "Boss") variant += 10;
                else if ((string)item["mode"] == "Special") variant += 13;
                int seed = (int)item["seed"];
                var random = new System.Random(seed);
                bool boss = (string)item["mode"] == "Boss";
                clear = boss ? 2.625f : 2.1875f;
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    ground = root.transform.Find("Ground").GetComponent<Tilemap>();
                    details = root.transform.Find("GroundDetails").GetComponent<Tilemap>();
                    scenery = root.transform.Find("Scenery").GetComponent<Tilemap>();
                    ground.ClearAllTiles(); details.ClearAllTiles(); scenery.ClearAllTiles();
                    ground.color = details.color = scenery.color = Color.white;
                    reservations.Clear(); occupied.Clear(); props.Clear();
                    if (family == "Forest") Forest(pool, variant, random);
                    else if (family == "Dungeon") Dungeon(variant, random);
                    else Wasteland(variant, random);
                    foreach (var map in new[] { ground, details, scenery })
                    {
                        map.CompressBounds();
                        if (map.GetComponent<Collider2D>() != null) throw new InvalidOperationException("Decorative collision: " + path);
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    item["compositionRevision"] = "2026-10-01";
                    item["composition"] = family == "Forest" ? ForestThemes[variant % ForestThemes.Length] :
                        family == "Dungeon" ? DungeonThemes[variant % DungeonThemes.Length] : WasteThemes[variant % WasteThemes.Length];
                    item["centralClearBounds"] = JArray.FromObject(new[] { -clear, -7f, clear, 7f });
                    item["layoutVariant"] = variant;
                    item["tileCounts"] = JObject.FromObject(new { ground = Count(ground), details = Count(details), scenery = Count(scenery) });
                    item["scenery"] = JArray.FromObject(props);
                    item["landmarks"] = JArray.FromObject(reservations.Select(r => new[] { r.xMin, r.yMin, r.xMax, r.yMax }));
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            File.WriteAllText(Manifest, manifest.ToString());
            AssetDatabase.SaveAssets();
            Debug.Log("ENVIRONMENT RICHNESS PASSED: " + manifest.Count + " presets, same 3 batched tilemaps, central exclusion retained");
        }

        public static void BuildDevice()
        {
            Prepare();
            EquipmentFeaturePreparation.Prepare();
            KingdomIdle.EditorTools.Optimization.OptAtlases.CreateInBuildAtlases();
            TitleLobbyDeviceBuild.Build();
        }

        static readonly string[] ForestThemes = { "QuietGrove", "TimberVerge", "MossyStones", "BirchClearing", "LowStoneGarden", "MushroomMargin", "OldWall", "TreeShade", "MushroomGrove", "QuietBank" };
        static readonly string[] DungeonThemes = { "PillarCourt", "BrokenGallery", "SupplyNiche", "QuietCrypt", "VineHall", "TwinPillars", "ForgottenBench", "StoreRoom", "CandleRecess", "FallenColumn" };
        static readonly string[] WasteThemes = { "DryRiverbank", "BoneField", "AncientStumps", "RockPass", "RuinedOutpost", "BleachedGrove", "FallenTimber", "SmallOasis", "ScatteredBones", "StoneShelter" };

        static Tile T(int x, int y) => Load($"Prep_T_{x:00}_{y:00}");
        static Tile Load(string name)
        {
            string path = $"Assets/_Project/Art/Environment/{family}/Tiles/Prepared/{name}.asset";
            if (!Tiles.TryGetValue(path, out var tile))
            {
                tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
                if (tile == null || tile.sprite == null) throw new InvalidOperationException("Missing authored tile: " + path);
                Tiles[path] = tile;
            }
            return tile;
        }
        static int Count(Tilemap map) { int n = 0; foreach (var p in map.cellBounds.allPositionsWithin) if (map.HasTile(p)) n++; return n; }
        static void Put(Tilemap map, int x, int y, Tile tile) => map.SetTile(new Vector3Int(x, y), tile);
        static void Feature(float x, int y, Tile tile)
        {
            int cx = Mathf.FloorToInt(x); var p = new Vector3Int(cx,y);
            details.SetTile(p,tile); details.SetTileFlags(p,TileFlags.None);
            details.SetTransformMatrix(p,Matrix4x4.Translate(new Vector3(x-cx,0)));
        }
        static void Fill(Tile tile) { for (int y = -24; y < 24; y++) for (int x = -16; x < 16; x++) Put(ground, x, y, tile); }

        // Each view has one primary group, a lower secondary group on the opposite side,
        // and quiet corner framing. Everything uses the purchased tiles at native scale.
        static void Forest(string pool, int v, System.Random random)
        {
            bool dirt = pool == "Stage01_ForestDirt", deep = pool == "Stage03_DeepForest";
            int theme = v % 10, side = v % 2 == 0 ? -1 : 1;
            float lift = (v / 10) * .625f + (v % 3 - 1) * .75f;
            Fill(T(3, 1)); // Uniform meadow; never repeat a hedge tile across the entire field.
            if (dirt) DirtLane(v);
            else if (deep)
            {
                ground.color = details.color = new Color(.84f, .91f, .84f, 1);
                scenery.color = new Color(.91f, .96f, .91f, 1);
            }
            else ground.color = details.color = new Color(.97f, 1f, .94f, 1);

            string canopy = deep ? "Willow" : dirt ? "PineLarge" : "GreenBirch";
            if (theme == 3) canopy = "GreenBirch";
            if (theme == 7) canopy = "RoundTree"; // The 6-unit oak reads as a cropped fragment here.
            if (theme == 9) canopy = deep ? "Willow" : "RoundTree";
            float primaryY = 1.0f + lift;
            switch (theme)
            {
                case 0:
                    EdgeProp(dirt ? "PineSmall" : "GreenBirch", side, primaryY, .18f);
                    EdgeProp(canopy, side, primaryY + 2.7f, .65f);
                    break;
                case 1: EdgeProp("Log", side, primaryY, .18f); EdgeProp("Bush", side, primaryY + 1.4f, .48f); break;
                case 2: EdgeProp("StoneCluster", side, primaryY, .12f); EdgeProp("Bush", side, primaryY + 1.35f, .5f); break;
                case 3: EdgeProp("GreenBirch", side, primaryY, .2f); EdgeProp("Grass", side, primaryY - 1.25f, .35f); break;
                case 4: EdgeProp("Stone", side, primaryY, .10f); EdgeProp("StoneCluster", side, primaryY + 1.4f, .6f); break;
                case 5: MushroomMargin(side, primaryY); EdgeProp("Bush", side, primaryY + 2f, .65f); break;
                case 6: SmallWall(side, Mathf.RoundToInt(primaryY)); break;
                case 7: EdgeProp(canopy, side, primaryY + 1f, .30f); EdgeProp("Log", side, primaryY - .5f, .1f); break;
                case 8: EdgeProp("RedMushrooms", side, primaryY, .1f); EdgeProp("Log", side, primaryY + 1.3f, .35f); break;
                case 9:
                    if (!dirt) Pond(side, Mathf.RoundToInt(primaryY) - 1, deep);
                    else { EdgeProp("Log", side, primaryY, .12f); EdgeProp("Stone", side, primaryY + 1.4f, .5f); }
                    break;
            }
            // Opposite group is lower and smaller, leaving the eye a clear route through battle.
            EdgeProp(theme == 8 ? "RedMushrooms" : theme == 4 ? "Stone" : "Bush", -side, -2.6f - lift, .16f);
            EdgeProp(theme % 3 == 0 ? "Stone" : "Grass", -side, -1.25f - lift, .65f);
            EdgeProp(canopy, -side, 3.8f - lift, .50f);
            EdgeProp(deep ? "GreenBirch" : canopy, side, -5.5f + lift, .60f);
            EdgeProp("Grass", -side, -5.2f, .25f);

            // Outside the standard view, repeat the visual language with much wider spacing.
            for (int y = -22; y < 24; y += 7)
            {
                if (y > -8 && y < 8) continue;
                EdgeProp(canopy, (y / 7 + v) % 2 == 0 ? side : -side, y + lift, .6f);
                EdgeProp("Bush", (y / 7 + v) % 2 == 0 ? -side : side, y + 2.3f, .35f);
            }
            // Sparse, transparent pebbles only at the edges, never the old random flower mosaic.
            for (int i = 0; i < 18; i++)
            {
                int y = -23 + i * 47 / 18;
                int x = i % 2 == 0 ? -4 : 3;
                if (!Reserved(new Rect(x, y, 1, 1))) Put(details, x, y, T(6 + random.Next(2), 20));
            }
        }

        static void DirtLane(int v)
        {
            // One broad shallow bend per view; proper convex/concave tiles keep every seam connected.
            bool Lane(int x, int y)
            {
                int bend = ((y + 48 + v * 3) / 13) % 3;
                int left = -3 - (bend == 0 ? 1 : 0), right = 2 + (bend == 2 ? 1 : 0);
                return x >= left && x <= right;
            }
            for (int y = -24; y < 24; y++) for (int x = -5; x <= 4; x++)
            {
                if (!Lane(x, y)) continue;
                int cx = !Lane(x - 1, y) ? 5 : !Lane(x + 1, y) ? 7 : 6;
                int cy = !Lane(x, y + 1) ? 0 : !Lane(x, y - 1) ? 2 : 1;
                if (cx == 6 && cy == 1)
                {
                    if (!Lane(x - 1, y + 1)) { cx = 4; cy = 2; }
                    else if (!Lane(x + 1, y + 1)) { cx = 2; cy = 2; }
                    else if (!Lane(x - 1, y - 1)) { cx = 4; cy = 0; }
                    else if (!Lane(x + 1, y - 1)) { cx = 2; cy = 0; }
                }
                Put(ground, x, y, T(cx, cy));
            }
        }

        static void EdgeProp(string name, int side, float y, float inset)
        {
            float width = Load("Prep_Prop_" + name).sprite.bounds.size.x;
            if (clear > 2.5f) inset = Mathf.Min(inset, .125f);
            Prop(name, side * (clear + width * .5f + inset), y);
        }
        static void MushroomMargin(int side, float y)
        {
            // Complete small mushrooms, not fragments of the authored 3x3 flower-bed stamp.
            float x = side < 0 ? -clear - 1.125f : clear + .125f;
            Reserve(new Rect(x, y, 1, 1.8f));
            Feature(x, Mathf.RoundToInt(y), T(8, 19));
            Feature(x + side * .25f, Mathf.RoundToInt(y) + 1, T(7, 19));
        }
        static void SmallWall(int side, int y)
        {
            // Source columns 3 + 4 already form a closed broken wall. Column 5 starts another wall.
            float x = side < 0 ? -clear - 2.125f : clear + .125f;
            Reserve(new Rect(x, y, 2, 1));
            Feature(x, y, T(3, 24)); Feature(x + 1, y, T(4, 24));
            EdgeProp("Stone", side, y - 1.25f, .2f);
        }
        static void Pond(int side, int y, bool deep)
        {
            // Water bank belongs on grass. Its continuous border extends beyond the screen edge.
            float x = side < 0 ? -clear - 3.125f : clear + .125f;
            Patch(x, y, 3, 3, 5, 6);
            EdgeProp(deep ? "Grass" : "Stone", -side, y + 1.6f, .20f);
        }

        static void Dungeon(int v, System.Random random)
        {
            int theme = v % 10, side = v % 2 == 0 ? -1 : 1;
            float lift = (v / 10) * .625f + (v % 3 - 1) * .625f;
            // Quiet intact slabs in the centre; cracked slabs belong only beside ruined structures.
            for (int y = -24; y < 24; y++) for (int x = -16; x < 16; x++)
                Put(ground, x, y, T((x + y + 80) % 4 == 0 ? 9 : 8, 5));
            ground.color = new Color(.93f, .94f, .96f, 1);
            details.color = scenery.color = new Color(.96f, .97f, 1f, 1);
            // A complete wall section with end caps, instead of disconnected vertical wall fragments.
            for (int s = -1; s <= 1; s += 2)
            {
                int wallX = s < 0 ? -5 : 4;
                for (int y = -24; y < 24; y++)
                {
                    int band = (y + 48 + v * 2) % 12;
                    if (band < 6) Put(details, wallX, y, T(s < 0 ? 0 : 2, band == 0 ? 2 : band == 5 ? 0 : 1));
                    // Opaque stone outside the corridor, never random brown dirt squares.
                    for (int x = 1; x < 10; x++) Put(ground, wallX + s * x, y, T(8, 5));
                }
            }
            string[] primary = { "Pillar", "BrokenPillar", "Crates", "Tomb", "Vines", "Pillar", "Bench", "Crates", "Candles", "BrokenPillar" };
            string[] secondary = { "StoneRubble", "StoneRubble", "Bench", "Candles", "BrokenPillar", "Pillar", "Candles", "Crates", "Tomb", "Bench" };
            EdgeProp(primary[theme], side, .6f + lift, .22f);
            EdgeProp(secondary[theme], side, 2.9f + lift, .55f);
            EdgeProp(theme == 2 || theme == 7 ? "Crates" : "Candles", -side, -2.7f - lift, .15f);
            EdgeProp(theme == 4 ? "Vines" : "Pillar", -side, 4f - lift, .4f);
            EdgeProp(secondary[theme], side, -5f + lift, .55f);
            for (int y = -22; y < 24; y += 8)
            {
                if (y > -8 && y < 8) continue;
                EdgeProp(primary[theme], side, y + lift, .4f);
                EdgeProp(secondary[theme], -side, y + 3, .3f);
            }
            // Two cohesive rubble deposits support the feature; no independent floor-wide scatter.
            for (int i = 0; i < 2; i++)
            {
                int x = side < 0 ? -4 : 3, y = (i == 0 ? -1 : 5) + Mathf.RoundToInt(lift);
                if (!Reserved(new Rect(x, y, 1, 1))) Put(details, x, y, T(4 + (theme + i) % 3, 4));
            }
        }

        static void Wasteland(int v, System.Random random)
        {
            int theme = v % 10, side = v % 2 == 0 ? -1 : 1;
            float lift = (v / 10) * .625f + (v % 3 - 1) * .75f;
            Fill(T(4, 1)); // Intact sand in the battle lane; random cracked tiles caused visible seams.
            ground.color = details.color = new Color(.95f, .91f, .88f, 1);
            scenery.color = new Color(.97f, .95f, .91f, 1);
            for (int y = -24; y < 24; y++)
            {
                // Authored fading crack edges meet a calm central strip.
                Put(ground, -4, y, T(3, 5)); Put(ground, 3, y, T(5, 5));
                for (int x = 4; x < 16; x++) { Put(ground, x, y, T(1, 4)); Put(ground, -x - 1, y, T(1, 4)); }
            }
            string[] main = { "Rock", "Ribcage", "GiantStump", "LargeBoulder", "FallenLog", "BleachedTree", "FallenLog", "DryGrass", "Skull", "Boulder" };
            string[] companion = { "DryGrass", "Skull", "DryGrass", "Rock", "Rock", "DryGrass", "GiantStump", "Rock", "Ribcage", "DryGrass" };
            EdgeProp(main[theme], side, .7f + lift, .15f);
            EdgeProp(companion[theme], side, 2.7f + lift, .6f);
            if (theme == 4)
            {
                float x = side < 0 ? -clear - 2.125f : clear + .125f;
                Reserve(new Rect(x, -1 + lift, 2, 1));
                Feature(x, Mathf.RoundToInt(-1 + lift), T(9, 14)); Feature(x + 1, Mathf.RoundToInt(-1 + lift), T(10, 14));
            }
            EdgeProp(theme == 1 || theme == 8 ? "Skull" : "DryGrass", -side, -2.7f - lift, .15f);
            EdgeProp(theme == 3 || theme == 9 ? "Boulder" : "CrookedTree", -side, 4f - lift, .5f);
            EdgeProp(theme == 5 ? "BleachedTree" : "DryTree", side, -5.4f + lift, .6f);
            for (int y = -22; y < 24; y += 8)
            {
                if (y > -8 && y < 8) continue;
                EdgeProp(main[theme], side, y + lift, .4f);
                EdgeProp("DryGrass", -side, y + 3, .3f);
            }
        }

        static void Patch(float x, int y, int width, int height, int col, int row)
        {
            var rect = new Rect(x, y, width, height); Reserve(rect);
            for (int yy = 0; yy < height; yy++) for (int xx = 0; xx < width; xx++)
                Feature(x + xx, y + yy, T(col + (xx == 0 ? 0 : xx == width - 1 ? 2 : 1), row + (yy == 0 ? 2 : yy == height - 1 ? 0 : 1)));
        }
        static bool Reserved(Rect rect) => reservations.Any(r => r.Overlaps(rect)) || occupied.Any(r => r.Overlaps(rect));
        static void Reserve(Rect rect)
        {
            if (rect.xMin < clear && rect.xMax > -clear) throw new InvalidOperationException("Landmark enters combat lane");
            reservations.Add(rect);
        }
        static void Prop(string name, float x, float y, bool landmark = false)
        {
            var tile = Load("Prep_Prop_" + name); var bounds = tile.sprite.bounds;
            // Alpha-trimmed sprites may have an odd pixel width; align their visible edge,
            // not the half-pixel centre, to the same 32 PPU grid as the ground.
            x = Mathf.Round((x + bounds.min.x) * 32) / 32 - bounds.min.x;
            y = Mathf.Round((y + bounds.min.y) * 32) / 32 - bounds.min.y;
            var rect = new Rect(x + bounds.min.x, y + bounds.min.y, bounds.size.x, bounds.size.y);
            if (rect.xMin < clear && rect.xMax > -clear || Reserved(rect)) return;
            int cx = Mathf.FloorToInt(x), cy = Mathf.FloorToInt(y);
            var cell = new Vector3Int(cx, cy); if (scenery.HasTile(cell)) return;
            scenery.SetTile(cell, tile); scenery.SetTileFlags(cell, TileFlags.None);
            scenery.SetTransformMatrix(cell, Matrix4x4.Translate(new Vector3(x - cx - .5f, y - cy - .5f)));
            props.Add(new { prop = name, cell = new[] { cx, cy }, bounds = new[] { rect.xMin, rect.xMax, rect.yMin, rect.yMax }, offsetX = x - cx - .5f });
            // Every prop reserves its footprint, including ordinary trees and small accents.
            // A native four-pixel gap prevents accidental tangencies at the field edge.
            occupied.Add(new Rect(rect.xMin - .125f, rect.yMin - .125f, rect.width + .25f, rect.height + .25f));
            if (landmark) reservations.Add(rect);
        }
    }
}
