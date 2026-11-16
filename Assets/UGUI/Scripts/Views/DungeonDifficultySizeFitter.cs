using UnityEngine;
using UnityEngine.UI;

namespace KingdomIdle.UGUI
{
    /// <summary>던전 상세만 중앙 본문 안에 맞추고 고정 제목·푸터 사이의 기존 난이도 스크롤을 줄인다.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(RectTransform))]
    public sealed class DungeonDifficultySizeFitter : MonoBehaviour
    {
        private const float EdgeInset = 18;
        private readonly Vector3[] _corners = new Vector3[4];
        private RectTransform _rect, _parent, _clip;
        private Vector2 _restingPosition, _lastPosition;
        private Rect _lastParentRect, _lastClipRect;
        private Matrix4x4 _lastParentMatrix, _lastClipMatrix;
        private float _lastPreferred, _lastHeight;
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
                _restingPosition = _rect.anchoredPosition;
                _initialized = true;
            }
            float preferred = LayoutUtility.GetPreferredHeight(_rect);
            if (preferred <= 0 || _clip.rect.height <= 0) return;
            var parentMatrix = _parent.worldToLocalMatrix;
            var clipMatrix = _clip.localToWorldMatrix;
            if (_hasLayout && _lastParentRect == _parent.rect && _lastClipRect == _clip.rect &&
                _lastParentMatrix == parentMatrix && _lastClipMatrix == clipMatrix &&
                Mathf.Approximately(_lastPreferred, preferred) && Mathf.Approximately(_lastHeight, _rect.rect.height) &&
                _lastPosition == _rect.anchoredPosition) return;

            _clip.GetWorldCorners(_corners);
            float bottom = float.PositiveInfinity, top = float.NegativeInfinity;
            foreach (var corner in _corners)
            {
                float y = _parent.InverseTransformPoint(corner).y;
                bottom = Mathf.Min(bottom, y); top = Mathf.Max(top, y);
            }
            bottom += EdgeInset; top -= EdgeInset;
            float height = Mathf.Min(preferred, Mathf.Max(0, top - bottom));
            if (height <= 0) return;
            float anchor = Mathf.Lerp(_parent.rect.yMin, _parent.rect.yMax,
                Mathf.Lerp(_rect.anchorMin.y, _rect.anchorMax.y, _rect.pivot.y));
            float pivot = Mathf.Clamp(anchor + _restingPosition.y, bottom + height * _rect.pivot.y,
                top - height * (1 - _rect.pivot.y));
            var position = new Vector2(_restingPosition.x, pivot - anchor);
            _applying = true;
            try
            {
                if (Mathf.Abs(_rect.rect.height - height) > .5f)
                    _rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
                if (_rect.anchoredPosition != position) _rect.anchoredPosition = position;
                _lastParentRect = _parent.rect; _lastClipRect = _clip.rect;
                _lastParentMatrix = parentMatrix; _lastClipMatrix = clipMatrix;
                _lastPreferred = preferred; _lastHeight = _rect.rect.height; _lastPosition = _rect.anchoredPosition;
                _hasLayout = true;
            }
            finally { _applying = false; }
        }
    }
}
