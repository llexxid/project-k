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
        private bool _closingAfterFailure;
        private float _nextTick;
        private int _lastFreeSlots = -1;

        private void Awake()
        {
            _view = GetComponent<MailboxPanelView>();
            // Disconnect before the closing animation, and prevent Update from reconnecting.
            _view.OnClosed = () => enabled = false;
            _list = GetComponent<VirtualizedMailList>() ?? gameObject.AddComponent<VirtualizedMailList>();
            _list.Initialize(_view.content, UIManager.Instance.Catalog.itemMailRow, transform, id => _service?.TryClaim(id));
            _view.claimAll.onClick.AddListener(ReceiveOrRetry);
            _view.deleteClaimed.onClick.AddListener(() => _service?.LoadMore());
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
            _closingAfterFailure = false;
            _subscribed = true;
            _service.Open();
        }
        private void OnDisable()
        {
            if (!_subscribed) return;
            _service.Changed -= Render;
            _service.ClaimCompleted -= OnClaimCompleted;
            _subscribed = false;
            _service.Close();
            _list.SetEntries(Array.Empty<Protocol.Message.Dto.MailDTO>(), false, 0);
        }
#if UNITY_EDITOR
        internal void UseTestService(MailboxService service) { _service = service; }
#endif
        private void Update()
        {
            if (Time.unscaledTime < _nextTick) return;
            _nextTick = Time.unscaledTime + .5f;
            Connect();
            if (!_subscribed) return;
            _service.Tick();
            if (EquipmentManager.IsInstanceInventory && _lastFreeSlots != MailboxService.FreeEquipmentSlots)
                Render();
            _list.UpdateTime(_service.ServerNow, _service.CanClaim);
        }
        private void ReceiveOrRetry()
        {
            if (_service == null || _service.IsBusy) return;
            if (!_service.HasSnapshot || _service.Error != null) _service.RequestRefresh();
            else _service.TryClaimAll();
        }
        private void Render()
        {
            if (!_subscribed || !isActiveAndEnabled) return;
            _view.content.gameObject.SetActive(_service.HasSnapshot);
            _list.SetEntries(_service.Messages, _service.CanClaim, _service.ServerNow);
            int count = _service.Messages.Count;
            int receivable = _service.CanClaim ? _service.ReceivableCount : 0;
            _lastFreeSlots = MailboxService.FreeEquipmentSlots;
            _view.totalLabel.text = _service.HasSnapshot ? $"전체 {_service.TotalCount}" : "전체 —";
            _view.unclaimedLabel.text = _service.HasSnapshot ? $"미수령 {_service.TotalCount}" : "미수령 —";
            _view.emptyLabel.text = _service.Error ?? (_service.State == MailboxState.Loading ? "우편을 불러오고 있습니다." :
                !_service.HasSnapshot ? "우편 상태를 확인하고 있습니다." : count == 0 ? "보관 중인 우편이 없습니다." : "");
            _view.claimAllLabel.text = _service.IsBusy ? "처리 중…" : !_service.HasSnapshot || _service.Error != null ? "다시 불러오기" :
                _service.HasMore ? $"표시된 우편 받기 ({receivable})" :
                receivable == count ? $"모두 받기 ({count})" : $"받을 수 있는 우편 ({receivable})";
            _view.claimAll.interactable = !_service.LoadFailed && !_service.IsBusy && (!_service.HasSnapshot || _service.Error != null || receivable > 0);
            _view.deleteClaimed.interactable = _service.HasMore && _service.CanClaim;
            _view.claimedLabel.text = _service.HasMore ? $"우편 더 보기 ({count}/{_service.TotalCount})" : "수령 시 자동 삭제";
            foreach (var popup in GetComponentsInChildren<RewardPopupView>())
                if (popup.SourceMailId.HasValue && (!_service.HasSnapshot || !_service.Messages.Any(m => m.MailId == popup.SourceMailId.Value))) popup.Close();
            if (_service.LoadFailed && !_closingAfterFailure)
            {
                _closingAfterFailure = true;
                void Close() { if (this != null && isActiveAndEnabled && _service.LoadFailed) _view.closeButton.onClick.Invoke(); }
                if (RewardPopupView.ShowMessage(UIManager.Instance.LayerPopups, "우편함 조회 실패", _service.Error, Close) == null) Close();
            }
        }
        private void OnClaimCompleted(MailClaimInfo result)
        {
            if (result.Rewards.Count > 0) RewardPopupView.ShowRewards(transform, result.Rewards);
            if (!string.IsNullOrEmpty(result.Message)) UIManager.Instance?.ShowToast(result.Message);
        }
    }
}
