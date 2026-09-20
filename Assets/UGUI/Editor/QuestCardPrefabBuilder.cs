using TMPro;
using UnityEditor;
using UnityEngine;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>기존 행을 보상·진행·행동 구조로 연결한다. 원래 GUID와 기존 컴포넌트 참조를 보존한다.</summary>
    public static class QuestCardPrefabBuilder
    {
        private const string Path = "Assets/UGUI/Prefabs/Items/Item_GuideStepRow.prefab";

        /// <summary>행 프리팹 하나만 Unity 직렬화 API로 저장한다. 열린 씬이나 다른 UI 자산은 저장하지 않는다.</summary>
        [MenuItem("KingdomIdle/UGUI/Apply quest reward cards")]
        public static void Apply()
        {
            var root = PrefabUtility.LoadPrefabContents(Path);
            try
            {
                Upgrade(root);
                PrefabUtility.SaveAsPrefabAsset(root, Path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        /// <summary>기존 생성기도 같은 업그레이드를 사용해 재생성 결과와 실제 자산을 일치시킨다.</summary>
        internal static void Upgrade(GameObject root)
        {
            var view = root.GetComponent<GuideStepRowView>();
            if (view == null) throw new System.InvalidOperationException("Quest row View is missing.");
            // 기존 자동 컬럼 배치를 중지하고 새 카드의 고정 두 행 + 가변 가로폭을 사용한다.
            foreach (var layout in root.GetComponentsInChildren<UnityEngine.UI.LayoutGroup>(true)) layout.enabled = false;
            var height = root.GetComponent<UnityEngine.UI.LayoutElement>() ?? root.AddComponent<UnityEngine.UI.LayoutElement>();
            height.minHeight = height.preferredHeight = 192;
            var background = root.GetComponent<UnityEngine.UI.Image>();
            background.color = UguiTheme.RusticPanel;
            background.raycastTarget = false;
            // 기존 전체 행 버튼이 있는 자산에서도 이중 수령/이동 입력을 만들지 않는다.
            if (root.TryGetComponent<UnityEngine.UI.Button>(out var oldArea)) oldArea.enabled = false;
            var inner = (RectTransform)root.transform.Find("RowInner");
            Stretch(inner, 0, 0, 0, 0);
            var body = (RectTransform)inner.Find("TextCol");
            Stretch(body, 172, 0, 24, 0);
            view.descLabel.gameObject.SetActive(false);
            view.hintLabel.gameObject.SetActive(false);
            // 상단 설명은 두 줄까지 허용하며 터치 영역/진행바와 겹치지 않게 높이를 고정한다.
            TopStretch(view.titleLabel.rectTransform, 18, 62, 0);
            view.titleLabel.fontSize = 28;
            view.titleLabel.fontStyle = FontStyles.Normal;
            view.titleLabel.alignment = TextAlignmentOptions.MidlineLeft;
            view.titleLabel.textWrappingMode = TextWrappingModes.Normal;
            view.titleLabel.overflowMode = TextOverflowModes.Ellipsis;
            view.titleLabel.maxVisibleLines = 2;

            var track = Image(body, "ProgressTrack", UguiTheme.Bronze);
            TopStretch(track.rectTransform, 106, 42, 188);
            var empty = Image(track.transform, "Empty", UguiTheme.RusticSurfaceDark);
            Stretch(empty.rectTransform, 3, 3, 3, 3);
            view.progressFill = Image(empty.transform, "Fill", new Color32(44, 115, 56, 255));
            Stretch(view.progressFill.rectTransform, 0, 0, 0, 0);
            // 별도 텍스처 없이 평면 사각형으로 표시한다. View가 anchorMax.x를 진행 비율로 맞춘다.
            view.progressFill.sprite = null;
            view.progressFill.type = UnityEngine.UI.Image.Type.Simple;
            view.progressLabel = Text(track.transform, "Value", view.titleLabel.font, 26);
            Stretch(view.progressLabel.rectTransform, 2, 0, 2, 0);

            // 좌측 체크 버튼 컴포넌트를 우측 행동 버튼으로 옮긴다. 원래 직렬화 필드도 같은 버튼을 가리킨다.
            view.actionButton = view.checkButton;
            view.actionButton.transform.SetParent(body, false);
            view.actionButton.name = "ActionButton";
            var actionRect = (RectTransform)view.actionButton.transform;
            actionRect.anchorMin = actionRect.anchorMax = new Vector2(1, 1);
            actionRect.pivot = new Vector2(1, 1);
            actionRect.anchoredPosition = new Vector2(0, -82);
            actionRect.sizeDelta = new Vector2(168, 92);
            foreach (Transform child in actionRect) child.gameObject.SetActive(false);
            var actionBg = view.actionButton.GetComponent<UnityEngine.UI.Image>();
            actionBg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UGUI/Sprites/RoundedRect.png");
            actionBg.type = UnityEngine.UI.Image.Type.Sliced;
            actionBg.color = UguiTheme.RusticSurface;
            actionBg.raycastTarget = true;
            actionBg.raycastPadding = new Vector4(0, -18, 0, -18);
            view.actionButton.targetGraphic = actionBg;
            view.actionButton.colors = UguiTheme.MakeColorBlock();
            view.actionLabel = Text(actionRect, "ActionLabel", view.titleLabel.font, 28);
            view.actionLabel.gameObject.SetActive(true);
            Stretch(view.actionLabel.rectTransform, 0, 0, 0, 0);
            view.checkLabel = view.actionLabel;
            view.actionLabel.text = "이동 ›";
            view.periodLabel = Text(body, "Period", view.titleLabel.font, 20);
            view.periodLabel.alignment = TextAlignmentOptions.Left;
            TopStretch(view.periodLabel.rectTransform, 165, 25, 188);
            view.periodLabel.gameObject.SetActive(false);
            AddReward(inner, "Reward", view.titleLabel.font, out view.rewardIcon, out view.rewardAmountLabel);
            AddReward(inner, "SecondaryReward", view.titleLabel.font, out view.secondaryRewardIcon, out view.secondaryRewardAmountLabel);
            view.secondaryRewardIcon.transform.parent.gameObject.SetActive(false);
        }

        /// <summary>아이콘과 수량을 한 묶음으로 만든다. 목록의 진행도와 관계없이 보상 정보는 항상 남긴다.</summary>
        private static void AddReward(Transform parent, string name, TMP_FontAsset font, out UnityEngine.UI.Image icon, out TMP_Text amount)
        {
            var slot = Rect(parent, name);
            slot.anchorMin = slot.anchorMax = slot.pivot = new Vector2(0, 1);
            slot.sizeDelta = new Vector2(120, 140);
            slot.anchoredPosition = new Vector2(24, -24);
            icon = Image(slot, "Icon", Color.white);
            icon.preserveAspect = true;
            icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = icon.rectTransform.pivot = new Vector2(.5f, 1);
            icon.rectTransform.anchoredPosition = Vector2.zero;
            icon.rectTransform.sizeDelta = new Vector2(96, 96);
            amount = Text(slot, "Amount", font, 24);
            TopStretch(amount.rectTransform, 102, 30, 0);
        }

        /// <summary>동일 이름의 기존 자식을 재사용해 반복 실행 시 중복 오브젝트가 생기지 않는다.</summary>
        private static RectTransform Rect(Transform parent, string name)
        {
            if (parent.Find(name) is RectTransform existing) return existing;
            var child = new GameObject(name, typeof(RectTransform));
            child.layer = parent.gameObject.layer;
            child.transform.SetParent(parent, false);
            return (RectTransform)child.transform;
        }

        /// <summary>평면 색 Image를 생성/재사용한다. 표시 전용 그래픽은 입력을 가로채지 않는다.</summary>
        private static UnityEngine.UI.Image Image(Transform parent, string name, Color color)
        {
            var rect = Rect(parent, name);
            var image = rect.GetComponent<UnityEngine.UI.Image>() ?? rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>기존 폰트 자산으로 글자를 만든다. 고정 크기와 생략 표시로 반복 AutoSize 계산을 피한다.</summary>
        private static TMP_Text Text(Transform parent, string name, TMP_FontAsset font, float size)
        {
            var rect = Rect(parent, name);
            var text = rect.GetComponent<TextMeshProUGUI>() ?? rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font; text.fontSize = size; text.color = UguiTheme.Parchment;
            text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }

        /// <summary>부모 안쪽 여백으로 늘린다. 프로젝트의 CanvasScaler 설정은 바꾸지 않는다.</summary>
        private static void Stretch(RectTransform rect, float left, float top, float right, float bottom)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom); rect.offsetMax = new Vector2(-right, -top);
        }

        /// <summary>상단에 고정하고 오른쪽 여백을 제외한 가로폭을 부모에 맞춘다.</summary>
        private static void TopStretch(RectTransform rect, float top, float height, float right)
        {
            rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, 1);
            rect.sizeDelta = new Vector2(-right, height); rect.anchoredPosition = new Vector2(-right / 2, -top);
        }
    }
}
