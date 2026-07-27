using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomIdle.UGUI
{
    public sealed class DungeonDifficultyRowView : MonoBehaviour
    {
        private static readonly Color NormalColor = new Color(0.16f, 0.18f, 0.22f, 1f);
        private static readonly Color SelectedColor = new Color(0.17f, 0.38f, 0.48f, 1f);
        private static readonly Color LockedColor = new Color(0.13f, 0.09f, 0.10f, 0.92f);
        private static readonly Color PowerNormalColor = new Color(0.74f, 0.78f, 0.82f, 1f);
        private static readonly Color PowerWarningColor = new Color(1f, 0.28f, 0.32f, 1f);

        [SerializeField] private Button button;
        [SerializeField] private Image background;
        [SerializeField] private TMP_Text stageLabel;
        [SerializeField] private TMP_Text powerLabel;
        [SerializeField] private GameObject lockIndicator;

        private int stage;
        private bool unlocked;
        private Action<int> selected;

        public int Stage => stage;

        public void Configure(
            int stageNumber,
            bool isUnlocked,
            long recommendedPower,
            long currentPower,
            Action<int> onSelected)
        {
            stage = stageNumber;
            unlocked = isUnlocked;
            selected = onSelected;

            if (stageLabel != null)
                stageLabel.text = $"{stageNumber}단계";
            if (powerLabel != null)
            {
                powerLabel.text = $"권장 전투력  {recommendedPower:N0}";
                powerLabel.color = currentPower < recommendedPower
                    ? PowerWarningColor
                    : PowerNormalColor;
            }
            if (lockIndicator != null)
                lockIndicator.SetActive(!isUnlocked);
            if (button != null)
            {
                button.interactable = isUnlocked;
                button.onClick.RemoveListener(HandleClick);
                button.onClick.AddListener(HandleClick);
            }

            SetSelected(false);
        }

        public void ConfigureUnavailable(int stageNumber)
        {
            stage = stageNumber;
            unlocked = false;
            selected = null;

            if (stageLabel != null)
                stageLabel.text = $"{stageNumber}단계";
            if (powerLabel != null)
            {
                powerLabel.text = "정보 없음";
                powerLabel.color = PowerNormalColor;
            }
            if (lockIndicator != null)
                lockIndicator.SetActive(true);
            if (button != null)
            {
                button.interactable = false;
                button.onClick.RemoveListener(HandleClick);
            }

            SetSelected(false);
        }

        public void SetSelected(bool value)
        {
            if (background != null)
                background.color = !unlocked ? LockedColor : value ? SelectedColor : NormalColor;
        }

        private void HandleClick()
        {
            if (unlocked)
                selected?.Invoke(stage);
        }

        private void OnDestroy()
        {
            if (button != null)
                button.onClick.RemoveListener(HandleClick);
        }
    }
}
