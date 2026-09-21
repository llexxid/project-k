using System;
using Direction;
using UnityEngine;

namespace KingdomIdle.UGUI
{
    /// <summary>일반 C# Player와 실제 UGUI 사이의 연결부다. View는 필요할 때 한 번 만들고 종료 시 소유 인스턴스만 정리한다.</summary>
    public sealed class UguiFeatureGuideSurface : IFeatureGuideSurface, IInteractiveGuideSurface
    {
        private UIManager _host;
        private FeatureGuideView _view;
        private bool _disposed;
        public event Action<GameDirectResult> Responded;
        public bool IsVisible => _view != null && _view.IsVisible;

        /// <summary>모달·로딩·메뉴와 대상 준비를 읽기만 한다. 안내 자체는 모달 목록에 등록하지 않아 자기 자신에게 막히지 않는다.</summary>
        public bool IsReady(GameDirectTarget target)
        {
            var host = UIManager.Instance;
            var step = GameDirectInteraction.Step;
            return !_disposed && host != null && (step != null ? host.CanRunFeatureGuide(step) : host.CanShowFeatureGuide) &&
                host.GuideTargets.TryGet(DisplayTarget(target), out _);
        }

        /// <summary>매 대기 프레임 현재 단계의 화면 복원을 요청한다. UIManager가 외부 모달과 중복 화면 생성을 차단한다.</summary>
        public void Prepare(GameDirectStep step) => UIManager.Instance?.PrepareFeatureGuide(step);

        /// <summary>성공 영수증과 목적 화면을 확인한다. 뽑기는 결과 창을 직접 닫아야 끝나며 재실행 시에는 영수증으로 재소비를 생략한다.</summary>
        public bool IsComplete(GameDirectStep step)
        {
            var host = UIManager.Instance;
            if (host == null || !host.CanRunFeatureGuide(step)) return false;
            if (step.Completion == GuideCompletion.Click)
                return GameDirectInteraction.Clicked && host.IsGuideContextReady(step.Destination);
            return step.Completion == GuideCompletion.Action && GameDirectInteraction.Succeeded && !GachaResultPopupController.IsOpen;
        }

        /// <summary>뽑기가 저장된 이후에는 원래 뽑기 버튼 대신 결과 확인 버튼을 강조한다. 결과가 없는 재개는 재뽑기하지 않는다.</summary>
        private static GameDirectTarget DisplayTarget(GameDirectTarget target) =>
            GameDirectInteraction.Succeeded && GachaResultPopupController.IsOpen ? GameDirectTarget.GachaResultClose : target;

        /// <summary>현재 UI 루트에 카탈로그 프리팹을 생성하고 한 단계를 표시한다. 없는 프리팹은 무한 대기 대신 명시적으로 실패시킨다.</summary>
        public void Show(GameDirectStep step)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(UguiFeatureGuideSurface));
            var host = UIManager.Instance;
            if (host == null || !host.GuideTargets.TryGet(DisplayTarget(step.Target), out var target))
                throw new InvalidOperationException("안내 대상이 준비되지 않았습니다.");
            if (_view == null || _host != host)
            {
                ReleaseView();
                if (host.Catalog == null || host.Catalog.overlayFeatureGuide == null)
                    throw new InvalidOperationException("기능 안내 프리팹이 카탈로그에 없습니다.");
                _host = host;
                var instance = UnityEngine.Object.Instantiate(host.Catalog.overlayFeatureGuide, host.LayerOverlays, false);
                _view = instance.GetComponent<FeatureGuideView>();
                if (_view == null) { UnityEngine.Object.Destroy(instance); throw new InvalidOperationException("FeatureGuideView 참조가 없습니다."); }
                _view.Responded += HandleResponse;
                _host.ActiveFeatureGuide = _view;
            }
            _view.Show(step, target, () => IsReady(step.Target) && host.GuideTargets.TryGet(DisplayTarget(step.Target), out var current) && current == target);
        }

        /// <summary>View의 한 번뿐인 응답을 Player에 전달한다. 여기서는 진행 저장이나 다음 단계 호출을 하지 않는다.</summary>
        private void HandleResponse(GameDirectResult result) => Responded?.Invoke(result);

        /// <summary>시스템 중단 및 단계 종료 공통 경로다. 비활성화와 동시에 레이캐스트 차단을 해제한다.</summary>
        public void Hide() { GameDirectInteraction.Suspend(); if (_view != null) _view.Hide(); }

        /// <summary>UI 루트가 교체되거나 Player가 종료될 때 자신이 등록한 View만 해제한다.</summary>
        private void ReleaseView()
        {
            if (_host != null && _host.ActiveFeatureGuide == _view) _host.ActiveFeatureGuide = null;
            if (_view != null)
            {
                _view.Responded -= HandleResponse;
                _view.Hide();
                UnityEngine.Object.Destroy(_view.gameObject);
            }
            _view = null; _host = null;
        }

        /// <summary>중복 호출에 안전한 종료다. 입력을 먼저 풀고 프리팹 인스턴스와 이벤트 참조를 해제한다.</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            ReleaseView();
            Responded = null;
        }
    }
}
