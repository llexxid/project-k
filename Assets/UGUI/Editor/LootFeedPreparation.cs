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
            root.sizeDelta = new Vector2(626f, 258f);
            var view = root.gameObject.AddComponent<LootFeedView>();
            view.group = root.gameObject.AddComponent<CanvasGroup>();
            view.group.interactable = view.group.blocksRaycasts = false;
            view.group.alpha = 0f;
            view.rows = new LootFeedView.Row[LootFeedView.Capacity];
            for (int i = 0; i < view.rows.Length; i++)
            {
                var wood = F.Box(root, "Row" + i, UguiTheme.RusticPanelDeep);
                var rect = wood.rectTransform;
                rect.anchorMin = new Vector2(0f, 1f); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, 1f);
                rect.anchoredPosition = new Vector2(0f, -i * (LootFeedView.RowHeight + LootFeedView.RowGap));
                rect.sizeDelta = new Vector2(0f, LootFeedView.RowHeight);
                var face = F.Box(rect, "InfoSurface", UguiTheme.InfoSurface);
                F.Stretch(face.rectTransform); face.rectTransform.offsetMin = new Vector2(3f, 3f); face.rectTransform.offsetMax = new Vector2(-3f, -3f);
                var frame = F.Frame(rect, "RarityBorder", UguiTheme.Bronze);
                var icon = F.Box(rect, "Icon", Color.white, rounded: false);
                icon.preserveAspect = true;
                Place(icon.rectTransform, new Vector2(0f, .5f), new Vector2(.5f, .5f), new Vector2(40f, 0f), new Vector2(48f, 48f));
                var name = F.Text(rect, "Name", "", 30f, UguiTheme.InfoInk, TextAlignmentOptions.MidlineLeft);
                name.richText = false;
                name.rectTransform.anchorMin = new Vector2(0f, 1f); name.rectTransform.anchorMax = Vector2.one;
                name.rectTransform.pivot = new Vector2(.5f, 1f); name.rectTransform.offsetMin = new Vector2(76f, -43f); name.rectTransform.offsetMax = new Vector2(-152f, -7f);
                var quantity = F.Text(rect, "Quantity", "", 32f, UguiTheme.InfoInk, TextAlignmentOptions.MidlineRight);
                quantity.enableAutoSizing = true; quantity.fontSizeMin = 26f; quantity.fontSizeMax = 32f;
                Place(quantity.rectTransform, new Vector2(1f, .5f), new Vector2(1f, .5f), new Vector2(-14f, 0f), new Vector2(136f, 54f));
                var state = F.Text(rect, "StorageState", "", 24f, UguiTheme.InfoMuted, TextAlignmentOptions.MidlineLeft);
                state.rectTransform.anchorMin = new Vector2(0f, 0f); state.rectTransform.anchorMax = new Vector2(1f, 0f);
                state.rectTransform.pivot = new Vector2(.5f, 0f); state.rectTransform.offsetMin = new Vector2(76f, 6f); state.rectTransform.offsetMax = new Vector2(-152f, 35f);
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
