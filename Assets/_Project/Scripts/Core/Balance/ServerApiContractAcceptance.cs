#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using IdleKingdomFunction.Common;
using Newtonsoft.Json.Linq;
using PlayFab.CloudScriptModels;
using Protocol.Message.Data;
using Protocol.Message.Dto;
using Scripts.Core.Manager;
using Scripts.Core;
using UnityEngine;

namespace KingdomIdle.Balance
{
    public static class ServerApiContractAcceptance
    {
        const string Nonce = "1234567890abcdef1234567890abcdef";

        // Runs the production response reader and request serializer without authentication or HTTP calls.
        public static object Run()
        {
            var checks = new List<string>();
            void Check(bool value, string name)
            { if (!value) throw new InvalidOperationException("Server API contract acceptance: " + name); checks.Add(name); }
            Check(Scripts.Server.Auth.Authentication.TryGetLoginRetrySeconds(new PlayFab.PlayFabError { HttpCode = 429, RetryAfterSeconds = 47 }, out int delay) && delay == 47,
                "login honors the rate-limit wait reported by PlayFab");
            Check(!Scripts.Server.Auth.Authentication.TryGetLoginRetrySeconds(new PlayFab.PlayFabError { HttpCode = 403 }, out _),
                "a permanent authentication refusal is not retried");
            bool Read<T>(JObject body, out T response) where T : ApiResponseDTO
            {
                object[] args = { "fixture", new ExecuteFunctionResult { FunctionName = "fixture", FunctionResult = body.ToString() }, null, null };
                bool valid = (bool)typeof(NetworkManager).GetMethod("TryReadResponse", BindingFlags.Static | BindingFlags.NonPublic)
                    .MakeGenericMethod(typeof(T)).Invoke(null, args);
                response = (T)args[2];
                return valid;
            }
            CheckPackedResponses(Check);
            CheckMenuRecovery(Check);
            Check(NetworkManager.IsMenuSnapshot(new OnGetMailListRequestDTO()) && NetworkManager.IsMenuSnapshot(new OnGetQuestStateRequestDTO()) &&
                NetworkManager.IsMenuSnapshot(new OnCheckAttendanceRequestDTO()) && !NetworkManager.IsMenuSnapshot(new OnClaimMailRewardRequestDTO()) &&
                !NetworkManager.IsMenuSnapshot(new OnClaimQuestRewardRequestDTO()) && !NetworkManager.IsMenuSnapshot(new OnEconomyBatchRequestDTO()),
                "menu lookups wait for settlement without reclassifying a reward claim or batch");
            JObject Body(ApiResponseDTO dto)
            { dto.ProtocolVersion = ApiVersions.ProtocolVersion; return JObject.FromObject(dto); }
            JObject Changed(JObject source, string path, JToken value)
            {
                var copy = (JObject)source.DeepClone();
                var token = copy.SelectToken(path);
                if (value == null) ((JProperty)token.Parent).Remove(); else token.Replace(value);
                return copy;
            }

            var growth = new RubyGrowthStateDTO
            { NextRequestSequence = 2, GoldLevel = 1, BestGoldLevel = 1, GoldSpent = 1, LastResetDay = "" };
            var enchant = Body(new OnEnchantRubyResponseDTO
            { RequestId = "1:" + Nonce, RubyGrowthState = growth, SpentRuby = 1, CurrentRuby = 4 });
            Check(Read(enchant, out OnEnchantRubyResponseDTO validEnchant) && validEnchant.CurrentRuby == 4, "ruby success uses the authoritative remaining balance");
            Check(Read<OnEnchantRubyResponseDTO>(Changed(enchant, "AlreadyApplied", true), out _), "idempotent replay accepts the same complete result");
            Check(!Read<OnEnchantRubyResponseDTO>(Changed(enchant, "CurrentRuby", null), out _), "missing ruby balance fails closed");
            Check(!Read<OnEnchantRubyResponseDTO>(Changed(enchant, "AlreadyApplied", null), out _), "missing replay flag fails closed");
            Check(!Read<OnEnchantRubyResponseDTO>(Changed(enchant, "RubyGrowthState.NextRequestSequence", null), out _), "missing request sequence fails closed");
            Check(!Read<OnEnchantRubyResponseDTO>(Changed(enchant, "RubyGrowthState.NextRequestSequence", 3), out _), "response sequence must follow the accepted request");
            Check(!Read<OnEnchantRubyResponseDTO>(Changed(enchant, "CurrentRuby", -1), out _) &&
                !Read<OnEnchantRubyResponseDTO>(Changed(enchant, "SpentRuby", 0), out _), "invalid ruby money is rejected");
            Check(!Read<OnEnchantRubyResponseDTO>(Changed(enchant, "RubyGrowthState.GoldLevel", 51), out _) &&
                !Read<OnEnchantRubyResponseDTO>(Changed(enchant, "RubyGrowthState.BestGoldLevel", 0), out _), "growth range and best-level invariants are enforced");
            Check(!Read<OnEnchantRubyResponseDTO>(Changed(enchant, "RubyGrowthState.LastResetDay", "2026-02-30"), out _), "impossible reset dates are rejected");
            Check(!Read<OnEnchantRubyResponseDTO>(Changed(enchant, "RubyGrowthState.GoldLevel", 1.5), out _), "fractional integer fields are rejected");
            foreach (string id in new[] { "01:" + Nonce, "0:" + Nonce, "1:" + Nonce.ToUpperInvariant(), "1:" + new string('0', 32), "1:short" })
                Check(!Read<OnEnchantRubyResponseDTO>(Changed(enchant, "RequestId", id), out _), "malformed ruby ID rejected: " + id);
            const long largeSequence = 9007199254740993L;
            var large = Changed(Changed(enchant, "RequestId", largeSequence + ":" + Nonce), "RubyGrowthState.NextRequestSequence", largeSequence + 1);
            Check(Read(large, out OnEnchantRubyResponseDTO largeResponse) && largeResponse.RubyGrowthState.NextRequestSequence == largeSequence + 1,
                "64-bit request sequences retain integer precision");
            var reset = Body(new OnResetRubyEnhancementResponseDTO
            { RequestId = "1:" + Nonce, RubyGrowthState = growth, RefundedRuby = 0, CurrentRuby = 4 });
            Check(Read<OnResetRubyEnhancementResponseDTO>(reset, out _) &&
                !Read<OnResetRubyEnhancementResponseDTO>(Changed(reset, "RefundedRuby", -1), out _), "zero-refund reset is valid but a negative refund is rejected");

            var tutorial = Body(new OnSaveTutorialProgressResponseDTO
            { Progress = new TutorialProgressDTO { SequenceId = "combat.v1", ConfirmedSteps = new List<string> { "attack" }, IsFinished = true } });
            Check(Read(tutorial, out OnSaveTutorialProgressResponseDTO saved) && saved.Progress.IsFinished, "completed tutorial survives decoding");
            Check(!Read<OnSaveTutorialProgressResponseDTO>(Changed(tutorial, "Progress.IsFinished", null), out _), "missing completion flag cannot restart a completed tutorial");
            Check(!Read<OnSaveTutorialProgressResponseDTO>(Changed(tutorial, "Progress.ConfirmedSteps", new JArray("attack", "attack")), out _) &&
                !Read<OnSaveTutorialProgressResponseDTO>(Changed(tutorial, "Progress.SequenceId", "combat v1"), out _), "invalid tutorial IDs and duplicate progress are rejected");

            var stage = Body(new OnChangeMainStageResponseDTO { StageState = new UserStageQuery
            { CurrentStage = (Protocol.Message.Data.eStage)(long)StageParser.MakeStage(eStageType.Main, 1),
                HighestStage = (Protocol.Message.Data.eStage)(long)StageParser.MakeStage(eStageType.Main, 1, 10),
                MainClear = (Protocol.Message.Data.eStage)(long)StageParser.MakeStage(eStageType.Main, 3, 11) } });
            Check(Read<OnChangeMainStageResponseDTO>(stage, out _), "stage move preserves a higher lifetime clear after reincarnation");
            Check(!Read<OnChangeMainStageResponseDTO>(Changed(stage, "StageState.CurrentStage", (long)StageParser.MakeStage(eStageType.Main, 1, 11)), out _), "current stage cannot exceed the cycle highest stage");
            Check(!Read<OnChangeMainStageResponseDTO>(Changed(stage, "StageState.CurrentStage", (long)StageParser.MakeStage(eStageType.GoldDungeon, 1)), out _), "stage move cannot return a dungeon as the main position");

            var login = Body(LegacyLogin());
            login.Remove("RubyGrowthState"); login.Remove("Tutorials");
            Check(Read(login, out UserInitResponseDTO oldServer) && oldServer.RubyGrowthState == null && oldServer.Tutorials == null,
                "old server login succeeds without fabricating zero growth or empty tutorial records");
            login["RubyGrowthState"] = JObject.FromObject(growth);
            login["Tutorials"] = new JArray(tutorial["Progress"].DeepClone());
            Check(Read<UserInitResponseDTO>(login, out _), "new login snapshot accepts growth and tutorial progress");
            Check(!Read<UserInitResponseDTO>(Changed(login, "RubyGrowthState.NextRequestSequence", 0), out _), "present but damaged growth cannot silently fall back to legacy login");
            login["Tutorials"] = new JArray(tutorial["Progress"].DeepClone(), tutorial["Progress"].DeepClone());
            Check(!Read<UserInitResponseDTO>(login, out _), "duplicate tutorial sequence records are rejected");

            var mainStage = (Protocol.Message.Data.eStage)(long)StageParser.MakeStage(eStageType.Main, 1);
            var nextMain = (Protocol.Message.Data.eStage)(long)StageParser.MakeStage(eStageType.Main, 1, 2);
            var mainMonster = (Protocol.Message.Data.eMonsterType)4294967301UL;
            var nextBattle = new MainBattleStateDTO { RunId = "fedcba0987654321fedcba0987654321", Stage = nextMain, AcceptedKills = new() };
            var source = LegacyLogin();
            var completedBody = Body(new OnCompleteMainWaveResponseDTO
            {
                RequestId = Nonce, CompletedRunId = Nonce, Replayed = false, IsFirstClear = true,
                GrantedRewards = new(), Level = 1, Currency = source.Currency, Equipment = source.Equipment,
                MageTowerState = source.MageTowerState, MainBattle = nextBattle,
                StageState = new UserStageQuery { CurrentStage = nextMain, HighestStage = nextMain, MainClear = mainStage }
            });
            Check(Read(completedBody, out OnCompleteMainWaveResponseDTO validMain) && validMain.MainBattle.RunId == nextBattle.RunId,
                "main completion includes authoritative progression and the next server run");
            foreach (string field in new[] { "RequestId", "CompletedRunId", "Replayed", "MainBattle", "Level", "Currency", "StageState" })
                Check(!Read<OnCompleteMainWaveResponseDTO>(Changed(completedBody, field, null), out _), "main completion rejects missing " + field);
            Check(!Read<OnCompleteMainWaveResponseDTO>(Changed(completedBody, "RequestId", Nonce.ToUpperInvariant()), out _), "main request IDs require canonical lowercase GUID N");
            Check(!Read<OnCompleteMainWaveResponseDTO>(Changed(completedBody, "MainBattle.RunId", Nonce), out _), "completed run cannot become its own next run");
            Check(!Read<OnCompleteMainWaveResponseDTO>(Changed(completedBody, "MainBattle.Stage", (long)mainStage), out _), "completion battle and destination must agree");
            Check(!Read<OnCompleteMainWaveResponseDTO>(Changed(completedBody, "GrantedRewards", new JArray(JValue.CreateNull())), out _),
                "null completion rewards are rejected before committing or triggering presentation callbacks");
            Check(!Read<OnCompleteMainWaveResponseDTO>(Changed(completedBody, "MainBattle.AcceptedKills", new JArray(
                JObject.FromObject(new HuntResult { MonsterType = mainMonster, Count = 1 }),
                JObject.FromObject(new HuntResult { MonsterType = mainMonster, Count = 1 }))), out _), "server accepted counts reject duplicate monster types");
            var legacyMain = Body(LegacyLogin());
            ((JProperty)legacyMain["MainBattle"].Parent).Remove();
            Check(Read<UserInitResponseDTO>(legacyMain, out _), "server without main-wave capability retains legacy login");

            var battleSave = new MainBattleSave { RunId = Nonce, Stage = (long)mainStage, AcceptedKills = new() { [(ulong)mainMonster] = 2 } };
            var observed = new Dictionary<Scripts.Core.eMonsterType, int> { [(Scripts.Core.eMonsterType)(ulong)mainMonster] = 1 };
            Check(MainWaveProgression.Delta(observed, battleSave).Count == 0, "reconnected observations below accepted count do not repay old kills");
            observed[(Scripts.Core.eMonsterType)(ulong)mainMonster] = 5;
            Check(MainWaveProgression.Delta(observed, battleSave)[0].Count == 3, "partial hunt sends observed-minus-accepted rather than adding the baseline");
            var acknowledged = battleSave.Copy(); acknowledged.AcceptedKills[(ulong)mainMonster] = 4;
            Check(MainWaveProgression.Delta(observed, acknowledged)[0].Count == 1,
                "fast partial acknowledgment leaves only the remaining delta before final completion");
            var pendingMain = new MainWaveCompletionSave { RunId = Nonce, RequestId = nextBattle.RunId,
                Stage = (long)mainStage, NextAction = 1, TotalKills = new() { [(ulong)mainMonster] = 5 } };
            var frozen = new ProgressionState { MainBattle = battleSave, PendingMainCompletion = pendingMain, AttackLevel = 42 };
            var cloned = frozen.DeepClone();
            cloned.MainBattle.AcceptedKills[(ulong)mainMonster] = 9; cloned.PendingMainCompletion.TotalKills[(ulong)mainMonster] = 9;
            Check(frozen.MainBattle.AcceptedKills[(ulong)mainMonster] == 2 && frozen.PendingMainCompletion.TotalKills[(ulong)mainMonster] == 5,
                "transaction drafts do not mutate saved accepted counts or pending payload");
            var roundTrip = Newtonsoft.Json.JsonConvert.DeserializeObject<ProgressionState>(Newtonsoft.Json.JsonConvert.SerializeObject(frozen));
            Check(roundTrip.AttackLevel == 42 && roundTrip.PendingMainCompletion.RequestId == nextBattle.RunId &&
                MainWaveProgression.Request(roundTrip.PendingMainCompletion).TotalKills[0].Count == 5,
                "restart preserves request identity, exact final count, intent and unrelated investments");

            var previousNetwork = NetworkManager.Instance;
            var owner = new GameObject("ServerApiContractAcceptance") { hideFlags = HideFlags.HideAndDontSave };
            owner.SetActive(false);
            try
            {
                CheckQuestLookup(owner.AddComponent<QuestManager>(), Check);
                var network = owner.AddComponent<NetworkManager>();
                const BindingFlags instance = BindingFlags.Instance | BindingFlags.NonPublic;
                typeof(NetworkManager).GetMethod("Init", instance).Invoke(network, null);
                ExecuteFunctionRequest Request<TRequest, TResponse>(TRequest request) where TRequest : RequestDTO where TResponse : ApiResponseDTO =>
                    (ExecuteFunctionRequest)typeof(NetworkManager).GetMethod("CreateFunctionRequest", instance)
                        .MakeGenericMethod(typeof(TRequest), typeof(TResponse)).Invoke(network, new object[] { request, "fixture", null });
                var request = Request<OnEnchantRubyRequestDTO, OnEnchantRubyResponseDTO>(new OnEnchantRubyRequestDTO
                { RequestId = "1:" + Nonce, Type = RubyGrowthType.Exp, ExpectedLevel = 0 });
                var payload = JObject.FromObject(request.FunctionParameter);
                Check(request.FunctionName == "OnEnchantRuby" && payload.Value<int>("Type") == 1 && payload.Value<string>("RequestId") == "1:" + Nonce,
                    "enchant route serializes numeric growth type and request identity");
                Check(Request<OnResetRubyEnhancementRequestDTO, OnResetRubyEnhancementResponseDTO>(new OnResetRubyEnhancementRequestDTO
                { RequestId = "1:" + Nonce, Type = RubyGrowthType.Gold }).FunctionName == "OnResetRubyEnhancement", "reset route is registered");
                Check(Request<OnChangeMainStageRequestDTO, OnChangeMainStageResponseDTO>(new OnChangeMainStageRequestDTO()).FunctionName == "OnChangeMainStage", "stage route is registered");
                Check(Request<OnSaveTutorialProgressRequestDTO, OnSaveTutorialProgressResponseDTO>(new OnSaveTutorialProgressRequestDTO { Progress = saved.Progress }).FunctionName == "OnSaveTutorialProgress", "tutorial route is registered");
                typeof(NetworkManager).GetField("_playFabId", instance).SetValue(network, "MAIL_FIXTURE");
                var list = Request<OnGetMailListRequestDTO, OnGetMailListResponseDTO>(new OnGetMailListRequestDTO { Locale = "en", Count = 50 });
                var mailPayload = JObject.FromObject(list.FunctionParameter);
                Check(list.FunctionName == "OnGetMailList" && mailPayload.Value<string>("Locale") == "en" &&
                    mailPayload.Value<string>("PlayFabId") == "MAIL_FIXTURE", "mail list sends locale and authenticated player identity");
                var claim = Request<OnClaimMailRewardRequestDTO, OnClaimMailRewardResponseDTO>(new OnClaimMailRewardRequestDTO { MailIds = new() { 9000000000001L } });
                Check(JObject.FromObject(claim.FunctionParameter)["Locale"] == null, "mail claim remains independent of locale and text");
                bool invalidTypeRejected = false;
                try { Request<OnEnchantRubyRequestDTO, OnEnchantRubyResponseDTO>(new OnEnchantRubyRequestDTO { RequestId = "1:" + Nonce, Type = (RubyGrowthType)2 }); }
                catch (TargetInvocationException ex) when (ex.InnerException is ArgumentException) { invalidTypeRejected = true; }
                Check(invalidTypeRejected, "invalid growth enum cannot leave the client");
                Check(new OnStageClearRequestDTO().NextAction == eStageClearAction.Advance, "existing clear calls retain advance semantics");
                var completedRequest = Request<OnCompleteMainWaveRequestDTO, OnCompleteMainWaveResponseDTO>(MainWaveProgression.Request(pendingMain));
                var completedPayload = JObject.FromObject(completedRequest.FunctionParameter);
                Check(completedRequest.FunctionName == "OnCompleteMainWave" && completedPayload.Value<string>("MainRunId") == Nonce &&
                    completedPayload.Value<string>("RequestId") == pendingMain.RequestId && completedPayload.Value<int>("NextAction") == 1,
                    "new completion route retains run, immutable request and repeat intent");
                var legacyHunt = Request<OnRewardRequestDTO, OnHuntResponseDTO>(new OnRewardRequestDTO
                { Stage = mainStage, Loots = new() { new HuntResult { MonsterType = mainMonster, Count = 1 } } });
                var legacyPayload = JObject.FromObject(legacyHunt.FunctionParameter);
                Check(legacyPayload["MainRunId"] == null && legacyPayload["BatchId"] == null, "legacy and dungeon hunts omit new optional identities");
                var partialRequest = Request<OnRewardRequestDTO, OnHuntResponseDTO>(new OnRewardRequestDTO
                { Stage = mainStage, MainRunId = Nonce, BatchId = nextBattle.RunId, Loots = new() { new HuntResult { MonsterType = mainMonster, Count = 1 } } });
                Check(JObject.FromObject(partialRequest.FunctionParameter).Value<string>("BatchId") == nextBattle.RunId, "new partial route retains its idempotent batch identity");
                return new { passed = true, count = checks.Count, checks };
            }
            finally
            {
                NetworkManager.Instance = previousNetwork;
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        private static void CheckMenuRecovery(Action<bool, string> check)
        {
            float time = 0;
            int reads = 0, held = 0;
            Action<bool> afterFence = null;
            var delayed = NetworkManager.WaitForMenuSnapshot(() => true, () => time, () => { },
                complete => { afterFence = complete; return true; }, () => reads++, () => held++);
            check(delayed.MoveNext() && afterFence != null && reads == 0, "lookup waits behind pending settlement");
            time = 18;
            check(!delayed.MoveNext() && held == 1 && reads == 0, "18-second unresolved lookup reports pending without a server write");
            afterFence(true);
            check(reads == 0, "late fence after timeout cannot duplicate the request");
            bool recovered = false;
            time = 21;
            var resumed = NetworkManager.WaitForMenuSnapshot(() => true, () => time, () => recovered = true,
                complete => { if (!recovered) return false; complete(true); return true; }, () => reads++, () => held++);
            check(!resumed.MoveNext() && reads == 1 && held == 1, "a retried lookup joins recovery and runs exactly once");
            var attendance = new KingdomIdle.CheckIn.CheckInService(null);
            long first = attendance.BeginOperation();
            check(attendance.IsBusy && attendance.IsCurrent(first), "attendance tracks current account operation");
            attendance.CheckAccount((LocalProgression.AccountGeneration + 1, -5));
            check(!attendance.IsBusy && !attendance.IsCurrent(first), "switching account ends pending attendance and rejects its late callback");
            long next = attendance.BeginOperation();
            check(attendance.IsCurrent(next) && !attendance.IsCurrent(first), "new attendance operation can finish independently");
            int notifications = 0;
            attendance.ClaimCompleted += _ => notifications++;
            attendance.FinishClaimFailure(first, null);
            check(attendance.IsBusy && notifications == 0, "late previous claim cannot unlock a pending recovery");
            attendance.FinishClaimFailure(next, null); attendance.FinishClaimFailure(next, null);
            check(!attendance.IsBusy && notifications == 1, "recovered claim releases busy with exactly one completion");
            bool open = true;
            afterFence = null;
            var closed = NetworkManager.WaitForMenuSnapshot(() => open, () => time, () => { },
                complete => { afterFence = complete; return true; }, () => reads++, () => held++);
            closed.MoveNext(); open = false; afterFence(true);
            check(!closed.MoveNext() && reads == 1 && held == 1, "closing a waiting mailbox skips its request without suppressing a future open");

            time = 0; afterFence = null;
            var quest = NetworkManager.WaitForMenuSnapshot(() => true, () => time, () => { },
                complete => { afterFence = complete; return true; }, () => reads++, () => held++, canPreserveQueuedOrder: () => true);
            check(quest.MoveNext() && afterFence != null, "the quest refresh acquires a boundary before selecting later inputs");
            time = 20;
            check(quest.MoveNext() && reads == 1 && held == 1, "the acquired quest boundary keeps later inputs behind a slow prior response");
            afterFence(true);
            check(!quest.MoveNext() && reads == 2 && held == 1, "the quest refresh is sent once when the original boundary clears");

            time = 0;
            var unavailableQuest = NetworkManager.WaitForMenuSnapshot(() => true, () => time, () => { },
                _ => false, () => reads++, () => held++, canPreserveQueuedOrder: () => true);
            unavailableQuest.MoveNext(); time = 18;
            check(!unavailableQuest.MoveNext() && reads == 2 && held == 2, "an unscheduled quest refresh retains the existing wait limit");

            time = 0; afterFence = null;
            bool canPreserve = true, questOpen = true;
            var blockedQuest = NetworkManager.WaitForMenuSnapshot(() => questOpen, () => time, () => { },
                complete => { afterFence = complete; return true; }, () => reads++, () => { held++; questOpen = false; },
                canPreserveQueuedOrder: () => canPreserve);
            blockedQuest.MoveNext(); time = 20; canPreserve = false;
            check(!blockedQuest.MoveNext() && reads == 2 && held == 3, "an unresolved prior result restores the usual quest refresh timeout");
            canPreserve = true; afterFence(true);
            check(reads == 2, "an expired quest boundary cannot send after recovery");
        }

        private static void CheckQuestLookup(QuestManager quest, Action<bool, string> check)
        {
            long previous = quest.BeginFetch();
            quest.ResetFetch();
            long next = quest.BeginFetch();
            check(quest.IsRefreshing && !quest.OwnsFetch(previous) && quest.OwnsFetch(next),
                "same-account reauthentication replaces the outstanding quest refresh and rejects its late callback");
            quest.ResetFetch();
        }

        private static void CheckPackedResponses(Action<bool, string> check)
        {
            var random = new byte[24576];
            new System.Random(803).NextBytes(random);
            var original = new JObject { ["ProtocolVersion"] = ApiVersions.ProtocolVersion, ["ServerVersion"] = 7,
                ["Memo"] = Convert.ToBase64String(random), ["Uid"] = 9223372036854775805L };
            byte[] raw = System.Text.Encoding.UTF8.GetBytes(original.ToString(Newtonsoft.Json.Formatting.None));
            byte[] zipped;
            using (var output = new System.IO.MemoryStream())
            {
                using (var gzip = new System.IO.Compression.GZipStream(output, System.IO.Compression.CompressionMode.Compress, true))
                    gzip.Write(raw, 0, raw.Length);
                zipped = output.ToArray();
            }
            using var hash = System.Security.Cryptography.SHA256.Create();
            string digest = BitConverter.ToString(hash.ComputeHash(raw)).Replace("-", "").ToLowerInvariant();
            int length = PackedServerResponse.ChunkSize;
            int count = (zipped.Length + length - 1) / length;
            JObject Envelope(int index) => new JObject
            {
                ["ProtocolVersion"] = ApiVersions.ProtocolVersion, ["ServerVersion"] = 7,
                ["PackedResponse"] = new JObject { ["Version"] = 1, ["Encoding"] = "gzip", ["RawBytes"] = raw.Length,
                    ["CompressedBytes"] = zipped.Length, ["Sha256"] = digest, ["ChunkIndex"] = index, ["ChunkCount"] = count,
                    ["SnapshotId"] = Nonce, ["Data"] = Convert.ToBase64String(zipped, index * length, Math.Min(length, zipped.Length - index * length)) }
            };
            bool Rejected(Action action)
            {
                try { action(); return false; }
                catch (Newtonsoft.Json.JsonException) { return true; }
            }
            var packet = new PackedServerResponse(Envelope(0));
            check(count > 2 && Rejected(() => packet.Decode()) && Rejected(() => packet.Add(Envelope(2))),
                "partial and out-of-order packed snapshots cannot apply");
            var changed = Envelope(1); changed["PackedResponse"]["SnapshotId"] = "another-snapshot";
            check(Rejected(() => packet.Add(changed)), "mixed snapshot identities rejected");
            for (int i = 1; i < count; i++) packet.Add(Envelope(i));
            check(JToken.DeepEquals(original, packet.Decode()), "multichunk result preserves large integer and source data");
            var corrupt = Envelope(0);
            corrupt["PackedResponse"]["RawBytes"] = int.MaxValue;
            check(Rejected(() => new PackedServerResponse(corrupt)), "excessive decoded allocation rejected");
            var wrongHash = new string('0', 64);
            var altered = Envelope(0); altered["PackedResponse"]["Sha256"] = wrongHash;
            var tampered = new PackedServerResponse(altered);
            for (int i = 1; i < count; i++)
            {
                var next = Envelope(i);
                next["PackedResponse"]["Sha256"] = wrongHash;
                tampered.Add(next);
            }
            check(Rejected(() => tampered.Decode()), "whole-result checksum verified before applying");
        }

        static UserInitResponseDTO LegacyLogin() => new UserInitResponseDTO
        {
            SessionID = "fixture", Level = 1,
            CharacterDatas = new List<CharacterDataQuery>
            { new CharacterDataQuery("a", 0, 1, 0, 0), new CharacterDataQuery("b", 0, 1, 0, 1), new CharacterDataQuery("c", 0, 1, 0, 2) },
            CharacterEquipmentDatas = new List<CharacterEquipmentQuery>(), Currency = new CurrencyQueryDTO(0, 0, 0, 0),
            SkillTreeDatas = new List<SkillCode>(), Inventory = new List<Protocol.Message.Dto.ItemCode>(), JobTrees = new List<JobTreeQuery>(),
            Equipment = new EquipmentInventoryDTO { Items = new List<EquipmentStackDTO>(), PendingItems = new List<EquipmentStackDTO>(),
                ReserveStacks = new List<EquipmentReserveStackDTO>(), Capacity = 1 },
            MageTowerState = new MageTowerStateDTO { Skills = new List<MageSkillDTO>(), EquippedSkillIds = new[] { -1, -1, -1, -1, -1 } },
            CharacterJobs = new List<CharacterDataQuery>(), GachaState = new GachaStateDTO(),
            Quests = new QuestStateDTO { DailyPeriodKey = "2026-11-04", WeeklyPeriodKey = "2026-W45", Rows = new List<QuestRowDTO>() },
            StageState = new UserStageQuery { CurrentStage = (Protocol.Message.Data.eStage)(long)StageParser.MakeStage(eStageType.Main, 1),
                HighestStage = (Protocol.Message.Data.eStage)(long)StageParser.MakeStage(eStageType.Main, 1) },
            DungeonState = new DungeonStateDTO(), Reincarnation = new ReincarnationStateDTO { Day = "" },
            Attendance = new AttendanceStateDTO { LastDay = "" }
        };
    }
}
#endif
