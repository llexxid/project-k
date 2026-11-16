#if UNITY_EDITOR
using System;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>공용 에셋만 재사용하는 표시 전용 3행 프리팹. 실행은 조율 담당이 수행한다.</summary>
    public static class LootFeedPreparation
    {
        public const string PrefabPath = "Assets/UGUI/Prefabs/Hud_LootFeed.prefab";

        [MenuItem("KingdomIdle/UGUI/획득 피드 준비")]
        public static void Prepare()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<UIViewCatalog>(PrefabGenUtil.CatalogPath);
            if (catalog == null) throw new InvalidOperationException("UIViewCatalog is required.");
            F.Init(); F.Catalog = catalog;
            var root = F.Root("Hud_LootFeed");
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0f, 1f);
            root.sizeDelta = new Vector2(376f, 158f);
            var view = root.gameObject.AddComponent<LootFeedView>();
            view.group = root.gameObject.AddComponent<CanvasGroup>();
            view.group.interactable = view.group.blocksRaycasts = false;
            view.group.alpha = 0f;
            view.rows = new LootFeedView.Row[LootFeedView.Capacity];
            for (int i = 0; i < view.rows.Length; i++)
            {
                var background = UguiTheme.RusticPanelDeep; background.a = .56f;
                var wood = F.Box(root, "Row" + i, background);
                var rect = wood.rectTransform;
                rect.anchorMin = new Vector2(0f, 1f); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, 1f);
                rect.anchoredPosition = new Vector2(0f, -i * (LootFeedView.RowHeight + LootFeedView.RowGap));
                rect.sizeDelta = new Vector2(0f, LootFeedView.RowHeight);
                var border = UguiTheme.Bronze; border.a = LootFeedView.BorderOpacity;
                var frame = F.Frame(rect, "RarityBorder", border);
                var icon = F.Box(rect, "Icon", new Color(1, 1, 1, .9f), rounded: false);
                icon.preserveAspect = true;
                Place(icon.rectTransform, new Vector2(0f, .5f), new Vector2(.5f, .5f), new Vector2(26f, 0f), new Vector2(32f, 32f));
                var name = F.Text(rect, "Name", "", 24f, new Color(.94f, .89f, .78f, .9f), TextAlignmentOptions.MidlineLeft);
                name.richText = false;
                name.enableAutoSizing = true; name.fontSizeMin = 22f; name.fontSizeMax = 24f;
                name.textWrappingMode = TextWrappingModes.NoWrap; name.overflowMode = TextOverflowModes.Ellipsis;
                name.rectTransform.anchorMin = new Vector2(0f, 1f); name.rectTransform.anchorMax = Vector2.one;
                name.rectTransform.pivot = new Vector2(.5f, 1f); name.rectTransform.offsetMin = new Vector2(52f, -28f); name.rectTransform.offsetMax = new Vector2(-142f, 0f);
                var quantity = F.Text(rect, "Quantity", "", 26f, new Color(.98f, .94f, .84f, .96f), TextAlignmentOptions.MidlineRight);
                quantity.enableAutoSizing = true; quantity.fontSizeMin = 22f; quantity.fontSizeMax = 26f;
                Place(quantity.rectTransform, new Vector2(1f, .5f), new Vector2(1f, .5f), new Vector2(-10f, 0f), new Vector2(124f, 40f));
                var state = F.Text(rect, "StorageState", "", 18f, new Color(.94f, .89f, .78f, .7f), TextAlignmentOptions.MidlineLeft);
                state.rectTransform.anchorMin = new Vector2(0f, 0f); state.rectTransform.anchorMax = new Vector2(1f, 0f);
                state.rectTransform.pivot = new Vector2(.5f, 0f); state.rectTransform.offsetMin = new Vector2(52f, 0f); state.rectTransform.offsetMax = new Vector2(-142f, 22f);
                view.rows[i] = new LootFeedView.Row { rect = rect, group = rect.gameObject.AddComponent<CanvasGroup>(), icon = icon, border = frame, name = name, quantity = quantity, state = state };
                view.rows[i].group.interactable = view.rows[i].group.blocksRaycasts = false;
                rect.gameObject.SetActive(false);
            }
            catalog.lootFeed = PrefabGenUtil.SavePrefab(root.gameObject, PrefabPath);
            EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets();
            Debug.Log("[LootFeedPreparation] 표시 전용 3행 피드 준비 완료. 획득·수령·저장 경로 변경 없음.");
        }

        private static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        { rect.anchorMin = rect.anchorMax = anchor; rect.pivot = pivot; rect.anchoredPosition = position; rect.sizeDelta = size; }
    }
}
#endif
