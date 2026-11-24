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
        private static float nextPreviewRefresh;

        public static bool IsOpen =>
            view != null && view.gameObject.activeSelf;

        public static void Show()
        {
            if (!EnsureBuilt())
                return;

            RefreshPreview();
            view.gameObject.SetActive(true);
            view.transform.SetAsLastSibling();
            UIManager.Instance.FrameTick -= RefreshWhileOpen;
            UIManager.Instance.FrameTick += RefreshWhileOpen;
            nextPreviewRefresh = Time.unscaledTime + .25f;
            if (view.panel != null)
                UITween.PopIn(view.panel);
        }

        public static void Hide()
        {
            if (UIManager.Instance != null) UIManager.Instance.FrameTick -= RefreshWhileOpen;
            if (view != null)
                view.gameObject.SetActive(false);
        }

        private static void RefreshWhileOpen()
        {
            if (!IsOpen) { if (UIManager.Instance != null) UIManager.Instance.FrameTick -= RefreshWhileOpen; return; }
            if (Time.unscaledTime < nextPreviewRefresh) return;
            nextPreviewRefresh = Time.unscaledTime + .25f;
            RefreshPreview();
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
                host.LayerOverlays,
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
            ModalBackHandler.Bind(instance, Hide);
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
                SetUnavailable(GetFailureMessage(preview.FailureReason),
                    preview.FailureReason == eReincarnationFailureReason.RequestDuplication ? "환생 요청 중" : "환생 불가");
                return;
            }

            ReincarnationState current = service.CurrentState;
            SetText(view.statusLabel, "환생 가능");
            view.statusLabel.color = UguiTheme.SuccessGreen;
            SetText(view.infoLabel,
                $"환생 레벨: {current.Level:N0} → " +
                $"<color={GainColor}>{preview.NextState.Level:N0} " +
                $"(+{preview.LevelGain:N0})</color>\n\n" + ResetSummary);
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
            if (result == ReincarnationExecutionResult.Pending)
            {
                // 서버 응답까지 버튼을 잠근다. 결과는 OnReincarnationCompleted로 온다.
                service.OnReincarnationCompleted -= OnCompleted;
                service.OnReincarnationCompleted += OnCompleted;
                SetUnavailable("서버에 환생을 요청했습니다. 잠시만 기다려 주세요.", "환생 요청 중");
                return;
            }

            RefreshPreview();
            if (result == ReincarnationExecutionResult.StorageUnavailable)
                UIManager.Instance?.ShowToast(service.LastFailure?.ErrorMessage ?? "서버 로그인이 필요합니다.");
            else if (result == ReincarnationExecutionResult.AlreadyProcessing)
                UIManager.Instance?.ShowToast("환생 요청을 처리 중입니다.");
        }

        /// <summary>서버 응답 처리 결과. 성공이면 팝업을 닫고, 거절·실패면 미리보기를 다시 그린다. 문구는 서비스가 만든다.</summary>
        private static void OnCompleted(bool ok, string message)
        {
            ReincarnationService service =
                GameManager.Instance != null
                    ? GameManager.Instance.Reincarnation
                    : null;
            if (service != null)
                service.OnReincarnationCompleted -= OnCompleted;
            if (ok)
                Hide();
            else if (IsOpen)
                RefreshPreview();
            UIManager.Instance?.ShowToast(message);
        }

        private const string ResetSummary = "메인 1-1에서 다시 시작합니다.\n초기화 · 골드, 골드로 강화한 공격력과 체력\n유지 · 계정 성장, 전직, 장비와 강화 단계, 마탑, 루비 성장, 골드 외 재화, 최초 클리어 기록";

        private static void SetUnavailable(string reason, string status = "환생 불가")
        {
            SetText(view.statusLabel, status);
            view.statusLabel.color = UguiTheme.Parchment;
            SetText(view.infoLabel, reason + "\n\n" + ResetSummary);
            view.confirmButton.interactable = false;
        }

        private static void SetText(TMPro.TMP_Text label, string text)
        { if (label != null && label.text != text) label.text = text; }

        private static string GetFailureMessage(
            eReincarnationFailureReason reason)
        {
            switch (reason)
            {
                case eReincarnationFailureReason.NotMainStage:
                    return "메인 일반 웨이브에서 환생할 수 있습니다.\n보스전·던전 종료 후 다시 확인하세요.";
                case eReincarnationFailureReason.StageRequirementNotMet:
                    return "이번 도전에서 메인 보스를 한 번 처치하면 환생할 수 있습니다.";
                case eReincarnationFailureReason.StateIsNotRunning:
                    return "현재 스테이지가 진행 중일 때만 환생할 수 있습니다.";
                case eReincarnationFailureReason.MaximumLevel: return "환생 최대 레벨 300입니다.";
                case eReincarnationFailureReason.Cooldown:
                    var s = KingdomIdle.Balance.LocalProgression.State;
                    long remaining = System.Math.Max(0,600 - (KingdomIdle.Balance.LocalProgression.UtcNow - s.LastReincarnationUtc));
                    return $"다음 환생까지 {remaining / 60}분 {remaining % 60:00}초";
                case eReincarnationFailureReason.DailyLimit: return "오늘 환생 3회를 모두 사용했습니다. KST 자정에 초기화됩니다.";
                case eReincarnationFailureReason.RequestDuplication: return "서버에 환생을 요청했습니다. 잠시만 기다려 주세요.";
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
