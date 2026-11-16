using TMPro;
using System.Collections.Generic;
using KingdomIdle.Balance;
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

        /// <summary>계정·서버를 열지 않고 실제 직렬화 자산과 수령 표시/입력 게이트를 검사한다.</summary>
        public static object ValidateBronzeCards()
        {
            if(Application.isPlaying)throw new System.InvalidOperationException("ValidateBronzeCards requires EditMode; no gameplay account is used.");
            var checks=new List<string>();
            void Check(bool value,string message)
            {
                if(!value)throw new System.InvalidOperationException("Bronze quest card: "+message);
                checks.Add(message);
            }
            var root=PrefabUtility.LoadPrefabContents(Path);
            try
            {
                var view=root.GetComponent<GuideStepRowView>();
                var catalog=AssetDatabase.LoadAssetAtPath<UIViewCatalog>(PrefabGenUtil.CatalogPath);
                Check(view.actionButton!=null && view.actionButton.gameObject!=root && view.checkButton==view.actionButton &&
                    (root.GetComponent<UnityEngine.UI.Button>() == null || !root.GetComponent<UnityEngine.UI.Button>().enabled),
                    "Only the separate action button can receive the selected quest reward");
                Check(view.claimBorder==root.GetComponent<UnityEngine.UI.Image>() && root.transform.Find("InfoSurface")!=null,
                    "Claim border surrounds a separate information surface");
                Check(view.titleLabel.fontSize>=42 && view.actionLabel.fontSize>=36,
                    "Title and status retain readable mobile text sizes");
                var rewards=new[]{new QuestRewardSnapshot(eCurrency.Gold,100,false),new QuestRewardSnapshot(eCurrency.Ruby,2,false)};
                QuestRowSnapshot Snapshot(QuestRowState state,long progress)=>new QuestRowSnapshot(
                    new QuestClaimToken(0,20001,"bronze-validation"),eQuestCategory.Daily,"검사","몬스터 10마리 처치",
                    eQuestObjectiveType.MonsterKill,0,10,progress,state,false,0,rewards);
                view.SetQuest(Snapshot(QuestRowState.InProgress,5),catalog);
                Check(Mathf.Approximately(view.progressFill.fillAmount,.5f) && view.secondaryRewardIcon.transform.parent.gameObject.activeSelf,
                    "In-progress fraction and both typed rewards remain visible");
                var dynamicGold=new QuestRowSnapshot(new QuestClaimToken(0,20001,"bronze-validation"),eQuestCategory.Daily,
                    "검사","몬스터 10마리 처치",eQuestObjectiveType.MonsterKill,0,10,5,QuestRowState.InProgress,false,0,
                    new[]{new QuestRewardSnapshot(eCurrency.Gold,0,true)});
                view.SetQuest(dynamicGold,catalog);
                Check(view.rewardAmountLabel.text=="2분\n골드" && view.rewardAmountLabel.rectTransform.rect.height>=88,
                    "Dynamic gold keeps both the duration and currency visible on two lines");
                view.SetQuest(Snapshot(QuestRowState.Claimable,10),catalog);
                Check(view.actionButton.interactable && view.actionLabel.text=="보상받기" && view.claimBorder.color==UguiTheme.ActionGold,
                    "Claimable cell lights its border and offers an individual reward button");
                view.SetQuest(Snapshot(QuestRowState.Claimable,10),catalog,claimBusy:true);
                Check(!view.actionButton.interactable && view.actionLabel.text=="수령 중…",
                    "Pending reward disables duplicate cell input");
                view.SetQuest(Snapshot(QuestRowState.Claimed,10),catalog);
                Check(!view.actionButton.interactable && view.actionLabel.text=="완료" && view.progressFill.fillAmount==1,
                    "Confirmed reward keeps its full gauge and completed cell");
                view.SetQuest(Snapshot(QuestRowState.Locked,0),catalog);
                Check(!view.actionButton.interactable && view.actionLabel.text=="잠김",
                    "Locked cell cannot move or claim");
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            var hintRoot=PrefabUtility.LoadPrefabContents("Assets/UGUI/Prefabs/Items/Item_GuideEmptyHint.prefab");
            try
            {
                var hint=hintRoot.GetComponent<GuideEmptyHintView>();
                hint.label.color=UguiTheme.InfoMuted;
                hint.SetText("퀘스트 정보를 준비하고 있습니다.");
                Check(hint.label.color==UguiTheme.TextSecondary,
                    "Empty guidance restores readable text on a dark shell even when an old prefab has dark ink");
            }
            finally{PrefabUtility.UnloadPrefabContents(hintRoot);}
            return new{passed=checks.Count,checks};
        }

        /// <summary>기존 생성기도 같은 업그레이드를 사용해 재생성 결과와 실제 자산을 일치시킨다.</summary>
        internal static void Upgrade(GameObject root)
        {
            var view = root.GetComponent<GuideStepRowView>();
            if (view == null) throw new System.InvalidOperationException("Quest row View is missing.");
            // 기존 자동 컬럼 배치를 중지하고 새 카드의 고정 두 행 + 가변 가로폭을 사용한다.
            foreach (var layout in root.GetComponentsInChildren<UnityEngine.UI.LayoutGroup>(true)) layout.enabled = false;
            var height = root.GetComponent<UnityEngine.UI.LayoutElement>() ?? root.AddComponent<UnityEngine.UI.LayoutElement>();
            height.minHeight = height.preferredHeight = 280;
            var background = root.GetComponent<UnityEngine.UI.Image>();
            var rounded = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UGUI/Sprites/RoundedRect.png");
            background.sprite = rounded;
            background.type = UnityEngine.UI.Image.Type.Sliced;
            background.color = UguiTheme.Bronze;
            background.raycastTarget = false;
            view.claimBorder = background;
            // 청동 외곽 안쪽에 밝은 정보면을 놓아 점등이 본문·아이콘을 덮지 않게 한다.
            var surface = Image(root.transform, "InfoSurface", UguiTheme.InfoSurface);
            surface.sprite = rounded;
            surface.type = UnityEngine.UI.Image.Type.Sliced;
            Stretch(surface.rectTransform, 5, 5, 5, 5);
            surface.transform.SetAsFirstSibling();
            // 셀 전체 수령을 끄고 우측 개별 버튼 하나만 입력을 받는다.
            if (view.checkButton != null && view.checkButton.gameObject != root)
                view.checkButton.gameObject.SetActive(false);
            if (root.TryGetComponent<UnityEngine.UI.Button>(out var area)) area.enabled = false;
            var inner = (RectTransform)root.transform.Find("RowInner");
            Stretch(inner, 0, 0, 0, 0);
            var body = (RectTransform)inner.Find("TextCol");
            Stretch(body, 196, 0, 28, 0);
            view.descLabel.gameObject.SetActive(false);
            view.hintLabel.gameObject.SetActive(false);
            // 상단 설명은 두 줄까지 허용하며 터치 영역/진행바와 겹치지 않게 높이를 고정한다.
            TopStretch(view.titleLabel.rectTransform, 22, 112, 0);
            view.titleLabel.fontSize = 42;
            view.titleLabel.color = UguiTheme.InfoInk;
            if(view.titleLabel.font!=null)view.titleLabel.fontSharedMaterial=view.titleLabel.font.material;
            view.titleLabel.fontStyle = FontStyles.Normal;
            view.titleLabel.alignment = TextAlignmentOptions.MidlineLeft;
            view.titleLabel.textWrappingMode = TextWrappingModes.Normal;
            view.titleLabel.overflowMode = TextOverflowModes.Ellipsis;
            view.titleLabel.maxVisibleLines = 2;

            var track = Image(body, "ProgressTrack", UguiTheme.Bronze);
            TopStretch(track.rectTransform, 154, 48, 256);
            var empty = Image(track.transform, "Empty", UguiTheme.RusticSurfaceDark);
            Stretch(empty.rectTransform, 3, 3, 3, 3);
            view.progressFill = Image(empty.transform, "Fill", new Color32(44, 115, 56, 255));
            Stretch(view.progressFill.rectTransform, 0, 0, 0, 0);
            // 별도 텍스처 없이 평면 사각형으로 표시한다. View가 anchorMax.x를 진행 비율로 맞춘다.
            view.progressFill.sprite = null;
            view.progressFill.type = UnityEngine.UI.Image.Type.Simple;
            view.progressLabel = Text(track.transform, "Value", view.titleLabel.font, 36);
            view.progressLabel.color = UguiTheme.Parchment;
            Stretch(view.progressLabel.rectTransform, 2, 0, 2, 0);

            var actionRect = Rect(body, "ActionButton");
            actionRect.gameObject.SetActive(true);
            actionRect.anchorMin = actionRect.anchorMax = new Vector2(1, 1);
            actionRect.pivot = new Vector2(1, 1);
            actionRect.anchoredPosition = new Vector2(0, -140);
            actionRect.sizeDelta = new Vector2(232, 128);
            var actionImage = Image(body, "ActionButton", UguiTheme.ActionGold);
            actionImage.sprite = rounded;
            actionImage.type = UnityEngine.UI.Image.Type.Sliced;
            actionImage.raycastTarget = true;
            view.actionButton = actionRect.GetComponent<UnityEngine.UI.Button>() ?? actionRect.gameObject.AddComponent<UnityEngine.UI.Button>();
            view.actionButton.enabled = true;
            view.actionButton.targetGraphic = actionImage;
            view.actionButton.transition = UnityEngine.UI.Selectable.Transition.ColorTint;
            view.actionButton.colors = UguiTheme.MakeColorBlock();
            if (actionRect.GetComponent<PlayClickSfxOnClick>() == null) actionRect.gameObject.AddComponent<PlayClickSfxOnClick>();
            view.checkButton = view.actionButton;
            view.actionLabel = Text(actionRect, "Label", view.titleLabel.font, 36);
            Stretch(view.actionLabel.rectTransform, 4, 0, 4, 0);
            view.actionLabel.gameObject.SetActive(true);
            view.checkLabel = view.actionLabel;
            view.actionLabel.text = "이동 ›";
            if (body.Find("Status") is Transform oldStatus) oldStatus.gameObject.SetActive(false);
            view.periodLabel = Text(body, "Period", view.titleLabel.font, 32);
            view.periodLabel.alignment = TextAlignmentOptions.Left;
            TopStretch(view.periodLabel.rectTransform, 220, 40, 256);
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
            slot.sizeDelta = new Vector2(180, 140);
            slot.anchoredPosition = new Vector2(8, -24);
            icon = Image(slot, "Icon", Color.white);
            icon.preserveAspect = true;
            icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = icon.rectTransform.pivot = new Vector2(.5f, 1);
            icon.rectTransform.anchoredPosition = Vector2.zero;
            icon.rectTransform.sizeDelta = new Vector2(96, 96);
            amount = Text(slot, "Amount", font, 36);
            TopStretch(amount.rectTransform, 102, 44, 0);
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
            text.font = font; text.fontSize = size; text.color = UguiTheme.InfoInk;
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
