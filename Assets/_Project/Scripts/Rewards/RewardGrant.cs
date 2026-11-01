using System;
using System.Collections.Generic;
using KingdomIdle.Balance;

namespace KingdomIdle.Rewards
{
    [Serializable]
    public sealed class RewardGrant
    {
        public eCurrency Currency;
        public long Amount;
        // 서버 보상(출석판·우편)의 장비 정의 ID. 0이면 재화다. 표시 전용이라 저장 상태에 쓰지 않고, 로컬 지급(TryApply)은 장비를 거절한다.
        [NonSerialized] public int EquipmentId;
        [NonSerialized] public Protocol.Message.Dto.ItemCode? EquipmentCode;
        [NonSerialized] public Protocol.Message.Dto.EquipmentDisposition Disposition = Protocol.Message.Dto.EquipmentDisposition.None;
        [NonSerialized] public bool ServerReward;
        public RewardGrant(eCurrency currency, long amount) { Currency = currency; Amount = amount; }
        public static RewardGrant Equipment(int definitionId, long amount) => new(eCurrency.Gold, amount) { EquipmentId = definitionId };
        public bool IsEquipment => EquipmentId != 0;
        public RewardGrant Copy() => new(Currency, Amount) { EquipmentId = EquipmentId, EquipmentCode = EquipmentCode, Disposition = Disposition, ServerReward = ServerReward };
    }

    // Pending: 서버 요청을 보냈고 결과는 완료 이벤트로 온다(출석판·우편).
    public enum RewardStatus { Success, NotReady, NothingAvailable, Stale, InvalidReward, SaveFailed, Pending }

    public sealed class RewardResult
    {
        public RewardStatus Status { get; }
        public IReadOnlyList<RewardGrant> Rewards { get; }
        public bool Succeeded => Status == RewardStatus.Success;
        public RewardResult(RewardStatus status, IReadOnlyList<RewardGrant> rewards = null)
        { Status = status; Rewards = rewards ?? Array.Empty<RewardGrant>(); }
    }

    /// <summary>재화 명세 검증과 초안 지급만 담당한다. 저장과 출석/우편 정책은 서비스가 소유한다.</summary>
    public static class RewardGrants
    {
        private const int CurrencyCount = (int)eCurrency.MageTicket + 1;
        public static long Now => LocalProgression.TryGetCommittedState(out var state)
            ? Math.Max(LocalProgression.UtcNow, state.QuestLastObservedUtc) : LocalProgression.UtcNow;

        public static bool IsValid(RewardGrant reward) => reward != null && reward.Amount > 0 &&
            (uint)reward.Currency < CurrencyCount;

        public static List<RewardGrant> Copy(IEnumerable<RewardGrant> rewards)
        {
            var copy = new List<RewardGrant>();
            foreach (var reward in rewards) copy.Add(reward.Copy());
            return copy;
        }

        /// <summary>같은 재화의 합계와 잔액 상한을 먼저 검사한다. 예상 가능한 오버플로는 예외로 처리하지 않는다.</summary>
        public static bool TryApply(ProgressionState draft, IEnumerable<RewardGrant> rewards, out List<RewardGrant> granted)
        {
            granted = new List<RewardGrant>();
            var totals = new long[CurrencyCount];
            foreach (var reward in rewards)
            {
                if (!IsValid(reward) || reward.IsEquipment || reward.ServerReward) return false;
                int index = (int)reward.Currency;
                if (reward.Amount > long.MaxValue - totals[index]) return false;
                totals[index] += reward.Amount;
            }
            for (int i = 0; i < totals.Length; i++)
            {
                long balance = draft.Wallet.TryGetValue((eCurrency)i, out var value) ? value : 0;
                if (balance < 0 || totals[i] > long.MaxValue - balance) return false;
            }
            for (int i = 0; i < totals.Length; i++)
            {
                if (totals[i] == 0) continue;
                LocalProgression.Credit(draft, (eCurrency)i, totals[i]);
                granted.Add(new RewardGrant((eCurrency)i, totals[i]));
            }
            return granted.Count > 0;
        }

        /// <summary>서버 우편 첨부를 표시용 보상으로 바꾼다. 알 수 없는 첨부는 null.</summary>
        public static RewardGrant FromServer(Protocol.Message.Dto.MailRewardDTO reward)
        {
            if (reward == null || reward.Amount <= 0) return null;
            if (reward.Kind == Protocol.Message.Dto.RewardKind.Equipment && reward.DefinitionId is int id && id > 0) return new RewardGrant(eCurrency.Gold, reward.Amount) { EquipmentId = id, ServerReward = true };
            if (reward.Kind == Protocol.Message.Dto.RewardKind.Currency && reward.CurrencyType.HasValue) return new RewardGrant((eCurrency)(int)reward.CurrencyType.Value, reward.Amount) { ServerReward = true };
            return null;
        }

        /// <summary>
        /// 서버 지급 결과(GachaRewardDTO)를 결과 팝업용 보상으로 바꾸고 같은 재화·같은 장비끼리 합친다.
        /// 자동 분해된 장비는 받은 강화석으로 보인다.
        /// </summary>
        public static List<RewardGrant> FromServer(IEnumerable<Protocol.Message.Dto.GachaRewardDTO> rewards)
        {
            var merged = new List<RewardGrant>();
            if (rewards == null) return merged;
            foreach (var reward in rewards)
            {
                if (reward == null) continue;
                RewardGrant grant = null;
                if (reward.Kind == Protocol.Message.Dto.RewardKind.Currency && reward.CurrencyType.HasValue && reward.Amount > 0)
                    grant = new RewardGrant((eCurrency)(int)reward.CurrencyType.Value, reward.Amount);
                else if (reward.Kind == Protocol.Message.Dto.RewardKind.Equipment && reward.Disposition == Protocol.Message.Dto.EquipmentDisposition.AutoDismantled)
                    grant = reward.GrantedEquipmentStone > 0 ? new RewardGrant(eCurrency.EquipmentStone, reward.GrantedEquipmentStone) : null;
                else if (reward.Kind == Protocol.Message.Dto.RewardKind.Equipment)
                {
                    int id = reward.ItemCode.HasValue ? (int)reward.ItemCode.Value.GetItemCode() : reward.DefinitionId ?? 0;
                    grant = id > 0 ? RewardGrant.Equipment(id, Math.Max(1, reward.Amount)) : null;
                }
                if (grant == null) continue;
                grant.ServerReward = true;
                grant.Disposition = reward.Disposition;
                grant.EquipmentCode = grant.IsEquipment ? reward.ItemCode : null;
                RewardGrant same = null;
                foreach (var entry in merged)
                    if (entry.EquipmentId == grant.EquipmentId && entry.Disposition == grant.Disposition &&
                        (entry.EquipmentCode?.Code & ~0xFFFFUL) == (grant.EquipmentCode?.Code & ~0xFFFFUL) &&
                        entry.EquipmentCode?.ExpireTimesc == grant.EquipmentCode?.ExpireTimesc &&
                        (grant.IsEquipment || entry.Currency == grant.Currency)) { same = entry; break; }
                if (same == null) merged.Add(grant);
                else if (same.Amount <= long.MaxValue - grant.Amount) same.Amount += grant.Amount;
                else merged.Add(grant);
            }
            return merged;
        }
    }
}
