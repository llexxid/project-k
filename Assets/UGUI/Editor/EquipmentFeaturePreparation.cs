using System;
using KingdomIdle.Balance;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomIdle.UGUI.Editor
{
    public static class EquipmentFeaturePreparation
    {
        [MenuItem("KingdomIdle/UGUI/Prepare bronze equipment UI")]
        public static void PrepareBronzeEquipmentUi()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            F.Init(); F.Catalog = PrefabGenUtil.GetOrCreateCatalog();
            F.Catalog.panelKAEquipment = KingdomArmyPanelPrefabGens.GenerateEquipment();
            Edit("Popups/Popup_EquipmentAction.prefab", root =>
            {
                var view = root.GetComponent<EquipmentActionDialog>();
                AttachDialogSettings(view);
                view.title.fontSize = 60;
                view.description.fontSize = 36;
                foreach (var label in view.panel.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (label != view.title && label != view.description) label.fontSize = 36;
                    label.fontSharedMaterial = F.Font.material;
                }
            });
            EditorUtility.SetDirty(F.Catalog); AssetDatabase.SaveAssets();
        }

        [MenuItem("KingdomIdle/UGUI/Validate bronze equipment UI")]
        public static void ValidateBronzeEquipmentUi()
        {
            using var session = LocalProgression.BeginTestSession();
            LocalProgression.OpenTestAccount("bronze-equipment-" + Guid.NewGuid().ToString("N"));
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UGUI/Prefabs/Panels/Panel_KAEquipment.prefab");
            var root = UnityEngine.Object.Instantiate(prefab);
            try
            {
                var view = root.GetComponent<KAEquipmentView>();
                var toolbar = root.GetComponentInChildren<EquipmentToolbarView>(true);
                if (view.equippedIcon == null || view.inventoryGrid == null || view.recommendButton == null || view.settingsButton == null || toolbar == null)
                    throw new InvalidOperationException("Bronze equipment references are incomplete.");
                if (view.transform.Find("OwnedTitle") != null || view.GetComponent<VerticalLayoutGroup>().spacing > 8 ||
                    view.unequipButton.transform.Find("LobbyBronze") == null)
                    throw new InvalidOperationException("Equipment must leave room for its first grid row and use the shared bronze unequip button.");
                var responsive = view.inventoryGrid.GetComponent<ResponsiveGrid>();
                if (responsive == null)
                    throw new InvalidOperationException("The army equipment grid must use the shared responsive layout.");
                var gridSettings = new SerializedObject(responsive);
                if (gridSettings.FindProperty("minimumCellWidth").floatValue < 225 || gridSettings.FindProperty("cellHeight").floatValue < 284 ||
                    gridSettings.FindProperty("maximumColumns").intValue > 5)
                    throw new InvalidOperationException("Army equipment cells must fit the shared two-line 225x284 layout, at most five columns.");
                int refreshes = 0;
                toolbar.Bind(null, () => refreshes++, true);
                toolbar.Bind(null, () => refreshes++, true);
                toolbar.rarity.onClick.Invoke();
                if (LocalProgression.State.EquipmentRarityFilter != 0 || refreshes != 1)
                    throw new InvalidOperationException("Rebinding a toolbar must change a filter and refresh exactly once.");
                if (toolbar.automatic.gameObject.activeSelf)
                    throw new InvalidOperationException("The army equipment toolbar duplicates the settings shortcut.");
                foreach (var button in new[] { toolbar.job, toolbar.rarity, toolbar.sort, toolbar.dismantle, view.recommendButton, view.settingsButton, view.unequipButton })
                    if (button.GetComponent<LayoutElement>().preferredHeight < 132)
                        throw new InvalidOperationException("Equipment touch targets must be at least 132px high: " + button.name);
                Debug.Log("BRONZE EQUIPMENT UI VALIDATION PASSED: references, responsive two-line equipment grid, one-click filter after rebind, single settings entry, 132px touch targets");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        public static void Prepare()
        {
            F.Init(); F.Catalog = AssetDatabase.LoadAssetAtPath<UIViewCatalog>("Assets/UGUI/UIViewCatalog.asset");
            PrepareStoneIcon();
            Edit("Panels/Panel_KAEquipment.prefab", root =>
            {
                var view = root.GetComponent<KAEquipmentView>();
                AttachEquipmentActions(view);
                AttachToolbar(view.inventoryGrid);
            });
            Edit("Items/Item_InventoryListPage.prefab", root => AttachToolbar(root.GetComponent<InventoryListPageView>().grid));
            F.Catalog.popupEquipmentAction = CreateDialog();
            ItemGens.GenerateCurrencyLine();
            Edit("Panels/Body_Development.prefab", root => {
                var body=root.GetComponent<DevelopmentBodyView>();
                if(body.navBar==null)DevelopmentPanelPrefabGens.AddGrowthTabs(body);
                if(body.rubyCardsRoot==null) {
                    var ruby=F.Container(root.transform,"RubyCards");F.VLayout(ruby.gameObject,10,expandWidth:true);body.rubyCardsRoot=ruby;
                    ruby.SetSiblingIndex(body.cardsRoot.GetSiblingIndex()+1);
                }
            });
            Edit("Screens/Screen_Main.prefab", root => {
                var dropdown=root.GetComponent<MainScreenView>().popupCurrenciesRect;
                dropdown.sizeDelta=new Vector2(UguiTheme.DropdownWidth,dropdown.sizeDelta.y);
            });
            EditorUtility.SetDirty(F.Catalog); AssetDatabase.SaveAssets();
            Debug.Log("EQUIPMENT FEATURE PREPARATION PASSED");
        }

        public static void PrepareRecommendationUi()
        {
            F.Init();
            F.Catalog = AssetDatabase.LoadAssetAtPath<UIViewCatalog>("Assets/UGUI/UIViewCatalog.asset");
            Edit("Panels/Panel_KAEquipment.prefab", root => AttachEquipmentActions(root.GetComponent<KAEquipmentView>()));
            Edit("Popups/Popup_EquipmentAction.prefab", root => AttachDialogSettings(root.GetComponent<EquipmentActionDialog>()));
        }

        static void PrepareStoneIcon()
        {
            const string relative = "Layer Lab/GUI Pro-MinimalGame/Shared/Icons/UniqueIcon/128/Material_Ore_02_01.png";
            const string target = "Assets/UGUI/Art/LayerLab/GUI Pro-MinimalGame/Shared/Icons/UniqueIcon/128/Material_Ore_02_01.png";
            if (!System.IO.File.Exists(target)) System.IO.File.Copy("Assets/ExternalAssets/" + relative, target);
            AssetDatabase.ImportAsset(target);
            var importer = (TextureImporter)AssetImporter.GetAtPath(target);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false; importer.isReadable = false; importer.filterMode = FilterMode.Bilinear;
            importer.alphaIsTransparency = true; importer.maxTextureSize = 128; importer.SaveAndReimport();
            F.Catalog.iconEquipmentStone = UguiGenAssets.IconEquipmentStone;
        }

        public static void Validate()
        {
            Prepare();
            var root = new GameObject("EquipmentAcceptanceManager");
            var previous = EquipmentManager.Instance;
            try
            {
                var manager = root.AddComponent<EquipmentManager>(); EquipmentManager.Instance=manager;
                var serialized = new SerializedObject(manager);
                serialized.FindProperty("_database").objectReferenceValue = AssetDatabase.LoadAssetAtPath<EquipmentDatabase>("Assets/_Project/Scripts/Player/Equipment/Prefab/Equipment.asset");
                serialized.ApplyModifiedPropertiesWithoutUndo();
                var report = KingdomIdle.Balance.EquipmentEconomyAcceptance.Run();
                System.IO.Directory.CreateDirectory("Recordings/FoundationRevision/Editor");
                System.IO.File.WriteAllText("Recordings/FoundationRevision/Editor/equipment-acceptance.json", Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));
                Debug.Log("EQUIPMENT ACCEPTANCE PASSED " + report["passed"]);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); EquipmentManager.Instance=previous; }
        }

        public static void BuildDevice()
        {
            Validate();
            MagePolishPreparation.BakeAuthoredGlyphs();
            TitleLobbyDeviceBuild.Build();
        }

        internal static void AttachToolbar(RectTransform grid)
        {
            var old = grid.parent.GetComponentInChildren<EquipmentToolbarView>(true);
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var root = F.Box(grid.parent, "EquipmentTools", UguiTheme.InfoSurface, rounded: false);
            root.transform.SetSiblingIndex(grid.GetSiblingIndex());
            F.VLayout(root.gameObject, 8, new RectOffset(14,14,12,12));
            var view = root.gameObject.AddComponent<EquipmentToolbarView>();
            view.summary = F.Text(root.transform, "Summary", "보유 0/999 · 강화석 0", 36, UguiTheme.InfoInk, wrap: true);
            F.Preferred(view.summary, height: 48);
            var filters = Row(root.transform, "Filters");
            F.Preferred(filters, height: 132);
            view.job = Action(filters, "JobFilter", "직업: 전체", UguiTheme.BtnCancel);
            view.rarity = Action(filters, "RarityFilter", "등급: 전체", UguiTheme.BtnCancel);
            view.sort = Action(filters, "Sort", "정렬: 등급", UguiTheme.BtnCancel);
            view.automatic = Action(filters, "AutoDismantle", "장비\n설정", UguiTheme.BtnCancel);
            view.dismantle = Action(filters, "BulkDismantle", "일괄\n분해", UguiTheme.BtnSpend);
            var note = F.Text(root.transform, "ProtectionHint", "잠금 · 장착 · 강화 장비는 일괄 분해에서 보호", 32, UguiTheme.InfoMuted);
            F.Preferred(note, height: 42);
            F.Preferred(root, height: 262);
        }

        internal static void AttachEquipmentActions(KAEquipmentView view)
        {
            var old = view.transform.Find("EquipmentShortcuts");
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            view.ownedListButton = null;
            var row = Row(view.transform, "EquipmentShortcuts");
            row.SetSiblingIndex(1);
            view.recommendButton = Action(row, "RecommendEquipment", "추천 장착", UguiTheme.ActionGold);
            view.settingsButton = Action(row, "EquipmentSettings", "장비 설정", UguiTheme.BtnCancel);
        }

        private static void AttachDialogSettings(EquipmentActionDialog view)
        {
            if (view.recommendation == null)
            {
                var row = Row(view.panel, "RecommendationSetting");
                row.SetSiblingIndex(view.description.transform.GetSiblingIndex() + 1);
                view.recommendation = Action(row, "RecommendationCriterion", "추천 기준: 전투력", UguiTheme.BtnCancel);
            }
            if (view.autoLockFirst != null) return;
            var toggle = UnityEngine.Object.Instantiate(view.rarityToggles[0], view.panel, false);
            toggle.name = "AutoLockFirstEnhancement";
            toggle.transform.SetSiblingIndex(view.confirm.transform.parent.GetSiblingIndex());
            toggle.GetComponentInChildren<TMP_Text>().text = "첫 강화 시 해당 장비 잠금";
            toggle.gameObject.SetActive(false);
            view.autoLockFirst = toggle;
        }

        private static RectTransform Row(Transform parent, string name)
        { var row = F.Container(parent, name); F.HLayout(row.gameObject, 10, expandWidth: true); F.Preferred(row, height: 132); return row; }
        private static Button Action(Transform row, string name, string label, Color color)
        {
            var button = F.TextButton(row, name, label, 36, color, out _);
            StyleActionButton(button, color);
            F.Preferred(button, width: 100, height: 132).minHeight = 132; F.Flexible(button, flexWidth: 1); return button;
        }

        internal static void StyleActionButton(Button button, Color color)
        {
            TitleLobbyBuilder.StyleButton(button, true);
            var text = button.GetComponentInChildren<TMP_Text>(true);
            text.fontStyle = FontStyles.Normal; text.fontSharedMaterial = F.Font.material;
            text.color = color == UguiTheme.ActionGold ? UguiTheme.InfoInk : UguiTheme.TextPrimary;
            button.targetGraphic.color = color;
        }

        private static GameObject CreateDialog()
        {
            var root = F.Root("Popup_EquipmentAction"); F.Stretch(root);
            var view = root.gameObject.AddComponent<EquipmentActionDialog>();
            view.backdrop = F.InvisibleCatcher(root, "Backdrop"); view.backdrop.targetGraphic.color = new Color(0,0,0,.78f);
            var window = F.PixelPanel(root, "Window", F.Catalog.kitWindow, F.FrameGold, 16, raycast:true, baseColor: new Color(.11f,.082f,.059f,1));
            F.AnchorCenter(window.rectTransform, 940, 940); view.panel = window.rectTransform;
            F.VLayout(window.gameObject, 16, new RectOffset(36,36,30,30));
            view.title = F.Text(window.transform, "Title", "장비 관리", 42, UguiTheme.Parchment, TextAlignmentOptions.Center, bold:true);
            F.Preferred(view.title, height: 70);
            view.description = F.Text(window.transform, "Description", "", 30, UguiTheme.TextPrimary, TextAlignmentOptions.TopLeft, wrap:true);
            F.Preferred(view.description, height: 280); F.Flexible(view.description, flexHeight:1);
            view.rarityToggles = new Toggle[3];
            var labels = new[] { "일반 장비", "레어 장비", "에픽 장비" };
            for (int i=0;i<3;i++)
            {
                var row = F.Box(window.transform, "Rarity" + i, UguiTheme.RusticSurfaceDark, raycast:true); F.Preferred(row, height:100);
                var toggle = row.gameObject.AddComponent<Toggle>(); toggle.targetGraphic = row;
                var outline = F.Box(row.transform, "ToggleOutline", UguiTheme.Bronze, rounded:false);
                F.AnchorCenter(outline.rectTransform, 48,48); outline.rectTransform.anchorMin=outline.rectTransform.anchorMax=new Vector2(0,.5f);
                outline.rectTransform.anchoredPosition = new Vector2(50,0);
                var off=F.Box(outline.transform,"Off",UguiTheme.RusticSurfaceDark,rounded:false);F.AnchorCenter(off.rectTransform,40,40);
                var check = F.Box(outline.transform, "Checked", UguiTheme.AccentGold, rounded:false);check.sprite=F.Catalog.iconCheck;check.preserveAspect=true;
                F.AnchorCenter(check.rectTransform, 34,34); toggle.graphic = check; toggle.isOn=false;
                var label = F.Text(row.transform, "Label", labels[i],32,UguiTheme.Parchment); F.Stretch(label.rectTransform);
                label.rectTransform.offsetMin = new Vector2(108,0); label.rectTransform.offsetMax = new Vector2(-24,0);
                row.gameObject.AddComponent<PlayClickSfxOnToggle>(); view.rarityToggles[i]=toggle;
            }
            var buttons = Row(window.transform, "Buttons");
            view.cancel = Action(buttons,"Cancel","취소",UguiTheme.BtnCancel);
            view.confirm = Action(buttons,"Confirm","확인",UguiTheme.BtnSpend);
            AttachDialogSettings(view);
            return PrefabGenUtil.SavePrefab(root.gameObject,"Assets/UGUI/Prefabs/Popups/Popup_EquipmentAction.prefab");
        }
        private static void Edit(string relative, Action<GameObject> action)
        {
            string path="Assets/UGUI/Prefabs/"+relative; var root=PrefabUtility.LoadPrefabContents(path);
            try { action(root); PrefabUtility.SaveAsPrefabAsset(root,path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
