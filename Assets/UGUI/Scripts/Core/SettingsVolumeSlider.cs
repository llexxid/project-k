using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KingdomIdle.UGUI
{
    /// <summary>Horizontal adjustment; vertical gestures scroll the containing settings list without changing audio.</summary>
    public sealed class SettingsVolumeSlider : Slider, IEndDragHandler
    {
        ScrollRect _scroll;
        bool _classified, _vertical, _pressed;
        Vector2 _origin;

        public override void OnInitializePotentialDrag(PointerEventData data)
        {
            _scroll = GetComponentInParent<ScrollRect>();
            data.useDragThreshold = true;
        }
        public override void OnPointerDown(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left || !IsActive() || !IsInteractable()) return;
            _origin = data.position; _classified = _vertical = false; _pressed = true;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(gameObject, data);
            // Wait for gesture direction or pointer release; vertical scrolling must never change volume.
        }
        public override void OnDrag(PointerEventData data)
        {
            if (!_pressed || data.button != PointerEventData.InputButton.Left) return;
            if (!_classified)
            {
                var delta = data.position - _origin;
                _vertical = _scroll != null && Mathf.Abs(delta.y) > Mathf.Abs(delta.x);
                _classified = true;
                if (_vertical) _scroll.OnBeginDrag(data);
                else base.OnPointerDown(data);
            }
            if (_vertical) _scroll.OnDrag(data); else base.OnDrag(data);
        }
        public override void OnPointerUp(PointerEventData data)
        {
            if (_pressed && !_classified) base.OnPointerDown(data);
            base.OnPointerUp(data);
            _pressed = false;
        }
        public void OnEndDrag(PointerEventData data)
        {
            if (_vertical && _scroll != null) _scroll.OnEndDrag(data);
            _classified = _vertical = _pressed = false;
            PlayerPrefs.Save();
        }
        protected override void OnDisable()
        {
            base.OnDisable(); _classified = _vertical = _pressed = false;
        }
    }
}
