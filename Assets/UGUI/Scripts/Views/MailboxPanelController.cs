using System.Collections.Generic;
using System.Linq;
using KingdomIdle.Balance;
using KingdomIdle.Mailbox;
using KingdomIdle.Rewards;
using Scripts.Core;
using UnityEngine;

namespace KingdomIdle.UGUI
{
    [RequireComponent(typeof(MailboxPanelView))]
    public sealed class MailboxPanelController : MonoBehaviour
    {
        private MailboxPanelView _view;
        private MailboxSnapshot _snapshot;
        private readonly Dictionary<string, MailRowView> _rows = new();
        private long _revision = -1, _account = -1, _expiry = long.MaxValue;
        private float _nextRefresh;
        private MailboxService Service => GameManager.Instance != null ? GameManager.Instance.Mailbox : null;
        private void Awake()
        {
            _view = GetComponent<MailboxPanelView>();
            _view.claimAll.onClick.AddListener(() => Receive(null));
            _view.deleteClaimed.onClick.AddListener(DeleteClaimed);
        }
        private void OnEnable() { _revision = -1; _nextRefresh = 0; }
        private void Update()
        {
            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + .5f;
            Refresh();
        }
        private void Refresh()
        {
            var service = Service;
            if (service == null || !LocalProgression.TryGetCommittedState(out var state))
            {
                _snapshot = null; _revision = -1;
                _view.content.gameObject.SetActive(false);
                foreach (var popup in GetComponentsInChildren<RewardPopupView>()) popup.Close();
                _view.emptyLabel.text = "계정 정보를 준비하고 있습니다.";
                _view.totalLabel.text = _view.unclaimedLabel.text = _view.claimedLabel.text = "";
                _view.claimAllLabel.text = "모두 받기";
                _view.claimAll.interactable = _view.deleteClaimed.interactable = false;
                return;
            }
            bool expired = RewardGrants.Now >= _expiry;
            if (!expired && _revision == state.RewardsRevision && _account == LocalProgression.AccountGeneration) return;
            bool saved = service.Refresh();
            _snapshot = service.GetSnapshot();
            _view.content.gameObject.SetActive(true);
            if (_account != LocalProgression.AccountGeneration)
            {
                foreach (var row in _rows.Values) Destroy(row.gameObject);
                _rows.Clear();
            }
            _account = LocalProgression.AccountGeneration;
            _revision = LocalProgression.State.RewardsRevision;
            var ids = _snapshot.Messages.Select(m => m.Id).ToHashSet();
            foreach (var id in _rows.Keys.Where(id => !ids.Contains(id)).ToArray()) { Destroy(_rows[id].gameObject); _rows.Remove(id); }
            foreach (var popup in GetComponentsInChildren<RewardPopupView>())
                if (popup.SourceMailId != null && !ids.Contains(popup.SourceMailId)) popup.Close();
            foreach (var mail in _snapshot.Messages)
            {
                if (!_rows.TryGetValue(mail.Id, out var row))
                {
                    row = Instantiate(UIManager.Instance.Catalog.itemMailRow, _view.content, false).GetComponent<MailRowView>();
                    _rows.Add(mail.Id, row);
                }
                string id = mail.Id;
                row.Bind(mail, transform, () => Receive(id));
            }
            _expiry = _snapshot.Messages.Count > 0 ? _snapshot.Messages.Min(m => m.ExpiresUtc) : long.MaxValue;
            _view.emptyLabel.text = !saved ? "만료 우편 정리를 저장하지 못했습니다." : ids.Count == 0 ? "보관 중인 우편이 없습니다." : "";
            int unclaimed = _snapshot.Messages.Count(m => !m.Claimed);
            int claimed = _snapshot.Messages.Count - unclaimed;
            _view.totalLabel.text = $"전체 {_snapshot.Messages.Count}";
            _view.unclaimedLabel.text = $"미수령 {unclaimed}";
            _view.claimedLabel.text = $"수령 완료 {claimed}";
            _view.claimAllLabel.text = $"모두 받기 ({unclaimed})";
            _view.claimAll.interactable = unclaimed > 0;
            _view.deleteClaimed.interactable = claimed > 0;
        }
        private void Receive(string id)
        {
            if (Service == null || _snapshot == null) return;
            var result = id == null ? Service.TryClaimAll(_snapshot) : Service.TryClaim(_snapshot, id);
            _revision = -1; Refresh();
            if (result.Succeeded) RewardPopupView.ShowRewards(transform, result.Rewards);
            else RewardPresentation.Report(result);
        }
        private void DeleteClaimed()
        {
            if (Service == null || _snapshot == null) return;
            var result = Service.DeleteClaimed(_snapshot);
            _revision = -1; Refresh();
            if (!result.Succeeded) RewardPresentation.Report(result);
        }
    }
}
