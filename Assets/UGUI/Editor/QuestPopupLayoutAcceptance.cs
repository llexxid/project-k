using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>계정이나 씬 저장 없이 높이 모드 격리·화면 비율·슬라이드 중 경계를 검사한다.</summary>
    public static class QuestPopupLayoutAcceptance
    {
        public static object Run()
        {
            var checks = new List<string>();
            void Check(bool value, string label)
            {
                if (!value) throw new InvalidOperationException(label);
                checks.Add(label);
            }
            var prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/UGUI/Prefabs/Panels" })
                .Select(AssetDatabase.GUIDToAssetPath).ToArray();
            foreach (string path in prefabs)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                foreach (var fitter in prefab.GetComponentsInChildren<SheetSizeFitter>(true))
                    Check(fitter.heightMode == (path.EndsWith("/Panel_Guide.prefab") ? SheetSizeFitter.HeightMode.AvailableSpace : SheetSizeFitter.HeightMode.Preferred),
                        path + " retains its intended height mode");
            }
            var guide = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UGUI/Prefabs/Panels/Panel_Guide.prefab").GetComponent<GuidePanelView>();
            var serialized = new SerializedObject(guide);
            var oldCard = serialized.FindProperty("currentQuestRoot").objectReferenceValue as GameObject;
            Check(oldCard != null && !oldCard.activeSelf, "Popup guide card is disabled with its reference preserved");
            var main = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UGUI/Prefabs/Screens/Screen_Main.prefab");
            Check(main.GetComponentsInChildren<GuideGoalView>(true).Any(x => x.gameObject.activeSelf), "In-game guide card remains present and enabled in the main prefab");

            var root = new GameObject("QuestLayoutGeometry", typeof(RectTransform));
            root.SetActive(false);
            try
            {
                var rootRect = (RectTransform)root.transform;
                var manager = root.AddComponent<UIManager>();
                var settings = new SerializedObject(manager);
                foreach (bool expanded in new[] { true, false })
                {
                    settings.FindProperty("expandLegacyTabPanels").boolValue = expanded;
                    settings.ApplyModifiedPropertiesWithoutUndo();
                    foreach (var id in new[] { KingdomIdle.UI.UIPanelId.Development, KingdomIdle.UI.UIPanelId.KingdomArmy, KingdomIdle.UI.UIPanelId.Dungeon })
                        Check(manager.IsExpandedPanel(id) == expanded, id + " retains opt-out to its original layout");
                    Check(manager.IsExpandedPanel(KingdomIdle.UI.UIPanelId.Gacha) && manager.IsExpandedPanel(KingdomIdle.UI.UIPanelId.Store),
                        "Gacha and store keep their expanded layout with legacy expansion " + expanded);
                }
                RectTransform Child(string name, Transform parent)
                {
                    var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
                    rect.SetParent(parent, false); return rect;
                }
                var clip = Child("SheetClip", rootRect);
                clip.anchorMin = Vector2.zero; clip.anchorMax = Vector2.one;
                clip.offsetMin = new Vector2(0, UguiTheme.BottomBarHeight); clip.offsetMax = Vector2.zero;
                var hud = Child("HudTop", rootRect);
                hud.anchorMin = new Vector2(0, 1); hud.anchorMax = Vector2.one; hud.pivot = new Vector2(.5f, 1);
                hud.sizeDelta = new Vector2(0, UguiTheme.HudTopHeight); hud.anchoredPosition = Vector2.zero;
                var sheet = Child("Sheet", clip);
                sheet.anchorMin = Vector2.zero; sheet.anchorMax = new Vector2(1, 0); sheet.pivot = new Vector2(.5f, 0);
                sheet.anchoredPosition = Vector2.zero; sheet.sizeDelta = new Vector2(0, 960);
                var fitter = sheet.gameObject.AddComponent<SheetSizeFitter>();
                fitter.heightMode = SheetSizeFitter.HeightMode.AvailableSpace;
                fitter.SetTopBoundary(null);
                Check(Mathf.Approximately(sheet.rect.height, 960), "Missing HUD preserves the existing sheet height");
                foreach (float height in new[] { 1920f, 2340f, 1440f })
                {
                    rootRect.sizeDelta = new Vector2(1080, height);
                    sheet.anchoredPosition = Vector2.zero;
                    fitter.SetTopBoundary(hud);
                    float expected = height - UguiTheme.HudTopHeight - UguiTheme.BottomBarHeight - 24;
                    Check(Mathf.Abs(sheet.rect.height - expected) < .01f, height + " viewport fills exactly the available space");
                    sheet.anchoredPosition = new Vector2(0, -400);
                    fitter.SetTopBoundary(hud);
                    Check(Mathf.Abs(sheet.rect.height - expected) < .01f, height + " slide position does not alter the sheet height");
                    fitter.heightMode = SheetSizeFitter.HeightMode.Preferred;
                    fitter.preferredHeight = 960;
                    fitter.SetTopBoundary(hud);
                    Check(Mathf.Abs(sheet.rect.height - Mathf.Clamp(clip.rect.height - UguiTheme.StageControlsBottom - 24, 380, 960)) < .01f,
                        height + " default mode keeps the original calculation even when a HUD is supplied");
                    fitter.heightMode = SheetSizeFitter.HeightMode.AvailableSpace;
                }
                var panel = root.AddComponent<BottomSheetView>();
                panel.sheet = sheet;
                var backdrop = Child("Backdrop", rootRect);
                backdrop.anchorMin = Vector2.zero; backdrop.anchorMax = Vector2.one;
                panel.backdrop = backdrop.gameObject.AddComponent<UnityEngine.UI.Button>();
                fitter.heightMode = SheetSizeFitter.HeightMode.BelowPartyHud;
                foreach (float height in new[] { 1440f, 1920f, 2340f })
                {
                    rootRect.sizeDelta = new Vector2(1080, height);
                    sheet.anchoredPosition = Vector2.zero;
                    fitter.SetTopBoundary(hud);
                    float partyHeight = PartyHudController.Instance != null && PartyHudController.Instance.HudRect != null
                        ? PartyHudController.Instance.HudRect.rect.height : UguiTheme.PartyHudHeight;
                    float expected = height - UguiTheme.HudTopHeight - UguiTheme.BottomBarHeight - partyHeight;
                    Check(Mathf.Abs(sheet.rect.height - expected) < .01f, height + " expanded panel reserves both HUDs and navigation");
                    Check(backdrop.offsetMin == clip.offsetMin && backdrop.offsetMax == clip.offsetMax,
                        height + " expanded panel input stays inside its content region");
                    sheet.anchoredPosition = new Vector2(0, -400);
                    fitter.SetTopBoundary(hud);
                    Check(Mathf.Abs(sheet.rect.height - expected) < .01f, height + " expanded panel slide keeps stable geometry");
                }
            }
            finally { Object.DestroyImmediate(root); }
            return new { passed = checks.Count, checks };
        }
    }
}
