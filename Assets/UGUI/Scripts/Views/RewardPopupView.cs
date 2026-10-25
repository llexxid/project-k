using System.Collections.Generic;
using KingdomIdle.Balance;
using KingdomIdle.Mailbox;
using KingdomIdle.Rewards;
using TMPro;
using UnityEngine;

namespace KingdomIdle.UGUI
{
    /// <summary>아이템 설명·우편 본문·확정 보상을 같은 작은 팝업 프리팹으로 표시한다.</summary>
    public sealed class RewardPopupView : MonoBehaviour
    {
        public TMP_Text title, description;
        public RectTransform panel, slots;
        public GameObject descriptionArea;
        public UnityEngine.UI.ScrollRect rewardScroll;
        public UnityEngine.UI.Button closeButton, backdrop;
        public string SourceMailId { get; private set; }
        private long _account;

        public static void ShowItem(Transform owner, RewardGrant reward, string mailId = null)
        {
            var view = Open(owner, RewardPresentation.Name(reward.Currency), mailId);
            if (view == null) return;
            view.SetDescription(RewardPresentation.Description(reward.Currency));
            view.AddSlots(new[] { new RewardGrant(reward.Currency, reward.Amount) }, false);
        }
        public static void ShowRewards(Transform owner, IReadOnlyList<RewardGrant> rewards)
        {
            var view = Open(owner, "보상 획득", null);
            if (view == null) return;
            view.SetDescription("");
            view.AddSlots(rewards, true);
        }
        public static void ShowMail(Transform owner, MailMessage mail)
        {
            var view = Open(owner, mail.Title, mail.Id);
            if (view == null) return;
            view.SetDescription(mail.Body);
            view.AddSlots(mail.Rewards, true);
        }
        private static RewardPopupView Open(Transform owner, string heading, string mailId)
        {
            var host = UIManager.Instance;
            if (host == null || host.Catalog.popupRewardInfo == null || owner == null || !owner.gameObject.activeInHierarchy) return null;
            var view = Instantiate(host.Catalog.popupRewardInfo, owner, false).GetComponent<RewardPopupView>();
            view.SourceMailId = mailId;
            view._account = LocalProgression.AccountGeneration;
            view.title.text = heading;
            view.closeButton.onClick.AddListener(view.Close);
            view.backdrop.onClick.AddListener(view.Close);
            ModalBackHandler.Bind(view.gameObject, view.Close);
            UITween.PopIn(view.panel);
            return view;
        }
        private void SetDescription(string text)
        {
            description.text = text;
            descriptionArea.SetActive(!string.IsNullOrWhiteSpace(text));
        }
        private void AddSlots(IEnumerable<RewardGrant> rewards, bool showInfo)
        {
            foreach (var reward in rewards)
            {
                var slot = Instantiate(UIManager.Instance.Catalog.itemRewardSlot, slots, false).GetComponent<RewardSlotView>();
                slot.Bind(reward, showInfo ? () => ShowItem(transform, reward, SourceMailId) : null);
            }
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
            rewardScroll.horizontalNormalizedPosition = 0;
        }
        private void Update() { if (_account != LocalProgression.AccountGeneration) Close(); }
        private void OnDisable() { if (Application.isPlaying) Destroy(gameObject); }
        public void Close() { gameObject.SetActive(false); Destroy(gameObject); }
    }
}
