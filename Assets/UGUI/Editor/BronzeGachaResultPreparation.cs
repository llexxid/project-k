using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>거래와 공개 연출을 유지하며 기존 결과창의 표시 참조와 스킨만 갱신한다.</summary>
    public static class BronzeGachaResultPreparation
    {
        private const string Path = "Assets/UGUI/Prefabs/Popups/Popup_GachaResult.prefab";

        [MenuItem("KingdomIdle/UGUI/Prepare bronze gacha result")]
        public static void Prepare()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            F.Init(); F.Catalog = PrefabGenUtil.GetOrCreateCatalog();
            var root = PrefabUtility.LoadPrefabContents(Path);
            try
            {
                Apply(root.GetComponent<GachaResultPopupView>());
                PrefabUtility.SaveAsPrefabAsset(root, Path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            F.Catalog.popupGachaResult = AssetDatabase.LoadAssetAtPath<GameObject>(Path);
            EditorUtility.SetDirty(F.Catalog); AssetDatabase.SaveAssets();
        }

        internal static void Apply(GachaResultPopupView view)
        {
            if (view == null || view.box == null || view.scroll == null || view.grid == null)
                throw new InvalidOperationException("Gacha result references are incomplete.");
            F.Stretch((RectTransform)view.transform);
            var dim = view.transform.Find("Dim").GetComponent<Image>();
            dim.color = new Color(0, 0, 0, .82f); dim.raycastTarget = true;
            var box = view.box.GetComponent<Image>();
            box.sprite = PrefabGenUtil.GetOrCreateSolidRect(); box.type = Image.Type.Simple;
            box.color = new Color(UguiTheme.RusticPanelDeep.r, UguiTheme.RusticPanelDeep.g, UguiTheme.RusticPanelDeep.b, 1);
            view.box.anchorMin = view.box.anchorMax = view.box.pivot = new Vector2(.5f, .5f);
            view.box.sizeDelta = new Vector2(980, 1120); view.box.anchoredPosition = Vector2.zero;
            var layout = view.box.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(28, 28, 24, 24); layout.spacing = 16;
            layout.childAlignment = TextAnchor.UpperCenter; layout.childForceExpandHeight = false;
            if (view.box.Find("BronzeFrame") == null)
                F.Frame(view.box, "BronzeFrame", UguiTheme.Bronze).gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            // 제목 객체는 유지하고 오래된 좁은 리본만 제거한다.
            if (view.title.transform.parent != view.box)
            {
                Transform old = view.title.transform;
                while (old.parent != view.box) old = old.parent;
                view.title.transform.SetParent(view.box, false);
                Object.DestroyImmediate(old.gameObject);
            }
            view.title.name = "Title"; view.title.transform.SetAsFirstSibling();
            view.title.fontSize = 60; view.title.alignment = TextAlignmentOptions.Center;
            view.title.fontSharedMaterial = F.Font.material;
            F.Preferred(view.title, height: 132).minHeight = 132;
            F.Flexible(view.scroll, flexHeight: 1).minHeight = 100;
            view.scroll.horizontal = false;
            view.scroll.movementType = ScrollRect.MovementType.Clamped;
            var contentLayout = view.scroll.content.GetComponent<VerticalLayoutGroup>();
            contentLayout.padding = new RectOffset(); contentLayout.spacing = 0;
            contentLayout.childForceExpandHeight = false;
            var responsive = view.grid.GetComponent<ResponsiveGrid>();
            if (responsive != null) Object.DestroyImmediate(responsive);
            var grid = view.grid.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(292, 440); grid.spacing = new Vector2(16, 16);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 3;
            F.Preferred(view.buttonRow, height: 180).minHeight = 180;
            view.buttonRow.GetComponent<HorizontalLayoutGroup>().spacing = 16;
            StyleButton(view.btnDone, false);
            StyleButton(view.btnRePull1, true);
            StyleButton(view.btnRePullN, true);
            foreach (var button in new[] { view.btnDone, view.btnRePull1, view.btnRePullN })
            {
                var size = F.Preferred(button, width: 0);
                size.minWidth = 0; size.flexibleWidth = 1;
            }
            view.btnRePull1Label.text = "1회 더 뽑기";
            view.btnRePullNLabel.text = "10회 더 뽑기";
        }

        private static void StyleButton(Button button, bool primary)
        {
            var image = button.GetComponent<Image>();
            image.sprite = UguiGenAssets.KitBtnGrey; image.type = Image.Type.Sliced;
            image.color = primary ? UguiTheme.ActionGold : UguiTheme.RusticSurface;
            button.targetGraphic = image;
            var flare = button.GetComponent<GachaButtonFlare>();
            foreach (var child in button.GetComponentsInChildren<Image>(true))
                if (child != image && (flare == null || flare.wisps == null || !flare.wisps.Contains(child))) child.enabled = false;
            var label = button.GetComponentInChildren<TMP_Text>(true);
            label.fontSize = primary ? 36 : 42;
            label.fontStyle = FontStyles.Normal; label.fontSharedMaterial = F.Font.material;
            label.color = primary ? UguiTheme.InfoInk : UguiTheme.TextPrimary;
            label.textWrappingMode = TextWrappingModes.Normal; label.overflowMode = TextOverflowModes.Ellipsis;
            F.Preferred(button, height: 180).minHeight = 132;
        }

        [MenuItem("KingdomIdle/UGUI/Validate bronze gacha result")]
        public static void Validate()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Use an isolated Edit Mode session.");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Path);
            var catalog = AssetDatabase.LoadAssetAtPath<UIViewCatalog>(PrefabGenUtil.CatalogPath);
            // UGUI는 비활성 LayoutGroup을 ForceRebuild에서도 제외한다. 공개/거래를 실행하지 않는 활성 기하 fixture를 쓴다.
            var fixture = new GameObject("BronzeGachaResultAcceptance", typeof(RectTransform));
            fixture.hideFlags = HideFlags.HideAndDontSave;
            var root = (RectTransform)fixture.transform; root.sizeDelta = new Vector2(1080, 1920);
            try
            {
                foreach (int count in new[] { 1, 2, 3, 10 })
                {
                    var popup = Object.Instantiate(prefab, root, false);
                    try
                    {
                        var view = popup.GetComponent<GachaResultPopupView>();
                        if (view.box.GetComponent<Image>().color.a != 1 || view.btnRePull1Label.color != UguiTheme.InfoInk ||
                            view.btnRePullNLabel.color != UguiTheme.InfoInk)
                            throw new InvalidOperationException("Result panel must be opaque and re-pull labels must use dark ink.");
                        view.btnRePullN.gameObject.SetActive(count > 1);
                        view.SetResultCount(count);
                        for (int i = 0; i < count; i++)
                        {
                            var card = Object.Instantiate(catalog.itemGachaCard, view.grid, false).GetComponent<GachaCardItemView>();
                            card.nameLabel.text = "보석 지팡이"; card.subLabel.text = "에픽 장비\nx10";
                            card.SetResultPresentation(count);
                        }
                        view.RefreshLayoutNow();
                        LayoutRebuilder.ForceRebuildLayoutImmediate(view.box);
                        var repeats = new[] { view.btnRePull1Label, view.btnRePullNLabel }.Where(x => x.gameObject.activeInHierarchy);
                        foreach (var label in repeats)
                            if (label.GetPreferredValues(label.text.Split('\n')[0]).x > label.rectTransform.rect.width - 16)
                                throw new InvalidOperationException("A result re-pull title must fit on one line: " + label.text);
                        if (count > 1 && Mathf.Abs(((RectTransform)view.btnDone.transform).rect.width - ((RectTransform)view.btnRePullN.transform).rect.width) > 1)
                            throw new InvalidOperationException("Result actions must share the available width equally.");
                        var cards = view.grid.GetComponentsInChildren<GachaCardItemView>(true);
                        if (cards.Length != count || cards.Any(card => card.nameLabel.fontSize < 42 || card.subLabel.text != "에픽 장비\nx10"))
                            throw new InvalidOperationException("Result card names or quantities were lost while sizing.");
                        if (count <= 3)
                        {
                            if (view.scroll.vertical) throw new InvalidOperationException("One to three result cards must fit without scrolling at 1080x1920. " +
                                "count=" + count + " root=" + ((RectTransform)view.transform).rect.size + " box=" + view.box.rect.size +
                                " scroll=" + ((RectTransform)view.scroll.transform).rect.size + " viewport=" + view.scroll.viewport.rect.size +
                                " content=" + view.scroll.content.rect.size + " grid=" + view.grid.rect.size +
                                " cell=" + view.grid.GetComponent<GridLayoutGroup>().cellSize);
                            foreach (var card in cards) RequireInside(view, (RectTransform)card.transform, count);
                        }
                        else if (!view.scroll.vertical || view.grid.GetComponent<GridLayoutGroup>().constraintCount != 3)
                            throw new InvalidOperationException("Ten results need readable three-column internal scrolling.");
                    }
                    finally { Object.DestroyImmediate(popup); }
                }
                if (GachaCardReveal.DurationFor(eEquipmentRarity.Normal) != .55f || GachaCardReveal.DurationFor(eEquipmentRarity.Rare) != 1 ||
                    GachaCardReveal.DurationFor(eEquipmentRarity.Epic) != 2)
                    throw new InvalidOperationException("Equipment reveal timing changed.");
                Debug.Log("BRONZE GACHA RESULT VALIDATION PASSED: opaque panel, dark-ink actions, 1/2/3 centered cards, 10-card readable scroll, content/timing preserved");
            }
            finally { Object.DestroyImmediate(fixture); }
        }

        private static void RequireInside(GachaResultPopupView view, RectTransform target, int count)
        {
            var viewport = view.scroll.viewport;
            var corners = new Vector3[4]; target.GetWorldCorners(corners);
            foreach (var corner in corners)
            {
                var point = viewport.InverseTransformPoint(corner);
                if (point.x < viewport.rect.xMin - 1 || point.x > viewport.rect.xMax + 1 || point.y < viewport.rect.yMin - 1 || point.y > viewport.rect.yMax + 1)
                    throw new InvalidOperationException("Featured result card is outside its viewport. count=" + count +
                        " target=" + target.name + " targetSize=" + target.rect.size + " point=" + point.ToString("F2") +
                        " viewport=" + viewport.rect + " box=" + view.box.rect.size + " scroll=" + ((RectTransform)view.scroll.transform).rect.size +
                        " content=" + view.scroll.content.rect + " contentPosition=" + view.scroll.content.anchoredPosition +
                        " grid=" + view.grid.rect + " gridPosition=" + view.grid.anchoredPosition +
                        " cell=" + view.grid.GetComponent<GridLayoutGroup>().cellSize);
            }
        }
    }
}
