using System;
using KingdomIdle.Rewards;
using TMPro;
using UnityEngine;

namespace KingdomIdle.UGUI
{
    /// <summary>재화 슬롯의 표시와 클릭 전달만 담당한다. 수령 정책은 소유 화면에서 결정한다.</summary>
    public sealed class RewardSlotView : MonoBehaviour
    {
        public UnityEngine.UI.Button button;
        public UnityEngine.UI.Image icon, frame, check;
        public TMP_Text quantity, day;

        public void Bind(RewardGrant reward, Action clicked, int dayNumber = 0, bool available = false, bool claimed = false)
        {
            icon.sprite = reward == null ? null : RewardPresentation.Icon(reward.Currency);
            icon.enabled = reward != null;
            icon.color = dayNumber == 0 || available ? Color.white : new Color(.55f, .55f, .55f);
            quantity.text = reward == null ? "" : NumberNotation.Format(reward.Amount);
            quantity.color = dayNumber == 0 || available ? UguiTheme.Parchment : UguiTheme.TextTertiary;
            frame.color = available ? UguiTheme.AccentGoldStrong : UguiTheme.Bronze;
            check.gameObject.SetActive(claimed);
            day.text = dayNumber > 0 ? dayNumber + "일차" : "";
            button.onClick.RemoveAllListeners();
            button.interactable = reward != null;
            button.onClick.AddListener(() => clicked?.Invoke());
        }

        internal void BindSummary(string label, Sprite sprite, long amount)
        {
            icon.sprite = sprite;
            icon.enabled = sprite != null;
            icon.color = Color.white;
            day.text = label;
            quantity.text = "+" + NumberNotation.Format(amount);
            quantity.color = UguiTheme.AccentGoldStrong;
            frame.color = UguiTheme.Bronze;
            check.gameObject.SetActive(false);
            button.onClick.RemoveAllListeners();
            button.transition = UnityEngine.UI.Selectable.Transition.None;
            button.interactable = false;
        }
    }

    internal static class RewardPresentation
    {
        internal static string Name(eCurrency currency) => currency switch
        {
            eCurrency.Gold => "골드", eCurrency.AncientCoin => "고대주화",
            eCurrency.ClassFragment => "전직 파편", eCurrency.ArcaneKnowledge => "비전지식",
            eCurrency.Ruby => "루비", eCurrency.EquipmentStone => "강화석",
            eCurrency.KingdomSupply => "왕국 보급품", eCurrency.TrainingTome => "훈련서",
            _ => currency.ToString()
        };
        internal static Sprite Icon(eCurrency currency, UIViewCatalog catalog = null)
        {
            var cat = catalog != null ? catalog : UIManager.Instance != null ? UIManager.Instance.Catalog : null;
            if (cat == null) return null;
            return currency switch
            {
                eCurrency.Gold => cat.iconCoin, eCurrency.AncientCoin => cat.iconAncientCoin,
                eCurrency.ClassFragment => cat.iconFragment, eCurrency.ArcaneKnowledge => cat.iconArcane,
                eCurrency.EquipmentStone => cat.iconEquipmentStone, _ => cat.iconGem
            };
        }
        internal static string Description(eCurrency currency) => currency switch
        {
            eCurrency.Gold => "왕국군의 공격력과 체력 강화에 사용합니다.",
            eCurrency.AncientCoin => "장비 뽑기에 사용합니다.",
            eCurrency.ClassFragment => "왕국군 전직에 사용합니다.",
            eCurrency.ArcaneKnowledge => "마탑 스킬 뽑기와 강화에 사용합니다.",
            eCurrency.Ruby => "환생 후에도 유지되는 영구 성장에 사용합니다.",
            eCurrency.EquipmentStone => "장비 강화에 사용합니다.",
            eCurrency.KingdomSupply => "왕국 보급 재화입니다.",
            _ => "훈련 재화입니다."
        };
        internal static void Report(RewardResult result)
        {
            if (result.Succeeded) return;
            UIManager.Instance?.ShowToast(result.Status switch
            {
                RewardStatus.NotReady => "계정 정보를 준비하고 있습니다.",
                RewardStatus.NothingAvailable => "받을 보상이 없습니다.",
                RewardStatus.Stale => "보상 상태가 변경되었습니다. 다시 확인해 주세요.",
                RewardStatus.InvalidReward => "보상 정보 또는 보유 수량을 확인해 주세요.",
                _ => "저장하지 못했습니다. 다시 시도해 주세요."
            });
        }
    }
}
