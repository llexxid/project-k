using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>왕국군의 종합·스킬·전직 표시만 후처리한다. 기존 View 참조와 게임 정책은 변경하지 않는다.</summary>
    public static class BronzeArmyPreparation
    {
        private const string Root = "Assets/UGUI/Prefabs/";
        private const float JobCardHeight = 544;
        private static readonly Color PortraitSurface = new Color32(220, 205, 174, 255);
        private static readonly Color ChipSurface = new Color32(222, 208, 177, 255);
        private static readonly Color BlueSurface = new Color32(31, 49, 58, 255);
        private static Sprite Rounded => AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UGUI/Sprites/RoundedRect.png");

        [MenuItem("KingdomIdle/UGUI/청동 원정대/왕국군 표시 적용")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("왕국군 프리팹을 갱신하려면 플레이 모드를 종료해 주세요.");
            Edit("Panels/Panel_KACharacterSheet.prefab", Character);
            Edit("Items/Item_SkillRow.prefab", Skill);
            Edit("Items/Item_StatTerm.prefab", Term);
            Edit("Items/Item_StatCompareRow.prefab", Compare);
            Edit("Items/Item_JobCard.prefab", JobCard);
            Edit("Panels/Panel_KAJobDetail.prefab", JobDetail);
            Edit("Panels/Panel_KAJobChange.prefab", JobChange);
            Debug.Log("[Bronze Army] 종합·스킬·전직 표시 적용 완료; 기존 직렬화 참조 보존");
        }

        /// <summary>계정·전직 실행 없이 실제 프리팹의 참조와 긴 설명 표시 공간을 검사한다.</summary>
        public static object Validate()
        {
            if (Application.isPlaying) throw new InvalidOperationException("왕국군 표시 검사는 EditMode에서 실행해 주세요.");
            var checks = new List<string>();
            void Check(bool value, string message)
            {
                if (!value) throw new InvalidOperationException("Bronze Army: " + message);
                checks.Add(message);
            }
            Inspect("Panels/Panel_KACharacterSheet.prefab", root =>
            {
                var view = root.GetComponent<KACharacterSheetView>();
                Check(view.statsButton.GetComponent<Image>().color == UguiTheme.InfoSurface && view.jobLabel.color == UguiTheme.InfoInk,
                    "Summary has a light surface with a dark job name");
                Check(view.portraitInner != null && view.hpFill != null && view.statsButton != null && view.detailRoot != null,
                    "Portrait, actual HP fill and detail toggle references remain connected");
                Check(view.atkValueLabel.color == UguiTheme.InfoInk && view.atkValueLabel.transform.parent.Find("MetricName") != null,
                    "Attack chip has a readable value and explicit metric name");
                Check(view.hpValueLabel.fontSharedMaterial == view.hpValueLabel.font.material &&
                    view.hpValueLabel.color == UguiTheme.InfoInk && view.hpValueLabel.fontSize == 36,
                    "HP numbers use solid dark text with the normal font material at 36px");
                foreach (var terms in new[] { view.atkEqRow, view.hpEqRow })
                    Check(terms.parent.GetComponent<ScrollRect>()?.content == terms && terms.parent.GetComponent<RectMask2D>() != null,
                        terms.name + " keeps the original term target inside a horizontal equation viewport");
            });
            Inspect("Items/Item_SkillRow.prefab", root =>
            {
                var view = root.GetComponent<SkillRowView>();
                Check(Layout(view.typeBadge.transform).minWidth >= 160 && view.typeLabel.fontSize == 36 && view.nameLabel.fontSize == 42,
                    "Active/passive badge and skill name retain their intended readable sizes");
                Check(Layout(view.detailLabel.transform).preferredHeight < 0 && Layout(root.transform).preferredHeight < 0 &&
                    view.detailLabel.textWrappingMode == TextWrappingModes.Normal && view.detailLabel.overflowMode == TextOverflowModes.Overflow,
                    "Skill detail and row derive height from wrapped text instead of a fixed rectangle");
                var rect = (RectTransform)root.transform;
                foreach (float width in new[] { 900f, 1000f })
                {
                    rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
                    foreach (var skill in SkillSystem.GetJobSkillInfo("Elite_Knight").Concat(SkillSystem.GetJobSkillInfo("Elite_Mage")))
                    {
                        view.Set(skill.Name, skill.Description, skill.IsPassive);
                        LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
                        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, LayoutUtility.GetPreferredHeight(rect));
                        LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
                        float required = view.detailLabel.GetPreferredValues(view.detailLabel.text, view.detailLabel.rectTransform.rect.width, float.PositiveInfinity).y;
                        Check(view.detailLabel.rectTransform.rect.height + 1 >= required,
                            width + "px " + skill.Name + " keeps its complete description within the row");
                        Check(view.typeLabel.GetPreferredValues(view.typeLabel.text).x <= view.typeLabel.rectTransform.rect.width + 1,
                            width + "px " + view.typeLabel.text + " badge does not truncate");
                    }
                }
            });
            Inspect("Items/Item_JobCard.prefab", root =>
            {
                var view = root.GetComponent<JobCardView>();
                Check(root.transform.Find("ArmyInfoSurface") != null && view.nameLabel.color == UguiTheme.InfoInk &&
                    view.background != null && view.stateFrame != null && view.Button != null,
                    "Job card separates information from condition/status surfaces while retaining its state references");
                Check(view.fragLabel.rectTransform.rect.height >= 88 && view.prereqLabel.rectTransform.rect.height >= 88,
                    "Job requirements have enough height for two readable lines");
                Check(view.badge.rectTransform.rect.height >= 56 && view.badge.GetPreferredValues("전직가능").y <= view.badge.rectTransform.rect.height,
                    "Job status text fits inside its pill instead of becoming an empty decoration");
            });
            Inspect("Panels/Panel_KAJobChange.prefab", root =>
            {
                var view = root.GetComponent<KAJobChangeView>();
                foreach (var grid in new[] { view.basicGrid, view.eliteGrid })
                {
                    var responsive = grid.GetComponent<ResponsiveGrid>();
                    var settings = new SerializedObject(responsive);
                    Check(responsive != null && responsive.enabled && !grid.GetComponent<PromotionTreeGrid>().enabled &&
                        settings.FindProperty("maximumColumns").intValue == 3 && settings.FindProperty("minimumCellWidth").floatValue == 1 &&
                        settings.FindProperty("cellHeight").floatValue == JobCardHeight,
                        grid.name + " preserves three branches and a complete job-card height");
                    var height = grid.GetComponent<LayoutElement>();
                    Check(height != null && height.minHeight >= JobCardHeight && height.preferredHeight >= JobCardHeight,
                        grid.name + " reserves its full row height before cards are populated");
                }
                view.placeholder.gameObject.SetActive(false); view.contentGroup.gameObject.SetActive(true);
                var rect = (RectTransform)root.transform;
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 1000);
                LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
                Check(LayoutUtility.GetPreferredHeight(view.contentGroup) >= JobCardHeight * 2 &&
                    LayoutUtility.GetPreferredHeight(rect) >= JobCardHeight * 2,
                    "Both job tiers contribute their full height to the outer scrolling content even before card creation");
            });
            Inspect("Panels/Panel_KAJobDetail.prefab", root =>
            {
                var view = root.GetComponent<KAJobDetailView>();
                Check(view.jobNameLabel.color == UguiTheme.InfoInk && view.stateBadge.transform.parent.name == "ArmyStateChip" &&
                    !Layout(view.stateBadge.transform.parent).ignoreLayout,
                    "Job-detail status occupies its own layout slot next to the readable name");
                Check(view.image != null && view.compareTable != null && view.skillList != null && view.changeRow != null && view.backButton != null,
                    "Job-detail portrait, comparisons, skills, change action and return references remain connected");
            });
            return new { passed = checks.Count, checks };
        }

        private static void Inspect(string relative, Action<GameObject> inspect)
        {
            var root = PrefabUtility.LoadPrefabContents(Root + relative);
            try { inspect(root); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void Edit(string relative, Action<GameObject> apply)
        {
            string path = Root + relative;
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var before = References(root);
                apply(root);
                var after = References(root);
                foreach (var entry in before)
                    if (!after.TryGetValue(entry.Key, out var value) || value != entry.Value)
                        throw new InvalidOperationException("기존 왕국군 View 참조가 변경되었습니다: " + relative + "/" + entry.Key);
                PrefabUtility.SaveAsPrefabAsset(root, path, out bool saved);
                if (!saved) throw new InvalidOperationException("왕국군 프리팹 저장 실패: " + path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static Dictionary<string, Object> References(GameObject root)
        {
            var result = new Dictionary<string, Object>();
            foreach (var view in root.GetComponents<MonoBehaviour>())
            {
                if (!(view is KACharacterSheetView || view is SkillRowView || view is StatTermView ||
                    view is StatCompareRowView || view is JobCardView || view is KAJobDetailView || view is KAJobChangeView)) continue;
                var property = new SerializedObject(view).GetIterator();
                while (property.NextVisible(true))
                    if (property.propertyType == SerializedPropertyType.ObjectReference)
                        result[view.GetType().Name + "/" + property.propertyPath] = property.objectReferenceValue;
            }
            return result;
        }

        private static void Character(GameObject root)
        {
            var view = root.GetComponent<KACharacterSheetView>();
            var block = view.statsButton.transform;
            Flat(block.GetComponent<Image>(), UguiTheme.InfoSurface);
            Height(block, 320);
            var row = block.GetComponent<HorizontalLayoutGroup>();
            row.padding = new RectOffset(24, 24, 24, 24); row.spacing = 24;
            row.childAlignment = TextAnchor.MiddleLeft;
            // ColorTint의 흰색 배수는 유지하되 누를 때도 밝은 정보면의 대비를 보존한다.
            view.statsButton.colors = UguiTheme.MakeColorBlock();
            var frame = block.Find("Frame")?.GetComponent<Image>();
            if (frame != null) frame.color = UguiTheme.Bronze;
            var ring = block.Find("PortraitRing");
            if (ring != null)
            {
                Size(ring, 176, 176);
                ring.GetComponent<Image>().color = UguiTheme.Bronze;
                var socket = ring.Find("Socket")?.GetComponent<Image>();
                if (socket != null) socket.color = PortraitSurface;
            }
            Type(view.jobLabel, 48, UguiTheme.InfoInk, 72);
            view.jobLabel.fontStyle = FontStyles.Bold;
            view.jobLabel.alignment = TextAlignmentOptions.MidlineLeft;
            var info = view.jobLabel.transform.parent.GetComponent<VerticalLayoutGroup>();
            if (info != null) info.spacing = 14;
            Type(view.hpValueLabel, 36, UguiTheme.InfoInk);
            var hpTrack = view.hpValueLabel.transform.parent;
            Flat(hpTrack.GetComponent<Image>(), BlueSurface);
            Height(hpTrack, 56); Height(hpTrack.parent, 64);
            foreach (var icon in hpTrack.parent.GetComponentsInChildren<Image>(true).Where(x => x.name == "HpIcon")) Size(icon.transform, 40, 40);
            Height(view.atkValueLabel.transform.parent.parent, 64);
            Metric(view.atkValueLabel, "공격력");
            Metric(view.moveValueLabel, "이동");
            Type(view.expandArrow.GetComponent<TMP_Text>(), 40, UguiTheme.InfoMuted);
            Size(view.expandArrow, 44, 56);
            Flat(view.detailRoot.GetComponent<Image>(), UguiTheme.RusticPanel);
            Height(view.termPopup.transform, 112);
            Flat(view.termPopup.GetComponent<Image>(), BlueSurface);
            Type(view.termPopupLabel, 36, UguiTheme.TextPrimary);
            AutoHeight(view.termPopupLabel);
            Equation(view.atkEqRow);
            Equation(view.hpEqRow);
            Type(view.equippedLabel, 36, UguiTheme.TextSecondary);
            AutoHeight(view.equippedLabel);
            foreach (var title in root.GetComponentsInChildren<TMP_Text>(true).Where(x => x.name == "SectionTitle" || x.name == "SubsectionTitle"))
                Type(title, 42, UguiTheme.TextPrimary, 64);
        }

        private static void Metric(TMP_Text value, string name)
        {
            var chip = value.transform.parent;
            Flat(chip.GetComponent<Image>(), ChipSurface);
            Height(chip, 64);
            var row = chip.GetComponent<HorizontalLayoutGroup>();
            row.padding = new RectOffset(12, 16, 4, 4); row.spacing = 10;
            var icon = chip.Find("Icon");
            if (icon != null) Size(icon, 40, 40);
            var label = Text(chip, "MetricName", value.font);
            label.text = name; Type(label, 36, UguiTheme.InfoMuted);
            Size(label.transform, 108, 56); label.transform.SetSiblingIndex(icon != null ? icon.GetSiblingIndex() + 1 : 0);
            Type(value, 36, UguiTheme.InfoInk);
            value.alignment = TextAlignmentOptions.MidlineRight;
            Flexible(value.transform);
        }

        private static void Equation(RectTransform terms)
        {
            if (terms == null) return;
            var row = terms.parent;
            if (row.name == "EquationViewport") row = row.parent;
            Height(row, 100);
            var label = row.Find("Label")?.GetComponent<TMP_Text>();
            Type(label, 36, UguiTheme.TextSecondary);
            if (label != null) Size(label.transform, 132, 64);
            // 컨트롤러는 기존 Terms 자식만 재구성한다. 바깥 viewport를 추가해 전체 방정식과 항 버튼을 보존한다.
            var viewport = row.Find("EquationViewport") as RectTransform;
            if (viewport == null)
            {
                viewport = Rect(row, "EquationViewport");
                terms.SetParent(viewport, false);
            }
            Flexible(viewport); Height(viewport, 92);
            var image = viewport.GetComponent<Image>() ?? viewport.gameObject.AddComponent<Image>();
            image.color = new Color(0, 0, 0, .001f); image.raycastTarget = true;
            if (viewport.GetComponent<RectMask2D>() == null) viewport.gameObject.AddComponent<RectMask2D>();
            var scroll = viewport.GetComponent<ScrollRect>() ?? viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = terms; scroll.viewport = viewport; scroll.horizontal = true; scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.inertia = true;
            terms.anchorMin = Vector2.zero; terms.anchorMax = Vector2.up; terms.pivot = new Vector2(0, .5f);
            terms.anchoredPosition = Vector2.zero; terms.sizeDelta = new Vector2(0, 0);
            var old = terms.GetComponent<LayoutElement>(); if (old != null) old.enabled = false;
            var fitter = terms.GetComponent<ContentSizeFitter>() ?? terms.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
            var layout = terms.GetComponent<HorizontalLayoutGroup>();
            layout.childForceExpandWidth = false; layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.MiddleLeft; layout.spacing = 8;
        }

        private static void Skill(GameObject root)
        {
            var view = root.GetComponent<SkillRowView>();
            Flat(root.GetComponent<Image>(), UguiTheme.InfoSurface);
            var row = root.GetComponent<HorizontalLayoutGroup>();
            row.padding = new RectOffset(24, 24, 22, 22); row.spacing = 20;
            row.childAlignment = TextAnchor.UpperLeft; row.childForceExpandHeight = false;
            var rootLayout = Layout(root.transform);
            rootLayout.minHeight = 184; rootLayout.preferredHeight = -1; rootLayout.flexibleHeight = 0;
            Size(view.typeBadge.transform, 160, 64);
            Type(view.typeLabel, 36, UguiTheme.InfoInk);
            view.typeLabel.alignment = TextAlignmentOptions.Center;
            view.typeLabel.textWrappingMode = TextWrappingModes.NoWrap;
            view.typeLabel.overflowMode = TextOverflowModes.Overflow;
            Fill(view.typeLabel.rectTransform, 4);
            Type(view.nameLabel, 42, UguiTheme.InfoInk, 64);
            view.nameLabel.fontStyle = FontStyles.Bold;
            Type(view.detailLabel, 36, UguiTheme.InfoMuted);
            AutoHeight(view.detailLabel);
            var info = view.nameLabel.transform.parent;
            Flexible(info);
            var column = info.GetComponent<VerticalLayoutGroup>();
            column.spacing = 8; column.childControlHeight = true; column.childForceExpandHeight = false;
        }

        private static void Term(GameObject root)
        {
            var view = root.GetComponent<StatTermView>();
            Flat(view.background, UguiTheme.RoyalBlue);
            Type(view.label, 36, UguiTheme.TextPrimary, 60);
            view.label.alignment = TextAlignmentOptions.Center;
            view.label.textWrappingMode = TextWrappingModes.NoWrap;
            view.label.overflowMode = TextOverflowModes.Overflow;
            Height(root.transform, 72);
        }

        private static void Compare(GameObject root)
        {
            var view = root.GetComponent<StatCompareRowView>();
            var image = root.GetComponent<Image>() ?? root.AddComponent<Image>();
            Flat(image, BlueSurface); image.raycastTarget = false;
            Height(root.transform, 80);
            var row = root.GetComponent<HorizontalLayoutGroup>();
            row.padding = new RectOffset(16, 16, 8, 8); row.spacing = 12;
            foreach (var text in new[] { view.cell0, view.cell1, view.cell2, view.cell3 })
            {
                Type(text, 36, UguiTheme.TextPrimary);
                Flexible(text.transform);
                text.textWrappingMode = TextWrappingModes.NoWrap;
            }
            Layout(view.cell0.transform).flexibleWidth = 1.3f;
        }

        private static void JobCard(GameObject root)
        {
            var view = root.GetComponent<JobCardView>();
            var oldLayout = root.GetComponent<VerticalLayoutGroup>(); if (oldLayout != null) oldLayout.enabled = false;
            Height(root.transform, JobCardHeight);
            var face = Surface(root.transform, "ArmyInfoSurface", UguiTheme.InfoSurface);
            Fill(face.rectTransform, 6); face.rectTransform.offsetMin = new Vector2(6, 204); face.transform.SetAsFirstSibling();
            var footer = Surface(root.transform, "ArmyConditionSurface", BlueSurface);
            Fill(footer.rectTransform, 6); footer.rectTransform.offsetMax = new Vector2(-6, -346); footer.transform.SetSiblingIndex(1);
            var chip = Surface(root.transform, "ArmyStateChip", BlueSurface);
            Top(chip.rectTransform, 14, 64, 28);
            if (view.badge.transform.parent != chip.transform) view.badge.transform.SetParent(chip.transform, false);
            Fill(view.badge.rectTransform, 4);
            Type(view.badge, 36, UguiTheme.TextSecondary);
            view.badge.alignment = TextAlignmentOptions.Center;
            var portrait = root.transform.Find("ImgWrap") as RectTransform;
            Top(portrait, 80, 96, 16);
            var ring = portrait.Find("PortraitRing") as RectTransform;
            ring.sizeDelta = new Vector2(96, 96); ring.GetComponent<Image>().color = UguiTheme.Bronze;
            var disc = ring.Find("PortraitDisc")?.GetComponent<Image>();
            if (disc != null) disc.color = PortraitSurface;
            Type(view.nameLabel, 42, UguiTheme.InfoInk);
            view.nameLabel.fontStyle = FontStyles.Bold;
            Top(view.nameLabel.rectTransform, 184, 60, 20);
            Type(view.statLabel, 36, UguiTheme.InfoMuted);
            Top(view.statLabel.rectTransform, 252, 88, 20);
            Type(view.fragLabel, 36, UguiTheme.TextPrimary);
            Top(view.fragLabel.rectTransform, 356, 88, 16);
            Type(view.prereqLabel, 36, UguiTheme.WarnRed);
            Top(view.prereqLabel.rectTransform, 448, 88, 16);
            foreach (var label in new[] { view.statLabel, view.fragLabel, view.prereqLabel })
            {
                label.textWrappingMode = TextWrappingModes.Normal;
                label.overflowMode = TextOverflowModes.Overflow;
                label.maxVisibleLines = 2;
            }
            // 장식은 입력과 정책에 관여하지 않는다. 상태 테두리·초상화·Button 참조는 그대로 둔다.
            foreach (var image in root.GetComponentsInChildren<Image>(true).Where(x => x.name == "InnerRim" || x.name == "Sheen")) image.gameObject.SetActive(false);
        }

        private static void JobDetail(GameObject root)
        {
            var view = root.GetComponent<KAJobDetailView>();
            var header = root.transform.Find("JobHeader");
            Flat(header.GetComponent<Image>(), UguiTheme.InfoSurface);
            var row = header.GetComponent<HorizontalLayoutGroup>();
            row.padding = new RectOffset(24, 24, 24, 24); row.spacing = 24; row.childAlignment = TextAnchor.MiddleLeft;
            Height(header, 260);
            var portrait = header.Find("PortraitWrap"); Size(portrait, 168, 168);
            var ring = portrait.Find("Ring") as RectTransform;
            ring.sizeDelta = new Vector2(164, 164); ring.GetComponent<Image>().color = UguiTheme.Bronze;
            var disc = ring.Find("Disc") as RectTransform;
            disc.sizeDelta = new Vector2(150, 150); disc.GetComponent<Image>().color = PortraitSurface;
            Type(view.jobNameLabel, 48, UguiTheme.InfoInk, 72); Flexible(view.jobNameLabel.transform);
            Type(view.roleLabel, 36, UguiTheme.InfoMuted, 96);
            view.roleLabel.textWrappingMode = TextWrappingModes.Normal;
            var nameRow = view.jobNameLabel.transform.parent; Height(nameRow, 72);
            var badge = Surface(nameRow, "ArmyStateChip", BlueSurface);
            Size(badge.transform, 160, 64);
            Layout(badge.transform).ignoreLayout = false;
            if (view.stateBadge.transform.parent != badge.transform) view.stateBadge.transform.SetParent(badge.transform, false);
            Fill(view.stateBadge.rectTransform, 4); Type(view.stateBadge, 36, UguiTheme.TextPrimary);
            view.stateBadge.alignment = TextAlignmentOptions.Center;
            Flat(view.compareTable.GetComponent<Image>(), UguiTheme.RusticPanelDeep);
            var table = view.compareTable.GetComponent<VerticalLayoutGroup>(); table.padding = new RectOffset(12, 12, 12, 12); table.spacing = 8;
            var conditions = view.freeLabel.transform.parent;
            Flat(conditions.GetComponent<Image>(), BlueSurface);
            var column = conditions.GetComponent<VerticalLayoutGroup>(); column.padding = new RectOffset(24, 24, 20, 20); column.spacing = 12;
            Type(view.freeLabel, 36, UguiTheme.SuccessGreenBright); AutoHeight(view.freeLabel);
            foreach (var condition in new[] { view.fragCondRow, view.prereqCondRow })
            {
                Height(condition, 112);
                var label = condition.Find("Name")?.GetComponent<TMP_Text>();
                Type(label, 36, UguiTheme.TextSecondary); if (label != null) Size(label.transform, 180, 96);
            }
            foreach (var value in new[] { view.fragCondValue, view.prereqCondValue })
            {
                Type(value, 36, UguiTheme.TextPrimary); AutoHeight(value);
            }
            Height(view.changeRow, 144);
            view.changeRow.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = true;
            Height(view.backButton.transform, 96);
            foreach (var text in view.backButton.GetComponentsInChildren<TMP_Text>(true)) Type(text, 36, UguiTheme.TextPrimary);
            foreach (var title in root.GetComponentsInChildren<TMP_Text>(true).Where(x => x.name == "SectionTitle" || x.name == "SubsectionTitle")) Type(title, 42, UguiTheme.TextPrimary, 64);
        }

        private static void JobChange(GameObject root)
        {
            var view = root.GetComponent<KAJobChangeView>();
            var banner = view.bannerValue.transform.parent;
            Flat(banner.GetComponent<Image>(), BlueSurface); Height(banner, 160);
            var row = banner.GetComponent<HorizontalLayoutGroup>(); row.padding = new RectOffset(24, 24, 20, 20); row.spacing = 20;
            var name = banner.Find("Name")?.GetComponent<TMP_Text>();
            Type(name, 36, UguiTheme.TextSecondary); if (name != null) Size(name.transform, 180, 96);
            Type(view.bannerValue, 44, UguiTheme.AccentGoldStrong); Size(view.bannerValue.transform, 148, 96);
            Type(view.bannerHint, 36, UguiTheme.TextSecondary); AutoHeight(view.bannerHint);
            foreach (var grid in new[] { view.basicGrid, view.eliteGrid })
            {
                // 기존 helper의 고정 208px가 큰 글자를 잘라 기존 범용 responsive helper를 같은 3열로 구성한다.
                var old = grid.GetComponent<PromotionTreeGrid>(); if (old != null) old.enabled = false;
                var responsive = grid.GetComponent<ResponsiveGrid>() ?? grid.gameObject.AddComponent<ResponsiveGrid>();
                var settings = new SerializedObject(responsive);
                settings.FindProperty("minimumCellWidth").floatValue = 1;
                settings.FindProperty("maximumColumns").intValue = 3;
                settings.FindProperty("cellHeight").floatValue = JobCardHeight;
                settings.ApplyModifiedPropertiesWithoutUndo(); responsive.enabled = true;
                var layout = grid.GetComponent<GridLayoutGroup>();
                layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount; layout.constraintCount = 3;
                layout.cellSize = new Vector2(layout.cellSize.x, JobCardHeight); layout.spacing = new Vector2(16, 16);
                // 현재 각 단계는 3개 직업의 한 행이다. 빈 grid로 최초 계산되어도 부모 scroll이 높이를 잃지 않게 한다.
                Height(grid, JobCardHeight + layout.padding.vertical);
            }
            Type(view.placeholder, 36, UguiTheme.TextSecondary); AutoHeight(view.placeholder);
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true).Where(x => x.name == "SectionTitle" || x.name == "Origin" || x.transform.parent.name == "JobSectionTitle"))
                Type(text, 42, UguiTheme.TextPrimary, 64);
            foreach (var section in root.GetComponentsInChildren<RectTransform>(true).Where(x => x.name == "JobSectionTitle")) Height(section, 72);
        }

        private static LayoutElement Layout(Transform target) => target.GetComponent<LayoutElement>() ?? target.gameObject.AddComponent<LayoutElement>();
        private static void Height(Transform target, float height) { var layout = Layout(target); layout.minHeight = layout.preferredHeight = height; layout.flexibleHeight = 0; }
        private static void Size(Transform target, float width, float height) { var layout = Layout(target); layout.minWidth = layout.preferredWidth = width; layout.flexibleWidth = 0; Height(target, height); }
        private static void Flexible(Transform target) { var layout = Layout(target); layout.minWidth = layout.preferredWidth = 0; layout.flexibleWidth = 1; }
        private static void Type(TMP_Text text, float size, Color color, float height = 0)
        {
            if (text == null) return;
            text.fontSize = text.fontSizeMin = text.fontSizeMax = size; text.enableAutoSizing = false;
            text.fontStyle = FontStyles.Normal; text.color = color; text.characterSpacing = 0; text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.Normal; text.overflowMode = TextOverflowModes.Ellipsis;
            if (text.font != null) text.fontSharedMaterial = text.font.material;
            if (height > 0) Height(text.transform, height);
        }
        private static void AutoHeight(TMP_Text text)
        {
            if (text == null) return;
            text.textWrappingMode = TextWrappingModes.Normal; text.overflowMode = TextOverflowModes.Overflow; text.maxVisibleLines = int.MaxValue;
            var layout = Layout(text.transform); layout.minHeight = layout.preferredHeight = -1; layout.flexibleHeight = 0;
        }
        private static RectTransform Rect(Transform parent, string name)
        {
            if (parent.Find(name) is RectTransform found) return found;
            var root = new GameObject(name, typeof(RectTransform)); root.layer = parent.gameObject.layer;
            root.transform.SetParent(parent, false); return (RectTransform)root.transform;
        }
        private static Image Surface(Transform parent, string name, Color color)
        {
            var rect = Rect(parent, name); var image = rect.GetComponent<Image>() ?? rect.gameObject.AddComponent<Image>();
            Flat(image, color); image.raycastTarget = false; Layout(rect).ignoreLayout = true; return image;
        }
        private static TMP_Text Text(Transform parent, string name, TMP_FontAsset font)
        {
            var rect = Rect(parent, name); var text = rect.GetComponent<TMP_Text>() ?? rect.gameObject.AddComponent<TextMeshProUGUI>(); text.font = font; return text;
        }
        private static void Flat(Image image, Color color)
        {
            if (image == null) return;
            image.sprite = Rounded; image.type = Image.Type.Sliced; image.pixelsPerUnitMultiplier = 1; image.color = color;
        }
        private static void Fill(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = Vector2.one * inset; rect.offsetMax = Vector2.one * -inset;
        }
        private static void Top(RectTransform rect, float top, float height, float inset)
        {
            rect.anchorMin = Vector2.up; rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, 1);
            rect.sizeDelta = new Vector2(-inset * 2, height); rect.anchoredPosition = new Vector2(0, -top);
        }
    }
}
