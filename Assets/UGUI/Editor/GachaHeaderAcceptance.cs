using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>실제 뽑기/종류 탭 프리팹의 닫기 하단 가림과 클릭 목적지를 계정 없이 검사한다.</summary>
    public static class GachaHeaderAcceptance
    {
        public static Dictionary<string, object> Run()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Gacha header acceptance requires EditMode.");
            var cases = new List<string>(); var failures = new List<string>();
            void Check(bool ok, string label) { cases.Add(label); if (!ok) failures.Add(label); }
            var catalog = AssetDatabase.LoadAssetAtPath<UIViewCatalog>(PrefabGenUtil.CatalogPath);
            var previousFont = F.Font;
            try
            {
                foreach (var size in new[] { new Vector2Int(1080, 2316), new Vector2Int(1080, 1440), new Vector2Int(1440, 1920) })
                {
                    using var fixture = new Fixture(catalog, size);
                    var view = fixture.View;
                    string before = EditorJsonUtility.ToJson(view) + EditorJsonUtility.ToJson(view.authoredContent);
                    void Geometry(string phase)
                    {
                        string label = size + " " + phase;
                        var close = (RectTransform)view.closeButton.transform;
                        Check(Inside(fixture.Header, close) && close.rect.width >= 144 && close.rect.height >= 144,
                            label + " contains the full 144x144 close target inside the header; header=" + fixture.Header.rect + " close=" + BoundsIn(fixture.Header, close));
                        Check(fixture.Tabs.All(tab => Inside(view.tabBar, (RectTransform)tab.Button.transform) && ((RectTransform)tab.Button.transform).rect.height >= 144),
                            label + " contains both real 144px navigation tabs");
                        Check(fixture.Tabs.All(tab => !Overlaps(view.sheet, close, (RectTransform)tab.Button.transform)),
                            label + " keeps the entire close target separate from both tab graphics and input regions");
                        Check(view.scroll.vertical && view.scroll.viewport.rect.height >= 80 && view.authoredContent.pullRow.GetComponent<LayoutElement>().preferredHeight >= 168,
                            label + " retains the internal body scroll and fixed pull action row");
                    }
                    Geometry("saved prefab");
                    // 실기기에서 확인된132/100/104 조합을 복원하여, 실제 빌드 진입점과 같은 연산의 복구를 검증한다.
                    SetHeight(fixture.Header, 132); SetHeight(view.tabBar, 100);
                    SetHeight(view.closeButton.transform, 104);
                    fixture.Refresh();
                    Check(!Inside(fixture.Header, (RectTransform)view.closeButton.transform) &&
                        fixture.Tabs.Any(tab => Overlaps(view.sheet, (RectTransform)view.closeButton.transform, (RectTransform)tab.Button.transform)),
                        size + " reproduces the former oversized close and overflowing tab overlap");
                    F.Font = view.authoredContent.ratesButton.GetComponentInChildren<TMP_Text>(true).font;
                    GachaPanelPrefabGens.ApplyBronzeInfoLinks(view);
                    fixture.Refresh();
                    Geometry("PrepareBronzeInfoLinks operation");
                    Check(before == EditorJsonUtility.ToJson(view) + EditorJsonUtility.ToJson(view.authoredContent),
                        size + " retains all serialized gacha references through the build preparation operation");

                    int closeClicks = 0; var tabClicks = new int[2];
                    view.closeButton.onClick.AddListener(() => closeClicks++);
                    for (int i = 0; i < 2; i++) { int tab = i; fixture.Tabs[i].Button.onClick.AddListener(() => tabClicks[tab]++); }
                    var closeRect = (RectTransform)view.closeButton.transform;
                    Check(fixture.Click(view.closeButton, new Vector2(closeRect.rect.center.x, closeRect.rect.yMin + 4)),
                        size + " raycasts and dispatches the lower edge of close to close rather than a tab; actual=" + fixture.LastHit);
                    Check(closeClicks == 1 && tabClicks[0] == 0 && tabClicks[1] == 0,
                        size + " close lower-edge input fires only its own callback");
                    for (int i = 0; i < 2; i++)
                    {
                        var rect = (RectTransform)fixture.Tabs[i].Button.transform;
                        Check(fixture.Click(fixture.Tabs[i].Button, new Vector2(rect.rect.xMax - 12, rect.rect.yMax - 4)),
                            size + " raycasts and dispatches the upper edge of tab " + i + " without close interference; actual=" + fixture.LastHit);
                    }
                    Check(closeClicks == 1 && tabClicks[0] == 1 && tabClicks[1] == 1,
                        size + " both tabs retain independent click callbacks");
                }
            }
            finally { F.Font = previousFont; }
            return new Dictionary<string, object> { ["passed"] = cases.Count - failures.Count, ["count"] = cases.Count, ["cases"] = cases,
                ["failures"] = failures, ["scope"] = "EditMode actual prefab clones, offscreen GraphicRaycaster and pointer-click dispatch; no managers, profile, API, pull or asset writes" };
        }

        private static void SetHeight(Transform target, float value)
        {
            var layout = target.GetComponent<LayoutElement>(); layout.minHeight = layout.preferredHeight = value;
        }
        private static Bounds BoundsIn(RectTransform parent, RectTransform child)
        {
            var corners = new Vector3[4]; child.GetWorldCorners(corners);
            var bounds = new Bounds(parent.InverseTransformPoint(corners[0]), Vector3.zero);
            for (int i = 1; i < 4; i++) bounds.Encapsulate(parent.InverseTransformPoint(corners[i]));
            return bounds;
        }
        private static bool Inside(RectTransform parent, RectTransform child)
        {
            var b = BoundsIn(parent, child); var r = parent.rect;
            return b.min.x >= r.xMin - .5f && b.max.x <= r.xMax + .5f && b.min.y >= r.yMin - .5f && b.max.y <= r.yMax + .5f;
        }
        private static bool Overlaps(RectTransform parent, RectTransform left, RectTransform right)
        {
            var a = BoundsIn(parent, left); var b = BoundsIn(parent, right);
            return a.min.x < b.max.x - .5f && a.max.x > b.min.x + .5f && a.min.y < b.max.y - .5f && a.max.y > b.min.y + .5f;
        }

        private sealed class Fixture : IDisposable
        {
            public readonly GachaPanelView View;
            public readonly RectTransform Header;
            public readonly NavTabButtonView[] Tabs = new NavTabButtonView[2];
            public string LastHit { get; private set; }
            private GameObject _canvas, _cameraObject, _events;
            private Camera _camera;
            private GraphicRaycaster _raycaster;
            private EventSystem _eventSystem;
            private RenderTexture _target;

            public Fixture(UIViewCatalog catalog, Vector2Int size)
            {
                try
                {
                    if (catalog == null || catalog.panelGacha == null || catalog.itemNavTabButton == null)
                        throw new InvalidOperationException("Actual gacha or navigation tab prefab is missing.");
                    _canvas = new GameObject("GachaHeaderAcceptance_Canvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
                    _canvas.hideFlags = HideFlags.HideAndDontSave; _canvas.layer = 5;
                    _cameraObject = new GameObject("GachaHeaderAcceptance_Camera", typeof(Camera));
                    _cameraObject.hideFlags = HideFlags.HideAndDontSave;
                    _camera = _cameraObject.GetComponent<Camera>(); _camera.enabled = false;
                    _target = new RenderTexture(size.x, size.y, 24, RenderTextureFormat.ARGB32); _target.Create();
                    _camera.targetTexture = _target; _camera.cullingMask = 1 << 5;
                    var canvas = _canvas.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = _camera; canvas.planeDistance = 1; canvas.scaleFactor = size.x / 1080f;
                    Canvas.ForceUpdateCanvases();
                    _raycaster = _canvas.GetComponent<GraphicRaycaster>();
                    _events = new GameObject("GachaHeaderAcceptance_EventSystem"); _events.SetActive(false);
                    _events.hideFlags = HideFlags.HideAndDontSave;
                    _eventSystem = _events.AddComponent<EventSystem>();
                    View = Object.Instantiate(catalog.panelGacha, _canvas.transform, false).GetComponent<GachaPanelView>();
                    View.gameObject.SetActive(true);
                    var root = (RectTransform)View.transform;
                    root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one; root.offsetMin = root.offsetMax = Vector2.zero;
                    Header = (RectTransform)View.sheet.Find("Header");
                    var hud = (RectTransform)new GameObject("HudTopBoundary", typeof(RectTransform)).transform;
                    hud.SetParent(root, false); hud.anchorMin = new Vector2(0, 1); hud.anchorMax = Vector2.one; hud.pivot = new Vector2(.5f, 1);
                    hud.sizeDelta = new Vector2(0, UguiTheme.HudTopHeight); hud.anchoredPosition = Vector2.zero;
                    View.sheet.GetComponent<SheetSizeFitter>().SetTopBoundary(hud);
                    for (int i = 0; i < 2; i++)
                    {
                        Tabs[i] = Object.Instantiate(catalog.itemNavTabButton, View.tabBar, false).GetComponent<NavTabButtonView>();
                        Tabs[i].SetLabel(i == 0 ? "장비" : "마탑 스킬");
                        Tabs[i].SetIcon(i == 0 ? catalog.iconChest : catalog.iconSkillWand);
                    }
                    // 실제 게임의 컨트롤러를 연결하지 않고 UI dispatch만 검사한다.
                    if (View.closeButton.onClick.GetPersistentEventCount() != 0 || Tabs.Any(x => x.Button.onClick.GetPersistentEventCount() != 0))
                        throw new InvalidOperationException("Fixture refuses to execute unknown persistent button actions.");
                    Refresh();
                }
                catch { Dispose(); throw; }
            }

            public void Refresh()
            {
                for (int i = 0; i < 2; i++) { Canvas.ForceUpdateCanvases(); LayoutRebuilder.ForceRebuildLayoutImmediate(View.sheet); }
                Canvas.ForceUpdateCanvases();
                var previous = RenderTexture.active;
                try { _camera.Render(); }
                finally { RenderTexture.active = previous; }
            }
            public bool Click(Button expected, Vector2 local)
            {
                var rect = (RectTransform)expected.transform;
                Vector3 screen = _camera.WorldToScreenPoint(rect.TransformPoint(local));
                var pointer = new PointerEventData(_eventSystem) { position = new Vector2(screen.x, screen.y), button = PointerEventData.InputButton.Left };
                var hits = new List<RaycastResult>(); _raycaster.Raycast(pointer, hits);
                var receiver = hits.Count > 0 ? ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) : null;
                LastHit = receiver != null ? receiver.name : hits.Count > 0 ? hits[0].gameObject.name : "none";
                return receiver == expected.gameObject && ExecuteEvents.Execute(receiver, pointer, ExecuteEvents.pointerClickHandler);
            }
            public void Dispose()
            {
                if (_camera != null) _camera.targetTexture = null;
                if (_canvas != null) Object.DestroyImmediate(_canvas);
                if (_cameraObject != null) Object.DestroyImmediate(_cameraObject);
                if (_events != null) Object.DestroyImmediate(_events);
                if (_target != null) { _target.Release(); Object.DestroyImmediate(_target); }
            }
        }
    }
}
