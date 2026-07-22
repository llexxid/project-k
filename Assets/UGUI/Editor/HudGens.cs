using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KingdomIdle.MageTower;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>파티 HUD / 마법탑 HUD / 데미지 텍스트 아이템 프리팹 생성기.</summary>
    internal static class HudGens
    {
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

            // .party-member: row gap 12 padding 10 bg black@30% radius 18
            var block = F.Box(parent, $"Member{index}", new Color(0f, 0f, 0f, 0.30f), rounded: true);
            F.HLayout(block.gameObject, 12f, new RectOffset(10, 10, 10, 10), TextAnchor.MiddleLeft);

            // 초상화 버튼 (74×74 원형 — 픽셀 원형 프레임 유지, 버튼 스킨 미적용)
            var portrait = F.CircleBox(block.transform, "Portrait", new Color(1f, 1f, 1f, 0.25f), raycast: true);
            F.Preferred(portrait, width: 74f, height: 74f);
            var portraitBtn = portrait.gameObject.AddComponent<Button>();
            portraitBtn.targetGraphic = portrait;
            portraitBtn.transition = Selectable.Transition.ColorTint;
            portraitBtn.colors = UguiTheme.MakeColorBlock();
            portrait.gameObject.AddComponent<PlayClickSfxOnClick>();
            member.portrait = portraitBtn;

            var portraitImg = F.Container(portrait.transform, "Sprite");
            F.Stretch(portraitImg);
            portraitImg.offsetMin = new Vector2(4f, 4f);
            portraitImg.offsetMax = new Vector2(-4f, -4f);
            var img = portraitImg.gameObject.AddComponent<Image>();
            img.preserveAspect = true;
            img.raycastTarget = false;
            member.portraitImage = img;

            // 정보 열: HP바 + 스킬 행
            var infoCol = F.Container(block.transform, "InfoCol");
            F.VLayout(infoCol.gameObject, 6f, null, TextAnchor.MiddleLeft, expandWidth: false);

            var hpFill = F.HFillBar(infoCol, "HpBar", new Color(1f, 1f, 1f, 0.12f), UguiTheme.HpGreen, out var hpTrack);
            F.Preferred(hpTrack, width: 180f, height: 16f);
            member.hpFill = hpFill;

            var skillRow = F.Container(infoCol, "SkillRow");
            F.HLayout(skillRow.gameObject, 6f, null, TextAnchor.MiddleLeft);
            F.Preferred(skillRow.gameObject.AddComponent<LayoutElement>(), height: 44f);

            for (int s = 0; s < 3; s++)
            {
                var slot = new PartyHudView.SkillSlot();

                // 어두운 슬롯 박스 + 은은한 테두리 (다이아몬드 SkillSlot 대신 정사각 슬롯)
                var slotBg = F.Box(skillRow, $"Skill{s}", new Color(0.1f, 0.11f, 0.16f, 0.9f), rounded: true);
                F.Frame(slotBg.transform, "Border", new Color(1f, 1f, 1f, 0.22f))
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

            // Auto 버튼
            var autoBg = F.Box(rootRt, "BtnAuto", UguiTheme.SurfaceFaint, rounded: true, raycast: true);
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

                // 어두운 슬롯 박스 + 골드 테두리 (정사각 마법탑 스킬 슬롯)
                var frame = F.Box(rootRt, $"Slot{i}", new Color(0.1f, 0.11f, 0.16f, 0.92f), rounded: true, raycast: true);
                F.Frame(frame.transform, "Border", new Color(1f, 220f / 255f, 130f / 255f, 0.35f))
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

            // 마탑 버튼
            var towerBg = F.Box(rootRt, "BtnTower", new Color(100f / 255f, 60f / 255f, 180f / 255f, 0.50f),
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
