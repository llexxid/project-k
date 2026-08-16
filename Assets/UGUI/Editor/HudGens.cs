using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KingdomIdle.MageTower;
using UnityEditor;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>파티 HUD / 마법탑 HUD / 데미지 텍스트 아이템 프리팹 생성기.</summary>
    internal static class HudGens
    {
        [MenuItem("KingdomIdle/UGUI/Generate Main Actions HUD", false, 5)]
        internal static void GenerateMainActionsOnly()
        {
            F.Init();
            var catalog = AssetDatabase.LoadAssetAtPath<UIViewCatalog>(
                PrefabGenUtil.CatalogPath);
            F.Catalog = catalog;
            GenerateMainActionsHud();
            if (catalog != null)
                CatalogGen.AssignPrefabs(catalog);
            AssetDatabase.Refresh();
        }

        internal static GameObject GenerateMainActionsHud()
        {
            var rootGo = new GameObject(
                "Hud_MainActions",
                typeof(RectTransform));
            rootGo.layer = 5;
            var root = (RectTransform)rootGo.transform;
            root.anchorMin = new Vector2(1f, 0.5f);
            root.anchorMax = new Vector2(1f, 0.5f);
            root.pivot = new Vector2(1f, 0.5f);
            root.anchoredPosition = new Vector2(-24f, 80f);
            root.sizeDelta = new Vector2(150f, 270f);

            var layout = rootGo.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var view = rootGo.AddComponent<MainActionsView>();
            view.dungeonButton = MakeMainActionButton(
                root,
                "BtnDungeon",
                "던전",
                "Assets/UGUI/UsingAssets/Dungeon_Chest01.png");
            view.reincarnationButton = MakeMainActionButton(
                root,
                "BtnReincarnation",
                "환생",
                "Assets/UGUI/UsingAssets/Dungeon_Gem01.png");

            return PrefabGenUtil.SavePrefab(
                rootGo,
                $"{PrefabGenUtil.PrefabRoot}/Huds/Hud_MainActions.prefab");
        }

        private static Button MakeMainActionButton(
            Transform parent,
            string name,
            string label,
            string iconPath)
        {
            var go = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button),
                typeof(LayoutElement));
            go.layer = 5;
            go.transform.SetParent(parent, false);

            var image = go.GetComponent<Image>();
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/UGUI/UsingAssets/Dungeon_Grey.png");
            image.type = Image.Type.Sliced;

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.colors = UguiTheme.MakeColorBlock();

            var element = go.GetComponent<LayoutElement>();
            element.preferredWidth = 142f;
            element.preferredHeight = 124f;

            var iconGo = new GameObject(
                "Icon",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            iconGo.layer = 5;
            iconGo.transform.SetParent(go.transform, false);
            var iconRect = (RectTransform)iconGo.transform;
            iconRect.anchorMin = new Vector2(0.5f, 1f);
            iconRect.anchorMax = new Vector2(0.5f, 1f);
            iconRect.pivot = new Vector2(0.5f, 1f);
            iconRect.anchoredPosition = new Vector2(0f, -14f);
            iconRect.sizeDelta = new Vector2(58f, 58f);
            var icon = iconGo.GetComponent<Image>();
            icon.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            var labelGo = new GameObject(
                "Label",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            labelGo.layer = 5;
            labelGo.transform.SetParent(go.transform, false);
            var labelRect = (RectTransform)labelGo.transform;
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 0f);
            labelRect.pivot = new Vector2(0.5f, 0f);
            labelRect.anchoredPosition = new Vector2(0f, 10f);
            labelRect.sizeDelta = new Vector2(-16f, 34f);
            var text = labelGo.GetComponent<TextMeshProUGUI>();
            text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/UGUI/UsingAssets/Dungeon_Galmuri11 SDF.asset");
            text.text = label;
            text.fontSize = 25f;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;

            return button;
        }
        // ═══ 파티 HUD (.party-*) ═══
        internal static GameObject GeneratePartyHud()
        {
            var rootGo = new GameObject("Hud_Party", typeof(RectTransform));
            rootGo.layer = 5;
            var rootRt = (RectTransform)rootGo.transform;

            // bottom-center 앵커/피벗 — 컨트롤러가 y만 조정
            rootRt.anchorMin = new Vector2(0.5f, 0f);
            rootRt.anchorMax = new Vector2(0.5f, 0f);
            rootRt.pivot = new Vector2(0.5f, 0f);
            rootRt.anchoredPosition = new Vector2(0f, UguiTheme.PartyHudBottom);

            var view = rootGo.AddComponent<PartyHudView>();
            view.rect = rootRt;

            F.HLayout(rootGo, 18f, null, TextAnchor.LowerCenter);
            var fitter = rootGo.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            for (int i = 0; i < 3; i++)
                view.members[i] = BuildMember(rootRt, i);

            return PrefabGenUtil.SavePrefab(rootGo, $"{PrefabGenUtil.PrefabRoot}/Huds/Hud_Party.prefab");
        }

        private static PartyHudView.Member BuildMember(RectTransform parent, int index)
        {
            var member = new PartyHudView.Member();

            // .party-member: 러스틱 웜 다크 블록 (15% 확대 — 초상화 78→90, 슬롯 40→46)
            var block = F.Box(parent, $"Member{index}", new Color(0.16f, 0.12f, 0.09f, 0.66f), rounded: true);
            F.HLayout(block.gameObject, 14f, new RectOffset(12, 12, 12, 12), TextAnchor.MiddleLeft);

            // 초상화 메달리온(버튼): 청동 링 + 어두운 원 + preserveAspect 스프라이트 (균일 초상화)
            var portrait = F.CircleBox(block.transform, "Portrait", UguiTheme.Bronze, raycast: true);
            F.Preferred(portrait, width: 90f, height: 90f);
            var portraitBtn = portrait.gameObject.AddComponent<Button>();
            portraitBtn.targetGraphic = portrait;
            portraitBtn.transition = Selectable.Transition.ColorTint;
            portraitBtn.colors = UguiTheme.MakeColorBlock();
            portrait.gameObject.AddComponent<PlayClickSfxOnClick>();
            member.portrait = portraitBtn;

            var disc = F.CircleBox(portrait.transform, "Disc", new Color(0.11f, 0.09f, 0.07f, 1f), raycast: false);
            F.AnchorCenter(disc.rectTransform, 78f, 78f);

            var portraitImg = F.Container(disc.transform, "Sprite");
            F.AnchorCenter(portraitImg, 70f, 70f);
            var img = portraitImg.gameObject.AddComponent<Image>();
            img.preserveAspect = true;
            img.raycastTarget = false;
            member.portraitImage = img;

            // 정보 열: HP바 + 스킬 행
            var infoCol = F.Container(block.transform, "InfoCol");
            F.VLayout(infoCol.gameObject, 6f, null, TextAnchor.MiddleLeft, expandWidth: false);

            var hpFill = F.HFillBar(infoCol, "HpBar", F.TrackDark, UguiTheme.HpGreen, out var hpTrack);
            F.Preferred(hpTrack, width: 218f, height: 25f);
            F.Frame(hpTrack.transform, "Frame", new Color(UguiTheme.Bronze.r, UguiTheme.Bronze.g, UguiTheme.Bronze.b, 0.7f))
                .gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            member.hpFill = hpFill;

            var skillRow = F.Container(infoCol, "SkillRow");
            F.HLayout(skillRow.gameObject, 6f, null, TextAnchor.MiddleLeft);
            F.Preferred(skillRow.gameObject.AddComponent<LayoutElement>(), height: 50f);

            for (int s = 0; s < 3; s++)
            {
                var slot = new PartyHudView.SkillSlot();

                // 어두운 슬롯 박스 + 청동 테두리 (정사각 스킬 슬롯, 러스틱)
                var slotBg = F.Box(skillRow, $"Skill{s}", new Color(0.16f, 0.12f, 0.09f, 0.95f), rounded: true);
                F.Frame(slotBg.transform, "Border", new Color(UguiTheme.Bronze.r, UguiTheme.Bronze.g, UguiTheme.Bronze.b, 0.5f))
                    .gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                F.Preferred(slotBg, width: 46f, height: 46f);
                slot.root = slotBg.gameObject;

                var mask = F.Box(slotBg.transform, "CdMask", new Color(0f, 0f, 0f, 0.55f), rounded: true);
                F.Stretch(mask.rectTransform);
                slot.cooldownMask = mask;

                var cd = F.Text(slotBg.transform, "CdText", "", 20f, Color.white, TextAlignmentOptions.Center, bold: true);
                F.Stretch(cd.rectTransform);
                slot.cooldownLabel = cd;

                var name = F.Text(slotBg.transform, "Name", "", 10f, Color.white, TextAlignmentOptions.Bottom);
                F.Stretch(name.rectTransform);
                slot.nameLabel = name;

                slotBg.gameObject.SetActive(false);
                mask.gameObject.SetActive(false);
                cd.gameObject.SetActive(false);

                member.skills[s] = slot;
            }

            return member;
        }

        // ═══ 마법탑 HUD (.mt-hud-*: 좌측 세로 열, top 300) ═══
        internal static GameObject GenerateMageTowerHud()
        {
            var rootGo = new GameObject("Hud_MageTower", typeof(RectTransform));
            rootGo.layer = 5;
            var rootRt = (RectTransform)rootGo.transform;
            rootRt.anchorMin = new Vector2(0f, 1f);
            rootRt.anchorMax = new Vector2(0f, 1f);
            rootRt.pivot = new Vector2(0f, 1f);
            rootRt.anchoredPosition = new Vector2(10f, -UguiTheme.MageTowerHudTop);
            rootRt.sizeDelta = new Vector2(UguiTheme.MageTowerHudWidth, 100f);

            var view = rootGo.AddComponent<MageTowerHudView>();

            F.VLayout(rootGo, 11f, new RectOffset(11, 11, 11, 11), TextAnchor.UpperCenter, expandWidth: false);
            var fitter = rootGo.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            float slotSize = UguiTheme.MageTowerSlotSize;

            // Auto 버튼 (러스틱)
            var autoBg = F.Box(rootRt, "BtnAuto", UguiTheme.RusticSurface, rounded: true, raycast: true);
            F.Preferred(autoBg, width: slotSize, height: slotSize);
            view.autoButton = F.ButtonOn(autoBg);
            view.autoButtonBg = autoBg;
            var autoLbl = F.Text(autoBg.transform, "Label", "Auto", 26f, new Color(1f, 1f, 1f, 0.30f),
                TextAlignmentOptions.Center, bold: true);
            F.Stretch(autoLbl.rectTransform);
            view.autoButtonLabel = autoLbl;

            // 스킬 슬롯 N개
            int slotCount = MageTowerManager.SlotCount;
            view.slots = new MageTowerHudView.Slot[slotCount];
            for (int i = 0; i < slotCount; i++)
            {
                var slot = new MageTowerHudView.Slot();

                // 어두운 슬롯 박스 + 청동 테두리 (정사각 마법탑 스킬 슬롯, 러스틱)
                var frame = F.Box(rootRt, $"Slot{i}", new Color(0.16f, 0.12f, 0.09f, 0.95f), rounded: true, raycast: true);
                F.Frame(frame.transform, "Border", new Color(UguiTheme.Bronze.r, UguiTheme.Bronze.g, UguiTheme.Bronze.b, 0.55f))
                    .gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                F.Preferred(frame, width: slotSize, height: slotSize);
                slot.frame = frame;
                var slotBtn = frame.gameObject.AddComponent<Button>();
                slotBtn.targetGraphic = frame;
                slotBtn.transition = Selectable.Transition.ColorTint;
                slotBtn.colors = UguiTheme.MakeColorBlock();
                frame.gameObject.AddComponent<PlayClickSfxOnClick>();
                slot.button = slotBtn;

                var icon = F.Container(frame.transform, "Icon");
                F.Stretch(icon);
                icon.offsetMin = new Vector2(5f, 5f);
                icon.offsetMax = new Vector2(-5f, -5f);
                var iconImg = icon.gameObject.AddComponent<Image>();
                iconImg.preserveAspect = true;
                iconImg.raycastTarget = false;
                slot.icon = iconImg;
                icon.gameObject.SetActive(false);

                var lbl = F.Text(frame.transform, "Label", "-", 28f, new Color(1f, 1f, 1f, 0.35f),
                    TextAlignmentOptions.Center);
                F.Stretch(lbl.rectTransform);
                slot.label = lbl;

                var mask = F.VFillMask(frame.transform, "CdMask", new Color(0f, 0f, 0f, 0.60f));
                slot.cooldownMask = mask;
                mask.gameObject.SetActive(false);

                var cdText = F.Text(frame.transform, "CdText", "", 34f, Color.white, TextAlignmentOptions.Center, bold: true);
                F.Stretch(cdText.rectTransform);
                slot.cooldownText = cdText;
                cdText.gameObject.SetActive(false);

                view.slots[i] = slot;
            }

            // 마탑 진입은 좌하단 환경 오브젝트(Hud_MageTowerEnv)가 담당한다 — 열의 버튼은 제거됨

            return PrefabGenUtil.SavePrefab(rootGo, $"{PrefabGenUtil.PrefabRoot}/Huds/Hud_MageTower.prefab");
        }

        // ═══ 마탑 환경 오브젝트 — 좌하단, 하단바 뒤에서 솟아오르는 인터랙티브 마탑 ═══
        internal static GameObject GenerateMageTowerEnv()
        {
            var rootGo = new GameObject("Hud_MageTowerEnv", typeof(RectTransform));
            rootGo.layer = 5;
            var rootRt = (RectTransform)rootGo.transform;

            // 좌하단 앵커 — 탑의 발치가 화면 바닥 아래(y=-24)에 묻히고 몸통 절반쯤이
            // 화면 왼쪽 가장자리 밖에 걸쳐 "삐죽" 솟는다. 파티 HUD 첫 멤버 블록(x≈15~)과의
            // 시각적 겹침을 줄이기 위해 중심을 x=40 까지 밀었다 (블록 배경이 반투명이라 겹치면 비쳐 보인다).
            rootRt.anchorMin = new Vector2(0f, 0f);
            rootRt.anchorMax = new Vector2(0f, 0f);
            rootRt.pivot = new Vector2(0.5f, 0f);
            rootRt.anchoredPosition = new Vector2(40f, -24f);

            // 호흡/흔들림/점등이 매 프레임 트랜스폼·알파를 만지므로 자체 Canvas 로 리빌드를 격리한다.
            // 탑에 Button 이 있으므로 GraphicRaycaster 필수 (중첩 캔버스는 부모 레이캐스터에 안 잡힌다).
            rootGo.AddComponent<Canvas>();
            rootGo.AddComponent<GraphicRaycaster>();

            var view = rootGo.AddComponent<MageTowerEnvView>();
            view.root = rootRt;

            var towerSprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Generated/ComfyUI/UI/MageTowerEnv.png");
            var litSprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Generated/ComfyUI/UI/MageTowerEnv_Lit.png");
            if (towerSprite == null)
                Debug.LogWarning("[UguiGen] MageTowerEnv.png 를 찾지 못했습니다 — 탑 이미지 없이 생성됩니다.");

            // 스프라이트 원본 비율 유지 (164x388 → 표시 폭 208)
            float dispW = 190f;
            float dispH = towerSprite != null
                ? dispW * towerSprite.rect.height / towerSprite.rect.width
                : 492f;
            rootRt.sizeDelta = new Vector2(dispW, dispH);

            // 바닥 접합부 보라 광원 — 탑이 바에 '심어진' 느낌을 주는 장식
            var glow = F.Container(rootRt, "BaseGlow");
            var glowImg = glow.gameObject.AddComponent<Image>();
            glowImg.sprite = F.CircleSoft;
            glowImg.color = new Color(0.55f, 0.30f, 0.85f, 0.20f);
            glowImg.raycastTarget = false;
            glow.anchorMin = new Vector2(0.5f, 0f);
            glow.anchorMax = new Vector2(0.5f, 0f);
            glow.pivot = new Vector2(0.5f, 0.5f);
            glow.anchoredPosition = new Vector2(0f, 190f); // 바 상단 모서리 부근
            glow.sizeDelta = new Vector2(dispW * 1.5f, 120f);
            view.baseGlow = glowImg;

            // 기본 마탑 (버튼 타깃)
            var towerGo = new GameObject("Tower", typeof(RectTransform));
            towerGo.layer = 5;
            towerGo.transform.SetParent(rootRt, false);
            var towerRt = (RectTransform)towerGo.transform;
            F.Stretch(towerRt);
            var towerImg = towerGo.AddComponent<Image>();
            if (towerSprite != null) towerImg.sprite = towerSprite;
            towerImg.preserveAspect = true;
            towerImg.raycastTarget = true;
            view.towerImage = towerImg;

            var btn = towerGo.AddComponent<Button>();
            btn.targetGraphic = towerImg;
            btn.transition = Selectable.Transition.ColorTint;
            btn.colors = UguiTheme.MakeColorBlock();
            towerGo.AddComponent<PlayClickSfxOnClick>();
            view.button = btn;

            // 점등 오버레이 — CanvasGroup 알파로 크로스페이드 (레이캐스트 차단 금지)
            var litGo = new GameObject("Lit", typeof(RectTransform));
            litGo.layer = 5;
            litGo.transform.SetParent(rootRt, false);
            var litRt = (RectTransform)litGo.transform;
            F.Stretch(litRt);
            var litImg = litGo.AddComponent<Image>();
            if (litSprite != null) litImg.sprite = litSprite;
            litImg.preserveAspect = true;
            litImg.raycastTarget = false;
            var litGroup = litGo.AddComponent<CanvasGroup>();
            litGroup.alpha = 0f;
            litGroup.interactable = false;
            litGroup.blocksRaycasts = false;
            view.litImage = litImg;
            view.litGroup = litGroup;

            return PrefabGenUtil.SavePrefab(rootGo, $"{PrefabGenUtil.PrefabRoot}/Huds/Hud_MageTowerEnv.prefab");
        }

        // ═══ 신성 스킬(궁극기) HUD — 하단 중앙 원형 대형 버튼 1개 ═══

        /// <summary>단일 대상 재생성 — 손댄 다른 프리팹을 건드리지 않고 궁극기 HUD만 다시 만든다.</summary>
        [MenuItem("KingdomIdle/UGUI/Generate Divine Skill HUD", false, 6)]
        internal static void GenerateDivineSkillHudOnly()
        {
            F.Init();
            var catalog = AssetDatabase.LoadAssetAtPath<UIViewCatalog>(
                PrefabGenUtil.CatalogPath);
            F.Catalog = catalog;
            GenerateDivineSkillHud();
            if (catalog != null)
                CatalogGen.AssignPrefabs(catalog);
            AssetDatabase.Refresh();
        }

        internal static GameObject GenerateDivineSkillHud()
        {
            var rootGo = new GameObject("Hud_DivineSkill", typeof(RectTransform));
            rootGo.layer = 5;
            var rootRt = (RectTransform)rootGo.transform;

            // 하단 중앙 앵커 — 가이드 퀘스트 창(임시 숨김)이 떠 있던 자리, 파티 HUD 바로 위.
            // 버튼 하단 y = PartyHudBottom(202) + PartyHudHeight(172, 15% 확대 반영) + 여백(24) = 398.
            // 레이아웃 그룹 없이 고정 크기 1칸이라 ContentSizeFitter도 쓰지 않는다.
            rootRt.anchorMin = new Vector2(0.5f, 0f);
            rootRt.anchorMax = new Vector2(0.5f, 0f);
            rootRt.pivot = new Vector2(0.5f, 0f);
            rootRt.anchoredPosition = new Vector2(0f, UguiTheme.DivineHudBottom);
            rootRt.sizeDelta = new Vector2(UguiTheme.DivineHudDiameter, UguiTheme.DivineHudDiameter);

            // 이 HUD 는 쿨다운 동안 0.1초마다 다시 그린다 — 자체 Canvas 로 리빌드를 격리해
            // 루트 캔버스(화면 전체) 리빌드를 막는다. 버튼이 있으므로 GraphicRaycaster 필수
            // (중첩 캔버스의 그래픽은 부모 캔버스의 레이캐스터에 잡히지 않는다).
            rootGo.AddComponent<Canvas>();
            rootGo.AddComponent<GraphicRaycaster>();

            var view = rootGo.AddComponent<DivineSkillHudView>();
            view.pulse = rootGo.AddComponent<UIPulseGroup>();

            float d = UguiTheme.DivineHudDiameter;

            // ① 준비 완료 후광 — 버튼보다 큰 소프트 원(방사형 페이드), 맨 뒤에서 맥동+호흡한다
            float pad = UguiTheme.DivineHudGlowPad;
            var glowRt = F.Container(rootRt, "ReadyGlow");
            F.Stretch(glowRt);
            glowRt.offsetMin = new Vector2(-pad, -pad);
            glowRt.offsetMax = new Vector2(pad, pad);
            var glow = glowRt.gameObject.AddComponent<Image>();
            glow.sprite = F.CircleSoft;
            glow.color = new Color(1f, 0.86f, 0.42f, 0.85f);   // 골드 — 저알파 흰색은 Linear에서 터진다
            glow.raycastTarget = false;
            view.readyGlow = glow;
            view.readyGlowGroup = glow.gameObject.AddComponent<CanvasGroup>();
            glow.gameObject.SetActive(false);

            // ② 버튼 본체 — 파티 초상화와 같은 원형 메달리온 언어: 청동 링 + 어두운 디스크.
            //    F.ButtonOn은 사각 LL 버튼 스킨(kitBtnGrey)으로 스프라이트를 덮어써 원형이 깨지므로
            //    파티 초상화(BuildMember)와 동일하게 수동 Button 관례를 쓴다.
            var frame = F.CircleBox(rootRt, "Btn", UguiTheme.Bronze, raycast: true);
            F.Stretch(frame.rectTransform);
            view.frame = frame;
            var btn = frame.gameObject.AddComponent<Button>();
            btn.targetGraphic = frame;
            btn.transition = Selectable.Transition.ColorTint;
            btn.colors = UguiTheme.MakeColorBlock();
            frame.gameObject.AddComponent<PlayClickSfxOnClick>();
            view.button = btn;
            // 탭/길게 판정 — 시전·자동 토글 입력은 Button.onClick이 아니라 이 컴포넌트가 가진다
            view.longPress = frame.gameObject.AddComponent<UILongPressButton>();

            // 등급 색 얇은 링 — 청동 링(176) 안쪽, 디스크(152) 바깥쪽 6px 밴드.
            // 컨트롤러가 장착 카드 등급색으로 칠한다 (미장착 = DisabledGrey).
            var gradeRing = F.CircleBox(frame.transform, "GradeRing", UguiTheme.DisabledGrey, raycast: false);
            F.AnchorCenter(gradeRing.rectTransform, d - 12f, d - 12f);
            view.gradeBorder = gradeRing;

            // ③ 어두운 디스크 = 원형 크롭 마스크 (마스크 1개로 해결 — showMaskGraphic으로 배경 겸용)
            var discRt = F.Container(frame.transform, "Disc");
            F.AnchorCenter(discRt, d - 24f, d - 24f);
            var disc = discRt.gameObject.AddComponent<Image>();
            disc.sprite = F.Circle;   // 절차 생성 원 — 마스크 스텐실 알파가 예측 가능해야 한다
            disc.color = new Color(0.11f, 0.09f, 0.07f, 1f);
            disc.raycastTarget = false;
            var mask = discRt.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = true;
            view.disc = disc;

            // 아이콘 — 디스크 마스크로 원형 크롭. 스프라이트가 없으면 컨트롤러가 꺼서 흰 박스를 막는다
            var iconRt = F.Container(discRt, "Icon");
            F.Stretch(iconRt);
            var iconImg = iconRt.gameObject.AddComponent<Image>();
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;
            view.icon = iconImg;
            iconRt.gameObject.SetActive(false);

            // 미장착 표기 (아이콘이 꺼져 있을 때만 컨트롤러가 켠다)
            var empty = F.Text(discRt, "EmptyLabel", "미장착", UguiTheme.FontDivineEmpty,
                UguiTheme.TextTertiary, TextAlignmentOptions.Center, bold: true, wrap: true);
            F.Stretch(empty.rectTransform);
            view.emptyLabel = empty;

            // 방사형 쿨다운 — Image.Type.Filled는 sprite가 null이면 fillAmount를 무시한다.
            var cdFillRt = F.Container(discRt, "CdFill");
            F.Stretch(cdFillRt);
            var cdFill = cdFillRt.gameObject.AddComponent<Image>();
            cdFill.sprite = F.Circle;
            cdFill.color = new Color(0f, 0f, 0f, 0.65f);
            cdFill.type = Image.Type.Filled;
            cdFill.fillMethod = Image.FillMethod.Radial360;
            cdFill.fillOrigin = (int)Image.Origin360.Top;
            cdFill.fillClockwise = true;
            cdFill.fillAmount = 0f;
            cdFill.raycastTarget = false;
            view.cooldownFill = cdFill;
            cdFillRt.gameObject.SetActive(false);

            var cdText = F.Text(discRt, "CdText", "", UguiTheme.FontDivineCooldown, Color.white,
                TextAlignmentOptions.Center, bold: true);
            F.Stretch(cdText.rectTransform);
            view.cooldownText = cdText;
            cdText.gameObject.SetActive(false);

            // ④ AUTO 회전 링 — 버튼 밖 반지름 100(=88+12) 궤도에 골드 틱 4개, 컨트롤러가 30°/s 회전.
            //    버튼(frame)의 눌림 스케일에 휩쓸리지 않도록 형제로 둔다.
            var autoRing = F.Container(rootRt, "AutoRing");
            F.AnchorCenter(autoRing, 0f, 0f);
            float tickR = d * 0.5f + UguiTheme.DivineHudAutoRingPad;
            for (int i = 0; i < 4; i++)
            {
                // 24×8 골드 바 — 9-slice 라운드는 높이 8px에서 코너가 뭉개지므로 플랫 사각
                var tick = F.Box(autoRing, $"Tick{i}", UguiTheme.AccentGoldStrong, rounded: false);
                var tickRt = tick.rectTransform;
                F.AnchorCenter(tickRt, 24f, 8f);
                float ang = 90f * i;
                float rad = ang * Mathf.Deg2Rad;
                tickRt.anchoredPosition = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad)) * tickR;
                tickRt.localRotation = Quaternion.Euler(0f, 0f, -ang);   // 궤도 접선 방향
                tick.raycastTarget = false;
            }
            view.autoRing = autoRing;
            autoRing.gameObject.SetActive(false);

            // AUTO 필 — 링 하단(버튼 아래 가장자리)에 붙는 라벨. 회전 링과 달리 고정.
            var pill = F.Box(rootRt, "AutoPill", UguiTheme.AccentGoldStrong, rounded: true);
            var pillRt = pill.rectTransform;
            pillRt.anchorMin = new Vector2(0.5f, 0f);
            pillRt.anchorMax = new Vector2(0.5f, 0f);
            pillRt.pivot = new Vector2(0.5f, 0.5f);
            pillRt.anchoredPosition = new Vector2(0f, -UguiTheme.DivineHudAutoRingPad);
            pillRt.sizeDelta = new Vector2(84f, 30f);
            pill.raycastTarget = false;
            var pillLbl = F.Text(pill.transform, "Label", "AUTO", 16f,
                new Color(0.16f, 0.11f, 0.05f, 1f), TextAlignmentOptions.Center, bold: true);
            F.Stretch(pillLbl.rectTransform);
            view.autoPill = pill.gameObject;
            pill.gameObject.SetActive(false);

            // ⑤ 시전 플래시 — 발동 시 1→1.6 확장하며 사라지는 골드 블룸. 캐시 1개 재사용(Instantiate 없음).
            var flashRt = F.Container(rootRt, "CastFlash");
            F.AnchorCenter(flashRt, d, d);
            var flash = flashRt.gameObject.AddComponent<Image>();
            flash.sprite = F.CircleSoft;
            flash.color = new Color(1f, 0.86f, 0.39f, 0.85f);
            flash.raycastTarget = false;
            view.castFlash = flash;
            flashRt.gameObject.SetActive(false);

            return PrefabGenUtil.SavePrefab(rootGo, $"{PrefabGenUtil.PrefabRoot}/Huds/Hud_DivineSkill.prefab");
        }

        // ═══ 데미지 텍스트 아이템 (.damage-text: 30px bold 빨강 + 아웃라인) ═══
        internal static GameObject GenerateDamageTextItem(Material outlineMaterial)
        {
            var go = new GameObject("Item_DamageText", typeof(RectTransform));
            go.layer = 5;
            var text = go.AddComponent<TextMeshProUGUI>();
            if (F.Font != null) text.font = F.Font;
            if (outlineMaterial != null) text.fontSharedMaterial = outlineMaterial;
            text.text = "0";
            text.fontSize = UguiTheme.FontDamageText;
            text.fontStyle = FontStyles.Bold;
            text.color = UguiTheme.WarnRed;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            ((RectTransform)go.transform).sizeDelta = new Vector2(240f, 44f);

            return PrefabGenUtil.SavePrefab(go, $"{PrefabGenUtil.PrefabRoot}/Items/Item_DamageText.prefab");
        }
    }
}
