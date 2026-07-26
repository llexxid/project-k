using Reincarnation;
using Scripts.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomIdle.UGUI
{
    [RequireComponent(typeof(Button))]
    public sealed class ReincarnationButton : MonoBehaviour
    {
        private Button openButton;
        private GameObject popup;
        private TMP_Text statusLabel;
        private TMP_Text infoLabel;
        private Button confirmButton;

        private void Awake()
        {
            openButton = GetComponent<Button>();
            openButton.onClick.AddListener(Open);
        }

        private void OnDestroy()
        {
            if (openButton != null)
                openButton.onClick.RemoveListener(Open);
            Close();
        }

        private void Open()
        {
            Close();

            Transform parent = UIManager.Instance != null
                ? UIManager.Instance.LayerPopups
                : transform.root;

            popup = UguiRuntimeFactory.Container(
                parent,
                "Popup_Reincarnation").gameObject;
            UguiRuntimeFactory.Stretch((RectTransform)popup.transform);

            Image backdrop = UguiRuntimeFactory.Box(
                popup.transform,
                "Backdrop",
                new Color(0f, 0f, 0f, 0.62f),
                rounded: false,
                raycastTarget: true);
            UguiRuntimeFactory.Stretch(backdrop.rectTransform);
            backdrop.gameObject.AddComponent<Button>()
                .onClick.AddListener(Close);

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
            windowRect.sizeDelta = new Vector2(800f, 540f);

            Image titleBar = UguiRuntimeFactory.Box(
                window.transform,
                "TitleBar",
                new Color(0.30f, 0.16f, 0.09f, 1f),
                rounded: true);
            SetRect(titleBar.rectTransform, 44f, 42f, -44f, -148f);

            TMP_Text title = UguiRuntimeFactory.Label(
                titleBar.transform,
                "환생",
                48f,
                UguiTheme.AccentGoldStrong,
                TextAlignmentOptions.Center,
                bold: true);
            UguiRuntimeFactory.Stretch(title.rectTransform);

            statusLabel = UguiRuntimeFactory.Label(
                window.transform,
                string.Empty,
                36f,
                UguiTheme.TextPrimary,
                TextAlignmentOptions.Center,
                bold: true);
            SetRect(statusLabel.rectTransform, 54f, 170f, -54f, -224f);

            infoLabel = UguiRuntimeFactory.Label(
                window.transform,
                string.Empty,
                30f,
                UguiTheme.TextSecondary,
                TextAlignmentOptions.Center,
                wrap: true);
            infoLabel.richText = true;
            SetRect(infoLabel.rectTransform, 60f, 236f, -60f, -372f);

            RectTransform buttons = UguiRuntimeFactory.Container(
                window.transform,
                "Buttons");
            SetRect(buttons, 110f, 410f, -110f, -492f);
            UguiRuntimeFactory.HorizontalLayout(
                buttons.gameObject,
                spacing: 22f,
                align: TextAnchor.MiddleCenter,
                childControlWidth: true,
                expandWidth: true);

            Button cancelButton = UguiRuntimeFactory.TextButton(
                buttons,
                "취소",
                30f,
                UguiTheme.DisabledGrey,
                Close);
            confirmButton = UguiRuntimeFactory.TextButton(
                buttons,
                "환생하기",
                30f,
                UguiTheme.SuccessGreen,
                Confirm);
            ConfigureButton(cancelButton);
            ConfigureButton(confirmButton);

            RefreshPreview();
        }

        private void RefreshPreview()
        {
            ReincarnationService service =
                GameManager.Instance != null
                    ? GameManager.Instance.Reincarnation
                    : null;

            if (service == null)
            {
                SetUnavailable("환생 정보를 불러올 수 없습니다.");
                return;
            }

            ReincarnationPreview preview = service.GetPreview();
            ReincarnationState current = service.CurrentState;
            if (!preview.CanReincarnate)
            {
                SetUnavailable(GetFailureMessage(preview.FailureReason));
                return;
            }

            long countGain = preview.NextState.Count - current.Count;
            statusLabel.text = "환생 가능";
            statusLabel.color = UguiTheme.SuccessGreen;
            infoLabel.text =
                $"레벨: {current.Level:N0} → " +
                $"<color=#5DE66C>{preview.NextState.Level:N0} " +
                $"(+{preview.LevelGain:N0})</color>\n" +
                $"환생 횟수: {current.Count:N0} → " +
                $"<color=#5DE66C>{preview.NextState.Count:N0} " +
                $"(+{countGain:N0})</color>";
            confirmButton.interactable = true;
        }

        private void SetUnavailable(string reason)
        {
            statusLabel.text = "환생 불가";
            statusLabel.color = UguiTheme.DangerRed;
            infoLabel.text = reason;
            confirmButton.interactable = false;
        }

        private void Confirm()
        {
            ReincarnationService service =
                GameManager.Instance != null
                    ? GameManager.Instance.Reincarnation
                    : null;
            if (service == null)
                return;

            ReincarnationExecutionResult result =
                service.TryReincarnate();
            if (result == ReincarnationExecutionResult.None)
            {
                Close();
                UIManager.Instance?.ShowToast("환생했습니다.");
                return;
            }

            SetUnavailable($"환생 처리에 실패했습니다. ({result})");
        }

        private void Close()
        {
            if (popup != null)
                Destroy(popup);
            popup = null;
        }

        private static string GetFailureMessage(
            eReincarnationFailureReason reason)
        {
            switch (reason)
            {
                case eReincarnationFailureReason.NotMainStage:
                    return "메인 스테이지에서만 환생할 수 있습니다.";
                case eReincarnationFailureReason.StageRequirementNotMet:
                    return "메인 스테이지 2 이상부터 환생할 수 있습니다.";
                case eReincarnationFailureReason.StateIsNotRunning:
                    return "현재 스테이지가 진행 중일 때만 환생할 수 있습니다.";
                case eReincarnationFailureReason.NumericOverflow:
                    return "환생 수치를 계산할 수 없습니다.";
                default:
                    return "현재는 환생할 수 없습니다.";
            }
        }

        private static void ConfigureButton(Button button)
        {
            LayoutElement layout =
                UguiRuntimeFactory.Flexible(button, 1f);
            layout.minHeight = 82f;
            layout.preferredHeight = 82f;
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
