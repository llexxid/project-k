using UnityEngine;
using UnityEngine.UI;

namespace KingdomIdle.UGUI
{
    /// <summary>Quarter-view ground glyph built on the same 32 px/world-unit grid as combat art.</summary>
    public sealed class MagicAimGraphic : MaskableGraphic
    {
        public bool healing, valid;
        public float worldRadius = 1;
        bool _lastValid, _lastHealing;
        protected override void Awake() { base.Awake(); raycastTarget = false; }
        void Update()
        {
            if (_lastValid != valid || _lastHealing != healing)
            { _lastValid = valid; _lastHealing = healing; SetVerticesDirty(); }
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Color tint = !valid ? new Color(1,.35f,.24f) : healing ? new Color(.45f,1,.65f) : new Color(.42f,.76f,1);
            int radius = Mathf.Clamp(Mathf.RoundToInt(worldRadius * 32), 12, 96);
            float pixel = rectTransform.rect.width / (radius * 2);
            // Scan-line runs preserve square pixels without a dense per-pixel mesh or textures.
            int height = Mathf.RoundToInt(radius * .64f);
            for (int y = -height; y < height; y++)
            {
                int outer = Extent(radius, height, y);
                int inner = Extent(radius - 1, height - 1, y);
                Color fill = tint; fill.a = .10f;
                Quad(vh, -outer, outer, y, pixel, fill);
                Color rim = tint; rim.a = .90f;
                Quad(vh, -outer, -inner, y, pixel, rim);
                Quad(vh, inner, outer, y, pixel, rim);
                int a = Extent(radius * .77f, height * .77f, y);
                int b = Extent(radius * .77f - 1, height * .77f - 1, y);
                rim.a = .55f;
                Quad(vh, -a, -b, y, pixel, rim); Quad(vh, b, a, y, pixel, rim);
            }
            tint.a = .9f;
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI / 4;
                int x = Mathf.RoundToInt(Mathf.Cos(angle) * radius * .88f);
                int y = Mathf.RoundToInt(Mathf.Sin(angle) * height * .88f);
                for (int dy = -1; dy <= 1; dy++)
                    Quad(vh, x - (dy == 0 ? 2 : 1), x + (dy == 0 ? 2 : 1), y + dy, pixel, tint);
            }
            Quad(vh, -2, 2, 0, pixel, tint);
        }
        static int Extent(float rx, float ry, float y)
        {
            float normal = (y + .5f) / Mathf.Max(.5f, ry);
            return Mathf.Abs(normal) >= 1 ? 0 : Mathf.RoundToInt(rx * Mathf.Sqrt(1 - normal * normal));
        }
        static void Quad(VertexHelper vh, int x0, int x1, int y, float pixel, Color color)
        {
            if (x1 <= x0) return;
            int n = vh.currentVertCount;
            vh.AddVert(new Vector3(x0 * pixel, y * pixel), color, Vector2.zero);
            vh.AddVert(new Vector3(x0 * pixel, (y+1) * pixel), color, Vector2.zero);
            vh.AddVert(new Vector3(x1 * pixel, (y+1) * pixel), color, Vector2.zero);
            vh.AddVert(new Vector3(x1 * pixel, y * pixel), color, Vector2.zero);
            vh.AddTriangle(n,n+1,n+2); vh.AddTriangle(n,n+2,n+3);
        }
    }
}
