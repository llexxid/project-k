using System;
using Protocol.Message.Dto;
using Scripts.Core.Manager;

namespace KingdomIdle.Balance
{
    // shortcut: 기존 성공 콜백은 동기 저장한다. 비동기 적용을 추가할 때는 그 완료 지점에서 명시적으로 revision을 저장한다.
    internal static class EconomyResponseApply
    {
        private static Scope _current;

        internal static Scope Begin(ApiResponseDTO response)
        {
            if (!response.EconomyRevision.HasValue) return null;
            if (!LocalProgression.TryGetCommittedState(out var state) || response.EconomyRevision < state.ServerEconomyRevision)
                throw new InvalidOperationException("서버 경제 상태의 순서를 확인하지 못했습니다.");
            if (_current != null) throw new InvalidOperationException("A server response is already being applied.");
            return _current = new Scope(response, LocalProgression.AccountGeneration, NetworkManager.Instance);
        }

        internal static Attempt BeginLocalApply()
        {
            var scope = _current;
            if (scope == null) return null;
            _current = null; // Changed 관찰자나 후속 저장은 응답의 첫 적용을 대신할 수 없다.
            scope.Attempted = true;
            return new Attempt(scope);
        }

        internal sealed class Scope : IDisposable
        {
            private readonly ApiResponseDTO _response;
            private readonly long _accountGeneration, _networkGeneration;
            private readonly NetworkManager _network;
            internal bool Attempted, Committed;
            internal long Revision => _response.EconomyRevision.Value;
            internal bool Current => _accountGeneration == LocalProgression.AccountGeneration && _network == NetworkManager.Instance &&
                (_network == null || _network.AccountGeneration == _networkGeneration);
            internal Scope(ApiResponseDTO response, long accountGeneration, NetworkManager network)
            { _response = response; _accountGeneration = accountGeneration; _network = network; _networkGeneration = network != null ? network.AccountGeneration : -1; }

            internal bool Complete(bool callbackSucceeded, bool unknownBeforeResponse)
            {
                Dispose();
                if (!callbackSucceeded || !Current) return false;
                if (Attempted) return Committed;
                if (!LocalProgression.TryGetCommittedState(out var state)) return false;
                if (state.ServerEconomyRevision == Revision) return true;
                if (unknownBeforeResponse || state.ServerEconomyRevision > Revision) return false;
                // 이 세 응답은 재화·성장·사냥 누적을 바꾸지 않는다. 우편 조회는 미지급 재화를 복구할 수 있어 제외한다.
                if (!(_response is OnGetQuestStateResponseDTO || _response is OnSaveTutorialProgressResponseDTO || _response is OnCheckAttendanceResponseDTO))
                    return false;
                return LocalProgression.Execute("server-economy-metadata", draft =>
                {
                    if (!Current || draft.ServerEconomyRevision > Revision) return false;
                    draft.ServerEconomyRevision = Revision;
                    return true;
                });
            }

            public void Dispose() { if (ReferenceEquals(_current, this)) _current = null; }
        }

        internal sealed class Attempt : IDisposable
        {
            private readonly Scope _scope;
            internal Attempt(Scope scope) => _scope = scope;
            internal void Prepare(ProgressionState draft)
            {
                if (!_scope.Current || draft.ServerEconomyRevision > _scope.Revision)
                    throw new InvalidOperationException("서버 경제 상태가 변경됐습니다. 다시 확인해 주세요.");
                draft.ServerEconomyRevision = _scope.Revision;
            }
            internal void Commit() => _scope.Committed = true;
            public void Dispose() => _scope.Dispose();
        }
    }
}
