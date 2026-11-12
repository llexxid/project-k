using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomIdle.UGUI
{
    public sealed class DungeonDifficultyRowView : MonoBehaviour
    {
        // 청동 원정대: 선택은 청색, 정보는 밝은 면과 어두운 글자로 구분한다.
        private static readonly Color NormalColor = UguiTheme.InfoSurface;
        private static readonly Color SelectedColor = UguiTheme.RoyalBlue;
        private static readonly Color LockedColor = new Color(0.84f, 0.79f, 0.69f, 1f);
        private static readonly Color PowerNormalColor = UguiTheme.InfoMuted;
        private static readonly Color PowerWarningColor = new Color(0.68f, 0.17f, 0.14f, 1f);
        private static readonly Color SelectedPowerWarningColor = new Color(1f, 0.77f, 0.68f, 1f);

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
                powerLabel.text = recommendedPower > 0 ? $"권장 전투력  {NumberNotation.Format(recommendedPower)}" : isUnlocked ? "입장 가능" : "이전 단계를 클리어하세요";
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
            bool selectedRow = unlocked && value;
            if (background != null)
                background.color = !unlocked ? LockedColor : selectedRow ? SelectedColor : NormalColor;
            if (stageLabel != null)
                stageLabel.color = selectedRow ? UguiTheme.Parchment : UguiTheme.InfoInk;
            if (powerLabel != null)
            {
                bool warning = powerLabel.color == PowerWarningColor || powerLabel.color == SelectedPowerWarningColor;
                powerLabel.color = warning
                    ? (selectedRow ? SelectedPowerWarningColor : PowerWarningColor)
                    : (selectedRow ? UguiTheme.Parchment : PowerNormalColor);
            }
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
