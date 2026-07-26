using System.Collections.Generic;
using Scripts.Core;
using Scripts.Core.Manager;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomIdle.UGUI
{
    public sealed class DungeonDifficultyPopupView : MonoBehaviour
    {
        [SerializeField] private Button backdropButton;
        [SerializeField] private Button enterButton;
        [SerializeField] private Image mainImage;
        [SerializeField] private TMP_Text dungeonName;
        [SerializeField] private TMP_Text description;
        [SerializeField] private TMP_Text selectedDifficultyLabel;
        [SerializeField] private ScrollRect difficultyScroll;
        [SerializeField] private DungeonDifficultyRowView[] difficultyRows;
        [SerializeField] private DungeonInfoCarouselView clearRewardCarousel;
        [SerializeField] private DungeonInfoCarouselView monsterCarousel;
        [SerializeField] private DungeonDifficultyDisplayData[] placeholderDifficulties;
        [SerializeField] private long placeholderCurrentPower = 4000;
        [SerializeField] private Sprite[] placeholderClearRewards;
        [SerializeField] private Sprite[] placeholderMonsters;

        private int selectedDifficulty = 1;
        private eStage selectedStageId;
        private bool hasDifficultyData;
        private readonly Dictionary<int, eStage> stageIdsByNumber = new();

        private void Awake()
        {
            if (backdropButton != null)
                backdropButton.onClick.AddListener(Hide);
            if (enterButton != null)
                enterButton.onClick.AddListener(HandleEnterClicked);

            SetInfoItems(placeholderClearRewards, placeholderMonsters);
        }

        private void OnDestroy()
        {
            if (backdropButton != null)
                backdropButton.onClick.RemoveListener(Hide);
            if (enterButton != null)
                enterButton.onClick.RemoveListener(HandleEnterClicked);
        }

        public void Show(DungeonCardView card)
        {
            if (card == null)
                return;

            if (!hasDifficultyData)
                SetDifficultyData(placeholderDifficulties, placeholderCurrentPower);

            if (dungeonName != null)
                dungeonName.text = card.DungeonName;
            if (description != null)
                description.text = card.Description;
            if (mainImage != null)
            {
                mainImage.sprite = card.PreviewSprite;
                mainImage.color = card.PreviewColor;
                mainImage.preserveAspect = card.PreviewSprite != null;
            }

            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            Canvas.ForceUpdateCanvases();
            if (difficultyScroll != null)
                difficultyScroll.verticalNormalizedPosition = 1f;
            if (selectedDifficulty > 0)
                SelectDifficulty(selectedDifficulty);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        public void SetDifficultyData(
            IReadOnlyList<DungeonDifficultyDisplayData> difficulties,
            long currentPower)
        {
            if (difficultyRows == null)
                return;

            hasDifficultyData = true;
            selectedStageId = default;
            stageIdsByNumber.Clear();
            int firstUnlockedStage = 0;
            for (int i = 0; i < difficultyRows.Length; i++)
            {
                DungeonDifficultyRowView row = difficultyRows[i];
                bool hasData = difficulties != null && i < difficulties.Count;
                if (row == null)
                    continue;

                row.gameObject.SetActive(true);
                if (!hasData)
                {
                    row.ConfigureUnavailable(i + 1);
                    continue;
                }

                DungeonDifficultyDisplayData data = difficulties[i];
                int stageNumber = data.StageNumber > 0
                    ? data.StageNumber
                    : i + 1;
                if (data.HasStage)
                    stageIdsByNumber[stageNumber] = data.stageId;
                row.Configure(
                    stageNumber,
                    data.isUnlocked,
                    data.recommendedPower,
                    currentPower,
                    SelectDifficulty);
                if (firstUnlockedStage == 0 && data.isUnlocked)
                    firstUnlockedStage = stageNumber;
            }

            selectedDifficulty = firstUnlockedStage;
            if (selectedDifficulty > 0)
                SelectDifficulty(selectedDifficulty);
            else if (selectedDifficultyLabel != null)
                selectedDifficultyLabel.text = "선택 가능한 난이도 없음";

            if (enterButton != null)
                enterButton.interactable = selectedStageId != default;
        }

        public void SetInfoItems(
            IReadOnlyList<Sprite> clearRewards,
            IReadOnlyList<Sprite> monsters)
        {
            if (clearRewardCarousel != null)
                clearRewardCarousel.SetItems(clearRewards);
            if (monsterCarousel != null)
                monsterCarousel.SetItems(monsters);
        }

        private void SelectDifficulty(int stage)
        {
            selectedDifficulty = stage;
            selectedStageId = stageIdsByNumber.TryGetValue(stage, out eStage stageId)
                ? stageId
                : default;
            if (enterButton != null)
                enterButton.interactable = selectedStageId != default;
            if (selectedDifficultyLabel != null)
                selectedDifficultyLabel.text = $"선택 난이도  {selectedDifficulty}단계";

            if (difficultyRows == null)
                return;

            foreach (DungeonDifficultyRowView row in difficultyRows)
            {
                if (row != null)
                    row.SetSelected(row.Stage == selectedDifficulty);
            }
        }

        private void HandleEnterClicked()
        {
            if (selectedStageId == default)
                return;

            StageManager stageManager = StageManager.Instance;
            if (stageManager == null ||
                !stageManager.TryEnterDungeon(selectedStageId))
            {
                return;
            }

            GameObject panel = transform.parent != null
                ? transform.parent.gameObject
                : null;
            Hide();

            if (UIManager.Instance != null)
                UIManager.Instance.PopPanel();
            else if (panel != null)
                panel.SetActive(false);
        }
    }
}
