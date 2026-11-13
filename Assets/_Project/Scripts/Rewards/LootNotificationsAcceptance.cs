#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using KingdomIdle.Balance;
using Protocol.Message.Dto;
using UnityEngine;

namespace KingdomIdle.Rewards
{
    public static class LootNotificationsAcceptance
    {
        public static Dictionary<string, object> Run()
        {
            if (!Application.isPlaying || LocalProgression.IsReady || Scripts.Core.Manager.NetworkManager.Instance != null || Scripts.Core.UserManager.Instance != null)
                throw new InvalidOperationException("빈 씬 PlayMode에서 계정과 네트워크 매니저 없이 실행해 주세요.");
            using var session = LocalProgression.BeginTestSession();
            var clock = typeof(LootNotifications).GetField("_clock", BindingFlags.Static | BindingFlags.NonPublic);
            var filter = typeof(LootNotifications).GetField("_filter", BindingFlags.Static | BindingFlags.NonPublic);
            var oldClock = clock.GetValue(null); var oldFilter = filter.GetValue(null);
            double now = 0;
            var checks = new List<string>();
            void Check(bool ok, string text)
            { if (!ok) throw new InvalidOperationException("Loot notifications: " + text); checks.Add(text); }
            void Write(Action<ProgressionState> change)
            { Check(LocalProgression.Execute("loot-fixture", s => { change(s); return true; }), "fixture transaction committed"); }
            GachaRewardDTO Gear(EquipmentDisposition disposition, long amount) => new()
            { Kind = RewardKind.Equipment, DefinitionId = 65537, Amount = amount, Disposition = disposition };
            IEnumerable<GachaRewardDTO> BrokenRewards()
            { yield return null; throw new InvalidOperationException("fixture iterator"); }
            try
            {
                clock.SetValue(null, (Func<double>)(() => now)); LootNotifications.SetFilter(null); LootNotifications.Clear();
                LocalProgression.OpenTestAccount("loot-feed-a-" + Guid.NewGuid().ToString("N"));
                Write(s => s.Wallet[eCurrency.Gold] = 100);
                var before = LootNotifications.Capture(false, false);
                Write(s => s.Wallet[eCurrency.Gold] += 25);
                LootNotifications.PublishCurrency(before, eCurrency.Gold, 25, "quest:one");
                Check(LootNotifications.TryTake(out var item) && item.Amount == 25 && item.Kind == LootNotificationKind.Currency, "confirmed grant has exact positive amount");
                LootNotifications.PublishCurrency(before, eCurrency.Gold, 25, "quest:one");
                Check(LootNotifications.Count == 0, "same captured commit cannot publish twice");
                before = LootNotifications.Capture(false, false);
                LootNotifications.PublishCurrency(before, eCurrency.Gold, 25, "quest:one");
                Check(LootNotifications.Count == 0, "same server receipt is not republished after consumption");
                before = LootNotifications.Capture(false, false);
                Write(s => s.Wallet[eCurrency.Gold] += 2);
                LootNotifications.PublishCurrency(before, eCurrency.Gold, 500);
                Check(LootNotifications.TryTake(out item) && item.Amount == 2, "notification is bounded by actual committed increase");
                before = LootNotifications.Capture(false, false);
                bool saved = LocalProgression.TestFailedCommit(() => LocalProgression.Execute("loot-failed", s => { s.Wallet[eCurrency.Gold] += 100; return true; }));
                if (saved) LootNotifications.PublishCurrency(before, eCurrency.Gold, 100);
                Check(!saved && LootNotifications.Count == 0 && LocalProgression.Balance(eCurrency.Gold) == 127, "failed commit never publishes or changes balance");
                Write(s => s.Wallet[eCurrency.Gold] += 100);
                Check(LootNotifications.Count == 0, "login or state-refresh style replacement alone produces no loot");
                before = LootNotifications.Capture(false, false);
                LootNotifications.PublishCurrency(before, eCurrency.Gold, 100);
                Check(LootNotifications.Count == 0, "replayed reward with no new balance increase produces no loot");
                before = LootNotifications.Capture(false, false);
                long large = (long)int.MaxValue + 77;
                Write(s => s.Wallet[eCurrency.Gold] += large);
                LootNotifications.PublishCurrency(before, eCurrency.Gold, large);
                Check(LootNotifications.TryTake(out item) && item.Amount == large, "confirmed amounts above int maximum remain exact long values");

                Write(s => s.Equipment.Add(new EquipmentSave { Id = "first", Code = 65537 }));
                before = LootNotifications.Capture();
                Write(s => { s.Equipment[0].Level = 1; s.Equipment[0].Player = 0; });
                LootNotifications.PublishGacha(before, new[] { Gear(EquipmentDisposition.Inventory, 1) });
                Check(LootNotifications.Count == 0, "enhancement and equipment replacement cannot masquerade as acquisition");
                Write(s => s.PendingEquipment.Add(new EquipmentSave { Id = "pending", Code = 65537 }));
                before = LootNotifications.Capture();
                Write(s => { s.Equipment.Add(s.PendingEquipment[0]); s.PendingEquipment.Clear(); });
                LootNotifications.PublishGacha(before, new[] { Gear(EquipmentDisposition.Inventory, 1) });
                Check(LootNotifications.Count == 0, "claim-pending storage move is not acquisition");
                before = LootNotifications.Capture();
                Write(s => s.Equipment.Add(new EquipmentSave { Id = "new", Code = 65537 }));
                LootNotifications.PublishGacha(before, new[] { Gear(EquipmentDisposition.Inventory, 5) });
                Check(LootNotifications.TryTake(out item) && item.Amount == 1 && item.DefinitionId == 65537, "equipment count uses owned quantity rather than rewritten instance IDs");
                before = LootNotifications.Capture();
                Write(s => s.Equipment.Add(new EquipmentSave { Id = "amount-omitted", Code = 65537 }));
                LootNotifications.PublishGacha(before, new[] { Gear(EquipmentDisposition.Inventory, 0) });
                Check(LootNotifications.TryTake(out item) && item.Amount == 1, "omitted equipment amount requires an actual owned increase");
                LootNotifications.PublishGacha(LootNotifications.Capture(), new[] { Gear(EquipmentDisposition.Inventory, 0) });
                Check(LootNotifications.Count == 0, "omitted amount without a new item remains suppressed");
                LootNotifications.PublishGacha(LootNotifications.Capture(false, false), new[] { Gear(EquipmentDisposition.Inventory, 1) });
                Check(LootNotifications.Count == 0, "uncaptured ownership cannot count existing equipment as newly acquired");
                LootNotifications.PublishGacha(LootNotifications.Capture(), new[] { Gear(EquipmentDisposition.Mail, 0), Gear(EquipmentDisposition.Mail, -1) });
                Check(LootNotifications.Count == 0, "mail needs explicit positive quantity and never infers a payout");

                before = LootNotifications.Capture();
                LootNotifications.PublishGacha(before, new[] { Gear(EquipmentDisposition.Mail, 3) }, "mailed:first");
                Check(LootNotifications.TryTake(out item) && item.Amount == 3 && item.Disposition == EquipmentDisposition.Mail, "server-confirmed mailed loot preserves quantity despite a full bag");
                LootNotifications.ClearPending();
                LootNotifications.PublishGacha(LootNotifications.Capture(), new[] { Gear(EquipmentDisposition.Mail, 3) }, "mailed:first");
                Check(LootNotifications.Count == 0, "UI clear does not forget a mailed reward receipt");
                before = LootNotifications.Capture(false, false);
                Write(s => s.Wallet[eCurrency.EquipmentStone] = 12);
                LootNotifications.PublishGacha(before, new[] { new GachaRewardDTO { Kind = RewardKind.Equipment, Disposition = EquipmentDisposition.AutoDismantled, GrantedEquipmentStone = 12 } });
                Check(LootNotifications.TryTake(out item) && item.Kind == LootNotificationKind.Currency && item.Currency == eCurrency.EquipmentStone && item.Amount == 12, "auto dismantle shows received stones rather than nonexistent equipment");
                before = LootNotifications.Capture();
                Write(s => s.MageSkills[0] = new MageSave { Fragments = 30 });
                LootNotifications.PublishGacha(before, new[] {
                    new GachaRewardDTO { Kind = RewardKind.Skill, DefinitionId = 0, Amount = 0 },
                    new GachaRewardDTO { Kind = RewardKind.Skill, DefinitionId = 0, Amount = 1, IsDuplicate = true, GrantedFragments = 30 } });
                Check(LootNotifications.TryTake(out var skill) && LootNotifications.TryTake(out var fragments) && skill.Amount == 1 && !skill.IsFragment && fragments.Amount == 30 && fragments.IsFragment, "new skill and duplicate fragments remain separate units");
                LootNotifications.PublishGacha(LootNotifications.Capture(), new[] { new GachaRewardDTO { Kind = (RewardKind)999, Amount = 4 } });
                Check(LootNotifications.Count == 0, "unknown reward DTO kind is ignored safely");
                long balance = LocalProgression.Balance(eCurrency.Gold);
                LootNotifications.PublishGacha(LootNotifications.Capture(), BrokenRewards());
                Check(LootNotifications.Count == 0 && LocalProgression.Balance(eCurrency.Gold) == balance, "invalid reward iterator cannot escape or disturb committed balance");

                LootNotifications.Clear();
                LootNotifications.Publish(new LootNotification(LootNotificationKind.Currency, eCurrency.Gold, 0, long.MaxValue));
                LootNotifications.Publish(new LootNotification(LootNotificationKind.Currency, eCurrency.Gold, 0, 1));
                Check(LootNotifications.Count == 2 && LootNotifications.TryTake(out item) && item.Amount == long.MaxValue && LootNotifications.TryTake(out item) && item.Amount == 1, "merge overflow never wraps or truncates quantities");
                for (int i = 0; i < 65; i++) LootNotifications.Publish(new LootNotification(LootNotificationKind.Equipment, 0, 100 + i, 1));
                Check(LootNotifications.Count == 64 && LootNotifications.TryTake(out item) && item.DefinitionId == 101, "queue is bounded to 64 and evicts oldest display only");
                LootNotifications.ClearPending();
                LootNotifications.Publish(new LootNotification(LootNotificationKind.Currency, eCurrency.Gold, 0, 1));
                LootNotifications.Publish(new LootNotification(LootNotificationKind.Currency, eCurrency.Gold, 0, 2));
                Check(LootNotifications.Count == 1 && LootNotifications.TryTake(out item) && item.Amount == 3, "same kind identity and disposition merge exactly");
                LootNotifications.Publish(new LootNotification(LootNotificationKind.Currency, eCurrency.Gold, 0, 1)); now = 30;
                Check(LootNotifications.Count == 0, "notifications expire after 30 seconds of unscaled time");
                LootNotifications.SetFilter(_ => false);
                Check(!LootNotifications.Publish(new LootNotification(LootNotificationKind.Currency, eCurrency.Gold, 0, 1)), "arrival filter rejects disabled loot");
                LootNotifications.SetFilter(null);
                LootNotifications.Publish(new LootNotification(LootNotificationKind.Currency, eCurrency.Gold, 0, 1));
                LootNotifications.SetFilter(_ => false);
                Check(LootNotifications.Count == 0, "filter changes prune existing queued loot without a second queue");
                LootNotifications.SetFilter(_ => throw new InvalidOperationException("fixture filter"));
                Check(!LootNotifications.Publish(new LootNotification(LootNotificationKind.Currency, eCurrency.Gold, 0, 1)), "observer failure does not escape into payout callbacks");
                LootNotifications.SetFilter(null);
                LootNotifications.PublishGacha(LootNotifications.Capture(), new[] { Gear(EquipmentDisposition.Mail, 3) }, "account-shared");
                Check(LootNotifications.TryTake(out item) && item.Amount == 3, "account receipt is recorded before switching accounts");
                LootNotifications.Publish(new LootNotification(LootNotificationKind.Currency, eCurrency.Gold, 0, 1));
                Check(LootNotifications.Count == 1, "a queued notification exists before switching accounts");
                var old = LootNotifications.Capture();
                LocalProgression.OpenTestAccount("loot-feed-b-" + Guid.NewGuid().ToString("N"));
                Check(LootNotifications.Count == 0, "account change clears queued loot");
                LootNotifications.PublishGacha(old, new[] { Gear(EquipmentDisposition.Mail, 3) }, "mailed:old-account");
                Check(LootNotifications.Count == 0, "late old-account confirmation cannot publish into a new account");
                LootNotifications.PublishGacha(LootNotifications.Capture(), new[] { Gear(EquipmentDisposition.Mail, 3) }, "account-shared");
                Check(LootNotifications.Count == 1, "receipt suppression is account scoped");
                return new Dictionary<string, object> { { "passed", checks.Count }, { "checks", checks } };
            }
            finally
            {
                LootNotifications.Clear(); clock.SetValue(null, oldClock); filter.SetValue(null, oldFilter);
            }
        }
    }
}
#endif
