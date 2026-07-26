using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomIdle.UGUI
{
    public sealed class DungeonClearPopupView : MonoBehaviour
    {
        private TMP_Text titleLabel;
        private Button exitButton;
        private Button nextButton;
        private Button retryButton;

        private void Awake()
        {
            BuildUi();
        }

        public void Bind(
            string title,
            bool hasNextStage,
            Action onExit,
            Action onNext,
            Action onRetry)
        {
            titleLabel.text = title;
            nextButton.interactable = hasNextStage;

            exitButton.onClick.AddListener(() => onExit?.Invoke());
            nextButton.onClick.AddListener(() => onNext?.Invoke());
            retryButton.onClick.AddListener(() => onRetry?.Invoke());
        }

        private void BuildUi()
        {
            Image backdrop = UguiRuntimeFactory.Box(
                transform,
                "Backdrop",
                new Color(0f, 0f, 0f, 0.62f),
                rounded: false,
                raycastTarget: true);
            UguiRuntimeFactory.Stretch(backdrop.rectTransform);

            Image window = UguiRuntimeFactory.PixelWindow(
                backdrop.transform,
                "Window",
                borderPx: 24f,
                raycastTarget: true,
                baseColor: new Color(0.08f, 0.09f, 0.14f, 1f),
                frameTint: UguiRuntimeFactory.FrameGold);
            RectTransform windowRect = window.rectTransform;
            windowRect.anchorMin = new Vector2(0.5f, 0.5f);
            windowRect.anchorMax = new Vector2(0.5f, 0.5f);
            windowRect.pivot = new Vector2(0.5f, 0.5f);
            windowRect.anchoredPosition = Vector2.zero;
            windowRect.sizeDelta = new Vector2(800f, 430f);

            Image titleBar = UguiRuntimeFactory.Box(
                window.transform,
                "TitleBar",
                new Color(0.30f, 0.16f, 0.09f, 1f),
                rounded: true);
            SetRect(titleBar.rectTransform, 44f, 44f, -44f, -172f);

            titleLabel = UguiRuntimeFactory.Label(
                titleBar.transform,
                "던전 클리어!",
                48f,
                UguiTheme.AccentGoldStrong,
                TextAlignmentOptions.Center,
                bold: true);
            UguiRuntimeFactory.Stretch(titleLabel.rectTransform);

            TMP_Text guideLabel = UguiRuntimeFactory.Label(
                window.transform,
                "다음 행동을 선택하세요.",
                30f,
                UguiTheme.TextSecondary,
                TextAlignmentOptions.Center);
            SetRect(guideLabel.rectTransform, 54f, 184f, -54f, -250f);

            RectTransform buttons = UguiRuntimeFactory.Container(
                window.transform,
                "Buttons");
            SetRect(buttons, 50f, 286f, -50f, -370f);
            UguiRuntimeFactory.HorizontalLayout(
                buttons.gameObject,
                spacing: 18f,
                padding: new RectOffset(),
                align: TextAnchor.MiddleCenter,
                childControlWidth: true,
                expandWidth: true);

            exitButton = UguiRuntimeFactory.TextButton(
                buttons,
                "나가기",
                30f,
                UguiTheme.DisabledGrey,
                null);
            nextButton = UguiRuntimeFactory.TextButton(
                buttons,
                "다음 스테이지",
                30f,
                UguiTheme.AccentBlue,
                null);
            retryButton = UguiRuntimeFactory.TextButton(
                buttons,
                "다시하기",
                30f,
                UguiTheme.SuccessGreen,
                null);

            ConfigureButtonLayout(exitButton);
            ConfigureButtonLayout(nextButton);
            ConfigureButtonLayout(retryButton);
        }

        private static void ConfigureButtonLayout(Button button)
        {
            LayoutElement layout = UguiRuntimeFactory.Flexible(button, 1f);
            layout.minHeight = 84f;
            layout.preferredHeight = 84f;
        }

        private static void SetRect(
            RectTransform rect,
            float left,
            float top,
            float right,
            float bottom)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(right, -top);
        }
    }
}
