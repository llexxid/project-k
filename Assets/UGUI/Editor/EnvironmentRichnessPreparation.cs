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
        static readonly List<object> props = new();

        public static void Prepare()
        {
            var manifest = JArray.Parse(File.ReadAllText(Manifest));
            foreach (JObject item in manifest)
            {
                string path = (string)item["path"], pool = (string)item["pool"];
                family = (string)item["family"];
                int variant = int.Parse(Path.GetFileNameWithoutExtension(path).Split('_').Last()) - 1;
                if (pool == "Stage02_ForestGrass") variant = (variant + 3) % 10;
                else if (pool == "Stage03_DeepForest") variant = (variant + 6) % 10;
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
                    reservations.Clear(); props.Clear();
                    if (family == "Forest") Forest(pool, variant, random);
                    else if (family == "Dungeon") Dungeon(variant, random);
                    else Wasteland(variant, random);
                    foreach (var map in new[] { ground, details, scenery })
                    {
                        map.CompressBounds();
                        if (map.GetComponent<Collider2D>() != null) throw new InvalidOperationException("Decorative collision: " + path);
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    item["compositionRevision"] = "2026-09-20";
                    item["composition"] = family == "Forest" ? (pool == "Stage01_ForestDirt" && (variant % 5 == 0 || variant % 5 == 3) ? "TimberAndRuins" : ForestThemes[variant % ForestThemes.Length]) :
                        family == "Dungeon" ? DungeonThemes[variant % DungeonThemes.Length] : WasteThemes[variant % WasteThemes.Length];
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

        static readonly string[] ForestThemes = { "WatersideBend", "MeadowAndTimber", "MossyRuins", "BirchPond", "StoneGarden", "FlowerGrove", "BrokenWall", "OldOakClearing", "MushroomBank", "WoodlandRemains" };
        static readonly string[] DungeonThemes = { "PillaredVault", "BrokenGallery", "SupplyAlcove", "QuietCrypt", "VineHall" };
        static readonly string[] WasteThemes = { "DryRiverbank", "BoneField", "AncientStumps", "RockPass", "RuinedOutpost" };

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

        static void Forest(string pool, int v, System.Random random)
        {
            bool dirt = pool == "Stage01_ForestDirt", deep = pool == "Stage03_DeepForest";
            Fill(T(3, dirt ? 1 : 4));
            if (deep) ground.color = details.color = new Color(.83f,.93f,.79f,1);
            // A broad readable lane with modest asymmetric bays, never a narrow combat bottleneck.
            bool Lane(int x, int y) => x >= -3 - ((y + 60 + v * 2) / 12 % 3 == 0 ? 1 : 0) && x <= 2 + ((y + 60 + v) / 14 % 3 == 1 ? 1 : 0);
            for (int y = -24; y < 24; y++)
            {
                for (int x = -5; x <= 4; x++)
                {
                    if (!Lane(x,y)) continue;
                    int cx = !Lane(x-1,y)?5:!Lane(x+1,y)?7:6;
                    int cy = !Lane(x,y+1)?0:!Lane(x,y-1)?2:1;
                    if (cx==6 && cy==1)
                    {
                        if(!Lane(x-1,y+1)){cx=4;cy=2;} else if(!Lane(x+1,y+1)){cx=2;cy=2;}
                        else if(!Lane(x-1,y-1)){cx=4;cy=0;} else if(!Lane(x+1,y-1)){cx=2;cy=0;}
                    }
                    Put(ground,x,y,T(cx,cy+(dirt?0:3)));
                }
            }
            int side = v % 2 == 0 ? -1 : 1;
            float bank = Mathf.Ceil((clear+.04f)*4)/4;
            float anchorX = side < 0 ? -bank-3 : bank;
            int anchorY = v % 3 == 0 ? 1 : -2;
            if (dirt && (v % 5 == 0 || v % 5 == 3))
                Ruin(anchorX, anchorY, false); // Water's authored grass backing does not belong on bare dirt.
            else if (v % 5 == 0 || v % 5 == 3)
                Patch(anchorX, anchorY, 3, 4, 5, 6); // Authored water corners, banks, center.
            else if (v % 5 == 2 || v % 5 == 1 && deep)
                Ruin(anchorX, anchorY, false);
            else if (v % 5 == 1 || v % 5 == 4)
                Flowers(anchorX, anchorY);

            if (v == 6 || v == 9) Ruin(side > 0 ? -bank-3 : bank, 4, false);
            else if (v == 7) Prop("AncientOak", side * (clear + 3.05f), 1.6f, true);
            string[] trees = deep ? new[] { "Willow", "GreenBirch", "Bush", "AncientOak" } : !dirt ? new[] { "GreenBirch", "AutumnBirch", "RoundTree", "Bush" } :
                v % 3 == 1 ? new[] { "GreenBirch", "AutumnBirch", "RoundTree" } : new[] { "PineLarge", "PineSmall", "RoundTree" };
            for (int s = -1; s <= 1; s += 2)
            {
                float y = -23 + (float)random.NextDouble() * 3;
                while (y < 24)
                {
                    string name = trees[random.Next(trees.Length)];
                    float w = Load("Prep_Prop_" + name).sprite.bounds.size.x;
                    Prop(name, s * (clear + w * .5f + .05f + (float)random.NextDouble() * .25f), y);
                    y += 3.2f + (float)random.NextDouble() * 2.3f;
                }
            }
            for (int i = 0; i < 105; i++)
            {
                int x = random.Next(-9, 9), y = random.Next(-24, 24);
                if (Math.Abs(x + .5f) <= clear + .35f || Reserved(new Rect(x, y, 1, 1))) continue;
                var tile = i % 5 == 0 ? T(6 + random.Next(3), 16) : i % 3 == 0 ? T(7 + random.Next(4), 19) : T(6 + random.Next(4), 20);
                Put(details, x, y, tile);
            }
            for (int i = 0; i < 13; i++)
                Prop(i % 3 == 0 ? "Log" : i % 3 == 1 ? "StoneCluster" : "Bush", (i % 2 == 0 ? -1 : 1) * (clear + .55f + (float)random.NextDouble() * .35f), -20 + i * 3.4f);
            // Small transparent ground marks break up flat lanes without obscuring combat silhouettes.
            for (int y = -24; y < 24; y++) for (int x = -2; x <= 1; x++)
                if (random.Next(18) == 0 && !details.HasTile(new Vector3Int(x,y)))
                    Put(details, x, y, dirt ? T(20,random.Next(4)) : T(19,6));
        }

        static void Dungeon(int v, System.Random random)
        {
            for (int y = -24; y < 24; y++) for (int x = -16; x < 16; x++)
                Put(ground, x, y, random.Next(15) == 0 ? T(12, 5) : T(8 + random.Next(4), 5)); // Column 7 is a transparent wall edge, not a floor.
            // Broken wall bays and darker side floors give each hall a silhouette, while all walls stay outside combat.
            for (int s = -1; s <= 1; s += 2)
            {
                int edge = s < 0 ? -5 : 4;
                for (int y = -24; y < 24; y++)
                {
                    if ((y + 30 + v * 2) % 9 < 5) Put(details, edge, y, T(s < 0 ? 0 : 2, 1));
                    for (int x = 0; x < 4; x++) Put(ground, edge + s * (x + 1), y, T(11 + v % 3, 10));
                }
            }
            string[][] sets = { new[] { "Pillar", "BrokenPillar", "StoneRubble" }, new[] { "BrokenPillar", "Bench", "StoneRubble" },
                new[] { "Crates", "Bench", "StoneRubble" }, new[] { "Tomb", "Candles", "BrokenPillar" }, new[] { "Vines", "Pillar", "StoneRubble" } };
            var chosen = sets[v % sets.Length];
            for (int i = 0; i < 24; i++)
            {
                string name = chosen[(i + v) % chosen.Length]; float w = Load("Prep_Prop_" + name).sprite.bounds.size.x;
                Prop(name, (i % 2 == 0 ? -1 : 1) * (clear + w * .5f + .3f), -22 + (i / 2) * 4 + (i % 2) * 1.8f);
            }
            for (int i = 0; i < 55; i++)
            {
                int x = random.Next(-7, 7), y = random.Next(-24, 24);
                if (Math.Abs(x + .5f) > clear + .3f) Put(details, x, y, T(4 + random.Next(3), 4));
            }
        }

        static void Wasteland(int v, System.Random random)
        {
            for (int y = -24; y < 24; y++) for (int x = -16; x < 16; x++)
                Put(ground, x, y, T(random.Next(4), 4 + random.Next(3)));
            int side = v % 2 == 0 ? -1 : 1;
            float bank = Mathf.Ceil((clear+.04f)*4)/4;
            float x0 = side < 0 ? -bank-3 : bank;
            if (v % 5 == 0) Patch(x0, 1, 3, 4, 0, 7); // Authored toxic pool is a distant landmark, never a combat hazard.
            else if (v % 5 == 4) Ruin(x0, 1, true);
            else Prop(v % 5 == 1 ? "Ribcage" : v % 5 == 2 ? "GiantStump" : "LargeBoulder", side * (clear + 1.6f), 1, true);
            string[] set = v % 5 == 1 ? new[] { "Ribcage", "Skull", "Rock" } : v % 5 == 3 ? new[] { "LargeBoulder", "Boulder", "Rock" } :
                new[] { "DryTree", "CrookedTree", "BleachedTree", "GiantStump", "DryGrass" };
            for (int i = 0; i < 24; i++)
            {
                string name = set[random.Next(set.Length)]; float w = Load("Prep_Prop_" + name).sprite.bounds.size.x;
                Prop(name, (i % 2 == 0 ? -1 : 1) * (clear + w * .5f + .15f + (float)random.NextDouble()), -23 + (i / 2) * 4 + (i % 2));
            }
            for (int i = 0; i < 75; i++)
            {
                int x = random.Next(-9, 9), y = random.Next(-24, 24);
                if (Math.Abs(x + .5f) > clear + .3f && !Reserved(new Rect(x, y, 1, 1))) Put(details, x, y, T(3 + random.Next(6), 12));
            }
        }

        static void Patch(float x, int y, int width, int height, int col, int row)
        {
            var rect = new Rect(x, y, width, height); Reserve(rect);
            for (int yy = 0; yy < height; yy++) for (int xx = 0; xx < width; xx++)
                Feature(x + xx, y + yy, T(col + (xx == 0 ? 0 : xx == width - 1 ? 2 : 1), row + (yy == 0 ? 2 : yy == height - 1 ? 0 : 1)));
        }
        static void Flowers(float x, int y)
        {
            Reserve(new Rect(x, y, 3, 3));
            for (int yy = 0; yy < 3; yy++) for (int xx = 0; xx < 3; xx++) Feature(x + xx, y + yy, T(6 + xx, 18 - yy));
        }
        static void Ruin(float x, int y, bool waste)
        {
            Reserve(new Rect(x, y, 3, 3));
            if (waste)
            {
                Feature(x,y+2,T(9,14)); Feature(x+1,y+2,T(10,14)); Feature(x+2,y+2,T(12,14));
                Feature(x,y+1,T(9,15)); Feature(x+2,y+1,T(12,15)); Feature(x+1,y,T(11,16));
            }
            else
            {
                Feature(x,y+2,T(3,24)); Feature(x+1,y+2,T(4,24)); Feature(x+2,y+2,T(5,24));
                Feature(x,y+1,T(3,25)); Feature(x+2,y+1,T(6,25)); Feature(x,y,T(3,26));
                Feature(x+1,y,T(4,26)); Feature(x+2,y,T(6,26));
            }
        }
        static bool Reserved(Rect rect) => reservations.Any(r => r.Overlaps(rect));
        static void Reserve(Rect rect)
        {
            if (rect.xMin < clear && rect.xMax > -clear) throw new InvalidOperationException("Landmark enters combat lane");
            reservations.Add(rect);
        }
        static void Prop(string name, float x, float y, bool landmark = false)
        {
            var tile = Load("Prep_Prop_" + name); var bounds = tile.sprite.bounds;
            x = Mathf.Round(x * 32) / 32; y = Mathf.Round(y * 32) / 32;
            var rect = new Rect(x + bounds.min.x, y + bounds.min.y, bounds.size.x, bounds.size.y);
            if (rect.xMin < clear && rect.xMax > -clear || Reserved(rect)) return;
            int cx = Mathf.FloorToInt(x), cy = Mathf.FloorToInt(y);
            var cell = new Vector3Int(cx, cy); if (scenery.HasTile(cell)) return;
            scenery.SetTile(cell, tile); scenery.SetTileFlags(cell, TileFlags.None);
            scenery.SetTransformMatrix(cell, Matrix4x4.Translate(new Vector3(x - cx - .5f, y - cy - .5f)));
            props.Add(new { prop = name, cell = new[] { cx, cy }, bounds = new[] { rect.xMin, rect.xMax, rect.yMin, rect.yMax }, offsetX = x - cx - .5f });
            if (landmark) reservations.Add(rect);
        }
    }
}
