using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>기존 화면 참조를 보존하는 스테이지 HUD 부분 적용. 공용 스킨이나 다른 화면은 저장하지 않는다.</summary>
    public static class StageHudPreparation
    {
        private const string MainPath = "Assets/UGUI/Prefabs/Screens/Screen_Main.prefab";
        private const string SelectPath = "Assets/UGUI/Prefabs/Popups/Popup_StageSelect.prefab";
        private const string ReturnPath = "Assets/UGUI/Prefabs/Popups/Popup_DungeonReturnConfirm.prefab";

        [MenuItem("KingdomIdle/UGUI/Apply stage and dungeon HUD")]
        public static void Apply()
        {
            Init();
            F.Catalog.popupStageSelect = Selection();
            var confirm = Confirmation();
            var root = PrefabUtility.LoadPrefabContents(MainPath);
            try { ApplyMain(root, confirm); PrefabUtility.SaveAsPrefabAsset(root, MainPath); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            EditorUtility.SetDirty(F.Catalog); AssetDatabase.SaveAssetIfDirty(F.Catalog);
        }

        private static void Init()
        {
            F.Catalog = AssetDatabase.LoadAssetAtPath<UIViewCatalog>(PrefabGenUtil.CatalogPath);
            if (F.Catalog == null) throw new InvalidOperationException("UIViewCatalog missing");
            F.Font=F.Catalog.defaultFont; F.Rounded=F.Catalog.roundedRect; F.Circle=F.Catalog.circle;
        }

        internal static void ApplyMain(GameObject root, GameObject confirm = null)
        {
            Init();
            var wave=root.GetComponent<MainScreenView>().waveHud;
            var area=(RectTransform)wave.transform;
            if (wave.mainHud != null) { Polish(wave); return; }
            RemoveLayout(area);
            area.anchorMin=new Vector2(0,1); area.anchorMax=Vector2.one; area.pivot=new Vector2(.5f,1);
            area.sizeDelta=new Vector2(-48,0); area.anchoredPosition=new Vector2(0,-182);
            Vertical(area, 12); Fit(area);
            wave.topArea=area;
            var anchor=area.GetComponent<StageBadgeAnchor>() ?? area.gameObject.AddComponent<StageBadgeAnchor>();
            anchor.topLayout=true;
            if (area.GetComponent<CanvasGroup>() == null) area.gameObject.AddComponent<CanvasGroup>();

            var main=F.Container(area,"MainStageHud"); Vertical(main,12); wave.mainHud=main.gameObject;
            var header=F.Container(main,"Header"); Horizontal(header,14); Height(header,96);
            var row=(RectTransform)wave.lblStage.transform.parent;
            row.SetParent(header,false); RemoveLayout(row); Flexible(row);
            wave.btnStageSelect=wave.btnStageAction ?? row.GetComponent<Button>() ?? F.ButtonOn(row.GetComponent<Image>());
            foreach (string name in new[]{"Frame","StageIcon"}) if(row.Find(name)!=null) row.Find(name).gameObject.SetActive(false);
            F.Stretch(wave.lblStage.rectTransform); wave.lblStage.rectTransform.offsetMin=new Vector2(16,0); wave.lblStage.rectTransform.offsetMax=new Vector2(-16,0);
            Type(wave.lblStage,32,TextAlignmentOptions.MidlineLeft);
            wave.lblStage.text="스테이지 9-9  ›";
            wave.bossTimerBar.transform.SetParent(main,false); wave.bossTimerBar.SetActive(false);
            var toggle=(RectTransform)wave.bossChallengeRoot.transform;
            toggle.SetParent(header,false); RemoveLayout(toggle); Width(toggle,300);
            var bg=toggle.Find("ReadabilityBackground") as RectTransform; if(bg!=null) F.Stretch(bg);
            var check=toggle.Find("CheckFrame") as RectTransform;
            if(check!=null) Pin(check,new Vector2(0,.5f),new Vector2(8,0),new Vector2(38,38),new Vector2(0,.5f));
            var toggleLabel=toggle.GetComponentInChildren<TMP_Text>();
            F.Stretch(toggleLabel.rectTransform); toggleLabel.rectTransform.offsetMin=new Vector2(56,0); toggleLabel.rectTransform.offsetMax=new Vector2(-6,0);
            Type(toggleLabel,26,TextAlignmentOptions.MidlineLeft); toggleLabel.text="보스전 자동 도전";

            var progress=F.Container(main,"StageProgress"); Vertical(progress,8); wave.mainProgress=progress.gameObject;
            var counts=F.Container(progress,"Counts"); Horizontal(counts,12); Height(counts,42);
            wave.progressLabel=Label(counts,"Kills","처치 0 / 20",28); Flexible(wave.progressLabel.transform);
            wave.remainingLabel=Label(counts,"Remaining","다음 스테이지까지 20마리",26,TextAlignmentOptions.MidlineRight); Flexible(wave.remainingLabel.transform,2);
            wave.progressFill=Bar(progress,"KillProgress");
            wave.btnStageAction=Button(main,"BtnStageProgression","최고 스테이지 이동",out var action,true); wave.stageActionLabel=action;
            Height(wave.btnStageAction.transform,96);
            var strip=F.Container(main,"ChapterProgress"); Horizontal(strip,10); Height(strip,12); wave.chapterProgress=strip.gameObject;
            wave.chapterSteps=new Image[11];
            for(int i=0;i<11;i++){wave.chapterSteps[i]=F.Box(strip,"Step"+(i+1),UguiTheme.RusticSurface,false); Flexible(wave.chapterSteps[i].transform);}

            var objective=F.PixelPanel(area,"DungeonObjective",F.Catalog.kitCard,UguiTheme.Bronze,12,baseColor:UguiTheme.RusticPanelDeep);
            Vertical(objective.transform,10,new RectOffset(22,22,18,18)); wave.dungeonObjective=objective.gameObject;
            var titleRow=F.Container(objective.transform,"TitleRow"); Horizontal(titleRow,12); Height(titleRow,48);
            wave.dungeonTitle=Label(titleRow,"Title","미믹 처치 0 / 20",32); Flexible(wave.dungeonTitle.transform,3);
            wave.dungeonTime=Label(titleRow,"Time","60초",30,TextAlignmentOptions.MidlineRight); Flexible(wave.dungeonTime.transform);
            wave.dungeonProgress=Label(objective.transform,"BossHp","현재 보스 HP 100%",26); Height(wave.dungeonProgress.transform,38);
            wave.dungeonFill=Bar(objective.transform,"ObjectiveProgress");
            wave.dungeonReward=Label(objective.transform,"Reward","획득 골드 +0",26); Height(wave.dungeonReward.transform,38);
            objective.gameObject.SetActive(false);

            var guide=root.GetComponentInChildren<GuideGoalView>(true); wave.guide=guide;
            guide.transform.SetParent(area,false); RemoveLayout(guide.transform); Height(guide.transform,88);
            F.Stretch((RectTransform)guide.body.transform);
            guide.progress.gameObject.SetActive(false); guide.progressFill.transform.parent.gameObject.SetActive(false);
            Pin(guide.stepLabel.rectTransform,new Vector2(0,.5f),new Vector2(18,0),new Vector2(142,68),new Vector2(0,.5f)); Type(guide.stepLabel,26,TextAlignmentOptions.MidlineLeft);
            F.Stretch(guide.description.rectTransform); guide.description.rectTransform.offsetMin=new Vector2(170,0); guide.description.rectTransform.offsetMax=new Vector2(-174,0); Type(guide.description,28,TextAlignmentOptions.MidlineLeft);
            Pin(guide.actionLabel.rectTransform,new Vector2(1,.5f),new Vector2(-16,0),new Vector2(150,68),new Vector2(1,.5f)); Type(guide.actionLabel,26,TextAlignmentOptions.MidlineRight);

            var returnRow=F.PixelPanel(root.transform,"DungeonReturn",F.Catalog.kitCard,UguiTheme.Bronze,10,baseColor:UguiTheme.RusticPanelDeep);
            Pin(returnRow.rectTransform,new Vector2(.5f,0),new Vector2(0,390),new Vector2(900,108),new Vector2(.5f,0));
            Horizontal(returnRow.transform,14,new RectOffset(18,18,8,8)); wave.dungeonReturn=returnRow.gameObject;
            returnRow.gameObject.AddComponent<StageBadgeAnchor>(); returnRow.gameObject.AddComponent<CanvasGroup>();
            wave.dungeonName=Label(returnRow.transform,"DungeonName","골드 던전 · 1단계",28); Flexible(wave.dungeonName.transform);
            wave.btnDungeonReturn=Button(returnRow.transform,"ReturnButton","메인 스테이지 복귀",out _,false); Width(wave.btnDungeonReturn.transform,380);
            returnRow.gameObject.SetActive(false);
            if(confirm==null) confirm=AssetDatabase.LoadAssetAtPath<GameObject>(ReturnPath) ?? Confirmation();
            var popup=(GameObject)PrefabUtility.InstantiatePrefab(confirm,root.transform);
            wave.returnPopup=popup; wave.returnPanel=(RectTransform)popup.transform.Find("Panel");
            wave.returnBackdrop=popup.transform.Find("Dim").GetComponent<Button>();
            wave.returnConfirm=popup.transform.Find("Panel/Actions/Confirm").GetComponent<Button>();
            wave.returnCancel=popup.transform.Find("Panel/Actions/Cancel").GetComponent<Button>(); popup.SetActive(false);
            wave.btnStageAction.gameObject.SetActive(false);
            Polish(wave);
        }

        private static void Polish(WaveHudView wave)
        {
            // 밝은 전장에서도 처치 수와 안내 문구를 읽을 수 있도록 기존 카드 배경을 재사용한다.
            var background=wave.mainHud.GetComponent<Image>() ?? wave.mainHud.AddComponent<Image>();
            background.sprite=F.Catalog.kitCard; background.type=Image.Type.Sliced;
            background.color=UguiTheme.RusticPanelDeep; background.raycastTarget=false;
            wave.mainHud.GetComponent<VerticalLayoutGroup>().padding=new RectOffset(16,16,12,16);
            var returnRect=(RectTransform)wave.dungeonReturn.transform;
            returnRect.anchorMin=new Vector2(0,0); returnRect.anchorMax=new Vector2(1,0);
            returnRect.sizeDelta=new Vector2(-48,108);
        }

        private static GameObject Selection()
        {
            var existing=AssetDatabase.LoadAssetAtPath<GameObject>(SelectPath);
            if(existing!=null)
            {
                var contents=PrefabUtility.LoadPrefabContents(SelectPath);
                try
                {
                    var previous=contents.GetComponent<StageSelectPopupView>().previous.GetComponentInChildren<TMP_Text>();
                    previous.text="›"; previous.rectTransform.localScale=new Vector3(-1,1,1);
                    PrefabUtility.SaveAsPrefabAsset(contents,SelectPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
                return existing;
            }
            var root=F.Root("Popup_StageSelect"); var view=root.gameObject.AddComponent<StageSelectPopupView>();
            view.backdrop=Dim(root);
            var panel=F.PixelPanel(root,"Panel",F.Catalog.kitWindow,UguiTheme.Bronze,20,true,baseColor:UguiTheme.RusticPanelDeep);
            panel.rectTransform.anchorMin=Vector2.zero; panel.rectTransform.anchorMax=Vector2.right; panel.rectTransform.pivot=new Vector2(.5f,0); panel.rectTransform.sizeDelta=Vector2.zero;
            Vertical(panel.transform,18,new RectOffset(28,28,22,28)); Fit(panel.transform); view.panel=panel.rectTransform;
            var header=F.Container(panel.transform,"Header"); Horizontal(header,12); Height(header,80);
            Flexible(Label(header,"Title","스테이지 선택",40).transform);
            view.close=Button(header,"Close","×",out _,false); Width(view.close.transform,80);
            var chapter=F.Container(panel.transform,"Chapter"); Horizontal(chapter,12); Height(chapter,88);
            view.previous=Button(chapter,"Previous","›",out var previousLabel,false); Width(view.previous.transform,88);
            previousLabel.rectTransform.localScale=new Vector3(-1,1,1);
            view.chapter=Label(chapter,"Title","9장 · 오크 황무지",32,TextAlignmentOptions.Center); Flexible(view.chapter.transform);
            view.next=Button(chapter,"Next","›",out _,false); Width(view.next.transform,88);
            view.current=Label(panel.transform,"Current","현재 스테이지 9-8",26); Height(view.current.transform,40);
            view.cells=new StageSelectPopupView.Cell[11];
            for(int r=0;r<2;r++)
            {
                var cells=F.Container(panel.transform,"Cells"+r); Horizontal(cells,12); Height(cells,128);
                for(int c=0;c<5;c++)
                {
                    int i=r*5+c; var button=Button(cells,"Stage"+(i+1),"9-"+(i+1),out var label,false); Flexible(button.transform);
                    Pin(label.rectTransform,new Vector2(.5f,1),new Vector2(0,-10),new Vector2(150,54),new Vector2(.5f,1));
                    var status=Label(button.transform,"Status","클리어",24,TextAlignmentOptions.Center); Pin(status.rectTransform,new Vector2(.5f,0),new Vector2(0,8),new Vector2(150,44),new Vector2(.5f,0));
                    view.cells[i]=new StageSelectPopupView.Cell{button=button,label=label,status=status};
                }
            }
            var boss=Button(panel.transform,"Boss","9장 보스",out var bossLabel,false); Height(boss.transform,88);
            F.Stretch(bossLabel.rectTransform); bossLabel.rectTransform.offsetMax=new Vector2(-250,0);
            var bossStatus=Label(boss.transform,"Status","잠금",26,TextAlignmentOptions.MidlineRight); F.Stretch(bossStatus.rectTransform); bossStatus.rectTransform.offsetMin=new Vector2(500,0); bossStatus.rectTransform.offsetMax=new Vector2(-20,0);
            view.cells[10]=new StageSelectPopupView.Cell{button=boss,label=bossLabel,status=bossStatus};
            view.details=Label(panel.transform,"Details","스테이지 정보",28); Height(view.details.transform,138); view.details.textWrappingMode=TextWrappingModes.Normal;
            var note=Label(panel.transform,"Hint","이번 환생에서 도달한 스테이지까지 이동 가능",24); Height(note.transform,38); note.color=UguiTheme.NavMuted;
            var actions=F.Container(panel.transform,"Actions"); Horizontal(actions,16); Height(actions,124);
            view.frontier=Button(actions,"Frontier","최고 스테이지 도전\n(9-9)",out var frontier,false); view.frontierLabel=frontier; Flexible(view.frontier.transform);
            view.travel=Button(actions,"Travel","9-6스테이지 이동",out var travel,true); view.travelLabel=travel; Flexible(view.travel.transform);
            return PrefabGenUtil.SavePrefab(root.gameObject,SelectPath);
        }

        private static GameObject Confirmation()
        {
            var existing=AssetDatabase.LoadAssetAtPath<GameObject>(ReturnPath); if(existing!=null)return existing;
            var root=F.Root("Popup_DungeonReturnConfirm"); Dim(root);
            var panel=F.PixelPanel(root,"Panel",F.Catalog.kitWindow,UguiTheme.Bronze,20,true,baseColor:UguiTheme.RusticPanelDeep);
            F.AnchorCenter(panel.rectTransform,880,390); Vertical(panel.transform,26,new RectOffset(38,38,34,34));
            var message=Label(panel.transform,"Message","메인 스테이지로 복귀하시겠습니까?\n진행 중인 던전 도전을 종료합니다.",30); Height(message.transform,160); message.textWrappingMode=TextWrappingModes.Normal;
            var actions=F.Container(panel.transform,"Actions"); Horizontal(actions,24); Height(actions,100);
            Flexible(Button(actions,"Confirm","확인",out _,true).transform); Flexible(Button(actions,"Cancel","취소",out _,false).transform);
            return PrefabGenUtil.SavePrefab(root.gameObject,ReturnPath);
        }

        private static Button Dim(Transform root) {var image=F.Box(root,"Dim",UguiTheme.DimHeavy,false,true); F.Stretch(image.rectTransform); var b=image.gameObject.AddComponent<Button>(); b.targetGraphic=image; b.transition=Selectable.Transition.None; return b;}
        private static Button Button(Transform parent,string name,string text,out TextMeshProUGUI label,bool primary)
        {var b=F.TextButton(parent,name,text,30,primary?UguiTheme.BtnConfirm:UguiTheme.BtnCancel,out label,UguiTheme.Parchment,false); label.textWrappingMode=TextWrappingModes.Normal;return b;}
        private static TMP_Text Label(Transform parent,string name,string text,int size,TextAlignmentOptions alignment=TextAlignmentOptions.MidlineLeft) => F.Text(parent,name,text,size,UguiTheme.Parchment,alignment);
        private static void Type(TMP_Text label,int size,TextAlignmentOptions alignment){label.fontStyle=FontStyles.Normal;label.fontSharedMaterial=label.font.material;label.enableAutoSizing=false;label.fontSize=size;label.alignment=alignment;label.textWrappingMode=TextWrappingModes.NoWrap;label.overflowMode=TextOverflowModes.Ellipsis;}
        private static Image Bar(Transform parent,string name){var fill=F.HFillBar(parent,name,UguiTheme.RusticSurfaceDark,UguiTheme.BronzeLight,out var track); F.RectangularBar(fill);fill.color=UguiTheme.BronzeLight;Height(track.transform,12);return fill;}
        private static void Vertical(Transform t,float gap,RectOffset padding=null){var l=t.gameObject.AddComponent<VerticalLayoutGroup>();l.padding=padding??new RectOffset();l.spacing=gap;l.childControlWidth=l.childControlHeight=true;l.childForceExpandWidth=true;l.childForceExpandHeight=false;}
        private static void Horizontal(Transform t,float gap,RectOffset padding=null){var l=t.gameObject.AddComponent<HorizontalLayoutGroup>();l.padding=padding??new RectOffset();l.spacing=gap;l.childControlWidth=l.childControlHeight=true;l.childForceExpandWidth=false;l.childForceExpandHeight=true;}
        private static LayoutElement Element(Transform t)=>t.GetComponent<LayoutElement>()??t.gameObject.AddComponent<LayoutElement>();
        private static void Height(Transform t,float height){var l=Element(t);l.minHeight=l.preferredHeight=height;l.flexibleHeight=0;}
        private static void Width(Transform t,float width){var l=Element(t);l.minWidth=l.preferredWidth=width;l.flexibleWidth=0;}
        private static void Flexible(Transform t,float weight=1){var l=Element(t);l.minWidth=0;l.preferredWidth=0;l.flexibleWidth=weight;}
        private static void Fit(Transform t){t.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;}
        private static void RemoveLayout(Transform t){foreach(var c in t.GetComponents<Component>())if(c is LayoutGroup || c is LayoutElement || c is ContentSizeFitter)Object.DestroyImmediate(c);}
        private static void Pin(RectTransform t,Vector2 anchor,Vector2 position,Vector2 size,Vector2 pivot){t.anchorMin=t.anchorMax=anchor;t.pivot=pivot;t.sizeDelta=size;t.anchoredPosition=position;}
    }
}
