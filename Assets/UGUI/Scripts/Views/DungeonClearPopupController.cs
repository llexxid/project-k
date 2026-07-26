using Scripts.Core;
using Scripts.Core.Manager;
using UnityEngine;

namespace KingdomIdle.UGUI
{
    public static class DungeonClearPopupController
    {
        private static GameObject instance;

        public static void Show(StageDefinition definition)
        {
            if (definition == null || definition.Type == eStageType.Main)
                return;

            UIManager host = UIManager.Instance;
            if (host == null ||
                host.Catalog == null ||
                host.Catalog.popupDungeonClear == null)
            {
                Debug.LogWarning("[DungeonClearPopup] 팝업 프리팹이 카탈로그에 없습니다.");
                return;
            }

            Close();
            instance = Object.Instantiate(
                host.Catalog.popupDungeonClear,
                host.LayerPopups,
                false);
            UguiRuntimeFactory.Stretch(instance.transform as RectTransform);

            DungeonClearPopupView view =
                instance.GetComponent<DungeonClearPopupView>();
            if (view == null)
            {
                Debug.LogError("[DungeonClearPopup] DungeonClearPopupView가 없습니다.");
                Close();
                return;
            }

            StageManager stageManager = StageManager.Instance;
            string dungeonName = definition.Type == eStageType.GoldDungeon
                ? "골드"
                : "루비";
            string title =
                $"{dungeonName} {definition.StageNumber}스테이지 클리어!";

            view.Bind(
                title,
                definition.HasNextDifficulty,
                () =>
                {
                    stageManager?.ReturnToMainStage();
                    Close();
                },
                () =>
                {
                    if (definition.HasNextDifficulty)
                        stageManager?.ContinueDungeon();
                    Close();
                },
                () =>
                {
                    stageManager?.RestartDungeon();
                    Close();
                });
        }

        public static void Close()
        {
            if (instance != null)
                Object.Destroy(instance);
            instance = null;
        }
    }
}
