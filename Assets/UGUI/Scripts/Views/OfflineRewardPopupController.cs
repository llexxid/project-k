using System;
using KingdomIdle.OfflineRewards;
using UnityEngine;

namespace KingdomIdle.UGUI
{
    /// <summary>확정된 오프라인 사냥 결과를 LayerPopups의 전용 프리팹에 표시한다.</summary>
    public static class OfflineRewardPopupController
    {
        private static OfflineRewardPopupView _view;
        public static bool IsOpen =>
            _view != null && _view.gameObject.activeSelf;

        public static void Show(OfflineRewardClaimResult result)
        {
            if (result == null || result.Plan == null || !EnsureBuilt())
                return;

            OfflineRewardPlan plan = result.Plan;
            _view.durationLabel.text = FormatDuration(plan);
            _view.killCountLabel.text =
                $"예상 처치  {NumberNotation.Format(plan.estimatedKillCount)}마리";

            _view.goldRow.gameObject.SetActive(true);
            _view.goldValueLabel.text = $"+{NumberNotation.Format(result.GoldGained)}";

            bool hasAncientCoin = result.AncientCoinGained > 0L;
            _view.ancientCoinRow.gameObject.SetActive(hasAncientCoin);
            if (hasAncientCoin)
            {
                _view.ancientCoinValueLabel.text =
                    $"+{NumberNotation.Format(result.AncientCoinGained)}";
            }

            _view.progressLabel.text =
                $"성장 결과  Lv.{result.CurrentLevel:N0} · EXP {NumberNotation.Format(result.CurrentExp)}\n" +
                $"획득 EXP +{NumberNotation.Format(result.ExperienceGained)} · 전투 처치 기록과 별도";
            _view.gameObject.SetActive(true);
            _view.transform.SetAsLastSibling();
            if (_view.panel != null)
                UITween.PopIn(_view.panel);
        }

        public static void Hide()
        {
            if (_view != null)
                _view.gameObject.SetActive(false);
        }

        private static bool EnsureBuilt()
        {
            if (_view != null)
                return true;

            UIManager host = UIManager.Instance;
            GameObject prefab =
                host != null && host.Catalog != null
                    ? host.Catalog.popupOfflineReward
                    : null;
            if (host == null || prefab == null)
            {
                Debug.LogWarning(
                    "[OfflineRewardPopup] 카탈로그의 팝업 프리팹이 없습니다.");
                return false;
            }

            GameObject instance = UnityEngine.Object.Instantiate(
                prefab,
                host.LayerPopups,
                false);
            Stretch(instance.transform as RectTransform);
            _view = instance.GetComponent<OfflineRewardPopupView>();
            if (_view == null)
            {
                Debug.LogError(
                    "[OfflineRewardPopup] OfflineRewardPopupView가 없습니다.");
                UnityEngine.Object.Destroy(instance);
                return false;
            }

            _view.confirmButton.onClick.AddListener(Hide);
            ModalBackHandler.Bind(instance, Hide);
            _view.backdropButton.onClick.AddListener(Hide);
            _view.gameObject.SetActive(false);
            return true;
        }

        private static string FormatDuration(OfflineRewardPlan plan)
        {
            string applied = FormatSeconds(plan.appliedOfflineSeconds);
            if (plan.actualOfflineSeconds <= plan.appliedOfflineSeconds)
                return $"방치 시간  {applied}";

            return $"방치 시간  {applied} 적용 · 최대 8시간";
        }

        private static string FormatSeconds(long seconds)
        {
            TimeSpan duration = TimeSpan.FromSeconds(Math.Max(0L, seconds));
            if (duration.TotalHours >= 1d)
                return $"{(int)duration.TotalHours}시간 {duration.Minutes}분";
            if (duration.TotalMinutes >= 1d)
                return $"{duration.Minutes}분 {duration.Seconds}초";
            return $"{duration.Seconds}초";
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
