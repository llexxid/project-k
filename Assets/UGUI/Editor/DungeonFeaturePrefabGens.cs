using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>
    /// 던전 기능 UI 프리팹 생성기 — 전부 현재 러스틱(Layer Lab) 스킨.
    /// 던전 카드 / 난이도 행 / 정보 슬롯 / 난이도 팝업 / 클리어 팝업 / 환생 팝업.
    /// 런타임 View 클래스(DungeonCardView 등)는 다른 멤버의 코드 그대로 — 직렬화 필드가
    /// private 이라 SerializedObject 로 배선한다 (View 코드는 건드리지 않는다).
    /// 던전 '패널'(하단 시트)은 PanelGens.GenerateDungeon 이 이 프리팹들을 조립해 만든다.
    /// </summary>
    internal static class DungeonFeaturePrefabGens
    {
        // 러스틱 던전 팔레트 (DungeonDifficultyRowView 코드 상수와 같은 계열)
        private static readonly Color CardBg = new Color(0.16f, 0.12f, 0.09f, 0.96f);       // 다크 우드 카드
        private static readonly Color SlotBg = new Color(0.10f, 0.08f, 0.06f, 0.96f);       // 더 깊은 슬롯/프리뷰 배경
        private static readonly Color BronzeFrame = new Color(UguiTheme.Bronze.r, UguiTheme.Bronze.g, UguiTheme.Bronze.b, 0.55f);
        private static readonly Color ParchmentText = new Color(0.95f, 0.90f, 0.80f, 1f);

        internal static void GenerateAll()
        {
            GenerateDungeonInfoSlot();
            GenerateDungeonDifficultyRow();
            GenerateDungeonCard();
            GenerateDungeonDifficultyPopup();
            GenerateDungeonClearPopup();
            GenerateReincarnationPopup();
        }

        // ═══ 배선 헬퍼 — private [SerializeField] 를 SerializedObject 로 채운다 ═══

        private static void Wire(Component target, params (string field, Object value)[] refs)
        {
            var so = new SerializedObject(target);
            foreach (var (field, value) in refs)
            {
                var prop = so.FindProperty(field);
                if (prop == null)
                {
                    Debug.LogError($"[DungeonGen] {target.GetType().Name}.{field} 직렬화 필드를 찾지 못했습니다.");
                    continue;
                }
                prop.objectReferenceValue = value;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ═══ 정보 슬롯 (보상/몬스터 캐러셀 칸) ═══

        private static GameObject GenerateDungeonInfoSlot()
        {
            var go = new GameObject("Item_DungeonInfoSlot", typeof(RectTransform));
            go.layer = 5;
            var bg = go.AddComponent<Image>();
            bg.sprite = F.Rounded;
            bg.type = Image.Type.Sliced;
            bg.color = SlotBg;
            bg.raycastTarget = false;
            ((RectTransform)go.transform).sizeDelta = new Vector2(82f, 82f);

            var layoutElement = go.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = 82f;
            layoutElement.preferredHeight = 82f;
            layoutElement.minWidth = 82f;
            layoutElement.minHeight = 82f;

            var frame = F.Frame(go.transform, "Frame", new Color(UguiTheme.Bronze.r, UguiTheme.Bronze.g, UguiTheme.Bronze.b, 0.40f));
            frame.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            var imgRt = F.Container(go.transform, "Image");
            F.Stretch(imgRt);
            imgRt.offsetMin = new Vector2(8f, 8f);
            imgRt.offsetMax = new Vector2(-8f, -8f);
            var itemImage = imgRt.gameObject.AddComponent<Image>();
            // 스프라이트가 없을 때의 색을 Awake가 placeholder 색으로 기억한다 → 은은한 빈 칸
            itemImage.color = new Color(1f, 1f, 1f, 0.06f);
            itemImage.raycastTarget = false;

            var placeholder = F.Text(go.transform, "Placeholder", "?", 24f, UguiTheme.TextTertiary,
                TextAlignmentOptions.Center, bold: true);
            F.Stretch(placeholder.rectTransform);

            var view = go.AddComponent<DungeonInfoSlotView>();
            Wire(view,
                ("itemImage", itemImage),
                ("layoutElement", layoutElement),
                ("placeholderLabel", placeholder));

            return PrefabGenUtil.SavePrefab(go, $"{PrefabGenUtil.PrefabRoot}/Items/Dungeons/Item_DungeonInfoSlot.prefab");
        }

        // ═══ 난이도 행 ═══

        private static GameObject GenerateDungeonDifficultyRow()
        {
            var go = new GameObject("Item_DungeonDifficultyRow", typeof(RectTransform));
            go.layer = 5;
            var bg = go.AddComponent<Image>();
            bg.sprite = F.Rounded;
            bg.type = Image.Type.Sliced;
            bg.color = CardBg;   // 초기값 — 상태색은 DungeonDifficultyRowView 코드가 덮어쓴다
            bg.raycastTarget = true;

            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 84f;
            le.flexibleWidth = 1f;

            var frame = F.Frame(go.transform, "Frame", new Color(UguiTheme.Bronze.r, UguiTheme.Bronze.g, UguiTheme.Bronze.b, 0.35f));
            frame.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.transition = Selectable.Transition.ColorTint;
            btn.colors = UguiTheme.MakeColorBlock();
            go.AddComponent<PlayClickSfxOnClick>();

            F.HLayout(go, 14f, new RectOffset(24, 18, 10, 10), TextAnchor.MiddleLeft);

            var stageLbl = F.Text(go.transform, "StageLabel", "1단계", 28f, ParchmentText,
                TextAlignmentOptions.Left, bold: true);
            F.Preferred(stageLbl, width: 130f, height: 40f);

            var powerLbl = F.Text(go.transform, "PowerLabel", "권장 전투력  0", 22f, UguiTheme.TextSecondary,
                TextAlignmentOptions.Left);
            F.Flexible(powerLbl, flexWidth: 1f);

            // 잠금 칩 (다크 크림슨 + 잠금 표기)
            var lockChip = F.Box(go.transform, "LockIndicator", new Color(0.30f, 0.10f, 0.08f, 0.95f), rounded: true);
            F.Preferred(lockChip, width: 96f, height: 44f);
            var lockLbl = F.Text(lockChip.transform, "Label", "잠금", 20f, new Color(1f, 0.82f, 0.78f, 0.95f),
                TextAlignmentOptions.Center, bold: true);
            F.Stretch(lockLbl.rectTransform);
            lockChip.gameObject.SetActive(false);

            var view = go.AddComponent<DungeonDifficultyRowView>();
            Wire(view,
                ("button", btn),
                ("background", bg),
                ("stageLabel", stageLbl),
                ("powerLabel", powerLbl),
                ("lockIndicator", lockChip.gameObject));

            return PrefabGenUtil.SavePrefab(go, $"{PrefabGenUtil.PrefabRoot}/Items/Dungeons/Item_DungeonDifficultyRow.prefab");
        }

        // ═══ 던전 카드 (패널 목록의 한 장) ═══

        private static GameObject GenerateDungeonCard()
        {
            var go = new GameObject("Item_DungeonCard", typeof(RectTransform));
            go.layer = 5;
            var bg = go.AddComponent<Image>();
            var kitCard = F.Catalog != null ? F.Catalog.kitCard : null;
            bg.sprite = kitCard != null ? kitCard : F.Rounded;
            bg.type = Image.Type.Sliced;
            bg.color = CardBg;
            bg.raycastTarget = true;

            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 210f;
            le.flexibleWidth = 1f;

            // 잠금 카드 딤 용 — 패널 생성기가 인스턴스별로 알파를 낮춘다
            go.AddComponent<CanvasGroup>();

            var frame = F.Frame(go.transform, "Frame", BronzeFrame);
            frame.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.transition = Selectable.Transition.ColorTint;
            btn.colors = UguiTheme.MakeColorBlock();
            go.AddComponent<PlayClickSfxOnClick>();

            F.HLayout(go, 18f, new RectOffset(16, 20, 14, 14), TextAnchor.MiddleLeft);

            // ── 프리뷰 창 (일러스트 + 청동 프레임) ──
            var preview = F.Box(go.transform, "PreviewFrame", SlotBg, rounded: true);
            F.Preferred(preview, width: 300f, height: 182f);
            var previewFrame = F.Frame(preview.transform, "Frame", new Color(UguiTheme.Bronze.r, UguiTheme.Bronze.g, UguiTheme.Bronze.b, 0.45f));
            previewFrame.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            var previewImgRt = F.Container(preview.transform, "Preview");
            F.Stretch(previewImgRt);
            previewImgRt.offsetMin = new Vector2(8f, 8f);
            previewImgRt.offsetMax = new Vector2(-8f, -8f);
            var previewImg = previewImgRt.gameObject.AddComponent<Image>();
            var illust = UguiGenAssets.ImageDungeon;
            if (illust != null) previewImg.sprite = illust;
            previewImg.preserveAspect = true;
            previewImg.raycastTarget = false;

            // 잠금 카드용 "준비 중" 태그 (기본 숨김 — 패널 생성기가 인스턴스별로 켠다)
            var lockedTag = F.Box(preview.transform, "LockedTag", new Color(0.08f, 0.06f, 0.05f, 0.88f), rounded: true);
            F.AnchorCenter(lockedTag.rectTransform, 150f, 52f);
            var lockedFrame = F.Frame(lockedTag.transform, "Frame", new Color(UguiTheme.Bronze.r, UguiTheme.Bronze.g, UguiTheme.Bronze.b, 0.6f));
            lockedFrame.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var lockedLbl = F.Text(lockedTag.transform, "Label", "준비 중", 22f, ParchmentText,
                TextAlignmentOptions.Center, bold: true);
            F.Stretch(lockedLbl.rectTransform);
            lockedTag.gameObject.SetActive(false);

            // ── 정보 열 (아이콘+이름 / 설명) ──
            var infoCol = F.Container(go.transform, "InfoCol");
            F.VLayout(infoCol.gameObject, 8f, null, TextAnchor.MiddleLeft, expandWidth: true);
            F.Flexible(infoCol.gameObject.AddComponent<LayoutElement>(), flexWidth: 1f);

            var nameRow = F.Container(infoCol, "NameRow");
            F.HLayout(nameRow.gameObject, 10f, null, TextAnchor.MiddleLeft);
            F.Preferred(nameRow.gameObject.AddComponent<LayoutElement>(), height: 48f);

            var iconImg = F.IconImage(nameRow, "DungeonIcon", UguiGenAssets.IconCoin, 44f, 44f);
            F.Preferred(iconImg, width: 44f, height: 44f);

            var nameLbl = F.Text(nameRow, "DungeonName", "골드 던전", 32f, ParchmentText,
                TextAlignmentOptions.Left, bold: true);
            F.Flexible(nameLbl, flexWidth: 1f);

            var descLbl = F.Text(infoCol, "Description", "던전 설명", 22f, UguiTheme.TextSecondary,
                TextAlignmentOptions.TopLeft, wrap: true);
            F.Preferred(descLbl, height: 60f);

            // ── 우측 진입 화살표 (arrow_back 좌우 반전) ──
            var chevron = F.IconImage(go.transform, "Chevron", F.Catalog != null ? F.Catalog.iconArrowLeft : null, 36f, 36f);
            F.Preferred(chevron, width: 36f, height: 36f);
            chevron.color = new Color(1f, 1f, 1f, 0.45f);
            chevron.transform.localScale = new Vector3(-1f, 1f, 1f);

            var view = go.AddComponent<DungeonCardView>();
            Wire(view,
                ("button", btn),
                ("previewImage", previewImg),
                ("dungeonName", nameLbl),
                ("description", descLbl),
                ("dungeonIcon", iconImg));

            return PrefabGenUtil.SavePrefab(go, $"{PrefabGenUtil.PrefabRoot}/Items/Dungeons/Item_DungeonCard.prefab");
        }

        // ═══ 난이도 선택 팝업 (패널 안 전체화면 모달) ═══

        private static GameObject GenerateDungeonDifficultyPopup()
        {
            var root = F.Root("Popup_DungeonDifficulty");
            var view = root.gameObject.AddComponent<DungeonDifficultyPopupView>();
            // 파티 HUD 는 LayerPopups 라 이 모달의 딤 위로 떠오른다 — 표시 중엔 내려 준다
            root.gameObject.AddComponent<PartyHudSuppressor>();

            var backdrop = MakeDim(root);

            // 창 본체 — 러스틱 윈도우 (kitWindow + 청동 프레임 + 코너 브래킷)
            var window = F.PixelPanel(root, "Window",
                F.Catalog != null ? F.Catalog.kitWindow : null, F.FrameGold, 24f,
                raycast: true, baseColor: F.PanelBaseDarker);
            F.AnchorCenter(window.rectTransform, 940f, 1420f);
            F.VLayout(window.gameObject, 16f, new RectOffset(34, 34, 26, 28), TextAnchor.UpperCenter);
            F.CornerBrackets(window.transform);

            // ── 프리뷰 카드 (카드의 일러스트/이름/설명을 이어받는다) ──
            var previewCard = F.Box(window.transform, "PreviewCard", SlotBg, rounded: true);
            F.Preferred(previewCard, height: 330f);
            var pcFrame = F.Frame(previewCard.transform, "Frame", BronzeFrame);
            pcFrame.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            var mainImgRt = F.Container(previewCard.transform, "MainImage");
            F.Stretch(mainImgRt);
            mainImgRt.offsetMin = new Vector2(10f, 10f);
            mainImgRt.offsetMax = new Vector2(-10f, -10f);
            var mainImg = mainImgRt.gameObject.AddComponent<Image>();
            var illust = UguiGenAssets.ImageDungeon;
            if (illust != null) mainImg.sprite = illust;
            mainImg.preserveAspect = true;
            mainImg.raycastTarget = false;

            // 하단 타이틀 밴드 (이름 + 설명)
            var titleBand = F.Box(previewCard.transform, "TitleBand", new Color(0f, 0f, 0f, 0.62f), rounded: true);
            var tbRt = titleBand.rectTransform;
            tbRt.anchorMin = new Vector2(0f, 0f);
            tbRt.anchorMax = new Vector2(1f, 0f);
            tbRt.pivot = new Vector2(0.5f, 0f);
            tbRt.anchoredPosition = new Vector2(0f, 8f);
            tbRt.sizeDelta = new Vector2(-16f, 104f);
            F.VLayout(titleBand.gameObject, 2f, new RectOffset(20, 20, 10, 10), TextAnchor.MiddleLeft);

            var nameLbl = F.Text(titleBand.transform, "DungeonName", "던전", 34f, UguiTheme.AccentGold,
                TextAlignmentOptions.Left, bold: true);
            F.Preferred(nameLbl, height: 44f);
            var descLbl = F.Text(titleBand.transform, "Description", "설명", 20f, UguiTheme.TextSecondary,
                TextAlignmentOptions.Left);
            F.Preferred(descLbl, height: 30f);

            // ── 보상/몬스터 캐러셀 2단 ──
            var infoRow = F.Container(window.transform, "InfoRow");
            F.HLayout(infoRow.gameObject, 16f, null, TextAnchor.MiddleCenter, expandWidth: true);
            F.Preferred(infoRow.gameObject.AddComponent<LayoutElement>(), height: 268f);

            var rewardCarousel = BuildInfoCarousel(infoRow, "ClearRewardCard", "클리어 보상");
            var monsterCarousel = BuildInfoCarousel(infoRow, "MonsterCard", "등장 몬스터");

            // ── 난이도 선택 ──
            var sectionLbl = F.Text(window.transform, "LblDifficultySection", "난이도 선택", 26f, ParchmentText,
                TextAlignmentOptions.Left, bold: true);
            F.Preferred(sectionLbl, height: 36f);

            var scroll = F.VScroll(window.transform, "DifficultyScroll", out var rowsContent,
                spacing: 10f, padding: new RectOffset(0, 0, 0, 4));
            F.Preferred(scroll.gameObject.AddComponent<LayoutElement>(), height: 462f);

            var rowPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"{PrefabGenUtil.PrefabRoot}/Items/Dungeons/Item_DungeonDifficultyRow.prefab");
            const int rowCount = 5;   // 골드/루비 각 5단계 — 데이터가 늘면 여기와 함께 조정
            var rowViews = new DungeonDifficultyRowView[rowCount];
            for (int i = 0; i < rowCount; i++)
            {
                var rowGo = (GameObject)PrefabUtility.InstantiatePrefab(rowPrefab);
                rowGo.transform.SetParent(rowsContent, false);
                rowGo.name = $"Row_{i + 1:00}";
                rowViews[i] = rowGo.GetComponent<DungeonDifficultyRowView>();
            }

            // ── 푸터 (선택 난이도 + 입장) ──
            var footer = F.Container(window.transform, "Footer");
            F.HLayout(footer.gameObject, 14f, null, TextAnchor.MiddleCenter, expandWidth: true);
            F.Preferred(footer.gameObject.AddComponent<LayoutElement>(), height: 86f);

            var selectedLbl = F.Text(footer, "SelectedDifficulty", "선택 난이도  1단계", 26f, ParchmentText,
                TextAlignmentOptions.Left, bold: true);
            F.Flexible(selectedLbl, flexWidth: 1f);

            var enterBtn = F.TextButton(footer, "EnterButton", "입장하기", 28f, UguiTheme.BtnSpend,
                out TextMeshProUGUI enterLbl, UguiTheme.TextPrimary, bold: true);
            F.Preferred(enterBtn, width: 280f, height: 78f);
            F.Stretch(enterLbl.rectTransform);

            // ── 우상단 원형 닫기 (패널 셸과 동일 언어) ──
            var closeImg = F.CircleBox(window.transform, "BtnClose", new Color(0.62f, 0.24f, 0.24f, 1f), raycast: true);
            var closeRt = closeImg.rectTransform;
            closeRt.anchorMin = new Vector2(1f, 1f);
            closeRt.anchorMax = new Vector2(1f, 1f);
            closeRt.pivot = new Vector2(1f, 1f);
            closeRt.anchoredPosition = new Vector2(-12f, -12f);
            closeRt.sizeDelta = new Vector2(60f, 60f);
            closeImg.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var closeBtn = closeImg.gameObject.AddComponent<Button>();
            closeBtn.targetGraphic = closeImg;
            closeBtn.transition = Selectable.Transition.ColorTint;
            closeBtn.colors = UguiTheme.MakeColorBlock();
            closeImg.gameObject.AddComponent<PlayClickSfxOnClick>();
            if (F.Catalog != null && F.Catalog.iconX != null)
            {
                var xIcon = F.IconImage(closeImg.transform, "Icon", F.Catalog.iconX, 30f, 30f);
                F.AnchorCenter(xIcon.rectTransform, 30f, 30f);
            }
            // View 에 닫기 필드가 없어 퍼시스턴트 리스너로 Hide 를 건다 (백드롭과 동일 동작)
            UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(closeBtn.onClick, view.Hide);

            // ── View 배선 ──
            Wire(view,
                ("backdropButton", backdrop),
                ("enterButton", enterBtn),
                ("mainImage", mainImg),
                ("dungeonName", nameLbl),
                ("description", descLbl),
                ("selectedDifficultyLabel", selectedLbl),
                ("difficultyScroll", scroll),
                ("clearRewardCarousel", rewardCarousel),
                ("monsterCarousel", monsterCarousel));

            var so = new SerializedObject(view);
            var rowsProp = so.FindProperty("difficultyRows");
            rowsProp.arraySize = rowCount;
            for (int i = 0; i < rowCount; i++)
                rowsProp.GetArrayElementAtIndex(i).objectReferenceValue = rowViews[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            root.gameObject.SetActive(false);
            return PrefabGenUtil.SavePrefab(
                root.gameObject,
                $"{PrefabGenUtil.PrefabRoot}/Popups/Popup_DungeonDifficulty.prefab");
        }

        /// <summary>보상/몬스터 가로 캐러셀 카드 한 장 — DungeonInfoCarouselView 배선까지 끝낸다.</summary>
        private static DungeonInfoCarouselView BuildInfoCarousel(RectTransform parent, string name, string title)
        {
            var card = F.Box(parent, name, CardBg, rounded: true);
            F.Flexible(card, flexWidth: 1f);
            var cardFrame = F.Frame(card.transform, "Frame", new Color(UguiTheme.Bronze.r, UguiTheme.Bronze.g, UguiTheme.Bronze.b, 0.40f));
            cardFrame.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            F.VLayout(card.gameObject, 8f, new RectOffset(12, 12, 10, 12), TextAnchor.UpperCenter);

            var titleLbl = F.Text(card.transform, "Title", title, 22f, ParchmentText,
                TextAlignmentOptions.Center, bold: true);
            F.Preferred(titleLbl, height: 32f);

            var row = F.Container(card.transform, "CarouselRow");
            F.HLayout(row.gameObject, 6f, null, TextAnchor.MiddleCenter);
            F.Flexible(row.gameObject.AddComponent<LayoutElement>(), flexHeight: 1f);

            Button MakeArrow(string btnName, bool right)
            {
                var arrowBg = F.Box(row, btnName, new Color(0.24f, 0.18f, 0.12f, 0.95f), rounded: true, raycast: true);
                F.Preferred(arrowBg, width: 40f, height: 88f);
                var b = arrowBg.gameObject.AddComponent<Button>();
                b.targetGraphic = arrowBg;
                b.transition = Selectable.Transition.ColorTint;
                b.colors = UguiTheme.MakeColorBlock();
                arrowBg.gameObject.AddComponent<PlayClickSfxOnClick>();
                var arrowIcon = F.IconImage(arrowBg.transform, "Icon", F.Catalog != null ? F.Catalog.iconArrowLeft : null, 22f, 22f);
                F.AnchorCenter(arrowIcon.rectTransform, 22f, 22f);
                if (right) arrowIcon.transform.localScale = new Vector3(-1f, 1f, 1f);
                return b;
            }

            var prevBtn = MakeArrow("PreviousButton", right: false);

            // 뷰포트 + 콘텐츠 (가로 스크롤)
            var scrollerRt = F.Container(row, "Scroller");
            F.Flexible(scrollerRt.gameObject.AddComponent<LayoutElement>(), flexWidth: 1f, flexHeight: 1f);
            var scrollRect = scrollerRt.gameObject.AddComponent<ScrollRect>();

            var viewportRt = F.Container(scrollerRt, "Viewport");
            F.Stretch(viewportRt);
            var vpImg = viewportRt.gameObject.AddComponent<Image>();
            vpImg.color = new Color(1f, 1f, 1f, 0.004f);   // 드래그 히트용 (거의 투명)
            viewportRt.gameObject.AddComponent<RectMask2D>();

            var contentRt = F.Container(viewportRt, "Content");
            contentRt.anchorMin = new Vector2(0f, 0f);
            contentRt.anchorMax = new Vector2(0f, 1f);
            contentRt.pivot = new Vector2(0f, 0.5f);
            contentRt.offsetMin = Vector2.zero;
            contentRt.offsetMax = new Vector2(300f, 0f);
            var layout = F.HLayout(contentRt.gameObject, 8f, null, TextAnchor.MiddleCenter);

            scrollRect.viewport = viewportRt;
            scrollRect.content = contentRt;
            scrollRect.horizontal = true;
            scrollRect.vertical = false;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 20f;

            // 슬롯 템플릿 (비활성 — 런타임 EnsureSlotCount 가 복제해 쓴다)
            var slotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"{PrefabGenUtil.PrefabRoot}/Items/Dungeons/Item_DungeonInfoSlot.prefab");
            var slotGo = (GameObject)PrefabUtility.InstantiatePrefab(slotPrefab);
            slotGo.transform.SetParent(contentRt, false);
            slotGo.name = "SlotTemplate";
            slotGo.SetActive(false);
            var slotView = slotGo.GetComponent<DungeonInfoSlotView>();

            var nextBtn = MakeArrow("NextButton", right: true);

            // 빈 상태 라벨 — 뷰포트 위에 겹침
            var emptyLbl = F.Text(scrollerRt, "EmptyLabel", "정보 없음", 20f, UguiTheme.TextTertiary,
                TextAlignmentOptions.Center);
            F.Stretch(emptyLbl.rectTransform);

            var view = card.gameObject.AddComponent<DungeonInfoCarouselView>();
            Wire(view,
                ("scrollRect", scrollRect),
                ("viewport", viewportRt),
                ("content", contentRt),
                ("layout", layout),
                ("slotTemplate", slotView),
                ("emptyLabel", emptyLbl),
                ("previousButton", prevBtn),
                ("nextButton", nextBtn));

            return view;
        }

        // ═══ 던전 클리어 팝업 ═══

        private static GameObject GenerateDungeonClearPopup()
        {
            var root = F.Root("Popup_DungeonClear");
            var view = root.gameObject.AddComponent<DungeonClearPopupView>();

            MakeDim(root);

            var panel = MakePanel(root, 820f, 440f);
            view.panel = panel.rectTransform;

            F.HeaderBanner(panel.transform, "던전 클리어", 520f, 94f, 36f);

            view.titleLabel = F.Text(
                panel.transform,
                "LblResult",
                "골드 1스테이지 클리어!",
                38f,
                UguiTheme.AccentGold,
                TextAlignmentOptions.Center,
                bold: true,
                wrap: true);
            F.Preferred(view.titleLabel, height: 72f);

            var guide = F.Text(
                panel.transform,
                "LblGuide",
                "다음 행동을 선택하세요.",
                24f,
                UguiTheme.TextSecondary,
                TextAlignmentOptions.Center);
            F.Preferred(guide, height: 40f);

            // 버튼 컬러 언어: 다크=나가기(취소) / 미드 우드=다시하기 / 청동 골드=다음(확정·전진)
            var buttons = MakeButtonRow(panel.transform);
            view.exitButton = MakeActionButton(
                buttons,
                "BtnExit",
                "나가기",
                UguiTheme.BtnCancel);
            view.retryButton = MakeActionButton(
                buttons,
                "BtnRetry",
                "다시하기",
                UguiTheme.RusticSurface);
            view.nextButton = MakeActionButton(
                buttons,
                "BtnNext",
                "다음 스테이지",
                UguiTheme.BtnConfirm);

            root.gameObject.SetActive(false);
            return PrefabGenUtil.SavePrefab(
                root.gameObject,
                $"{PrefabGenUtil.PrefabRoot}/Popups/Popup_DungeonClear.prefab");
        }

        // ═══ 환생 팝업 ═══

        private static GameObject GenerateReincarnationPopup()
        {
            var root = F.Root("Popup_Reincarnation");
            var view = root.gameObject.AddComponent<ReincarnationPopupView>();

            view.backdropButton = MakeDim(root);

            // 높이 640: 배너(100)+모래시계(84)+상태(54)+카드(170)+버튼(86)+간격(64)+패딩(56)=614
            var panel = MakePanel(root, 820f, 640f);
            view.panel = panel.rectTransform;

            F.HeaderBanner(panel.transform, "환생", 460f, 94f, 38f);

            // 금 모래시계 — 상단바 환생 버튼과 같은 상징 (시간을 되돌려 더 강하게)
            var hourglass = F.IconImage(panel.transform, "Hourglass", UguiGenAssets.IconHourglass, 84f, 84f);
            F.Preferred(hourglass, width: 84f, height: 84f);

            view.statusLabel = F.Text(
                panel.transform,
                "LblStatus",
                "환생 가능",
                34f,
                UguiTheme.SuccessGreen,
                TextAlignmentOptions.Center,
                bold: true);
            F.Preferred(view.statusLabel, height: 54f);

            var infoCard = F.Box(
                panel.transform,
                "InfoCard",
                F.CardDark,
                rounded: true);
            F.Preferred(infoCard, height: 170f);
            var frame = F.Frame(
                infoCard.transform,
                "Frame",
                new Color(
                    UguiTheme.Bronze.r,
                    UguiTheme.Bronze.g,
                    UguiTheme.Bronze.b,
                    0.7f));
            frame.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            view.infoLabel = F.Text(
                infoCard.transform,
                "LblInfo",
                "레벨: 15 → <color=#5DE66C>22 (+7)</color>\n" +
                "환생 횟수: 1 → <color=#5DE66C>2 (+1)</color>",
                28f,
                UguiTheme.TextPrimary,
                TextAlignmentOptions.Center,
                wrap: true);
            view.infoLabel.richText = true;
            F.Stretch(view.infoLabel.rectTransform);
            view.infoLabel.rectTransform.offsetMin = new Vector2(22f, 18f);
            view.infoLabel.rectTransform.offsetMax = new Vector2(-22f, -18f);

            // 버튼 컬러 언어: 진행 리셋을 동반하는 주요 액션 = 러스틱 크림슨(BtnSpend)
            var buttons = MakeButtonRow(panel.transform);
            view.cancelButton = MakeActionButton(
                buttons,
                "BtnCancel",
                "취소",
                UguiTheme.BtnCancel);
            view.confirmButton = MakeActionButton(
                buttons,
                "BtnConfirm",
                "환생하기",
                UguiTheme.BtnSpend);

            root.gameObject.SetActive(false);
            return PrefabGenUtil.SavePrefab(
                root.gameObject,
                $"{PrefabGenUtil.PrefabRoot}/Popups/Popup_Reincarnation.prefab");
        }

        private static Button MakeDim(RectTransform root)
        {
            var dim = F.Box(
                root,
                "Dim",
                UguiTheme.DimHeavy,
                rounded: false,
                raycast: true);
            F.Stretch(dim.rectTransform);
            var button = dim.gameObject.AddComponent<Button>();
            button.targetGraphic = dim;
            button.transition = Selectable.Transition.None;
            return button;
        }

        private static Image MakePanel(
            RectTransform root,
            float width,
            float height)
        {
            var panel = F.PixelPanel(
                root,
                "Panel",
                F.Catalog != null ? F.Catalog.kitWindow : null,
                F.FrameGold,
                24f,
                raycast: true,
                baseColor: F.PanelBaseDarker);
            F.AnchorCenter(panel.rectTransform, width, height);
            F.VLayout(
                panel.gameObject,
                16f,
                new RectOffset(38, 38, 26, 30),
                TextAnchor.UpperCenter);
            F.CornerBrackets(panel.transform);
            return panel;
        }

        private static RectTransform MakeButtonRow(Transform parent)
        {
            var row = F.Container(parent, "ButtonRow");
            F.HLayout(
                row.gameObject,
                14f,
                null,
                TextAnchor.MiddleCenter,
                expandWidth: true);
            F.Preferred(row.gameObject.AddComponent<LayoutElement>(), height: 86f);
            return row;
        }

        private static Button MakeActionButton(
            Transform parent,
            string name,
            string label,
            Color color)
        {
            var button = F.TextButton(
                parent,
                name,
                label,
                27f,
                color,
                out TextMeshProUGUI labelText,
                UguiTheme.TextPrimary,
                bold: true);
            F.Flexible(button, flexWidth: 1f);
            F.Preferred(button, height: 78f);
            F.Stretch(labelText.rectTransform);
            return button;
        }
    }
}
