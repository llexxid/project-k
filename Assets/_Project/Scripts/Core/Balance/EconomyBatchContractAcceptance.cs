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
            var legacy = (JObject)body.DeepClone();
            legacy["CurrentState"]["MageTowerState"].Parent.Remove();
            legacy["CurrentState"]["EconomyBatchState"]["SupportsMageEnhance"].Parent.Remove();
            legacy["CurrentState"]["EconomyBatchState"]["MageEnhanceMode"].Parent.Remove();
            foreach (var row in legacy["Receipt"]["Results"]) row["SpentArcaneKnowledge"].Parent.Remove();
            Check(Read(legacy), "old v1 response without mage fields still decodes");
            var capability = new EconomyBatchStateDTO();
            Check(EconomyBatchProtocol.MageMode(capability) == MageEnhanceBatchMode.Unknown, "missing capability keeps legacy single routing");
            capability.SupportsMageEnhance = true;
            Check(EconomyBatchProtocol.MageMode(capability) == MageEnhanceBatchMode.Active, "old capability still enables mage batches");
            capability.MageEnhanceMode = MageEnhanceBatchMode.RecoveryOnly;
            Check(EconomyBatchProtocol.MageMode(capability) == MageEnhanceBatchMode.RecoveryOnly, "explicit rollback overrides a stale legacy flag");
            Check(!Read(Change(body, "CurrentState.EconomyBatchState.MageEnhanceMode", 3)), "undefined mage mode cannot choose a spending route");
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
            response.Receipt.PayloadHash = EconomyInputJournal.Hash(request);
            Check(response.Receipt.PayloadHash == "D1D2DACD4AC86593C430F246055EB6F92A6953DF0338CC09CBF998234683422B",
                "adding mage commands leaves the existing mixed growth hash unchanged");
            EconomyBatchProtocol.ValidateRequest(request); EconomyBatchProtocol.MatchRequest(response, request);
            Check(true, "x1 input identities survive mixed growth prefix and rejection");
            var independent = Fixture();
            request.IndependentGrowthFailures = true;
            independent.Receipt.IndependentGrowthFailures = true;
            independent.Receipt.PayloadHash = EconomyInputJournal.Hash(request);
            Check(independent.Receipt.PayloadHash == "E8DE3A952EB40B97F2DD834752A1928FFA469D60FC388FA62B6CCE4B590FCA90",
                "opt-in hash agrees with the deployed server's independent-growth vector");
            independent.Receipt.Results[2].Status = EconomyCommandStatus.Applied;
            independent.Receipt.Results[2].AppliedCount = 1;
            independent.Receipt.Results[2].SpentRuby = 20;
            EconomyBatchProtocol.ValidateReceipt(independent.Receipt);
            var restored = JsonConvert.DeserializeObject<OnEconomyBatchRequestDTO>(JsonConvert.SerializeObject(request));
            EconomyBatchProtocol.MatchRequest(independent, restored);
            Check(EconomyCopies.Copy(independent.Receipt).IndependentGrowthFailures &&
                EconomyInputJournal.Hash(restored) == independent.Receipt.PayloadHash,
                "reloading retains independent growth and a different kind can complete after rejection");
            independent.Receipt.Results[2].Kind = EconomyCommandKind.GoldHealth;
            Check(Rejected(() => EconomyBatchProtocol.ValidateReceipt(independent.Receipt)), "a rejected growth kind cannot apply again in the same receipt");
            independent.Receipt.Results[2].Kind = EconomyCommandKind.RubyGold;
            request.IndependentGrowthFailures = false;
            Check(Rejected(() => EconomyBatchProtocol.MatchRequest(independent, request)), "legacy requests cannot accept independent failure receipts");
            request.Commands[0].SpendLimit = 49;
            response.Receipt.PayloadHash = EconomyInputJournal.Hash(request);
            Check(Rejected(() => EconomyBatchProtocol.MatchRequest(response, request)), "result cannot exceed click spend cap");
            request.Commands[0].SpendLimit = 50; request.Sequence++;
            Check(Rejected(() => EconomyBatchProtocol.MatchRequest(response, request)), "same nonce with wrong sequence is not accepted");
            request.Sequence--; request.Commands[0].Mode = 10;
            Check(Rejected(() => EconomyBatchProtocol.ValidateRequest(request)), "x1 cannot masquerade as a reduced x10 transaction");
            request.Commands[0].ExpectedLevel = 299;
            EconomyBatchProtocol.ValidateRequest(request);
            Check(true, "x10 still caps to the one remaining level");
            CheckMageContract(request, Check, Rejected, Read);
            CheckIndependentGrowthContract(Check, Rejected, Read);
            return new { passed = checks.Count, checks };
        }

        private static void CheckMageContract(OnEconomyBatchRequestDTO request, Action<bool, string> check,
            Func<Action, bool> rejected, Func<JObject, bool> read)
        {
            EconomyCommandDTO Mage(int id, int level) => new() { Kind = EconomyCommandKind.MageEnhance, Mode = 1,
                SkillId = id, ExpectedLevel = level, CountLimit = 1, SpendLimit = BalanceMath.MageCost(level).Value };
            var first = Mage(0, 0);
            request.Commands = new() { first };
            check(EconomyInputJournal.Hash(request) == "F58A2E6473AB9E5E6B3CADF29EA940FCEB7FBD955B5638B6F39498C56C785AAD",
                "skill zero is present after TotalKills in the canonical mage hash");
            foreach (var id in new int?[] { null, -1, 6, 8, 10 })
            {
                first.SkillId = id;
                check(!EconomyInputJournal.ValidCommand(first) && rejected(() => EconomyBatchProtocol.ValidateRequest(request)),
                    "invalid mage target cannot be journaled or sent: " + id);
            }
            first.SkillId = 0; first.Kind = EconomyCommandKind.GoldHealth;
            check(!EconomyInputJournal.ValidCommand(first) && rejected(() => EconomyBatchProtocol.ValidateRequest(request)), "non-mage command cannot carry a skill ID");
            first.Kind = EconomyCommandKind.MageEnhance; first.CountLimit = 2;
            check(rejected(() => EconomyBatchProtocol.ValidateRequest(request)), "one mage command is exactly one click");
            first.CountLimit = 1;
            request.Commands.Add(Mage(0, 1));
            request.Commands.Add(Mage(9, 0));
            request.Commands.Add(new() { Kind = EconomyCommandKind.GoldHealth, Mode = 1, CountLimit = 1, SpendLimit = 50 });
            request.Commands.Add(Mage(9, 1));
            EconomyBatchProtocol.ValidateRequest(request);

            var response = Fixture();
            response.Receipt.PayloadHash = EconomyInputJournal.Hash(request);
            response.Receipt.Results = new()
            {
                new() { Index = 0, Kind = first.Kind, Status = EconomyCommandStatus.Rejected, ErrorCode = eErrorCode.EconomyExpectedLevelMismatch },
                new() { Index = 1, Kind = first.Kind, Status = EconomyCommandStatus.Unprocessed },
                new() { Index = 2, Kind = first.Kind, Status = EconomyCommandStatus.Applied, AppliedCount = 1, SpentArcaneKnowledge = 10 },
                new() { Index = 3, Kind = EconomyCommandKind.GoldHealth, Status = EconomyCommandStatus.Applied, AppliedCount = 1, SpentGold = 50 },
                new() { Index = 4, Kind = first.Kind, Status = EconomyCommandStatus.Applied, AppliedCount = 1, SpentArcaneKnowledge = request.Commands[4].SpendLimit }
            };
            var state = response.CurrentState;
            state.EconomyBatchState.MageEnhanceMode = MageEnhanceBatchMode.Active;
            check(!read(JObject.FromObject(response)), "a completed mage receipt requires its canonical tower");
            var skill = new MageSkillDTO { SkillId = 9, EnhancementLevel = 2, KnowledgeSpent = 10 + request.Commands[4].SpendLimit };
            state.MageTowerState = new() { Skills = new() { skill }, EquippedSkillIds = new[] { 9, -1, -1, -1, -1 } };
            var saved = EconomyInputJournal.Clone(request);
            EconomyBatchProtocol.MatchRequest(response, saved);
            check(read(JObject.FromObject(response)) && EconomyCopies.Copy(first).SkillId == 0 &&
                EconomyBatchProtocol.MageMode(EconomyCopies.Copy(state.EconomyBatchState)) == MageEnhanceBatchMode.Active &&
                EconomyCopies.Copy(response.Receipt).Results[2].SpentArcaneKnowledge == 10,
                "recovered mage rejection skips its skill and retains another mage and gold success");
            state.MageTowerState.EquippedSkillIds[1] = 9;
            check(!read(JObject.FromObject(response)), "duplicate equipped mage skills cannot overwrite confirmed slots");
            state.MageTowerState.EquippedSkillIds[1] = -1;
            response.Replayed = true;
            state.MageTowerState.Skills.Clear(); state.MageTowerState.EquippedSkillIds[0] = -1;
            check(rejected(() => EconomyBatchProtocol.MatchRequest(response, saved)), "newer replay snapshot must still own each enhanced skill");
            state.MageTowerState.Skills.Add(skill);
            response.EconomyRevision = response.Receipt.CommittedRevision;
            EconomyBatchProtocol.MatchRequest(response, saved);
            check(true, "same-revision state uses the last of two applied enhancements to one skill");
            skill.EnhancementLevel = 1;
            check(rejected(() => EconomyBatchProtocol.MatchRequest(response, saved)), "same-revision snapshot cannot stop at an earlier accepted skill level");
            response.EconomyRevision++;
            skill.EnhancementLevel = 0; skill.KnowledgeSpent = 0;
            EconomyBatchProtocol.MatchRequest(response, saved);
            check(true, "later canonical revision can contain a valid enhancement reset");
            saved.Commands[1].SkillId = 1;
            check(rejected(() => EconomyBatchProtocol.MatchRequest(response, saved)), "lookup compares the receipt to the frozen skill IDs");
            response.Receipt.PayloadHash = EconomyInputJournal.Hash(saved);
            check(rejected(() => EconomyBatchProtocol.MatchRequest(response, saved)), "a different skill cannot be silently skipped");
            response.Receipt.PayloadHash = EconomyInputJournal.Hash(request);
            var applied = response.Receipt.Results[2];
            applied.SpentArcaneKnowledge++;
            check(rejected(() => EconomyBatchProtocol.MatchRequest(response, request)), "mage result cannot exceed click spend limit");
            applied.SpentArcaneKnowledge = 0;
            check(!read(JObject.FromObject(response)), "applied mage result requires an arcane knowledge debit");
            applied.SpentArcaneKnowledge = 10;
            state.EconomyBatchState.MageEnhanceMode = MageEnhanceBatchMode.RecoveryOnly;
            response.Receipt.Results[0].ErrorCode = eErrorCode.UnsupportedProtocolVersion;
            EconomyBatchProtocol.MatchRequest(response, request);
            check(read(JObject.FromObject(response)), "rollback can recover an immutable mixed receipt with a skill-local unsupported result");
            response.Receipt.Results[0].ErrorCode = eErrorCode.EconomyRevisionMismatch;
            check(!read(JObject.FromObject(response)), "global revision failure on a mage command stops the whole batch");
            response.Receipt = null; response.Status = EconomyBatchStatus.NotFound; state.MageTowerState = null;
            check(!read(JObject.FromObject(response)), "capable recovery response requires a full mage snapshot");
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

        internal static (OnEconomyBatchRequestDTO Request, OnEconomyBatchResponseDTO Response) IndependentGrowthFixture()
        {
            EconomyCommandDTO Growth(EconomyCommandKind kind, int level, long cost) => new()
            { Kind = kind, ExpectedLevel = level, Mode = 1, CountLimit = 1, SpendLimit = cost };
            var request = new OnEconomyBatchRequestDTO
            {
                RequestId = Id, Generation = 1, Sequence = 1, ExpectedEconomyRevision = 3, AppliedRewardEpoch = 1,
                IndependentGrowthFailures = true,
                Commands = new()
                {
                    Growth(EconomyCommandKind.GoldAttack, 0, 50), Growth(EconomyCommandKind.GoldAttack, 1, 54),
                    Growth(EconomyCommandKind.GoldHealth, 0, 50), Growth(EconomyCommandKind.GoldAttack, 2, 58),
                    Growth(EconomyCommandKind.RubyGold, 1, 22), Growth(EconomyCommandKind.RubyExperience, 0, 20),
                    Growth(EconomyCommandKind.RubyGold, 2, 24),
                    new() { Kind = EconomyCommandKind.Hunt, Stage = eStage.Stage1_1, MainRunId = Id, RewardEpoch = 1,
                        TotalKills = new() { new() { MonsterType = eMonsterType.MON_GOBLIN, Count = 3 } } }
                }
            };
            var response = Fixture();
            response.Receipt.IndependentGrowthFailures = true;
            response.Receipt.PayloadHash = EconomyInputJournal.Hash(request);
            response.Receipt.Results = new()
            {
                new() { Index = 0, Kind = EconomyCommandKind.GoldAttack, Status = EconomyCommandStatus.Applied, AppliedCount = 1, SpentGold = 50 },
                new() { Index = 1, Kind = EconomyCommandKind.GoldAttack, Status = EconomyCommandStatus.Rejected, ErrorCode = eErrorCode.EconomyExpectedLevelMismatch },
                new() { Index = 2, Kind = EconomyCommandKind.GoldHealth, Status = EconomyCommandStatus.Applied, AppliedCount = 1, SpentGold = 50 },
                new() { Index = 3, Kind = EconomyCommandKind.GoldAttack, Status = EconomyCommandStatus.Unprocessed },
                new() { Index = 4, Kind = EconomyCommandKind.RubyGold, Status = EconomyCommandStatus.Rejected, ErrorCode = eErrorCode.EconomyExpectedLevelMismatch },
                new() { Index = 5, Kind = EconomyCommandKind.RubyExperience, Status = EconomyCommandStatus.Applied, AppliedCount = 1, SpentRuby = 20 },
                new() { Index = 6, Kind = EconomyCommandKind.RubyGold, Status = EconomyCommandStatus.Unprocessed },
                new() { Index = 7, Kind = EconomyCommandKind.Hunt, Status = EconomyCommandStatus.Applied, AppliedCount = 3, GrantedGold = 30, GrantedExp = 12 }
            };
            var state = response.CurrentState;
            state.EnchantHPCount = 1; state.Currency.Gold = 930; state.Currency.Ruby = 80;
            state.RubyGrowthState.ExpLevel = state.RubyGrowthState.BestExpLevel = 1; state.RubyGrowthState.ExpSpent = 20;
            state.MainBattle.AcceptedKills = EconomyCopies.Kills(request.Commands[7].TotalKills);
            state.EconomyBatchState.IndependentGrowthFailures = true;
            state.EconomyBatchState.RewardEpoch = 2;
            state.EconomyBatchState.RewardEpochs.Add(new() { Id = 2, ExpLevel = 1 });
            state.EconomyBatchState.PendingReceipt = EconomyCopies.Copy(response.Receipt);
            return (request, response);
        }

        private static void CheckIndependentGrowthContract(Action<bool, string> check, Func<Action, bool> rejected, Func<JObject, bool> read)
        {
            var (request, response) = IndependentGrowthFixture();
            var frozen = EconomyInputJournal.Clone(request);
            EconomyBatchProtocol.ValidateRequest(frozen);
            EconomyBatchProtocol.MatchRequest(response, frozen);
            var metadata = EconomyCopies.Copy(response.CurrentState.EconomyBatchState);
            check(read(JObject.FromObject(response)) && frozen.IndependentGrowthFailures && metadata.IndependentGrowthFailures &&
                metadata.PendingReceipt.IndependentGrowthFailures, "new growth receipt and canonical capability survive decode and local copies");
            var malformed = JObject.FromObject(response);
            malformed["Receipt"]["IndependentGrowthFailures"] = 1;
            check(!read(malformed), "numeric receipt marker cannot enable independent growth failure handling");
            malformed = JObject.FromObject(response);
            malformed["CurrentState"]["EconomyBatchState"]["IndependentGrowthFailures"] = "true";
            check(!read(malformed), "string capability cannot opt a new batch into independent growth failures");
            frozen.IndependentGrowthFailures = false;
            string newHash = response.Receipt.PayloadHash;
            response.Receipt.PayloadHash = EconomyInputJournal.Hash(frozen);
            check(newHash != response.Receipt.PayloadHash && rejected(() => EconomyBatchProtocol.MatchRequest(response, frozen)),
                "independent result requires the original request opt-in even when the receipt hash is forged to match");
            response.Receipt.PayloadHash = newHash;
            response.CurrentState.EconomyBatchState.IndependentGrowthFailures = false;
            response.Replayed = true;
            check(read(JObject.FromObject(response)), "compatibility rollback can read and replay a previously committed independent receipt");

            var rows = response.Receipt.Results;
            var laterAttack = rows[3];
            rows[3] = new() { Index = 3, Kind = laterAttack.Kind, Status = EconomyCommandStatus.Applied, AppliedCount = 1, SpentGold = 58 };
            check(!read(JObject.FromObject(response)), "same growth target cannot apply after its rejection");
            rows[3] = laterAttack;
            var health = rows[2];
            rows[2] = new() { Index = 2, Kind = health.Kind, Status = EconomyCommandStatus.Unprocessed };
            check(!read(JObject.FromObject(response)), "independent gold health cannot be skipped because gold attack failed");
            rows[2] = health;
            rows[1].ErrorCode = eErrorCode.EconomyRevisionMismatch;
            check(!read(JObject.FromObject(response)), "revision mismatch still stops the entire batch");
            rows[1].ErrorCode = eErrorCode.InvalidRewardEpoch;
            check(!read(JObject.FromObject(response)), "invalid reward cutoff still stops the entire batch");
            rows[1].ErrorCode = eErrorCode.EconomyExpectedLevelMismatch;
            response.Receipt.IndependentGrowthFailures = false;
            for (int i = 2; i < rows.Count; i++) rows[i] = new() { Index = i, Kind = rows[i].Kind, Status = EconomyCommandStatus.Unprocessed };
            EconomyBatchProtocol.MatchRequest(response, request);
            check(read(JObject.FromObject(response)), "stale opt-in request accepts the compatibility server's legacy prefix response");
            response.Receipt.IndependentGrowthFailures = true;
            rows[1].ErrorCode = eErrorCode.EconomyRevisionMismatch;
            check(!read(JObject.FromObject(response)), "receipt cannot claim independence without an isolated gold or ruby rejection");
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
