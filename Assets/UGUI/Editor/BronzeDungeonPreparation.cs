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
        }

        private static void ApplyCard(DungeonCardView card)
        {
            var root = card.transform;
            Flat(root.GetComponent<Image>(), UguiTheme.InfoSurface);
            Height(root, 300);
            var group = root.GetComponent<CanvasGroup>();
            if (group != null) group.alpha = 1f; // 준비 중 상태도 글자는 같은 명도로 읽힌다.
            var button = Ref<Button>(card, "button");
            NeutralTint(button);
            var row = root.GetComponent<HorizontalLayoutGroup>();
            if (row != null) { row.spacing = 22; row.padding = new RectOffset(22, 24, 20, 20); }
            var preview = root.Find("PreviewFrame");
            if (preview != null)
            {
                Width(preview, 260); Height(preview, 230);
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
            Type(description, 36, UguiTheme.InfoMuted); Height(description.transform, 92);
            description.textWrappingMode = TextWrappingModes.Normal;
            // 기존 카드의 보상 목적을 짧게 표시한다. 해금·보상 정책은 만들지 않는다.
            if (card.DungeonType == Scripts.Core.eStageType.GoldDungeon || card.DungeonName.Contains("골드")) description.text = "골드를 획득합니다.";
            else if (card.DungeonType == Scripts.Core.eStageType.RubyDungeon || card.DungeonName.Contains("루비")) description.text = "루비를 획득합니다.";
            else if (card.DungeonName.Contains("장비")) description.text = "장비 · 강화 재료";
            var nameRow = title.transform.parent;
            Height(nameRow, 64);
            var info = root.Find("InfoCol")?.GetComponent<VerticalLayoutGroup>();
            if (info != null) info.spacing = 12;
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
            // Window와 팝업 루트의 앵커·높이·반응형 피터는 기존 값을 유지한다.
            Flat(window.GetComponent<Image>(), UguiTheme.RusticPanel);
            var layout = window.GetComponent<VerticalLayoutGroup>();
            if (layout != null) { layout.spacing = 14; layout.padding = new RectOffset(30, 30, 28, 28); }
            var preview = window.Find("PreviewCard") as RectTransform;
            if (preview == null) throw new InvalidOperationException("던전 프리뷰 카드 누락");
            Flat(preview.GetComponent<Image>(), UguiTheme.InfoSurface);
            Height(preview, 330);
            var band = preview.Find("TitleBand") as RectTransform;
            if (band == null) throw new InvalidOperationException("던전 설명 영역 누락");
            var bandImage = band.GetComponent<Image>();
            if (bandImage != null) bandImage.color = Color.clear;
            SetRect(band, Vector2.zero, new Vector2(.68f, 1), new Vector2(22, 18), new Vector2(-8, -18));
            var bandLayout = band.GetComponent<VerticalLayoutGroup>();
            if (bandLayout != null) { bandLayout.padding = new RectOffset(0, 0, 8, 8); bandLayout.spacing = 10; }
            var name = Ref<TMP_Text>(popup, "dungeonName");
            var description = Ref<TMP_Text>(popup, "description");
            Type(name, 54, UguiTheme.InfoInk); Height(name.transform, 70);
            Type(description, 34, UguiTheme.InfoMuted); Height(description.transform, 180);
            description.textWrappingMode = TextWrappingModes.Normal;
            description.overflowMode = TextOverflowModes.Ellipsis;
            var art = preview.Find("PortalArtFrame") as RectTransform;
            if (art == null) art = F.Box(preview, "PortalArtFrame", UguiTheme.RusticPanelDeep, rounded: true).rectTransform;
            SetRect(art, new Vector2(.69f, 0), Vector2.one, new Vector2(4, 22), new Vector2(-16, -94));
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
            Type(section, 42, UguiTheme.Parchment); Height(section.transform, 58);
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
            Width(enter.transform, 350); Height(enter.transform, 132);
            NeutralTint(enter, tintDisabled: true);
            var close = window.Find("BtnClose") as RectTransform;
            if (close != null)
            {
                close.sizeDelta = Vector2.one * 112;
                Layout(close).ignoreLayout = true;
                var icon = close.Find("Icon") as RectTransform;
                if (icon != null) icon.sizeDelta = Vector2.one * 46;
            }
            Frame(window, "BronzeDungeonFrame", UguiTheme.BronzeLight);
            DisableDecoration(window);
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
                    Ref<Button>(card, "button") == null)
                    throw new InvalidOperationException("던전 카드 표시/참조 불일치: " + card.name);
            }
            foreach (var popup in root.GetComponentsInChildren<DungeonDifficultyPopupView>(true))
            {
                if (popup.GetComponentsInChildren<PartyHudSuppressor>(true).Length != 0 ||
                    Ref<Image>(popup, "mainImage").transform.parent.name != "PortalArtFrame" ||
                    Ref<TMP_Text>(popup, "dungeonName").color != UguiTheme.InfoInk ||
                    Ref<Button>(popup, "enterButton").GetComponent<Image>().color != UguiTheme.ActionGold ||
                    Ref<ScrollRect>(popup, "difficultyScroll") == null)
                    throw new InvalidOperationException("던전 상세 표시/참조 불일치: " + popup.name);
            }
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
