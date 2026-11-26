using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>계정·실제 장비·서버 없이 가상 장비 목록의 캐시와 바인딩/입력을 검사한다.</summary>
    public static class BronzeEquipmentGridAcceptance
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [MenuItem("KingdomIdle/UGUI/Validate bronze equipment grid cache")]
        public static void Validate() => UnityEngine.Debug.Log(Newtonsoft.Json.JsonConvert.SerializeObject(Run(), Newtonsoft.Json.Formatting.Indented));

        public static object Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Use an isolated Edit Mode session.");
            var checks = new List<string>();
            void Check(bool value, string label)
            {
                if (!value) throw new InvalidOperationException("Equipment grid acceptance: " + label);
                checks.Add(label);
            }
            var root = new GameObject("BronzeEquipmentGridAcceptance", typeof(RectTransform));
            root.hideFlags = HideFlags.HideAndDontSave;
            double idleMilliseconds = 0;
            try
            {
                var rootRect = (RectTransform)root.transform; rootRect.sizeDelta = new Vector2(960, 900);
                var scroll = root.AddComponent<ScrollRect>();
                var viewport = Child("Viewport", rootRect);
                viewport.anchorMin = viewport.anchorMax = viewport.pivot = new Vector2(0, 1);
                viewport.sizeDelta = new Vector2(960, 600); viewport.anchoredPosition = Vector2.zero;
                var content = Child("Content", viewport); TopStretch(content);
                var gridRect = Child("Grid", content); TopStretch(gridRect);
                scroll.viewport = viewport; scroll.content = content;
                var grid = gridRect.gameObject.AddComponent<GridLayoutGroup>();
                grid.cellSize = new Vector2(200, 200); grid.spacing = new Vector2(16, 16);
                grid.padding = new RectOffset(8, 8, 10, 12);
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 4;
                var prefab = Child("FixtureCard", rootRect).gameObject; prefab.SetActive(false);
                var template = prefab.AddComponent<EquipCellView>(); template.button = prefab.AddComponent<Button>();
                var bound = new Dictionary<EquipCellView, int>();
                int binds = 0, clicked = -1, clicks = 0;
                void Add(int index) => VirtualEquipmentGrid.Add(gridRect, prefab, card =>
                {
                    binds++; bound[card] = index;
                    card.OnClick(() => { clicked = index; clicks++; });
                });
                for (int i = 0; i < 998; i++) Add(i);
                var view = gridRect.GetComponent<VirtualEquipmentGrid>();
                if (Field<RectTransform>(view, "_rect") == null) Invoke(view, "Awake");
                view.Refresh(); view.Refresh();
                Check(!grid.enabled && gridRect.GetComponent<LayoutElement>() != null, "Virtualization keeps the authored grid disabled and supplies its scroll height");
                CheckHeight(view, grid, gridRect, 998, Check);
                Check(Field<List<EquipCellView>>(view, "_pool").Count < 64, "998 rows instantiate only the viewport and buffer cards");
                CheckBindings(view, bound, Check);
                int initialBinds = binds, initialPool = Field<List<EquipCellView>>(view, "_pool").Count;
                var marker = new Vector3(12345, 23456, 34567);
                var corners = Field<Vector3[]>(view, "_corners");
                for (int i = 0; i < corners.Length; i++) corners[i] = marker;
                foreach (var card in Field<List<EquipCellView>>(view, "_pool")) card.transform.hasChanged = false;
                var timer = Stopwatch.StartNew();
                for (int i = 0; i < 1000; i++) view.Refresh();
                timer.Stop(); idleMilliseconds = timer.Elapsed.TotalMilliseconds;
                Check(corners.All(x => x == marker), "1000 unchanged frames skip viewport GetWorldCorners");
                Check(Field<List<EquipCellView>>(view, "_pool").All(x => !x.transform.hasChanged) && binds == initialBinds &&
                    Field<List<EquipCellView>>(view, "_pool").Count == initialPool,
                    "1000 unchanged frames do not write card transforms, bind again or instantiate cards");

                content.anchoredPosition = new Vector2(0, 20); view.Refresh();
                Check(corners.Any(x => x != marker) && binds == initialBinds,
                    "Scrolling within the buffered range updates viewport geometry without rebinding cards");
                content.anchoredPosition = new Vector2(0, 800); view.Refresh();
                Check(Field<List<int>>(view, "_indices")[0] > 0 && binds > initialBinds, "Scrolling to another range rebinds the correct later rows");
                CheckBindings(view, bound, Check);
                var pool = Field<List<EquipCellView>>(view, "_pool");
                int expectedClick = Field<List<int>>(view, "_indices")[0];
                pool[0].Button.onClick.Invoke();
                Check(clicked == expectedClick && clicks == 1, "A reused card invokes its current row action exactly once");

                viewport.sizeDelta = new Vector2(720, 380); view.Refresh();
                CheckBindings(view, bound, Check);
                Check(pool.Count < 64 && pool.Any(x => !x.gameObject.activeSelf), "A shorter viewport deactivates surplus pool cards without recreating the list");
                grid.padding = new RectOffset(24, 20, 30, 18); grid.spacing = new Vector2(20, 24); view.Refresh();
                CheckHeight(view, grid, gridRect, 998, Check);
                CheckPositions(view, grid, Check);
                grid.cellSize = new Vector2(225, 284); grid.constraintCount = 3; view.Refresh();
                CheckHeight(view, grid, gridRect, 998, Check);
                CheckPositions(view, grid, Check);
                CheckBindings(view, bound, Check);

                // The row total stays the same when the 999th item is appended: dirty must still invalidate bindings.
                int beforeAppend = binds;
                Add(998); view.Refresh();
                Check(binds > beforeAppend, "Appending a row invalidates bindings even when the rounded row count stays unchanged");
                content.anchoredPosition = new Vector2(0, gridRect.rect.height - viewport.rect.height); view.Refresh();
                Check(Field<List<int>>(view, "_indices").Contains(998), "The appended 999th row is reachable at the bottom");
                CheckBindings(view, bound, Check);

                var replacement = Child("ReplacementViewport", rootRect);
                replacement.sizeDelta = new Vector2(720, 500);
                replacement.position = viewport.position;
                scroll.viewport = replacement; view.Refresh();
                Check(Field<RectTransform>(view, "_viewport") == replacement, "Replacing the ScrollRect viewport refreshes the cached viewport reference");
                scroll.viewport = viewport; view.Refresh();
                for (int i = 0; i < corners.Length; i++) corners[i] = marker;
                pool[0].GetComponent<RectTransform>().anchoredPosition = new Vector2(9000, 9000);
                Invoke(view, "OnEnable"); view.Refresh();
                Check(corners.Any(x => x != marker), "The re-enable path invalidates the viewport geometry snapshot");
                CheckPositions(view, grid, Check);
                CheckBindings(view, bound, Check);
                Check(pool.Count < 64, "Scrolling, resizing, list growth and re-enable preserve the bounded card pool");
                ValidateNames(rootRect, Check);
                return new { passed = checks.Count, checks, idleRefreshes = 1000, idleMilliseconds,
                    source = "Synthetic rows and buttons only; no game account, server, rewards or editor scene save",
                    limitation = "Edit Mode fixture. Actual ScrollRect motion, canvas render cost and Android profiling require coordinator execution." };
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void ValidateNames(RectTransform parent, Action<bool, string> check)
        {
            var canvasRect = Child("EquipmentNameCanvas", parent);
            var canvas = canvasRect.gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
            canvasRect.sizeDelta = new Vector2(960, 900);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UGUI/Prefabs/Items/Item_EquipCell.prefab");
            var card = Object.Instantiate(prefab, canvasRect, false).GetComponent<EquipCellView>();
            var rect = (RectTransform)card.transform;
            var catalogEquipment = AssetDatabase.FindAssets("t:EquipmentData", new[] { "Assets/_Project/Scripts/Player/Equipment/Prefab" })
                .Select(AssetDatabase.GUIDToAssetPath).Where(path => !path.Contains("/Test/"))
                .Select(AssetDatabase.LoadAssetAtPath<EquipmentData>).Where(data => data != null).ToArray();
            var equipment = catalogEquipment.Where(data => data.CanBeRewarded).ToArray();
            check(equipment.Length == 12 && equipment.Select(data => data.rarity).Distinct().Count() == 3,
                "Name fixture reads all twelve active normal, rare and epic catalog assets without changing them");
            foreach (float width in new[] { 210f, 225f })
                foreach (var data in catalogEquipment)
                    foreach (string name in new[] { data.DisplayName, data.DisplayName + " +" + data.maxEnhancementLevel })
                    {
                        rect.sizeDelta = new Vector2(width, 284);
                        card.Set(data.icon, name, UguiTheme.TextPrimary, "ATK +999", UguiTheme.RarityColor(data.rarity), false, false);
                        LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
                        var label = card.nameLabel;
                        label.ForceMeshUpdate(true, true);
                        check(label.GetParsedText() == name && !label.isTextTruncated && label.textInfo.lineCount <= 2 &&
                            label.GetPreferredValues(label.text, label.rectTransform.rect.width, float.PositiveInfinity).y <= label.rectTransform.rect.height + 1,
                            width + "px " + name + " keeps the complete name and enhancement within two lines at the authored font size");
                        int start = 0;
                        foreach (string word in name.Split(' '))
                        {
                            int line = label.textInfo.characterInfo[start].lineNumber;
                            check(Enumerable.Range(start, word.Length).All(index => label.textInfo.characterInfo[index].lineNumber == line),
                                width + "px " + name + " keeps the word " + word + " on one line");
                            start += word.Length + 1;
                        }
                    }
            rect.sizeDelta = new Vector2(225, 284);
            foreach (int count in new[] { 1, 999, 9999, 999999, 99999999, int.MaxValue })
                foreach (NumberStyle style in Enum.GetValues(typeof(NumberStyle)))
                {
                    string state = "추가 보관 ×" + NumberNotation.Format(count, style);
                    card.Set(equipment[0].icon, "보석 지팡이 +10 ×" + count, UguiTheme.TextPrimary,
                        "1개 받기", UguiTheme.RarityColor(eEquipmentRarity.Rare), false, false, state);
                    LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
                    var label = card.stateLabel;
                    label.ForceMeshUpdate(true, true);
                    check(label.GetParsedText() == state && !label.isTextTruncated && label.textInfo.lineCount == 1 &&
                        label.fontSize >= 22 && label.textInfo.lineInfo[0].maxAdvance <= label.rectTransform.rect.width + 1,
                        "225px stored quantity " + count + " stays visible in the separate state line with " + style + " notation");
                }
        }

        private static void CheckHeight(VirtualEquipmentGrid view, GridLayoutGroup grid, RectTransform rect, int count, Action<bool, string> check)
        {
            int rows = Mathf.CeilToInt((float)count / grid.constraintCount);
            float expected = grid.padding.vertical + rows * grid.cellSize.y + Mathf.Max(0, rows - 1) * grid.spacing.y;
            var layout = rect.GetComponent<LayoutElement>();
            check(Mathf.Abs(rect.rect.height - expected) < .01f && Mathf.Abs(layout.preferredHeight - expected) < .01f &&
                Mathf.Abs(layout.minHeight - expected) < .01f, "Scroll height follows rows, cell height, vertical spacing and padding: " + expected);
        }

        private static void CheckBindings(VirtualEquipmentGrid view, Dictionary<EquipCellView, int> bound, Action<bool, string> check)
        {
            var pool = Field<List<EquipCellView>>(view, "_pool"); var indices = Field<List<int>>(view, "_indices");
            for (int i = 0; i < pool.Count; i++)
                if (pool[i].gameObject.activeSelf && (!bound.TryGetValue(pool[i], out int index) || index != indices[i]))
                    throw new InvalidOperationException("A visible card is bound to the wrong row.");
            check(true, "Visible card bindings match source order and current virtual indices");
        }

        private static void CheckPositions(VirtualEquipmentGrid view, GridLayoutGroup grid, Action<bool, string> check)
        {
            var pool = Field<List<EquipCellView>>(view, "_pool"); var indices = Field<List<int>>(view, "_indices");
            for (int i = 0; i < pool.Count; i++)
            {
                if (!pool[i].gameObject.activeSelf) continue;
                int index = indices[i]; var rect = (RectTransform)pool[i].transform;
                var expected = new Vector2(grid.padding.left + index % grid.constraintCount * (grid.cellSize.x + grid.spacing.x),
                    -grid.padding.top - index / grid.constraintCount * (grid.cellSize.y + grid.spacing.y));
                if (rect.sizeDelta != grid.cellSize || rect.anchoredPosition != expected)
                    throw new InvalidOperationException("Card geometry is stale after a grid or lifecycle change.");
            }
            check(true, "Visible card sizes and positions follow the current cell, columns, spacing and padding");
        }

        private static RectTransform Child(string name, Transform parent)
        { var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform; rect.SetParent(parent, false); return rect; }
        private static void TopStretch(RectTransform rect)
        { rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, 1); rect.sizeDelta = Vector2.zero; rect.anchoredPosition = Vector2.zero; }
        private static T Field<T>(VirtualEquipmentGrid view, string name) => (T)typeof(VirtualEquipmentGrid).GetField(name, Private).GetValue(view);
        private static void Invoke(VirtualEquipmentGrid view, string name) => typeof(VirtualEquipmentGrid).GetMethod(name, Private).Invoke(view, null);
    }
}
