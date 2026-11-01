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

        // The server runs every call of an account behind one lock and answers 1017 while another call holds it, so
        // session calls from this client go out one at a time in request order (a hunt batch and an enhance tap
        // would otherwise collide). Login calls bypass the queue; BeginAuthentication drops whatever is pending.
        private const float CallTimeoutSeconds = 30f;
        private readonly Queue<Action> _pendingCalls = new Queue<Action>();
        private bool _callInFlight;
        private float _callStartedAt;
        private string _callInFlightName;
        private Action<PlayFabError> _callInFlightTimeout;

        private void EnqueueCall(string function, Action send, Action<PlayFabError> timeout)
        {
            _pendingCalls.Enqueue(() =>
            {
                _callInFlight = true;
                _callStartedAt = Time.realtimeSinceStartup;
                _callInFlightName = function;
                _callInFlightTimeout = timeout;
                send();
            });
            PumpCalls();
        }
        private void PumpCalls()
        {
            if (_callInFlight || _pendingCalls.Count == 0) return;
            _pendingCalls.Dequeue()();
        }
        private void CompleteQueuedCall()
        {
            _callInFlight = false;
            _callInFlightName = null;
            _callInFlightTimeout = null;
            PumpCalls();
        }
        private void ResetCallQueue()
        {
            _pendingCalls.Clear();
            _callInFlight = false;
            _callInFlightName = null;
            _callInFlightTimeout = null;
        }
        private void Update()
        {
            // The SDK has no timeout on the UnityWebRequest path; a lost response must not block the queue forever.
            if (!_callInFlight || Time.realtimeSinceStartup - _callStartedAt < CallTimeoutSeconds) return;
            var timeout = _callInFlightTimeout;
            string function = _callInFlightName;
            Debug.LogWarning($"[Network] {function} 응답이 {CallTimeoutSeconds}초 동안 없어 실패로 처리하고 다음 요청을 보냅니다.");
            timeout?.Invoke(CreateError("Timeout", "서버 응답이 없습니다.", function));
        }

        // Managed initialization runs once on the surviving singleton.
        private void Init()
        {
            _functions = new Dictionary<Type, (string, Type)>(41)
            {
                [typeof(InitUserRequestDTO)] = ("OnSignUpInitUser", typeof(UserInitResponseDTO)),
                [typeof(AuthRequestDTO)] = ("OnLogIn", typeof(UserInitResponseDTO)),
                [typeof(OnRewardRequestDTO)] = ("OnHuntReward", typeof(OnHuntResponseDTO)),
                [typeof(OnStageClearRequestDTO)] = ("OnStageClear", typeof(OnStageClearResponseDTO)),
                [typeof(OnGachaEquipmentClassFragmentRequestDTO)] = ("OnGachaEquipmentClassFragment", typeof(OnGachaEquipmentClassFragmentResponseDTO)),
                [typeof(OnGachaSkillArcaneKnowledgeRequestDTO)] = ("OnGachaSkillArcaneKnowledge", typeof(OnGachaSkillArcaneKnowledgeResponseDTO)),
                [typeof(OnEnchantHPRequestDTO)] = ("OnEnChantHP", typeof(OnEnchantResponseDTO)),
                [typeof(OnEnchantATKRequestDTO)] = ("OnEnChantATK", typeof(OnEnchantResponseDTO)),
                [typeof(OnEnchantEquipmentRequestDTO)] = ("OnEnchantEquipment", typeof(OnEnchantEquipmentResponseDTO)),
                [typeof(OnEnchantSkillRequestDTO)] = ("OnEnChantSkill", typeof(OnEnchantSkillResponseDTO)),
                [typeof(OnAwakeningSkillRequestDTO)] = ("OnAwakeningSkill", typeof(OnAwakeningSkillResponseDTO)),
                [typeof(OnGetJobRequestDTO)] = ("OnGetJob", typeof(OnGetJobResponseDTO)),
                [typeof(OnChangeJobRequestDTO)] = ("OnChangeJob", typeof(OnChangeJobResponseDTO)),
                [typeof(OnSetJobTreeRequestDTO)] = ("OnSetJobTree", typeof(OnSetJobTreeResponseDTO)),
                [typeof(OnDeleteAccountRequestDTO)] = ("OnDeleteAccount", typeof(OnDeleteAccountResponseDTO)),
                [typeof(OnGetUserStateRequestDTO)] = ("OnGetUserState", typeof(OnGetUserStateResponseDTO)),
                [typeof(OnGetEquipmentInventoryRequestDTO)] = ("OnGetEquipmentInventory", typeof(OnGetEquipmentInventoryResponseDTO)),
                [typeof(OnEquipEquipmentRequestDTO)] = ("OnEquipEquipment", typeof(OnEquipEquipmentResponseDTO)),
                [typeof(OnUnequipEquipmentRequestDTO)] = ("OnUnequipEquipment", typeof(OnUnequipEquipmentResponseDTO)),
                [typeof(OnSetEquipmentLockRequestDTO)] = ("OnSetEquipmentLock", typeof(OnSetEquipmentLockResponseDTO)),
                [typeof(OnDismantleEquipmentRequestDTO)] = ("OnDismantleEquipment", typeof(OnDismantleEquipmentResponseDTO)),
                [typeof(OnSetAutoDismantleRequestDTO)] = ("OnSetAutoDismantle", typeof(OnSetAutoDismantleResponseDTO)),
                [typeof(OnClaimPendingEquipmentRequestDTO)] = ("OnClaimPendingEquipment", typeof(OnClaimPendingEquipmentResponseDTO)),
                [typeof(OnClaimReserveEquipmentRequestDTO)] = ("OnClaimReserveEquipment", typeof(OnClaimReserveEquipmentResponseDTO)),
                [typeof(OnSetSkillLoadoutRequestDTO)] = ("OnSetSkillLoadout", typeof(OnSetSkillLoadoutResponseDTO)),
                [typeof(OnSetSkillBloomRequestDTO)] = ("OnSetSkillBloom", typeof(OnSetSkillBloomResponseDTO)),
                [typeof(OnResetSkillEnhancementRequestDTO)] = ("OnResetSkillEnhancement", typeof(OnResetSkillEnhancementResponseDTO)),
                [typeof(OnEnterDungeonRequestDTO)] = ("OnEnterDungeon", typeof(OnEnterDungeonResponseDTO)),
                [typeof(OnCompleteDungeonRequestDTO)] = ("OnCompleteDungeon", typeof(OnCompleteDungeonResponseDTO)),
                [typeof(OnExitDungeonRequestDTO)] = ("OnExitDungeon", typeof(OnExitDungeonResponseDTO)),
                [typeof(OnGetQuestStateRequestDTO)] = ("OnGetQuestState", typeof(OnGetQuestStateResponseDTO)),
                [typeof(OnClearQuestRequestDTO)] = ("OnClearQuest", typeof(OnClearQuestResponseDTO)),
                [typeof(OnClearAchievementRequestDTO)] = ("OnClearAchievement", typeof(OnClearAchievementResponseDTO)),
                [typeof(OnClaimQuestRewardRequestDTO)] = ("OnClaimQuestReward", typeof(OnClaimQuestRewardResponseDTO)),
                [typeof(OnReincarnationRequestDTO)] = ("OnReincarnation", typeof(OnReincarnationResponseDTO)),
                [typeof(OnClaimOfflineRewardRequestDTO)] = ("OnClaimOfflineReward", typeof(OnClaimOfflineRewardResponseDTO)),
                [typeof(OnAddRubyRequestDTO)] = ("OnAddRuby", typeof(OnAddRubyResponseDTO)),
                [typeof(OnGetMailListRequestDTO)] = ("OnGetMailList", typeof(OnGetMailListResponseDTO)),
                [typeof(OnClaimMailRewardRequestDTO)] = ("OnClaimMailReward", typeof(OnClaimMailRewardResponseDTO)),
                [typeof(OnCheckAttendanceRequestDTO)] = ("OnCheckAttendance", typeof(OnCheckAttendanceResponseDTO)),
                [typeof(OnClaimAttendanceRewardRequestDTO)] = ("OnClaimAttendanceReward", typeof(OnClaimAttendanceRewardResponseDTO)),
                [typeof(OnClaimGuideRewardRequestDTO)] = ("OnClaimGuideReward", typeof(OnClaimGuideRewardResponseDTO)),
                [typeof(OnClaimAdRewardRequestDTO)] = ("OnClaimAdReward", typeof(OnClaimAdRewardResponseDTO))
            };
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

        // Existing account-scoped stores use the PlayFab ID, not the game SessionID. The ID is known as soon as the
        // PlayFab login succeeds (Loading), so the login snapshot is written to the account's own progression file.
        public string GetSessionID() => _playFabId;
        public string PlayFabId => _playFabId;
        public string GetSessionTicket() => _sessionTicket;
        public string GameSessionID => _sessionGUID;

        internal long BeginAuthentication()
        {
            AccountGeneration++;
            _playFabId = null; _sessionTicket = null; _sessionGUID = null; _context = null; LastFailure = null;
            ResetCallQueue();
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
        internal void CompleteLogin()
        {
            if (!HasSessionGuid) throw new InvalidOperationException("Game session must be set before the login completes.");
            SetLoginState(GameLoginState.Ready);
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
            bool queued = request is SessionRequestDTO;
            bool IsCurrent() => this != null && generation == AccountGeneration;
            // Callbacks may enqueue follow-up calls; those wait until this one has fully completed (FIFO).
            void Done()
            {
                if (queued && IsCurrent()) CompleteQueuedCall();
            }
            void Fail(PlayFabError error)
            {
                if (completed || !IsCurrent()) return;
                completed = true;
                try { errorCallback?.Invoke(UnwrapFailure(cloudFunction.FunctionName, error)); }
                finally { Done(); }
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
                }
                finally { Done(); }
            }
            void Send()
            {
                if (!IsCurrent()) return;
                try { PlayFabCloudScriptAPI.ExecuteFunction(cloudFunction, Success, Fail); }
                catch (Exception ex)
                {
                    Fail(CreateError("Transport", "요청을 전송하지 못했습니다.", cloudFunction.FunctionName, ex));
                    Debug.LogException(ex);
                }
            }

            if (queued) EnqueueCall(cloudFunction.FunctionName, Send, Fail);
            else Send();
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
            // The server rejects a mailbox request whose PlayFabId is not the caller's, so always send the signed-in account.
            if (payload is MailboxRequestDTO mailbox) mailbox.PlayFabId = _playFabId;
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
                // PlayFab reports a non-200 Azure Function response here with the HTTP body embedded in the message.
                failure = TryParseWrappedError(result.Error.Message, out var business)
                    ? CreateError("Business", business.Message, function, business)
                    : CreateError("Function", "서버 함수 실행에 실패했습니다.", function, result.Error);
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
                    user.EnchantHPCount < 0 || user.EnchantATKCount < 0 || user.StageState == null || (long)user.StageState.CurrentStage <= 0 ||
                    user.CharacterDatas.Count != 3 || user.CharacterDatas.Any(x => x == null || x.Index < 0 || x.Index > 2 || x.Hp <= 0 || x.Atk < 0) ||
                    user.CharacterDatas.Select(x => x.Index).Distinct().Count() != 3 ||
                    user.JobTrees.Any(x => x == null || x.Index < 0 || x.Index > 2) ||
                    user.JobTrees.Select(x => x.Index).Distinct().Count() != user.JobTrees.Count)
                    throw new JsonSerializationException("Invalid initial player state.");
                if (user.Currency.Gold < 0 || user.Currency.AncientCoin < 0 || user.Currency.ArcaneKnowledge < 0 ||
                    user.Currency.ClassFragment < 0 || user.Currency.Ruby < 0 || user.Currency.EquipmentStone < 0 ||
                    user.Currency.EquipmentTicket < 0 || user.Currency.MageTicket < 0)
                    throw new JsonSerializationException("Invalid wallet.");
                if (user.AdRewards == null || user.AdRewards.Day == null || user.AdRewards.ClaimedOfferIds == null)
                    throw new JsonSerializationException("Invalid ad reward state.");
                ValidateEquipment(user.Equipment);
                ValidateQuests(user.Quests);
            }
            if (response is OnHuntResponseDTO hunt)
            {
                if (hunt.Level < 1 || hunt.Exp < 0 || hunt.KillScore < 0 || hunt.Gold < 0 || hunt.AncientCoin < 0)
                    throw new JsonSerializationException("Invalid hunt result.");
                ValidateEquipment(hunt.Equipment);
            }
            if (response is OnStageClearResponseDTO clear)
            {
                if (clear.StageState == null || (long)clear.StageState.CurrentStage <= 0) throw new JsonSerializationException("Invalid stage clear result.");
                ValidateEquipment(clear.Equipment);
            }
            if (response is OnEnchantResponseDTO enchant && (enchant.CurrentLevel < 0 || enchant.CurrentGold < 0))
                throw new JsonSerializationException("Invalid enhancement result.");
            if (response is EquipmentActionResponseDTO equipment) ValidateEquipment(equipment.Equipment);
            if (response is OnEnchantEquipmentResponseDTO enchantEquipment && (enchantEquipment.CurrentEquipmentStone < 0 || enchantEquipment.SpentEquipmentStone < 0))
                throw new JsonSerializationException("Invalid equipment enhancement result.");
            if (response is OnDismantleEquipmentResponseDTO dismantle && (dismantle.CurrentEquipmentStone < 0 || dismantle.GrantedEquipmentStone < 0))
                throw new JsonSerializationException("Invalid dismantle result.");
            // 마탑 응답: 스킬 목록·슬롯 5칸·음수 없음. 어긋나면 플레이어 상태에 쓰지 않는다.
            if (response is MageActionResponseDTO mage) ValidateMageTower(mage.MageTowerState);
            if (response is OnEnchantSkillResponseDTO enchantSkill && (enchantSkill.SpentArcaneKnowledge < 0 || enchantSkill.CurrentArcaneKnowledge < 0))
                throw new JsonSerializationException("Invalid skill enhancement result.");
            if (response is OnAwakeningSkillResponseDTO awakenSkill && awakenSkill.SpentFragments < 0)
                throw new JsonSerializationException("Invalid skill awakening result.");
            if (response is OnResetSkillEnhancementResponseDTO resetSkill && (resetSkill.RefundedArcaneKnowledge < 0 || resetSkill.CurrentArcaneKnowledge < 0))
                throw new JsonSerializationException("Invalid skill reset result.");
            if (response is OnGachaEquipmentClassFragmentResponseDTO gacha) ValidateEquipment(gacha.Equipment);
            if (response is QuestActionResponseDTO quests) ValidateQuests(quests.Quests);
            if (response is OnClaimQuestRewardResponseDTO claim) ValidateEquipment(claim.Equipment);
            // 직업 응답: 캐릭터 인덱스 0..2, 트리는 캐릭터별 하나씩(JobList 필수). 어긋나면 플레이어 상태에 쓰지 않는다.
            if (response is JobActionResponseDTO job)
            {
                if (job.Index < 0 || job.Index > 2 || job.JobTrees == null || job.JobTrees.Any(x => x == null || x.Index < 0 || x.Index > 2 || x.JobList == null) ||
                    job.JobTrees.Select(x => x.Index).Distinct().Count() != job.JobTrees.Count)
                    throw new JsonSerializationException("Invalid job result.");
            }
            if (response is OnGetJobResponseDTO unlock && (unlock.SpentClassFragment < 0 || unlock.CurrentClassFragment < 0))
                throw new JsonSerializationException("Invalid job unlock result.");
            // 환생·오프라인·실습 보상 응답: 음수·null이면 플레이어 상태에 쓰지 않는다.
            if (response is OnReincarnationResponseDTO reincarnation)
            {
                var r = reincarnation.Reincarnation;
                if (r == null || r.Day == null || r.Level < 0 || r.Count < 0 || r.TodayCount < 0 || r.LastUtc < 0 || reincarnation.GainedLevel < 0 ||
                    reincarnation.EnchantHPCount < 0 || reincarnation.EnchantATKCount < 0 || reincarnation.StageState == null || (long)reincarnation.StageState.CurrentStage <= 0 || reincarnation.Currency == null)
                    throw new JsonSerializationException("Invalid reincarnation result.");
            }
            if (response is OnClaimOfflineRewardResponseDTO offline && (offline.ElapsedSeconds < 0 || offline.AppliedSeconds < 0 || offline.KillCount < 0 ||
                offline.GainedGold < 0 || offline.GainedExp < 0 || offline.Level < 1 || offline.Exp < 0 || offline.Currency == null))
                throw new JsonSerializationException("Invalid offline reward result.");
            if (response is OnClaimGuideRewardResponseDTO guide && (string.IsNullOrEmpty(guide.GuideId) || guide.GrantedAncientCoin < 0 || guide.Currency == null))
                throw new JsonSerializationException("Invalid guide reward result.");
            if (response is OnClaimAdRewardResponseDTO adReward)
            {
                // 상점 일일 광고는 상품 id가 있고, 오프라인 2배는 없다. 이미 받았으면 지급이 비어 있어야 한다.
                bool shop = adReward.AdType == Protocol.Message.Data.eAdType.ShopDaily;
                if ((!shop && adReward.AdType != Protocol.Message.Data.eAdType.OfflineBonus) || shop == string.IsNullOrEmpty(adReward.OfferId) ||
                    adReward.GrantedCurrencies == null || adReward.GrantedExp < 0 || adReward.Level < 1 || adReward.Exp < 0 ||
                    adReward.Currency == null || adReward.Currency.EquipmentTicket < 0 || adReward.Currency.MageTicket < 0 ||
                    adReward.AdRewards == null || adReward.AdRewards.Day == null || adReward.AdRewards.ClaimedOfferIds == null ||
                    (adReward.AlreadyClaimed && (adReward.GrantedCurrencies.Count != 0 || adReward.GrantedExp != 0)))
                    throw new JsonSerializationException("Invalid ad reward result.");
                foreach (var granted in adReward.GrantedCurrencies)
                {
                    if (granted == null || granted.Amount <= 0)
                        throw new JsonSerializationException("Invalid ad reward grant.");
                }
            }
            if (response is OnCheckAttendanceResponseDTO checkIn)
            {
                ValidateAttendance(checkIn.Attendance);
                if (checkIn.CycleDay < 1 || checkIn.CycleDay > 28 || checkIn.Rewards == null || checkIn.CarryoverMailIds == null)
                    throw new JsonSerializationException("Invalid attendance check-in result.");
            }
            if (response is OnClaimAttendanceRewardResponseDTO attendanceClaim)
            {
                ValidateAttendance(attendanceClaim.Attendance);
                var changes = attendanceClaim.EquipmentChanges;
                if (attendanceClaim.Days == null || attendanceClaim.Days.Count == 0 || attendanceClaim.Rewards == null || attendanceClaim.Currency == null ||
                    changes == null || changes.Items == null || changes.PendingItems == null || changes.ReserveStacks == null)
                    throw new JsonSerializationException("Invalid attendance claim result.");
                ValidateStacks(changes.Items, new HashSet<int>(), true);
                ValidateStacks(changes.PendingItems, new HashSet<int>(), false);
                foreach (var stack in changes.ReserveStacks)
                {
                    if (stack == null || stack.Count <= 0 || (stack.ItemCode.Code >> 56) != 0)
                        throw new JsonSerializationException("Invalid reserve stack.");
                }
            }
            ValidateMailboxResponse(response);
        }

        // 출석판: 칸 수 0~28, 받은 칸은 1~OpenedDays. 출석판 이전 서버(Month 없음)는 빈 판으로 본다.
        private static void ValidateAttendance(AttendanceStateDTO attendance)
        {
            if (attendance == null || attendance.LastDay == null || attendance.TotalDays < 0 || attendance.OpenedDays < 0 || attendance.OpenedDays > 28)
                throw new JsonSerializationException("Invalid attendance state.");
            if (attendance.ClaimedDays != null && attendance.ClaimedDays.Any(d => d < 1 || d > attendance.OpenedDays))
                throw new JsonSerializationException("Invalid attendance claimed days.");
        }

        // 마탑 상태: 스킬 목록·슬롯 5칸이 있고 값이 음수가 아니어야 한다. 보유·중복 검사는 적용 함수(UserManager.ApplyMageTower)가 한다.
        private static void ValidateMageTower(MageTowerStateDTO state)
        {
            if (state == null || state.Skills == null || state.EquippedSkillIds == null || state.EquippedSkillIds.Length != 5)
                throw new JsonSerializationException("Missing mage tower state.");
            foreach (var skill in state.Skills)
            {
                if (skill == null || skill.SkillId < 0 || skill.EnhancementLevel < 0 || skill.AwakeningLevel < 0 || skill.Fragments < 0 || skill.KnowledgeSpent < 0)
                    throw new JsonSerializationException("Invalid mage skill.");
            }
        }

        // 서버 EquipmentRules.Valid와 같은 규칙. 어긋나면 InvalidResponse로 거절해 손상된 인벤토리를 플레이어 상태에 쓰지 않는다.
        internal static void ValidateMailboxResponse(ApiResponseDTO response)
        {
            if (response is OnGetMailListResponseDTO list) KingdomIdle.Mailbox.MailboxProtocol.ValidateList(list);
            else if (response is OnClaimMailRewardResponseDTO claim) KingdomIdle.Mailbox.MailboxProtocol.ValidateClaim(claim);
            else if (response == null) throw new JsonSerializationException("Empty mailbox response.");
        }

        internal static void ValidateEquipment(EquipmentInventoryDTO inventory)
        {
            if (inventory == null || inventory.Items == null || inventory.PendingItems == null || inventory.ReserveStacks == null)
                throw new JsonSerializationException("Missing equipment inventory.");
            if (inventory.Capacity <= 0 || inventory.PendingCapacity < 0 || inventory.AutoDismantleRarityMask < 0 || inventory.AutoDismantleRarityMask > 7)
                throw new JsonSerializationException("Invalid equipment inventory header.");
            var equipped = new HashSet<int>();
            ValidateStacks(inventory.Items, equipped, true);
            ValidateStacks(inventory.PendingItems, equipped, false);
            var keys = new HashSet<(ulong, ulong)>();
            foreach (var stack in inventory.ReserveStacks)
            {
                if (stack == null || stack.Count <= 0 || (stack.ItemCode.Code >> 56) != 0 || stack.ItemCode.GetItemEnchantCount() != 0 ||
                    !keys.Add((stack.ItemCode.Code & ~0xFFFFUL, stack.ItemCode.ExpireTimesc)))
                    throw new JsonSerializationException("Invalid reserve stack.");
            }
        }

        private static void ValidateStacks(List<EquipmentStackDTO> stacks, HashSet<int> equipped, bool allowEquip)
        {
            var keys = new HashSet<(ulong, ulong)>();
            foreach (var stack in stacks)
            {
                if (stack == null || stack.EquippedCharacterIndexes == null) throw new JsonSerializationException("Missing equipment stack.");
                ulong code = stack.ItemCode.Code;
                long count = (long)(code & 0xFFFF);
                if ((code >> 56) != 0 || count <= 0 || stack.EnhancementStonesSpent < 0 || !keys.Add((code & ~0xFFFFUL, stack.ItemCode.ExpireTimesc)))
                    throw new JsonSerializationException("Invalid equipment stack.");
                if ((!allowEquip && stack.EquippedCharacterIndexes.Count > 0) || stack.EquippedCharacterIndexes.Count > count)
                    throw new JsonSerializationException("Invalid equipped indexes.");
                foreach (int index in stack.EquippedCharacterIndexes)
                    if (index < 0 || index > 2 || !equipped.Add(index)) throw new JsonSerializationException("Invalid equipped character index.");
            }
        }

        // 서버 QuestRules.Snapshot의 행 규칙. 기간 키·필수 수·상태 enum이 어긋나면 InvalidResponse다.
        private static void ValidateQuests(QuestStateDTO quests)
        {
            if (quests == null || quests.Rows == null || string.IsNullOrWhiteSpace(quests.DailyPeriodKey) || string.IsNullOrWhiteSpace(quests.WeeklyPeriodKey) || quests.ServerTimeUtc < 0)
                throw new JsonSerializationException("Missing quest state.");
            foreach (var row in quests.Rows)
            {
                if (row == null || row.QuestId <= 0 || string.IsNullOrWhiteSpace(row.PeriodKey) || row.RequiredCount <= 0 || row.Progress < 0 || row.Rewards == null ||
                    !Enum.IsDefined(typeof(QuestRowState), row.State) || !Enum.IsDefined(typeof(QuestCategory), row.Category))
                    throw new JsonSerializationException("Invalid quest row.");
            }
        }

        /// <summary>SDK failure callback: recover the server's ErrorResponseDTO when PlayFab wrapped a non-200 function response.</summary>
        private static PlayFabError UnwrapFailure(string function, PlayFabError error)
        {
            if (error == null) return CreateError("Transport", "서버 응답이 없습니다.", function);
            if (error.CustomData is ErrorResponseDTO) return error;
            if (!TryParseWrappedError(error.ErrorMessage, out var business) && error.ErrorDetails != null)
            {
                foreach (var detail in error.ErrorDetails)
                {
                    if (detail.Value == null) continue;
                    foreach (string line in detail.Value)
                    {
                        if (TryParseWrappedError(line, out business)) break;
                    }
                    if (business != null) break;
                }
            }
            if (business == null) return error;
            var converted = CreateError("Business", business.Message, function, business);
            converted.HttpCode = error.HttpCode;
            converted.HttpStatus = error.HttpStatus;
            converted.ErrorDetails["WrappedBy"] = new List<string> { error.Error.ToString() };
            return converted;
        }

        // PlayFab wraps a non-200 Azure Function response as
        // "Invoking an Azure Function failed with HttpStatus <n> and response body {...}". Read the server body back.
        internal static bool TryParseWrappedError(string text, out ErrorResponseDTO error)
        {
            error = null;
            if (string.IsNullOrEmpty(text)) return false;
            int start = text.IndexOf('{');
            int end = text.LastIndexOf('}');
            if (start < 0 || end <= start) return false;
            try
            {
                var body = JObject.Parse(text.Substring(start, end - start + 1));
                if (body["ErrorCode"]?.Type != JTokenType.Integer) return false;
                var serializer = new JsonSerializer { DateParseHandling = DateParseHandling.None, MaxDepth = 64 };
                serializer.Converters.Add(new StrictIntegerConverter());
                error = body.ToObject<ErrorResponseDTO>(serializer);
                return error != null;
            }
            catch (Exception ex) when (ex is JsonException || ex is ArgumentException || ex is OverflowException || ex is InvalidOperationException)
            {
                error = null;
                return false;
            }
        }

        /// <summary>True when the failure carries the server ErrorResponseDTO (Kind "Business"), so callers can branch on ErrorCode.</summary>
        /// <summary>실패 콜백의 PlayFabError에서 Kind(ErrorDetails["Kind"])를 읽는다. SDK 원시 오류는 빈 문자열이다.</summary>
        public static string GetFailureKind(PlayFabError error)
        {
            if (error?.ErrorDetails == null || !error.ErrorDetails.TryGetValue("Kind", out var kinds) || kinds == null || kinds.Count == 0) return "";
            return kinds[0] ?? "";
        }

        /// <summary>서버 ErrorCode·Kind를 사용자에게 보여 줄 문구로 바꾼다. 강화·가챠·공통 코드만 다루고 나머지는 코드를 붙여 돌려준다.</summary>
        public static string DescribeFailure(PlayFabError error, string action = "요청")
        {
            if (TryGetBusinessError(error, out var business))
            {
                switch ((int)business.ErrorCode)
                {
                    case 3: return "세션이 만료되었습니다. 다시 로그인해 주세요.";
                    case 7: return "뽑기 횟수가 올바르지 않습니다.";
                    case 8: return "고대주화가 부족합니다.";
                    case 9: return "골드가 부족합니다.";
                    case 11: return "장착할 여분의 장비가 없습니다.";
                    case 12: return "지식이 부족합니다.";
                    case 13: return "서버에 없는 스킬이거나 조건을 만족하지 않습니다.";
                    case 14: return "스킬 파편이 부족합니다.";
                    case 15: return "스킬 강화 상한에 도달했습니다.";
                    case 16: return "장비 강화 상한에 도달했습니다.";
                    case 17: return "선택할 수 없는 직업입니다.";
                    case 18: return "캐릭터 정보가 올바르지 않습니다.";
                    case 19: return "직업 조각이 부족합니다.";
                    case 21: return "해금되지 않은 직업입니다. 선행 직업을 먼저 해금해 주세요.";
                    case 1004: return "강화 상한에 도달했습니다.";
                    case 1010: return "각성 상한에 도달했습니다.";
                    case 1012: return "계정 초기화가 필요합니다. 다시 로그인해 주세요.";
                    case 1014:
                    case 1015: return "서버 저장소 오류입니다. 잠시 후 다시 시도해 주세요.";
                    case 1017: return "이전 요청을 처리 중입니다. 잠시 후 다시 시도해 주세요.";
                    case 1018: return "서버에 없는 장비이거나 직업에 맞지 않습니다.";
                    case 1019: return "잠금되었거나 장착 중인 장비는 처리할 수 없습니다.";
                    case 1020: return "가방이 가득 찼습니다. 장비를 정리한 뒤 다시 시도해 주세요.";
                    case 1021: return "강화석이 부족합니다.";
                    case 1022: return "스킬 편성이 올바르지 않습니다.";
                    case 1025: return "던전 진행 중에는 바꿀 수 없습니다. 던전을 끝낸 뒤 다시 시도해 주세요.";
                    case 1028: return "아직 받을 수 없는 퀘스트입니다.";
                    case 1029: return "이미 받은 보상입니다.";
                    case 1030: return "계정 데이터 갱신이 필요합니다. 다시 로그인해 주세요.";
                    case 1031: return "보유 한도를 넘었습니다.";
                    case 1032: return "환생 최대 레벨 300입니다.";
                    case 1033: return "오늘 환생 3회를 모두 사용했습니다. KST 자정에 초기화됩니다.";
                    case 1034: return "이번 환생 사이클에서 메인 보스를 1회 이상 처치해야 합니다.";
                    case 1035: return "환생 쿨다운 중입니다. 잠시 후 다시 시도해 주세요.";
                    case 1036: return "메인 일반 웨이브에서만 환생할 수 있습니다. 보스전·던전 종료 후 다시 확인하세요.";
                    case 1037: return "오프라인 보상 요청값이 올바르지 않습니다.";
                    case 1038: return "오늘은 이미 출석했습니다.";
                    case 1039: return "이미 받았거나 없는 우편입니다. 우편함을 다시 확인해 주세요.";
                    case 1040: return "보관 기간이 지난 우편입니다.";
                    case 1041: return "우편함이 가득 찼습니다. 우편을 받은 뒤 다시 시도해 주세요.";
                    case 1042: return "우편 요청이 올바르지 않습니다.";
                    case 1043: return "실습 보상을 받을 수 없는 단계입니다.";
                    case 1044: return "출석 보상 요청이 올바르지 않습니다.";
                    case 1045: return "출석부가 새 달로 바뀌었습니다. 잠시 후 다시 확인해 주세요.";
                    case 1046: return "아직 열리지 않은 출석 보상입니다.";
                    case 1047: return "이번 달 출석부를 모두 채웠습니다. 다음 달 1일에 새 출석부가 열립니다.";
                    case 1048: return "개발 계정 전용 기능입니다.";
                    case 1049: return "테스트 요청 값이 올바르지 않습니다.";
                    case 1050: return "광고 보상 요청이 올바르지 않습니다.";
                    case 1051: return "받을 수 있는 오프라인 2배 보상이 없습니다.";
                    default: return $"{action}에 실패했습니다. (오류 {(int)business.ErrorCode})";
                }
            }
            string kind = GetFailureKind(error);
            if (kind == "Timeout" || kind == "Transport") return "서버 응답이 없습니다. 잠시 후 다시 시도해 주세요.";
            if (kind == "NotAuthenticated" || kind == "IntegrationPending") return "서버 로그인이 필요합니다.";
            return error == null ? $"{action}에 실패했습니다." : $"{action}에 실패했습니다. ({error.ErrorMessage})";
        }

        public static bool TryGetBusinessError(PlayFabError error, out ErrorResponseDTO business)
        {
            business = error?.CustomData as ErrorResponseDTO;
            return business != null;
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
