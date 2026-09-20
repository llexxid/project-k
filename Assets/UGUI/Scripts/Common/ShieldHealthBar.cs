using UnityEngine;
using UnityEngine.UI;

namespace KingdomIdle.UGUI
{
    /// <summary>Health followed by a white shield segment on the same effective-health scale.</summary>
    [DisallowMultipleComponent]
    public sealed class ShieldHealthBar : MonoBehaviour
    {
        private Image _health, _shield;
        private RectTransform _overlay;

        public static void Set(Image fill, float healthRatio, long shield, long maximumHealth)
        {
            if (fill == null) return;
            var bar = fill.GetComponent<ShieldHealthBar>() ?? fill.gameObject.AddComponent<ShieldHealthBar>();
            bar.Refresh(fill, healthRatio, maximumHealth > 0 ? (double)shield / maximumHealth : 0);
        }

        private void Refresh(Image fill, float healthRatio, double shieldRatio)
        {
            if (_health == null)
            {
                _health = fill;
                var go = new GameObject("ShieldOverlay", typeof(RectTransform));
                go.layer = gameObject.layer;
                _overlay = (RectTransform)go.transform;
                _overlay.SetParent(fill.transform.parent, false);
                var source = fill.rectTransform;
                _overlay.anchorMin = source.anchorMin; _overlay.anchorMax = source.anchorMax;
                _overlay.pivot = source.pivot;
                _overlay.offsetMin = source.offsetMin; _overlay.offsetMax = source.offsetMax;
                _overlay.SetSiblingIndex(fill.transform.GetSiblingIndex() + 1);
                var segment = new GameObject("Shield", typeof(RectTransform), typeof(Image));
                segment.layer = gameObject.layer; segment.transform.SetParent(_overlay, false);
                _shield = segment.GetComponent<Image>();
                _shield.color = new Color(.96f, .98f, 1f, 1f); _shield.raycastTarget = false;
                _shield.rectTransform.offsetMin = _shield.rectTransform.offsetMax = Vector2.zero;
            }
            healthRatio = Mathf.Clamp01(healthRatio);
            shieldRatio = System.Math.Max(0, shieldRatio);
            double scale = System.Math.Max(1, healthRatio + shieldRatio);
            float healthEnd = (float)(healthRatio / scale);
            _health.fillAmount = healthEnd;
            bool visible = shieldRatio > 0 && healthRatio > 0;
            if (_shield.enabled != visible) _shield.enabled = visible;
            if (visible)
            {
                _shield.rectTransform.anchorMin = new Vector2(healthEnd, 0);
                _shield.rectTransform.anchorMax = new Vector2((float)((healthRatio + shieldRatio) / scale), 1);
            }
        }

        private void OnDestroy() { if (_overlay != null) Destroy(_overlay.gameObject); }
    }
}
