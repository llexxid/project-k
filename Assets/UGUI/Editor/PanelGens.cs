using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>
    /// 하단 시트형 패널 6종 프리팹 생성기.
    /// 공통 스켈레톤(UXML 패널 셸 대응): PanelRoot / Backdrop / Sheet(헤더 + 본문).
    /// </summary>
    internal static class PanelGens
    {
        private struct Shell
        {
            public RectTransform Root;
            public Button Backdrop;
            public RectTransform Sheet;
            public TextMeshProUGUI Title;
            public Button CloseButton;
            public RectTransform Body;   // 헤더 아래 본문 (VerticalLayout)
        }

        /// <summary>
        /// 패널 공통 셸 생성.
        /// Backdrop: 하단바(190) 위 전체를 덮는 딤. Sheet: bottom 190, 높이 = 화면의 heightPct.
        /// </summary>
        private static Shell BuildShell(string rootName, string title, float heightPct)
        {
            var root = F.Root(rootName);

            // Backdrop (.panel-backdrop: bottom 190, black@35%)
            var backdropImg = F.Box(root, "Backdrop", UguiTheme.DimLight, rounded: false, raycast: true);
            var backdropRt = backdropImg.rectTransform;
            backdropRt.anchorMin = Vector2.zero;
            backdropRt.anchorMax = Vector2.one;
            backdropRt.offsetMin = new Vector2(0f, UguiTheme.BottomBarHeight);
            backdropRt.offsetMax = Vector2.zero;
            var backdropBtn = backdropImg.gameObject.AddComponent<Button>();
            backdropBtn.targetGraphic = backdropImg;
            backdropBtn.transition = Selectable.Transition.None;

            // SheetClip — 탭바(190) 위 영역만 그리는 마스크. 시트가 탭바 뒤에서 떠오르는 슬라이드 연출용
            var clip = F.Container(root, "SheetClip");
            clip.anchorMin = Vector2.zero;
            clip.anchorMax = Vector2.one;
            clip.offsetMin = new Vector2(0f, UguiTheme.BottomBarHeight);
            clip.offsetMax = Vector2.zero;
            clip.gameObject.AddComponent<RectMask2D>();

            // Sheet — 어두운 배경 + 픽셀 윈도우 프레임 테두리 (가독성 + 판타지 창)
            float sheetHeight = Mathf.Max(UguiTheme.PanelSheetMinHeight, UguiTheme.RefHeight * heightPct);
            var sheetImg = F.PixelPanel(clip, "Sheet",
                F.Catalog != null ? F.Catalog.kitWindow : null, F.FrameGold, 24f, raycast: true,
                baseColor: F.PanelBaseDarker);
            F.AnchorBottomStretch(sheetImg.rectTransform, 0f, sheetHeight);
            F.VLayout(sheetImg.gameObject, 14f, new RectOffset(30, 30, 24, 28));

            // 데모식 청동 코너 브래킷 (금속 보강 룩)
            F.CornerBrackets(sheetImg.transform);

            // 헤더(데모 스타일): LL 리본 배너에 얹은 중앙 제목 + 우상단 원형 닫기 버튼
            var header = F.Container(sheetImg.transform, "Header");
            F.VLayout(header.gameObject, 2f, new RectOffset(70, 70, 2, 2), TextAnchor.UpperCenter);
            F.Preferred(header.gameObject.AddComponent<LayoutElement>(), height: 112f);

            var titleLbl = F.HeaderBanner(header, title);

            // 닫기 — 시트 우상단 절대 위치(레이아웃 무시)
            var closeImg = F.CircleBox(sheetImg.transform, "BtnPanelClose", new Color(0.62f, 0.24f, 0.24f, 1f), raycast: true);
            var closeRt = closeImg.rectTransform;
            closeRt.anchorMin = new Vector2(1f, 1f); closeRt.anchorMax = new Vector2(1f, 1f); closeRt.pivot = new Vector2(1f, 1f);
            closeRt.anchoredPosition = new Vector2(-14f, -14f); closeRt.sizeDelta = new Vector2(64f, 64f);
            closeImg.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var closeBtn = closeImg.gameObject.AddComponent<Button>();
            closeBtn.targetGraphic = closeImg;
            closeBtn.transition = Selectable.Transition.ColorTint;
            closeBtn.colors = UguiTheme.MakeColorBlock();
            closeImg.gameObject.AddComponent<PlayClickSfxOnClick>();
            if (F.Catalog != null && F.Catalog.iconX != null)
            {
                var xIcon = F.IconImage(closeImg.transform, "Icon", F.Catalog.iconX, 32f, 32f);
                F.AnchorCenter(xIcon.rectTransform, 32f, 32f);
            }
            else
            {
                var closeLbl = F.Text(closeImg.transform, "Label", "X", 28f, UguiTheme.TextPrimary, TextAlignmentOptions.Center, bold: true);
                F.Stretch(closeLbl.rectTransform);
            }

            // 본문 컨테이너
            var body = F.Container(sheetImg.transform, "Body");
            F.VLayout(body.gameObject, 14f);
            F.Flexible(body.gameObject.AddComponent<LayoutElement>(), flexHeight: 1f);

            return new Shell
            {
                Root = root,
                Backdrop = backdropBtn,
                Sheet = sheetImg.rectTransform,
                Title = titleLbl,
                CloseButton = closeBtn,
                Body = body,
            };
        }

        private static void WireBase(BottomSheetView view, Shell shell)
        {
            view.backdrop = shell.Backdrop;
            view.sheet = shell.Sheet;
            view.title = shell.Title;
            view.closeButton = shell.CloseButton;
        }

        /// <summary>본문에 세로 ScrollRect 추가 (flex-grow).</summary>
        private static ScrollRect AddScroll(RectTransform body, out RectTransform content)
        {
            var scroll = F.VScroll(body, "Scroll", out content, spacing: 10f, padding: new RectOffset(0, 0, 0, 10));
            F.Flexible(scroll.gameObject.AddComponent<LayoutElement>(), flexHeight: 1f);
            return scroll;
        }

        /// <summary>본문에 탭/네비 바 컨테이너 추가.</summary>
        private static RectTransform AddBar(RectTransform body, string name, float height)
        {
            var bar = F.Container(body, name);
            F.HLayout(bar.gameObject, 8f, null, TextAnchor.MiddleCenter, expandWidth: true);
            F.Preferred(bar.gameObject.AddComponent<LayoutElement>(), height: height);
            return bar;
        }

        // ═══ 개별 패널 ═══

        internal static GameObject GeneratePlaceholder()
        {
            var shell = BuildShell("Panel_Placeholder", "[패널]", UguiTheme.PanelSheetHeightPct);
            var view = shell.Root.gameObject.AddComponent<PlaceholderPanelView>();
            WireBase(view, shell);

            var hint = F.Text(shell.Body, "Hint", "(작업 예정)", 26f, UguiTheme.TextTertiary, TextAlignmentOptions.Center);
            F.Flexible(hint, flexWidth: 1f);
            F.Preferred(hint, height: 60f);
            view.hint = hint;

            return PrefabGenUtil.SavePrefab(shell.Root.gameObject, $"{PrefabGenUtil.PrefabRoot}/Panels/Panel_Placeholder.prefab");
        }

        internal static GameObject GenerateGuide()
        {
            var shell = BuildShell("Panel_Guide", "가이드", UguiTheme.GuideSheetHeightPct);
            var view = shell.Root.gameObject.AddComponent<GuidePanelView>();
            WireBase(view, shell);

            // 진행 바 (.guide-progress-bar: h14 + 초록 fill)
            var fill = F.HFillBar(shell.Body, "GuideProgressBar", new Color(1f, 1f, 1f, 0.12f),
                new Color(100f / 255f, 210f / 255f, 130f / 255f, 0.90f), out var track);
            F.Preferred(track, height: 14f);
            fill.fillAmount = 0f;
            view.progressFill = fill;

            // 진행 라벨 (.guide-progress-label: 22 @75% 우측 정렬)
            var progressLbl = F.Text(shell.Body, "LblGuideProgress", "0 / 0 완료", 22f,
                new Color(1f, 1f, 1f, 0.75f), TextAlignmentOptions.Right);
            F.Preferred(progressLbl, height: 30f);
            view.progressLabel = progressLbl;

            view.scroll = AddScroll(shell.Body, out var content);
            view.listContent = content;

            return PrefabGenUtil.SavePrefab(shell.Root.gameObject, $"{PrefabGenUtil.PrefabRoot}/Panels/Panel_Guide.prefab");
        }

        internal static GameObject GenerateGacha()
        {
            var shell = BuildShell("Panel_Gacha", "뽑기", UguiTheme.PanelSheetHeightPct);
            var view = shell.Root.gameObject.AddComponent<GachaPanelView>();
            WireBase(view, shell);

            view.tabBar = AddBar(shell.Body, "GachaTabBar", 104f);
            view.scroll = AddScroll(shell.Body, out var content);
            view.content = content;

            return PrefabGenUtil.SavePrefab(shell.Root.gameObject, $"{PrefabGenUtil.PrefabRoot}/Panels/Panel_Gacha.prefab");
        }

        internal static GameObject GenerateKingdomArmy()
        {
            var shell = BuildShell("Panel_KingdomArmy", "왕국군", UguiTheme.PanelSheetHeightPct);
            var view = shell.Root.gameObject.AddComponent<KingdomArmyPanelView>();
            WireBase(view, shell);

            view.memberTabs = AddBar(shell.Body, "ArmyMemberTabs", 92f);
            view.scroll = AddScroll(shell.Body, out var content);
            view.content = content;
            view.navBar = AddBar(shell.Body, "ArmyNavBar", 104f);

            return PrefabGenUtil.SavePrefab(shell.Root.gameObject, $"{PrefabGenUtil.PrefabRoot}/Panels/Panel_KingdomArmy.prefab");
        }

        internal static GameObject GenerateDevelopment()
        {
            var shell = BuildShell("Panel_Development", "육성", UguiTheme.PanelSheetHeightPct);
            var view = shell.Root.gameObject.AddComponent<DevelopmentPanelView>();
            WireBase(view, shell);

            view.scroll = AddScroll(shell.Body, out var content);
            view.content = content;

            return PrefabGenUtil.SavePrefab(shell.Root.gameObject, $"{PrefabGenUtil.PrefabRoot}/Panels/Panel_Development.prefab");
        }

        internal static GameObject GenerateInventory()
        {
            var shell = BuildShell("Panel_Inventory", "인벤토리", UguiTheme.PanelSheetHeightPct);
            var view = shell.Root.gameObject.AddComponent<InventoryPanelView>();
            WireBase(view, shell);

            view.navBar = AddBar(shell.Body, "InvNavBar", 104f);
            view.scroll = AddScroll(shell.Body, out var content);
            view.content = content;

            return PrefabGenUtil.SavePrefab(shell.Root.gameObject, $"{PrefabGenUtil.PrefabRoot}/Panels/Panel_Inventory.prefab");
        }
    }
}
