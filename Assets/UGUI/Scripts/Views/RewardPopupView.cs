using System.Collections.Generic;
using System.Linq;
using Protocol.Message.Dto;
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
        public long? SourceMailId { get; private set; }
        private long _account;
        private System.Action _onClosed;

        public static void ShowItem(Transform owner, RewardGrant reward, long? mailId = null)
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
        internal static RewardPopupView ShowClaimedRewards(Transform owner, IReadOnlyList<RewardGrant> rewards,
            System.Action onClosed, string message = null, bool failed = false)
        {
            var view = Open(owner, "보상 획득", null);
            if (view == null) return null;
            view._onClosed = onClosed;
            try { view.ApplyClaimResult(rewards, message, failed); return view; }
            catch { view.Close(); throw; }
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
        public static void ShowMail(Transform owner, MailDTO mail)
        {
            var view = Open(owner, MailText.Title(mail), mail.MailId);
            if (view == null) return;
            view.title.textWrappingMode = TextWrappingModes.Normal;
            view.title.fontSizeMin = Mathf.Min(28f, view.title.fontSize);
            view.title.fontSizeMax = view.title.fontSize;
            view.title.enableAutoSizing = true;
            string body = MailText.Body(mail);
            if (mail.IsEquipmentOverflow)
                body += $"\n\n기한 없음 · 빈 칸 {MailboxService.FreeEquipmentSlots} / 필요 {MailboxProtocol.RequiredEquipmentSlots(mail)}";
            view.SetDescription(body);
            view.AddSlots(mail.Rewards.Select(RewardGrants.FromServer), true);
        }
        public static RewardPopupView ShowMessage(Transform owner, string heading, string message, System.Action onClosed)
        {
            var view = Open(owner, heading, null);
            if (view == null) return null;
            view.SetDescription(message);
            view._onClosed = onClosed;
            view.confirmButton.gameObject.SetActive(true);
            view.confirmButton.onClick.AddListener(view.Close);
            return view;
        }
        private static RewardPopupView Open(Transform owner, string heading, long? mailId)
        {
            var host = UIManager.Instance;
            if (host == null || host.Catalog == null || host.Catalog.popupRewardInfo == null || owner == null || !owner.gameObject.activeInHierarchy) return null;
            var instance = Instantiate(host.Catalog.popupRewardInfo, owner, false);
            var view = instance.GetComponent<RewardPopupView>();
            if (view == null) { Destroy(instance); return null; }
            try
            {
            view.SourceMailId = mailId;
            view._account = LocalProgression.AccountGeneration;
            view.title.text = heading;
            view.closeButton.onClick.AddListener(view.Close);
            view.backdrop.onClick.AddListener(view.Close);
            ModalBackHandler.Bind(view.gameObject, view.Close);
            UITween.PopIn(view.panel);
            return view;
            }
            catch { Destroy(instance); throw; }
        }
        private void SetDescription(string text)
        {
            description.text = text;
            descriptionArea.SetActive(!string.IsNullOrWhiteSpace(text));
        }
        internal void ApplyClaimResult(IReadOnlyList<RewardGrant> rewards, string message, bool failed)
        {
            bool hasRewards = rewards.Count != 0;
            title.text = failed ? "보상 수령 실패" : hasRewards ? "보상 획득" : "퀘스트 완료";
            SetDescription(message);
            rewardScroll.gameObject.SetActive(hasRewards);
            AddSlots(rewards, false);
        }

        private void AddSlots(IEnumerable<RewardGrant> rewards, bool showInfo)
        {
            int index = 0;
            foreach (var reward in rewards)
            {
                var slot = index < slots.childCount
                    ? slots.GetChild(index).GetComponent<RewardSlotView>()
                    : Instantiate(UIManager.Instance.Catalog.itemRewardSlot, slots, false).GetComponent<RewardSlotView>();
                slot.gameObject.SetActive(true);
                slot.Bind(reward, showInfo ? () => ShowItem(transform, reward, SourceMailId) : null);
                if (reward != null && reward.Amount == 0) slot.quantity.text = "";
                index++;
            }
            for (; index < slots.childCount; index++) slots.GetChild(index).gameObject.SetActive(false);
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
