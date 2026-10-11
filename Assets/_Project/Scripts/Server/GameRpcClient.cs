using System;
using System.Collections.Generic;
using IdleKingdomFunction.Common;
using IdleKingdomFunction.Common.Dto;
using Newtonsoft.Json;
using PlayFab;
using PlayFab.CloudScriptModels;

namespace Scripts.Server
{
    public interface IGameRpcTransport
    {
        void Execute(ExecuteFunctionRequest request, Action<ExecuteFunctionResult> success, Action<PlayFabError> failure);
    }

    public sealed class PlayFabGameRpcTransport : IGameRpcTransport
    {
        private readonly Func<PlayFabAuthenticationContext> _context;
        public PlayFabGameRpcTransport(Func<PlayFabAuthenticationContext> context) => _context = context;
        public void Execute(ExecuteFunctionRequest request, Action<ExecuteFunctionResult> success, Action<PlayFabError> failure)
        {
            request.AuthenticationContext = _context();
            PlayFabCloudScriptAPI.ExecuteFunction(request, success, failure);
        }
    }

    public enum GameRpcKind { Authentication, Query, Mutation }

    /// <summary>One ordered stream per authenticated account. Session generations never go on the wire.</summary>
    public sealed class GameRpcClient
    {
        private readonly IGameRpcTransport _transport;
        private readonly Func<double> _clock;
        private readonly Queue<PendingCall> _queue = new Queue<PendingCall>();
        private readonly HashSet<string> _pendingFunctions = new HashSet<string>();
        private PendingCall _active;
        private bool _pumping;
        private double _started;
        private long _generation;
        public string SessionID { get; private set; }
        public bool NeedsStateRefresh { get; private set; }
        public bool ProtocolMismatch { get; private set; }
        public bool IsBusy => _active != null || _queue.Count != 0;
        public long Generation => _generation;
        public const double TimeoutSeconds = 30;
        public event Action<Exception> CallbackException;

        public GameRpcClient(IGameRpcTransport transport, Func<double> clock)
        { _transport = transport ?? throw new ArgumentNullException(nameof(transport)); _clock = clock ?? throw new ArgumentNullException(nameof(clock)); }

        public void ResetAccount()
        {
            _generation++;
            SessionID = null;
            _active = null;
            _queue.Clear();
            _pendingFunctions.Clear();
            NeedsStateRefresh = false;
            ProtocolMismatch = false;
        }

        public void SetSession(string session)
        {
            if (string.IsNullOrWhiteSpace(session)) throw new ArgumentException("Empty game session.", nameof(session));
            if (SessionID != null && SessionID != session)
                throw new InvalidOperationException("Reset the account before replacing its server session.");
            SessionID = session;
        }

        // Only the full state consumer may resume mutations, after validating and applying its response.
        internal void StateRefreshApplied() => NeedsStateRefresh = false;

        public bool Call<TRequest, TResponse>(string function, TRequest request, Action<TResponse> success,
            Action<GameRpcFailure> failure, GameRpcKind kind = GameRpcKind.Mutation)
            where TRequest : RequestDTO where TResponse : ApiResponseDTO
        {
            if (string.IsNullOrWhiteSpace(function) || request == null) throw new ArgumentException("RPC name and request are required.");
            if (ProtocolMismatch) return Reject(failure, GameRpcFailureKind.Protocol, "서버와 앱의 버전이 맞지 않습니다.");
            if (kind != GameRpcKind.Authentication && (request is not SessionRequestDTO || string.IsNullOrEmpty(SessionID)))
                return Reject(failure, GameRpcFailureKind.NotAuthenticated, "서버 로그인부터 완료해 주세요.");
            if (kind == GameRpcKind.Authentication && request is SessionRequestDTO)
                throw new ArgumentException("Authentication must not send a game session.");
            if (NeedsStateRefresh && kind == GameRpcKind.Mutation)
                return Reject(failure, GameRpcFailureKind.RecoveryRequired, "처리 결과를 확인하지 못했습니다. 서버 상태를 다시 불러와야 합니다.");
            if (!_pendingFunctions.Add(function)) return Reject(failure, GameRpcFailureKind.Busy, "같은 요청을 처리 중입니다.");
            request.ProtocolVersion = ApiVersions.ProtocolVersion;
            if (request is SessionRequestDTO session) session.SessionID = SessionID;
            TRequest snapshot;
            try
            {
                // Capture mutable arrays/DTOs now so UI changes cannot change an already queued request.
                snapshot = JsonConvert.DeserializeObject<TRequest>(JsonConvert.SerializeObject(request), GameFunctionSerializer.Settings);
            }
            catch
            {
                _pendingFunctions.Remove(function);
                throw;
            }
            var call = new PendingCall
            {
                Function = function, Generation = _generation, Kind = kind, Failure = failure,
                Request = new ExecuteFunctionRequest { FunctionName = function, FunctionParameter = snapshot, GeneratePlayStreamEvent = false }
            };
            call.ReadAndApply = result =>
            {
                if (!GameRpcResponse.TryRead<TResponse>(function, result, out var response, out var error)) return error;
                success?.Invoke(response);
                return null;
            };
            _queue.Enqueue(call);
            Pump();
            return true;
        }

        public void Tick()
        {
            if (_active != null && _clock() - _started >= TimeoutSeconds)
                Complete(_active, null, new GameRpcFailure(GameRpcFailureKind.Timeout, "응답 대기 시간이 초과되었습니다. 요청을 자동 재전송하지 않습니다."));
        }

        private void Pump()
        {
            if (_pumping) return;
            _pumping = true;
            try
            {
                while (_active == null && _queue.Count > 0)
                {
                    var call = _queue.Dequeue();
                    if (call.Generation != _generation) continue;
                    _active = call;
                    _started = _clock();
                    if (ProtocolMismatch || (NeedsStateRefresh && call.Kind == GameRpcKind.Mutation))
                    {
                        Complete(call, null, new GameRpcFailure(ProtocolMismatch ? GameRpcFailureKind.Protocol : GameRpcFailureKind.RecoveryRequired,
                            "서버 상태 확인 전에는 추가 변경을 보낼 수 없습니다."));
                        continue;
                    }
                    try
                    {
                        _transport.Execute(call.Request, result => Complete(call, result, null),
                            error => Complete(call, null, new GameRpcFailure(GameRpcFailureKind.Transport, "서버에 연결하지 못했습니다.", transport: error)));
                    }
                    catch (Exception ex)
                    {
                        Complete(call, null, new GameRpcFailure(GameRpcFailureKind.Transport, "요청을 전송하지 못했습니다."));
                        ReportException(ex);
                    }
                }
            }
            finally { _pumping = false; }
        }

        private void Complete(PendingCall call, ExecuteFunctionResult result, GameRpcFailure failure)
        {
            if (!ReferenceEquals(_active, call) || call.Generation != _generation) return;
            try
            {
                if (failure == null) failure = call.ReadAndApply(result);
            }
            catch (Exception ex)
            {
                failure = new GameRpcFailure(GameRpcFailureKind.ApplyFailed, "서버 결과 적용에 실패했습니다. 상태를 다시 불러와야 합니다.");
                ReportException(ex);
            }
            // A consumer may start a new login while handling this completion.
            if (call.Generation != _generation) return;
            if (failure != null)
            {
                if (failure.Kind == GameRpcFailureKind.Protocol) ProtocolMismatch = true;
                if (call.Kind == GameRpcKind.Mutation && failure.OutcomeUnknown) NeedsStateRefresh = true;
                try { call.Failure?.Invoke(failure); }
                catch (Exception ex) { ReportException(ex); }
            }
            if (call.Generation != _generation) return;
            _pendingFunctions.Remove(call.Function);
            _active = null;
            Pump();
        }

        private static bool Reject(Action<GameRpcFailure> callback, GameRpcFailureKind kind, string message)
        { callback?.Invoke(new GameRpcFailure(kind, message)); return false; }

        private void ReportException(Exception exception)
        {
            if (CallbackException == null) return;
            foreach (Action<Exception> observer in CallbackException.GetInvocationList())
                try { observer(exception); } catch { /* Diagnostics must not strand the request queue. */ }
        }

        private sealed class PendingCall
        {
            public string Function;
            public long Generation;
            public GameRpcKind Kind;
            public ExecuteFunctionRequest Request;
            public Func<ExecuteFunctionResult, GameRpcFailure> ReadAndApply;
            public Action<GameRpcFailure> Failure;
        }
    }
}
