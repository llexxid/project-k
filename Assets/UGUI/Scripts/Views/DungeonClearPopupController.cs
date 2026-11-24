using Scripts.Core;
using Scripts.Core.Manager;
using UnityEngine;

namespace KingdomIdle.UGUI
{
    /// <summary>던전 결과 팝업을 1회 생성해 재사용하고 StageManager 명령만 전달한다.</summary>
    public static class DungeonClearPopupController
    {
        private static DungeonClearPopupView view;

        public static bool IsOpen =>
            view != null && view.gameObject.activeSelf;

        public static void Show(StageDefinition clearedDefinition)
        {
            if (clearedDefinition == null ||
                clearedDefinition.Type == eStageType.Main ||
                !EnsureBuilt())
            {
                return;
            }

            string dungeonName =
                clearedDefinition.Type == eStageType.GoldDungeon
                    ? "골드"
                    : "루비";

            var state = KingdomIdle.Balance.LocalProgression.State;
            long reward = clearedDefinition.Type == eStageType.GoldDungeon ? state.LastDungeonGold : state.LastDungeonRuby;
            view.titleLabel.enableAutoSizing = true;
            view.titleLabel.fontSizeMin = 24;
            view.titleLabel.fontSizeMax = 32;
            view.titleLabel.text = $"{dungeonName} 던전 {clearedDefinition.StageNumber}단계 클리어\n획득 {dungeonName} +{NumberNotation.Format(reward)}";
            RefreshActions();
            view.gameObject.SetActive(true);
            view.transform.SetAsLastSibling();
            if (view.panel != null)
                UITween.PopIn(view.panel);
        }

        internal static void RefreshActions()
        {
            if (view == null) return;
            var stage = StageManager.Instance;
            bool canAdvance = stage != null && stage.CanAdvanceDungeonResult;
            view.retryButton.interactable = stage != null && stage.CanRetryDungeonResult;
            view.nextButton.interactable = canAdvance;
            view.autoAdvance.interactable = canAdvance;
            view.autoAdvance.SetIsOnWithoutNotify(canAdvance && stage.AutoAdvanceDungeon);
            view.autoAdvance.targetGraphic.canvasRenderer.SetAlpha(canAdvance ? 1f : .55f);
        }

        public static void Hide()
        {
            if (view != null)
                view.gameObject.SetActive(false);
        }

        private static bool EnsureBuilt()
        {
            if (view != null)
                return true;

            UIManager host = UIManager.Instance;
            GameObject prefab =
                host != null && host.Catalog != null
                    ? host.Catalog.popupDungeonClear
                    : null;
            if (host == null || prefab == null)
            {
                Debug.LogWarning(
                    "[DungeonClearPopup] 카탈로그의 팝업 프리팹이 없습니다.");
                return false;
            }

            GameObject instance = Object.Instantiate(
                prefab,
                host.LayerPopups,
                false);
            Stretch(instance.transform as RectTransform);
            view = instance.GetComponent<DungeonClearPopupView>();
            if (view == null)
            {
                Debug.LogError(
                    "[DungeonClearPopup] DungeonClearPopupView가 없습니다.");
                Object.Destroy(instance);
                return false;
            }

            view.exitButton.onClick.AddListener(Exit);
            ModalBackHandler.Bind(instance, Exit);
            view.nextButton.onClick.AddListener(Next);
            view.autoAdvance.onValueChanged.AddListener(SetAutoAdvance);
            view.retryButton.onClick.AddListener(Retry);
            view.gameObject.SetActive(false);
            return true;
        }

        private static void Exit()
        {
            StageManager.Instance?.ReturnToMainStage();
            Hide();
        }

        private static void SetAutoAdvance(bool enabled)
        {
            var stage = StageManager.Instance;
            if (stage != null) stage.AutoAdvanceDungeon = enabled && stage.CanAdvanceDungeonResult;
        }

        // Keep actions available if the server rejects entry. StageEntered dismisses the result on success.
        private static void Next() => StageManager.Instance?.ContinueDungeon();
        private static void Retry() => StageManager.Instance?.RestartDungeon();

        private static void Stretch(RectTransform rect)
        {
            if (rect == null)
                return;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
