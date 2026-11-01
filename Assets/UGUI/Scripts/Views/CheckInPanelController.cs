using System;
using System.Linq;
using KingdomIdle.Balance;
using KingdomIdle.CheckIn;
using KingdomIdle.Rewards;
using Scripts.Core;
using UnityEngine;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// 서버 월별 출석판을 그린다. 칸 표시는 서버 보상표 사본, 열린 칸·받은 칸은 서버 출석판(CheckInService)이다.
    /// 받을 수 있는 칸을 누르면 받을 수 있는 칸 전체를 서버에 요청하고, 완료 이벤트에서 서버 지급 결과로 팝업을 띄운다.
    /// </summary>
    [RequireComponent(typeof(CheckInPanelView))]
    public sealed class CheckInPanelController : MonoBehaviour
    {
        private CheckInPanelView _view;
        private CheckInSnapshot _snapshot;
        private CheckInService _subscribed;
        private long _revision = -1, _account = -1, _remaining = -1;
        private float _nextRefresh;
        private CheckInService Service => GameManager.Instance != null ? GameManager.Instance.CheckIn : null;

        private void Awake() => _view = GetComponent<CheckInPanelView>();
        private void OnEnable() { _revision = _remaining = -1; _nextRefresh = 0; }
        private void OnDisable() { Subscribe(null); }
        private void Update()
        {
            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + .2f;
            Refresh();
        }
        private void Subscribe(CheckInService service)
        {
            if (_subscribed == service) return;
            if (_subscribed != null) _subscribed.ClaimCompleted -= OnClaimCompleted;
            _subscribed = service;
            if (_subscribed != null) _subscribed.ClaimCompleted += OnClaimCompleted;
        }
        private void Refresh()
        {
            var service = Service;
            Subscribe(service);
            if (service == null || !LocalProgression.IsReady)
            { ShowUnavailable("계정 정보를 준비하고 있습니다."); return; }
            if (_revision != CheckInService.Revision || _account != LocalProgression.AccountGeneration)
            {
                _snapshot = service.GetSnapshot();
                if (_snapshot == null) { ShowUnavailable("출석 정보를 갱신하고 있습니다."); return; }
                _view.grid.gameObject.SetActive(true);
                _revision = CheckInService.Revision; _account = LocalProgression.AccountGeneration;
                _remaining = -1;
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
            if (service.IsBusy)
            {
                const string busy = "출석 보상을 받는 중입니다.";
                if (_view.countdown.text != busy) { _view.countdown.text = busy; _remaining = -1; }
                return;
            }
            long remaining = Math.Max(0, _snapshot.NextRefreshUtc - CheckInService.ServerNow);
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
            var service = Service;
            if (_snapshot == null || service == null || service.IsBusy) return;
            if (!_snapshot.Available.Contains(index))
            {
                // 받을 수 없는 칸은 그날 보상을 보여 준다. 보상이 여럿이면 목록으로.
                var rewards = _snapshot.DayRewards[index];
                if (rewards.Count == 1) RewardPopupView.ShowItem(transform, rewards[0]);
                else if (rewards.Count > 1) RewardPopupView.ShowRewards(transform, rewards, $"{index + 1}일차 보상");
                return;
            }
            var result = service.TryClaimAvailable(_snapshot);
            _revision = -1;
            Refresh();
            RewardPresentation.Report(result);
        }
        private void OnClaimCompleted(CheckInClaimInfo info)
        {
            if (this == null || !isActiveAndEnabled) return;
            _revision = -1;
            Refresh();
            if (info.Ok) RewardPopupView.ShowRewards(transform, info.Rewards);
            else UIManager.Instance?.ShowToast(info.Message);
        }
    }
}
