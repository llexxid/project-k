using System;
using System.Linq;
using KingdomIdle.Balance;
using KingdomIdle.CheckIn;
using KingdomIdle.Rewards;
using Scripts.Core;
using UnityEngine;

namespace KingdomIdle.UGUI
{
    [RequireComponent(typeof(CheckInPanelView))]
    public sealed class CheckInPanelController : MonoBehaviour
    {
        private CheckInPanelView _view;
        private CheckInSnapshot _snapshot;
        private long _revision = -1, _account = -1, _remaining = -1;
        private float _nextRefresh;
        private bool _tableValid;
        private CheckInService Service => GameManager.Instance != null ? GameManager.Instance.CheckIn : null;

        private void Awake() => _view = GetComponent<CheckInPanelView>();
        private void OnEnable() { _revision = _remaining = -1; _nextRefresh = 0; }
        private void Update()
        {
            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + .2f;
            Refresh();
        }
        private void Refresh()
        {
            var service = Service;
            if (service == null || !LocalProgression.TryGetCommittedState(out var state))
            { ShowUnavailable("계정 정보를 준비하고 있습니다."); return; }
            bool tableValid = service.IsRewardTableValid;
            if (_revision != state.RewardsRevision || _account != LocalProgression.AccountGeneration || _tableValid != tableValid)
            {
                _snapshot = service.GetSnapshot();
                if (_snapshot == null) { ShowUnavailable(tableValid ? "출석 정보를 갱신하고 있습니다." : "출석 보상표 오류로 새 출석을 기록할 수 없습니다."); return; }
                _view.grid.gameObject.SetActive(true);
                _revision = state.RewardsRevision; _account = LocalProgression.AccountGeneration;
                _tableValid = tableValid; _remaining = -1;
                _view.monthLabel.text = _snapshot.Month.Substring(5, 2) + "월 출석부";
                _view.countLabel.text = $"누적 {_snapshot.Count} / {_snapshot.Rewards.Count}일";
                for (int i = 0; i < 28; i++)
                {
                    int index = i;
                    _view.slots[i].Bind(_snapshot.Rewards[i], () => Click(index), i + 1,
                        _snapshot.Available.Contains(i), _snapshot.Claimed.Contains(i));
                }
            }
            if (_snapshot == null) return;
            if (!tableValid)
            {
                const string message = "출석 보상표 오류로 새 출석을 기록할 수 없습니다.";
                if (_view.countdown.text != message) _view.countdown.text = message;
                return;
            }
            long remaining = Math.Max(0, _snapshot.NextRefreshUtc - RewardGrants.Now);
            if (remaining == _remaining) return;
            _remaining = remaining;
            _view.countdown.text = FormatCountdown(remaining);
        }
        private void ShowUnavailable(string message)
        {
            _snapshot = null; _revision = _remaining = -1;
            _view.grid.gameObject.SetActive(false);
            _view.monthLabel.text = "";
            _view.countLabel.text = "";
            _view.countdown.text = message;
            foreach (var popup in GetComponentsInChildren<RewardPopupView>()) popup.Close();
        }
        public static string FormatCountdown(long seconds)
        {
            seconds = Math.Max(0, seconds);
            return $"다음 보상까지 {seconds / 3600:00} : {seconds / 60 % 60:00} : {seconds % 60:00}";
        }
        private void Click(int index)
        {
            if (_snapshot == null || Service == null) return;
            if (!_snapshot.Available.Contains(index)) { RewardPopupView.ShowItem(transform, _snapshot.Rewards[index]); return; }
            var result = Service.TryClaimAvailable(_snapshot);
            Refresh();
            if (result.Succeeded) RewardPopupView.ShowRewards(transform, result.Rewards);
            else RewardPresentation.Report(result);
        }
    }
}
