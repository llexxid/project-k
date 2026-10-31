#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using KingdomIdle.Balance;
using KingdomIdle.Rewards;
using Newtonsoft.Json;
using PlayFab;
using Protocol.Message.Dto;
using Scripts.Core.Manager;

namespace KingdomIdle.Mailbox.Testing
{
    public static class MailboxAcceptance
    {
        internal sealed class Fixture
        {
            internal readonly List<MailDTO> Mails = new();
            internal readonly Queue<Action> Lists = new(), Claims = new();
            internal int ListCalls, ClaimCalls, Applied, Recovered, FailListAt, FailClaimAt;
            internal bool HoldLists, HoldClaims, DuplicateOnce, DuplicateAlways, FailApply, RecoveryOk = true, CommitThenTimeout;
            internal long Generation = 1, Gold, AppliedGold, Time = 1793400000;
            internal double Clock;
            internal MailboxService Service;
            internal MailboxClaimResult Result;

            internal Fixture(int count)
            {
                for (int i = 0; i < count; i++) Mails.Add(Mail(9000000000000L + i, Time));
                Service = new MailboxService(Get, Claim, r =>
                {
                    if (FailApply) return false;
                    Applied++; AppliedGold = r.Currency.Gold; return true;
                }, done => { Recovered++; if (RecoveryOk) AppliedGold = Gold; done(RecoveryOk); },
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
                    ok(new OnClaimMailRewardResponseDTO { MailboxId = "box", ClaimedMailIds = r.MailIds.ToList(),
                        RemainingCount = Mails.Count, Rewards = rewards, Currency = new CurrencyQueryDTO(Gold, 0, 0, 0),
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
                var f = new Fixture(100); f.Service.Open(); f.Service.ClaimAll();
                Check(f.ClaimCalls == 2 && f.Applied == 2 && f.AppliedGold == 1000 && f.Result.Rewards.Count == 100 && f.Service.Messages.Count == 0, "100 claims split 50+50, absolute wallet, refresh");
            }
            {
                var f = new Fixture(100) { FailClaimAt = 2 }; f.Service.Open(); f.Service.ClaimAll();
                Check(f.Applied == 1 && f.AppliedGold == 500 && f.Result.Rewards.Count == 50 && f.Result.Message != null && f.Service.Messages.Count == 50, "partial success retained");
            }
            {
                var f = new Fixture(51) { FailListAt = 2 }; f.Service.Open();
                Check(!f.Service.HasSnapshot && !f.Service.CanClaim && f.Service.Error != null, "partial page is not a complete mailbox");
                f.Service.Refresh(); Check(f.Service.Messages.Count == 51 && f.Service.CanClaim, "retry list");
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
                var f = new Fixture(1) { HoldClaims = true }; f.Service.Open(); long id = f.Service.Messages[0].MailId;
                f.Service.Claim(id); f.Service.Claim(id); Check(f.ClaimCalls == 1, "duplicate click blocked with long ID");
                f.Service.Close(); f.Claims.Dequeue()();
                Check(f.Applied == 1 && f.AppliedGold == 10 && f.Result == null && f.Service.Messages.Count == 0, "closed panel still applies reward without popup/cache");
            }
            {
                var f = new Fixture(1) { HoldClaims = true }; f.Service.Open(); f.Service.ClaimAll(); f.Service.Close(); f.Service.Open();
                Check(!f.Service.CanClaim, "reopen waits for previous claim");
                f.Claims.Dequeue()(); Check(f.Applied == 1 && f.Result == null && f.Service.HasSnapshot, "reopened view receives fresh list, no stale popup");
            }
            {
                var f = new Fixture(1) { HoldClaims = true }; f.Service.Open(); f.Service.ClaimAll(); f.Generation++; f.Service.CheckAccount();
                f.Claims.Dequeue()(); Check(f.Applied == 0 && f.Result == null, "old account cannot receive callback reward");
            }
            {
                var f = new Fixture(1) { CommitThenTimeout = true }; f.Service.Open(); f.Service.ClaimAll();
                Check(f.ClaimCalls == 1 && f.Recovered == 1 && f.AppliedGold == 10 && f.Result.Rewards.Count == 0 && f.Service.Messages.Count == 0, "lost success reconciles without regrant/success popup");
            }
            {
                var f = new Fixture(1) { CommitThenTimeout = true, RecoveryOk = false }; f.Service.Open(); f.Service.ClaimAll();
                Check(!f.Service.CanClaim && f.Service.Error != null, "failed reconciliation blocks claims");
                f.RecoveryOk = true; f.Service.Refresh();
                Check(f.Recovered == 2 && f.Service.HasSnapshot && f.ClaimCalls == 1, "retry recovers, never retries claim");
            }
            {
                var f = new Fixture(1) { FailApply = true }; f.Service.Open(); f.Service.ClaimAll();
                Check(f.Applied == 0 && f.Recovered == 1 && f.AppliedGold == 10 && f.Result.Rewards.Count == 0, "failed local apply reconciles authoritative wallet");
            }
            {
                var f = new Fixture(1); f.Service.Open(); f.Clock = 86401; f.Service.ClaimAll();
                Check(f.ClaimCalls == 0, "estimated server time blocks expired mail");
                var g = new Fixture(1); g.Mails[0].Rewards[0].Amount = -1; g.Service.Open();
                Check(!g.Service.HasSnapshot && g.Applied == 0, "invalid reward rejected before UI/apply");
            }
            {
                var f = new Fixture(1); f.Service.Open(); f.Mails.Clear(); f.Service.ClaimAll();
                Check(f.Applied == 0 && f.Recovered == 0 && f.Service.Messages.Count == 0 && f.Result.Message != null, "already claimed business failure refreshes without local reward");
            }
            return new Dictionary<string, object> { ["passed"] = checks.Count, ["checks"] = checks };
        }

        public static Dictionary<string, object> RunLocal()
        {
            if (UnityEngine.Application.isPlaying) throw new InvalidOperationException("격리 저장 검증은 Play를 종료한 뒤 실행해 주세요.");
            string beforeAccount = LocalProgression.AccountKey;
            string before = LocalProgression.TryGetCommittedState(out var previous) ? JsonConvert.SerializeObject(previous) : null;
            var checks = new List<string>();
            void Check(bool ok, string label) { if (!ok) throw new InvalidOperationException("LOCAL MAIL ASSERT: " + label); checks.Add(label); }
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
