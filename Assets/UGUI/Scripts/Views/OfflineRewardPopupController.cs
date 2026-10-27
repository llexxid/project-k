using System;
using System.Collections.Generic;
using KingdomIdle.Ads;
using KingdomIdle.OfflineRewards;
using KingdomIdle.Rewards;
using UnityEngine;

namespace KingdomIdle.UGUI
{
    /// <summary>확정된 오프라인 사냥 결과를 LayerPopups의 전용 프리팹에 표시한다.</summary>
    public static class OfflineRewardPopupController
    {
        private static OfflineRewardPopupView _view;
        private static RewardChoice _choice;
        private static readonly Queue<OfflineRewardClaimResult> Waiting = new();
        private static RewardPopupView _resultView;
        private static bool ResultOpen => _resultView != null && _resultView.gameObject.activeSelf;
        private sealed class RewardChoice
        {
            public readonly OfflineRewardClaimResult Result;
            public bool Busy, Earned, Granted;
            public RewardChoice(OfflineRewardClaimResult result) { Result = result; }
        }
        public static bool IsOpen =>
            _view != null && _view.gameObject.activeSelf;

        public static void Show(OfflineRewardClaimResult result)
        {
            if (result == null || result.Plan == null || !EnsureBuilt())
                return;

            // A later app resume must not replace an earned bonus awaiting a save retry.
            if (_choice != null && OfflineRewardManager.IsCurrentAccount(_choice.Result) &&
                (IsOpen || ResultOpen || _choice.Busy || (_choice.Earned && !_choice.Granted)))
            {
                Waiting.Enqueue(result);
                if (!ResultOpen) { _view.gameObject.SetActive(true); RefreshButtons(); }
                return;
            }
            _choice = new RewardChoice(result);
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
            RefreshButtons();
            if (_view.panel != null)
                UITween.PopIn(_view.panel);
        }

        public static void Hide()
        {
            if (_view != null)
                _view.gameObject.SetActive(false);
            Waiting.Clear();
            if (_choice != null && !_choice.Busy && (!_choice.Earned || _choice.Granted)) _choice = null;
            var resultView = _resultView;
            _resultView = null;
            if (resultView != null) resultView.Close();
        }

        private static void WatchAd()
        {
            var choice = _choice;
            if (!IsOpen || choice == null || choice.Busy || choice.Earned) return;
            if (!CheckAccount(choice)) return;
            choice.Busy = true;
            RefreshButtons();
            // shortcut: use Google's sample unit until production ads and consent are configured.
            RewardedAdService.Show("ca-app-pub-3940256099942544/5224354917", (result, error) =>
            {
                choice.Busy = false;
                if (!CheckAccount(choice)) return;
                if (result == RewardedAdResult.Completed)
                {
                    choice.Earned = true;
                    FinishChoice(choice);
                }
                else if (ReferenceEquals(_choice, choice) && IsOpen)
                {
                    RefreshButtons();
                    if (result != RewardedAdResult.Skipped)
                        UIManager.Instance?.ShowToast("광고를 표시하지 못했습니다. 잠시 후 다시 시도해 주세요.");
                }
            });
        }

        private static void Collect()
        {
            if (IsOpen && _choice != null && !_choice.Busy) FinishChoice(_choice);
        }

        private static void FinishChoice(RewardChoice choice)
        {
            if (!CheckAccount(choice)) return;
            choice.Busy = true;
            if (ReferenceEquals(_choice, choice)) RefreshButtons();
            RewardStatus status = choice.Earned
                ? OfflineRewardManager.TryGrantAdBonus(choice.Result) : RewardStatus.Success;
            choice.Busy = false;
            if (status == RewardStatus.Success && choice.Earned) choice.Granted = true;
            if (!ReferenceEquals(_choice, choice) || !IsOpen) return;
            if (status == RewardStatus.Stale || status == RewardStatus.NotReady)
            {
                Hide();
                UIManager.Instance?.ShowToast("계정 정보가 변경되었습니다.");
                return;
            }
            if (status != RewardStatus.Success)
            {
                // shortcut: retain earned bonus in this session only; add durable recovery if required.
                RefreshButtons();
                UIManager.Instance?.ShowToast("보상 저장에 실패했습니다. 보상 수령을 눌러 다시 시도해 주세요.");
                return;
            }
            var host = UIManager.Instance;
            _resultView = host != null ? RewardPopupView.ShowOfflineRewards(host.LayerPopups, choice.Result, choice.Earned,
                    () => ShowNext(choice)) : null;
            if (_resultView != null)
            {
                _view.gameObject.SetActive(false);
            }
            else
            {
                RefreshButtons();
                UIManager.Instance?.ShowToast("보상 결과를 표시하지 못했습니다. 다시 눌러 주세요.");
            }
        }

        private static void ShowNext(RewardChoice choice)
        {
            if (!ReferenceEquals(_choice, choice)) return;
            _resultView = null;
            _choice = null;
            while (Waiting.Count > 0)
            {
                var result = Waiting.Dequeue();
                if (!OfflineRewardManager.IsCurrentAccount(result)) continue;
                Show(result);
                break;
            }
        }

        private static bool CheckAccount(RewardChoice choice)
        {
            if (OfflineRewardManager.IsCurrentAccount(choice.Result)) return true;
            if (ReferenceEquals(_choice, choice) && IsOpen)
            {
                Hide();
                UIManager.Instance?.ShowToast("계정 정보가 변경되었습니다.");
            }
            return false;
        }

        private static void RefreshButtons()
        {
            if (_view == null || _choice == null) return;
            _view.adButton.interactable = !_choice.Busy && !_choice.Earned;
            _view.confirmButton.interactable = !_choice.Busy;
            _view.adButtonLabel.text = _choice.Busy && !_choice.Earned ? "광고 불러오는 중…" : "광고보고 보상 2배";
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

            _view.confirmButton.onClick.AddListener(Collect);
            _view.adButton.onClick.AddListener(WatchAd);
            // Consume back without closing; Hide remains available for forced screen cleanup.
            ModalBackHandler.Bind(instance, () => { });
            _view.backdropButton.onClick.RemoveAllListeners();
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
