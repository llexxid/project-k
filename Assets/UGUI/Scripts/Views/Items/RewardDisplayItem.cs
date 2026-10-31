using System.Collections.Generic;
using Protocol.Message.Dto;
using UnityEngine;

namespace KingdomIdle.UGUI
{
    /// <summary>Presentation only; this type never grants an item.</summary>
    public sealed class RewardDisplayItem
    {
        public RewardKind Kind;
        public eCurrency Currency;
        public int DefinitionId;
        public long Amount, Stones;
        public EquipmentDisposition Disposition;
        private EquipmentData Equipment => EquipmentManager.Instance?.GetData(DefinitionId);
        public Sprite Icon => Kind == RewardKind.Currency ? RewardPresentation.Icon(Currency) : Equipment?.icon;
        public string Name => Kind == RewardKind.Currency ? RewardPresentation.Name(Currency) : Equipment?.DisplayName ?? "장비 정보 확인 중";
        public Color Frame => Kind == RewardKind.Equipment && Equipment != null ? UguiTheme.RarityColor(Equipment.rarity) : UguiTheme.Bronze;
        public string Description => Kind == RewardKind.Currency ? RewardPresentation.Description(Currency) :
            (Equipment?.description ?? "장비 정보를 불러오지 못했습니다.") + (Disposition switch
            {
                EquipmentDisposition.Pending => "\n대기 보관함에 보관했습니다.",
                EquipmentDisposition.Reserve => "\n예비 보관함에 보관했습니다.",
                EquipmentDisposition.AutoDismantled => $"\n자동 분해: 강화석 {NumberNotation.Format(Stones)}개 획득",
                EquipmentDisposition.Inventory => "\n장비 보관함에 보관했습니다.",
                _ => ""
            });
        public string ResultLabel => Disposition switch
        {
            EquipmentDisposition.AutoDismantled => "자동 분해",
            EquipmentDisposition.Pending => "대기 보관",
            EquipmentDisposition.Reserve => "예비 보관",
            _ => ""
        };

        public static RewardDisplayItem From(MailRewardDTO r) => new()
        { Kind = r.Kind, Currency = (eCurrency)(int)(r.CurrencyType ?? 0), DefinitionId = r.DefinitionId ?? 0, Amount = r.Amount };

        public static List<RewardDisplayItem> Granted(IReadOnlyList<GachaRewardDTO> rewards)
        {
            var result = new List<RewardDisplayItem>();
            var grouped = new Dictionary<(RewardKind, int, int, EquipmentDisposition, ulong, ulong), RewardDisplayItem>();
            foreach (var r in rewards)
            {
                var key = (r.Kind, (int)(r.CurrencyType ?? 0), r.DefinitionId ?? 0, r.Disposition,
                    r.ItemCode.HasValue ? r.ItemCode.Value.Code & ~0xFFFFUL : 0, r.ItemCode?.ExpireTimesc ?? 0);
                if (grouped.TryGetValue(key, out var previous) && previous.Amount <= long.MaxValue - r.Amount && previous.Stones <= long.MaxValue - r.GrantedEquipmentStone)
                { previous.Amount += r.Amount; previous.Stones += r.GrantedEquipmentStone; continue; }
                var item = new RewardDisplayItem { Kind = r.Kind, Currency = (eCurrency)(int)(r.CurrencyType ?? 0),
                    DefinitionId = r.DefinitionId ?? 0, Amount = r.Amount, Disposition = r.Disposition,
                    Stones = r.GrantedEquipmentStone };
                grouped[key] = item; result.Add(item);
            }
            return result;
        }
    }
}
