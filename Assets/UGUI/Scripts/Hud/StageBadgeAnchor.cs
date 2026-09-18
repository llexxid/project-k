using UnityEngine;

namespace KingdomIdle.UGUI
{
    public sealed class StageBadgeAnchor : MonoBehaviour
    {
        RectTransform _rect;
        readonly Vector3[] _corners = new Vector3[4];
        void Awake() => _rect = (RectTransform)transform;
        void LateUpdate()
        {
            var party = PartyHudController.Instance?.HudRect;
            if (party == null || _rect == null) return;
            var ui = UIManager.Instance;
            var group = GetComponent<CanvasGroup>();
            bool visible = party.gameObject.activeInHierarchy && ui != null && !ui.HasActiveTabPanel && !ui.HasBlockingPanel;
            if (group != null && group.alpha != (visible ? 1 : 0)) group.alpha = visible ? 1 : 0;
            if (!visible) return;
            party.GetWorldCorners(_corners);
            float top = _rect.parent.InverseTransformPoint(_corners[1]).y;
            var position = _rect.localPosition;
            if (Mathf.Abs(position.y - top - 14) > .1f) _rect.localPosition = new Vector3(position.x, top + 14, 0);
        }
    }
}
