using Reincarnation;
using Scripts.Core;
using UnityEngine;

namespace KingdomIdle.UGUI
{
    /// <summary>기존 ReincarnationService를 프리팹 팝업에 연결한다.</summary>
    public static class ReincarnationPopupController
    {
        private const string GainColor = "#5DE66C";
        private static ReincarnationPopupView view;

        public static bool IsOpen =>
            view != null && view.gameObject.activeSelf;

        public static void Show()
        {
            if (!EnsureBuilt())
                return;

            RefreshPreview();
            view.gameObject.SetActive(true);
            view.transform.SetAsLastSibling();
            if (view.panel != null)
                UITween.PopIn(view.panel);
        }

        public static void Hide()
        {
            if (view != null)
                view.gameObject.SetActive(false);
        }

        private static bool EnsureBuilt()
        {
            if (view != null)
                return true;

            UIManager host = UIManager.Instance;
            GameObject prefab =
                host != null && host.Catalog != null
                    ? host.Catalog.popupReincarnation
                    : null;
            if (host == null || prefab == null)
            {
                Debug.LogWarning(
                    "[ReincarnationPopup] 카탈로그의 팝업 프리팹이 없습니다.");
                return false;
            }

            GameObject instance = Object.Instantiate(
                prefab,
                host.LayerPopups,
                false);
            Stretch(instance.transform as RectTransform);
            view = instance.GetComponent<ReincarnationPopupView>();
            if (view == null)
            {
                Debug.LogError(
                    "[ReincarnationPopup] ReincarnationPopupView가 없습니다.");
                Object.Destroy(instance);
                return false;
            }

            view.backdropButton.onClick.AddListener(Hide);
            view.cancelButton.onClick.AddListener(Hide);
            view.confirmButton.onClick.AddListener(Confirm);
            view.gameObject.SetActive(false);
            return true;
        }

        private static void RefreshPreview()
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
            if (!preview.CanReincarnate)
            {
                SetUnavailable(GetFailureMessage(preview.FailureReason));
                return;
            }

            ReincarnationState current = service.CurrentState;
            long countGain = preview.NextState.Count - current.Count;
            view.statusLabel.text = "환생 가능";
            view.statusLabel.color = UguiTheme.SuccessGreen;
            view.infoLabel.text =
                $"레벨: {current.Level:N0} → " +
                $"<color={GainColor}>{preview.NextState.Level:N0} " +
                $"(+{preview.LevelGain:N0})</color>\n" +
                $"환생 횟수: {current.Count:N0} → " +
                $"<color={GainColor}>{preview.NextState.Count:N0} " +
                $"(+{countGain:N0})</color>";
            view.confirmButton.interactable = true;
        }

        private static void Confirm()
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
                Hide();
                UIManager.Instance?.ShowToast("환생했습니다.");
                return;
            }

            SetUnavailable($"환생 처리에 실패했습니다. ({result})");
        }

        private static void SetUnavailable(string reason)
        {
            view.statusLabel.text = "환생 불가";
            view.statusLabel.color = UguiTheme.DangerRed;
            view.infoLabel.text = reason;
            view.confirmButton.interactable = false;
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
