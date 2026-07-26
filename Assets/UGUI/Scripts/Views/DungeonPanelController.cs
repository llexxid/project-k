using System.Collections.Generic;
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

        private static readonly eStage[] RubyDungeonStages =
        {
            eStage.RubyDungeon1_1,
            eStage.RubyDungeon2_1,
            eStage.RubyDungeon3_1,
            eStage.RubyDungeon4_1,
            eStage.RubyDungeon5_1,
        };

        [SerializeField] private DungeonCardView[] cards;
        [SerializeField] private DungeonDifficultyPopupView difficultyPopup;
        [SerializeField] private long placeholderCurrentPower = 4000;

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
            if (difficultyPopup == null || card == null)
                return;

            IReadOnlyList<eStage> stages = GetDungeonStages(card.DungeonType);
            var difficulties = new DungeonDifficultyDisplayData[stages.Count];
            StageManager stageManager = StageManager.Instance;

            for (int i = 0; i < stages.Count; i++)
            {
                eStage stage = stages[i];
                bool isUnlocked = stageManager != null &&
                                  stageManager.IsDungeonStageUnlocked(stage);
                difficulties[i] = new DungeonDifficultyDisplayData(
                    stage,
                    isUnlocked,
                    GetPlaceholderRecommendedPower(stage));
            }

            difficultyPopup.SetDifficultyData(
                difficulties,
                placeholderCurrentPower);
            difficultyPopup.Show(card);
        }

        private static IReadOnlyList<eStage> GetDungeonStages(eStageType dungeonType)
        {
            switch (dungeonType)
            {
                case eStageType.GoldDungeon:
                    return GoldDungeonStages;
                case eStageType.RubyDungeon:
                    return RubyDungeonStages;
                default:
                    return System.Array.Empty<eStage>();
            }
        }

        private static long GetPlaceholderRecommendedPower(eStage stage)
        {
            return StageParser.GetStageNumber(stage) * 2700L;
        }
    }
}
