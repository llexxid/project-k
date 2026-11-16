using System;
using System.Collections.Generic;
using System.Linq;
using KingdomIdle.Balance;
using Protocol.Message.Dto;
using UnityEngine;

namespace KingdomIdle.Rewards
{
    public enum LootNotificationKind { Currency, Equipment, MageSkill }

    public struct LootNotification
    {
        public LootNotificationKind Kind;
        public eCurrency Currency;
        public int DefinitionId;
        public long Amount;
        public EquipmentDisposition Disposition;
        public bool IsFragment;
        public LootNotification(LootNotificationKind kind, eCurrency currency, int definitionId, long amount,
            EquipmentDisposition disposition = EquipmentDisposition.None, bool isFragment = false)
        { Kind = kind; Currency = currency; DefinitionId = definitionId; Amount = amount; Disposition = disposition; IsFragment = isFragment; }
    }

    /// <summary>확정 획득의 일시적인 표시 큐. 지급·저장·서버 요청은 수행하지 않는다.</summary>
    public static class LootNotifications
    {
        const int Capacity = 64, ReceiptCapacity = 256;
        const double Lifetime = 30;
        sealed class Entry { internal LootNotification Item; internal double Expires; }
        static readonly List<Entry> Items = new();
        static readonly HashSet<string> Receipts = new();
        static readonly Queue<string> ReceiptOrder = new();
        static Func<double> _clock = () => Time.realtimeSinceStartupAsDouble;
        static Func<LootNotification, bool> _filter;
        static long _sequence;
        static LootNotifications() => LocalProgression.AccountChanged += Clear;
        public static int Count { get { Expire(); return Items.Count; } }
        public static void Clear() { Items.Clear(); Receipts.Clear(); ReceiptOrder.Clear(); }
        public static void ClearPending() => Items.Clear();
        public static void SetFilter(Func<LootNotification, bool> allows) { _filter = allows; Prune(); }
        static bool Allowed(LootNotification item)
        {
            try { return _filter == null || _filter(item); }
            catch (Exception exception) { Warn(exception); return false; }
        }
        public static void Prune() { Expire(); Items.RemoveAll(entry => !Allowed(entry.Item)); }
        static void Expire() => Items.RemoveAll(entry => entry.Expires <= _clock());
        public static bool TryTake(out LootNotification notification)
        {
            Expire(); notification = default;
            if (Items.Count == 0) return false;
            notification = Items[0].Item; Items.RemoveAt(0); return true;
        }
        static bool Same(LootNotification a, LootNotification b) => a.Kind == b.Kind && a.Currency == b.Currency &&
            a.DefinitionId == b.DefinitionId && a.Disposition == b.Disposition && a.IsFragment == b.IsFragment;
        /// <summary>표시 중인 동일 항목만 합산한다. 다른 항목의 순서·만료와 합산할 수 없는 수량은 보존한다.</summary>
        public static bool TryTakeMatching(LootNotification current, out LootNotification notification)
        {
            notification = default;
            if (current.Amount <= 0) return false;
            Expire();
            for (int i = 0; i < Items.Count; i++)
            {
                var item = Items[i].Item;
                if (!Same(current, item) || current.Amount > long.MaxValue - item.Amount) continue;
                notification = item; Items.RemoveAt(i); return true;
            }
            return false;
        }
        public static bool Publish(LootNotification notification)
        {
            try { return Enqueue(notification); }
            catch (Exception exception) { Warn(exception); return false; }
        }
        static bool Enqueue(LootNotification notification)
        {
            if (notification.Amount <= 0 || !Enum.IsDefined(typeof(LootNotificationKind), notification.Kind) || !Enum.IsDefined(typeof(EquipmentDisposition), notification.Disposition) ||
                (notification.Kind == LootNotificationKind.Currency && !Enum.IsDefined(typeof(eCurrency), notification.Currency)) ||
                (notification.Kind == LootNotificationKind.Equipment && notification.DefinitionId <= 0) ||
                (notification.Kind == LootNotificationKind.MageSkill && notification.DefinitionId < 0) || !Allowed(notification)) return false;
            Expire();
            foreach (var entry in Items)
                if (Same(entry.Item, notification) && entry.Item.Amount <= long.MaxValue - notification.Amount)
                {
                    var sum = entry.Item; sum.Amount += notification.Amount; entry.Item = sum;
                    entry.Expires = _clock() + Lifetime; return true;
                }
            if (Items.Count == Capacity) Items.RemoveAt(0);
            Items.Add(new Entry { Item = notification, Expires = _clock() + Lifetime }); return true;
        }

        public sealed class Snapshot
        {
            internal long Generation, Sequence;
            internal bool Published, EquipmentCaptured, MageCaptured;
            internal readonly Dictionary<eCurrency, long> Wallet = new();
            internal readonly Dictionary<int, long> Equipment = new(), Fragments = new();
            internal readonly HashSet<int> Skills = new();
        }
        public static Snapshot Capture(bool equipment = true, bool mage = true)
        {
            try { return CaptureState(equipment, mage); }
            catch (Exception exception) { Warn(exception); return null; }
        }
        static Snapshot CaptureState(bool equipment, bool mage)
        {
            if (!LocalProgression.IsReady) return null;
            var s = LocalProgression.State;
            var before = new Snapshot { Generation = LocalProgression.AccountGeneration, Sequence = ++_sequence, EquipmentCaptured = equipment, MageCaptured = mage };
            foreach (var pair in s.Wallet) before.Wallet[pair.Key] = pair.Value;
            if (equipment) CountEquipment(s, before.Equipment);
            if (mage) foreach (var pair in s.MageSkills) { before.Skills.Add(pair.Key); before.Fragments[pair.Key] = pair.Value.Fragments; }
            return before;
        }
        static void CountEquipment(ProgressionState state, Dictionary<int, long> counts)
        {
            foreach (var item in state.Equipment.Concat(state.PendingEquipment))
            { counts.TryGetValue(item.Code, out long count); counts[item.Code] = count + 1; }
            foreach (var item in state.LegacyEquipment)
            { counts.TryGetValue(item.Code, out long count); counts[item.Code] = checked(count + item.Count); }
        }
        static long Value<TKey>(Dictionary<TKey, long> values, TKey key) => values.TryGetValue(key, out long value) ? value : 0;
        static IEnumerable<LootNotification> CurrencyIncreases(Snapshot before)
        {
            if (before == null || !LocalProgression.IsReady) yield break;
            foreach (var pair in LocalProgression.State.Wallet)
            {
                long amount = pair.Value - Value(before.Wallet, pair.Key);
                if (amount > 0) yield return new LootNotification(LootNotificationKind.Currency, pair.Key, 0, amount);
            }
        }
        static void Confirm(Snapshot before, IEnumerable<LootNotification> rewards, string receipt)
        {
            if (before == null || before.Published || !LocalProgression.IsReady || before.Generation != LocalProgression.AccountGeneration) return;
            before.Published = true;
            string key = receipt ?? "commit:" + before.Sequence;
            if (!Receipts.Add(key)) return;
            ReceiptOrder.Enqueue(key);
            while (ReceiptOrder.Count > ReceiptCapacity) Receipts.Remove(ReceiptOrder.Dequeue());
            var state = LocalProgression.State;
            var equipment = new Dictionary<int, long>();
            var budget = new Dictionary<(LootNotificationKind, eCurrency, int, bool), long>();
            bool equipmentCounted = false;
            foreach (var reward in rewards ?? Enumerable.Empty<LootNotification>())
            {
                var item = reward;
                if (item.Amount <= 0) continue;
                // 우편 보관은 가방 증가가 없으므로 서버의 명시 disposition과 같은 receipt의 중복 방지로 확인한다.
                if (item.Kind == LootNotificationKind.Equipment && item.Disposition == EquipmentDisposition.Mail) { Publish(item); continue; }
                var id = (item.Kind, item.Currency, item.DefinitionId, item.IsFragment);
                if (!budget.TryGetValue(id, out long available))
                {
                    if (item.Kind == LootNotificationKind.Currency) available = Value(state.Wallet, item.Currency) - Value(before.Wallet, item.Currency);
                    else if (item.Kind == LootNotificationKind.Equipment)
                    {
                        if (!equipmentCounted) { CountEquipment(state, equipment); equipmentCounted = true; }
                        available = before.EquipmentCaptured ? Value(equipment, item.DefinitionId) - Value(before.Equipment, item.DefinitionId) : 0;
                    }
                    else if (item.IsFragment) available = before.MageCaptured && state.MageSkills.TryGetValue(item.DefinitionId, out var skill) ? skill.Fragments - Value(before.Fragments, item.DefinitionId) : 0;
                    else available = before.MageCaptured && !before.Skills.Contains(item.DefinitionId) && state.MageSkills.ContainsKey(item.DefinitionId) ? 1 : 0;
                }
                item.Amount = Math.Min(item.Amount, Math.Max(0, available));
                budget[id] = Math.Max(0, available - item.Amount);
                if (item.Amount > 0) Publish(item);
            }
        }
        static IEnumerable<LootNotification> Gacha(IEnumerable<GachaRewardDTO> rewards)
        {
            foreach (var r in rewards ?? Enumerable.Empty<GachaRewardDTO>())
            {
                if (r == null || r.Amount < 0) continue;
                if (r.Kind == RewardKind.Currency && r.CurrencyType.HasValue)
                    yield return new LootNotification(LootNotificationKind.Currency, (eCurrency)(int)r.CurrencyType.Value, 0, r.Amount);
                else if (r.Kind == RewardKind.Equipment && r.Disposition == EquipmentDisposition.AutoDismantled)
                    yield return new LootNotification(LootNotificationKind.Currency, eCurrency.EquipmentStone, 0, r.GrantedEquipmentStone, r.Disposition);
                else if (r.Kind == RewardKind.Equipment)
                    // 기존 장비 DTO의 생략된 수량은 1개다. 우편은 보유 증가로 검증할 수 없어 명시 수량만 허용한다.
                    yield return new LootNotification(LootNotificationKind.Equipment, 0, r.ItemCode.HasValue ? (int)r.ItemCode.Value.GetItemCode() : r.DefinitionId ?? 0,
                        r.Disposition == EquipmentDisposition.Mail ? r.Amount : Math.Max(1, r.Amount), r.Disposition);
                else if (r.Kind == RewardKind.Skill && r.DefinitionId.HasValue)
                    yield return new LootNotification(LootNotificationKind.MageSkill, 0, r.DefinitionId.Value, r.IsDuplicate ? r.GrantedFragments : Math.Max(1, r.Amount), r.Disposition, r.IsDuplicate);
            }
        }
        static void Observe(Action publish)
        {
            try { publish(); }
            catch (Exception exception) { Warn(exception); }
        }
        static void Warn(Exception exception) => Debug.LogWarning("[LootNotifications] 표시를 생략했습니다: " + exception.GetType().Name);
        public static void PublishGacha(Snapshot before, IEnumerable<GachaRewardDTO> rewards, string receipt = null) => Observe(() => Confirm(before, Gacha(rewards), receipt));
        public static void PublishHunt(Snapshot before, IEnumerable<GachaRewardDTO> drops) => Observe(() => Confirm(before, CurrencyIncreases(before).Concat(Gacha(drops)), null));
        public static void PublishCurrencyIncreases(Snapshot before, string receipt = null) => Observe(() => Confirm(before, CurrencyIncreases(before), receipt));
        public static void PublishCurrency(Snapshot before, eCurrency currency, long amount, string receipt = null) =>
            Observe(() => Confirm(before, new[] { new LootNotification(LootNotificationKind.Currency, currency, 0, amount) }, receipt));
        public static void PublishCurrencies(Snapshot before, IEnumerable<CurrencyAmountDTO> rewards, string receipt = null) =>
            Observe(() => Confirm(before, (rewards ?? Enumerable.Empty<CurrencyAmountDTO>()).Where(r => r != null).Select(r => new LootNotification(LootNotificationKind.Currency, (eCurrency)(int)r.CurrencyType, 0, r.Amount)), receipt));
        public static void PublishRewards(Snapshot before, IEnumerable<QuestRewardDTO> rewards, string receipt = null) => Observe(() => Quest(before, rewards, receipt));
        public static void PublishBattle(Snapshot before, IEnumerable<QuestRewardDTO> rewards, string receipt = null) => Observe(() => Quest(before, rewards, receipt, true));
        static void Quest(Snapshot before, IEnumerable<QuestRewardDTO> rewards, string receipt, bool currencyIncrease = false)
        {
            if (before == null || before.Generation != LocalProgression.AccountGeneration || !LocalProgression.IsReady) return;
            var entries = currencyIncrease ? CurrencyIncreases(before).ToList() : new List<LootNotification>();
            foreach (var r in rewards ?? Enumerable.Empty<QuestRewardDTO>())
            {
                if (r == null || r.Amount <= 0 || r.Amount > long.MaxValue || r.Amount != decimal.Truncate(r.Amount)) continue;
                if (r.Kind == RewardKind.Currency && r.CurrencyType.HasValue) entries.Add(new LootNotification(LootNotificationKind.Currency, (eCurrency)(int)r.CurrencyType.Value, 0, (long)r.Amount));
                else if (r.Kind == RewardKind.Equipment && r.DefinitionId.HasValue) entries.Add(new LootNotification(LootNotificationKind.Equipment, 0, r.DefinitionId.Value, (long)r.Amount));
                else if (r.Kind == RewardKind.Skill && r.DefinitionId.HasValue)
                {
                    entries.Add(new LootNotification(LootNotificationKind.MageSkill, 0, r.DefinitionId.Value, 1));
                    if (LocalProgression.State.MageSkills.TryGetValue(r.DefinitionId.Value, out var skill))
                        entries.Add(new LootNotification(LootNotificationKind.MageSkill, 0, r.DefinitionId.Value, Math.Max(0, skill.Fragments - Value(before.Fragments, r.DefinitionId.Value)), isFragment: true));
                }
            }
            Confirm(before, entries, receipt);
        }
    }
}
