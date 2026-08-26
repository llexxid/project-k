using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomIdle.UGUI
{
    public sealed class DungeonDifficultyRowView : MonoBehaviour
    {
        // 러스틱 팔레트 (UGUI 리텍스처 — 다크 우드/청동 골드, UguiTheme 언어와 정렬)
        private static readonly Color NormalColor = new Color(0.16f, 0.12f, 0.09f, 0.95f);      // 다크 우드
        private static readonly Color SelectedColor = new Color(0.62f, 0.45f, 0.18f, 1f);       // 청동 골드 하이라이트
        private static readonly Color LockedColor = new Color(0.09f, 0.07f, 0.05f, 0.92f);      // 더 깊은 우드 (잠금)
        private static readonly Color PowerNormalColor = new Color(0.90f, 0.85f, 0.75f, 1f);    // 양피지 텍스트
        private static readonly Color PowerWarningColor = new Color(1f, 0.32f, 0.30f, 1f);      // 전투력 부족 경고

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
