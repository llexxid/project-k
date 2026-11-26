using Scripts.Core;
using Scripts.Core.Manager;
using UnityEngine;

namespace KingdomIdle.UGUI
{
    public sealed class DungeonPanelController : MonoBehaviour
    {
        private static readonly eStage[] GoldDungeonStages =
        {
            eStage.GoldDungeon1_1,
            eStage.GoldDungeon2_1,
            eStage.GoldDungeon3_1,
            eStage.GoldDungeon4_1,
            eStage.GoldDungeon5_1,
        };

        [SerializeField] private DungeonCardView[] cards;
        [SerializeField] private DungeonDifficultyPopupView difficultyPopup;

        /// <summary>안내가 중단된 상세 화면을 복원한다. 입장 로직은 호출하지 않고 기존 카드의 정보만 표시한다.</summary>
        internal void PrepareGuide(Direction.GuideContext context)
        {
            if (context == Direction.GuideContext.Dungeon) { if (difficultyPopup != null && difficultyPopup.gameObject.activeSelf) difficultyPopup.Hide(); return; }
            var type = context == Direction.GuideContext.GoldDungeonDetail ? eStageType.GoldDungeon : eStageType.RubyDungeon;
            if (difficultyPopup != null && difficultyPopup.gameObject.activeInHierarchy && difficultyPopup.GuideType == type) return;
            foreach (var card in cards) if (card != null && card.DungeonType == type) { OpenDifficultyPopup(card); return; }
        }

        private void OnEnable()
        {
            if (cards == null)
                return;

            foreach (DungeonCardView card in cards)
            {
                if (card != null)
                    card.Clicked += OpenDifficultyPopup;
            }
        }

        private void OnDisable()
        {
            if (cards == null)
                return;

            foreach (DungeonCardView card in cards)
            {
                if (card != null)
                    card.Clicked -= OpenDifficultyPopup;
            }
        }

        private void OpenDifficultyPopup(DungeonCardView card)
        {
            if (difficultyPopup == null || card == null || card.DungeonType != eStageType.GoldDungeon)
                return;

            var difficulties = new DungeonDifficultyDisplayData[GoldDungeonStages.Length];
            StageManager stageManager = StageManager.Instance;

            for (int i = 0; i < GoldDungeonStages.Length; i++)
            {
                eStage stage = GoldDungeonStages[i];
                bool isUnlocked = stageManager != null &&
                                  stageManager.IsDungeonStageUnlocked(stage);
                difficulties[i] = new DungeonDifficultyDisplayData(
                    stage,
                    isUnlocked,
                    0); // No authored recommendation exists; do not fabricate a power gate.
            }

            difficultyPopup.SetDifficultyData(
                difficulties,
                CombatPowerCalculator.CalculatePartyPowerV1(UserManager.Instance != null ? UserManager.Instance.GetPlayers() : null));
            difficultyPopup.Show(card);
        }

    }
}
