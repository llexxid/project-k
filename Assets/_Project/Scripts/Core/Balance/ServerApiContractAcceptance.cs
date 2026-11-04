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
                bool invalidTypeRejected = false;
                try { Request<OnEnchantRubyRequestDTO, OnEnchantRubyResponseDTO>(new OnEnchantRubyRequestDTO { RequestId = "1:" + Nonce, Type = (RubyGrowthType)2 }); }
                catch (TargetInvocationException ex) when (ex.InnerException is ArgumentException) { invalidTypeRejected = true; }
                Check(invalidTypeRejected, "invalid growth enum cannot leave the client");
                Check(new OnStageClearRequestDTO().NextAction == eStageClearAction.Advance, "existing clear calls retain advance semantics");
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
