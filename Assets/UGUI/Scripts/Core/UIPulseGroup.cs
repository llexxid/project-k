using System.Collections.Generic;
using UnityEngine;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// 선택 모드 등에서 대상 CanvasGroup들의 alpha를 700ms 주기로 0.35↔1 사이 부드럽게 토글하는
    /// 재사용 펄스 컴포넌트 (기존 MageTowerPopupController.PulseDriver를 top-level 프리팹 부착용으로 추출).
    /// unscaledDeltaTime 기반. 프리팹 루트에 부착해 팝업과 수명을 함께한다.
    /// </summary>
    public sealed class UIPulseGroup : MonoBehaviour
    {
        private const float IntervalSec = 0.7f;
        private const float FadeSec = 0.7f;
        private const float DimAlpha = 0.35f;

        private readonly List<CanvasGroup> _targets = new();
        private bool _running;
        private bool _dim;
        private float _timer;

        public void Begin(List<CanvasGroup> targets)
        {
            Stop();
            if (targets != null) _targets.AddRange(targets);
            _running = _targets.Count > 0;
            _dim = false;
            _timer = 0f;
        }

        public void Stop()
        {
            _running = false;
            for (int i = 0; i < _targets.Count; i++)
                if (_targets[i] != null) _targets[i].alpha = 1f;
            _targets.Clear();
        }

        private void Update()
        {
            if (!_running) return;
            _timer += Time.unscaledDeltaTime;
            if (_timer >= IntervalSec) { _timer -= IntervalSec; _dim = !_dim; }

            float target = _dim ? DimAlpha : 1f;
            float maxDelta = (1f - DimAlpha) / FadeSec * Time.unscaledDeltaTime;
            for (int i = 0; i < _targets.Count; i++)
            {
                var cg = _targets[i];
                if (cg != null) cg.alpha = Mathf.MoveTowards(cg.alpha, target, maxDelta);
            }
        }
    }
}
