using System;
using System.IO;
using System.Linq;
using KingdomIdle.Balance;
using KingdomIdle.MageTower;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace KingdomIdle.UGUI.Editor
{
    public static class PlayabilityRevisionPreparation
    {
        const string Root = "Assets/UGUI/Prefabs/";
        static TMP_FontAsset Font => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/UGUI/Art/Font/Galmuri11 SDF.asset");
        public static void Prepare()
        {
            Edit("Panels/Panel_Guide.prefab", Center);
            Edit("Panels/Panel_Inventory.prefab", Center);
            Edit("Items/Item_GuideStepRow.prefab", go => {
                var row = go.GetComponent<GuideStepRowView>();
                if (row.checkLabel == null)
                {
                    var label = Rect(row.checkButton.transform, "ClaimLabel"); Stretch(label,0);
                    row.checkLabel = label.gameObject.AddComponent<TextMeshProUGUI>();
                    row.checkLabel.font = Font; row.checkLabel.fontSize = 23;
                    row.checkLabel.alignment = TextAlignmentOptions.Center; row.checkLabel.raycastTarget = false;
                }
                row.checkLabel.gameObject.SetActive(true); row.checkLabel.color = UguiTheme.Parchment;
                row.checkBorder.color = UguiTheme.RusticPanelDeep;
                row.checkButton.transition = Selectable.Transition.None;
            });
            Edit("Panels/Panel_KACharacterSheet.prefab", go => {
                var view = go.GetComponent<KACharacterSheetView>();
                view.hpFill.sprite = null; view.hpFill.type = Image.Type.Filled;
                view.hpFill.fillMethod = Image.FillMethod.Horizontal; view.hpFill.fillOrigin = 0;
                var track = view.hpFill.transform.parent.GetComponent<Image>();
                if (track != null) { track.sprite = null; track.type = Image.Type.Simple; }
            });
            Edit("Items/Item_JobCard.prefab", go => {
                var view = go.GetComponent<JobCardView>();
                var layout = go.GetComponent<VerticalLayoutGroup>();
                layout.padding = new RectOffset(8,8,18,8); layout.spacing = 2;
                var wrap = go.transform.Find("ImgWrap");
                if (wrap != null) { var size = wrap.GetComponent<LayoutElement>(); size.minHeight = size.preferredHeight = 52; }
                foreach (var rect in go.GetComponentsInChildren<RectTransform>(true))
                    if (rect.name == "PortraitRing") rect.sizeDelta = new Vector2(52,52);
                    else if (rect.name == "PortraitDisc") rect.sizeDelta = new Vector2(48,48);
                view.image.rectTransform.sizeDelta = new Vector2(44,44);
                foreach (var label in new[] { view.nameLabel, view.statLabel, view.fragLabel, view.prereqLabel })
                {
                    label.fontSize = label == view.nameLabel ? 27 : 22;
                    label.enableAutoSizing = false;
                    label.textWrappingMode = TextWrappingModes.NoWrap;
                    var size = label.GetComponent<LayoutElement>();
                    if (size != null) { size.minHeight = size.preferredHeight = label == view.nameLabel ? 32 : 26; }
                }
            });
            Edit("Panels/Panel_KAJobChange.prefab", go => {
                foreach (var grid in go.GetComponentsInChildren<GridLayoutGroup>(true)) grid.cellSize = new Vector2(grid.cellSize.x,208);
                foreach (var layout in go.GetComponentsInChildren<LayoutElement>(true))
                    if (layout.name == "Origin") layout.minHeight = layout.preferredHeight = 36;
                    else if (layout.name == "BranchRoots" || layout.name == "BranchLinks") layout.minHeight = layout.preferredHeight = 18;
            });
            Edit("Screens/Screen_Main.prefab", go => {
                var main = go.GetComponent<MainScreenView>(); var wave = main.waveHud;
                var rt = (RectTransform)wave.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(.5f,0); rt.pivot = new Vector2(.5f,0);
                rt.anchoredPosition = new Vector2(0,370); rt.sizeDelta = new Vector2(410,86);
                if (rt.GetComponent<StageBadgeAnchor>() == null) rt.gameObject.AddComponent<StageBadgeAnchor>();
                if (rt.GetComponent<CanvasGroup>() == null) rt.gameObject.AddComponent<CanvasGroup>();
                var bg = wave.lblStage.transform.parent.GetComponent<Image>(); bg.color = new Color(.10f,.075f,.04f,.93f);
                var outline = bg.GetComponent<Outline>() ?? bg.gameObject.AddComponent<Outline>();
                outline.effectColor = UguiTheme.Bronze; outline.effectDistance = new Vector2(2,-2);
                wave.lblStage.color = UguiTheme.Parchment; wave.lblStage.fontSize = 32; wave.lblStage.fontSizeMax = 32;
                var goal = go.transform.Find("GuideGoal") as RectTransform;
                if (goal != null) goal.anchoredPosition = new Vector2(24,-174);
            });
            CreateManualHud();
            MageSkillAssetPreparation.RefreshSanctuary();
            var registry = AssetDatabase.LoadAssetAtPath<MageTowerSkillRegistrySO>("Assets/MageTower/SO/MageTowerSkillList.asset");
            foreach (var skill in registry.skills)
            {
                if (skill.id == 4) { skill.nameKor = "맹독 늪"; skill.description = "맹독 늪을 펼쳐 범위 안의 적에게 지속 피해를 주고 이동 속도를 25% 낮춥니다."; }
                if (skill.id == 7) skill.description = "성역을 펼쳐 범위 안에서 체력 비율이 가장 낮은 왕국군을 반복해서 회복합니다.";
                EditorUtility.SetDirty(skill);
            }
            var table = AssetDatabase.LoadAssetAtPath<KingdomIdle.Gacha.GachaTableSO>("Assets/Gacha/SO/GachaTable_MageTowerSkill.asset");
            foreach (var entry in table.rewards) if (entry.rewardType == KingdomIdle.Gacha.eGachaRewardType.Skill && entry.skillId == 4) entry.nameKor = "맹독 늪";
            EditorUtility.SetDirty(table);
            PlayerSettings.bundleVersion = "0.11.0";
            MagePolishPreparation.BakeAuthoredGlyphs();
            KingdomIdle.EditorTools.Optimization.OptAtlases.CreateInBuildAtlases();
            AssetDatabase.SaveAssets();
            Validate();
        }
        public static void BuildDevice() { Prepare(); TitleLobbyDeviceBuild.Build(); }
        public static void BuildManual() { PrepareSessionPolish(); TitleLobbyDeviceBuild.BuildForManualTesting(); }
        public static void ValidateSessionRoutes() { PrepareSessionPolish(); PlayabilityLiveValidation.RunSessionRoutes(); }
        public static void BuildSessionPolish()
        {
            PrepareSessionPolish();
            TitleLobbyDeviceBuild.Build();
        }
        public static void PrepareSessionPolish()
        {
            Edit("Popups/Popup_Profile.prefab", go => {
                var profile = go.GetComponent<ProfilePopupView>();
                profile.powerButton.transition = Selectable.Transition.None;
                profile.editNameButton.gameObject.SetActive(false);
                profile.idLabel.gameObject.SetActive(false);
                foreach (var field in new[]{profile.trophyLabel,profile.guildLabel})
                    for (var node = field.transform; node != null && node != go.transform; node = node.parent)
                        if (node.name == "Pill" || node.name == "InfoRow") { node.gameObject.SetActive(false); break; }
                foreach (var node in go.GetComponentsInChildren<Transform>(true))
                {
                    if (node.name == "LeagueCard")
                    {
                        node.gameObject.SetActive(false);
                        var previous = node.GetSiblingIndex() - 1;
                        if (previous >= 0 && node.parent.GetChild(previous).name == "DecoDivider")
                            node.parent.GetChild(previous).gameObject.SetActive(false);
                    }
                    if (node.name == "SummaryPills" || node.name == "UniqueRow")
                        node.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = true;
                }
                string[] labels = {"최고 클리어","환생 레벨","환생 횟수","골드 던전","몬스터 처치","루비 던전"};
                for (int i = 0; i < labels.Length; i++)
                    foreach (var label in profile.statValues[i].transform.parent.GetComponentsInChildren<TMP_Text>(true))
                        if (label != profile.statValues[i]) label.text = labels[i];
            });
            Edit("Items/Item_GachaCard.prefab", go => {
                var name = go.GetComponent<GachaCardItemView>().nameLabel;
                name.enableAutoSizing = true;
                name.fontSizeMin = 24;
                name.fontSizeMax = name.fontSize = 30;
            });
            Edit("Screens/Screen_Main.prefab", go => {
                var wave = go.GetComponent<MainScreenView>().waveHud;
                ((RectTransform)wave.transform).sizeDelta = new Vector2(560,108);
                var row = wave.lblStage.transform.parent.GetComponent<Image>();
                row.raycastTarget = true;
                wave.btnStageAction = row.GetComponent<Button>() ?? row.gameObject.AddComponent<Button>();
                wave.btnStageAction.targetGraphic = row;
                wave.btnStageAction.transition = Selectable.Transition.None;
                wave.lblStage.fontSize = wave.lblStage.fontSizeMax = 30;
                wave.lblStage.raycastTarget = false;
            });
            Edit("Panels/Panel_Dungeon.prefab", go => {
                foreach (var popup in go.GetComponentsInChildren<DungeonDifficultyPopupView>(true))
                foreach (var text in popup.GetComponentsInChildren<TMP_Text>(true).Where(t => t.name == "Description"))
                {
                    text.fontSize = 22; text.enableAutoSizing = false;
                    text.textWrappingMode = TextWrappingModes.Normal;
                    text.overflowMode = TextOverflowModes.Ellipsis;
                    var size = text.GetComponent<LayoutElement>();
                    if (size != null) size.minHeight = size.preferredHeight = 64;
                    var band = (RectTransform)text.transform.parent;
                    band.sizeDelta = new Vector2(band.sizeDelta.x,136);
                }
            });
            PlayerSettings.bundleVersion = "0.11.1";
            AssetDatabase.SaveAssets();
            Validate();
        }
        public static void Validate()
        {
            BalanceEditorValidation.Run();
            MagePolishPreparation.Validate();
            var report = new { passed = true, roster = MageSkillRules.SkillCount };
            Directory.CreateDirectory("Recordings/PlayabilityRevision/Editor");
            File.WriteAllText("Recordings/PlayabilityRevision/Editor/mage-acceptance.json", Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));
            Debug.Log("PLAYABILITY VALIDATION PASSED");
        }
        static void Center(GameObject go)
        {
            var view = go.GetComponent<BottomSheetView>(); view.centeredModal = true;
            view.sheet.SetParent(go.transform,false);
            var fitter = view.sheet.GetComponent<SheetSizeFitter>(); if(fitter != null) Object.DestroyImmediate(fitter);
            var modal = view.sheet.GetComponent<CenteredPanelSize>() ?? view.sheet.gameObject.AddComponent<CenteredPanelSize>();
            modal.maxHeight = 1440;
            view.sheet.anchorMin = view.sheet.anchorMax = view.sheet.pivot = new Vector2(.5f,.5f);
            view.sheet.anchoredPosition = Vector2.zero; view.sheet.sizeDelta = new Vector2(980,1440);
            if (view.backdrop != null)
            {
                Stretch((RectTransform)view.backdrop.transform,0);
                var image = view.backdrop.GetComponent<Image>();
                if (image != null) image.color = new Color(0,0,0,.55f);
            }
            if (go.GetComponent<PartyHudSuppressor>() == null) go.AddComponent<PartyHudSuppressor>();
        }
        public static void CreateManualHud()
        {
            var aim = new GameObject("MageGroundAim",typeof(RectTransform),typeof(CanvasRenderer),typeof(MagicAimGraphic));
            aim.layer=5;
            var aimPrefab = PrefabUtility.SaveAsPrefabAsset(aim,Root+"Huds/Hud_MageGroundAim.prefab"); Object.DestroyImmediate(aim);
            Edit("Huds/Hud_MageTowerEnv.prefab",go=>{
                var old=go.transform.Find("ManualSkills");if(old!=null)Object.DestroyImmediate(old.gameObject);
                var hud=go.GetComponent<MageManualCastHud>()??go.AddComponent<MageManualCastHud>();
                var tray=Rect(go.transform,"ManualSkills");
                tray.anchorMin=tray.anchorMax=new Vector2(.5f,1);tray.pivot=new Vector2(.5f,0);
                tray.anchoredPosition=new Vector2(-48,156);tray.sizeDelta=new Vector2(156,610);
                hud.tray=tray;hud.group=tray.gameObject.AddComponent<CanvasGroup>();hud.aimPrefab=aimPrefab;
                hud.buttons=new MageManualSkillButton[5];
                for(int i=0;i<5;i++)
                {
                    var root=Rect(tray,"ManualSkill"+i);root.anchorMin=root.anchorMax=new Vector2(1,0);root.sizeDelta=new Vector2(132,132);
                    root.anchoredPosition=new Vector2(i%2==0?-12:12,62+i*118);
                    var frame=root.gameObject.AddComponent<Image>();frame.color=UguiTheme.Bronze;
                    var button=root.gameObject.AddComponent<Button>();button.targetGraphic=frame;
                    var cell=root.gameObject.AddComponent<MageManualSkillButton>();cell.slot=i;cell.button=button;cell.frame=frame;cell.visibility=root.gameObject.AddComponent<CanvasGroup>();button.transition=Selectable.Transition.None;
                    var bg=Rect(root,"Wood");Stretch(bg,3);var bgImage=bg.gameObject.AddComponent<Image>();bgImage.color=UguiTheme.RusticPanelDeep;bgImage.raycastTarget=false;
                    var icon=Rect(bg,"Icon");icon.anchorMin=icon.anchorMax=new Vector2(.5f,.61f);icon.sizeDelta=new Vector2(96,96);
                    cell.icon=icon.gameObject.AddComponent<Image>();cell.icon.preserveAspect=true;cell.icon.raycastTarget=false;
                    var mask=Rect(bg,"Cooldown");mask.anchorMin=mask.anchorMax=new Vector2(.5f,.61f);mask.sizeDelta=new Vector2(96,96);cell.cooldown=mask.gameObject.AddComponent<Image>();
                    cell.cooldown.sprite=AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");cell.cooldown.color=new Color(.02f,.03f,.06f,.55f);cell.cooldown.fillClockwise=false;cell.cooldown.fillOrigin=(int)Image.Origin360.Top;cell.cooldown.type=Image.Type.Filled;cell.cooldown.fillMethod=Image.FillMethod.Radial360;cell.cooldown.fillAmount=0;cell.cooldown.raycastTarget=false;
                    var text=Rect(bg,"Status");text.anchorMin=new Vector2(0,0);text.anchorMax=new Vector2(1,0);text.pivot=new Vector2(.5f,0);text.sizeDelta=new Vector2(0,26);
                    cell.label=text.gameObject.AddComponent<TextMeshProUGUI>();cell.label.font=Font;cell.label.fontSize=21;cell.label.enableAutoSizing=true;cell.label.fontSizeMin=17;cell.label.fontSizeMax=21;cell.label.textWrappingMode=TextWrappingModes.NoWrap;cell.label.alignment=TextAlignmentOptions.Center;cell.label.raycastTarget=false;cell.label.color=UguiTheme.Parchment;
                    var seconds=Rect(bg,"Seconds");seconds.anchorMin=seconds.anchorMax=new Vector2(.5f,.60f);seconds.sizeDelta=new Vector2(102,46);
                    cell.cooldownLabel=seconds.gameObject.AddComponent<TextMeshProUGUI>();cell.cooldownLabel.font=Font;cell.cooldownLabel.fontSize=31;cell.cooldownLabel.fontStyle=FontStyles.Bold;cell.cooldownLabel.alignment=TextAlignmentOptions.Center;cell.cooldownLabel.raycastTarget=false;
                    cell.cooldownLabel.color=UguiTheme.Parchment;
                    hud.buttons[i]=cell;
                }
                tray.gameObject.SetActive(false);
            });
        }
        static RectTransform Rect(Transform parent,string name)
        {var go=new GameObject(name,typeof(RectTransform));go.layer=5;go.transform.SetParent(parent,false);return (RectTransform)go.transform;}
        static void Stretch(RectTransform rect,float inset)
        {rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=Vector2.one*inset;rect.offsetMax=-Vector2.one*inset;}
        static void Edit(string path,Action<GameObject> action)
        {
            path=Root+path;var go=PrefabUtility.LoadPrefabContents(path);
            try{action(go);PrefabUtility.SaveAsPrefabAsset(go,path);}finally{PrefabUtility.UnloadPrefabContents(go);}
        }
    }
}
