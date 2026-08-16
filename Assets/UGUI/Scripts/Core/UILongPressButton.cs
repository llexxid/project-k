using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// 짧은 탭과 길게 누르기(기본 0.5초)를 구분하는 재사용 입력 컴포넌트.
    ///  - 탭: 임계 시간 전에 손을 떼면 Tapped
    ///  - 길게: 임계 시간 도달 '즉시' LongPressed 발화(뗄 때까지 기다리지 않음), 이후 탭은 무효
    ///  - 포인터가 밖으로 나가면 둘 다 취소 (Button 클릭 관례와 동일 — 밖에서 떼면 아무 일 없음)
    ///  - 멀티터치: 최초 포인터 1개만 추적한다 (두 번째 손가락은 무시 — 타이머 리셋/이중 발화 방지)
    /// 같은 GameObject의 Selectable.interactable을 존중하되, allowLongPressWhenDisabled 가 켜져 있으면
    /// 비활성 상태에서도 "길게 누르기"만은 판정한다(탭은 억제) — 시전 불가 상태에서도 AUTO 토글은 가능해야 한다.
    /// unscaled time 기준이라 타임스케일 정지 중에도 동작하고, 프레임당 할당이 없다.
    /// 시각 효과(눌림 틴트/SFX)는 기존 Button + PlayClickSfxOnClick에 그대로 맡긴다 —
    /// 이 컴포넌트는 판정만 하므로 Button.onClick에는 리스너를 달지 말 것(이중 발화 방지).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UILongPressButton : MonoBehaviour,
        IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [Tooltip("길게 누르기로 판정되는 시간(초, unscaled).")]
        [SerializeField] internal float longPressSeconds = 0.5f;

        /// <summary>Selectable 이 비활성이어도 길게 누르기 판정은 허용한다 (탭은 계속 억제).</summary>
        internal bool allowLongPressWhenDisabled;

        /// <summary>짧은 탭 (임계 시간 전에 뗌).</summary>
        public event Action Tapped;
        /// <summary>길게 누르기 (임계 시간 도달 시점에 즉시 1회).</summary>
        public event Action LongPressed;

        private const int NoPointer = int.MinValue;

        private Selectable _selectable;
        private bool _pressing;
        private bool _longFired;
        private bool _suppressTap;
        private float _pressStartTime;
        private int _activePointerId = NoPointer;

        private void Awake()
        {
            _selectable = GetComponent<Selectable>();
        }

        private void OnDisable()
        {
            // 눌린 채로 숨겨지면 상태가 얼어붙지 않도록 초기화
            _pressing = false;
            _longFired = false;
            _suppressTap = false;
            _activePointerId = NoPointer;
        }

        private void Update()
        {
            if (!_pressing) return;   // 누르는 중이 아니면 즉시 반환 — 프레임당 작업 없음

            if (Time.unscaledTime - _pressStartTime >= longPressSeconds)
            {
                // 길게 누르기 확정 — 즉시 발화하고 이후 포인터 업의 탭은 막는다
                _pressing = false;
                _longFired = true;
                LongPressed?.Invoke();
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            bool blocked = _selectable != null && !_selectable.interactable;
            if (blocked && !allowLongPressWhenDisabled) return;

            // 이미 다른 포인터가 잡고 있으면 무시 — _pressing 이 아니라 래치로 판정해야 한다
            // (길게 누르기 발화 후 손가락이 아직 붙어 있는 동안 _pressing 은 false 이므로)
            if (_activePointerId != NoPointer) return;
            _activePointerId = eventData.pointerId;

            _suppressTap = blocked;   // 비활성 상태의 누름은 길게만 유효, 탭은 억제
            _pressing = true;
            _longFired = false;
            _pressStartTime = Time.unscaledTime;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != _activePointerId) return;
            _activePointerId = NoPointer;

            bool wasTap = _pressing && !_longFired && !_suppressTap;
            _pressing = false;
            _longFired = false;
            _suppressTap = false;
            if (wasTap) Tapped?.Invoke();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (eventData.pointerId != _activePointerId) return;
            // 누른 채 밖으로 드래그 → 탭/길게 모두 취소. 래치는 유지한다 —
            // uGUI 는 pointer-down 을 받은 오브젝트에 반드시 pointer-up 을 전달하므로
            // 같은 포인터의 업에서 래치가 확실히 풀리고, 그동안 다른 손가락은 계속 차단된다.
            _pressing = false;
        }
    }
}
