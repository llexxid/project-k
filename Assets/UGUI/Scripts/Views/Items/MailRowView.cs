using System;
using KingdomIdle.Mailbox;
using KingdomIdle.Rewards;
using TMPro;
using UnityEngine;

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
        private bool _built;
        private void Awake()
        {
            _backgroundColor = background.color;
        }
        public void Bind(MailMessage mail, Transform popupOwner, Action receive)
        {
            heading.text = mail.Title;
            statusLabel.text = mail.Claimed ? "수령 완료" : "미수령";
            statusLabel.color = mail.Claimed ? UguiTheme.SuccessGreenBright : UguiTheme.AccentGold;
            sender.text = mail.Sender;
            _expiresUtc = mail.ExpiresUtc;
            RefreshExpiry(RewardGrants.Now);
            background.color = mail.Claimed ? claimedBackground : _backgroundColor;
            attachments.alpha = mail.Claimed ? .55f : 1f;
            claimLabel.text = mail.Claimed ? "수령 완료" : "받기";
            claim.interactable = !mail.Claimed;
            claim.onClick.RemoveAllListeners(); claim.onClick.AddListener(() => receive());
            details.onClick.RemoveAllListeners(); details.onClick.AddListener(() => RewardPopupView.ShowMail(popupOwner, mail));
            if (_built) return;
            _built = true;
            foreach (var reward in mail.Rewards)
            {
                var slot = Instantiate(UIManager.Instance.Catalog.itemRewardSlot, slots, false).GetComponent<RewardSlotView>();
                slot.Bind(reward, () => RewardPopupView.ShowItem(popupOwner, reward, mail.Id));
            }
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
