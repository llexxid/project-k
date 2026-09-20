using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomIdle.UGUI.Editor
{
    public static class EquipmentFeaturePreparation
    {
        public static void Prepare()
        {
            F.Init(); F.Catalog = AssetDatabase.LoadAssetAtPath<UIViewCatalog>("Assets/UGUI/UIViewCatalog.asset");
            PrepareStoneIcon();
            Edit("Panels/Panel_KAEquipment.prefab", root => AttachToolbar(root.GetComponent<KAEquipmentView>().inventoryGrid));
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
            var root = F.Box(grid.parent, "EquipmentTools", UguiTheme.RusticSurfaceDark, rounded: false);
            root.transform.SetSiblingIndex(grid.GetSiblingIndex());
            F.VLayout(root.gameObject, 10, new RectOffset(14,14,14,14)); F.Preferred(root, height: 378);
            var view = root.gameObject.AddComponent<EquipmentToolbarView>();
            view.summary = F.Text(root.transform, "Summary", "보유 0/300 · 추가 보관 0\n강화석 0 · 추가 보관은 기한 없이 유지됩니다", 27, UguiTheme.Parchment, wrap: true);
            F.Preferred(view.summary, height: 76);
            var filters = Row(root.transform, "Filters");
            view.job = Action(filters, "JobFilter", "직업: 전체", UguiTheme.BtnCancel);
            view.rarity = Action(filters, "RarityFilter", "등급: 전체", UguiTheme.BtnCancel);
            view.sort = Action(filters, "Sort", "정렬: 등급", UguiTheme.BtnCancel);
            var actions = Row(root.transform, "Actions");
            view.automatic = Action(actions, "AutoDismantle", "자동 분해 꺼짐", UguiTheme.BtnCancel);
            view.dismantle = Action(actions, "BulkDismantle", "필터 내 일괄 분해", UguiTheme.BtnSpend);
            var note = F.Text(root.transform, "ProtectionHint", "일괄 분해는 잠금·장착·강화 장비를 제외합니다", 25, UguiTheme.TextSecondary);
            F.Preferred(note, height: 32);
        }

        private static RectTransform Row(Transform parent, string name)
        { var row = F.Container(parent, name); F.HLayout(row.gameObject, 10, expandWidth: true); F.Preferred(row, height: 104); return row; }
        private static Button Action(Transform row, string name, string label, Color color)
        {
            var button = F.TextButton(row, name, label, 28, color, out var text);
            TitleLobbyBuilder.StyleButton(button,true);
            text.fontStyle=FontStyles.Normal;text.fontSharedMaterial=F.Font.material;
            button.targetGraphic.color=color;
            F.Preferred(button, width: 100, height: 104); F.Flexible(button, flexWidth: 1); return button;
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
