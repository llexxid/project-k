using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using KingdomIdle.Balance;
using KingdomIdle.UGUI;
using Scripts.Core.Manager;
using UnityEngine;

namespace Direction
{
    /// <summary>
    /// 조건부 연출의 공통 실행 창구다. 로그인 수단과 관계없이 메인 진입 시 미완료 안내를 요청한다.
    /// GameManager 오브젝트에 붙고 각 연출 Player의 생성·취소·폐기를 소유한다.
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
        private Request _activeRequest;
        private long _guideLoginGeneration = -1;
        private static readonly string[] GuideIds = {
            "guide_development", "guide_kingdom_army", "guide_dungeon", "guide_gacha", "guide_mage_tower"
        };
        public string ActiveSequenceId => _activeId;
        public int PendingCount => _queue.Count;
        public string LastError { get; private set; }

        private sealed class Request
        {
            public string Id;
            public GameDirectStep[] Steps;
            public bool Preview;
            public bool Interactive;
            public long AccountGeneration;
            public long NetworkGeneration;
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

        private void Update()
        {
            // 같은 계정의 재로그인도 이전 입력·응답의 실행 범위를 끝낸다.
            if (_activeRequest != null && !_activeRequest.Preview &&
                (!GameDirectProgressStore.Available || !GameDirectProgressStore.IsCurrent(_activeRequest.AccountGeneration, _activeRequest.NetworkGeneration)))
                ClearRequests();
        }

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

        public void RequestIncompleteGuides()
        {
            var network = NetworkManager.Instance;
            if (!isActiveAndEnabled || network == null || !network.IsGameDataReady ||
                LocalProgression.AccountKey != network.PlayFabId ||
                !LocalProgression.IsReady ||
                _guideLoginGeneration == network.AccountGeneration) return;
            if (!GameDirectProgressStore.Available) { LastError = GameDirectProgressStore.NotReadyMessage; return; }
            // 던전 복귀 등 같은 로그인 내 씬 재진입으로 중단한 안내를 다시 띄우지 않는다.
            _guideLoginGeneration = network.AccountGeneration;
            foreach (string id in GuideIds)
            {
                if (!_store.TryLoad(id, LocalProgression.AccountGeneration, out var progress))
                {
                    LastError = "안내 진행 정보를 읽을 수 없습니다: " + id;
                    Debug.LogError("[GameDirect] " + LastError);
                    return;
                }
                if (progress.Completed || progress.Skipped) continue;
                if (!RequestPlay(id))
                {
                    Debug.LogError("[GameDirect] " + LastError);
                    return;
                }
            }
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
            if (preview && sequence.IsInteractive) { LastError = "실습형 안내는 현재 계정에 저장하는 일반 실행으로 요청하세요."; return false; }
            if (_requestedIds.Contains(sequence.sequenceId)) { LastError = "이미 요청된 연출입니다."; return false; }
            long generation = LocalProgression.AccountGeneration;
            if (!preview)
            {
                if (!_store.TryLoad(sequence.sequenceId, generation, out var progress))
                { LastError = GameDirectProgressStore.NotReadyMessage; return false; }
                if (progress.Completed || progress.Skipped) { LastError = "이미 확인하거나 건너뛴 안내입니다."; return false; }
            }
            var steps = new GameDirectStep[sequence.steps.Length];
            for (int i = 0; i < steps.Length; i++) steps[i] = new GameDirectStep(sequence.steps[i], i + 1, steps.Length);
            _requestedIds.Add(sequence.sequenceId);
            _queue.Enqueue(new Request { Id = sequence.sequenceId, Steps = steps, Preview = preview, Interactive = sequence.IsInteractive,
                AccountGeneration = generation, NetworkGeneration = NetworkManager.Instance != null ? NetworkManager.Instance.AccountGeneration : -1,
                ExecutionGeneration = _executionGeneration });
            if (!_pumping) PumpAsync().Forget();
            return true;
        }

        /// <summary>현재 연출만 중단한다. 확인한 단계는 유지하고 나머지 대기 요청은 순서대로 진행한다.</summary>
        public void CancelCurrent() { GameDirectInteraction.End(); _currentCancellation?.Cancel(); }

        /// <summary>계정 전환 시 이전 계정의 실행과 대기열을 함께 폐기한다. 새 계정으로 결과가 넘어갈 수 없다.</summary>
        private void HandleAccountChanged() => ClearRequests();

        /// <summary>수명 종료 시 실행 세대를 먼저 바꾼 뒤 취소한다. 큐를 비워도 실행 중 작업의 finally는 끝까지 수행한다.</summary>
        private void ClearRequests()
        {
            _executionGeneration++;
            GameDirectInteraction.End();
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
                    _activeRequest = request;
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
                        GameDirectInteraction.End();
                        _currentCancellation = null;
                        _activeId = null;
                        _activeRequest = null;
                        // 계정 전환 후 새로 등록된 같은 ID를 이전 실행의 finally가 지우면 안 된다.
                        if (request.ExecutionGeneration == _executionGeneration) _requestedIds.Remove(request.Id);
                    }
                }
            }
            finally { _pumping = false; }
        }

        /// <summary>서버에서 확인되지 않은 단계만 실행하고 저장 응답을 받은 뒤 다음 단계로 이동한다.</summary>
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
                // 실습 지급은 서버가 한다. 단계를 표시하기 전에 응답을 기다리고, 실패하면 이 요청을 끝내 다음 요청에서 같은 단계부터 재개한다.
                if (!request.Preview && step.GrantPracticeCoins &&
                    !await GameDirectInteraction.ClaimPracticeCoinsAsync(request.Id, step, request.AccountGeneration, token))
                    throw new InvalidOperationException("실습 보상을 받지 못했습니다. 다음 요청에서 이어갈 수 있습니다.");
                GameDirectInteraction.Begin(request.Id, step, request.AccountGeneration, () => true);
                GameDirectResult result;
                try { result = await _player.PlayAsync(step, token); }
                finally { GameDirectInteraction.End(); }
                token.ThrowIfCancellationRequested();
                if (request.ExecutionGeneration != _executionGeneration ||
                    request.AccountGeneration != LocalProgression.AccountGeneration ||
                    (!request.Preview && !GameDirectProgressStore.IsCurrent(request.AccountGeneration, request.NetworkGeneration))) throw new OperationCanceledException();
                // 나중에 계속은 사용자 건너뛰기와 다르다. 현재 단계를 미확인으로 남겨 외부 재요청 시 재개한다.
                if (result == GameDirectResult.Deferred) return;
                var candidate = progress.Copy();
                if (result == GameDirectResult.Skipped) candidate.Skipped = true;
                else candidate.ConfirmedSteps.Add(step.Id);
                candidate.Completed = !candidate.Skipped && Array.TrueForAll(request.Steps, s => candidate.ConfirmedSteps.Contains(s.Id));
                if (!request.Preview)
                {
                    bool notified = false;
                    while (!await _store.TrySaveAsync(request.Id, request.AccountGeneration, candidate, token))
                    {
                        token.ThrowIfCancellationRequested();
                        LastError = "안내 진행을 서버에 저장하지 못했습니다. 연결되면 다시 확인합니다.";
                        if (!notified) { UIManager.Instance?.ShowToast(LastError); notified = true; }
                        // 표시는 이미 정리됐다. 게임 입력을 막지 않고 동일 단계의 확인만 재전송한다.
                        await UniTask.Delay(TimeSpan.FromSeconds(5), ignoreTimeScale: true, cancellationToken: token);
                    }
                    if (!_store.TryLoad(request.Id, request.AccountGeneration, out candidate)) throw new OperationCanceledException();
                    LastError = null;
                }
                // 서버가 수락한 합집합만 진행으로 인정한다. 미리보기는 이 지역 변수에서만 진행한다.
                progress = candidate;
                if (progress.Completed || progress.Skipped)
                {
                    if (request.Interactive) UIManager.Instance?.FinishFeatureGuide();
                    return;
                }
            }
        }
    }
}
