using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using KingdomIdle.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>계정·서버·씬 저장 없이 청동 원정대의 공통 화면 경계와 입력 상태를 검사한다.</summary>
    public static class BronzeUiAcceptance
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;
        private static readonly UIPanelId[] BottomMenus = {
            UIPanelId.Development, UIPanelId.KingdomArmy, UIPanelId.Dungeon, UIPanelId.Gacha, UIPanelId.Store
        };

        [MenuItem("KingdomIdle/UGUI/Validate bronze common UI")]
        public static void Validate() => Debug.Log(Newtonsoft.Json.JsonConvert.SerializeObject(Run(), Newtonsoft.Json.Formatting.Indented));

        public static object Run()
        {
            if (Application.isPlaying || UIManager.Instance != null || PartyHudController.Instance != null ||
                StaticField(typeof(InventoryPanelController), "_view").GetValue(null) != null ||
                ((System.Collections.IDictionary)StaticField(typeof(InventoryPanelController), "_currencySlots").GetValue(null)).Count != 0)
                throw new InvalidOperationException("Run bronze UI acceptance in Edit Mode without an active UI or party HUD. No scene or account will be opened.");
            var checks = new List<string>();
            void Check(bool value, string label)
            {
                if (!value) throw new InvalidOperationException("Bronze UI acceptance: " + label);
                checks.Add(label);
            }
            ValidatePrefabs(Check);
            ValidateInventoryPresentation(Check);
            ValidateGeometry(Check);
            ValidateInventoryBack(Check);
            ValidateInactivePulse(Check);
            return new { passed = checks.Count, checks, pending = new[] {
                "Play Mode: actual menu/inventory stack and Android-back routing",
                "Play Mode: graphic pulse timing, unscaled time, low-spec switch and disable/re-enable",
                "Unity Game View and Android: visual layout, safe areas, scrolling and touch"
            } };
        }

        private static void ValidatePrefabs(Action<bool, string> check)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<UIViewCatalog>(PrefabGenUtil.CatalogPath);
            check(catalog != null, "UIViewCatalog exists");
            var root = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UGUI/Prefabs/UGUI_UIRoot.prefab");
            check(root != null && root.GetComponent<UIManager>() != null, "Generated UI root retains its manager");
            var settings = new SerializedObject(root.GetComponent<UIManager>());
            check(settings.FindProperty("expandLegacyTabPanels").boolValue, "Generated UI root enables all five expanded bottom menus");
            var panels = new[] { catalog.panelDevelopment, catalog.panelKingdomArmy, catalog.panelDungeon,
                catalog.panelGacha, catalog.popupShop, catalog.panelInventory };
            foreach (var panel in panels)
            {
                check(panel != null, "All five menu prefabs and the inventory popup are assigned");
                var view = panel.GetComponent<BottomSheetView>();
                check(view != null && view.sheet != null && view.backdrop != null && view.closeButton != null && view.title != null,
                    panel.name + " retains its sheet, input blocker and close/title references");
                check(view.sheet.GetComponent<SheetSizeFitter>() != null && view.sheet.parent.GetComponent<RectMask2D>() != null,
                    panel.name + " retains a fitted and clipped content area");
                check(panel.GetComponentsInChildren<CenteredPanelSize>(true).Length == 0 &&
                    panel.GetComponentsInChildren<PartyHudSuppressor>(true).Length == 0,
                    panel.name + " has one size owner and preserves the compact party HUD");
                var scroll = panel.GetComponentsInChildren<ScrollRect>(true);
                check(scroll.Length > 0 && scroll.All(x => x.viewport != null && x.content != null), panel.name + " retains its internal scrolling references");
                ValidateReferences(panel, check);
            }
            var main = catalog.screenMain != null ? catalog.screenMain.GetComponent<MainScreenView>() : null;
            check(main != null && main.tabStore != null, "Main screen retains the fifth bottom shop tab");
            var equipment = catalog.panelKAEquipment != null ? catalog.panelKAEquipment.GetComponent<KAEquipmentView>() : null;
            check(equipment != null && equipment.equippedIcon != null && equipment.inventoryGrid != null &&
                equipment.recommendButton != null && equipment.settingsButton != null, "Equipment retains authored image, owned grid and explicit actions");
            var gacha = catalog.panelGacha.GetComponent<GachaPanelView>().authoredContent;
            check(gacha != null && gacha.featuredIcons?.Length == 3 && gacha.featuredNames?.Length == 3 &&
                gacha.featuredFrames?.Length == 3 && gacha.ticketHint != null && gacha.skillRule != null,
                "Gacha retains fixed real-reward preview cells and ticket/skill-rule references");
            ValidateReferences(root, check);
        }

        private static void ValidateReferences(GameObject prefab, Action<bool, string> check)
        {
            int references = 0;
            foreach (var component in prefab.GetComponentsInChildren<Component>(true))
            {
                if (component == null) throw new InvalidOperationException("Missing script: " + prefab.name);
                var iterator = new SerializedObject(component).GetIterator();
                while (iterator.NextVisible(true))
                    if (iterator.propertyType == SerializedPropertyType.ObjectReference)
                    {
                        references++;
                        if (iterator.objectReferenceValue == null && iterator.objectReferenceInstanceIDValue != 0)
                            throw new InvalidOperationException("Missing reference: " + prefab.name + "/" + iterator.propertyPath);
                    }
            }
            check(references > 0, prefab.name + " scripts and " + references + " serialized object references resolve");
        }

        private static void ValidateInventoryPresentation(Action<bool, string> check)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<UIViewCatalog>(PrefabGenUtil.CatalogPath);
            var fixture = new GameObject("BronzeInventoryPresentation", typeof(RectTransform));
            fixture.hideFlags = HideFlags.HideAndDontSave;
            ((RectTransform)fixture.transform).sizeDelta = new Vector2(1000, 1600);
            try
            {
                var detail = Object.Instantiate(catalog.itemInventoryEquipDetail, fixture.transform, false).GetComponent<InventoryEquipDetailView>();
                var rect = (RectTransform)detail.transform;
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 960);
                LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, LayoutUtility.GetPreferredHeight(rect));
                LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
                foreach (var button in new[] { detail.detailButton, detail.enhanceButton })
                    check(((RectTransform)button.transform).rect.height >= 132,
                        "Inventory " + button.name + " keeps a visible label and at least 132px touch height inside its action row");
                var card = Object.Instantiate(catalog.itemEquipCell, fixture.transform, false).GetComponent<EquipCellView>();
                var cardRect = (RectTransform)card.transform;
                cardRect.sizeDelta = new Vector2(225, 284);
                card.Set(null, "엑스칼리버 +15", UguiTheme.TextPrimary, "ATK +999K", UguiTheme.Bronze, true, false, "장착 중");
                LayoutRebuilder.ForceRebuildLayoutImmediate(cardRect);
                check(LayoutUtility.GetPreferredHeight(cardRect) <= 284.1f,
                    "Equipment icon, two-line name, attack and equipped status fit together in the shared 284px card");
                foreach (var label in new[] { card.nameLabel, card.subLabel, card.stateLabel })
                    check(label.rectTransform.rect.height + 1 >= label.GetPreferredValues(label.text, label.rectTransform.rect.width, float.PositiveInfinity).y,
                        "Equipment card label fits without vertical clipping: " + label.text + " rect=" + label.rectTransform.rect.size +
                        " preferred=" + label.GetPreferredValues(label.text, label.rectTransform.rect.width, float.PositiveInfinity));
            }
            finally { Object.DestroyImmediate(fixture); }
        }

        private static void ValidateGeometry(Action<bool, string> check)
        {
            var root = new GameObject("BronzeUiAcceptance_Geometry", typeof(RectTransform));
            root.SetActive(false);
            try
            {
                var rect = (RectTransform)root.transform;
                var manager = root.AddComponent<UIManager>();
                var setting = new SerializedObject(manager);
                foreach (bool expanded in new[] { false, true })
                {
                    setting.FindProperty("expandLegacyTabPanels").boolValue = expanded;
                    setting.ApplyModifiedPropertiesWithoutUndo();
                    foreach (var id in BottomMenus)
                        check(manager.IsExpandedPanel(id) == (expanded || id == UIPanelId.Gacha || id == UIPanelId.Store),
                            id + " honors the expansion switch " + expanded);
                    check(manager.IsExpandedPanel(UIPanelId.Inventory), "Inventory remains large with expansion switch " + expanded);
                }
                var view = root.AddComponent<BottomSheetView>();
                var clip = Child("SheetClip", rect); Stretch(clip);
                clip.gameObject.AddComponent<RectMask2D>();
                var sheet = Child("Sheet", clip);
                sheet.anchorMin = Vector2.zero; sheet.anchorMax = new Vector2(1, 0); sheet.pivot = new Vector2(.5f, 0);
                sheet.sizeDelta = new Vector2(0, 600); sheet.anchoredPosition = Vector2.zero;
                view.sheet = sheet;
                var backdrop = Child("Backdrop", rect); Stretch(backdrop);
                view.backdrop = backdrop.gameObject.AddComponent<Button>();
                var topHud = Child("HudTop", rect);
                topHud.anchorMin = new Vector2(0, 1); topHud.anchorMax = Vector2.one; topHud.pivot = new Vector2(.5f, 1);
                topHud.sizeDelta = new Vector2(0, UguiTheme.HudTopHeight); topHud.anchoredPosition = Vector2.zero;
                var partyRect = Child("PartyHud", rect);
                partyRect.sizeDelta = new Vector2(800, UguiTheme.PartyHudHeight);
                var partyView = partyRect.gameObject.AddComponent<PartyHudView>(); partyView.rect = partyRect;
                var party = partyRect.gameObject.AddComponent<PartyHudController>();
                InstanceField(party.GetType(), "_view").SetValue(party, partyView);
                SetParty(party);
                var fitter = sheet.gameObject.AddComponent<SheetSizeFitter>();
                fitter.heightMode = SheetSizeFitter.HeightMode.BelowPartyHud;
                fitter.SetTopBoundary(null);
                check(Mathf.Approximately(sheet.rect.height, 600), "Missing top HUD preserves existing sheet height");
                foreach (var size in new[] { new Vector2(1080, 1920), new Vector2(1080, 2400), new Vector2(1080, 1440), new Vector2(1440, 1920) })
                {
                    rect.sizeDelta = size;
                    foreach (float inset in new[] { 0f, 24f })
                    {
                        fitter.horizontalInset = fitter.verticalInset = inset;
                        fitter.SetTopBoundary(topHud);
                        CheckRegion(check, rect, sheet, backdrop, topHud.rect.height, partyRect.rect.height, inset, size + " inset " + inset);
                        var offsets = new[] { clip.offsetMin, clip.offsetMax, sheet.sizeDelta };
                        TickFitter(fitter); TickFitter(fitter);
                        check(offsets[0] == clip.offsetMin && offsets[1] == clip.offsetMax && offsets[2] == sheet.sizeDelta,
                            size + " unchanged frames keep stable geometry at inset " + inset);
                        float height = sheet.rect.height;
                        sheet.anchoredPosition = new Vector2(0, -320); TickFitter(fitter);
                        check(Mathf.Approximately(sheet.rect.height, height), size + " slide position does not change available height");
                        sheet.anchoredPosition = Vector2.zero;
                    }
                }
                // Do not rebind: these changes exercise the runtime cache invalidation path.
                rect.sizeDelta = new Vector2(1080, 2400); TickFitter(fitter);
                CheckRegion(check, rect, sheet, backdrop, topHud.rect.height, partyRect.rect.height, 24, "Resize without reopening");
                topHud.sizeDelta += new Vector2(0, 64); TickFitter(fitter);
                CheckRegion(check, rect, sheet, backdrop, topHud.rect.height, partyRect.rect.height, 24, "Top HUD height changes without reopening");
                partyRect.sizeDelta += new Vector2(0, 72); TickFitter(fitter);
                CheckRegion(check, rect, sheet, backdrop, topHud.rect.height, partyRect.rect.height, 24, "Party HUD height changes without reopening");
            }
            finally { SetParty(null); Object.DestroyImmediate(root); }
        }

        private static void CheckRegion(Action<bool, string> check, RectTransform root, RectTransform sheet, RectTransform backdrop,
            float topHeight, float partyHeight, float inset, string label)
        {
            var body = Bounds(root, sheet);
            check(Near(body.min.x, root.rect.xMin + inset) && Near(body.max.x, root.rect.xMax - inset) &&
                Near(body.min.y, root.rect.yMin + UguiTheme.BottomBarHeight + inset) &&
                Near(body.max.y, root.rect.yMax - topHeight - partyHeight - inset), label + " sheet fills exactly below both HUDs and above navigation");
            var blocker = Bounds(root, backdrop);
            check(Near(blocker.min.x, root.rect.xMin) && Near(blocker.max.x, root.rect.xMax) &&
                Near(blocker.min.y, root.rect.yMin + UguiTheme.BottomBarHeight) &&
                Near(blocker.max.y, root.rect.yMax - topHeight - partyHeight), label + " input blocker covers the popup margin without blocking HUD/navigation");
        }

        private static void ValidateInventoryBack(Action<bool, string> check)
        {
            var type = typeof(InventoryPanelController);
            var fields = new[] { "_view", "_detailItemId", "_activeTab", "_equipmentDirty" }.Select(name => StaticField(type, name)).ToArray();
            var saved = fields.Select(field => field.GetValue(null)).ToArray();
            var root = new GameObject("BronzeUiAcceptance_InventoryBack", typeof(RectTransform)); root.SetActive(false);
            try
            {
                var view = root.AddComponent<InventoryPanelView>(); view.content = Child("Content", root.transform);
                fields[0].SetValue(null, view); fields[1].SetValue(null, null);
                var selectedTab = Enum.Parse(fields[2].FieldType, "Etc"); fields[2].SetValue(null, selectedTab);
                check(!InventoryPanelController.HandleBack(view), "Inventory list leaves back input for the outer popup close route");
                fields[1].SetValue(null, "fixture-equipment");
                check(!InventoryPanelController.HandleBack(null) && (string)fields[1].GetValue(null) == "fixture-equipment",
                    "An unrelated inventory view cannot consume or clear the current detail");
                check(InventoryPanelController.HandleBack(view) && fields[1].GetValue(null) == null && Equals(fields[2].GetValue(null), selectedTab),
                    "Detail back is consumed once, clears detail and preserves selected category without an account");
                check(!InventoryPanelController.HandleBack(view), "The next back input reaches popup close after returning from detail");
            }
            finally
            {
                for (int i = 0; i < fields.Length; i++) fields[i].SetValue(null, saved[i]);
                Object.DestroyImmediate(root);
            }
        }

        private static void ValidateInactivePulse(Action<bool, string> check)
        {
            var root = new GameObject("BronzeUiAcceptance_InactivePulse", typeof(RectTransform)); root.SetActive(false);
            try
            {
                var image = root.AddComponent<Image>();
                Color dim = UguiTheme.Bronze, bright = UguiTheme.ActionGold;
                UITween.PulseGraphic(image, dim, bright);
                UITween.PulseGraphic(image, dim, bright);
                check(image.color == bright && root.GetComponents<UITween>().Length == 1,
                    "Repeated pulse requests on an inactive claim border keep one tween and visible ready information");
                UITween.StopGraphicPulse(image);
                check(image.color == dim, "Stopping an inactive pulse restores the unavailable border color");
                UITween.PulseGraphic(image, dim, UguiTheme.RoyalBlue);
                check(image.color == UguiTheme.RoyalBlue && root.GetComponents<UITween>().Length == 1,
                    "A reused border accepts the next ready color without another tween");
                UITween.StopGraphicPulse(image);
                UITween.StopGraphicPulse(image);
                UITween.StopGraphicPulse(null);
                check(image.color == dim, "Repeated and missing-target pulse stops are safe");
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static RectTransform Child(string name, Transform parent)
        { var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform; rect.SetParent(parent, false); return rect; }
        private static void Stretch(RectTransform rect)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        private static bool Near(float actual, float expected) => Mathf.Abs(actual - expected) <= .05f;
        private static Bounds Bounds(RectTransform root, RectTransform target)
        {
            var corners = new Vector3[4]; target.GetWorldCorners(corners);
            var bounds = new Bounds(root.InverseTransformPoint(corners[0]), Vector3.zero);
            for (int i = 1; i < corners.Length; i++) bounds.Encapsulate(root.InverseTransformPoint(corners[i]));
            return bounds;
        }
        private static FieldInfo StaticField(Type type, string name) => type.GetField(name, PrivateStatic) ?? throw new MissingFieldException(type.FullName, name);
        private static FieldInfo InstanceField(Type type, string name) => type.GetField(name, PrivateInstance) ?? throw new MissingFieldException(type.FullName, name);
        private static void SetParty(PartyHudController party) => typeof(PartyHudController).GetProperty("Instance").GetSetMethod(true).Invoke(null, new object[] { party });
        private static void TickFitter(SheetSizeFitter fitter) => typeof(SheetSizeFitter).GetMethod("LateUpdate", PrivateInstance).Invoke(fitter, null);
    }
}
