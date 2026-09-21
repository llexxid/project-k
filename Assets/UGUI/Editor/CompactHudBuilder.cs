using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace KingdomIdle.UGUI.Editor
{
    // Bakes the main HUD, guide sheet, and guide row prefabs.
    public static class CompactHudBuilder
    {
        const string Root = "Assets/UGUI/Prefabs/";
        const string Picto = "Assets/UGUI/Art/LayerLab/GUI Pro-MinimalGame/Shared/Icons/PictoIcon/64/";
        static Sprite Rounded => AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UGUI/Sprites/RoundedRect.png");
        static TMP_FontAsset Font => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/UGUI/Art/Font/Galmuri11 SDF.asset");
        static readonly Color Glass = new Color(.085f, .065f, .045f, .76f);
        // 런타임이 공용 탭 버튼 네 개를 생성할 컨테이너 이름과 기존 터치 높이다.
        const string QuestTabBarName = "QuestTabBar";
        const float QuestTabHeight = 144f;

        [MenuItem("KingdomIdle/UGUI/Apply compact battle HUD")]
        public static void Apply()
        {
            Edit("Screens/Screen_Main.prefab", ApplyMain);
            Edit("Panels/Panel_Guide.prefab", ApplyGuide);
            Edit("Items/Item_GuideStepRow.prefab", go => Surface(go.GetComponent<Image>(), UguiTheme.RusticSurface));
            AssetDatabase.SaveAssets();
            Debug.Log("[Compact HUD] Prefabs updated.");
        }

        public static void ApplyAndBuild()
        {
            Apply();
            UguiRegressionChecks.Run();
            TitleLobbyDeviceBuild.Build();
        }

        /// <summary>
        /// 퀘스트 탭만 적용하는 메뉴/CLI 진입점이다. 열린 씬이나 다른 HUD 프리팹을 저장하지 않는다.
        /// 기존 패널을 별도 편집 공간에서 읽고 수정한 뒤 해당 프리팹 하나만 저장한다.
        /// </summary>
        [MenuItem("KingdomIdle/UGUI/Apply quest category tabs")]
        public static void ApplyQuestTabs()
        {
            // 전체 HUD 재생성 대신 현재 수동 편집을 포함한 퀘스트 패널만 대상으로 삼는다.
            string path = Root + "Panels/Panel_Guide.prefab";
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                ApplyGuideTabs(contents);
                PrefabUtility.SaveAsPrefabAsset(contents, path, out bool saved);
                if (!saved) throw new InvalidOperationException("퀘스트 탭 프리팹을 저장하지 못했습니다: " + path);
            }
            finally
            {
                // 실패해도 임시 편집 공간은 닫아 원본 씬의 편집 상태를 보존한다.
                PrefabUtility.UnloadPrefabContents(contents);
            }
            Debug.Log("[Quest tabs] Panel_Guide의 탭 컨테이너와 참조를 갱신했습니다.");
        }

        static void Edit(string relative, Action<GameObject> edit)
        {
            string path = Root + relative;
            var go = PrefabUtility.LoadPrefabContents(path);
            try { edit(go); PrefabUtility.SaveAsPrefabAsset(go, path); }
            finally { PrefabUtility.UnloadPrefabContents(go); }
        }

        internal static void ApplyMain(GameObject go)
        {
            var main = go.GetComponent<MainScreenView>();
            var wave = main.waveHud;
            var area = (RectTransform)wave.transform;
            RemoveLayout(area);
            Pin(area, new Vector2(.5f, 1), new Vector2(0, -184), new Vector2(380, 80), new Vector2(.5f, 1));
            var row = (RectTransform)wave.lblStage.transform.parent;
            RemoveLayout(row);
            Fill(row, 0);
            row.offsetMin = new Vector2(0, 8);
            var rowImage = row.GetComponent<Image>();
            Surface(rowImage, Glass);
            rowImage.raycastTarget = false;
            foreach (string name in new[] { "Frame", "StageIcon" })
                if (row.Find(name) != null) row.Find(name).gameObject.SetActive(false);
            RemoveLayout(wave.lblStage.transform);
            Fill(wave.lblStage.rectTransform, 12);
            Type(wave.lblStage, 30, TextAlignmentOptions.Center);
            wave.lblStage.enableAutoSizing = true;
            wave.lblStage.fontSizeMin = 24;
            wave.lblStage.fontSizeMax = 30;
            wave.lblStage.textWrappingMode = TextWrappingModes.NoWrap;
            wave.lblStage.overflowMode = TextOverflowModes.Ellipsis;
            var timer = (RectTransform)wave.bossTimerBar.transform;
            RemoveLayout(timer);
            Pin(timer, new Vector2(.5f, 0), new Vector2(0, 5), new Vector2(336, 5), new Vector2(.5f, 0));

            var menu = main.popupHamburgerRect;
            menu.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 452);
            var menuLayout = menu.GetComponent<VerticalLayoutGroup>();
            menuLayout.padding = new RectOffset(14, 14, 14, 14);
            menuLayout.spacing = 8;
            if (main.btnMenuGuide == null)
            {
                var copy = Object.Instantiate(main.btnMenuInventory.gameObject, menu, false);
                copy.name = "BtnMenuGuide";
                main.btnMenuGuide = copy.GetComponent<Button>();
            }
            main.btnMenuGuide.gameObject.SetActive(true);
            main.btnMenuGuide.transform.SetAsFirstSibling();
            main.btnMenuGuide.transform.Find("MenuLabel").GetComponent<TMP_Text>().text = "퀘스트 / 가이드";
            // This book is already packed in Atlas_UI; reuse it without another texture allocation.
            main.btnMenuGuide.transform.Find("Icon").GetComponent<Image>().sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Picto + "book.png");
            foreach (var button in new[] { main.btnMenuGuide, main.btnMenuInventory, main.btnMenuSettings })
            {
                SizeLayout(button.transform, 144);
                Surface(button.GetComponent<Image>(), UguiTheme.RusticSurfaceDark);
                foreach (var shadow in button.GetComponents<Shadow>()) Object.DestroyImmediate(shadow);
                if (button.transform.Find("Frame") != null) button.transform.Find("Frame").gameObject.SetActive(false);
                var label = button.transform.Find("MenuLabel").GetComponent<TMP_Text>();
                Type(label, 32, TextAlignmentOptions.MidlineLeft);
                label.textWrappingMode = TextWrappingModes.NoWrap;
            }
            wave.btnLoopIcon.transform.SetParent(menu, false);
            SizeLayout(wave.btnLoopIcon.transform, 144);
            var repeatIcon = wave.btnLoopIcon.transform.Find("Icon") as RectTransform;
            if (repeatIcon != null) Pin(repeatIcon, new Vector2(0,.5f), new Vector2(52,0), new Vector2(52,52), new Vector2(.5f,.5f));
            var repeatText = wave.btnLoopIcon.transform.Find("Label").GetComponent<TMP_Text>();
            Fill(repeatText.rectTransform, 16);
            repeatText.rectTransform.offsetMin = new Vector2(104, 16);
            Type(repeatText, 30, TextAlignmentOptions.MidlineLeft);
            repeatText.text = "반복 사냥 종료";

            var goal = go.transform.Find("GuideGoal") as RectTransform;
            if (goal == null) goal = Child(go.transform, "GuideGoal");
            Pin(goal, new Vector2(0,1), new Vector2(24,-280), new Vector2(540,184), new Vector2(0,1));
            BuildGoal(goal, true);
            F.Init();
            ProgressionFlowPreparation.ApplyMain(go);
        }

        /// <summary>전체 HUD 생성 경로에서도 퀘스트 전용 확장·탭 배선을 사용한다.</summary>
        /// <param name="go">전체 HUD 적용 경로에서 불러온 퀘스트 패널 루트다.</param>
        static void ApplyGuide(GameObject go)
        {
            var view = go.GetComponent<GuidePanelView>();
            if (view.progressLabel != null)
            {
                Type(view.progressLabel, 28, TextAlignmentOptions.MidlineLeft);
                SizeLayout(view.progressLabel.transform, 48);
            }
            foreach (var t in go.GetComponentsInChildren<TMP_Text>(true))
                if (t.text == "가이드 진행도") t.text = "플레이 도움말";
            ApplyGuideTabs(go);
        }

        /// <summary>
        /// 생성기와 좁은 프리팹 갱신이 공유하는 탭 배선이다. 기존 카드와 스크롤 내용은 재생성하지 않는다.
        /// 탭 버튼 자체는 런타임이 공용 프리팹으로 만들므로 여기서는 빈 가로 컨테이너만 준비한다.
        /// </summary>
        /// <param name="go">GuidePanelView가 붙은 퀘스트 패널 루트다.</param>
        internal static void ApplyGuideTabs(GameObject go)
        {
            // 실제 스크롤의 부모를 본문으로 사용해 패널 바깥의 동명 오브젝트를 건드리지 않는다.
            var view = go.GetComponent<GuidePanelView>();
            if (view == null || view.scroll == null || view.scroll.transform.parent == null)
                throw new InvalidOperationException("퀘스트 패널의 GuidePanelView/Scroll 참조가 필요합니다.");
            Transform body = view.scroll.transform.parent;
            view.SetTitle("퀘스트");
            var fitter = view.sheet.GetComponent<SheetSizeFitter>() ?? view.sheet.gameObject.AddComponent<SheetSizeFitter>();
            fitter.heightMode = SheetSizeFitter.HeightMode.AvailableSpace;

            // 저장된 참조를 먼저 재사용하고, 첫 적용에서만 이름으로 기존 컨테이너를 찾거나 만든다.
            RectTransform tabs = view.tabBar != null ? view.tabBar : body.Find(QuestTabBarName) as RectTransform;
            if (tabs == null) tabs = Child(body, QuestTabBarName);
            if (tabs.parent != body) tabs.SetParent(body, false);
            tabs.gameObject.SetActive(true);
            SizeLayout(tabs, QuestTabHeight);

            // 공용 버튼 높이는 유지하며, 런타임의 동일한 LayoutElement 폭 설정으로 네 칸을 균등 배치한다.
            var layout = tabs.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>() ??
                tabs.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            layout.padding = new RectOffset();
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = true;
            layout.childScaleWidth = layout.childScaleHeight = false;
            layout.reverseArrangement = false;

            // 기존 분류 라벨 바로 앞에 배치한다. 이미 앞에 있는 탭 자신의 인덱스를 빼 재실행 위치를 고정한다.
            Transform position = view.progressLabel != null && view.progressLabel.transform.parent == body
                ? view.progressLabel.transform : view.scroll.transform;
            int sibling = position.GetSiblingIndex();
            if (tabs.GetSiblingIndex() < sibling) sibling--;
            tabs.SetSiblingIndex(sibling);
            view.tabBar = tabs;

            // 팝업의 중복 카드만 숨긴다. 인게임 HUD의 GuideGoalView는 별도 프리팹에 그대로 둔다.
            if (view.currentQuestRoot == null)
                view.currentQuestRoot = body.Find("CurrentQuest")?.gameObject;
            if (view.currentQuestRoot != null) view.currentQuestRoot.SetActive(false);

            // 이전 진행 표시를 삭제하지 않아 기존 직렬화 참조를 유지하면서 중복 정보만 숨긴다.
            if (view.progressLabel != null) view.progressLabel.gameObject.SetActive(false);
            if (view.progressFill != null)
            {
                Transform track = view.progressFill.transform.parent;
                if (track != null && track.parent == body) track.gameObject.SetActive(false);
                else view.progressFill.gameObject.SetActive(false);
            }
        }

        static void BuildGoal(RectTransform root, bool compact)
        {
            var view = root.GetComponent<GuideGoalView>() ?? root.gameObject.AddComponent<GuideGoalView>();
            // Rebuild this small owned widget deterministically, including stale layout components.
            for (int i = root.childCount - 1; i >= 0; --i) Object.DestroyImmediate(root.GetChild(i).gameObject);
            var body = Child(root, "Body");
            Fill(body, 0);
            var bg = body.gameObject.AddComponent<Image>();
            Surface(bg, compact ? Glass : UguiTheme.RusticSurfaceDark);
            var button = body.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            button.transition = Selectable.Transition.ColorTint;
            button.colors = UguiTheme.MakeColorBlock();
            body.gameObject.AddComponent<PlayClickSfxOnClick>();
            var step = Label(body, "Step", 26, UguiTheme.AccentGold);
            TopRect(step.rectTransform, 20, 22, 240, 30);
            var progress = Label(body, "Progress", 26, UguiTheme.Parchment);
            progress.alignment = TextAlignmentOptions.MidlineRight;
            progress.rectTransform.anchorMin = progress.rectTransform.anchorMax = Vector2.one;
            progress.rectTransform.pivot = Vector2.one;
            progress.rectTransform.anchoredPosition = new Vector2(-20,-22);
            progress.rectTransform.sizeDelta = new Vector2(200,30);
            var description = Label(body, "Description", compact ? 30 : 34, UguiTheme.Parchment);
            description.text = "현재 목표를 확인하세요";
            description.textWrappingMode = TextWrappingModes.Normal;
            description.overflowMode = TextOverflowModes.Ellipsis;
            description.rectTransform.anchorMin = new Vector2(0,1);
            description.rectTransform.anchorMax = Vector2.one;
            description.rectTransform.pivot = new Vector2(.5f,1);
            description.rectTransform.anchoredPosition = new Vector2(0,-57);
            description.rectTransform.sizeDelta = new Vector2(-40,compact ? 76 : 84);
            var action = Label(body, "ActionLabel", 24, UguiTheme.AccentGold);
            action.alignment = TextAlignmentOptions.MidlineRight;
            action.rectTransform.anchorMin = Vector2.zero;
            action.rectTransform.anchorMax = Vector2.right;
            action.rectTransform.pivot = new Vector2(.5f,0);
            action.rectTransform.anchoredPosition = new Vector2(0,compact ? 19 : 25);
            action.rectTransform.sizeDelta = new Vector2(-40,28);
            var track = Child(body, "ProgressTrack");
            track.anchorMin = Vector2.zero; track.anchorMax = Vector2.right;
            track.pivot = new Vector2(.5f,0); track.anchoredPosition = new Vector2(0,9);
            track.sizeDelta = new Vector2(-40,3);
            var trackImage = track.gameObject.AddComponent<Image>();
            trackImage.color = new Color(1,1,1,.14f); trackImage.raycastTarget = false;
            var fill = Child(track, "Fill"); Fill(fill,0);
            var fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.sprite = Rounded; fillImage.type = Image.Type.Filled; fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.color = UguiTheme.AccentGold; fillImage.raycastTarget = false;
            view.body = body.gameObject; view.description = description; view.progress = progress;
            view.stepLabel = step; view.actionLabel = action; view.actionButton = button; view.progressFill = fillImage;
            view.compact = compact; view.hideWithPanels = compact;
        }

        static TMP_Text Label(Transform parent, string name, float size, Color color)
        {
            var rt = Child(parent, name);
            var label = rt.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = Font; Type(label,size,TextAlignmentOptions.MidlineLeft); label.color = color;
            return label;
        }
        static void Type(TMP_Text t, float size, TextAlignmentOptions alignment)
        { t.fontSize=size; t.enableAutoSizing=false; t.fontStyle=FontStyles.Normal; t.alignment=alignment; t.raycastTarget=false; }
        static RectTransform Child(Transform parent,string name)
        { var go=new GameObject(name,typeof(RectTransform)); go.transform.SetParent(parent,false); return (RectTransform)go.transform; }
        static void Surface(Image image,Color color)
        { image.sprite=Rounded; image.type=Image.Type.Sliced; image.color=color; image.pixelsPerUnitMultiplier=1; }
        static void Fill(RectTransform rt,float inset)
        { rt.anchorMin=Vector2.zero; rt.anchorMax=Vector2.one; rt.offsetMin=Vector2.one*inset; rt.offsetMax=-Vector2.one*inset; }
        static void Pin(RectTransform rt,Vector2 anchor,Vector2 position,Vector2 size,Vector2 pivot)
        { rt.anchorMin=rt.anchorMax=anchor; rt.pivot=pivot; rt.anchoredPosition=position; rt.sizeDelta=size; }
        static void TopRect(RectTransform rt,float x,float y,float w,float h)
        { Pin(rt,new Vector2(0,1),new Vector2(x,-y),new Vector2(w,h),new Vector2(0,1)); }
        static void SizeLayout(Transform t,float height,float flexibleWidth=1)
        {
            var le=t.GetComponent<LayoutElement>()??t.gameObject.AddComponent<LayoutElement>();
            le.minWidth=0; le.preferredWidth=-1; le.flexibleWidth=flexibleWidth;
            le.minHeight=height; le.preferredHeight=height; le.flexibleHeight=0;
        }
        static void RemoveLayout(Transform t)
        {
            foreach(var c in t.GetComponents<Component>())
                if(c is LayoutGroup || c is LayoutElement || c is ContentSizeFitter) Object.DestroyImmediate(c);
        }
    }
}
