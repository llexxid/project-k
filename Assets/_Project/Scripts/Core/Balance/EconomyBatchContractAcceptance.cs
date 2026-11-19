#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PlayFab.CloudScriptModels;
using Protocol.Message.Data;
using Protocol.Message.Dto;
using Scripts.Core.Manager;

namespace KingdomIdle.Balance
{
    public static class EconomyBatchContractAcceptance
    {
        private const string Id = "1234567890abcdef1234567890abcdef";

        public static object Run()
        {
            var checks = new List<string>();
            void Check(bool value, string name) { if (!value) throw new InvalidOperationException(name); checks.Add(name); }
            bool Read(JObject body)
            {
                object[] args = { "OnEconomyBatch", new ExecuteFunctionResult { FunctionResult = body.ToString() }, null, null };
                return (bool)typeof(NetworkManager).GetMethod("TryReadResponse", BindingFlags.NonPublic | BindingFlags.Static)
                    .MakeGenericMethod(typeof(OnEconomyBatchResponseDTO)).Invoke(null, args);
            }
            JObject Change(JObject body, string path, object value)
            {
                var copy = (JObject)body.DeepClone(); var token = copy.SelectToken(path);
                if (value == null) ((JProperty)token.Parent).Remove(); else token.Replace(JToken.FromObject(value));
                return copy;
            }
            bool Rejected(Action action) { try { action(); return false; } catch (JsonException) { return true; } }

            var response = Fixture();
            var body = JObject.FromObject(response);
            Check(Read(body), "immutable prefix result and newer current state decode together");
            var quests = (JObject)body.DeepClone();
            quests["Quests"] = JObject.FromObject(new QuestStateDTO
            {
                DailyPeriodKey = "2026-11-19", WeeklyPeriodKey = "2026-11-16", ServerTimeUtc = 1, Rows = new()
            });
            Check(Read(quests), "new economy response can carry an authoritative quest snapshot");
            Check(!Read(Change(quests, "Quests.DailyPeriodKey", "")), "invalid optional quest snapshot is rejected");
            const string wrapped = "Invoking an Azure Function failed with HttpStatus 500 and response body ";
            const string errorHeader = "{\"ProtocolVersion\":2,\"ServerVersion\":1,\"ErrorCode\":1014,";
            Check(NetworkManager.TryParseWrappedError(wrapped + errorHeader + "\"Message\":\"limited\",\"StorageThrottled\":true,\"RetryAfterSeconds\":5}", out var throttle) &&
                NetworkManager.TryGetStorageThrottle(NetworkManager.CreateError("Business", throttle.Message, details: throttle), out int? retry) && retry == 5,
                "storage throttle and delay survive the PlayFab HTTP wrapper");
            Check(NetworkManager.TryParseWrappedError(wrapped + errorHeader + "\"Message\":\"old server\"}", out var oldError) &&
                !NetworkManager.TryGetStorageThrottle(NetworkManager.CreateError("Business", oldError.Message, details: oldError), out _),
                "unclassified storage failure does not enable the live 429 path");
            throttle.RetryAfterSeconds = null;
            Check(NetworkManager.TryGetStorageThrottle(NetworkManager.CreateError("Business", throttle.Message, details: throttle), out int? withoutDelay) && withoutDelay == null,
                "storage throttle without a server delay can use local backoff");
            foreach (string path in new[] { "EconomyRevision", "Receipt", "CurrentState", "Receipt.Results[1].Status", "CurrentState.EconomyBatchState.RewardEpochs" })
                Check(!Read(Change(body, path, null)), "missing recovery evidence rejected: " + path);
            Check(!Read(Change(body, "Receipt.Results[2].Status", 0)), "success after business rejection cannot be applied");
            Check(!Read(Change(body, "Receipt.Results[2].SpentRuby", 20)), "unprocessed command cannot consume currency");
            Check(!Read(Change(body, "Receipt.Results[1].Index", 0)), "duplicate result index cannot delete the wrong input");
            Check(!Read(Change(body, "Receipt.CommittedRevision", 7)), "receipt cannot be newer than current canonical state");
            Check(!Read(Change(body, "CurrentState.EconomyBatchState.RewardEpochs[0].GoldLevel", 1)), "epoch must match actual confirmed ruby growth");
            Check(!Read(Change(body, "CurrentState.EconomyBatchState.GoldRemainderMicros", 1000000)), "invalid fractional reward cannot enter projection");
            Check(!Read(Change(body, "CurrentState.Currency.Gold", 1.5)), "fractional currency cannot truncate on decode");
            const long large = 9007199254740993L;
            var largeBody = Change(Change(body, "EconomyRevision", large + 1), "Receipt.CommittedRevision", large);
            Check(Read(largeBody), "economic revisions preserve integers beyond double precision");

            var request = new OnEconomyBatchRequestDTO
            {
                RequestId = Id, Generation = 1, Sequence = 1, ExpectedEconomyRevision = 3, AppliedRewardEpoch = 1,
                Commands = new()
                {
                    new() { Kind = EconomyCommandKind.GoldAttack, Mode = 1, ExpectedLevel = 0, CountLimit = 1, SpendLimit = 50 },
                    new() { Kind = EconomyCommandKind.GoldHealth, Mode = 1, ExpectedLevel = 0, CountLimit = 1, SpendLimit = 50 },
                    new() { Kind = EconomyCommandKind.RubyGold, Mode = 1, ExpectedLevel = 0, CountLimit = 1, SpendLimit = 20 }
                }
            };
            EconomyBatchProtocol.ValidateRequest(request); EconomyBatchProtocol.MatchRequest(response, request);
            Check(true, "x1 input identities survive mixed growth prefix and rejection");
            request.Commands[0].SpendLimit = 49;
            Check(Rejected(() => EconomyBatchProtocol.MatchRequest(response, request)), "result cannot exceed click spend cap");
            request.Commands[0].SpendLimit = 50; request.Sequence++;
            Check(Rejected(() => EconomyBatchProtocol.MatchRequest(response, request)), "same nonce with wrong sequence is not accepted");
            request.Sequence--; request.Commands[0].Mode = 10;
            Check(Rejected(() => EconomyBatchProtocol.ValidateRequest(request)), "x1 cannot masquerade as a reduced x10 transaction");
            request.Commands[0].ExpectedLevel = 299;
            EconomyBatchProtocol.ValidateRequest(request);
            Check(true, "x10 still caps to the one remaining level");
            return new { passed = checks.Count, checks };
        }

        internal static OnEconomyBatchResponseDTO Fixture()
        {
            var stage = eStage.Stage1_1;
            return new OnEconomyBatchResponseDTO
            {
                ProtocolVersion = IdleKingdomFunction.Common.ApiVersions.ProtocolVersion, EconomyRevision = 5,
                RequestId = Id, Status = EconomyBatchStatus.Completed,
                Receipt = new()
                {
                    RequestId = Id, Generation = 1, Sequence = 1, PayloadHash = new string('a', 64), CommittedRevision = 4,
                    Results = new()
                    {
                        new() { Index = 0, Kind = EconomyCommandKind.GoldAttack, Status = EconomyCommandStatus.Applied, AppliedCount = 1, SpentGold = 50 },
                        new() { Index = 1, Kind = EconomyCommandKind.GoldHealth, Status = EconomyCommandStatus.Rejected, ErrorCode = eErrorCode.eNotEnoughGold },
                        new() { Index = 2, Kind = EconomyCommandKind.RubyGold, Status = EconomyCommandStatus.Unprocessed }
                    }
                },
                CurrentState = new()
                {
                    Currency = new(10, 0, 0, 0, 100, 0), EnchantATKCount = 1, Level = 1,
                    RubyGrowthState = new() { NextRequestSequence = 1, LastResetDay = "" },
                    StageState = new() { CurrentStage = stage, HighestStage = stage },
                    MainBattle = new() { RunId = Id, Stage = stage, AcceptedKills = new() },
                    EconomyBatchState = new()
                    {
                        Version = 1, OwnerClientId = Id, Generation = 1, NextSequence = 2, RewardEpoch = 1,
                        RewardEpochs = new() { new() { Id = 1 } }
                    }
                }
            };
        }

        public static object RunApplyBoundary()
        {
            if (LocalProgression.IsReady || NetworkManager.Instance != null) throw new InvalidOperationException("계정 없는 빈 편집 씬에서 실행해 주세요.");
            using var session = LocalProgression.BeginTestSession();
            LocalProgression.OpenTestAccount("economy-response-" + Guid.NewGuid().ToString("N"));
            LocalProgression.Execute("fixture", s => { s.ServerEconomyRevision = 7; s.Wallet[eCurrency.Gold] = 100; return true; });
            var checks = new List<string>();
            void Check(bool value, string name) { if (!value) throw new InvalidOperationException(name); checks.Add(name); }
            using (var scope = EconomyResponseApply.Begin(new OnEnchantResponseDTO { EconomyRevision = 8 }))
            {
                Check(!LocalProgression.TestFailedCommit(() => LocalProgression.Execute("apply", s => { s.Wallet[eCurrency.Gold] = 50; return true; })), "simulated disk failure rejects economic apply");
                Check(!scope.Complete(true, false) && LocalProgression.State.ServerEconomyRevision == 7 && LocalProgression.Balance(eCurrency.Gold) == 100,
                    "failed write leaves wallet and server revision unchanged");
            }
            using (var scope = EconomyResponseApply.Begin(new OnEnchantResponseDTO { EconomyRevision = 8 }))
            {
                LocalProgression.Execute("rejected", _ => false);
                LocalProgression.Execute("unrelated-followup", s => { s.Modules["test"] = "other"; return true; });
                Check(!scope.Complete(true, false) && LocalProgression.State.ServerEconomyRevision == 7, "later unrelated save cannot hide a failed apply");
            }
            bool observed = false;
            Action observer = () =>
            {
                if (observed) return; observed = true;
                LocalProgression.Execute("changed-observer", s => { s.Modules["observer"] = "visited"; return true; });
            };
            LocalProgression.Changed += observer;
            try
            {
                using var scope = EconomyResponseApply.Begin(new OnEnchantResponseDTO { EconomyRevision = 8 });
                Check(LocalProgression.Execute("apply", s => { s.Wallet[eCurrency.Gold] = 50; return true; }) && scope.Complete(true, false), "durable economic callback records revision");
                Check(observed && LocalProgression.State.ServerEconomyRevision == 8, "Changed reentry cannot consume the response scope");
            }
            finally { LocalProgression.Changed -= observer; }
            using (var scope = EconomyResponseApply.Begin(new OnGetMailListResponseDTO { EconomyRevision = 9 }))
                Check(!scope.Complete(true, false) && LocalProgression.State.ServerEconomyRevision == 8, "mail list recovery requires a fresh economic snapshot");
            using (var scope = EconomyResponseApply.Begin(new OnGetQuestStateResponseDTO { EconomyRevision = 9 }))
                Check(scope.Complete(true, false) && LocalProgression.State.ServerEconomyRevision == 9, "metadata-only quest response can durably advance revision");
            using (var scope = EconomyResponseApply.Begin(new OnSaveTutorialProgressResponseDTO { EconomyRevision = 10 }))
                Check(!scope.Complete(true, true) && LocalProgression.State.ServerEconomyRevision == 9, "metadata cannot hide an earlier unresolved result");
            using (var scope = EconomyResponseApply.Begin(new OnGetQuestStateResponseDTO { EconomyRevision = 9 }))
            {
                long before = LocalProgression.State.Revision;
                Check(scope.Complete(true, false) && LocalProgression.State.Revision == before, "same server revision requires no extra disk write");
            }
            return new { passed = checks.Count, checks };
        }
    }
}
#endif
