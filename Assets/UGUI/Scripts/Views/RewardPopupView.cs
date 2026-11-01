using System.Collections.Generic;
using KingdomIdle.Balance;
using KingdomIdle.Mailbox;
using KingdomIdle.OfflineRewards;
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
        public UnityEngine.UI.Button closeButton, backdrop, confirmButton;
        public string SourceMailId { get; private set; }
        private long _account;
        private System.Action _onClosed;

        public static void ShowItem(Transform owner, RewardGrant reward, string mailId = null)
        {
            var view = Open(owner, RewardPresentation.Name(reward), mailId);
            if (view == null) return;
            view.SetDescription(RewardPresentation.Description(reward));
            view.AddSlots(new[] { reward.Copy() }, false);
        }
        public static void ShowRewards(Transform owner, IReadOnlyList<RewardGrant> rewards) => ShowRewards(owner, rewards, "보상 획득");
        public static void ShowRewards(Transform owner, IReadOnlyList<RewardGrant> rewards, string heading)
        {
            var view = Open(owner, heading, null);
            if (view == null) return;
            view.SetDescription("");
            view.AddSlots(rewards, true);
        }
        public static RewardPopupView ShowOfflineRewards(Transform owner, OfflineRewardClaimResult result, bool doubled, System.Action onClosed = null)
        {
            if (result == null || result.GoldGained < 0 || result.ExperienceGained < 0) return null;
            long gold, experience;
            try
            {
                gold = checked(result.GoldGained * (doubled ? 2 : 1));
                experience = checked(result.ExperienceGained * (doubled ? 2 : 1));
            }
            catch (System.OverflowException) { return null; }
            var catalog = UIManager.Instance != null ? UIManager.Instance.Catalog : null;
            var template = catalog != null && catalog.popupRewardInfo != null
                ? catalog.popupRewardInfo.GetComponent<RewardPopupView>() : null;
            if (template == null || catalog.itemRewardSlot == null || template.confirmButton == null) return null;
            var view = Open(owner, "보상 획득", null);
            if (view == null) return null;
            view._onClosed = onClosed;
            view.closeButton.gameObject.SetActive(false);
            view.confirmButton.gameObject.SetActive(true);
            view.confirmButton.onClick.AddListener(view.Close);
            view.SetDescription(doubled ? "보상 2배 획득" : "");
            view.description.color = UguiTheme.AccentGoldStrong;
            view.slots.GetComponent<UnityEngine.UI.GridLayoutGroup>().cellSize = new Vector2(220, 220);
            view.slots.sizeDelta = new Vector2(view.slots.sizeDelta.x, 220);
            var layout = view.rewardScroll.GetComponent<UnityEngine.UI.LayoutElement>();
            layout.minHeight = layout.preferredHeight = 238;
            view.AddSummary("골드", catalog.iconCoin, gold);
            view.AddSummary("경험치", catalog.iconStar, experience);
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(view.panel);
            view.rewardScroll.horizontalNormalizedPosition = 0;
            return view;
        }

        private void AddSummary(string label, Sprite icon, long amount)
        {
            var slot = Instantiate(UIManager.Instance.Catalog.itemRewardSlot, slots, false).GetComponent<RewardSlotView>();
            slot.BindSummary(label, icon, amount);
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
        public void Close()
        {
            var closed = _onClosed;
            _onClosed = null;
            gameObject.SetActive(false);
            Destroy(gameObject);
            closed?.Invoke();
        }
    }
}
