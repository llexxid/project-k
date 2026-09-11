using UnityEngine;

namespace KingdomIdle.UGUI
{
    public sealed class ModalSizeFitter : MonoBehaviour
    {
        [SerializeField] private float maxWidth = 980f;
        private RectTransform _rect;
        private float _parentWidth;
        private void OnEnable() { _rect = (RectTransform)transform; _parentWidth = -1f; Apply(); }
        private void OnRectTransformDimensionsChange() { if (_rect != null && isActiveAndEnabled) Apply(); }
        private void Apply()
        {
            if (!(_rect.parent is RectTransform parent)) return;
            float width = parent.rect.width;
            if (Mathf.Abs(width - _parentWidth) < 0.5f || width < 1f) return;
            _parentWidth = width;
            _rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Min(maxWidth, width - 64f));
        }
    }
}
