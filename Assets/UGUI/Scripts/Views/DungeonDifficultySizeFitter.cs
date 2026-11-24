using UnityEngine;

namespace KingdomIdle.UGUI
{
    /// <summary>던전 상세만 중앙 본문의 사방에 같은 여백을 두고 나머지 공간을 난이도 스크롤에 배분한다.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(RectTransform))]
    public sealed class DungeonDifficultySizeFitter : MonoBehaviour
    {
        private const float EdgeInset = 18;
        private readonly Vector3[] _corners = new Vector3[4];
        private RectTransform _rect, _parent, _clip;
        private Vector2 _lastPosition, _lastSize;
        private Rect _lastParentRect, _lastClipRect;
        private Matrix4x4 _lastParentMatrix, _lastClipMatrix;
        private bool _initialized, _hasLayout, _applying;

        private void OnEnable() { _hasLayout = false; RefreshNow(); }
        private void OnRectTransformDimensionsChange() { if (isActiveAndEnabled) RefreshNow(); }
        private void LateUpdate() => RefreshNow();

        internal void RefreshNow()
        {
            if (_applying) return;
            if (!_initialized)
            {
                _rect = (RectTransform)transform;
                _parent = _rect.parent as RectTransform;
                if (_parent == null) return;
                var panel = GetComponentInParent<DungeonPanelView>(true);
                _clip = panel != null && panel.Sheet != null ? panel.Sheet.parent as RectTransform : _parent;
                if (_clip == null) return;
                _initialized = true;
            }
            if (_clip.rect.width <= EdgeInset * 2 || _clip.rect.height <= EdgeInset * 2) return;
            var parentMatrix = _parent.worldToLocalMatrix;
            var clipMatrix = _clip.localToWorldMatrix;
            if (_hasLayout && _lastParentRect == _parent.rect && _lastClipRect == _clip.rect &&
                _lastParentMatrix == parentMatrix && _lastClipMatrix == clipMatrix &&
                _lastSize == _rect.rect.size &&
                _lastPosition == _rect.anchoredPosition) return;

            _clip.GetWorldCorners(_corners);
            var low = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var high = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            foreach (var corner in _corners)
            {
                Vector2 point = _parent.InverseTransformPoint(corner);
                low = Vector2.Min(low, point);
                high = Vector2.Max(high, point);
            }
            low += Vector2.one * EdgeInset;
            high -= Vector2.one * EdgeInset;
            var size = high - low;
            if (size.x <= 0 || size.y <= 0) return;
            var anchor = _parent.rect.min + Vector2.Scale(_parent.rect.size,
                _rect.anchorMin + Vector2.Scale(_rect.anchorMax - _rect.anchorMin, _rect.pivot));
            var position = low + Vector2.Scale(size, _rect.pivot) - anchor;
            _applying = true;
            try
            {
                if (Mathf.Abs(_rect.rect.width - size.x) > .5f)
                    _rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
                if (Mathf.Abs(_rect.rect.height - size.y) > .5f)
                    _rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);
                if (_rect.anchoredPosition != position) _rect.anchoredPosition = position;
                _lastParentRect = _parent.rect; _lastClipRect = _clip.rect;
                _lastParentMatrix = parentMatrix; _lastClipMatrix = clipMatrix;
                _lastSize = _rect.rect.size; _lastPosition = _rect.anchoredPosition;
                _hasLayout = true;
            }
            finally { _applying = false; }
        }
    }
}
