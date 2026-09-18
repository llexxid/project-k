using UnityEngine;

namespace KingdomIdle.UGUI
{
    public sealed class CenteredPanelSize : MonoBehaviour
    {
        [SerializeField] internal float maxHeight = 1440;
        RectTransform _rect;
        void OnEnable() { _rect = (RectTransform)transform; Apply(); }
        void OnRectTransformDimensionsChange() { if (_rect != null && isActiveAndEnabled) Apply(); }
        void Apply()
        {
            if (!(_rect.parent is RectTransform parent)) return;
            var size = new Vector2(Mathf.Min(980, parent.rect.width - 64), Mathf.Min(maxHeight, parent.rect.height - 120));
            if ((_rect.sizeDelta - size).sqrMagnitude > .25f) _rect.sizeDelta = size;
        }
    }
}
