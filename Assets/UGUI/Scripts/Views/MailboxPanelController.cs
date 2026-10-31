using System;
using System.Linq;
using KingdomIdle.Mailbox;
using Scripts.Core;
using UnityEngine;

namespace KingdomIdle.UGUI
{
    [RequireComponent(typeof(MailboxPanelView))]
    public sealed class MailboxPanelController : MonoBehaviour
    {
        private MailboxPanelView _view;
        private VirtualizedMailList _list;
        private MailboxService _service;
        private bool _subscribed;
        private float _nextTick;
        private long _lastExpiryRefresh;

        private void Awake()
        {
            _view = GetComponent<MailboxPanelView>();
            // Disconnect before the closing animation, and prevent Update from reconnecting.
            _view.OnClosed = () => enabled = false;
            _list = GetComponent<VirtualizedMailList>() ?? gameObject.AddComponent<VirtualizedMailList>();
            _list.Initialize(_view.content, UIManager.Instance.Catalog.itemMailRow, transform, id => _service?.Claim(id));
            _view.claimAll.onClick.AddListener(ReceiveOrRetry);
            _view.deleteClaimed.interactable = false;
            _view.claimedLabel.text = "수령 시 자동 삭제";
        }
        private void OnEnable() { Connect(); }
        private void Connect()
        {
            if (_subscribed) return;
            _service ??= GameManager.Instance != null ? GameManager.Instance.Mailbox : null;
            if (_service == null) { _view.emptyLabel.text = "계정 정보를 준비하고 있습니다."; _view.claimAll.interactable = false; return; }
            _service.Changed += Render;
            _service.ClaimCompleted += OnClaimCompleted;
            _subscribed = true;
            _lastExpiryRefresh = 0;
            _service.Open();
        }
        private void OnDisable()
        {
            if (!_subscribed) return;
            _service.Changed -= Render;
            _service.ClaimCompleted -= OnClaimCompleted;
            _subscribed = false;
            _service.Close();
        }
        internal void UseTestService(MailboxService service) { _service = service; }
        private void Update()
        {
            if (Time.unscaledTime < _nextTick) return;
            _nextTick = Time.unscaledTime + .5f;
            Connect();
            if (!_subscribed) return;
            _service.CheckAccount();
            long now = _service.ServerNow;
            _list.UpdateTime(now, _service.CanClaim);
            if (!_service.HasSnapshot || _service.IsBusy || _service.Messages.Count == 0) return;
            long expires = _service.Messages.Min(m => m.ExpiresUtc);
            if (expires <= now && expires != _lastExpiryRefresh)
            { _lastExpiryRefresh = expires; _service.Refresh(); }
        }
        private void ReceiveOrRetry()
        {
            if (_service == null || _service.IsBusy) return;
            if (!_service.HasSnapshot || _service.Error != null) _service.Refresh();
            else _service.ClaimAll();
        }
        private void Render()
        {
            if (!_subscribed || !isActiveAndEnabled) return;
            _view.content.gameObject.SetActive(_service.HasSnapshot);
            _list.SetEntries(_service.Messages, _service.CanClaim, _service.ServerNow);
            int count = _service.Messages.Count;
            _view.totalLabel.text = _service.HasSnapshot ? $"전체 {count}" : "전체 —";
            _view.unclaimedLabel.text = _service.HasSnapshot ? $"미수령 {count}" : "미수령 —";
            _view.emptyLabel.text = _service.Error ?? (_service.IsLoading ? "우편을 불러오고 있습니다." :
                !_service.HasSnapshot ? "우편 상태를 확인하고 있습니다." : count == 0 ? "보관 중인 우편이 없습니다." : "");
            _view.claimAllLabel.text = _service.IsBusy ? "처리 중…" : !_service.HasSnapshot || _service.Error != null ? "다시 불러오기" : $"모두 받기 ({count})";
            _view.claimAll.interactable = !_service.IsBusy && (!_service.HasSnapshot || _service.Error != null || count > 0);
            _view.deleteClaimed.interactable = false;
            if (_service.HasSnapshot)
                foreach (var popup in GetComponentsInChildren<RewardPopupView>())
                    if (long.TryParse(popup.SourceMailId, out var id) && !_service.Messages.Any(m => m.MailId == id)) popup.Close();
        }
        private void OnClaimCompleted(MailboxClaimResult result)
        {
            if (result.Rewards.Count > 0) RewardPopupView.ShowServerRewards(transform, result.Rewards);
            if (!string.IsNullOrEmpty(result.Message)) UIManager.Instance?.ShowToast(result.Message);
        }
    }
}
