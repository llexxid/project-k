using System;
using KingdomIdle.Mailbox;
using KingdomIdle.Rewards;
using TMPro;
using UnityEngine;
using System.Collections.Generic;
using Protocol.Message.Dto;

namespace KingdomIdle.UGUI
{
    public sealed class MailRowView : MonoBehaviour
    {
        public TMP_Text heading, statusLabel, sender, expiry, claimLabel;
        public RectTransform slots;
        public UnityEngine.UI.Button claim, details;
        public UnityEngine.UI.Image background;
        public CanvasGroup attachments;
        public Color claimedBackground = new Color32(43, 43, 42, 255);
        private Color _backgroundColor;
        private long _expiresUtc, _lastExpiryMinute = long.MinValue;
        private readonly List<RewardSlotView> _slots = new();
        private UnityEngine.UI.ScrollRect _rewardScroll;
        public long MailId { get; private set; }
        private void Awake()
        {
            _backgroundColor = background.color;
            _rewardScroll = slots.GetComponentInParent<UnityEngine.UI.ScrollRect>();
        }
        public void Bind(MailDTO mail, Transform popupOwner, Action receive, long now, bool canClaim)
        {
            bool changedMail = MailId != mail.MailId;
            MailId = mail.MailId;
            heading.text = MailText.Title(mail);
            statusLabel.text = "미수령";
            statusLabel.color = UguiTheme.AccentGold;
            sender.text = MailText.Sender(mail);
            _expiresUtc = mail.ExpiresUtc;
            _lastExpiryMinute = long.MinValue;
            RefreshExpiry(now);
            background.color = _backgroundColor;
            attachments.alpha = 1f;
            claimLabel.text = "받기";
            claim.interactable = canClaim && _expiresUtc > now;
            claim.onClick.RemoveAllListeners(); claim.onClick.AddListener(() => receive());
            details.onClick.RemoveAllListeners(); details.onClick.AddListener(() => RewardPopupView.ShowMail(popupOwner, mail));
            // Reserve the server attachment limit once; scrolling never instantiates slots.
            while (_slots.Count < 10)
            {
                var slot = Instantiate(UIManager.Instance.Catalog.itemRewardSlot, slots, false).GetComponent<RewardSlotView>();
                _slots.Add(slot);
            }
            for (int i = 0; i < _slots.Count; i++)
            {
                bool active = i < mail.Rewards.Count;
                _slots[i].gameObject.SetActive(active);
                if (!active) { _slots[i].button.onClick.RemoveAllListeners(); continue; }
                var reward = RewardDisplayItem.From(mail.Rewards[i]);
                _slots[i].Bind(reward, () => RewardPopupView.ShowItem(popupOwner, reward, mail.MailId.ToString()));
            }
            if (changedMail && _rewardScroll != null) { _rewardScroll.StopMovement(); _rewardScroll.horizontalNormalizedPosition = 0; }
        }
        public void SetCanClaim(bool canClaim, long now) => claim.interactable = canClaim && _expiresUtc > now;
        public void Unbind()
        {
            MailId = 0; claim.onClick.RemoveAllListeners(); details.onClick.RemoveAllListeners();
            foreach (var slot in _slots) slot.button.onClick.RemoveAllListeners();
        }
        public void RefreshExpiry(long now)
        {
            long remaining = Math.Max(0, _expiresUtc - now);
            long minutes = remaining > 0 ? remaining / 60 : -1;
            if (_lastExpiryMinute == minutes) return;
            _lastExpiryMinute = minutes;
            expiry.text = minutes < 0 ? "만료" : minutes == 0 ? "1분 미만 남음" :
                minutes >= 1440 ? $"{minutes / 1440}일 {minutes / 60 % 24}시간 {minutes % 60}분 남음" :
                minutes >= 60 ? $"{minutes / 60}시간 {minutes % 60}분 남음" : $"{minutes}분 남음";
        }
    }
}
