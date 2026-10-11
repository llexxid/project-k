using System;
using System.Collections.Generic;
using IdleKingdomFunction.Common.Dto;
using PlayFab;
using PlayFab.ClientModels;
using Scripts.Server;
using Scripts.Server.Auth;
using UnityEngine;
using ServerStage = IdleKingdomFunction.Common.enums.eStage;

namespace Scripts.Core.Manager
{
    using ItemCode = IdleKingdomFunction.Common.Dto.ItemCode;
    public enum GameLoginState { SignedOut, Authenticating, Loading, Ready, Failed }

    public class NetworkManager : MonoBehaviour
    {
        public static NetworkManager Instance;
        private string _playFabId;
        private string _sessionTicket;
        private PlayFabAuthenticationContext _context;
        private Authentication _authentication;
        private GameRpcClient _rpc;
        private readonly HuntRewardSequence _hunt = new HuntRewardSequence();
        public GameLoginState LoginState { get; private set; }
        public GameRpcFailure LastFailure { get; private set; }
        public bool IsGameDataReady => LoginState == GameLoginState.Ready;
        public bool IsAuthenticating => LoginState == GameLoginState.Authenticating || LoginState == GameLoginState.Loading;
        public bool HasSessionGuid => !string.IsNullOrEmpty(_rpc?.SessionID);
        public bool IsRequestPending => _rpc?.IsBusy == true;
        public bool NeedsStateRefresh => _rpc?.NeedsStateRefresh == true;
        public long AccountGeneration => _rpc?.Generation ?? 0;
        public long NextHuntRewardSequence => _hunt.Next;
        public event Action LoginStateChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            GameFunctionSerializer.Install();
            _rpc = new GameRpcClient(new PlayFabGameRpcTransport(() => _context), () => Time.realtimeSinceStartupAsDouble);
            _rpc.CallbackException += Debug.LogException;
            _authentication = new Authentication(this);
            DontDestroyOnLoad(gameObject);
        }
        private void Update() => _rpc?.Tick();
        private void OnDestroy()
        {
            if (Instance != this) return;
            _rpc?.ResetAccount();
            Instance = null;
        }

        // Existing account-scoped stores use the PlayFab ID, not the game SessionID.
        public string GetSessionID() => IsGameDataReady ? _playFabId : null;
        public string PlayFabId => _playFabId;
        public string GetSessionTicket() => _sessionTicket;
        public string GameSessionID => _rpc?.SessionID;

        internal long BeginAuthentication()
        {
            _rpc.ResetAccount(); _hunt.Reset();
            _playFabId = null; _sessionTicket = null; _context = null; LastFailure = null;
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
            _rpc.SetSession(session);
            _hunt.SetSession(session, () => UnityEngine.Random.Range(1000, 50001));
        }
        internal void FailLogin(GameRpcFailure failure)
        { LastFailure = failure; SetLoginState(GameLoginState.Failed); }
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

        public bool Call<TRequest, TResponse>(string function, TRequest request, Action<TResponse> success, Action<GameRpcFailure> failure,
            GameRpcKind kind = GameRpcKind.Mutation) where TRequest : RequestDTO where TResponse : ApiResponseDTO =>
            CallWhenReady(function, request, success, failure, kind);

        private bool CallWhenReady<TRequest, TResponse>(string function, TRequest request, Action<TResponse> success,
            Action<GameRpcFailure> failure, GameRpcKind kind) where TRequest : RequestDTO where TResponse : ApiResponseDTO
        {
            if (kind != GameRpcKind.Authentication && !IsGameDataReady)
            {
                failure?.Invoke(new GameRpcFailure(GameRpcFailureKind.IntegrationPending, "전체 게임 데이터 로딩이 필요합니다."));
                return false;
            }
            return _rpc.Call(function, request, success, failure, kind);
        }

        public bool OnSignUpInitUser(Action<UserInitResponseDTO> success, Action<GameRpcFailure> failure) =>
            Call("OnSignUpInitUser", new InitUserRequestDTO(), success, failure, GameRpcKind.Authentication);
        // The agreed name is OnLogIn. The server must still rename its old registration.
        public bool OnLogIn(Action<UserInitResponseDTO> success, Action<GameRpcFailure> failure) =>
            Call("OnLogIn", new AuthRequestDTO(), success, failure, GameRpcKind.Authentication);

        public bool OnHuntReward(eStage stage, List<HuntResult> loots, Action<OnHuntResponseDTO> success, Action<GameRpcFailure> failure)
        {
            if (_hunt.IsPending || _hunt.NeedsRecovery || !HasSessionGuid)
            {
                failure?.Invoke(new GameRpcFailure(_hunt.IsPending ? GameRpcFailureKind.Busy : GameRpcFailureKind.RecoveryRequired,
                    "사냥 보상 동기화가 준비되지 않았습니다."));
                return false;
            }
            long sent = _hunt.Begin();
            return Call("OnHuntReward", new OnRewardRequestDTO { Stage = (ServerStage)(long)stage, Loots = loots, RewardSequence = sent },
                (OnHuntResponseDTO response) =>
                {
                    _hunt.ValidateNext(response.NextRewardSequence);
                    success?.Invoke(response);
                    _hunt.Acknowledge(response.NextRewardSequence);
                }, error => { _hunt.Fail(); failure?.Invoke(error); });
        }
        public bool OnStageClear(eStage stage, Action<OnStageClearResponseDTO> success, Action<GameRpcFailure> failure) =>
            Call("OnStageClear", new OnStageClearRequestDTO { ClearedStage = (ServerStage)(long)stage }, success, failure);
        public bool OnGachaEquipmentClick(int count, Action<OnGachaEquipmentClassFragmentResponseDTO> success, Action<GameRpcFailure> failure) =>
            Call("OnGacha_EquipmentClassFragment", new OnGachaRequestDTO { Count = count }, success, failure);
        public bool OnGachaSkillClick(int count, Action<OnGachaSkillArcaneKnowledgeResponseDTO> success, Action<GameRpcFailure> failure) =>
            Call("OnGacha_SkillArcaneKnowledge", new OnGachaRequestDTO { Count = count }, success, failure);
        public bool OnEnchantHp(int count, Action<OnEnchantResponseDTO> success, Action<GameRpcFailure> failure) =>
            Call("OnEnChantHP", new OnEnchantRequestDTO { Count = count }, success, failure);
        public bool OnEnchantATK(int count, Action<OnEnchantResponseDTO> success, Action<GameRpcFailure> failure) =>
            Call("OnEnChantATK", new OnEnchantRequestDTO { Count = count }, success, failure);
        public bool OnEnchantEquipment(ItemCode code, Action<OnEnchantEquipmentResponseDTO> success, Action<GameRpcFailure> failure) =>
            Call("OnEnchantEquipment", new OnEnchantEquipmentRequestDTO { ItemCode = code }, success, failure);
        public bool OnEnchantSkill(SkillCode code, Action<OnEnchantSkillResponseDTO> success, Action<GameRpcFailure> failure) =>
            Call("OnEnChantSkill", new OnEnchantSkillRequestDTO { SkillCode = code }, success, failure);
        public bool OnAwakenSkill(SkillCode code, Action<OnAwakeningSkillResponseDTO> success, Action<GameRpcFailure> failure) =>
            Call("OnAwakeningSkill", new OnAwakeningSkillRequestDTO { SkillCode = code }, success, failure);
        public bool OnGetJob(ulong job, int index, Action<OnGetJobResponseDTO> success, Action<GameRpcFailure> failure) =>
            Call("OnGetJob", new OnGetJobRequestDTO { JobCode = job, Index = index }, success, failure);
        public bool OnChangeJob(ulong job, int index, Action<OnChangeJobResponseDTO> success, Action<GameRpcFailure> failure) =>
            Call("OnChangeJob", new OnChangeJobRequestDTO { JobCode = job, Index = index }, success, failure);
        public bool OnSetJobTree(Action<OnSetJobTreeResponseDTO> success, Action<GameRpcFailure> failure) =>
            Call("OnSetJobTree", new OnSetJobTreeRequestDTO {  }, success, failure);
        public bool OnDeleteAccount(Action<OnDeleteAccountResponseDTO> success, Action<GameRpcFailure> failure) =>
            Call("OnDeleteAccount", new OnDeleteAccountRequestDTO {  }, success, failure);
    }
}

