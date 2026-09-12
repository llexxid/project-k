using UnityEngine;
using UnityEngine.UI;

namespace KingdomIdle.UGUI
{
    /// <summary>Small continuous joint deformations of one static illustration; no frame textures or allocations.</summary>
    [DisallowMultipleComponent]
    public sealed class LobbyActorRig : BaseMeshEffect
    {
        public enum Actor { Knight, Archer, Mage }
        public Actor actor;
        const int Columns = 16, Rows = 18;
        float _step, _reach, _cloth;
        bool _posed;

        public void Sample(float time)
        {
            float phase = (int)actor * 1.8f;
            _step = Mathf.Sin(time * 2.5f + phase);
            // A deliberate prepare/relax action, separated by a short rest.
            float cycle = Mathf.Repeat(time + phase, 5.4f);
            _reach = cycle < 2.4f ? Mathf.Sin(cycle / 2.4f * Mathf.PI) : 0f;
            _cloth = Mathf.Sin(time * 3.2f + phase);
            _posed = true;
            if (graphic != null) graphic.SetVerticesDirty();
        }

        public void ResetPose()
        {
            _posed = false;
            if (graphic != null) graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper mesh)
        {
            if (!IsActive() || !_posed || mesh.currentVertCount != 4) return;
            UIVertex bl = default, tl = default, tr = default, br = default;
            mesh.PopulateUIVertex(ref bl, 0); mesh.PopulateUIVertex(ref tl, 1);
            mesh.PopulateUIVertex(ref tr, 2); mesh.PopulateUIVertex(ref br, 3);
            mesh.Clear();
            for (int y = 0; y <= Rows; y++)
                for (int x = 0; x <= Columns; x++)
                {
                    float u = (float)x / Columns, v = (float)y / Rows;
                    var vertex = bl;
                    vertex.position = Vector3.Lerp(Vector3.Lerp(bl.position, br.position, u), Vector3.Lerp(tl.position, tr.position, u), v);
                    vertex.uv0 = Vector4.Lerp(Vector4.Lerp(bl.uv0, br.uv0, u), Vector4.Lerp(tl.uv0, tr.uv0, u), v);
                    // Alternating boots flex below the hips; torso remains anchored.
                    float foot = 1f - Smooth(.10f, .34f, v);
                    float side = 1f - 2f * Smooth(.38f, .60f, u);
                    vertex.position += new Vector3(_step * foot * side * 3.5f, _step * foot * side * 5.5f, 0);
                    // Cape tail / ponytail flutter, with a gradual weight at the attachment.
                    float cloth = (1f - Smooth(.14f, .5f, u)) * Smooth(.26f, .45f, v) * (1f - Smooth(.64f, .8f, v));
                    vertex.position.y += _cloth * cloth * 3.5f;
                    if (actor != Actor.Knight)
                    {
                        // Forward arm and held staff/bow move as a small reach, not a whole-body wobble.
                        float arm = Smooth(.49f, .72f, u) * Smooth(.08f, .28f, v);
                        vertex.position += new Vector3(_reach * arm * 3f, _reach * arm * 4f, 0);
                    }
                    mesh.AddVert(vertex);
                }
            for (int y = 0; y < Rows; y++)
                for (int x = 0; x < Columns; x++)
                {
                    int i = y * (Columns + 1) + x;
                    mesh.AddTriangle(i, i + Columns + 1, i + 1);
                    mesh.AddTriangle(i + 1, i + Columns + 1, i + Columns + 2);
                }
        }

        static float Smooth(float min, float max, float value)
        {
            float t = Mathf.Clamp01((value - min) / (max - min));
            return t * t * (3 - 2 * t);
        }
    }
}
