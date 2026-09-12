using UnityEngine;
using UnityEngine.UI;

namespace KingdomIdle.UGUI
{
    /// <summary>Two brief, local intracloud pulses. Uses the title's existing ambient clock.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class LobbyStormFlash : MaskableGraphic
    {
        public const float Period = 8.6f;
        public float Intensity { get; private set; }
        int _pattern;
        static readonly Vector2[] PathA = { new(-.34f, .10f), new(-.13f, .18f), new(-.19f, -.02f), new(.05f, .08f), new(-.02f, -.16f), new(.31f, -.08f) };
        static readonly Vector2[] PathB = { new(-.31f, -.09f), new(-.10f, .03f), new(-.02f, -.13f), new(.08f, .16f), new(.20f, .04f), new(.34f, .09f) };

        public static float IntensityAt(float time)
        {
            float phase = Mathf.Repeat(time, Period);
            return Mathf.Max(Pulse(phase, 5.62f, .11f) * .8f, Pulse(phase, 6.08f, .16f));
        }

        static float Pulse(float t, float center, float halfWidth)
        {
            float x = Mathf.Clamp01(1 - Mathf.Abs(t - center) / halfWidth);
            return x * x * (3 - 2 * x);
        }

        public void SetFlash(float intensity, int pattern)
        {
            intensity = Mathf.Clamp01(intensity);
            if (Mathf.Abs(Intensity - intensity) < .001f && _pattern == pattern) return;
            Intensity = intensity; _pattern = pattern;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (Intensity <= .001f) return;
            var rect = rectTransform.rect;
            var center = rect.center;
            var glow = new Color(.50f, .52f, 1f, Intensity * .28f);
            Vertex(mesh, center, glow);
            for (int i = 0; i < 16; i++)
            {
                float angle = i * (Mathf.PI * 2 / 16);
                Vertex(mesh, center + new Vector2(Mathf.Cos(angle) * rect.width * .48f, Mathf.Sin(angle) * rect.height * .46f), new Color(.40f, .48f, 1f, 0));
            }
            for (int i = 0; i < 16; i++) mesh.AddTriangle(0, 1 + i, 1 + (i + 1) % 16);
            var path = _pattern == 0 ? PathA : PathB;
            for (int i = 1; i < path.Length; i++)
            {
                var a = center + Vector2.Scale(path[i - 1], rect.size);
                var b = center + Vector2.Scale(path[i], rect.size);
                Segment(mesh, a, b, 5f, new Color(.43f, .54f, 1f, Intensity * .20f));
                Segment(mesh, a, b, 1.8f, new Color(.85f, .89f, 1f, Intensity * .92f));
            }
        }

        static void Segment(VertexHelper mesh, Vector2 a, Vector2 b, float width, Color tint)
        {
            var direction = (b - a).normalized;
            var normal = new Vector2(-direction.y, direction.x) * (width * .5f);
            int start = mesh.currentVertCount;
            Vertex(mesh, a - normal, tint); Vertex(mesh, a + normal, tint);
            Vertex(mesh, b + normal, tint); Vertex(mesh, b - normal, tint);
            mesh.AddTriangle(start, start + 1, start + 2); mesh.AddTriangle(start, start + 2, start + 3);
        }

        static void Vertex(VertexHelper mesh, Vector2 position, Color tint)
        {
            var vertex = UIVertex.simpleVert; vertex.position = position; vertex.color = tint;
            mesh.AddVert(vertex);
        }
    }
}
