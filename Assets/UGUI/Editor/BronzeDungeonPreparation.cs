using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>던전 표시만 갱신한다. 내장 카드와 팝업을 함께 처리하고 기존 기능 참조를 보존한다.</summary>
    public static class BronzeDungeonPreparation
    {
        private const string Root = "Assets/UGUI/Prefabs/";
        private static readonly string[] Paths =
        {
            "Items/Dungeons/Item_DungeonCard.prefab",
            "Popups/Popup_DungeonDifficulty.prefab",
            "Panels/Panel_Dungeon.prefab"
        };

        [MenuItem("KingdomIdle/UGUI/청동 원정대/던전 표시 적용")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("플레이 모드를 종료해 주세요.");
            F.Init();
            F.Catalog = AssetDatabase.LoadAssetAtPath<UIViewCatalog>("Assets/UGUI/UIViewCatalog.asset");
            foreach (string relative in Paths)
            {
                string path = Root + relative;
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var references = CaptureReferences(root);
                    foreach (var card in root.GetComponentsInChildren<DungeonCardView>(true)) ApplyCard(card);
                    foreach (var popup in root.GetComponentsInChildren<DungeonDifficultyPopupView>(true)) ApplyPopup(popup);
                    RecordNestedOverrides(root);
                    AssertReferences(references);
                    ValidateRoot(root);
                    PrefabUtility.SaveAsPrefabAsset(root, path, out bool saved);
                    if (!saved) throw new InvalidOperationException("던전 프리팹 저장 실패: " + path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
            Validate();
            Debug.Log("[BronzeDungeonPreparation] 던전 카드·내장 난이도 팝업 표시 적용. 입장·티켓·보상·스크롤 참조 보존.");
        }

        [MenuItem("KingdomIdle/UGUI/청동 원정대/던전 표시 검증")]
        public static void Validate()
        {
            foreach (string relative in Paths)
            {
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(Root + relative);
                if (root == null) throw new InvalidOperationException("던전 프리팹 없음: " + relative);
                ValidateRoot(root);
            }
            Debug.Log(Newtonsoft.Json.JsonConvert.SerializeObject(ValidateGeometry(), Newtonsoft.Json.Formatting.Indented));
        }

        /// <summary>입장 처리를 실행하지 않고 실제 프리팹의 상태별 문구와 버튼 가드를 대조한다.</summary>
        public static void ValidateEntryState()
        {
            if (Application.isPlaying) throw new InvalidOperationException("입장 표시 검사는 EditMode에서 실행해 주세요.");
            var canvas = new GameObject("DungeonEntryPresentation", typeof(RectTransform), typeof(Canvas));
            canvas.hideFlags = HideFlags.HideAndDontSave;
            canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            ((RectTransform)canvas.transform).sizeDelta = new Vector2(1080, 1920);
            var fixture = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Popups/Popup_DungeonDifficulty.prefab"), canvas.transform, false);
            fixture.SetActive(true);
            var checks = new List<string>();
            void Check(bool ok, string name)
            {
                if (!ok) throw new InvalidOperationException("Dungeon entry presentation: " + name);
                checks.Add(name);
            }
            try
            {
                var view = fixture.GetComponent<DungeonDifficultyPopupView>();
                var enter = Ref<Button>(view, "enterButton");
                var label = enter.GetComponentInChildren<TMP_Text>(true);
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                var type = typeof(DungeonDifficultyPopupView);
                void Display(Scripts.Core.eStage stage, int tickets, bool pending, string text, bool enabled)
                {
                    type.GetField("selectedStageId", flags).SetValue(view, stage);
                    type.GetField("displayedTickets", flags).SetValue(view, tickets);
                    type.GetField("entryPending", flags).SetValue(view, pending);
                    type.GetMethod("RefreshEnterButton", flags).Invoke(view, null);
                    Check(enter.interactable == enabled && label.text == text, text + " matches the existing entry guard");
                    label.ForceMeshUpdate(true, true);
                    Check(label.textInfo.lineCount <= 1 && label.GetPreferredValues(text).x <= label.rectTransform.rect.width + 1,
                        text + " fits the existing footer label");
                }
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)fixture.transform.Find("Window"));
                Canvas.ForceUpdateCanvases();
                Display(Scripts.Core.eStage.GoldDungeon1_1, 0, false, "입장권 없음", false);
                Display(Scripts.Core.eStage.GoldDungeon1_1, 1, false, "입장하기", true);
                Display(Scripts.Core.eStage.GoldDungeon1_1, 1, true, "입장 대기 중", false);
                Display(default, 1, false, "입장 잠김", false);
                Display(Scripts.Core.eStage.RubyDungeon1_1, 2, false, "준비 중", false);
                var row = fixture.GetComponentInChildren<DungeonDifficultyRowView>(true);
                row.Configure(1, true, 0, 0, null);
                Check(Ref<TMP_Text>(row, "powerLabel").text == "선택 가능", "Unlocked difficulty describes selection, not ticket availability");
                row.Configure(2, false, 0, 0, null);
                Check(!Ref<Button>(row, "button").interactable && Ref<TMP_Text>(row, "powerLabel").text == "이전 단계를 클리어하세요",
                    "Locked difficulty still preserves the unlock rule");
                Check(((RectTransform)enter.transform).rect.height >= 144, "Footer retains a 144px touch height");
                Debug.Log("BRONZE DUNGEON ENTRY PRESENTATION PASSED " + checks.Count);
            }
            finally { Object.DestroyImmediate(canvas); }
        }

        private static void ApplyCard(DungeonCardView card)
        {
            var root = card.transform;
            Flat(root.GetComponent<Image>(), UguiTheme.InfoSurface);
            Height(root, 240);
            var group = root.GetComponent<CanvasGroup>();
            if (group != null) group.alpha = 1f; // 준비 중 상태도 글자는 같은 명도로 읽힌다.
            var button = Ref<Button>(card, "button");
            NeutralTint(button);
            var row = root.GetComponent<HorizontalLayoutGroup>();
            if (row != null) { row.spacing = 18; row.padding = new RectOffset(22, 24, 16, 16); }
            var preview = root.Find("PreviewFrame");
            if (preview != null)
            {
                Width(preview, 200); Height(preview, 180);
                Flat(preview.GetComponent<Image>(), UguiTheme.RusticPanelDeep);
                var tag = preview.Find("LockedTag");
                if (tag != null)
                {
                    ((RectTransform)tag).sizeDelta = new Vector2(174, 64);
                    Flat(tag.GetComponent<Image>(), UguiTheme.RusticSurface);
                    var tagLabel = tag.GetComponentInChildren<TMP_Text>(true);
                    Type(tagLabel, 34, UguiTheme.Parchment);
                    tagLabel.alignment = TextAlignmentOptions.Center;
                }
            }
            var title = Ref<TMP_Text>(card, "dungeonName");
            var description = Ref<TMP_Text>(card, "description");
            Type(title, 46, UguiTheme.InfoInk); Height(title.transform, 64);
            Type(description, 36, UguiTheme.InfoMuted); Height(description.transform, 52);
            description.textWrappingMode = TextWrappingModes.Normal;
            // 기존 카드의 보상 목적을 짧게 표시한다. 해금·보상 정책은 만들지 않는다.
            if (card.DungeonType == Scripts.Core.eStageType.GoldDungeon || card.DungeonName.Contains("골드")) description.text = "미믹을 처치해 골드를 획득하세요.";
            else if (card.DungeonType == Scripts.Core.eStageType.RubyDungeon || card.DungeonName.Contains("루비")) description.text = "루비 광산 · 준비 중";
            else if (card.DungeonName.Contains("장비")) description.text = "황폐한 전장 · 준비 중";
            var nameRow = title.transform.parent;
            Height(nameRow, 64);
            var info = root.Find("InfoCol")?.GetComponent<VerticalLayoutGroup>();
            if (info != null) info.spacing = 8;
            var chevron = root.Find("Chevron")?.GetComponent<Image>();
            if (chevron != null) chevron.color = UguiTheme.InfoMuted;
            DisableDecoration(root);
        }

        private static void ApplyPopup(DungeonDifficultyPopupView popup)
        {
            // 이 팝업은 중앙 본문 영역만 덮는다. 상단 왕국군 HUD를 계속 표시한다.
            foreach (var suppressor in popup.GetComponentsInChildren<PartyHudSuppressor>(true))
                Object.DestroyImmediate(suppressor);
            var window = popup.transform.Find("Window") as RectTransform;
            if (window == null) throw new InvalidOperationException("던전 난이도 Window 누락");
            // 폭 정책은 유지하고, 높이는 선호 콘텐츠가 중앙 가용 영역을 넘지 않도록 전용 피터가 소유한다.
            var contentFit = window.GetComponent<ContentSizeFitter>();
            if (contentFit != null) contentFit.enabled = false;
            if (window.GetComponent<DungeonDifficultySizeFitter>() == null) window.gameObject.AddComponent<DungeonDifficultySizeFitter>();
            Flat(window.GetComponent<Image>(), UguiTheme.RusticPanel);
            var layout = window.GetComponent<VerticalLayoutGroup>();
            if (layout != null) { layout.spacing = 5; layout.padding = new RectOffset(18, 18, 18, 18); }
            var preview = window.Find("PreviewCard") as RectTransform;
            if (preview == null) throw new InvalidOperationException("던전 프리뷰 카드 누락");
            Flat(preview.GetComponent<Image>(), UguiTheme.InfoSurface);
            Height(preview, 232);
            var band = preview.Find("TitleBand") as RectTransform;
            if (band == null) throw new InvalidOperationException("던전 설명 영역 누락");
            var bandImage = band.GetComponent<Image>();
            if (bandImage != null) bandImage.color = Color.clear;
            SetRect(band, Vector2.zero, Vector2.one, new Vector2(16, 2), new Vector2(-16, -2));
            var bandLayout = band.GetComponent<VerticalLayoutGroup>();
            if (bandLayout != null) { bandLayout.padding = new RectOffset(); bandLayout.spacing = 2; }
            var name = Ref<TMP_Text>(popup, "dungeonName");
            var description = Ref<TMP_Text>(popup, "description");
            Type(name, 54, UguiTheme.InfoInk); Height(name.transform, 64);
            Type(description, 34, UguiTheme.InfoMuted); Height(description.transform, 162);
            description.textWrappingMode = TextWrappingModes.Normal;
            description.overflowMode = TextOverflowModes.Ellipsis;
            var art = preview.Find("PortalArtFrame") as RectTransform;
            if (art == null) art = F.Box(preview, "PortalArtFrame", UguiTheme.RusticPanelDeep, rounded: true).rectTransform;
            // 보상 조건은 원래 설명 높이와 전체 폭으로 보존하고, 그림은 제목 우측에만 둔다.
            SetRect(art, Vector2.one, Vector2.one, new Vector2(-80, -72), new Vector2(-16, -8));
            name.margin = Vector4.zero;
            art.gameObject.SetActive(false);
            Flat(art.GetComponent<Image>(), UguiTheme.RusticPanelDeep);
            var image = Ref<Image>(popup, "mainImage");
            if (image.transform.parent != art) image.transform.SetParent(art, false);
            F.Stretch(image.rectTransform);
            image.rectTransform.offsetMin = new Vector2(10, 10);
            image.rectTransform.offsetMax = new Vector2(-10, -10);
            image.preserveAspect = true;
            image.raycastTarget = false;
            Frame(art, "PortalBorder", UguiTheme.Bronze);
            var section = window.Find("LblDifficultySection")?.GetComponent<TMP_Text>();
            Type(section, 42, UguiTheme.Parchment); Height(section.transform, 52);
            // 슬롯과 순회 버튼은 그대로 두고, 위아래 빈 공간만 줄인다.
            var infoRow = window.Find("InfoRow");
            if (infoRow != null)
            {
                Height(infoRow, 144);
                foreach (var carousel in infoRow.GetComponentsInChildren<DungeonInfoCarouselView>(true))
                {
                    var column = carousel.GetComponent<VerticalLayoutGroup>();
                    if (column != null) { column.padding = new RectOffset(52, 52, 1, 1); column.spacing = 2; }
                    var heading = carousel.transform.Find("Title");
                    if (heading != null) Height(heading, 54);
                    var carouselRow = carousel.transform.Find("CarouselRow");
                    if (carouselRow != null) Height(carouselRow, 86);
                    // 화살표의 기존 132px 터치를 유지하면서 제목/아이콘의 세로 최소 높이에서는 분리한다.
                    foreach (string arrowName in new[] { "PreviousButton", "NextButton" })
                    {
                        var arrow = carouselRow?.Find(arrowName) ?? carousel.transform.Find(arrowName);
                        if (arrow == null) continue;
                        if (arrow.parent != carousel.transform) arrow.SetParent(carousel.transform, false);
                        Layout(arrow).ignoreLayout = true;
                        Width(arrow, 40); Height(arrow, 132);
                        bool next = arrowName == "NextButton";
                        var rect = (RectTransform)arrow;
                        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(next ? 1 : 0, .5f);
                        rect.anchoredPosition = new Vector2(next ? -6 : 6, 0);
                        rect.sizeDelta = new Vector2(40, 132);
                        var icon = arrow.Find("Icon") as RectTransform;
                        if (icon != null) icon.anchoredPosition = new Vector2(0, -28);
                    }
                }
            }
            var scroll = Ref<ScrollRect>(popup, "difficultyScroll");
            var scrollLayout = Layout(scroll.transform);
            scrollLayout.minHeight = 0; scrollLayout.preferredHeight = 462; scrollLayout.flexibleHeight = 1;
            foreach (var row in popup.GetComponentsInChildren<DungeonDifficultyRowView>(true)) ApplyRow(row);
            var footer = window.Find("Footer");
            Height(footer, 144);
            Type(Ref<TMP_Text>(popup, "selectedDifficultyLabel"), 36, UguiTheme.Parchment);
            var enter = Ref<Button>(popup, "enterButton");
            Flat(enter.GetComponent<Image>(), UguiTheme.ActionGold);
            var enterLabel = enter.GetComponentInChildren<TMP_Text>(true);
            Type(enterLabel, 42, UguiTheme.InfoInk);
            enterLabel.alignment = TextAlignmentOptions.Center;
            Width(enter.transform, 350); Height(enter.transform, 144);
            NeutralTint(enter, tintDisabled: true);
            ApplyCloseButton(popup, window, footer, enter);
            Frame(window, "BronzeDungeonFrame", UguiTheme.BronzeLight);
            DisableDecoration(window);
        }

        private static void ApplyCloseButton(DungeonDifficultyPopupView popup, RectTransform window, Transform footer, Button enter)
        {
            // 일부 기존 프리팹에는 상단 닫기가 없거나 숨겨져 있다. 아트와 겹치지 않는 푸터 보조 행동을 보장한다.
            var oldClose = window.Find("BtnClose");
            if (oldClose != null) oldClose.gameObject.SetActive(false);
            var close = footer.Find("BtnClose")?.GetComponent<Button>();
            if (close == null)
                close = F.TextButton(footer, "BtnClose", "닫기", 38, UguiTheme.RusticSurface, out _, UguiTheme.Parchment);
            close.gameObject.SetActive(true);
            close.enabled = true;
            close.interactable = true;
            Width(close.transform, 200); Height(close.transform, 144);
            Flat(close.GetComponent<Image>(), UguiTheme.RusticSurface);
            close.GetComponent<Image>().raycastTarget = true;
            var label = close.GetComponentInChildren<TMP_Text>(true);
            Type(label, 38, UguiTheme.Parchment);
            label.text = "닫기";
            label.alignment = TextAlignmentOptions.Center;
            NeutralTint(close);
            close.transform.SetAsLastSibling();
            enter.transform.SetAsLastSibling();
            var footerLayout = footer.GetComponent<HorizontalLayoutGroup>();
            if (footerLayout != null) footerLayout.childForceExpandWidth = false;
            var selection = Ref<TMP_Text>(popup, "selectedDifficultyLabel");
            var selectionLayout = Layout(selection.transform);
            selectionLayout.minWidth = selectionLayout.preferredWidth = 0;
            selectionLayout.flexibleWidth = 1;
            selection.textWrappingMode = TextWrappingModes.Normal;
            Height(selection.transform, 144);

            bool connected = false;
            for (int i = 0; i < close.onClick.GetPersistentEventCount(); i++)
                if (close.onClick.GetPersistentTarget(i) == popup &&
                    close.onClick.GetPersistentMethodName(i) == nameof(DungeonDifficultyPopupView.Hide)) connected = true;
            if (!connected) UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(close.onClick, popup.Hide);
        }

        private static void ApplyRow(DungeonDifficultyRowView row)
        {
            Height(row.transform, 144);
            Flat(Ref<Image>(row, "background"), UguiTheme.InfoSurface);
            Type(Ref<TMP_Text>(row, "stageLabel"), 40, UguiTheme.InfoInk);
            var power = Ref<TMP_Text>(row, "powerLabel");
            Type(power, 34, UguiTheme.InfoMuted);
            power.textWrappingMode = TextWrappingModes.Normal;
            power.overflowMode = TextOverflowModes.Ellipsis;
            var locked = Ref<GameObject>(row, "lockIndicator");
            Flat(locked.GetComponent<Image>(), UguiTheme.RusticSurface);
            var lockedLabel = locked.GetComponentInChildren<TMP_Text>(true);
            Type(lockedLabel, 32, UguiTheme.Parchment);
            lockedLabel.alignment = TextAlignmentOptions.Center;
            Width(locked.transform, 106); Height(locked.transform, 52);
            NeutralTint(Ref<Button>(row, "button"));
        }

        private static void RecordNestedOverrides(GameObject root)
        {
            // Panel_Dungeon 안의 카드와 팝업은 중첩 프리팹이다. 내장 인스턴스의 기존 오버라이드도 갱신한다.
            foreach (var component in root.GetComponentsInChildren<Component>(true))
            {
                if (component == null) continue;
                EditorUtility.SetDirty(component);
                if (PrefabUtility.IsPartOfPrefabInstance(component))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            }
        }

        private static Dictionary<MonoBehaviour, string> CaptureReferences(GameObject root)
        {
            var result = new Dictionary<MonoBehaviour, string>();
            foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
                if (component is DungeonPanelController || component is DungeonCardView || component is DungeonDifficultyPopupView ||
                    component is DungeonDifficultyRowView || component is DungeonInfoCarouselView || component is DungeonInfoSlotView)
                    result.Add(component, EditorJsonUtility.ToJson(component));
            return result;
        }

        private static void AssertReferences(Dictionary<MonoBehaviour, string> before)
        {
            foreach (var pair in before)
                if (pair.Key == null || EditorJsonUtility.ToJson(pair.Key) != pair.Value)
                    throw new InvalidOperationException("던전 기능 참조 변경 감지: " + pair.Key);
        }

        private static void ValidateRoot(GameObject root)
        {
            foreach (var card in root.GetComponentsInChildren<DungeonCardView>(true))
            {
                if (Ref<TMP_Text>(card, "dungeonName").color != UguiTheme.InfoInk ||
                    Ref<TMP_Text>(card, "description").color != UguiTheme.InfoMuted ||
                    Ref<Button>(card, "button") == null || LayoutUtility.GetPreferredHeight((RectTransform)card.transform) > 240.5f)
                    throw new InvalidOperationException("던전 카드 표시/참조 불일치: " + card.name);
            }
            foreach (var popup in root.GetComponentsInChildren<DungeonDifficultyPopupView>(true))
            {
                var window = popup.transform.Find("Window");
                if (window == null || window.GetComponent<DungeonDifficultySizeFitter>() == null ||
                    window.GetComponent<ContentSizeFitter>()?.enabled == true)
                    throw new InvalidOperationException("던전 상세 높이 소유자 불일치: " + popup.name);
                var close = popup.transform.Find("Window/Footer/BtnClose")?.GetComponent<Button>();
                bool connected = false;
                if (close != null)
                    for (int i = 0; i < close.onClick.GetPersistentEventCount(); i++)
                        if (close.onClick.GetPersistentTarget(i) == popup &&
                            close.onClick.GetPersistentMethodName(i) == nameof(DungeonDifficultyPopupView.Hide)) connected = true;
                var closeLayout = close != null ? close.GetComponent<LayoutElement>() : null;
                if (close == null || !close.gameObject.activeSelf || !close.enabled || !close.interactable ||
                    !connected || closeLayout == null || closeLayout.minHeight < 144 || closeLayout.minWidth < 200)
                    throw new InvalidOperationException("던전 상세 닫기 버튼/Hide 연결 불일치: " + popup.name);
                if (popup.GetComponentsInChildren<PartyHudSuppressor>(true).Length != 0 ||
                    Ref<Image>(popup, "mainImage").transform.parent.gameObject.activeSelf ||
                    Ref<TMP_Text>(popup, "dungeonName").color != UguiTheme.InfoInk ||
                    Ref<Button>(popup, "enterButton").GetComponent<Image>().color != UguiTheme.ActionGold ||
                    Ref<ScrollRect>(popup, "difficultyScroll") == null)
                    throw new InvalidOperationException("던전 상세 표시/참조 불일치: " + popup.name);
            }
        }

        /// <summary>실제 내장 팝업과 기존 스크롤의 기하만 검사한다. 입장·선택·계정 API는 호출하지 않는다.</summary>
        public static object ValidateGeometry()
        {
            if (Application.isPlaying) throw new InvalidOperationException("던전 기하 검사는 EditMode에서 실행해 주세요.");
            var checks = new List<string>();
            void Check(bool value, string message)
            {
                if (!value) throw new InvalidOperationException("Bronze Dungeon geometry: " + message);
                checks.Add(message);
            }
            var fixture = new GameObject("BronzeDungeonGeometry", typeof(RectTransform), typeof(Canvas));
            fixture.hideFlags = HideFlags.HideAndDontSave;
            fixture.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var fixtureRect = (RectTransform)fixture.transform;
            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Panels/Panel_Dungeon.prefab");
                var panel = Object.Instantiate(prefab, fixture.transform, false).GetComponent<DungeonPanelView>();
                var panelRect = (RectTransform)panel.transform;
                panelRect.anchorMin = Vector2.zero; panelRect.anchorMax = Vector2.one;
                var popup = panel.GetComponentInChildren<DungeonDifficultyPopupView>(true);
                popup.gameObject.SetActive(true);
                popup.SetInfoItems(Array.Empty<Sprite>(), Array.Empty<Sprite>());
                var window = (RectTransform)popup.transform.Find("Window");
                var fitter = window.GetComponent<DungeonDifficultySizeFitter>();
                Check(fitter != null, "The embedded dungeon window has its dedicated height owner");
                var title = Ref<TMP_Text>(popup, "dungeonName");
                title.text = "골드 던전";
                Ref<TMP_Text>(popup, "description").text = "제한시간: 60초\n등장 미믹: 20\n최대 획득 골드: 123.45K";
                var scroll = Ref<ScrollRect>(popup, "difficultyScroll");
                var enter = Ref<Button>(popup, "enterButton");
                var close = window.Find("Footer/BtnClose").GetComponent<Button>();
                var hud = (RectTransform)new GameObject("HudTopBoundary", typeof(RectTransform)).transform;
                hud.SetParent(panelRect, false);
                hud.anchorMin = new Vector2(0, 1); hud.anchorMax = Vector2.one; hud.pivot = new Vector2(.5f, 1);
                hud.sizeDelta = new Vector2(0, UguiTheme.HudTopHeight); hud.anchoredPosition = Vector2.zero;
                var sheetFit = panel.Sheet.GetComponent<SheetSizeFitter>();
                var clip = (RectTransform)panel.Sheet.parent;
                var infoIcon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UGUI/UsingAssets/Dungeon_Chest01.png");
                Check(infoIcon != null, "Existing sprite is available for complete reward/monster carousel coverage");
                foreach (bool withInfo in new[] { false, true })
                foreach (var size in new[] { new Vector3(2316, 0, 0), new Vector3(1920, 0, 0),
                    new Vector3(1440, 0, 0), new Vector3(1440, 64, 24), new Vector3(1920, 0, 0) })
                {
                    fixtureRect.sizeDelta = new Vector2(1080, size.x);
                    panelRect.offsetMin = new Vector2(0, size.z); panelRect.offsetMax = new Vector2(0, -size.y);
                    sheetFit.SetTopBoundary(hud);
                    var icons = withInfo ? new[] { infoIcon, infoIcon, infoIcon, infoIcon, infoIcon } : Array.Empty<Sprite>();
                    popup.SetInfoItems(icons, icons);
                    LayoutRebuilder.ForceRebuildLayoutImmediate(window);
                    fitter.RefreshNow();
                    LayoutRebuilder.ForceRebuildLayoutImmediate(window);
                    Canvas.ForceUpdateCanvases();
                    // 캐러셀은 viewport 크기가 확정된 뒤에도 모든 아이콘과 마지막 페이지를 보존해야 한다.
                    popup.SetInfoItems(icons, icons);
                    LayoutRebuilder.ForceRebuildLayoutImmediate(window);
                    Canvas.ForceUpdateCanvases();
                    string label = "1080x" + size.x + " safeTop=" + size.y + " safeBottom=" + size.z + " info=" + withInfo;
                    var available = RectBounds(panelRect, clip);
                    var bounds = RectBounds(panelRect, window);
                    Check(Mathf.Abs(bounds.min.y - available.min.y - 18) <= .5f && Mathf.Abs(available.max.y - bounds.max.y - 18) <= .5f &&
                        Mathf.Abs(bounds.min.x - available.min.x - 18) <= .5f && Mathf.Abs(available.max.x - bounds.max.x - 18) <= .5f,
                        label + " keeps the full window below the party HUD and above the bottom navigation");
                    foreach (var target in new[] { title.rectTransform, (RectTransform)enter.transform, (RectTransform)close.transform })
                    {
                        var targetBounds = RectBounds(panelRect, target);
                        Check(targetBounds.min.y >= bounds.min.y - .5f && targetBounds.max.y <= bounds.max.y + .5f,
                            label + " keeps " + target.name + " fully inside the window");
                    }
                    Check(((RectTransform)enter.transform).rect.height >= 144 && ((RectTransform)close.transform).rect.height >= 144 &&
                        title.rectTransform.rect.height + 1 >= title.GetPreferredValues(title.text).y,
                        label + " preserves readable title and both 144px footer touch areas");
                    var description = Ref<TMP_Text>(popup, "description");
                    var descriptionPreferred = description.GetPreferredValues(description.text, description.rectTransform.rect.width, Mathf.Infinity);
                    Check(descriptionPreferred.y <= description.rectTransform.rect.height + 1,
                        label + " retains the full selected-difficulty reward and reset description; rect=" + description.rectTransform.rect +
                        " preferred=" + descriptionPreferred + " fontSize=" + description.fontSize);
                    string originalDescription = description.text;
                    foreach (string text in new[]
                    {
                        "제한시간: 60초\n등장 미믹: 20\n최대 획득 골드: " + NumberNotation.Exact(long.MaxValue),
                        "Time limit: 60s\nMimics: 20\nMaximum gold: " + NumberNotation.Exact(long.MaxValue)
                    })
                    {
                        var preferred = description.GetPreferredValues(text, description.rectTransform.rect.width, Mathf.Infinity);
                        Check(preferred.y <= description.rectTransform.rect.height + 1,
                            label + " fits complete Korean/Latin long reward conditions; rect=" + description.rectTransform.rect +
                            " preferred=" + preferred + " fontSize=" + description.fontSize + " text=" + text);
                    }
                    description.text = originalDescription;
                    if (withInfo)
                        foreach (var carousel in popup.GetComponentsInChildren<DungeonInfoCarouselView>(true))
                        {
                            var heading = carousel.transform.Find("Title").GetComponent<TMP_Text>();
                            var headingPreferred = heading.GetPreferredValues(heading.text, heading.rectTransform.rect.width, Mathf.Infinity);
                            Check(headingPreferred.y <= heading.rectTransform.rect.height + 1,
                                label + " retains the complete " + carousel.name + " title; text=" + heading.text +
                                " rect=" + heading.rectTransform.rect + " preferred=" + headingPreferred + " fontSize=" + heading.fontSize);
                            var content = Ref<RectTransform>(carousel, "content");
                            int visibleSlots = 0;
                            foreach (Transform child in content) if (child.gameObject.activeSelf) visibleSlots++;
                            Check(visibleSlots == icons.Length, label + " retains all five " + carousel.name + " entries");
                            var carouselScroll = Ref<ScrollRect>(carousel, "scrollRect");
                            var infoBand = (RectTransform)carousel.transform.parent;
                            foreach (var part in new[] { heading.rectTransform, carouselScroll.viewport,
                                (RectTransform)Ref<Button>(carousel, "previousButton").transform,
                                (RectTransform)Ref<Button>(carousel, "nextButton").transform })
                            {
                                var partBounds = RectBounds(infoBand, part);
                                Check(partBounds.min.y >= infoBand.rect.yMin - 1 && partBounds.max.y <= infoBand.rect.yMax + 1,
                                    label + " keeps " + carousel.name + "/" + part.name + " inside the info band without overlapping difficulty selection");
                            }
                            carouselScroll.horizontalNormalizedPosition = 1;
                            Canvas.ForceUpdateCanvases();
                            var finalIcon = (RectTransform)content.GetChild(content.childCount - 1);
                            var iconBounds = RectBounds(carouselScroll.viewport, finalIcon);
                            Check(iconBounds.min.x >= carouselScroll.viewport.rect.xMin - 1 && iconBounds.max.x <= carouselScroll.viewport.rect.xMax + 1,
                                label + " can reach the final " + carousel.name + " entry without removing rewards");
                            Check(iconBounds.min.y >= carouselScroll.viewport.rect.yMin - 1 && iconBounds.max.y <= carouselScroll.viewport.rect.yMax + 1,
                                label + " keeps reward/monster icons vertically within the compact info band");
                        }
                    int bottomPadding = scroll.content.GetComponent<VerticalLayoutGroup>()?.padding.bottom ?? 0;
                    Check(scroll.vertical && scroll.viewport.rect.height >= 144 + bottomPadding - .5f && scroll.content.rect.height >= 144,
                        label + " retains one complete difficulty row plus its bottom padding and internal scrolling; viewport=" + scroll.viewport.rect +
                        " content=" + scroll.content.rect + " bottomPadding=" + bottomPadding);
                    scroll.verticalNormalizedPosition = 0;
                    Canvas.ForceUpdateCanvases();
                    var last = (RectTransform)scroll.content.GetChild(scroll.content.childCount - 1);
                    var lastBounds = RectBounds(scroll.viewport, last);
                    Check(lastBounds.min.y >= scroll.viewport.rect.yMin - 1 && lastBounds.max.y <= scroll.viewport.rect.yMax + 1,
                        label + " can scroll the final difficulty row fully into view; viewport=" + scroll.viewport.rect + " last=" + lastBounds +
                        " content=" + scroll.content.rect + " bottomPadding=" + bottomPadding);
                    if (size.x >= 1920)
                        Check(Mathf.Abs(bounds.min.y - available.min.y - (available.max.y - bounds.max.y)) <= 1,
                            label + " preserves or restores the unconstrained phone layout");
                    window.hasChanged = false;
                    for (int i = 0; i < 20; i++) fitter.RefreshNow();
                    Check(!window.hasChanged, label + " does not rewrite unchanged window geometry");
                }
                var detail = Ref<TMP_Text>(popup, "description");
                detail.text = "제한시간: 60초\n등장 미믹: 20\n최대 획득 골드: 123.45K";
                var rubyPreferred = detail.GetPreferredValues(detail.text, detail.rectTransform.rect.width, Mathf.Infinity);
                Check(rubyPreferred.y <= detail.rectTransform.rect.height + 1,
                    "Ruby first-clear quantity, payout condition and reset description fit without dropping information; rect=" + detail.rectTransform.rect +
                    " preferred=" + rubyPreferred + " fontSize=" + detail.fontSize);
                LayoutRebuilder.ForceRebuildLayoutImmediate(panel.Sheet);
                Canvas.ForceUpdateCanvases();
                foreach (var card in panel.GetComponentsInChildren<DungeonCardView>(true))
                {
                    var rect = (RectTransform)card.transform;
                    var preview = (RectTransform)card.transform.Find("PreviewFrame");
                    var description = Ref<TMP_Text>(card, "description");
                    var artBounds = RectBounds(rect, preview);
                    var textBounds = RectBounds(rect, description.rectTransform);
                    Check(Mathf.Abs(rect.rect.height - 240) <= 1 && preview.rect.height >= 179 && preview.rect.height <= 181,
                        card.name + " uses the compact 240px card with 180px preview");
                    Check(artBounds.min.y >= rect.rect.yMin - 1 && artBounds.max.y <= rect.rect.yMax + 1 && artBounds.max.x <= textBounds.min.x + 1,
                        card.name + " separates art from its description within the whole-card touch target");
                }
            }
            finally { Object.DestroyImmediate(fixture); }
            return new { passed = checks.Count, checks, source = "Actual embedded prefab and UGUI geometry only; no account, network, entry, ticket or reward operation" };
        }

        private static Bounds RectBounds(RectTransform root, RectTransform target)
        {
            var corners = new Vector3[4]; target.GetWorldCorners(corners);
            var bounds = new Bounds(root.InverseTransformPoint(corners[0]), Vector3.zero);
            for (int i = 1; i < corners.Length; i++) bounds.Encapsulate(root.InverseTransformPoint(corners[i]));
            return bounds;
        }

        private static T Ref<T>(Component owner, string field) where T : Object
        {
            using var serialized = new SerializedObject(owner);
            var property = serialized.FindProperty(field);
            var value = property != null ? property.objectReferenceValue as T : null;
            if (value == null) throw new InvalidOperationException(owner.GetType().Name + "." + field + " 참조 누락");
            return value;
        }
        private static LayoutElement Layout(Transform t) => t.GetComponent<LayoutElement>() ?? t.gameObject.AddComponent<LayoutElement>();
        private static void Height(Transform t, float value) { var le = Layout(t); le.minHeight = le.preferredHeight = value; le.flexibleHeight = 0; }
        private static void Width(Transform t, float value) { var le = Layout(t); le.minWidth = le.preferredWidth = value; le.flexibleWidth = 0; }
        private static void Type(TMP_Text text, float size, Color color)
        {
            if (text == null) throw new InvalidOperationException("던전 텍스트 누락");
            text.fontSize = text.fontSizeMin = text.fontSizeMax = size;
            text.enableAutoSizing = false; text.color = color; text.raycastTarget = false;
            text.alignment = TextAlignmentOptions.Left; text.fontStyle = FontStyles.Normal;
        }
        private static void Flat(Image image, Color color)
        {
            if (image == null) return;
            image.sprite = F.Rounded; image.type = Image.Type.Sliced; image.color = color; image.pixelsPerUnitMultiplier = 1;
        }
        private static void NeutralTint(Button button, bool tintDisabled = false)
        {
            if (button == null) return;
            var colors = button.colors;
            colors.normalColor = colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(.88f, .88f, .88f, 1);
            colors.disabledColor = tintDisabled ? new Color(.7f, .7f, .7f, 1) : Color.white;
            button.colors = colors;
        }
        private static void SetRect(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = offsetMin; rect.offsetMax = offsetMax;
        }
        private static void Frame(RectTransform parent, string name, Color color)
        {
            var frame = parent.Find(name)?.GetComponent<Image>();
            if (frame == null) frame = F.Frame(parent, name, color);
            frame.color = color; frame.raycastTarget = false; Layout(frame.transform).ignoreLayout = true;
            frame.transform.SetAsLastSibling();
        }
        private static void DisableDecoration(Transform root)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == "Sheen" || child.name == "InnerRim" || child.name == "CornerBrackets") child.gameObject.SetActive(false);
        }
    }
}
