using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IdleKingdomFunction.Common;
using Protocol.Message.Dto;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PlayFab;
using PlayFab.ClientModels;
using PlayFab.CloudScriptModels;
using Scripts.Server;
using Scripts.Server.Auth;
using UnityEngine;

namespace Scripts.Core.Manager
{
    public enum GameLoginState { SignedOut, Authenticating, Loading, Ready, Failed }

    public class NetworkManager : MonoBehaviour
    {
        public static NetworkManager Instance;
        private string _playFabId;
        private string _sessionTicket;
        private string _sessionGUID;
        private PlayFabAuthenticationContext _context;
        private Authentication _authentication;
        public GameLoginState LoginState { get; private set; }
        public PlayFabError LastFailure { get; private set; }
        public bool IsGameDataReady => LoginState == GameLoginState.Ready;
        public bool IsAuthenticating => LoginState == GameLoginState.Authenticating || LoginState == GameLoginState.Loading;
        public bool HasSessionGuid => !string.IsNullOrEmpty(_sessionGUID);
        public long AccountGeneration { get; private set; }
        public event Action LoginStateChanged;

        private Dictionary<Type, (string Function, Type Response)> _functions;
        private System.Random _rewardSequenceRandom;

        // Managed initialization runs once on the surviving singleton.
        private void Init()
        {
            _functions = new Dictionary<Type, (string, Type)>(15)
            {
                [typeof(InitUserRequestDTO)] = ("OnSignUpInitUser", typeof(UserInitResponseDTO)),
                [typeof(AuthRequestDTO)] = ("OnLogIn", typeof(UserInitResponseDTO)),
                [typeof(OnRewardRequestDTO)] = ("OnHuntReward", typeof(OnHuntResponseDTO)),
                [typeof(OnStageClearRequestDTO)] = ("OnStageClear", typeof(OnStageClearResponseDTO)),
                [typeof(OnGachaEquipmentClassFragmentRequestDTO)] = ("OnGacha_EquipmentClassFragment", typeof(OnGachaEquipmentClassFragmentResponseDTO)),
                [typeof(OnGachaSkillArcaneKnowledgeRequestDTO)] = ("OnGacha_SkillArcaneKnowledge", typeof(OnGachaSkillArcaneKnowledgeResponseDTO)),
                [typeof(OnEnchantHPRequestDTO)] = ("OnEnChantHP", typeof(OnEnchantResponseDTO)),
                [typeof(OnEnchantATKRequestDTO)] = ("OnEnChantATK", typeof(OnEnchantResponseDTO)),
                [typeof(OnEnchantEquipmentRequestDTO)] = ("OnEnchantEquipment", typeof(OnEnchantEquipmentResponseDTO)),
                [typeof(OnEnchantSkillRequestDTO)] = ("OnEnChantSkill", typeof(OnEnchantSkillResponseDTO)),
                [typeof(OnAwakeningSkillRequestDTO)] = ("OnAwakeningSkill", typeof(OnAwakeningSkillResponseDTO)),
                [typeof(OnGetJobRequestDTO)] = ("OnGetJob", typeof(OnGetJobResponseDTO)),
                [typeof(OnChangeJobRequestDTO)] = ("OnChangeJob", typeof(OnChangeJobResponseDTO)),
                [typeof(OnSetJobTreeRequestDTO)] = ("OnSetJobTree", typeof(OnSetJobTreeResponseDTO)),
                [typeof(OnDeleteAccountRequestDTO)] = ("OnDeleteAccount", typeof(OnDeleteAccountResponseDTO))
            };
            _rewardSequenceRandom = new System.Random();
            _authentication = new Authentication(this);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) 
            { 
                Destroy(gameObject); 
                return; 
            }
            Instance = this;
            Init();
            DontDestroyOnLoad(gameObject);
        }
        private void OnDestroy()
        {
            if (Instance != this) return;
            AccountGeneration++;
            Instance = null;
        }

        // Existing account-scoped stores use the PlayFab ID, not the game SessionID.
        public string GetSessionID() => IsGameDataReady ? _playFabId : null;
        public string PlayFabId => _playFabId;
        public string GetSessionTicket() => _sessionTicket;
        public string GameSessionID => _sessionGUID;

        internal long BeginAuthentication()
        {
            AccountGeneration++;
            _playFabId = null; _sessionTicket = null; _sessionGUID = null; _context = null; LastFailure = null;
            SetLoginState(GameLoginState.Authenticating);
            return AccountGeneration;
        }
        internal void SetAuthenticatedAccount(string playerId, string ticket, PlayFabAuthenticationContext context)
        {
            if (string.IsNullOrWhiteSpace(playerId) || string.IsNullOrWhiteSpace(ticket))
                throw new ArgumentException("Incomplete PlayFab authentication result.");
            _playFabId = playerId; _sessionTicket = ticket; _context = context;
            SetLoginState(GameLoginState.Loading);
        }
        internal void SetGameSession(string session)
        {
            if (string.IsNullOrWhiteSpace(session)) throw new ArgumentException("Empty game session.", nameof(session));
            _sessionGUID = session;
        }
        internal void FailLogin(PlayFabError error)
        { LastFailure = error; SetLoginState(GameLoginState.Failed); }
        private void SetLoginState(GameLoginState state)
        { LoginState = state; LoginStateChanged?.Invoke(); }

        public void AuthenticateTest() => Authenticate(eAuthType.CustomLogin);
        public void DummyLogin() => Authenticate(eAuthType.DummyLogin);
        public void Authenticate(eAuthType type)
        { if (!IsAuthenticating) _authentication.Authenticate(type); }

        public void CheckDuplicatedNickName(string nickname, Action<UpdateUserTitleDisplayNameResult> success, Action<PlayFabError> failure)
        {
            long generation = AccountGeneration;
            new PlayFabClientInstanceAPI(_context).UpdateUserTitleDisplayName(new UpdateUserTitleDisplayNameRequest { DisplayName = nickname },
                result => { if (generation == AccountGeneration) success?.Invoke(result); },
                error => { if (generation == AccountGeneration) failure?.Invoke(error); });
        }

        /// <summary>Build a DTO-routed request and send it directly through the PlayFab SDK.</summary>
        public void Call<TRequest, TResponse>(TRequest request, Action<TResponse> successCallback, Action<PlayFabError> errorCallback)
            where TRequest : RequestDTO where TResponse : ApiResponseDTO
        {
            ExecuteFunctionRequest cloudFunction;
            try { cloudFunction = CreateFunctionRequest<TRequest, TResponse>(request, _sessionGUID, _context); }
            catch (Exception ex) when (ex is ArgumentException || ex is JsonException)
            {
                errorCallback?.Invoke(CreateError("InvalidRequest", ex.Message));
                return;
            }
            catch (InvalidOperationException ex)
            {
                errorCallback?.Invoke(CreateError("NotAuthenticated", ex.Message));
                return;
            }
            if (_context == null)
            {
                errorCallback?.Invoke(CreateError("NotAuthenticated", "로그인이 필요합니다.", cloudFunction.FunctionName));
                return;
            }
            if (request is SessionRequestDTO && !IsGameDataReady)
            {
                errorCallback?.Invoke(CreateError("IntegrationPending", "전체 게임 데이터 로딩이 필요합니다.", cloudFunction.FunctionName));
                return;
            }

            long generation = AccountGeneration;
            bool completed = false;
            bool IsCurrent() => this != null && generation == AccountGeneration;
            void Fail(PlayFabError error)
            {
                if (completed || !IsCurrent()) return;
                completed = true;
                errorCallback?.Invoke(error);
            }
            void Success(ExecuteFunctionResult result)
            {
                if (completed || !IsCurrent()) return;
                if (!TryReadResponse<TResponse>(cloudFunction.FunctionName, result, out var response, out var failure))
                {
                    Fail(failure);
                    return;
                }
                completed = true;
                try { successCallback?.Invoke(response); }
                catch (Exception ex)
                {
                    if (IsCurrent())
                    {
                        errorCallback?.Invoke(CreateError("ApplyFailed",
                            "서버 결과 적용에 실패했습니다. 상태를 다시 불러와야 합니다.", cloudFunction.FunctionName, ex));
                    }
                    Debug.LogException(ex);
                    return;
                }
            }

            try { PlayFabCloudScriptAPI.ExecuteFunction(cloudFunction, Success, Fail); }
            catch (Exception ex)
            {
                Fail(CreateError("Transport", "요청을 전송하지 못했습니다.", cloudFunction.FunctionName, ex));
                Debug.LogException(ex);
            }
        }

        // Type-key lookup only; no type-name parsing, member discovery or reflective invocation.
        private (string Function, Type Response) ResolveFunction<TRequest, TResponse>(TRequest request)
            where TRequest : RequestDTO where TResponse : ApiResponseDTO
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (!_functions.TryGetValue(typeof(TRequest), out var route) || request.GetType() != typeof(TRequest))
                throw new ArgumentException("등록되지 않았거나 구체적이지 않은 요청 DTO입니다.", nameof(request));
            if (route.Response != typeof(TResponse)) throw new ArgumentException("요청 DTO와 응답 DTO가 일치하지 않습니다.");
            return route;
        }

        private ExecuteFunctionRequest CreateFunctionRequest<TRequest, TResponse>(TRequest request, string sessionGuid,
            PlayFabAuthenticationContext context) where TRequest : RequestDTO where TResponse : ApiResponseDTO
        {
            var route = ResolveFunction<TRequest, TResponse>(request);
            if (request is SessionRequestDTO && string.IsNullOrWhiteSpace(sessionGuid))
                throw new InvalidOperationException("서버 로그인이 필요합니다.");

            // Retain an independent payload if the caller clears/reuses a collection after Call.
            var payload = JsonConvert.DeserializeObject<TRequest>(JsonConvert.SerializeObject(request));
            payload.ProtocolVersion = ApiVersions.ProtocolVersion;
            if (payload is SessionRequestDTO session) session.SessionID = sessionGuid;
            // Per-call random value, inclusive at both ends. It is not a counter or a unique ID.
            if (payload is OnRewardRequestDTO hunt) hunt.RewardSequence = _rewardSequenceRandom.Next(1000, 50001);
            return new ExecuteFunctionRequest
            {
                FunctionName = route.Function,
                // The default SDK serializer writes enums as names. Materialize the numeric
                // DTO JSON as plain values so stage and monster IDs stay numeric on the wire.
                FunctionParameter = PluginManager.GetPlugin<ISerializerPlugin>(PluginContract.PlayFab_Serializer)
                    .DeserializeObject(JsonConvert.SerializeObject(payload)),
                AuthenticationContext = context,
                GeneratePlayStreamEvent = true
            };
        }
        private static bool TryReadResponse<T>(string function, ExecuteFunctionResult result, out T response, out PlayFabError failure)
            where T : ApiResponseDTO
        {
            response = null;
            failure = null;
            if (result == null) return FailResponse(function, "InvalidResponse", "서버 응답이 없습니다.", out failure);
            if (result.Error != null)
            {
                failure = CreateError("Function", "서버 함수 실행에 실패했습니다.", function, result.Error);
                return false;
            }
            if (result.FunctionResultTooLarge == true || result.FunctionResult == null ||
                (!string.IsNullOrEmpty(result.FunctionName) && result.FunctionName != function))
                return FailResponse(function, "InvalidResponse", "서버 응답이 누락되었거나 요청과 일치하지 않습니다.", out failure);
            try
            {
                // Validate only our DTO conversion; leave the PlayFab serializer unchanged.
                var serializer = new JsonSerializer { DateParseHandling = DateParseHandling.None, MaxDepth = 64 };
                serializer.Converters.Add(new StrictIntegerConverter());
                JObject body;
                if (result.FunctionResult is JObject json) body = json;
                else if (result.FunctionResult is string text)
                {
                    using var reader = new JsonTextReader(new StringReader(text))
                    { DateParseHandling = DateParseHandling.None, MaxDepth = 64 };
                    body = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                    if (reader.Read()) throw new JsonSerializationException("Trailing response data.");
                }
                else body = JObject.FromObject(result.FunctionResult, serializer);
                if (body["ProtocolVersion"]?.Type != JTokenType.Integer || body["ServerVersion"]?.Type != JTokenType.Integer)
                    throw new JsonSerializationException("Missing protocol header.");
                if (body.Value<int>("ProtocolVersion") != ApiVersions.ProtocolVersion)
                    return FailResponse(function, "Protocol", "서버와 앱의 버전이 맞지 않습니다. 업데이트를 확인해 주세요.", out failure);
                if (body.Value<long>("ServerVersion") < 0) throw new JsonSerializationException("Invalid server version.");
                if (body.Property("ErrorCode") != null)
                {
                    if (body["ErrorCode"].Type != JTokenType.Integer) throw new JsonSerializationException("Invalid error code.");
                    var error = body.ToObject<ErrorResponseDTO>(serializer);
                    failure = CreateError("Business", error.Message, function, error);
                    return false;
                }
                response = body.ToObject<T>(serializer);
                ValidateResponse(response);
                return true;
            }
            catch (Exception ex) when (ex is JsonException || ex is ArgumentException || ex is OverflowException || ex is InvalidOperationException)
            {
                response = null;
                return FailResponse(function, "InvalidResponse", "서버 데이터 형식이 올바르지 않습니다.", out failure);
            }
        }

        private static void ValidateResponse(ApiResponseDTO response)
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

        private static bool FailResponse(string function, string kind, string message, out PlayFabError failure)
        { failure = CreateError(kind, message, function); return false; }

        internal static PlayFabError CreateError(string kind, string message, string function = null, object details = null)
        {
            var errorDetails = new Dictionary<string, List<string>> { ["Kind"] = new List<string> { kind } };
            if (details is ErrorResponseDTO business)
                errorDetails["BusinessErrorCode"] = new List<string> { ((int)business.ErrorCode).ToString() };
            return new PlayFabError
            {
                ApiEndpoint = function,
                Error = kind == "InvalidRequest" ? PlayFabErrorCode.InvalidParams :
                    kind == "NotAuthenticated" ? PlayFabErrorCode.NotAuthenticated : PlayFabErrorCode.Unknown,
                ErrorMessage = message,
                ErrorDetails = errorDetails,
                CustomData = details
            };
        }
    }
}
