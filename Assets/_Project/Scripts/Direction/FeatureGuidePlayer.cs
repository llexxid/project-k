using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Direction
{
    /// <summary>연출 종류별 실행 계약이다. 순서와 저장은 소유자인 Manager가 처리한다.</summary>
    public interface IGameDirectPlayer : IDisposable
    {
        UniTask<GameDirectResult> PlayAsync(GameDirectStep step, CancellationToken cancellationToken);
    }

    /// <summary>Player가 Unity 하이어라키를 검색하지 않도록 화면 준비·표시·입력을 제공하는 연결부다.</summary>
    public interface IFeatureGuideSurface : IDisposable
    {
        event Action<GameDirectResult> Responded;
        bool IsVisible { get; }
        bool IsReady(GameDirectTarget target);
        void Show(GameDirectStep step);
        void Hide();
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
                    bool ready = _surface.IsReady(step.Target);
                    if (response.HasValue && ready) return response.Value;
                    if (!ready) response = null;
                    if (ready && (!visible || !_surface.IsVisible)) { _surface.Show(step); visible = true; }
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
