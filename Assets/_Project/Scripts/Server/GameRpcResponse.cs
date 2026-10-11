using System;
using System.IO;
using System.Linq;
using IdleKingdomFunction.Common;
using IdleKingdomFunction.Common.Dto;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PlayFab.CloudScriptModels;

namespace Scripts.Server
{
    public static class GameRpcResponse
    {
        public static bool TryRead<T>(string function, ExecuteFunctionResult result, out T response, out GameRpcFailure failure)
            where T : ApiResponseDTO
        {
            response = null;
            failure = null;
            if (result == null) return Fail(GameRpcFailureKind.InvalidResponse, "서버 응답이 없습니다.", out failure);
            if (result.Error != null)
            {
                failure = new GameRpcFailure(GameRpcFailureKind.Function, "서버 함수 실행에 실패했습니다.", function: result.Error);
                return false;
            }
            if (result.FunctionResultTooLarge == true || result.FunctionResult == null ||
                (!string.IsNullOrEmpty(result.FunctionName) && result.FunctionName != function))
                return Fail(GameRpcFailureKind.InvalidResponse, "서버 응답이 누락되었거나 요청과 일치하지 않습니다.", out failure);
            try
            {
                var serializer = JsonSerializer.Create(GameFunctionSerializer.Settings);
                JObject body;
                if (result.FunctionResult is JObject json) body = json;
                else if (result.FunctionResult is string text)
                {
                    using var reader = new JsonTextReader(new StringReader(text))
                    { FloatParseHandling = FloatParseHandling.Decimal, DateParseHandling = DateParseHandling.None, MaxDepth = 64 };
                    body = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                    if (reader.Read()) throw new JsonSerializationException("Trailing response data.");
                }
                else body = JObject.FromObject(result.FunctionResult, serializer);
                if (body["ProtocolVersion"]?.Type != JTokenType.Integer || body["ServerVersion"]?.Type != JTokenType.Integer)
                    throw new JsonSerializationException("Missing protocol header.");
                if (body.Value<int>("ProtocolVersion") != ApiVersions.ProtocolVersion)
                    return Fail(GameRpcFailureKind.Protocol, "서버와 앱의 버전이 맞지 않습니다. 업데이트를 확인해 주세요.", out failure);
                if (body.Value<long>("ServerVersion") < 0) throw new JsonSerializationException("Invalid server version.");
                if (body.Property("ErrorCode") != null)
                {
                    if (body["ErrorCode"].Type != JTokenType.Integer) throw new JsonSerializationException("Invalid error code.");
                    var error = body.ToObject<ErrorResponseDTO>(serializer);
                    failure = new GameRpcFailure(GameRpcFailureKind.Business, error.Message, business: error);
                    return false;
                }
                response = body.ToObject<T>(serializer);
                Validate(response);
                return true;
            }
            catch (Exception ex) when (ex is JsonException || ex is ArgumentException || ex is OverflowException || ex is InvalidOperationException)
            {
                response = null;
                return Fail(GameRpcFailureKind.InvalidResponse, "서버 데이터 형식이 올바르지 않습니다.", out failure);
            }
        }

        internal static void Validate(ApiResponseDTO response)
        {
            if (response == null) throw new JsonSerializationException("Empty response.");
            if (response is UserInitResponseDTO user)
            {
                if (string.IsNullOrWhiteSpace(user.SessionID) || user.Level < 1 || user.Exp < 0 || user.KillScore < 0 ||
                    user.EnchantHPCount < 0 || user.EnchantATKCount < 0 || (long)user.CurrentStage <= 0 ||
                    user.CharacterDatas.Count != 3 || user.CharacterDatas.Any(x => x == null || x.Index < 0 || x.Index > 2 || x.Hp <= 0 || x.Atk < 0) ||
                    user.CharacterDatas.Select(x => x.Index).Distinct().Count() != 3 ||
                    user.JobTrees.Any(x => x == null || x.Index < 0 || x.Index > 2) ||
                    user.JobTrees.Select(x => x.Index).Distinct().Count() != user.JobTrees.Count)
                    throw new JsonSerializationException("Invalid initial player state.");
                if (user.Currency.Gold < 0 || user.Currency.AncientCoin < 0 || user.Currency.ArcaneKnowledge < 0 ||
                    user.Currency.ClassFragment < 0 || user.Currency.Ruby < 0)
                    throw new JsonSerializationException("Invalid wallet.");
            }
            if (response is OnHuntResponseDTO hunt && (hunt.Level < 1 || hunt.Exp < 0 || hunt.KillScore < 0 ||
                hunt.Gold < 0 || hunt.AncientCoin < 0 || hunt.NextRewardSequence < 1))
                throw new JsonSerializationException("Invalid hunt result.");
            if (response is OnEnchantResponseDTO enchant && (enchant.CurrentLevel < 0 || enchant.CurrentGold < 0))
                throw new JsonSerializationException("Invalid enhancement result.");
        }

        private static bool Fail(GameRpcFailureKind kind, string message, out GameRpcFailure failure)
        { failure = new GameRpcFailure(kind, message); return false; }
    }
}
