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
        static bool forestDirt;
        static int forestVariant;
        const float CombatBottom = -3.125f, CombatTop = 4.5f;
        static Rect CombatBounds => new Rect(-clear, CombatBottom, clear * 2f, CombatTop - CombatBottom);
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
                // Spawn extent ±2.5 + widest active body half-width + four native pixels.
                clear = boss ? 3.25f : 3.1875f;
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
                    item["compositionRevision"] = "2026-10-01-r2";
                    item["composition"] = family == "Forest" ? ForestThemes[variant % ForestThemes.Length] :
                        family == "Dungeon" ? DungeonThemes[variant % DungeonThemes.Length] : WasteThemes[variant % WasteThemes.Length];
                    item["centralClearBounds"] = JArray.FromObject(new[] { -clear, CombatBottom, clear, CombatTop });
                    item["layoutVariant"] = variant;
                    item["groundLayout"] = family == "Forest" ? (forestDirt ? "Dirt" : "Grass") + (HasClearing(variant, forestDirt) ? "ConnectedClearing" : "WindingRoad") : "OpenFloor";
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
        static readonly string[] DungeonThemes = { "PillarCourt", "BrokenGallery", "StoneAlcove", "QuietCrypt", "StoneRecess", "TwinPillars", "StoneGrate", "QuietGallery", "CandleRecess", "FallenColumn" };
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
            forestDirt = dirt; forestVariant = v;
            // Authored light-green path/clearing transitions, with textured forest outside.
            // Dirt and grass share the same connected footprint, not a full-width flat fill.
            Fill(dirt ? T(3, 1) : T(3, 4));
            ForestSurface(v, dirt);
            if (deep)
            {
                ground.color = details.color = new Color(.91f, .96f, .91f, 1);
                scenery.color = new Color(.95f, .98f, .95f, 1);
            }
            else if (!dirt) ground.color = details.color = new Color(.99f, 1f, .97f, 1);

            string canopy = deep ? "Willow" : dirt ? "PineLarge" : "GreenBirch";
            if (theme == 3) canopy = "GreenBirch";
            if (theme == 7) canopy = "RoundTree"; // The 6-unit oak reads as a cropped fragment here.
            if (theme == 9) canopy = deep ? "Willow" : "RoundTree";
            // Reserve the structural tree frame first. Trunks stay outside the opening;
            // diverse canopies frame its shoulders without entering the combat envelope.
            ForestFrame(v, dirt, deep, canopy);
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
                case 9: EdgeProp("Log", side, primaryY, .12f); EdgeProp("Stone", side, primaryY + 1.4f, .5f); break;
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

        static bool HasClearing(int v, bool dirt) => !dirt || v >= 10 || v % 10 == 1 || v % 10 == 3 || v % 10 == 4 || v % 10 == 7 || v % 10 == 9;

        static void ForestRow(int v, bool dirt, int y, out int left, out int right)
        {
            // Four/five-tile approaches visibly open into a seven/eight-tile clearing.
            // A small stepped shoulder retains the original 32px tile grid and connected edges.
            int bend = ((y + 72 + v * 2) / 14) % 3;
            left = bend == 0 ? -3 : -2;
            right = bend == 2 ? 2 : 1;
            if (!HasClearing(v, dirt))
            {
                right++; return;
            }
            int centre = v % 3 - 1;
            int distance = Mathf.Abs(y - centre);
            int radius = 3 + v % 2;
            if (distance <= radius - 2) { left = -4; right = 3; }
            else if (distance <= radius - 1) { left = v % 2 == 0 ? -4 : -3; right = v % 2 == 0 ? 2 : 3; }
            else if (distance <= radius) { left = -3; right = 2; }
        }

        static bool ForestCell(int v, bool dirt, int x, int y)
        {
            ForestRow(v, dirt, y, out int left, out int right);
            return x >= left && x <= right;
        }

        static void ForestSurface(int v, bool dirt)
        {
            bool Lane(int x, int y) => ForestCell(v, dirt, x, y);
            int rowOffset = dirt ? 0 : 3;
            for (int y = -24; y < 24; y++) for (int x = -6; x <= 5; x++)
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
                Put(ground, x, y, T(cx, cy + rowOffset));
            }
        }

        static void ForestFrame(int v, bool dirt, bool deep, string canopy)
        {
            string[] trees = dirt ? new[] { "PineLarge", "GreenBirch", "RoundTree", "PineSmall" } :
                deep ? new[] { "Willow", "RoundTree", "GreenBirch", "PineLarge" } :
                new[] { "GreenBirch", "RoundTree", "PineSmall", "AutumnBirch" };
            int count = HasClearing(v, dirt) ? 4 : 3;
            for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i < count; i++)
                {
                    float y = -7.25f + i * (count == 4 ? 3.8f : 5f) + (side > 0 ? .65f : 0) + (v % 3 - 1) * .25f;
                    string tree = trees[(i + v + (side > 0 ? 1 : 0)) % trees.Length];
                    EdgeProp(tree, side, y, i % 2 == 0 ? .05f : .25f);
                }
        }

        static void EdgeProp(string name, int side, float y, float inset)
        {
            Bounds bounds = Load("Prep_Prop_" + name).sprite.bounds;
            float width = bounds.size.x;
            if (clear > 2.5f) inset = Mathf.Min(inset, .125f);
            float edge = y + bounds.max.y <= CombatBottom || y + bounds.min.y >= CombatTop ? 2.1875f : clear;
            if (family == "Forest")
            {
                // A trunk must not stand in the broad clearing; allow only its outer crown
                // to overhang the native transition, always outside the protected battle area.
                for (int row = Mathf.FloorToInt(y); row <= Mathf.CeilToInt(y + bounds.size.y * .35f); row++)
                {
                    ForestRow(forestVariant, forestDirt, row, out int left, out int right);
                    edge = Mathf.Max(edge, (side < 0 ? -left : right + 1) - width * .5f + .125f);
                }
            }
            Prop(name, side * (edge + width * .5f + inset), y);
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
        static void Dungeon(int v, System.Random random)
        {
            int theme = v % 10, side = v % 2 == 0 ? -1 : 1;
            float lift = (v / 10) * .625f + (v % 3 - 1) * .625f;
            var pillars = new List<Rect>();
            void Place(string name, int edge, float y, float inset = .20f) => DungeonEdgeProp(name, edge, y, inset, pillars);
            void Pair(float y)
            {
                // A single transverse pair establishes architecture without lining the whole field.
                Place("Pillar", -1, y, .28f);
                Place("Pillar", 1, y, .28f);
            }
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
            // Source audit: the old "Crates" slice is a closed double wooden door,
            // and "Vines" is hanging fabric. Neither belongs loose on a stone floor.
            // Use complete floor-standing props, with 0-2 tall piers in the whole preset.
            switch (theme)
            {
                case 0:
                    Pair(CombatTop - Load("Prep_Prop_Pillar").sprite.bounds.min.y + .25f + (v % 3) * .1875f);
                    Place("Candles", -side, -4.5f + lift);
                    break;
                case 1:
                    Place("BrokenPillar", side, 2f + lift);
                    Place("Bench", -side, -3.3f - lift);
                    break;
                case 2:
                    Place("Bench", side, 1.8f + lift);
                    Place("Candles", side, .4f + lift, .35f);
                    Place("Tomb", -side, -3.5f - lift);
                    break;
                case 3:
                    Place("Tomb", side, 2.8f + lift);
                    Place("Candles", side, 1f + lift, .35f);
                    Place("Tomb", -side, -3.8f - lift);
                    Place("Candles", -side, -2.4f - lift, .35f);
                    break;
                case 4:
                    Place("BrokenPillar", side, 3f + lift);
                    Place("Tomb", -side, -2.8f - lift);
                    break;
                case 5:
                    Pair(CombatBottom - Load("Prep_Prop_Pillar").sprite.bounds.max.y - .375f - (v % 3) * .1875f);
                    Place("Bench", side, CombatTop + .25f + (v % 3) * .1875f);
                    Place("Candles", -side, 3.5f - lift);
                    break;
                case 6:
                    Place("Bench", side, 2.3f + lift);
                    Place("Candles", side, .8f + lift, .35f);
                    break;
                case 7:
                    Place("Bench", side, 2.4f + lift);
                    Place("Bench", -side, -3.6f - lift);
                    Place("Candles", side, .8f + lift, .35f);
                    break;
                case 8:
                    Place("Pillar", -side, 4f - lift);
                    Place("Tomb", side, 1f + lift);
                    Place("Candles", side, 2.3f + lift, .35f);
                    Place("Candles", -side, -3.8f - lift);
                    break;
                case 9:
                    Place("BrokenPillar", side, 2.8f + lift);
                    Place("Bench", -side, -3.7f - lift);
                    break;
            }
            // Wider or shifted framing continues the same restrained language without
            // adding ranks of pillars beyond the normal viewport.
            foreach (int y in new[] { -19, -11, 11, 19 })
            {
                int edge = y < 0 ? -side : side;
                if (theme == 3 || theme == 8) Place("Tomb", edge, y + lift, .3f);
                Place(theme == 2 || theme == 6 || theme == 7 ? "Bench" : "Candles", -edge, y + 2f, .3f);
            }
            if (pillars.Count > 2) throw new InvalidOperationException("Dungeon pillar budget exceeded: " + v);
            if (pillars.Count == 2 && (Mathf.Sign(pillars[0].center.x) == Mathf.Sign(pillars[1].center.x) ||
                Mathf.Abs(pillars[0].yMin - pillars[1].yMin) > .0625f ||
                Mathf.Abs(pillars[0].center.x - pillars[1].center.x) < 5f))
                throw new InvalidOperationException("Dungeon pillars must form one widely spaced opposing pair: " + v);
        }

        static void DungeonEdgeProp(string name, int side, float y, float inset, List<Rect> pillars)
        {
            Tile tile = Load("Prep_Prop_" + name);
            Rect source = tile.sprite.rect;
            // Check actual source rectangles as well as the misleading legacy names.
            // This prevents a future theme from accidentally reinstating unattached doors/banners.
            if (name == "Crates" || source == new Rect(10, 16, 48, 29))
                throw new InvalidOperationException("Dungeon double door requires supporting architecture; not a floor prop");
            if (name == "Vines" || source == new Rect(3, 64, 60, 38))
                throw new InvalidOperationException("Dungeon hanging fabric requires a backing wall; not a floor prop");
            if (name == "StoneRubble" || source == new Rect(71, 139, 19, 10))
                throw new InvalidOperationException("Legacy Dungeon StoneRubble is linked metal debris, not verified masonry");
            Bounds bounds = tile.sprite.bounds;
            if (name == "Bench" && y + bounds.max.y > CombatBottom && y + bounds.min.y < CombatTop)
            {
                // The wide slatted grate (legacy asset name "Bench") belongs in a quiet corner instead of being
                // cropped by the now wider combat lane. Keep a small authored variant offset.
                float stagger = Mathf.Repeat(Mathf.Abs(y), .625f);
                y = y < 0 ? CombatBottom - bounds.max.y - .5f - stagger :
                    CombatTop - bounds.min.y + .25f + stagger;
            }
            int before = props.Count;
            EdgeProp(name, side, y, inset);
            if (props.Count != before + 1)
                throw new InvalidOperationException($"Dungeon planned prop could not be placed: {name}, edge {side}, y {y}");
            if (name == "Pillar" || name == "BrokenPillar") pillars.Add(occupied[occupied.Count - 1]);
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

        static bool Reserved(Rect rect) => reservations.Any(r => r.Overlaps(rect)) || occupied.Any(r => r.Overlaps(rect));
        static void Reserve(Rect rect)
        {
            if (rect.Overlaps(CombatBounds)) throw new InvalidOperationException("Landmark enters combat area");
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
            if (rect.Overlaps(CombatBounds) || Reserved(rect)) return;
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
