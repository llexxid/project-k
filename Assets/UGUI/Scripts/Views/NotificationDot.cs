using UnityEngine;
using UnityEngine.UI;

namespace KingdomIdle.UGUI
{
    internal static class NotificationDot
    {
        internal static GameObject Create(Button button, Sprite circle)
        {
            if (button == null || circle == null) return null;

            var dot = new GameObject("NotificationDot", typeof(RectTransform), typeof(Image));
            dot.layer = button.gameObject.layer;
            dot.SetActive(false);
            var rect = (RectTransform)dot.transform;
            rect.SetParent(button.transform, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
            rect.anchoredPosition = new Vector2(-6f, -6f);
            rect.sizeDelta = new Vector2(22f, 22f);
            var image = dot.GetComponent<Image>();
            image.sprite = circle;
            image.color = UguiTheme.DangerRedBright;
            image.raycastTarget = false;
            return dot;
        }

        internal static void SetVisible(GameObject dot, bool visible)
        {
            if (dot != null && dot.activeSelf != visible) dot.SetActive(visible);
        }
    }
}
