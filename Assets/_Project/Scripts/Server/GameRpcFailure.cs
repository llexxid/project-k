using IdleKingdomFunction.Common.Dto;
using PlayFab;
using PlayFab.CloudScriptModels;

namespace Scripts.Server
{
    public enum GameRpcFailureKind
    {
        Transport, Function, Business, Protocol, InvalidResponse,
        NotAuthenticated, Busy, RecoveryRequired, Timeout, ApplyFailed, IntegrationPending
    }

    public sealed class GameRpcFailure
    {
        public GameRpcFailureKind Kind { get; }
        public string Message { get; }
        public PlayFabError TransportError { get; }
        public FunctionExecutionError FunctionError { get; }
        public ErrorResponseDTO BusinessError { get; }

        public GameRpcFailure(GameRpcFailureKind kind, string message,
            PlayFabError transport = null, FunctionExecutionError function = null, ErrorResponseDTO business = null)
        {
            Kind = kind; Message = message; TransportError = transport;
            FunctionError = function; BusinessError = business;
        }

        // A failed transport/function response cannot prove that a mutation was not committed.
        public bool OutcomeUnknown => Kind == GameRpcFailureKind.Transport || Kind == GameRpcFailureKind.Function ||
            Kind == GameRpcFailureKind.Timeout || Kind == GameRpcFailureKind.InvalidResponse ||
            Kind == GameRpcFailureKind.Protocol || Kind == GameRpcFailureKind.ApplyFailed;
    }
}
