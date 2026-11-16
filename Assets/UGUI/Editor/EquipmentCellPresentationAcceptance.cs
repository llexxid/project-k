#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using KingdomIdle.Balance;
using Newtonsoft.Json;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>실제 셀/생성 경로의 표시 회귀 검사. 계정·수령·네트워크·저장을 실행하지 않는다.</summary>
    public static class EquipmentCellPresentationAcceptance
    {
        const BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        const BindingFlags StaticFields = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
        static readonly string[] PendingIds = { "ui-only-pending-zero", "ui-only-pending-nine" };

        public static Dictionary<string, object> Run()
        {
            var cases = new List<Dictionary<string, object>>();
            var failures = new List<string>();
            void Case(string name, Func<Dictionary<string, object>> run)
            {
                try
                {
                    var result = run(); result["name"] = name; cases.Add(result);
                    if (!(bool)result["passed"]) failures.Add(name);
                }
                catch (Exception ex)
                {
                    cases.Add(new Dictionary<string, object> { ["name"] = name, ["passed"] = false,
                        ["error"] = (ex.InnerException ?? ex).Message }); failures.Add(name);
                }
            }
            Fixture fixture = null;
            try
            {
                Guard(); fixture = new Fixture();
                Case("pending_levels_distinguishable", () =>
                {
                    var names = fixture.PendingCells.Select(c => Parsed(Field<TMP_Text>(c, "nameLabel"))).ToArray();
                    return Result(names.Length == 2 && names.Distinct().Count() == 2 && names[1].Contains("+9"),
                        ("levels", new[] { 0, 9 }), ("names", names), ("definition", fixture.Data.DisplayName));
                });
                Case("pending_claim_targets_preserved", () =>
                {
                    var targets = fixture.PendingCells.Select(c => CapturedIds(c.Button.onClick)).ToArray();
                    bool match = targets.Length == 2 && targets.Select((ids, i) => ids.Length == 1 && ids[0] == PendingIds[i]).All(x => x);
                    return Result(match, ("captured_original_ids", targets), ("invoked", false));
                });
                foreach (var state in States())
                {
                    var captured = state;
                    Case(captured.Name, () =>
                    {
                        var cell = fixture.MakeCell();
                        bool input = cell.Button.interactable;
                        cell.Set(fixture.Staff.icon, fixture.Staff.DisplayName, UguiTheme.RarityColor(fixture.Staff.rarity), "ATK +1",
                            UguiTheme.RarityColor(fixture.Staff.rarity), captured.Equipped, captured.Dimmed, captured.Text);
                        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)cell.transform);
                        var text = Field<TMP_Text>(cell, "stateLabel");
                        string actual = Parsed(text);
                        bool active = text.gameObject.activeSelf;
                        bool meaning = captured.Name == "normal_without_state" ? !active : active &&
                            (!captured.NeedLock || actual.Contains("잠금")) && (!captured.NeedMismatch || HasMismatch(actual)) &&
                            (!captured.Equipped || actual.Contains("장착"));
                        bool readable = !active || (!text.isTextTruncated && text.textBounds.size.x <= text.rectTransform.rect.width + 1 &&
                            text.textBounds.size.y <= text.rectTransform.rect.height + 1);
                        bool frames = Field<Image>(cell, "equippedFrame").gameObject.activeSelf == captured.Equipped &&
                            Field<Image>(cell, "rarityFrame").gameObject.activeSelf != captured.Equipped;
                        return Result(meaning && readable && frames && cell.Button.interactable == input && Field<CanvasGroup>(cell, "dimGroup").alpha == 1,
                            ("text", actual), ("state_active", active), ("text_truncated", text.isTextTruncated),
                            ("text_width", text.textBounds.size.x), ("rect_width", text.rectTransform.rect.width),
                            ("input_unchanged", cell.Button.interactable == input), ("full_cell_alpha", Field<CanvasGroup>(cell, "dimGroup").alpha));
                    });
                }
                Case("display_keeps_fixture_data_and_ids", () => Result(fixture.DataUnchanged,
                    ("pending_ids", fixture.State.PendingEquipment.Select(e => e.Id).ToArray()),
                    ("levels", fixture.State.PendingEquipment.Select(e => e.Level).ToArray()),
                    ("account_opened", false), ("network_called", false), ("claim_invoked", false)));
                Case("account_context_unchanged_during_projection", () => Result(fixture.ContextUnchanged,
                    ("account_generation_changed", false), ("save_path_allocated", false)));
            }
            catch (Exception ex)
            {
                cases.Add(new Dictionary<string, object> { ["name"] = "fixture_setup", ["passed"] = false,
                    ["error"] = (ex.InnerException ?? ex).Message }); failures.Add("fixture_setup");
            }
            finally
            {
                if (fixture != null)
                {
                    try { fixture.Dispose(); }
                    catch (Exception ex) { failures.Add("fixture_cleanup: " + ex.Message); }
                }
            }
            if (fixture != null)
                Case("finally_restores_original_context", () => Result(fixture.Cleaned));
            return new Dictionary<string, object> { ["passed"] = cases.Count(c => (bool)c["passed"]),
                ["failures"] = failures, ["cases"] = cases,
                ["scope"] = "Empty PlayMode; compiled production pending caller + actual catalog/prefab; new in-memory fixture only. No account open/save, request, ownership change or claim invocation." };
        }

        /// <summary>실제 셀만 임시 카메라로 렌더한다. 출력은 프로젝트 밖 PNG만 허용한다.</summary>
        public static Dictionary<string, object> Snapshot(string outputPath)
        {
            Guard();
            string path = Path.GetFullPath(outputPath);
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            if (string.Equals(path, project, StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith(project + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(Path.GetExtension(path), ".png", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Equipment gallery requires a PNG path outside the project.");
            var previous = RenderTexture.active;
            GameObject cameraRoot = null; RenderTexture render = null; Texture2D image = null;
            using (var fixture = new Fixture())
            {
                try
                {
                    const int width = 1180, height = 900;
                    ((RectTransform)fixture.Canvas.transform).sizeDelta = new Vector2(width, height);
                    fixture.PendingGrid.gameObject.SetActive(false);
                    var gallery = new List<(string name, EquipCellView cell)>
                    { ("Pending +0", fixture.PendingCells[0]), ("Pending +9", fixture.PendingCells[1]) };
                    foreach (var state in States())
                    {
                        var cell = fixture.MakeCell();
                        cell.Set(fixture.Staff.icon, fixture.Staff.DisplayName, UguiTheme.RarityColor(fixture.Staff.rarity), "ATK +1",
                            UguiTheme.RarityColor(fixture.Staff.rarity), state.Equipped, state.Dimmed, state.Text);
                        gallery.Add((state.Name, cell));
                    }
                    for (int i = 0; i < gallery.Count; i++)
                    {
                        var rect = (RectTransform)gallery[i].cell.transform; rect.SetParent(fixture.Canvas.transform, false);
                        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
                        rect.sizeDelta = new Vector2(250, 330);
                        rect.anchoredPosition = new Vector2(24 + i % 4 * 288, -80 - i / 4 * 412);
                        rect.gameObject.SetActive(true);
                        var labelObject = new GameObject("CaseLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
                        labelObject.layer = 5;
                        var label = labelObject.GetComponent<TextMeshProUGUI>(); label.font = fixture.Catalog.defaultFont;
                        label.fontSize = 22; label.color = UguiTheme.Parchment; label.text = gallery[i].name;
                        label.raycastTarget = false; label.textWrappingMode = TextWrappingModes.Normal;
                        var labelRect = (RectTransform)labelObject.transform; labelRect.SetParent(fixture.Canvas.transform, false);
                        labelRect.anchorMin = labelRect.anchorMax = labelRect.pivot = new Vector2(0, 1);
                        labelRect.sizeDelta = new Vector2(270, 56); labelRect.anchoredPosition = new Vector2(24 + i % 4 * 288, -18 - i / 4 * 412);
                    }
                    Canvas.ForceUpdateCanvases();
                    foreach (var text in fixture.Canvas.GetComponentsInChildren<TMP_Text>(false)) text.ForceMeshUpdate(true, true);
                    cameraRoot = new GameObject("EquipmentGalleryCamera", typeof(Camera));
                    var camera = cameraRoot.GetComponent<Camera>(); camera.transform.position = new Vector3(0, 0, -100);
                    camera.orthographic = true; camera.orthographicSize = height * .5f;
                    camera.nearClipPlane = .1f; camera.farClipPlane = 200f;
                    camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = UguiTheme.RusticPanel;
                    camera.cullingMask = 1 << fixture.Canvas.layer;
                    render = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32); render.Create();
                    camera.targetTexture = render; fixture.Canvas.GetComponent<Canvas>().worldCamera = camera;
                    if (GraphicsSettings.currentRenderPipeline != null)
                    {
                        cameraRoot.AddComponent<UniversalAdditionalCameraData>();
                        RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = render });
                    }
                    else camera.Render();
                    RenderTexture.active = render; image = new Texture2D(width, height, TextureFormat.RGBA32, false);
                    image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
                    Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllBytes(path, image.EncodeToPNG());
                    if (!fixture.DataUnchanged || !fixture.ContextUnchanged) throw new InvalidOperationException("Gallery changed fixture data/context.");
                    return Result(true, ("output", path), ("width", width), ("height", height),
                        ("classification", "Synthetic display-only actual-prefab gallery; no real reward or device capture."));
                }
                finally
                {
                    RenderTexture.active = previous;
                    if (cameraRoot != null) Object.DestroyImmediate(cameraRoot);
                    if (image != null) Object.DestroyImmediate(image);
                    if (render != null) { render.Release(); Object.DestroyImmediate(render); }
                }
            }
        }

        readonly struct StateCase
        {
            internal readonly string Name, Text;
            internal readonly bool Equipped, Dimmed, NeedLock, NeedMismatch;
            internal StateCase(string name, string text, bool equipped = false, bool dimmed = false, bool needLock = false, bool needMismatch = false)
            { Name = name; Text = text; Equipped = equipped; Dimmed = dimmed; NeedLock = needLock; NeedMismatch = needMismatch; }
        }
        static StateCase[] States() => new[]
        {
            new StateCase("locked_and_job_mismatch", "잠금", dimmed: true, needLock: true, needMismatch: true),
            new StateCase("job_mismatch_only", null, dimmed: true, needMismatch: true),
            new StateCase("locked_only", "잠금", needLock: true),
            new StateCase("equipped_only", "장착 중", equipped: true),
            new StateCase("normal_without_state", null)
        };
        static bool HasMismatch(string value) => value.Contains("직업 불일치") || value.Contains("직업 불가") || value.Contains("직업불가");
        static Dictionary<string, object> Result(bool passed, params (string key, object value)[] values)
        { var result = new Dictionary<string, object> { ["passed"] = passed }; foreach (var value in values) result[value.key] = value.value; return result; }
        static void Guard()
        {
            if (!Application.isPlaying || LocalProgression.IsReady || UIManager.Instance != null || EquipmentManager.Instance != null ||
                Scripts.Core.UserManager.Instance != null || Scripts.Core.Manager.NetworkManager.Instance != null)
                throw new InvalidOperationException("Equipment cell presentation acceptance requires empty PlayMode without an account or live managers.");
        }
        static string Parsed(TMP_Text text) { text.ForceMeshUpdate(true, true); return text.GetParsedText(); }
        static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, InstanceFields).GetValue(target);
        static void SetUi(UIManager value) => typeof(UIManager).GetProperty("Instance").GetSetMethod(true).Invoke(null, new object[] { value });

        // 수령을 실행하지 않고 실제 Button runtime listener에 캡처된 원래 ID만 확인한다.
        static string[] CapturedIds(UnityEngine.Events.UnityEvent value)
        {
            var ids = new HashSet<string>(); var visited = new HashSet<object>(ReferenceComparer.Instance);
            void Walk(object item, int depth)
            {
                if (item == null || depth > 12 || visited.Count > 512) return;
                if (item is string text) { if (PendingIds.Contains(text)) ids.Add(text); return; }
                if (item is Object || item.GetType().IsPrimitive || !visited.Add(item)) return;
                if (item is Delegate action) { foreach (var call in action.GetInvocationList()) Walk(call.Target, depth + 1); return; }
                if (item is IEnumerable sequence) { foreach (var element in sequence) Walk(element, depth + 1); return; }
                for (Type type = item.GetType(); type != null && type != typeof(object); type = type.BaseType)
                    foreach (var field in type.GetFields(InstanceFields | BindingFlags.DeclaredOnly))
                        Walk(field.GetValue(item), depth + 1);
            }
            Walk(value, 0); return ids.OrderBy(id => id).ToArray();
        }
        sealed class ReferenceComparer : IEqualityComparer<object>
        {
            internal static readonly ReferenceComparer Instance = new();
            public new bool Equals(object a, object b) => ReferenceEquals(a, b);
            public int GetHashCode(object value) => RuntimeHelpers.GetHashCode(value);
        }

        sealed class Fixture : IDisposable
        {
            readonly FieldInfo _stateField = typeof(LocalProgression).GetField("_state", StaticFields);
            readonly FieldInfo _openingField = typeof(LocalProgression).GetField("_opening", StaticFields);
            readonly object _oldState, _oldOpening;
            readonly string _oldAccount, _oldPath;
            readonly long _oldGeneration;
            readonly string _before;
            GameObject _managers;
            internal readonly ProgressionState State;
            internal UIViewCatalog Catalog;
            internal EquipmentData Data, Staff;
            internal GameObject Canvas;
            internal RectTransform PendingGrid;
            internal EquipCellView[] PendingCells;
            internal bool DataUnchanged => JsonConvert.SerializeObject(State) == _before;
            internal bool ContextUnchanged => LocalProgression.AccountKey == _oldAccount && LocalProgression.AccountGeneration == _oldGeneration &&
                (string)typeof(LocalProgression).GetField("_path", StaticFields).GetValue(null) == _oldPath;
            internal bool Cleaned => ReferenceEquals(_stateField.GetValue(null), _oldState) && Equals(_openingField.GetValue(null), _oldOpening) &&
                UIManager.Instance == null && EquipmentManager.Instance == null && ContextUnchanged;

            internal Fixture()
            {
                _oldState = _stateField.GetValue(null); _oldOpening = _openingField.GetValue(null);
                _oldAccount = LocalProgression.AccountKey; _oldGeneration = LocalProgression.AccountGeneration;
                _oldPath = (string)typeof(LocalProgression).GetField("_path", StaticFields).GetValue(null);
                try
                {
                    Catalog = AssetDatabase.LoadAssetAtPath<UIViewCatalog>("Assets/UGUI/UIViewCatalog.asset");
                    if (Catalog == null || Catalog.itemEquipCell == null) throw new InvalidOperationException("Actual equipment-cell catalog/prefab is missing.");
                    var database = AssetDatabase.FindAssets("t:EquipmentDatabase")
                        .Select(g => AssetDatabase.LoadAssetAtPath<EquipmentDatabase>(AssetDatabase.GUIDToAssetPath(g)))
                        .First(d => d != null && d.equipmentList.Any(e => e != null && e.IsAllowedForJob("Knight") && e.maxEnhancementLevel >= 9));
                    Data = database.equipmentList.First(e => e != null && e.icon != null && e.IsAllowedForJob("Knight") && e.maxEnhancementLevel >= 9);
                    Staff = database.equipmentList.First(e => e != null && e.icon != null && e.IsAllowedForJob("Mage") && !e.IsAllowedForJob("Knight"));
                    _managers = new GameObject("EquipmentPresentation_ReadOnlyManagers"); _managers.SetActive(false);
                    var equipment = _managers.AddComponent<EquipmentManager>();
                    typeof(EquipmentManager).GetField("_database", InstanceFields).SetValue(equipment, database); EquipmentManager.Instance = equipment;
                    var ui = _managers.AddComponent<UIManager>(); typeof(UIManager).GetField("catalog", InstanceFields).SetValue(ui, Catalog); SetUi(ui);
                    State = new ProgressionState();
                    State.PendingEquipment.Add(new EquipmentSave { Id = PendingIds[0], Code = Data.itemCode, Level = 0 });
                    State.PendingEquipment.Add(new EquipmentSave { Id = PendingIds[1], Code = Data.itemCode, Level = 9 });
                    _before = JsonConvert.SerializeObject(State);
                    // Ensure의 재진입 가드로 Open/계정 파일 접근을 막고 새 메모리 객체만 동기 투영한다.
                    _openingField.SetValue(null, true); _stateField.SetValue(null, State);
                    Canvas = new GameObject("EquipmentPresentation_Canvas", typeof(RectTransform), typeof(Canvas)); Canvas.layer = 5;
                    Canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                    ((RectTransform)Canvas.transform).sizeDelta = new Vector2(1080, 1920);
                    PendingGrid = (RectTransform)new GameObject("ActualPendingGrid", typeof(RectTransform), typeof(GridLayoutGroup), typeof(LayoutElement)).transform;
                    PendingGrid.SetParent(Canvas.transform, false); PendingGrid.sizeDelta = new Vector2(960, 400);
                    var layout = PendingGrid.GetComponent<GridLayoutGroup>(); layout.cellSize = new Vector2(220, 330); layout.spacing = new Vector2(16, 16);
                    layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount; layout.constraintCount = 4;
                    var virtualGrid = PendingGrid.gameObject.AddComponent<VirtualEquipmentGrid>();
                    typeof(KingdomArmyPanelController).GetMethod("BuildStoredEquipmentCells", StaticFields)
                        .Invoke(null, new object[] { PendingGrid, (Action)(() => { }), null });
                    typeof(VirtualEquipmentGrid).GetMethod("Refresh", InstanceFields).Invoke(virtualGrid, null);
                    UnityEngine.Canvas.ForceUpdateCanvases(); PendingCells = PendingGrid.GetComponentsInChildren<EquipCellView>(false);
                    if (PendingCells.Length != 2) throw new InvalidOperationException("Actual pending caller did not create its two distinct level groups.");
                }
                catch { Dispose(); throw; }
            }
            internal EquipCellView MakeCell()
            {
                var cell = Object.Instantiate(Catalog.itemEquipCell, Canvas.transform, false).GetComponent<EquipCellView>();
                ((RectTransform)cell.transform).sizeDelta = new Vector2(220, 330); return cell;
            }
            public void Dispose()
            {
                _stateField.SetValue(null, _oldState); _openingField.SetValue(null, _oldOpening);
                SetUi(null); EquipmentManager.Instance = null;
                if (Canvas != null) Object.DestroyImmediate(Canvas);
                if (_managers != null) Object.DestroyImmediate(_managers);
            }
        }
    }
}
#endif
