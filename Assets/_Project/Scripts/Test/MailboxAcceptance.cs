#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KingdomIdle.Balance;
using KingdomIdle.Rewards;
using Newtonsoft.Json;
using PlayFab;
using Protocol.Message.Dto;
using Scripts.Core;
using Scripts.Core.Manager;

namespace KingdomIdle.Mailbox.Testing
{
    public static class MailboxAcceptance
    {
        internal sealed class Fixture
        {
            internal readonly List<MailDTO> Mails = new();
            internal readonly Queue<Action> Lists = new(), Claims = new(), Recoveries = new();
            internal int ListCalls, ClaimCalls, Applied, Recovered, FailListAt, FailClaimAt;
            internal bool HoldLists, HoldClaims, HoldRecovery, WrongClaimId, InvalidClaim, DuplicateOnce, DuplicateAlways, FailApply, RecoveryOk = true, CommitThenTimeout, CommitThenStorageFailure;
            internal long Generation = 1, Gold, AppliedGold, Time = 1793400000;
            internal double Clock;
            internal MailboxService Service;
            internal MailClaimInfo Result;

            internal Fixture(int count)
            {
                for (int i = 0; i < count; i++) Mails.Add(Mail(9000000000000L + i, Time));
                Service = new MailboxService(Get, Claim, r =>
                {
                    if (FailApply) return false;
                    Applied++; AppliedGold = r.Currency.Gold; return true;
                }, done => { Recovered++; void Complete() { if (RecoveryOk) AppliedGold = Gold; done(RecoveryOk); }
                    if (HoldRecovery) Recoveries.Enqueue(Complete); else Complete(); },
                    () => (Generation, Generation), () => true, () => Clock);
                Service.ClaimCompleted += r => Result = r;
            }
            private void Get(OnGetMailListRequestDTO r, Action<OnGetMailListResponseDTO> ok, Action<PlayFabError> fail)
            {
                int call = ++ListCalls;
                void Respond()
                {
                    if (call == FailListAt) { fail(Failure("Transport")); return; }
                    var page = Mails.Skip(r.StartIndex).Take(r.Count).ToList();
                    if (r.StartIndex == 50 && (DuplicateOnce || DuplicateAlways) && page.Count > 0)
                    { page[0] = Mails[0]; DuplicateOnce = false; }
                    ok(new OnGetMailListResponseDTO { MailboxId = "box", StartIndex = r.StartIndex,
                        TotalCount = Mails.Count, Mails = page, ServerTimeUtc = Time });
                }
                if (HoldLists) Lists.Enqueue(Respond); else Respond();
            }
            private void Claim(OnClaimMailRewardRequestDTO r, Action<OnClaimMailRewardResponseDTO> ok, Action<PlayFabError> fail)
            {
                int call = ++ClaimCalls;
                void Respond()
                {
                    if (call == FailClaimAt) { fail(Failure("Business")); return; }
                    var selected = Mails.Where(m => r.MailIds.Contains(m.MailId)).ToList();
                    if (selected.Count != r.MailIds.Count || selected.Any(m => m.ExpiresUtc <= Time)) { fail(Failure("Business")); return; }
                    var rewards = selected.SelectMany(m => m.Rewards).Select((r, i) => new GachaRewardDTO
                    { Order = i, Kind = r.Kind, CurrencyType = r.CurrencyType, DefinitionId = r.DefinitionId,
                        Amount = r.Amount, Disposition = r.Kind == RewardKind.Currency ? EquipmentDisposition.None : EquipmentDisposition.AutoDismantled,
                        GrantedEquipmentStone = r.Kind == RewardKind.Equipment ? 10 * r.Amount : 0 }).ToList();
                    Gold += rewards.Where(x => x.Kind == RewardKind.Currency && x.CurrencyType == Protocol.Message.Data.eCurrency.Gold).Sum(x => x.Amount);
                    Mails.RemoveAll(m => r.MailIds.Contains(m.MailId));
                    if (CommitThenTimeout) { fail(Failure("Timeout")); return; }
                    if (CommitThenStorageFailure)
                    {
                        fail(NetworkManager.CreateError("Business", "Expected storage failure", details: new ErrorResponseDTO
                        { ErrorCode = Protocol.Message.Data.eErrorCode.StorageFailure, Message = "Storage write failed" })); return;
                    }
                    ok(new OnClaimMailRewardResponseDTO { MailboxId = "box", ClaimedMailIds = WrongClaimId ? new List<long> { 123 } : r.MailIds.ToList(),
                        RemainingCount = Mails.Count, Rewards = rewards, Currency = new CurrencyQueryDTO(InvalidClaim ? -1 : Gold, 0, 0, 0),
                        Equipment = Inventory() });
                }
                if (HoldClaims) Claims.Enqueue(Respond); else Respond();
            }
            internal static EquipmentInventoryDTO Inventory() => new() { Items = new(), PendingItems = new(), ReserveStacks = new(), Capacity = 200, PendingCapacity = 100 };
            internal static MailDTO Mail(long id, long now) => new() { MailId = id, TitleKey = "mail.test.title", BodyKey = "mail.test.body",
                Category = MailCategory.System, SentUtc = now - 60, ExpiresUtc = now + 86400,
                Rewards = new() { new MailRewardDTO { Kind = RewardKind.Currency, CurrencyType = Protocol.Message.Data.eCurrency.Gold, Amount = 10 } } };
            internal static PlayFabError Failure(string kind) => NetworkManager.CreateError(kind, "Expected mailbox fixture failure");
        }

        public static Dictionary<string, object> Run()
        {
            var checks = new List<string>();
            void Check(bool ok, string label) { if (!ok) throw new InvalidOperationException("MAIL ASSERT: " + label); checks.Add(label); }
            foreach (int count in new[] { 0, 1, 50, 51, 100 })
            {
                var f = new Fixture(count); f.Service.Open();
                Check(f.Service.HasSnapshot && f.Service.Messages.Count == count && f.ListCalls == (count > 50 ? 2 : 1), "complete list " + count);
                f.Service.Close(); Check(f.Service.Messages.Count == 0 && !f.Service.HasSnapshot, "close clears " + count);
            }
            {
                var f = new Fixture(100); f.Service.Open(); f.Service.TryClaimAll();
                Check(f.ClaimCalls == 2 && f.Applied == 2 && f.AppliedGold == 1000 && f.Result.MailIds.Length == 100 && f.Service.Messages.Count == 0, "100 claims split 50+50, absolute wallet, refresh");
            }
            {
                var f = new Fixture(100) { FailClaimAt = 2 }; f.Service.Open(); f.Service.TryClaimAll();
                Check(f.Applied == 1 && f.AppliedGold == 500 && f.Result.MailIds.Length == 50 && f.Result.Message != null && f.Service.Messages.Count == 50, "partial success retained");
            }
            {
                var f = new Fixture(51) { FailListAt = 2 }; f.Service.Open();
                Check(!f.Service.HasSnapshot && !f.Service.CanClaim && f.Service.Error != null, "partial page is not a complete mailbox");
                f.Service.RequestRefresh(); Check(f.Service.Messages.Count == 51 && f.Service.CanClaim, "retry list");
            }
            {
                var f = new Fixture(51) { DuplicateOnce = true }; f.Service.Open();
                Check(f.ListCalls == 4 && f.Service.Messages.Count == 51, "restart from zero after shifted pages");
                var g = new Fixture(51) { DuplicateAlways = true }; g.Service.Open();
                Check(g.ListCalls == 4 && !g.Service.HasSnapshot && g.Service.Error != null, "bounded consistency retry");
            }
            {
                var f = new Fixture(1) { HoldLists = true }; f.Service.Open(); f.Service.Close(); f.Service.Open();
                f.Lists.Dequeue()(); Check(!f.Service.HasSnapshot, "old open result ignored");
                f.Lists.Dequeue()(); Check(f.Service.Messages.Count == 1, "new open result accepted");
            }
            {
                var f = new Fixture(51) { HoldLists = true }; f.Service.Open(); f.Lists.Dequeue()();
                f.Mails.RemoveAt(50); f.Lists.Dequeue()(); f.Lists.Dequeue()();
                Check(f.ListCalls == 3 && f.Service.Messages.Count == 50 && f.Service.HasSnapshot, "count change between pages restarts from zero");
                var g = new Fixture(1) { HoldLists = true }; g.Service.Open(); g.Generation++; g.Service.CheckAccount();
                g.Lists.Dequeue()(); Check(!g.Service.HasSnapshot, "old account list ignored");
                g.Lists.Dequeue()(); Check(g.Service.HasSnapshot, "new account list accepted");
            }
            {
                var f = new Fixture(51) { HoldClaims = true }; f.Service.Open(); f.Service.TryClaimAll();
                f.Mails.Add(Fixture.Mail(9000000009999L, f.Time));
                f.Claims.Dequeue()(); f.Claims.Dequeue()();
                Check(f.ClaimCalls == 2 && f.AppliedGold == 510 && f.Service.Messages.Single().MailId == 9000000009999L,
                    "claim all fixes IDs at click time and leaves newly arrived mail");
            }
            {
                var f = new Fixture(1) { HoldClaims = true }; f.Service.Open(); long id = f.Service.Messages[0].MailId;
                f.Service.TryClaim(id); f.Service.TryClaim(id); Check(f.ClaimCalls == 1, "duplicate click blocked with long ID");
                f.Service.Close(); f.Claims.Dequeue()();
                Check(f.Applied == 1 && f.AppliedGold == 10 && f.Result == null && f.Service.Messages.Count == 0, "closed panel still applies reward without popup/cache");
            }
            {
                var f = new Fixture(1) { HoldClaims = true }; f.Service.Open(); f.Service.TryClaimAll(); f.Service.Close(); f.Service.Open();
                Check(!f.Service.CanClaim, "reopen waits for previous claim");
                f.Claims.Dequeue()(); Check(f.Applied == 1 && f.Result == null && f.Service.HasSnapshot, "reopened view receives fresh list, no stale popup");
            }
            {
                var f = new Fixture(1) { HoldClaims = true }; f.Service.Open(); f.Service.TryClaimAll(); f.Generation++; f.Service.CheckAccount();
                f.Claims.Dequeue()(); Check(f.Applied == 0 && f.Result == null, "old account cannot receive callback reward");
            }
            {
                var f = new Fixture(1) { CommitThenTimeout = true }; f.Service.Open(); f.Service.TryClaimAll();
                Check(f.ClaimCalls == 1 && f.Recovered == 1 && f.AppliedGold == 10 && f.Result.Rewards.Count == 0 && f.Service.Messages.Count == 0, "lost success reconciles without regrant/success popup");
            }
            {
                var f = new Fixture(1) { CommitThenStorageFailure = true }; f.Service.Open(); f.Service.TryClaimAll();
                Check(f.ClaimCalls == 1 && f.Recovered == 1 && f.AppliedGold == 10 && f.Result.Rewards.Count == 0,
                    "server storage error reconciles even when wrapped as Business");
            }
            {
                var f = new Fixture(1) { CommitThenTimeout = true, RecoveryOk = false }; f.Service.Open(); f.Service.TryClaimAll();
                Check(!f.Service.CanClaim && f.Service.Error != null, "failed reconciliation blocks claims");
                f.RecoveryOk = true; f.Service.RequestRefresh();
                Check(f.Recovered == 2 && f.Service.HasSnapshot && f.ClaimCalls == 1, "retry recovers, never retries claim");
            }
            {
                var f = new Fixture(1) { FailApply = true }; f.Service.Open(); f.Service.TryClaimAll();
                Check(f.Applied == 0 && f.Recovered == 1 && f.AppliedGold == 10 && f.Result.Rewards.Count == 0, "failed local apply reconciles authoritative wallet");
            }
            {
                var f = new Fixture(1); f.Service.Open(); f.Clock = 86401; f.Service.TryClaimAll();
                Check(f.ClaimCalls == 0, "estimated server time blocks expired mail");
                var g = new Fixture(1); g.Mails[0].Rewards[0].Amount = -1; g.Service.Open();
                Check(!g.Service.HasSnapshot && g.Applied == 0, "invalid reward rejected before UI/apply");
            }
            {
                var f = new Fixture(1); f.Service.Open(); f.Mails.Clear(); f.Service.TryClaimAll();
                Check(f.Applied == 0 && f.Recovered == 0 && f.Service.Messages.Count == 0 && f.Result.Message != null, "already claimed business failure refreshes without local reward");
            }
            {
                var f = new Fixture(1) { FailListAt = 1 }; f.Service.Open();
                f.Clock = 29; f.Service.Tick(); Check(f.ListCalls == 1, "retry waits 30 seconds");
                f.Clock = 30; f.Service.Tick(); Check(f.ListCalls == 2 && f.Service.CanClaim, "open mailbox automatically retries read");
                var g = new Fixture(1) { FailListAt = 1 }; g.Service.Open(); g.Service.Close(); g.Clock = 60; g.Service.Tick();
                Check(g.ListCalls == 1, "closed mailbox does not retry reads");
            }
            {
                var f = new Fixture(1) { HoldLists = true }; f.Service.Open();
                f.Service.RequestRefresh("carryover"); f.Service.RequestRefresh("carryover"); f.Lists.Dequeue()();
                Check(f.ListCalls == 2 && !f.Service.CanClaim, "refresh invalidations coalesce while loading");
                f.Lists.Dequeue()(); Check(f.Service.CanClaim && f.ListCalls == 2, "coalesced refresh publishes only final list");
            }
            {
                var f = new Fixture(1); f.Service.Open(); f.Clock = 86401; f.Service.Tick(); f.Service.Tick();
                Check(f.ListCalls == 2, "one expiry observation schedules one read");
            }
            {
                var f = new Fixture(1) { WrongClaimId = true }; f.Service.Open(); f.Service.TryClaimAll();
                Check(f.Applied == 0 && f.Recovered == 1 && f.Result.MailIds.Length == 0, "mismatched claim IDs recover before apply");
                var g = new Fixture(1) { InvalidClaim = true }; g.Service.Open(); g.Service.TryClaimAll();
                Check(g.Applied == 0 && g.Recovered == 1 && g.Result.Rewards.Count == 0, "invalid response follows production validation and recovery");
            }
            {
                var f = new Fixture(1) { CommitThenTimeout = true, FailListAt = 2 }; f.Service.Open(); f.Service.TryClaimAll();
                Check(f.Service.State == MailboxState.RecoveryRequired && !f.Service.CanClaim, "player recovery alone cannot unlock failed mail reload");
                f.Service.RequestRefresh(); Check(f.Recovered == 2 && f.Service.CanClaim && f.ClaimCalls == 1, "recovery retry checks player and mailbox without re-claim");
            }
            {
                var f = new Fixture(1) { CommitThenTimeout = true, HoldRecovery = true }; f.Service.Open(); f.Service.TryClaimAll();
                f.Generation++; f.Service.CheckAccount(); f.Recoveries.Dequeue()();
                Check(f.Service.State == MailboxState.Ready && f.Service.CanClaim, "old recovery callback cannot overwrite new account state");
                var g = new Fixture(1) { HoldClaims = true, CommitThenTimeout = true }; g.Service.Open(); g.Service.TryClaimAll(); g.Service.Close(); g.Claims.Dequeue()();
                Check(g.Recovered == 1 && g.AppliedGold == 10 && g.Service.Messages.Count == 0 && g.ListCalls == 1, "closed claim recovery updates player without caching mail");
            }
            {
                GachaRewardDTO Gear(EquipmentDisposition disposition, int level = 0, ulong expiry = 0) => new()
                {
                    Kind = RewardKind.Equipment, DefinitionId = 65540, Amount = 1, Disposition = disposition,
                    ItemCode = new Protocol.Message.Dto.ItemCode { Code = (ulong)global::ItemCode.PackInstance(65540, level, 1), ExpireTimesc = expiry }
                };
                var rewards = RewardGrants.FromServer(new[] { Gear(EquipmentDisposition.Inventory), Gear(EquipmentDisposition.Inventory),
                    Gear(EquipmentDisposition.Pending), Gear(EquipmentDisposition.Inventory, 1), Gear(EquipmentDisposition.Inventory, 0, 100) });
                Check(rewards.Count == 4 && rewards[0].Amount == 2, "grouping preserves equipment level expiry and disposition");
                var copied = rewards[2].Copy();
                Check(copied.ServerReward && copied.EquipmentCode?.Code == rewards[2].EquipmentCode?.Code && copied.Disposition == rewards[2].Disposition,
                    "display copy retains server metadata");
                Check(!JsonConvert.SerializeObject(copied).Contains("EquipmentCode") && !JsonConvert.SerializeObject(copied).Contains("Disposition"),
                    "server display metadata is not persisted");
                var auto = Gear(EquipmentDisposition.AutoDismantled); auto.GrantedEquipmentStone = 17;
                var stones = RewardGrants.FromServer(new[] { auto }).Single();
                Check(!stones.IsEquipment && stones.Currency == eCurrency.EquipmentStone && stones.Amount == 17 &&
                    KingdomIdle.UGUI.RewardPresentation.ResultLabel(stones) == "자동 분해", "auto dismantle displays actual stones and label");
            }
            return new Dictionary<string, object> { ["passed"] = checks.Count, ["checks"] = checks };
        }

        public static Dictionary<string, object> RunServerApply()
        {
            if (UnityEngine.Application.isPlaying) throw new InvalidOperationException("격리 저장 검증은 Play를 종료한 뒤 실행해 주세요.");
            string beforeAccount = LocalProgression.AccountKey;
            string before = LocalProgression.TryGetCommittedState(out var previous) ? JsonConvert.SerializeObject(previous) : null;
            var checks = new List<string>();
            void Check(bool ok, string label) { if (!ok) throw new InvalidOperationException("SERVER MAIL ASSERT: " + label); checks.Add(label); }
            var preview = RewardGrants.FromServer(new MailRewardDTO { Kind = RewardKind.Equipment, DefinitionId = 65540, Amount = 2 });
            Check(preview.Disposition == EquipmentDisposition.None && !KingdomIdle.UGUI.RewardPresentation.Description(preview).Contains("보관했습니다"), "unclaimed preview has no storage result");

            using (LocalProgression.BeginTestSession())
            {
                LocalProgression.TestUtcNow = 1793400000;
                string account = "server-mail-" + Guid.NewGuid().ToString("N");
                LocalProgression.OpenTestAccount(account);
                EquipmentStackDTO Stack(int code, int count, int level = 0) => new()
                {
                    ItemCode = new Protocol.Message.Dto.ItemCode { Code = (ulong)global::ItemCode.PackInstance(code, level, count) },
                    EquippedCharacterIndexes = new()
                };
                foreach (var disposition in new[] { EquipmentDisposition.Inventory, EquipmentDisposition.Pending, EquipmentDisposition.Reserve, EquipmentDisposition.AutoDismantled })
                foreach (string mode in new[] { "success", "save failure", "lost response" })
                {
                    string label = disposition + " / " + mode + ": ";
                    var inventory = Fixture.Inventory();
                    var protectedItem = Stack(65540, 1, 3);
                    protectedItem.IsLocked = true; protectedItem.EnhancementStonesSpent = 37;
                    protectedItem.EquippedCharacterIndexes.Add(0);
                    inventory.Items.Add(protectedItem);
                    if (disposition == EquipmentDisposition.Pending || disposition == EquipmentDisposition.Reserve)
                        inventory.Items.Add(Stack(131076, inventory.Capacity - 1));
                    if (disposition == EquipmentDisposition.Reserve)
                        inventory.PendingItems.Add(Stack(131076, inventory.PendingCapacity));
                    inventory.AutoDismantleRarityMask = disposition == EquipmentDisposition.AutoDismantled ? 1 : 0;
                    Check(LocalProgression.Execute("server-mail-fixture", s =>
                    {
                        UserManager.ApplyCurrency(s, new CurrencyQueryDTO(100, 2, 3, 4, 5, 6) { TrainingTome = 7, EquipmentStone = 40 });
                        UserManager.ApplyEquipment(s, inventory); return true;
                    }), label + "seed durable inventory");
                    string protectedId = LocalProgression.State.Equipment.Single(e => e.Player == 0).Id;
                    string seed = JsonConvert.SerializeObject(LocalProgression.State), disk = File.ReadAllText(LocalProgression.SnapshotPath);

                    var grantedItem = Stack(65540, 2);
                    if (disposition == EquipmentDisposition.Inventory) inventory.Items.Add(grantedItem);
                    if (disposition == EquipmentDisposition.Pending) inventory.PendingItems.Add(grantedItem);
                    if (disposition == EquipmentDisposition.Reserve)
                        inventory.ReserveStacks.Add(new EquipmentReserveStackDTO { ItemCode = grantedItem.ItemCode, Count = 2 });
                    var mail = Fixture.Mail(9000000000001L, LocalProgression.UtcNow);
                    mail.Rewards.Add(new MailRewardDTO { Kind = RewardKind.Equipment, DefinitionId = 65540, Amount = 2 });
                    var response = new OnClaimMailRewardResponseDTO
                    {
                        MailboxId = "box", ClaimedMailIds = new() { mail.MailId }, RemainingCount = 0, Equipment = inventory,
                        Currency = new CurrencyQueryDTO(110, 2, 3, 4, 5, 6) { TrainingTome = 7, EquipmentStone = disposition == EquipmentDisposition.AutoDismantled ? 60 : 40 },
                        Rewards = new()
                        {
                            new GachaRewardDTO { Order = 0, Kind = RewardKind.Currency, CurrencyType = Protocol.Message.Data.eCurrency.Gold, Amount = 10, Disposition = EquipmentDisposition.None },
                            new GachaRewardDTO { Order = 1, Kind = RewardKind.Equipment, DefinitionId = 65540, Amount = 2, ItemCode = grantedItem.ItemCode,
                                Disposition = disposition, GrantedEquipmentStone = disposition == EquipmentDisposition.AutoDismantled ? 20 : 0 }
                        }
                    };
                    response = JsonConvert.DeserializeObject<OnClaimMailRewardResponseDTO>(JsonConvert.SerializeObject(response));
                    bool claimed = false;
                    int claimCalls = 0, recoveryCalls = 0;
                    Action<bool> recoveryDone = null;
                    MailClaimInfo result = null;
                    var service = new MailboxService((r, ok, fail) => ok(new OnGetMailListResponseDTO
                    {
                        MailboxId = "box", StartIndex = r.StartIndex, TotalCount = claimed ? 0 : 1,
                        Mails = claimed ? new() : new() { mail }, ServerTimeUtc = LocalProgression.UtcNow
                    }), (r, ok, fail) =>
                    {
                        claimCalls++; claimed = true;
                        if (mode == "lost response") fail(Fixture.Failure("Timeout")); else ok(response);
                    }, MailboxService.Apply, done => { recoveryCalls++; recoveryDone = done; },
                        () => (1L, LocalProgression.AccountGeneration), () => LocalProgression.IsReady, () => 0);
                    service.ClaimCompleted += r => result = r;
                    service.Open();
                    if (mode == "save failure") LocalProgression.TestFailedCommit(() => { service.TryClaimAll(); return true; });
                    else service.TryClaimAll();
                    if (mode != "success")
                    {
                        Check(JsonConvert.SerializeObject(LocalProgression.State) == seed && File.ReadAllText(LocalProgression.SnapshotPath) == disk,
                            label + "failure leaves memory and file unchanged");
                        service.TryClaimAll();
                        Check(!service.CanClaim && claimCalls == 1 && recoveryCalls == 1 && result.Rewards.Count == 0,
                            label + "recovery blocks claims and suppresses success reward");
                        recoveryDone(false);
                        Check(!service.CanClaim && service.Error != null, label + "failed recovery stays blocked");
                        service.RequestRefresh();
                        // The transport/recovery callback is controlled; snapshot application and disk writes are real.
                        recoveryDone(MailboxService.Apply(response));
                        Check(recoveryCalls == 2 && claimCalls == 1 && service.CanClaim, label + "recovery retry never resends claim");
                    }
                    else Check(result.Rewards.Count == 2 && result.Message == null && recoveryCalls == 0, label + "success publishes server rewards");
                    var state = LocalProgression.State;
                    Check(state.Wallet[eCurrency.Gold] == 110 && state.Wallet[eCurrency.EquipmentStone] == response.Currency.EquipmentStone &&
                        state.Wallet[eCurrency.AncientCoin] == 2 && state.Wallet[eCurrency.ArcaneKnowledge] == 3 && state.Wallet[eCurrency.ClassFragment] == 4 &&
                        state.Wallet[eCurrency.Ruby] == 5 && state.Wallet[eCurrency.KingdomSupply] == 6 && state.Wallet[eCurrency.TrainingTome] == 7,
                        label + "all eight currencies match absolute snapshot");
                    string counts = disposition switch { EquipmentDisposition.Inventory => "3/0/0", EquipmentDisposition.Pending => "200/2/0", EquipmentDisposition.Reserve => "200/100/2", _ => "1/0/0" };
                    Check(UserManager.DescribeInventory(state) == counts && state.AutoDismantleMask == inventory.AutoDismantleRarityMask,
                        label + "equipment storage counts " + counts);
                    Check(state.Equipment.Count(e => e.Code == 65540 && e.Level == 0) == (disposition == EquipmentDisposition.Inventory ? 2 : 0) &&
                        state.PendingEquipment.Count(e => e.Code == 65540 && e.Level == 0) == (disposition == EquipmentDisposition.Pending ? 2 : 0) &&
                        state.LegacyEquipment.Where(e => e.Code == 65540 && e.Level == 0).Sum(e => e.Count) == (disposition == EquipmentDisposition.Reserve ? 2 : 0),
                        label + "received definition reaches only its server-assigned storage");
                    var retained = state.Equipment.Single(e => e.Player == 0);
                    Check(retained.Id == protectedId && retained.Code == 65540 && retained.Level == 3 && retained.Locked && retained.EnhancementStonesSpent == 37,
                        label + "existing ID, lock, equip and enhancement cost retained");
                    string equipment = JsonConvert.SerializeObject(new { state.Equipment, state.PendingEquipment, state.LegacyEquipment, state.Wallet });
                    Check(MailboxService.Apply(response) && equipment == JsonConvert.SerializeObject(new
                        { LocalProgression.State.Equipment, LocalProgression.State.PendingEquipment, LocalProgression.State.LegacyEquipment, LocalProgression.State.Wallet }),
                        label + "repeated snapshot neither duplicates rewards nor changes IDs");
                    service.TryClaimAll();
                    Check(claimCalls == 1 && service.Messages.Count == 0, label + "claimed mail cannot be requested again");
                    service.Close();
                    string saved = JsonConvert.SerializeObject(LocalProgression.State);
                    LocalProgression.OpenTestAccount(account);
                    Check(saved == JsonConvert.SerializeObject(LocalProgression.State), label + "reload reads the same durable state");
                }
            }
            Check(LocalProgression.AccountKey == beforeAccount && (before == null ? !LocalProgression.TryGetCommittedState(out _) : JsonConvert.SerializeObject(LocalProgression.State) == before),
                "original account and state restored");
            return new Dictionary<string, object> { ["passed"] = checks.Count, ["checks"] = checks };
        }

        public static Dictionary<string, object> RunLocal()
        {
            if (UnityEngine.Application.isPlaying) throw new InvalidOperationException("격리 저장 검증은 Play를 종료한 뒤 실행해 주세요.");
            string beforeAccount = LocalProgression.AccountKey;
            string before = LocalProgression.TryGetCommittedState(out var previous) ? JsonConvert.SerializeObject(previous) : null;
            var checks = new List<string>();
            void Check(bool ok, string label) { if (!ok) throw new InvalidOperationException("LOCAL MAIL ASSERT: " + label); checks.Add(label); }
            bool rejected = false;
            try { new LocalMailboxService().GetSnapshot(); } catch (InvalidOperationException) { rejected = true; }
            Check(rejected, "local mail service rejects the real account");
            using (LocalProgression.BeginTestSession())
            {
                LocalProgression.TestUtcNow = 1793400000;
                LocalProgression.OpenTestAccount("mail-" + Guid.NewGuid().ToString("N"));
                var service = new LocalMailboxService();
                long startGold = LocalProgression.Balance(eCurrency.Gold);
                var mail = new MailMessage { Id = "fixture", Title = "격리 우편", Sender = "테스트", Body = "격리 지급", CreatedUtc = LocalProgression.UtcNow - 1,
                    ExpiresUtc = LocalProgression.UtcNow + 100, Rewards = new() { new RewardGrant(eCurrency.Gold, 10) } };
                Check(service.TryAddMail(mail).Succeeded, "local add");
                Check(!LocalProgression.TestFailedCommit(() => service.TryClaimAll(service.GetSnapshot()).Succeeded) && LocalProgression.Balance(eCurrency.Gold) == startGold, "failed local save grants nothing");
                Check(service.TryClaimAll(service.GetSnapshot()).Succeeded && LocalProgression.Balance(eCurrency.Gold) == startGold + 10, "isolated local claim");
                Check(!service.TryClaimAll(service.GetSnapshot()).Succeeded, "no double local claim");
                Check(service.DeleteClaimed(service.GetSnapshot()).Succeeded && service.GetSnapshot().Messages.Count == 0, "legacy delete preserved");
                Check(!service.TryAddMail(mail).Succeeded, "deleted mail cannot be issued twice");
            }
            Check(LocalProgression.AccountKey == beforeAccount && (before == null || JsonConvert.SerializeObject(LocalProgression.State) == before), "original account and state restored");
            return new Dictionary<string, object> { ["passed"] = checks.Count, ["checks"] = checks };
        }
    }
}
#endif
