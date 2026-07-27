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

            F.HLayout(rootGo, 16f, null, TextAnchor.LowerCenter);
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

            // .party-member: 러스틱 웜 다크 블록
            var block = F.Box(parent, $"Member{index}", new Color(0.16f, 0.12f, 0.09f, 0.66f), rounded: true);
            F.HLayout(block.gameObject, 12f, new RectOffset(10, 10, 10, 10), TextAnchor.MiddleLeft);

            // 초상화 메달리온(버튼): 청동 링 + 어두운 원 + preserveAspect 스프라이트 (균일 초상화)
            var portrait = F.CircleBox(block.transform, "Portrait", UguiTheme.Bronze, raycast: true);
            F.Preferred(portrait, width: 78f, height: 78f);
            var portraitBtn = portrait.gameObject.AddComponent<Button>();
            portraitBtn.targetGraphic = portrait;
            portraitBtn.transition = Selectable.Transition.ColorTint;
            portraitBtn.colors = UguiTheme.MakeColorBlock();
            portrait.gameObject.AddComponent<PlayClickSfxOnClick>();
            member.portrait = portraitBtn;

            var disc = F.CircleBox(portrait.transform, "Disc", new Color(0.11f, 0.09f, 0.07f, 1f), raycast: false);
            F.AnchorCenter(disc.rectTransform, 68f, 68f);

            var portraitImg = F.Container(disc.transform, "Sprite");
            F.AnchorCenter(portraitImg, 60f, 60f);
            var img = portraitImg.gameObject.AddComponent<Image>();
            img.preserveAspect = true;
            img.raycastTarget = false;
            member.portraitImage = img;

            // 정보 열: HP바 + 스킬 행
            var infoCol = F.Container(block.transform, "InfoCol");
            F.VLayout(infoCol.gameObject, 6f, null, TextAnchor.MiddleLeft, expandWidth: false);

            var hpFill = F.HFillBar(infoCol, "HpBar", F.TrackDark, UguiTheme.HpGreen, out var hpTrack);
            F.Preferred(hpTrack, width: 190f, height: 22f);
            F.Frame(hpTrack.transform, "Frame", new Color(UguiTheme.Bronze.r, UguiTheme.Bronze.g, UguiTheme.Bronze.b, 0.7f))
                .gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            member.hpFill = hpFill;

            var skillRow = F.Container(infoCol, "SkillRow");
            F.HLayout(skillRow.gameObject, 6f, null, TextAnchor.MiddleLeft);
            F.Preferred(skillRow.gameObject.AddComponent<LayoutElement>(), height: 44f);

            for (int s = 0; s < 3; s++)
            {
                var slot = new PartyHudView.SkillSlot();

                // 어두운 슬롯 박스 + 청동 테두리 (정사각 스킬 슬롯, 러스틱)
                var slotBg = F.Box(skillRow, $"Skill{s}", new Color(0.16f, 0.12f, 0.09f, 0.95f), rounded: true);
                F.Frame(slotBg.transform, "Border", new Color(UguiTheme.Bronze.r, UguiTheme.Bronze.g, UguiTheme.Bronze.b, 0.5f))
                    .gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                F.Preferred(slotBg, width: 40f, height: 40f);
                slot.root = slotBg.gameObject;

                var mask = F.Box(slotBg.transform, "CdMask", new Color(0f, 0f, 0f, 0.55f), rounded: true);
                F.Stretch(mask.rectTransform);
                slot.cooldownMask = mask;

                var cd = F.Text(slotBg.transform, "CdText", "", 18f, Color.white, TextAlignmentOptions.Center, bold: true);
                F.Stretch(cd.rectTransform);
                slot.cooldownLabel = cd;

                var name = F.Text(slotBg.transform, "Name", "", 8f, Color.white, TextAlignmentOptions.Bottom);
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

            // 마탑 버튼 (마법 보라 — 리치하게)
            var towerBg = F.Box(rootRt, "BtnTower", new Color(0.42f, 0.28f, 0.62f, 0.95f),
                rounded: true, raycast: true);
            F.Preferred(towerBg, width: slotSize, height: slotSize);
            view.towerButton = F.ButtonOn(towerBg);
            var towerLbl = F.Text(towerBg.transform, "Label", "마탑", 31f, UguiTheme.TextPrimary,
                TextAlignmentOptions.Center, bold: true);
            F.Stretch(towerLbl.rectTransform);

            return PrefabGenUtil.SavePrefab(rootGo, $"{PrefabGenUtil.PrefabRoot}/Huds/Hud_MageTower.prefab");
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
