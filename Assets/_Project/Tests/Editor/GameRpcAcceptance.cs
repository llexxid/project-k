using System;
using System.Collections.Generic;
using System.Linq;
using IdleKingdomFunction.Common;
using IdleKingdomFunction.Common.Dto;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PlayFab;
using PlayFab.CloudScriptModels;
using Scripts.Server;

namespace KingdomIdle.Tests
{
    /// <summary>Pure protocol tests: no HTTP, account login, player files or live grants.</summary>
    public static class GameRpcAcceptance
    {
        public static string[] Run()
        {
            var passed = new List<string>();
            void Test(string name, Action test) { test(); passed.Add(name); }
            Test("SDK precision: long, ulong and decimal survive the initial FunctionResult parse", () =>
            {
                var before = PluginManager.GetPlugin<ISerializerPlugin>(PluginContract.PlayFab_Serializer);
                try
                {
                    GameFunctionSerializer.Install();
                    var sdk = PluginManager.GetPlugin<ISerializerPlugin>(PluginContract.PlayFab_Serializer);
                    const string wire = "{\"code\":200,\"status\":\"OK\",\"data\":{\"FunctionName\":\"Precision\",\"FunctionResult\":{\"ProtocolVersion\":2,\"ServerVersion\":1,\"Signed\":9223372036854775807,\"Unsigned\":18446744073709551615,\"Amount\":12345678901234567890.12345678}}}";
                    var envelope = sdk.DeserializeObject<PlayFab.SharedModels.HttpResponseObject>(wire);
                    var result = sdk.DeserializeObject<ExecuteFunctionResult>(sdk.SerializeObject(envelope.data));
                    Check(GameRpcResponse.TryRead<PrecisionResponse>("Precision", result, out var value, out _));
                    Check(value.Signed == long.MaxValue && value.Unsigned == ulong.MaxValue && value.Amount == 12345678901234567890.12345678m);
                    var request = new OnRewardRequestDTO { ProtocolVersion = 2, SessionID = "test", RewardSequence = 9007199254740993L,
                        Stage = (IdleKingdomFunction.Common.enums.eStage)0x200040001, Loots = new List<HuntResult>() };
                    Check(JObject.Parse(sdk.SerializeObject(request)).Value<long>("RewardSequence") == request.RewardSequence);
                }
                finally { PluginManager.SetPlugin(before, PluginContract.PlayFab_Serializer); }
            });
            Test("Initial v2 sample: zero character/skill IDs and empty inventories are valid", () =>
            {
                var body = Initial();
                Check(Read<UserInitResponseDTO>(body, out var value, out _));
                Check(value.CharacterDatas[0].Index == 0 && value.SkillTreeDatas[0].GetSkillId() == 0 && value.Inventory.Count == 0);
                foreach (string field in new[] { "ProtocolVersion", "ServerVersion", "SessionID", "CharacterDatas", "Inventory", "Currency" })
                {
                    var missing = (JObject)body.DeepClone(); missing.Remove(field);
                    Check(!Read<UserInitResponseDTO>(missing, out _, out _), "Missing " + field);
                }
                var missingIndex = (JObject)body.DeepClone(); ((JObject)missingIndex["CharacterDatas"][0]).Remove("Index");
                Check(!Read<UserInitResponseDTO>(missingIndex, out _, out _));
                var missingCurrency = (JObject)body.DeepClone(); ((JObject)missingCurrency["Currency"]).Remove("Gold");
                Check(!Read<UserInitResponseDTO>(missingCurrency, out _, out _));
            });
            Test("Envelope serializer retains ordinary SDK login response compatibility", () =>
            {
                var fallback = PluginManager.GetPlugin<ISerializerPlugin>(PluginContract.PlayFab_Serializer);
                var sdk = new GameFunctionSerializer(fallback);
                var envelope = sdk.DeserializeObject<PlayFab.SharedModels.HttpResponseObject>("{\"code\":200,\"status\":\"OK\",\"data\":{\"PlayFabId\":\"sample-player\",\"SessionTicket\":\"sample-ticket\",\"NewlyCreated\":false}}");
                var login = sdk.DeserializeObject<PlayFab.ClientModels.LoginResult>(sdk.SerializeObject(envelope.data));
                Check(login.PlayFabId == "sample-player" && login.SessionTicket == "sample-ticket" && !login.NewlyCreated);
            });
            Test("Initial state rejects duplicate/missing character slots and negative balances", () =>
            {
                var body = Initial(); body["CharacterDatas"][1]["Index"] = 0;
                Check(!Read<UserInitResponseDTO>(body, out _, out _));
                body = Initial(); ((JArray)body["CharacterDatas"]).RemoveAt(2);
                Check(!Read<UserInitResponseDTO>(body, out _, out _));
                body = Initial(); body["Currency"]["Gold"] = -1;
                Check(!Read<UserInitResponseDTO>(body, out _, out _));
            });
            Test("Integer fields reject decimal, string and overflow coercion", () =>
            {
                foreach (JToken invalid in new JToken[] { new JValue(1.5m), new JValue("123"), JToken.Parse("18446744073709551615") })
                {
                    var body = Enchant(); body["CurrentLevel"] = invalid;
                    Check(!Read<OnEnchantResponseDTO>(body, out _, out _));
                }
            });
            Test("Business error cannot become a default-valued success response", () =>
            {
                var error = JObject.Parse("{\"ProtocolVersion\":2,\"ServerVersion\":1,\"ErrorCode\":9,\"Message\":\"Not enough gold\"}");
                Check(!Read<OnEnchantResponseDTO>(error, out var value, out var failure));
                Check(value == null && failure.Kind == GameRpcFailureKind.Business && (int)failure.BusinessError.ErrorCode == 9);
            });
            Test("Protocol mismatch is checked on both success and business error", () =>
            {
                var body = Enchant(); body["ProtocolVersion"] = 99;
                Check(!Read<OnEnchantResponseDTO>(body, out _, out var failure) && failure.Kind == GameRpcFailureKind.Protocol);
                body["ErrorCode"] = 9; body["Message"] = "No";
                Check(!Read<OnEnchantResponseDTO>(body, out _, out failure) && failure.Kind == GameRpcFailureKind.Protocol);
            });
            Test("Function errors and oversized/missing/wrong-function results reject success", () =>
            {
                Check(!GameRpcResponse.TryRead<OnEnchantResponseDTO>("X", new ExecuteFunctionResult { Error = new FunctionExecutionError { Error = "HTTPError" } }, out _, out var failure));
                Check(failure.Kind == GameRpcFailureKind.Function && failure.FunctionError != null);
                foreach (var result in new[] { new ExecuteFunctionResult(), new ExecuteFunctionResult { FunctionResult = Enchant(), FunctionResultTooLarge = true },
                    new ExecuteFunctionResult { FunctionName = "Other", FunctionResult = Enchant() } })
                    Check(!GameRpcResponse.TryRead<OnEnchantResponseDTO>("X", result, out _, out _));
            });
            Test("Zero skill code is valid; absent code is invalid", () =>
            {
                var body = JObject.Parse("{\"ProtocolVersion\":2,\"ServerVersion\":1,\"SkillCode\":{\"Code\":0}}");
                Check(Read<OnEnchantSkillResponseDTO>(body, out var value, out _) && value.SkillCode.Code == 0);
                body["SkillCode"] = new JObject();
                Check(!Read<OnEnchantSkillResponseDTO>(body, out _, out _));
            });
            Test("RPC injects fixed protocol and server session, preserves request parameters", () =>
            {
                var transport = new FakeTransport(); var rpc = Client(transport);
                rpc.Call<OnChangeJobRequestDTO, OnChangeJobResponseDTO>("OnChangeJob", new OnChangeJobRequestDTO { Index = 0, JobCode = 4 }, _ => { }, _ => { });
                var sent = (OnChangeJobRequestDTO)transport.Calls[0].Request.FunctionParameter;
                Check(sent.ProtocolVersion == ApiVersions.ProtocolVersion && sent.SessionID == "session-A" && sent.Index == 0 && sent.JobCode == 4);
            });
            Test("Signup/login requests have a parameter and no SessionID", () =>
            {
                var transport = new FakeTransport(); var rpc = new GameRpcClient(transport, () => 0);
                rpc.Call<AuthRequestDTO, UserInitResponseDTO>("OnLogIn", new AuthRequestDTO(), _ => { }, _ => { }, GameRpcKind.Authentication);
                var body = JObject.FromObject(transport.Calls[0].Request.FunctionParameter);
                Check(body.Value<int>("ProtocolVersion") == 2 && body.Property("SessionID") == null);
            });
            Test("Queries follow mutations only after result application completes", () =>
            {
                var transport = new FakeTransport(); var rpc = Client(transport); bool applied = false, read = false;
                rpc.Call<OnEnchantRequestDTO, OnEnchantResponseDTO>("Change", new OnEnchantRequestDTO { Count = 10 }, _ => { Check(transport.Calls.Count == 1); applied = true; }, _ => { });
                rpc.Call<OnSetJobTreeRequestDTO, OnEnchantResponseDTO>("Read", new OnSetJobTreeRequestDTO(), _ => { Check(applied); read = true; }, _ => { }, GameRpcKind.Query);
                Check(transport.Calls.Count == 1); transport.Succeed(0, Enchant());
                Check(applied && transport.Calls.Count == 2); transport.Succeed(1, Enchant()); Check(read && !rpc.IsBusy);
            });
            Test("Double click rejects the second operation without dispatch", () =>
            {
                var transport = new FakeTransport(); var rpc = Client(transport); GameRpcFailure failure = null;
                Enqueue(rpc, "Change", _ => { }, _ => { });
                Check(!Enqueue(rpc, "Change", _ => { }, error => failure = error));
                Check(transport.Calls.Count == 1 && failure.Kind == GameRpcFailureKind.Busy);
            });
            Test("Queued requests own a snapshot of mutable payloads", () =>
            {
                var transport = new FakeTransport(); var rpc = Client(transport);
                Enqueue(rpc, "Change", _ => { }, _ => { });
                var request = new OnRewardRequestDTO { RewardSequence = 12000, Stage = (IdleKingdomFunction.Common.enums.eStage)0x200010001,
                    Loots = new List<HuntResult> { new HuntResult { Count = 1, MonsterType = (IdleKingdomFunction.Common.enums.Scripts.Core.eMonsterType)4294967297UL } } };
                rpc.Call<OnRewardRequestDTO, OnHuntResponseDTO>("Hunt", request, _ => { }, _ => { });
                request.Loots.Clear(); request.RewardSequence = 999;
                transport.Succeed(0, Enchant());
                var sent = (OnRewardRequestDTO)transport.Calls[1].Request.FunctionParameter;
                Check(sent.Loots.Count == 1 && sent.RewardSequence == 12000);
            });
            Test("Account reset discards active/queued callbacks and starts an independent stream", () =>
            {
                var transport = new FakeTransport(); var rpc = Client(transport); int old = 0, current = 0;
                Enqueue(rpc, "Change", _ => old++, _ => old++); Enqueue(rpc, "Queued", _ => old++, _ => old++);
                rpc.ResetAccount(); rpc.SetSession("session-B");
                Enqueue(rpc, "Change", _ => current++, _ => current++);
                transport.Succeed(0, Enchant()); Check(old == 0 && current == 0 && rpc.IsBusy);
                transport.Succeed(1, Enchant()); Check(current == 1 && old == 0 && !rpc.IsBusy && transport.Calls.Count == 2);
            });
            Test("SDK failure retains original error and blocks queued/new mutations without retry", () =>
            {
                var transport = new FakeTransport(); var rpc = Client(transport); GameRpcFailure actual = null, blocked = null;
                Enqueue(rpc, "Change", _ => { }, error => actual = error); Enqueue(rpc, "Queued", _ => { }, error => blocked = error);
                var error = new PlayFabError { ErrorMessage = "network" }; transport.Calls[0].Failure(error);
                Check(actual.Kind == GameRpcFailureKind.Transport && ReferenceEquals(actual.TransportError, error));
                Check(rpc.NeedsStateRefresh && blocked.Kind == GameRpcFailureKind.RecoveryRequired && transport.Calls.Count == 1);
                Check(!Enqueue(rpc, "Another", _ => { }, _ => { }));
            });
            Test("Timeout rejects once; late success cannot apply or retry the mutation", () =>
            {
                double now = 0; var transport = new FakeTransport(); var rpc = new GameRpcClient(transport, () => now); rpc.SetSession("session-A");
                int success = 0, failure = 0;
                Enqueue(rpc, "Change", _ => success++, error => { Check(error.Kind == GameRpcFailureKind.Timeout); failure++; });
                now = 31; rpc.Tick(); rpc.Tick(); transport.Succeed(0, Enchant());
                Check(success == 0 && failure == 1 && rpc.NeedsStateRefresh && transport.Calls.Count == 1);
            });
            Test("Application exceptions block following mutations and release the queue", () =>
            {
                var transport = new FakeTransport(); var rpc = Client(transport); GameRpcFailure failure = null;
                Enqueue(rpc, "Change", _ => throw new InvalidOperationException("apply failed"), error => failure = error);
                transport.Succeed(0, Enchant());
                Check(failure.Kind == GameRpcFailureKind.ApplyFailed && rpc.NeedsStateRefresh && !rpc.IsBusy);
            });
            Test("Failing UI/error observers cannot strand the queue", () =>
            {
                var transport = new FakeTransport(); var rpc = Client(transport);
                rpc.CallbackException += _ => throw new InvalidOperationException("diagnostic observer");
                Enqueue(rpc, "Change", _ => throw new InvalidOperationException("view"), _ => throw new InvalidOperationException("error view"));
                transport.Succeed(0, Enchant());
                Check(!rpc.IsBusy && rpc.NeedsStateRefresh);
                rpc.Call<OnSetJobTreeRequestDTO, OnEnchantResponseDTO>("Read", new OnSetJobTreeRequestDTO(), _ => { }, _ => { }, GameRpcKind.Query);
                Check(transport.Calls.Count == 2);
            });
            Test("Business rejection preserves state and allows the next request", () =>
            {
                var transport = new FakeTransport(); var rpc = Client(transport);
                Enqueue(rpc, "Change", _ => throw new Exception("Must not apply"), _ => { });
                Enqueue(rpc, "Next", _ => { }, _ => { });
                transport.Succeed(0, JObject.Parse("{\"ProtocolVersion\":2,\"ServerVersion\":1,\"ErrorCode\":9,\"Message\":\"No\"}"));
                Check(!rpc.NeedsStateRefresh && transport.Calls.Count == 2);
            });
            Test("Protocol mismatch freezes queued queries as well as mutations", () =>
            {
                var transport = new FakeTransport(); var rpc = Client(transport);
                Enqueue(rpc, "Change", _ => { }, _ => { });
                rpc.Call<OnSetJobTreeRequestDTO, OnEnchantResponseDTO>("Read", new OnSetJobTreeRequestDTO(), _ => { }, _ => { }, GameRpcKind.Query);
                var wrong = Enchant(); wrong["ProtocolVersion"] = 3; transport.Succeed(0, wrong);
                Check(rpc.ProtocolMismatch && transport.Calls.Count == 1 && !rpc.IsBusy);
            });
            Test("Hunt sequence: inclusive start range, same-session retention and response acknowledgement", () =>
            {
                var sequence = new HuntRewardSequence(); sequence.SetSession("A", () => 1000); Check(sequence.Begin() == 1000);
                Check(sequence.Next == 1000 && sequence.IsPending); sequence.Acknowledge(1001);
                sequence.SetSession("A", () => throw new Exception("Must not reseed")); Check(sequence.Begin() == 1001);
                sequence.Acknowledge(1002); sequence.SetSession("B", () => 50000);
                Check(sequence.Begin() == 50000); sequence.Acknowledge(50001); Check(sequence.Next == 50001);
            });
            Test("Hunt sequence: unexpected response/failure cannot advance, reseed or overlap", () =>
            {
                var sequence = new HuntRewardSequence(); sequence.SetSession("A", () => 12000); sequence.Begin();
                Throws(() => sequence.Begin()); Throws(() => sequence.Acknowledge(12002));
                Check(sequence.Next == 12000); sequence.Fail(); Throws(() => sequence.Begin());
                sequence.SetSession("A", () => 20000); Check(sequence.Next == 12000 && sequence.NeedsRecovery);
                Throws(() => new HuntRewardSequence().SetSession("A", () => 999));
                Throws(() => new HuntRewardSequence().SetSession("A", () => 50001));
            });
            return passed.ToArray();
        }

        private static GameRpcClient Client(FakeTransport transport)
        { var rpc = new GameRpcClient(transport, () => 0); rpc.SetSession("session-A"); return rpc; }
        private static bool Enqueue(GameRpcClient rpc, string name, Action<OnEnchantResponseDTO> success, Action<GameRpcFailure> failure) =>
            rpc.Call(name, new OnEnchantRequestDTO { Count = 1 }, success, failure);
        private static bool Read<T>(JObject body, out T value, out GameRpcFailure failure) where T : ApiResponseDTO =>
            GameRpcResponse.TryRead("Test", new ExecuteFunctionResult { FunctionName = "Test", FunctionResult = body }, out value, out failure);
        private static JObject Enchant() => JObject.Parse("{\"ProtocolVersion\":2,\"ServerVersion\":1,\"CurrentLevel\":10,\"CurrentGold\":9007199254740993}");
        private static JObject Initial() => JObject.Parse(@"{
            'ProtocolVersion':2,'ServerVersion':1,'SessionID':'server-session','Level':1,'Exp':0,'KillScore':0,
            'CurrentStage':8590000129,'EnchantHPCount':0,'EnchantATKCount':0,
            'CharacterDatas':[{'Index':0,'NickName':'A','JobCode':0,'Hp':50,'Atk':10},
                {'Index':1,'NickName':'B','JobCode':0,'Hp':50,'Atk':10},{'Index':2,'NickName':'C','JobCode':0,'Hp':50,'Atk':10}],
            'CharacterEquipmentDatas':[],'Currency':{'Gold':0,'AncientCoin':0,'ArcaneKnowledge':0,'ClassFragment':0,'Ruby':0},
            'SkillTreeDatas':[{'Code':0}],'Inventory':[],'JobTrees':[{'Index':0,'JobList':[]},{'Index':1,'JobList':[]},{'Index':2,'JobList':[]}]
        }");
        private static void Check(bool condition, string message = "Assertion failed")
        { if (!condition) throw new InvalidOperationException(message); }
        private static void Throws(Action action)
        { try { action(); } catch (InvalidOperationException) { return; } catch (ArgumentOutOfRangeException) { return; } throw new Exception("Expected failure"); }

        private sealed class PrecisionResponse : ApiResponseDTO
        {
            [JsonProperty(Required = Required.Always)] public long Signed { get; set; }
            [JsonProperty(Required = Required.Always)] public ulong Unsigned { get; set; }
            [JsonProperty(Required = Required.Always)] public decimal Amount { get; set; }
        }
        private sealed class FakeTransport : IGameRpcTransport
        {
            public readonly List<Invocation> Calls = new List<Invocation>();
            public void Execute(ExecuteFunctionRequest request, Action<ExecuteFunctionResult> success, Action<PlayFabError> failure) =>
                Calls.Add(new Invocation { Request = request, Success = success, Failure = failure });
            public void Succeed(int index, JObject body) => Calls[index].Success(new ExecuteFunctionResult
                { FunctionName = Calls[index].Request.FunctionName, FunctionResult = body });
        }
        private sealed class Invocation
        {
            public ExecuteFunctionRequest Request;
            public Action<ExecuteFunctionResult> Success;
            public Action<PlayFabError> Failure;
        }
    }
}
