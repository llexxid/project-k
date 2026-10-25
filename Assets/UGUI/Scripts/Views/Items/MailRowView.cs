using System;
using KingdomIdle.Mailbox;
using TMPro;
using UnityEngine;

namespace KingdomIdle.UGUI
{
    public sealed class MailRowView : MonoBehaviour
    {
        public TMP_Text heading, statusLabel, sender, expiry, claimLabel;
        public RectTransform slots;
        public UnityEngine.UI.Button claim, details;
        private bool _built;
        public void Bind(MailMessage mail, Transform popupOwner, Action receive)
        {
            heading.text = mail.Title;
            statusLabel.text = mail.Claimed ? "수령 완료" : "미수령";
            statusLabel.color = mail.Claimed ? UguiTheme.SuccessGreenBright : UguiTheme.AccentGold;
            sender.text = mail.Sender;
            expiry.text = DateTimeOffset.FromUnixTimeSeconds(mail.ExpiresUtc).ToOffset(TimeSpan.FromHours(9)).ToString("MM.dd HH:mm") + "까지";
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
    }
}
