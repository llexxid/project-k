using UnityEngine;
using UnityEngine.UI;

namespace KingdomIdle.UGUI
{
    /// <summary>One clockwise perimeter pass and a local flash, using the default masked UI material.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class GachaRevealBorder : MaskableGraphic
    {
        // Includes the repeated top-center endpoint. Arrays are allocated once per result card.
        private readonly Vector2[] _points = new Vector2[10];
        private readonly Vector2[] _joins = new Vector2[10];
        private readonly float[] _distances = new float[10];
        private float _progress, _flash;
        private bool _flashing, _lowSpec;
        private Color _rarity;
        private static readonly Color Ivory = new Color(1f, .94f, .78f, 1f);

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        internal void Configure(Color rarity, bool lowSpec)
        {
            _rarity = rarity;
            _lowSpec = lowSpec;
            _flashing = false;
            _progress = _flash = 0f;
            SetVerticesDirty();
        }

        internal void SetProgress(float progress)
        {
            progress = Mathf.Clamp01(progress);
            if (!_flashing && _progress == progress) return;
            _flashing = false;
            _progress = progress;
            SetVerticesDirty();
        }

        internal void SetFlash(float intensity)
        {
            intensity = Mathf.Clamp01(intensity);
            if (_flashing && _flash == intensity) return;
            _flashing = true;
            _flash = intensity;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var rect = rectTransform.rect;
            if (rect.width <= 8f || rect.height <= 8f || (_flashing && _flash <= .001f)) return;
            float perimeter = BuildPath(rect);
            if (_flashing)
            {
                if (!_lowSpec)
                {
                    Range(mesh, 0f, perimeter, 10f, _rarity, _flash * .08f, _flash * .08f);
                    Range(mesh, 0f, perimeter, 6f, _rarity, _flash * .24f, _flash * .24f);
                }
                Range(mesh, 0f, perimeter, 3f, _rarity, _flash, _flash);
                var hot = Color.Lerp(_rarity, Color.white, .4f);
                Range(mesh, 0f, perimeter, 1.2f, hot, _flash * .85f, _flash * .85f);
                return;
            }

            float head = _progress * perimeter;
            float tail = Mathf.Max(0f, head - Mathf.Min(90f, perimeter * .2f));
            Range(mesh, 0f, head, 1.2f, Ivory, .16f, .16f);
            if (!_lowSpec) Range(mesh, tail, head, 6f, Ivory, 0f, .16f);
            Range(mesh, tail, head, 2.2f, Ivory, .04f, 1f);
            Head(mesh, PointAt(head), Ivory);
        }

        private float BuildPath(Rect rect)
        {
            // Existing LayerLab card border is 4px thick with small rounded corners.
            // Center this path on it without resizing the card or its icon/layout.
            const float inset = 2f;
            float left = rect.xMin + inset, right = rect.xMax - inset;
            float bottom = rect.yMin + inset, top = rect.yMax - inset;
            float cut = Mathf.Min(3f, Mathf.Min(rect.width, rect.height) * .05f);
            _points[0] = new Vector2(rect.center.x, top);
            _points[1] = new Vector2(right - cut, top);
            _points[2] = new Vector2(right, top - cut);
            _points[3] = new Vector2(right, bottom + cut);
            _points[4] = new Vector2(right - cut, bottom);
            _points[5] = new Vector2(left + cut, bottom);
            _points[6] = new Vector2(left, bottom + cut);
            _points[7] = new Vector2(left, top - cut);
            _points[8] = new Vector2(left + cut, top);
            _points[9] = _points[0];
            _distances[0] = 0f;
            for (int i = 1; i < _points.Length; i++)
                _distances[i] = _distances[i - 1] + Vector2.Distance(_points[i - 1], _points[i]);
            for (int i = 0; i < 9; i++)
            {
                var incoming = (_points[i] - _points[(i + 8) % 9]).normalized;
                var outgoing = (_points[i + 1] - _points[i]).normalized;
                var n0 = new Vector2(-incoming.y, incoming.x);
                var n1 = new Vector2(-outgoing.y, outgoing.x);
                var join = (n0 + n1).normalized;
                _joins[i] = join / Mathf.Max(.5f, Vector2.Dot(join, n1));
            }
            _joins[9] = _joins[0];
            return _distances[9];
        }

        private void Range(VertexHelper mesh, float start, float end, float width, Color tint, float startAlpha, float endAlpha)
        {
            if (end <= start) return;
            for (int i = 0; i < 9; i++)
            {
                float a = Mathf.Max(start, _distances[i]);
                float b = Mathf.Min(end, _distances[i + 1]);
                if (b <= a) continue;
                var delta = _points[i + 1] - _points[i];
                float length = _distances[i + 1] - _distances[i];
                var direction = delta / length;
                var normal = new Vector2(-direction.y, direction.x);
                var p0 = _points[i] + direction * (a - _distances[i]);
                var p1 = _points[i] + direction * (b - _distances[i]);
                var n0 = a <= _distances[i] ? _joins[i] : normal;
                var n1 = b >= _distances[i + 1] ? _joins[i + 1] : normal;
                Color c0 = tint, c1 = tint;
                c0.a *= Mathf.Lerp(startAlpha, endAlpha, (a - start) / (end - start));
                c1.a *= Mathf.Lerp(startAlpha, endAlpha, (b - start) / (end - start));
                Quad(mesh, p0 - n0 * (width * .5f), p0 + n0 * (width * .5f),
                    p1 + n1 * (width * .5f), p1 - n1 * (width * .5f), c0, c1);
            }
        }

        private Vector2 PointAt(float distance)
        {
            for (int i = 0; i < 9; i++)
                if (distance <= _distances[i + 1])
                    return Vector2.Lerp(_points[i], _points[i + 1], (distance - _distances[i]) / (_distances[i + 1] - _distances[i]));
            return _points[0];
        }

        private static void Head(VertexHelper mesh, Vector2 point, Color tint)
        {
            const float radius = 1.45f;
            Quad(mesh, point + new Vector2(-radius, -radius), point + new Vector2(-radius, radius),
                point + new Vector2(radius, radius), point + new Vector2(radius, -radius), tint, tint);
        }

        private static void Quad(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color start, Color end)
        {
            int index = mesh.currentVertCount;
            mesh.AddVert(a, start, Vector2.zero);
            mesh.AddVert(b, start, Vector2.zero);
            mesh.AddVert(c, end, Vector2.zero);
            mesh.AddVert(d, end, Vector2.zero);
            mesh.AddTriangle(index, index + 1, index + 2);
            mesh.AddTriangle(index, index + 2, index + 3);
        }
    }
}
