using System;
using KingdomIdle.Rewards;
using TMPro;
using UnityEngine;

namespace KingdomIdle.UGUI
{
    /// <summary>재화·장비 보상 슬롯의 표시와 클릭 전달만 담당한다. 수령 정책은 소유 화면에서 결정한다.</summary>
    public sealed class RewardSlotView : MonoBehaviour
    {
        public UnityEngine.UI.Button button;
        public UnityEngine.UI.Image icon, frame, check;
        public TMP_Text quantity, day;

        public void Bind(RewardGrant reward, Action clicked, int dayNumber = 0, bool available = false, bool claimed = false)
        {
            icon.sprite = reward == null ? null : RewardPresentation.Icon(reward);
            icon.enabled = icon.sprite != null;
            icon.color = dayNumber == 0 || available ? Color.white : new Color(.55f, .55f, .55f);
            quantity.text = reward == null ? "" : NumberNotation.Format(reward.Amount);
            quantity.color = dayNumber == 0 || available ? UguiTheme.Parchment : UguiTheme.TextTertiary;
            frame.color = available ? UguiTheme.AccentGoldStrong : RewardPresentation.Frame(reward);
            check.gameObject.SetActive(claimed);
            day.text = dayNumber > 0 ? dayNumber + "일차" : RewardPresentation.ResultLabel(reward);
            button.onClick.RemoveAllListeners();
            button.interactable = reward != null && clicked != null;
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
        internal static Color Frame(RewardGrant reward)
        {
            var data = reward != null && reward.IsEquipment && EquipmentManager.Instance != null ? EquipmentManager.Instance.GetData(reward.EquipmentId) : null;
            return data != null ? UguiTheme.RarityColor(data.rarity) : UguiTheme.Bronze;
        }
        internal static string ResultLabel(RewardGrant reward) => reward?.Disposition switch
        {
            Protocol.Message.Dto.EquipmentDisposition.Pending => "대기 보관",
            Protocol.Message.Dto.EquipmentDisposition.Reserve => "예비 보관",
            Protocol.Message.Dto.EquipmentDisposition.AutoDismantled => "자동 분해",
            _ => ""
        };
        // 서버 보상(출석판·우편)의 장비는 클라이언트 장비 카탈로그로 이름·아이콘을 찾는다. 없으면 기본 표기.
        internal static string Name(RewardGrant reward)
        {
            if (reward == null) return "";
            if (!reward.IsEquipment) return Name(reward.Currency);
            var data = EquipmentManager.Instance != null ? EquipmentManager.Instance.GetData(reward.EquipmentId) : null;
            return data != null ? data.DisplayName : $"장비 {reward.EquipmentId}";
        }
        internal static string Description(RewardGrant reward)
        {
            if (reward == null) return "";
            string description = reward.IsEquipment ? "장비 보관함에 들어갑니다. 보관함이 가득 차면 대기함이나 예비 보관으로 갑니다." : Description(reward.Currency);
            return reward.Disposition switch
            {
                Protocol.Message.Dto.EquipmentDisposition.Inventory => "장비 보관함에 보관했습니다.",
                Protocol.Message.Dto.EquipmentDisposition.Pending => "대기 보관함에 보관했습니다.",
                Protocol.Message.Dto.EquipmentDisposition.Reserve => "예비 보관함에 보관했습니다.",
                Protocol.Message.Dto.EquipmentDisposition.AutoDismantled => $"장비 자동 분해로 강화석 {NumberNotation.Format(reward.Amount)}개를 획득했습니다.",
                _ => description
            };
        }
        internal static Sprite Icon(RewardGrant reward, UIViewCatalog catalog = null)
        {
            if (reward == null) return null;
            if (!reward.IsEquipment) return Icon(reward.Currency, catalog);
            var data = EquipmentManager.Instance != null ? EquipmentManager.Instance.GetData(reward.EquipmentId) : null;
            if (data != null && data.icon != null) return data.icon;
            var cat = catalog != null ? catalog : UIManager.Instance != null ? UIManager.Instance.Catalog : null;
            return cat != null ? cat.iconChest : null;
        }
        internal static string Name(eCurrency currency) => currency switch
        {
            eCurrency.Gold => "골드", eCurrency.AncientCoin => "고대주화",
            eCurrency.ClassFragment => "전직 파편", eCurrency.ArcaneKnowledge => "비전지식",
            eCurrency.Ruby => "루비", eCurrency.EquipmentStone => "강화석",
            eCurrency.KingdomSupply => "왕국 보급품", eCurrency.TrainingTome => "훈련서",
            eCurrency.EquipmentTicket => "장비 뽑기권", eCurrency.MageTicket => "마탑 뽑기권",
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
                eCurrency.EquipmentStone => cat.iconEquipmentStone,
                eCurrency.EquipmentTicket => cat.iconChest, eCurrency.MageTicket => cat.iconSkillWand,
                _ => cat.iconGem
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
            eCurrency.EquipmentTicket => "장비 뽑기권입니다. 사용 기능은 준비 중이며 보유 수량은 저장됩니다.",
            eCurrency.MageTicket => "마탑 뽑기권입니다. 사용 기능은 준비 중이며 보유 수량은 저장됩니다.",
            _ => "훈련 재화입니다."
        };
        internal static void Report(RewardResult result)
        {
            // Pending은 서버 요청을 보냈다는 뜻이다. 결과는 완료 이벤트에서 알린다.
            if (result.Succeeded || result.Status == RewardStatus.Pending) return;
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
