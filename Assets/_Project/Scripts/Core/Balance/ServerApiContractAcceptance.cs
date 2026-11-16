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
            bool Read<T>(JObject body, out T response) where T : ApiResponseDTO
            {
                object[] args = { "fixture", new ExecuteFunctionResult { FunctionName = "fixture", FunctionResult = body.ToString() }, null, null };
                bool valid = (bool)typeof(NetworkManager).GetMethod("TryReadResponse", BindingFlags.Static | BindingFlags.NonPublic)
                    .MakeGenericMethod(typeof(T)).Invoke(null, args);
                response = (T)args[2];
                return valid;
            }
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
