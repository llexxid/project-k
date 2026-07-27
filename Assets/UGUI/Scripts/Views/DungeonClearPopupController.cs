using Scripts.Core;
using Scripts.Core.Manager;
using UnityEngine;

namespace KingdomIdle.UGUI
{
    /// <summary>던전 결과 팝업을 1회 생성해 재사용하고 StageManager 명령만 전달한다.</summary>
    public static class DungeonClearPopupController
    {
        private static DungeonClearPopupView view;
        private static StageDefinition definition;

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

            definition = clearedDefinition;
            string dungeonName =
                clearedDefinition.Type == eStageType.GoldDungeon
                    ? "골드"
                    : "루비";

            view.titleLabel.text =
                $"{dungeonName} {clearedDefinition.StageNumber}스테이지 클리어!";
            view.nextButton.interactable =
                clearedDefinition.HasNextDifficulty;
            view.gameObject.SetActive(true);
            view.transform.SetAsLastSibling();
            if (view.panel != null)
                UITween.PopIn(view.panel);
        }

        public static void Hide()
        {
            if (view != null)
                view.gameObject.SetActive(false);
            definition = null;
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
            view.nextButton.onClick.AddListener(Next);
            view.retryButton.onClick.AddListener(Retry);
            view.gameObject.SetActive(false);
            return true;
        }

        private static void Exit()
        {
            StageManager.Instance?.ReturnToMainStage();
            Hide();
        }

        private static void Next()
        {
            if (definition != null &&
                definition.HasNextDifficulty)
            {
                StageManager.Instance?.ContinueDungeon();
            }
            Hide();
        }

        private static void Retry()
        {
            StageManager.Instance?.RestartDungeon();
            Hide();
        }

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
