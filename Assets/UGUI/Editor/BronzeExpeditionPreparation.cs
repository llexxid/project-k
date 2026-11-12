using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>청동 원정대의 공통 셸과 기존 화면을 갱신한다. 게임 데이터와 컨트롤러 참조는 보존한다.</summary>
    public static class BronzeExpeditionPreparation
    {
        private const string Root = "Assets/UGUI/Prefabs/";
        private static Sprite Rounded => AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UGUI/Sprites/RoundedRect.png");

        [MenuItem("KingdomIdle/UGUI/청동 원정대/전체 개편 적용")]
        public static void Apply()
        {
            EquipmentFeaturePreparation.PrepareBronzeEquipmentUi();
            GachaPanelPrefabGens.Prepare();
            ShopPopupPrefabGen.ApplyPanelLayout();
            DevelopmentPanelPrefabGens.ApplyBronzeGrowth();
            QuestCardPrefabBuilder.Apply();
            Edit("Screens/Screen_Main.prefab", GuidePanelPrefabGens.ApplyGuideGoal);
            ApplyCommon();
            BronzeEquipmentIconPreparation.ValidateOrThrow();
            EquipmentFeaturePreparation.ValidateBronzeEquipmentUi();
            GachaPanelPrefabGens.ValidateBronzeUi();
            DevelopmentPanelPrefabGens.ValidateBronzeGrowth();
            QuestCardPrefabBuilder.ValidateBronzeCards();
        }

        [MenuItem("KingdomIdle/UGUI/청동 원정대/공통 화면 적용")]
        public static void ApplyCommon()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("플레이 모드를 종료해 주세요.");
            F.Init();
            F.Catalog = PrefabGenUtil.GetOrCreateCatalog();
            InventoryPanelPrefabGens.GenerateAll();
            Edit("UGUI_UIRoot.prefab", go =>
            {
                var manager = go.GetComponentInChildren<UIManager>(true);
                if (manager == null) throw new InvalidOperationException("UIManager 참조 누락");
                var so = new SerializedObject(manager);
                so.FindProperty("expandLegacyTabPanels").boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();
            });
            foreach (string path in new[] { "Panel_Development", "Panel_KingdomArmy", "Panel_Dungeon", "Panel_Gacha", "Panel_Inventory", "Panel_Guide" })
                Edit("Panels/" + path + ".prefab", ApplyShell);
            Edit("Popups/Popup_Shop.prefab", ApplyShell);
            Edit("Screens/Screen_Main.prefab", ApplyMain);
            Edit("Huds/Hud_Party.prefab", ApplyParty);
            Edit("Items/Item_NavTabButton.prefab", ApplyTab);
            Edit("Items/Item_GachaPullButton.prefab", ApplyPull);
            Edit("Items/Item_EquipCell.prefab", ApplyEquipmentCell);
            foreach (string path in new[] {
                "Panels/Panel_KACharacterSheet.prefab", "Panels/Panel_KAEquipDetail.prefab", "Panels/Panel_KAJobChange.prefab", "Panels/Panel_KAJobDetail.prefab",
                "Items/Item_SkillRow.prefab", "Items/Item_JobCard.prefab", "Items/Item_StatCompareRow.prefab", "Items/Item_StatTerm.prefab",
                "Items/Dungeons/Item_DungeonCard.prefab", "Items/Dungeons/Item_DungeonDifficultyRow.prefab", "Items/Dungeons/Item_DungeonInfoSlot.prefab",
                "Popups/Popup_DungeonDifficulty.prefab", "Popups/Popup_DungeonClear.prefab", "Items/Item_CurrencyLine.prefab" })
                Edit(path, ApplyReadableBody);
            AssetDatabase.SaveAssets();
            Debug.Log("[Bronze UI] 공통 셸·인벤토리·왕국군·던전·HUD 프리팹 적용 완료");
        }

        private static void Edit(string relative, Action<GameObject> apply)
        {
            string path = Root + relative;
            var go = PrefabUtility.LoadPrefabContents(path);
            try
            {
                apply(go);
                PrefabUtility.SaveAsPrefabAsset(go, path, out bool saved);
                if (!saved) throw new InvalidOperationException("프리팹 저장 실패: " + path);
            }
            finally { PrefabUtility.UnloadPrefabContents(go); }
        }

        private static void ApplyShell(GameObject root)
        {
            var view = root.GetComponent<BottomSheetView>();
            if (view == null || view.Sheet == null) throw new InvalidOperationException("패널 셸 참조 누락: " + root.name);
            PanelGens.ConfigureSheet(view);
            bool inventory = view is InventoryPanelView;
            bool guide = view is GuidePanelView;
            view.centeredModal = inventory;
            var fit = view.Sheet.GetComponent<SheetSizeFitter>();
            fit.heightMode = guide ? SheetSizeFitter.HeightMode.AvailableSpace : SheetSizeFitter.HeightMode.BelowPartyHud;
            fit.horizontalInset = inventory ? 24 : 0;
            fit.verticalInset = inventory ? 24 : 0;
            Flat(view.Sheet.GetComponent<Image>(), inventory ? UguiTheme.RusticPanel : UguiTheme.RusticPanelDeep);
            if (view.backdrop != null) view.backdrop.GetComponent<Image>().color = new Color(0, 0, 0, inventory ? .8f : .5f);
            var layout = view.Sheet.GetComponent<VerticalLayoutGroup>();
            if (layout != null) { layout.padding = new RectOffset(30, 30, 18, 24); layout.spacing = 18; }
            var header = view.Sheet.Find("Header");
            if (header != null)
            {
                Height(header, 132);
                var headerLayout = header.GetComponent<VerticalLayoutGroup>();
                if (headerLayout != null) headerLayout.padding = new RectOffset(6, 156, 0, 0);
                var headerRow = header.GetComponent<HorizontalLayoutGroup>();
                if (headerRow != null) headerRow.padding = new RectOffset(6, 156, 0, 0);
            }
            Type(view.title, 60);
            if (view.title != null)
            {
                view.title.color = UguiTheme.TextPrimary;
                view.title.alignment = TextAlignmentOptions.MidlineLeft;
            }
            foreach (var banner in view.Sheet.GetComponentsInChildren<RectTransform>(true).Where(t => t.name == "TitleBanner")) Height(banner, 112);
            if (view.closeButton != null)
            {
                var close = (RectTransform)view.closeButton.transform;
                close.anchorMin = close.anchorMax = close.pivot = Vector2.one;
                close.anchoredPosition = new Vector2(-6, -6);
                close.sizeDelta = Vector2.one * 144;
                Layout(close).ignoreLayout = true;
                Flat(view.closeButton.GetComponent<Image>(), UguiTheme.RusticSurface);
                var icon = close.Find("Icon") as RectTransform;
                if (icon != null) { icon.sizeDelta = Vector2.one * 52; icon.GetComponent<Image>().color = UguiTheme.TextPrimary; }
            }
            foreach (var rt in view.Sheet.GetComponentsInChildren<RectTransform>(true))
            {
                if (rt.name.EndsWith("NavBar") || rt.name == "TabBar" || rt.name == "MemberBar") Height(rt, 144);
                if (rt.name == "Sheen" || rt.name == "InnerRim" || rt.name == "CornerBrackets") rt.gameObject.SetActive(false);
            }
            var frame = view.Sheet.Find("ExpeditionFrame")?.GetComponent<Image>();
            if (frame == null) frame = F.Frame(view.Sheet, "ExpeditionFrame", UguiTheme.BronzeLight);
            Layout(frame.transform).ignoreLayout = true;
            frame.color = inventory ? UguiTheme.BronzeLight : UguiTheme.Bronze;
            frame.raycastTarget = false;
            frame.transform.SetAsLastSibling();
            if (inventory) view.SetTitle("인벤토리");
            if (root.name == "Panel_Dungeon")
            {
                var hint = root.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(t => t.name == "Hint");
                Type(hint, 36, 64);
            }
        }

        private static void ApplyMain(GameObject root)
        {
            var main = root.GetComponent<MainScreenView>();
            foreach (var tab in root.GetComponentsInChildren<MainTabButtonView>(true))
            {
                Type(tab.label, 36);
                tab.label.color = UguiTheme.TextSecondary;
                tab.label.rectTransform.sizeDelta = new Vector2(200, 54);
                Flat(tab.background, Color.clear);
                if (tab.indicator != null) tab.indicator.rectTransform.sizeDelta = new Vector2(96, 6);
            }
            Flat(main.bottomBar.GetComponent<Image>(), UguiTheme.RusticBarDeep, false);
            var top = root.transform.Find("HudTop");
            if (top != null) Flat(top.GetComponent<Image>(), UguiTheme.RusticBar, false);
            foreach (var value in new[] { main.lblGold, main.lblAncientCoin })
            {
                Type(value, 42);
                value.enableAutoSizing = true; value.fontSizeMin = 36; value.fontSizeMax = 42;
                value.color = UguiTheme.TextPrimary;
            }
            Type(main.lblProfileLevel, 32);
            if (main.lblProfileLevel != null) main.lblProfileLevel.color = UguiTheme.InfoInk;
            foreach (var button in new[] { main.btnHamburger, main.btnReincarnation })
            {
                var label = button.transform.Find("Label")?.GetComponent<TMP_Text>();
                Type(label, 32);
                if (label != null) label.rectTransform.sizeDelta = new Vector2(132, 48);
            }
            foreach (var button in new[] { main.btnMenuGuide, main.btnMenuInventory, main.btnMenuNotice, main.btnMenuMail, main.btnMenuCheckIn, main.btnMenuSettings })
            {
                Height(button.transform, 144);
                foreach (var text in button.GetComponentsInChildren<TMP_Text>(true)) Type(text, 36);
                Flat(button.GetComponent<Image>(), UguiTheme.RusticSurface);
            }
            var wave = main.waveHud;
            if (wave != null)
            {
                Type(wave.lblStage, 38);
                wave.lblStage.enableAutoSizing = true; wave.lblStage.fontSizeMin = 36; wave.lblStage.fontSizeMax = 38;
            }
            var goal = root.transform.Find("GuideGoal") as RectTransform;
            if (goal != null)
            {
                goal.sizeDelta = new Vector2(610, 248);
                var body = goal.Find("Body");
                if (body != null)
                {
                    Place(body.Find("Step") as RectTransform, new Vector2(0, 1), new Vector2(24, -18), new Vector2(310, 48), new Vector2(0, 1));
                    Place(body.Find("Progress") as RectTransform, Vector2.one, new Vector2(-24, -18), new Vector2(220, 48), Vector2.one);
                    var desc = body.Find("Description") as RectTransform;
                    Place(desc, new Vector2(0, 1), new Vector2(24, -76), new Vector2(562, 100), new Vector2(0, 1));
                    var action = body.Find("ActionLabel") as RectTransform;
                    if (action != null) { action.sizeDelta = new Vector2(-48, 44); action.anchoredPosition = new Vector2(0, 14); }
                    foreach (var text in body.GetComponentsInChildren<TMP_Text>(true)) Type(text, text.name == "Description" ? 40 : 36);
                }
            }
        }

        private static void ApplyParty(GameObject root)
        {
            Flat(root.GetComponent<Image>(), UguiTheme.RusticPanel, false);
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
                if (text.name.Contains("Cooldown")) Type(text, 36);
        }

        private static void ApplyTab(GameObject root)
        {
            var tab = root.GetComponent<NavTabButtonView>();
            Type(tab.label, 36);
            tab.label.color = UguiTheme.TextPrimary;
            Flat(tab.background, UguiTheme.RusticSurfaceDark);
            Height(root.transform, 144);
            if (tab.selectedFrame != null) tab.selectedFrame.color = Color.clear;
        }

        private static void ApplyPull(GameObject root)
        {
            var button = root.GetComponent<GachaPullButtonView>();
            button.authoredBackground = false;
            Flat(button.background, UguiTheme.ActionGold);
            Type(button.titleLabel, 44);
            Type(button.costLabel, 36);
            button.titleLabel.color = button.costLabel.color = UguiTheme.InfoInk;
            Height(root.transform, 168);
        }

        private static void ApplyEquipmentCell(GameObject root)
        {
            var cell = root.GetComponent<EquipCellView>();
            Type(cell.nameLabel, 36, 90);
            cell.nameLabel.textWrappingMode = TextWrappingModes.Normal;
            cell.nameLabel.overflowMode = TextOverflowModes.Ellipsis;
            Type(cell.subLabel, 32, 44);
            Type(cell.stateLabel, 30, 42);
            Flat(cell.background, UguiTheme.RusticPanel);
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true)) text.raycastTarget = false;
        }

        private static void ApplyReadableBody(GameObject root)
        {
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                float size = text.name.Contains("Title") || text.name == "JobLabel" || text.name == "Name" ? 44 : 36;
                Type(text, Mathf.Max(size, text.fontSize));
                var layout = text.GetComponent<LayoutElement>();
                if (layout != null && layout.preferredHeight > 0)
                {
                    float height = Mathf.Max(layout.preferredHeight, text.fontSize * 1.5f);
                    layout.minHeight = height; layout.preferredHeight = height;
                }
                if (text.color.a < .85f) text.color = UguiTheme.TextSecondary;
            }
            foreach (var button in root.GetComponentsInChildren<Button>(true))
            {
                var layout = button.GetComponent<LayoutElement>();
                if (layout != null && layout.preferredHeight > 0 && layout.preferredHeight < 132) Height(button.transform, 132);
            }
            foreach (var image in root.GetComponentsInChildren<Image>(true))
                if (image.name == "Sheen" || image.name == "InnerRim") image.gameObject.SetActive(false);
            if (root.GetComponent<DungeonCardView>() != null) Height(root.transform, 300);
            if (root.GetComponent<DungeonDifficultyRowView>() != null) Height(root.transform, 144);
            var character = root.GetComponent<KACharacterSheetView>();
            if (character != null)
            {
                Height(character.statsButton.transform, 288);
                Type(character.jobLabel, 48, 66);
                Type(character.hpValueLabel, 36);
                Height(character.hpValueLabel.transform.parent, 54);
                Height(character.hpValueLabel.transform.parent.parent, 60);
                Height(character.atkValueLabel.transform.parent.parent, 66);
                Type(character.atkValueLabel, 36); Type(character.moveValueLabel, 36);
            }
        }

        private static LayoutElement Layout(Transform transform) => transform.GetComponent<LayoutElement>() ?? transform.gameObject.AddComponent<LayoutElement>();
        private static void Height(Transform transform, float height)
        {
            var layout = Layout(transform); layout.minHeight = height; layout.preferredHeight = height; layout.flexibleHeight = 0;
        }
        private static void Type(TMP_Text text, float size, float height = 0)
        {
            if (text == null) return;
            text.fontSize = text.fontSizeMin = text.fontSizeMax = size; text.enableAutoSizing = false;
            text.fontStyle = FontStyles.Normal; text.characterSpacing = 0; text.raycastTarget = false;
            if (text.font != null) text.fontSharedMaterial = text.font.material;
            if (height > 0) Height(text.transform, height);
        }
        private static void Flat(Image image, Color color, bool rounded = true)
        {
            if (image == null) return;
            image.sprite = rounded ? Rounded : null; image.type = rounded ? Image.Type.Sliced : Image.Type.Simple;
            image.pixelsPerUnitMultiplier = 1; image.color = color;
        }
        private static void Place(RectTransform transform, Vector2 anchor, Vector2 position, Vector2 size, Vector2 pivot)
        {
            if (transform == null) return;
            transform.anchorMin = transform.anchorMax = anchor; transform.pivot = pivot;
            transform.anchoredPosition = position; transform.sizeDelta = size;
        }
    }
}
