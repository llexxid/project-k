using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using KingdomIdle.Balance;
using KingdomIdle.UGUI;
using UnityEngine;

namespace Direction
{
    /// <summary>
    /// 조건부 연출의 공통 실행 창구다. 이번 단계는 명시적인 요청만 받으며 자동 발생 조건은 연결하지 않는다.
    /// bootstrap의 영속 GameManager 오브젝트에 붙고, 일반 C# Player의 생성·취소·폐기를 소유한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameDirectManager : MonoBehaviour
    {
        public static GameDirectManager Instance { get; private set; }
        [SerializeField] private GameDirectSequenceSO[] sequences = Array.Empty<GameDirectSequenceSO>();
        private readonly Queue<Request> _queue = new();
        private readonly HashSet<string> _requestedIds = new(StringComparer.Ordinal);
        private readonly GameDirectProgressStore _store = new();
        private IGameDirectPlayer _player;
        private CancellationTokenSource _currentCancellation;
        private bool _pumping;
        private bool _destroyed;
        private long _executionGeneration;
        private string _activeId;
        public string ActiveSequenceId => _activeId;
        public int PendingCount => _queue.Count;
        public string LastError { get; private set; }

        private sealed class Request
        {
            public string Id;
            public GameDirectStep[] Steps;
            public bool Preview;
            public long AccountGeneration;
            public long ExecutionGeneration;
        }

        /// <summary>중복 컴포넌트만 제거하고 Player를 구성한다. 부모 GameManager 오브젝트는 제거하지 않는다.</summary>
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            _player = new FeatureGuidePlayer(new UguiFeatureGuideSurface());
        }

        /// <summary>활성 기간에만 계정 변경을 관찰한다. 계정마다 별도 실행 세대를 사용한다.</summary>
        private void OnEnable() => LocalProgression.AccountChanged += HandleAccountChanged;

        /// <summary>비활성화 시 모든 요청을 취소한다. 잠시 비활성화한 컴포넌트가 보이지 않는 연출을 계속하지 않게 한다.</summary>
        private void OnDisable()
        {
            LocalProgression.AccountChanged -= HandleAccountChanged;
            ClearRequests();
        }

        /// <summary>즉시 입력을 복구하고 Player를 폐기한다. OnDestroy에서 비동기 작업 완료를 기다린다고 가정하지 않는다.</summary>
        private void OnDestroy()
        {
            _destroyed = true;
            ClearRequests();
            _player?.Dispose();
            if (Instance == this) Instance = null;
        }

        /// <summary>등록된 ID로 재생을 요청한다. preview는 세이브를 읽거나 쓰지 않는 수동 미리보기다.</summary>
        public bool RequestPlay(string sequenceId, bool preview = false)
        {
            foreach (var sequence in sequences)
                if (sequence != null && sequence.sequenceId == sequenceId) return RequestPlay(sequence, preview);
            LastError = "등록되지 않은 연출입니다: " + sequenceId;
            return false;
        }

        /// <summary>
        /// 유효한 연출의 실행 복사본을 FIFO에 넣는다. 재생 중 Inspector 변경이나 에셋 수정이 현재 요청을 바꾸지 않는다.
        /// false는 미준비 계정, 중복 요청, 잘못된 정의 또는 이미 처리한 안내를 뜻하며 LastError에 원인을 제공한다.
        /// </summary>
        public bool RequestPlay(GameDirectSequenceSO sequence, bool preview = false)
        {
            LastError = null;
            if (_destroyed || !isActiveAndEnabled || _player == null) { LastError = "연출 관리자가 준비되지 않았습니다."; return false; }
            if (sequence == null) { LastError = "연출 데이터가 없습니다."; return false; }
            if (!sequence.TryValidate(out string error)) { LastError = error; return false; }
            if (_requestedIds.Contains(sequence.sequenceId)) { LastError = "이미 요청된 연출입니다."; return false; }
            long generation = LocalProgression.AccountGeneration;
            if (!preview)
            {
                if (!_store.TryLoad(sequence.sequenceId, generation, out var progress))
                { LastError = "계정 또는 안내 저장 정보를 읽을 수 없습니다."; return false; }
                if (progress.Completed || progress.Skipped) { LastError = "이미 확인하거나 건너뛴 안내입니다."; return false; }
            }
            var steps = new GameDirectStep[sequence.steps.Length];
            for (int i = 0; i < steps.Length; i++) steps[i] = new GameDirectStep(sequence.steps[i], i + 1, steps.Length);
            _requestedIds.Add(sequence.sequenceId);
            _queue.Enqueue(new Request { Id = sequence.sequenceId, Steps = steps, Preview = preview,
                AccountGeneration = generation, ExecutionGeneration = _executionGeneration });
            if (!_pumping) PumpAsync().Forget();
            return true;
        }

        /// <summary>현재 연출만 중단한다. 확인한 단계는 유지하고 나머지 대기 요청은 순서대로 진행한다.</summary>
        public void CancelCurrent() => _currentCancellation?.Cancel();

        /// <summary>계정 전환 시 이전 계정의 실행과 대기열을 함께 폐기한다. 새 계정으로 결과가 넘어갈 수 없다.</summary>
        private void HandleAccountChanged() => ClearRequests();

        /// <summary>수명 종료 시 실행 세대를 먼저 바꾼 뒤 취소한다. 큐를 비워도 실행 중 작업의 finally는 끝까지 수행한다.</summary>
        private void ClearRequests()
        {
            _executionGeneration++;
            _queue.Clear();
            _requestedIds.Clear();
            _currentCancellation?.Cancel();
        }

        /// <summary>단일 소비 루프가 FIFO를 비운다. 각 요청의 예외를 격리하여 후속 요청과 UI 정리가 보장되게 한다.</summary>
        private async UniTask PumpAsync()
        {
            _pumping = true;
            try
            {
                while (!_destroyed && isActiveAndEnabled && _queue.Count > 0)
                {
                    var request = _queue.Dequeue();
                    _activeId = request.Id;
                    using var cancellation = new CancellationTokenSource();
                    _currentCancellation = cancellation;
                    try { await RunRequestAsync(request, cancellation.Token); }
                    catch (OperationCanceledException) { /* 시스템 중단은 완료/건너뛰기 상태를 저장하지 않는다. */ }
                    catch (Exception exception)
                    {
                        LastError = exception.Message;
                        Debug.LogError("[GameDirect] " + exception);
                        UIManager.Instance?.ShowToast("안내를 표시하지 못했습니다. 다시 시도해 주세요.");
                    }
                    finally
                    {
                        _currentCancellation = null;
                        _activeId = null;
                        // 계정 전환 후 새로 등록된 같은 ID를 이전 실행의 finally가 지우면 안 된다.
                        if (request.ExecutionGeneration == _executionGeneration) _requestedIds.Remove(request.Id);
                    }
                }
            }
            finally { _pumping = false; }
        }

        /// <summary>확인되지 않은 단계만 실행한다. 저장 실패는 즉시 요청을 끝내고 다음 요청에서 재시도 가능하게 한다.</summary>
        private async UniTask RunRequestAsync(Request request, CancellationToken token)
        {
            GameDirectProgress progress = new();
            if (!request.Preview && !_store.TryLoad(request.Id, request.AccountGeneration, out progress))
                throw new InvalidOperationException("안내 진행 상태를 읽을 수 없습니다.");
            if (progress.Completed || progress.Skipped) return;
            foreach (var step in request.Steps)
            {
                token.ThrowIfCancellationRequested();
                if (progress.ConfirmedSteps.Contains(step.Id)) continue;
                var result = await _player.PlayAsync(step, token);
                token.ThrowIfCancellationRequested();
                if (request.ExecutionGeneration != _executionGeneration ||
                    request.AccountGeneration != LocalProgression.AccountGeneration) throw new OperationCanceledException();
                var candidate = progress.Copy();
                if (result == GameDirectResult.Skipped) candidate.Skipped = true;
                else candidate.ConfirmedSteps.Add(step.Id);
                candidate.Completed = !candidate.Skipped && Array.TrueForAll(request.Steps, s => candidate.ConfirmedSteps.Contains(s.Id));
                if (!request.Preview && !_store.TrySave(request.Id, request.AccountGeneration, candidate))
                {
                    LastError = "안내 진행을 저장하지 못했습니다.";
                    UIManager.Instance?.ShowToast(LastError + " 다음 요청에서 이어갑니다.");
                    return;
                }
                // 디스크 교체가 성공한 후보만 현재 상태로 인정한다. 미리보기는 이 지역 변수에서만 진행한다.
                progress = candidate;
                if (progress.Completed || progress.Skipped) return;
            }
        }
    }
}
