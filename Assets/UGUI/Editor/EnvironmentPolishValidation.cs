using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>Actual prefab renders in an isolated preview scene, with the shipped gameplay camera framing.</summary>
    public static class EnvironmentPolishValidation
    {
        const string Manifest = "Docs/ArtPreparation/Manifests/environment-presets.json";
        const string MainScene = "Assets/_Project/Scenes/buildScenes/main.unity";
        static string Output => Environment.GetEnvironmentVariable("ENVIRONMENT_POLISH_OUTPUT") ?? "Docs/ArtPreparation/Validation/EnvironmentPolish20261001/revision2";
        static readonly string[] Pools = { "Stage01_ForestDirt", "Stage02_ForestGrass", "Stage03_DeepForest", "GoldDungeon", "RubyWasteland" };
        static readonly string[] Modes = { "Main", "Boss", "Special" };
        static readonly (string name, int width, int height)[] ExtraCases =
        {
            ("narrow", 360, 800), ("tall", 432, 1008), ("tablet", 720, 960)
        };

        public static void CaptureBefore() => Capture("before");
        public static void CaptureAfter() => Capture("after");
        public static void PrepareAndCaptureAfter()
        {
            EnvironmentRichnessPreparation.Prepare();
            CaptureAfter();
        }

        static void Capture(string phase)
        {
            var timer = Stopwatch.StartNew();
            string directory = Path.Combine(Output, phase);
            Directory.CreateDirectory(directory);
            var entries = JArray.Parse(File.ReadAllText(Manifest)).Cast<JObject>()
                .OrderBy(x => Array.IndexOf(Pools, (string)x["pool"]))
                .ThenBy(x => Array.IndexOf(Modes, (string)x["mode"]))
                .ThenBy(x => (string)x["name"], StringComparer.Ordinal).ToArray();
            if (entries.Length != 90 || entries.Any(x => !Pools.Contains((string)x["pool"]) || !Modes.Contains((string)x["mode"])))
                throw new InvalidOperationException("Expected the established 5 pools × 18 presets; review preview numbering before continuing.");

            CameraSpec spec = ReadCameraSpec();
            var results = new JArray();
            var scene = EditorSceneManager.NewPreviewScene();
            RenderTexture previousTarget = RenderTexture.active;
            try
            {
                var cameraObject = new GameObject("EnvironmentPolishPreviewCamera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
                Camera camera = cameraObject.GetComponent<Camera>();
                camera.scene = scene;
                camera.enabled = false;
                camera.transform.SetPositionAndRotation(spec.position, spec.rotation);
                camera.orthographic = spec.orthographic;
                camera.orthographicSize = spec.orthographicSize;
                camera.fieldOfView = spec.fieldOfView;
                camera.nearClipPlane = spec.nearClip;
                camera.farClipPlane = spec.farClip;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.allowHDR = false;
                camera.allowMSAA = false;

                for (int i = 0; i < entries.Length; i++)
                {
                    JObject entry = entries[i];
                    string path = (string)entry["path"];
                    var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (asset == null) throw new FileNotFoundException("Missing environment prefab", path);
                    var root = (GameObject)PrefabUtility.InstantiatePrefab(asset, scene);
                    try
                    {
                        // Environment assets have no gameplay scripts; never run preview-only simulation.
                        foreach (MonoBehaviour script in root.GetComponentsInChildren<MonoBehaviour>(true))
                            if (script != null) script.enabled = false;
                        foreach (Tilemap map in root.GetComponentsInChildren<Tilemap>(true)) map.RefreshAllTiles();
                        JObject result = Inspect(root, entry, i + 1);
                        string filename = $"{i + 1:00}_{entry["name"]}_540x960.png";
                        JObject primary = Render(camera, root, Path.Combine(directory, filename), 540, 960);
                        result["image"] = filename;
                        result["capture"] = primary;
                        var otherRatios = new JArray();
                        // One Main/Boss/Special per family gives every composition mode aspect coverage.
                        if (((string)entry["name"]).EndsWith("_01", StringComparison.Ordinal))
                            foreach (var item in ExtraCases)
                            {
                                string extra = $"{i + 1:00}_{entry["name"]}_{item.name}_{item.width}x{item.height}.png";
                                JObject capture = Render(camera, root, Path.Combine(directory, extra), item.width, item.height);
                                capture["name"] = item.name; capture["image"] = extra;
                                otherRatios.Add(capture);
                            }
                        result["aspectChecks"] = otherRatios;
                        results.Add(result);
                    }
                    finally { Object.DestroyImmediate(root); }
                    if ((i + 1) % 18 == 0) Debug.Log($"ENVIRONMENT PREVIEW {phase}: {i + 1}/90");
                }
            }
            finally
            {
                RenderTexture.active = previousTarget;
                EditorSceneManager.ClosePreviewScene(scene);
            }

            var duplicateLayoutGroups = DuplicateGroups(results, x => (string)x["layoutSha256"]);
            var duplicateImageGroups = DuplicateGroups(results, x => (string)x["capture"]["pixelSha256"]);
            bool valid = results.All(x => !(bool)x["hasMissingReferences"] && (int)x["colliderCount"] == 0 &&
                (int)x["centralSceneryIntrusions"] == 0 && (int)x["sceneryOverlapPairs"] == 0 && (int)x["tilemapCount"] == 3 &&
                (int)x["waterTileCount"] == 0 && (int)x["prohibitedDoorOrHangingVineCount"] == 0 && (bool)x["pillarArrangementValid"] &&
                (int)x["capture"]["groundHolesInViewport"] == 0 && (bool)x["capture"]["nonBlank"] &&
                x["aspectChecks"].All(a => (int)a["groundHolesInViewport"] == 0 && (bool)a["nonBlank"])) &&
                duplicateLayoutGroups.Count == 0 && duplicateImageGroups.Count == 0;
            var report = new JObject
            {
                ["phase"] = phase, ["valid"] = valid, ["presetCount"] = entries.Length,
                ["captureCount"] = results.Sum(x => 1 + x["aspectChecks"].Count()),
                ["camera"] = spec.ToJson(), ["durationSeconds"] = timer.Elapsed.TotalSeconds,
                ["renderingDevice"] = SystemInfo.graphicsDeviceName,
                ["unityVersion"] = Application.unityVersion,
                ["scope"] = "Static Unity PreviewScene rendering of actual saved prefabs. No HUD, input, gameplay, safe-area or physical-device claim.",
                ["numbering"] = "Pools: Stage01, Stage02, Stage03, GoldDungeon, RubyWasteland; Main 10, Boss 3, Special 5 in each; lightning icon reserved 91.",
                ["duplicateLayouts"] = duplicateLayoutGroups, ["duplicateImages"] = duplicateImageGroups,
                ["results"] = results
            };
            File.WriteAllText(Path.Combine(directory, "report.json"), report.ToString(Formatting.Indented), new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(directory, "index.html"), Gallery(results, phase), new UTF8Encoding(false));
            Debug.Log($"ENVIRONMENT POLISH {phase.ToUpperInvariant()}: {entries.Length} prefabs, {report["captureCount"]} real renders, valid={valid}; {Path.GetFullPath(directory)}");
            // A before capture records pre-existing defects; an after capture is an acceptance gate.
            if (phase == "after" && !valid) throw new InvalidOperationException("Environment validation failed; inspect report.json for exact preset numbers.");
        }

        static JObject Inspect(GameObject root, JObject entry, int number)
        {
            Tilemap[] maps = root.GetComponentsInChildren<Tilemap>(true).OrderBy(x => x.name, StringComparer.Ordinal).ToArray();
            var missing = new List<string>();
            var references = new HashSet<Object>();
            var layout = new StringBuilder();
            var counts = new JObject();
            var intrusions = new JArray();
            var boundsReport = new JArray();
            var waterTiles = new JArray();
            var prohibitedTiles = new JArray();
            int visibleTallPillars = 0;
            var pillars = new List<Rect>();
            float clear = (string)entry["mode"] == "Boss" ? 2.625f : 2.1875f;
            Rect protectedArea = Rect.MinMaxRect(-clear, -7, clear, 7);
            if (entry["centralClearBounds"] is JArray clearBounds && clearBounds.Count == 4) clear = (float)clearBounds[2];
            if (entry["centralClearBounds"] is JArray area && area.Count == 4)
                protectedArea = Rect.MinMaxRect((float)area[0], (float)area[1], (float)area[2], (float)area[3]);
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                foreach (Component component in transform.GetComponents<Component>())
                    if (component == null) missing.Add(transform.name + ": missing script");
                    else CheckReferences(component, missing);
            }
            foreach (Tilemap map in maps)
            {
                int count = 0;
                layout.Append(map.name).Append('|').Append(map.color.ToString("F5")).AppendLine();
                foreach (Vector3Int cell in map.cellBounds.allPositionsWithin)
                {
                    TileBase tile = map.GetTile(cell);
                    if (tile == null) continue;
                    count++;
                    Match coordinates = Regex.Match(tile.name, @"^Prep_T_(\d+)_(\d+)$");
                    if (coordinates.Success)
                    {
                        int column = int.Parse(coordinates.Groups[1].Value), row = int.Parse(coordinates.Groups[2].Value);
                        bool water = (string)entry["family"] == "Forest" && column >= 5 && column <= 7 && row >= 6 && row <= 8 ||
                            (string)entry["family"] == "Wasteland" && column >= 0 && column <= 2 && row >= 7 && row <= 9;
                        if (water) waterTiles.Add(map.name + "/" + cell + "/" + tile.name);
                    }
                    if ((string)entry["family"] == "Dungeon" && (tile.name == "Prep_Prop_Crates" || tile.name == "Prep_Prop_Vines" || tile.name == "Prep_Prop_StoneRubble"))
                        prohibitedTiles.Add(map.name + "/" + cell + "/" + tile.name);
                    Sprite sprite = map.GetSprite(cell);
                    if (sprite == null) missing.Add(map.name + "/" + cell + ": missing sprite");
                    if (references.Add(tile)) CheckReferences(tile, missing);
                    Matrix4x4 matrix = map.GetTransformMatrix(cell);
                    layout.Append(cell).Append('|').Append(AssetDatabase.GetAssetPath(tile)).Append('|')
                        .Append(matrix.ToString("F5")).Append('|').Append(map.GetColor(cell).ToString("F5")).AppendLine();
                    if (map.name == "Scenery" && sprite != null)
                    {
                        Rect bounds = SpriteBounds(map, cell, sprite);
                        var record = new JObject { ["cell"] = new JArray(cell.x, cell.y), ["tile"] = tile.name,
                            ["bounds"] = new JArray(bounds.xMin, bounds.yMin, bounds.xMax, bounds.yMax) };
                        boundsReport.Add(record);
                        if ((string)entry["family"] == "Dungeon" && (tile.name == "Prep_Prop_Pillar" || tile.name == "Prep_Prop_BrokenPillar"))
                        { pillars.Add(bounds); if (bounds.yMin < 8 && bounds.yMax > -8) visibleTallPillars++; }
                        // The protected battle rectangle includes body/HP-bar margins; framing outside it is allowed.
                        var insetArea = new Rect(protectedArea.xMin + .001f, protectedArea.yMin + .001f, protectedArea.width - .002f, protectedArea.height - .002f);
                        if (bounds.Overlaps(insetArea)) intrusions.Add(record.DeepClone());
                    }
                }
                counts[map.name] = count;
            }
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
                foreach (Material material in renderer.sharedMaterials)
                    if (material == null) missing.Add(renderer.name + ": missing material");
                    else if (references.Add(material)) CheckReferences(material, missing);
            int overlaps = 0;
            for (int a = 0; a < boundsReport.Count; a++)
                for (int b = a + 1; b < boundsReport.Count; b++)
                {
                    JToken p = boundsReport[a]["bounds"], q = boundsReport[b]["bounds"];
                    if (Rect.MinMaxRect((float)p[0], (float)p[1], (float)p[2], (float)p[3])
                        .Overlaps(Rect.MinMaxRect((float)q[0], (float)q[1], (float)q[2], (float)q[3]))) overlaps++;
                }
            return new JObject
            {
                ["number"] = number, ["name"] = entry["name"], ["path"] = entry["path"], ["pool"] = entry["pool"], ["mode"] = entry["mode"],
                ["composition"] = entry["composition"], ["compositionRevision"] = entry["compositionRevision"],
                ["groundLayout"] = entry["groundLayout"],
                ["tilemapCount"] = maps.Length, ["tileCounts"] = counts,
                ["rendererCount"] = renderers.Length,
                ["colliderCount"] = root.GetComponentsInChildren<Collider2D>(true).Length + root.GetComponentsInChildren<Collider>(true).Length,
                ["hasMissingReferences"] = missing.Count != 0, ["missingReferences"] = JArray.FromObject(missing.Distinct()),
                ["centralClearHalfWidth"] = clear, ["centralSceneryIntrusions"] = intrusions.Count,
                ["waterTileCount"] = waterTiles.Count, ["waterTiles"] = waterTiles,
                ["prohibitedDoorOrHangingVineCount"] = prohibitedTiles.Count, ["prohibitedTiles"] = prohibitedTiles,
                ["prohibitedStandalonePropCount"] = prohibitedTiles.Count,
                ["visibleTallPillars"] = visibleTallPillars,
                ["tallPillarCount"] = pillars.Count,
                ["pillarArrangementValid"] = pillars.Count <= 2 && (pillars.Count != 2 ||
                    pillars[0].center.x * pillars[1].center.x < 0 && Mathf.Abs(pillars[0].yMin - pillars[1].yMin) <= .0625f),
                ["protectedBattleRectangle"] = new JArray(protectedArea.xMin, protectedArea.yMin, protectedArea.xMax, protectedArea.yMax),
                ["sceneryOverlapPairs"] = overlaps,
                ["intrusions"] = intrusions, ["scenerySpriteBounds"] = boundsReport,
                ["layoutSha256"] = Hash(Encoding.UTF8.GetBytes(layout.ToString()))
            };
        }

        static void CheckReferences(Object target, List<string> missing)
        {
            using var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.GetIterator();
            while (property.Next(true))
                if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue == null && property.objectReferenceInstanceIDValue != 0)
                    missing.Add(target.name + ": " + property.propertyPath);
        }

        static Rect SpriteBounds(Tilemap map, Vector3Int cell, Sprite sprite)
        {
            Vector3 origin = map.CellToLocalInterpolated((Vector3)cell + map.tileAnchor);
            Matrix4x4 matrix = map.GetTransformMatrix(cell);
            Bounds bounds = sprite.bounds;
            Vector3[] points =
            {
                new(bounds.min.x, bounds.min.y), new(bounds.max.x, bounds.min.y),
                new(bounds.min.x, bounds.max.y), new(bounds.max.x, bounds.max.y)
            };
            Vector3[] world = points.Select(p => map.transform.TransformPoint(origin + matrix.MultiplyPoint3x4(p))).ToArray();
            return Rect.MinMaxRect(world.Min(p => p.x), world.Min(p => p.y), world.Max(p => p.x), world.Max(p => p.y));
        }

        static JObject Render(Camera camera, GameObject root, string path, int width, int height)
        {
            RenderTexture previous = RenderTexture.active;
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1, filterMode = FilterMode.Point };
            Texture2D texture = null;
            var timer = Stopwatch.StartNew();
            try
            {
                target.Create();
                camera.targetTexture = target;
                camera.aspect = (float)width / height;
                camera.Render();
                RenderTexture.active = target;
                texture = new Texture2D(width, height, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();
                byte[] pixels = texture.GetRawTextureData();
                var uniqueColors = new HashSet<int>();
                for (int i = 0; i + 2 < pixels.Length; i += 3 * 11)
                {
                    uniqueColors.Add((pixels[i] << 16) | (pixels[i + 1] << 8) | pixels[i + 2]);
                    if (uniqueColors.Count >= 32) break;
                }
                File.WriteAllBytes(path, texture.EncodeToPNG());
                Vector3 bottom = camera.ViewportToWorldPoint(new Vector3(0, 0, -camera.transform.position.z));
                Vector3 top = camera.ViewportToWorldPoint(new Vector3(1, 1, -camera.transform.position.z));
                Tilemap ground = root.GetComponentsInChildren<Tilemap>(true).FirstOrDefault(x => x.name == "Ground");
                int holes = 0;
                if (ground == null) holes = -1;
                else
                {
                    Vector3Int a = ground.WorldToCell(bottom), b = ground.WorldToCell(top);
                    for (int y = a.y; y <= b.y; y++) for (int x = a.x; x <= b.x; x++)
                        if (!ground.HasTile(new Vector3Int(x, y, 0))) holes++;
                }
                return new JObject { ["width"] = width, ["height"] = height, ["pixelSha256"] = Hash(pixels),
                    ["nonBlank"] = uniqueColors.Count >= 4, ["groundHolesInViewport"] = holes,
                    ["worldBoundsAtZ0"] = new JArray(bottom.x, bottom.y, top.x, top.y),
                    ["renderAndEncodeMilliseconds"] = timer.Elapsed.TotalMilliseconds };
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                if (texture != null) Object.DestroyImmediate(texture);
                target.Release(); Object.DestroyImmediate(target);
            }
        }

        static JArray DuplicateGroups(JArray results, Func<JToken, string> key) => JArray.FromObject(results.GroupBy(key)
            .Where(x => x.Count() > 1).Select(x => x.Select(y => (int)y["number"]).ToArray()));
        static string Hash(byte[] bytes)
        {
            using var hash = SHA256.Create();
            return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }

        sealed class CameraSpec
        {
            public Vector3 position;
            public Quaternion rotation;
            public bool orthographic;
            public float fieldOfView, orthographicSize, nearClip, farClip;
            public JObject ToJson() => new()
            {
                ["source"] = MainScene + " / Main Camera + CM_GamePlay Lens", ["orthographic"] = orthographic,
                ["fieldOfView"] = fieldOfView, ["orthographicSize"] = orthographicSize,
                ["position"] = new JArray(position.x, position.y, position.z),
                ["rotation"] = new JArray(rotation.x, rotation.y, rotation.z, rotation.w)
            };
        }

        // Read the saved scene, without opening it or running scene scripts in the user's editor.
        static CameraSpec ReadCameraSpec()
        {
            string yaml = File.ReadAllText(MainScene);
            var blocks = Regex.Matches(yaml, @"^--- !u!(\d+) &([^\r\n]+)\r?\n(.*?)(?=^---|\z)", RegexOptions.Multiline | RegexOptions.Singleline)
                .Cast<Match>().ToDictionary(m => m.Groups[2].Value, m => (type: int.Parse(m.Groups[1].Value), text: m.Groups[3].Value));
            string Named(string name) => blocks.Values.Single(x => x.type == 1 && Regex.IsMatch(x.text, @"^  m_Name: " + Regex.Escape(name) + @"\r?$", RegexOptions.Multiline)).text;
            string Component(string go, int type) => Regex.Matches(go, @"component: \{fileID: (\d+)\}").Cast<Match>()
                .Select(m => blocks[m.Groups[1].Value]).Single(x => x.type == type).text;
            string main = Named("Main Camera"), gameplay = Named("CM_GamePlay");
            string camera = Component(main, 20), transform = Component(gameplay, 4), lens = Component(gameplay, 114);
            float Value(string source, string field) => float.Parse(Regex.Match(source, @"^\s*" + Regex.Escape(field) + @": ([\-\d.eE]+)\r?$", RegexOptions.Multiline).Groups[1].Value, CultureInfo.InvariantCulture);
            float[] Vector(string source, string field) => Regex.Matches(Regex.Match(source, Regex.Escape(field) + @": \{([^}]+)\}").Groups[1].Value, @"[xyzw]: ([\-\d.eE]+)")
                .Cast<Match>().Select(m => float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)).ToArray();
            float[] position = Vector(transform, "m_LocalPosition"), rotation = Vector(transform, "m_LocalRotation");
            return new CameraSpec
            {
                position = new Vector3(position[0], position[1], position[2]), rotation = new Quaternion(rotation[0], rotation[1], rotation[2], rotation[3]),
                orthographic = Value(camera, "orthographic") != 0, fieldOfView = Value(lens, "FieldOfView"),
                orthographicSize = Value(lens, "OrthographicSize"), nearClip = Value(lens, "NearClipPlane"), farClip = Value(lens, "FarClipPlane")
            };
        }

        static string Gallery(JArray results, string phase)
        {
            var html = new StringBuilder("<!doctype html><html lang='ko'><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'><title>환경 미리보기 01–90</title><style>body{margin:0;padding:24px;background:#111616;color:#e6ebde;font:15px system-ui,sans-serif}header{max-width:1000px;margin-bottom:24px}h1{font-size:26px}p{line-height:1.7;color:#b8c8bb}nav{display:flex;gap:10px;flex-wrap:wrap;margin:16px 0}a{color:#d9e7a9}section{margin:32px 0}h2{font-size:20px}.grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(210px,1fr));gap:18px}figure{margin:0;background:#1b2220;border:1px solid #35403a;border-radius:8px;overflow:hidden}figure img{display:block;width:100%;image-rendering:pixelated}figcaption{padding:12px;line-height:1.7}.number{font-size:22px;font-weight:700;color:#e7cd89}.name{font-size:12px;color:#b7c3b8;overflow-wrap:anywhere}.aspect{font-size:12px}button{padding:8px 12px;background:#d9e7a9;color:#192118;border:0;border-radius:5px;cursor:pointer}</style><header><h1>배경 미리보기 01–90</h1><p>실제 저장된 환경 프리팹을 Unity에서 렌더링했습니다. 각 번호를 눌러 원본 크기로 확인할 수 있습니다. 기준 화면은 540×960이며 실제 전투 카메라의 시야를 사용합니다. 캐릭터와 HUD를 제외한 배경 검토용입니다.</p>");
            if (phase == "after") html.Append("<button onclick=\"document.querySelectorAll('img').forEach(i=>{let before=i.dataset.before==='1';i.src=(before?'':'../before/')+i.dataset.file;i.dataset.before=before?'0':'1'});this.textContent=this.textContent==='이전 배경 보기'?'개선 배경 보기':'이전 배경 보기'\">이전 배경 보기</button>");
            html.Append("<nav>");
            foreach (string pool in Pools) html.Append($"<a href='#{pool}'>{WebUtility.HtmlEncode(pool)}</a>");
            html.Append("</nav></header>");
            foreach (string pool in Pools)
            {
                html.Append($"<section id='{pool}'><h2>{WebUtility.HtmlEncode(pool)}</h2><div class='grid'>");
                foreach (JToken item in results.Where(x => (string)x["pool"] == pool))
                {
                    string file = WebUtility.HtmlEncode((string)item["image"]), name = WebUtility.HtmlEncode((string)item["name"]);
                    html.Append($"<figure><a href='{file}' target='_blank'><img loading='lazy' src='{file}' data-file='{file}' alt='{item["number"]}: {name}'></a><figcaption><span class='number'>#{(int)item["number"]:00}</span> · {item["mode"]}<div class='name'>{name}</div><div class='aspect'>");
                    foreach (JToken aspect in item["aspectChecks"])
                        html.Append($"<a target='_blank' href='{WebUtility.HtmlEncode((string)aspect["image"])}'>{aspect["name"]} {aspect["width"]}×{aspect["height"]}</a> ");
                    html.Append("</div></figcaption></figure>");
                }
                html.Append("</div></section>");
            }
            return html.Append("</html>").ToString();
        }
    }
}
