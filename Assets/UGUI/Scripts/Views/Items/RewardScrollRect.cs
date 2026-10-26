using UnityEngine;
using UnityEngine.EventSystems;

namespace KingdomIdle.UGUI
{
    /// <summary>가로 보상 목록에서 시작한 세로 드래그는 바깥 우편 목록으로 전달한다.</summary>
    public sealed class RewardScrollRect : UnityEngine.UI.ScrollRect
    {
        private UnityEngine.UI.ScrollRect _parentScroll;
        private bool _dragParent;

        protected override void Awake()
        {
            base.Awake();
            _parentScroll = transform.parent.GetComponentInParent<UnityEngine.UI.ScrollRect>();
        }
        protected override void OnDisable()
        {
            // 드래그 중 우편이 만료되어도 바깥 목록의 드래그 상태를 끝낸다.
            if (_dragParent && _parentScroll != null)
                _parentScroll.OnEndDrag(new PointerEventData(EventSystem.current));
            _dragParent = false;
            base.OnDisable();
        }
        public override void OnInitializePotentialDrag(PointerEventData data)
        {
            base.OnInitializePotentialDrag(data);
            if (_parentScroll != null) _parentScroll.OnInitializePotentialDrag(data);
        }
        public override void OnBeginDrag(PointerEventData data)
        {
            _dragParent = _parentScroll != null && _parentScroll.vertical && Mathf.Abs(data.delta.y) > Mathf.Abs(data.delta.x);
            if (_dragParent) _parentScroll.OnBeginDrag(data);
            else base.OnBeginDrag(data);
        }
        public override void OnDrag(PointerEventData data)
        {
            if (_dragParent) _parentScroll.OnDrag(data);
            else base.OnDrag(data);
        }
        public override void OnEndDrag(PointerEventData data)
        {
            if (_dragParent) _parentScroll.OnEndDrag(data);
            else base.OnEndDrag(data);
            _dragParent = false;
        }
        public override void OnScroll(PointerEventData data)
        {
            if (_parentScroll != null && _parentScroll.vertical && Mathf.Abs(data.scrollDelta.y) >= Mathf.Abs(data.scrollDelta.x))
                _parentScroll.OnScroll(data);
            else base.OnScroll(data);
        }
    }
}
