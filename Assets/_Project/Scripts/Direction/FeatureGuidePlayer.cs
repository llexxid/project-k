using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Direction
{
    /// <summary>연출 종류별 실행 계약이다. 순서와 저장은 소유자인 Manager가 처리한다.</summary>
    public interface IGameDirectPlayer : IDisposable
    {
        /// <summary>입력 단계 하나를 표시하고 사용자 응답을 반환한다. 취소는 예외로 구별하고 표시·입력 정리까지 마친다.</summary>
        UniTask<GameDirectResult> PlayAsync(GameDirectStep step, CancellationToken cancellationToken);
    }

    /// <summary>Player가 Unity 하이어라키를 검색하지 않도록 화면 준비·표시·입력을 제공하는 연결부다.</summary>
    public interface IFeatureGuideSurface : IDisposable
    {
        event Action<GameDirectResult> Responded;
        bool IsVisible { get; }
        /// <summary>입력 대상이 현재 화면에서 표시 가능한지 확인한다. 저장이나 화면 상태를 변경하지 않는다.</summary>
        bool IsReady(GameDirectTarget target);
        /// <summary>준비된 단계의 화면과 입력을 활성화한다. 응답 이후 단계 결정은 호출자의 책임이다.</summary>
        void Show(GameDirectStep step);
        /// <summary>단계 종료·시스템 중단 시 화면과 입력 차단을 해제한다. 완료 기록을 남기지 않는다.</summary>
        void Hide();
    }

    /// <summary>실제 화면만 제공하는 선택 계약이다. 기존 설명형 테스트 연결부와 Player 계약을 유지한다.</summary>
    public interface IInteractiveGuideSurface
    {
        /// <summary>미확인 단계의 화면을 복원한다. 관련 없는 모달이 있으면 아무 것도 변경하지 않는다.</summary>
        void Prepare(GameDirectStep step);
        /// <summary>직접 클릭 후 목적 화면 도착 또는 저장된 실습 성공을 확인한다.</summary>
        bool IsComplete(GameDirectStep step);
    }

    /// <summary>일반 C# 안내 재생기. 화면이 가려지면 숨기고 같은 단계를 기다리며 게임 시간을 변경하지 않는다.</summary>
    public sealed class FeatureGuidePlayer : IGameDirectPlayer
    {
        private readonly IFeatureGuideSurface _surface;
        private bool _disposed;
        private bool _playing;

        /// <summary>실제 UGUI 연결부 또는 테스트 연결부를 받는다. 연결부의 종료도 이 Player가 책임진다.</summary>
        public FeatureGuidePlayer(IFeatureGuideSurface surface)
            => _surface = surface ?? throw new ArgumentNullException(nameof(surface));

        /// <summary>
        /// 대상이 준비되면 한 단계를 표시하고 확인/건너뛰기를 반환한다. 모달은 일시 중단으로 취급한다.
        /// 토큰 취소는 OperationCanceledException으로 전달하여 사용자 건너뛰기와 저장 의미를 구별한다.
        /// </summary>
        public async UniTask<GameDirectResult> PlayAsync(GameDirectStep step, CancellationToken cancellationToken)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(FeatureGuidePlayer));
            if (_playing) throw new InvalidOperationException("한 Player에서 두 단계를 동시에 재생할 수 없습니다.");
            _playing = true;
            GameDirectResult? response = null;
            bool visible = false;
            // 버튼 클릭 직전에 팝업이 열린 경우도 재검사해 가려진 안내의 응답을 적용하지 않는다.
            void OnResponse(GameDirectResult result)
            {
                if (visible && !cancellationToken.IsCancellationRequested && _surface.IsReady(step.Target))
                    response ??= result;
            }
            _surface.Responded += OnResponse;
            try
            {
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (GameDirectInteraction.SaveFailed) throw new InvalidOperationException("실습 결과를 저장하지 못했습니다. 다음 요청에서 이어갑니다.");
                    var interactive = _surface as IInteractiveGuideSurface;
                    // 클릭으로 원래 대상이 사라지기 전에 저장된 성공/화면 전환부터 확인한다.
                    if (interactive != null && interactive.IsComplete(step)) return GameDirectResult.Confirmed;
                    interactive?.Prepare(step);
                    bool ready = _surface.IsReady(step.Target);
                    if (response.HasValue && ready) return response.Value;
                    if (!ready) response = null;
                    if (ready && (!visible || !_surface.IsVisible)) { GameDirectInteraction.Arm(); _surface.Show(step); visible = true; }
                    else if (!ready && visible) { _surface.Hide(); visible = false; }
                    // NextFrame은 timeScale 0에서도 진행된다. UI 때문에 전투를 정지하거나 다시 시작하지 않는다.
                    await UniTask.NextFrame(cancellationToken: cancellationToken);
                }
            }
            finally
            {
                // 정상 완료, 취소, 예외가 모두 동일한 정리 경로를 거쳐 투명 입력 차단이 남지 않게 한다.
                _surface.Responded -= OnResponse;
                _surface.Hide();
                _playing = false;
            }
        }

        /// <summary>Manager 종료 후 호출한다. 실행 취소는 Manager의 토큰으로 먼저 요청해야 한다.</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _surface.Dispose();
        }
    }
}
