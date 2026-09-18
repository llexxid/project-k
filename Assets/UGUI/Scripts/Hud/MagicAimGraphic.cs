using UnityEngine;
using UnityEngine.UI;

namespace KingdomIdle.UGUI
{
    /// <summary>A ground circle; bolt/cloud height never affects the footprint.</summary>
    public sealed class MagicAimGraphic : MaskableGraphic
    {
        public bool healing, valid;
        float _angle;
        bool _lastValid, _lastHealing;
        protected override void Awake() { base.Awake(); raycastTarget = false; }
        void Update()
        {
            _angle += Time.unscaledDeltaTime * 26; rectTransform.localRotation = Quaternion.Euler(0, 0, _angle);
            if (_lastValid != valid || _lastHealing != healing) { _lastValid = valid; _lastHealing = healing; SetVerticesDirty(); }
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Color tint = !valid ? new Color(1,.35f,.24f) : healing ? new Color(.45f,1,.65f) : new Color(.42f,.76f,1);
            float radius = rectTransform.rect.width * .5f;
            const int segments = 64;
            tint.a = .14f;
            vh.AddVert(Vector3.zero, tint, Vector2.zero);
            for (int i = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2 / segments;
                vh.AddVert(new Vector3(Mathf.Cos(a), Mathf.Sin(a)) * radius, tint, Vector2.zero);
                if (i > 0) vh.AddTriangle(0, i, i + 1);
            }
            tint.a = .85f;
            Ring(vh, radius, radius * .965f, tint, segments);
            Ring(vh, radius * .79f, radius * .77f, tint, segments);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4; Vector3 dir = new Vector3(Mathf.Cos(a),Mathf.Sin(a));
                Vector3 side = new Vector3(-dir.y,dir.x) * radius * .035f;
                int n = vh.currentVertCount;
                vh.AddVert(dir * radius * .91f, tint, Vector2.zero);
                vh.AddVert(dir * radius * .84f + side, tint, Vector2.zero);
                vh.AddVert(dir * radius * .84f - side, tint, Vector2.zero);
                vh.AddTriangle(n,n+1,n+2);
            }
        }
        static void Ring(VertexHelper vh, float outer, float inner, Color tint, int segments)
        {
            int start = vh.currentVertCount;
            for (int i = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2 / segments; var direction = new Vector3(Mathf.Cos(a),Mathf.Sin(a));
                vh.AddVert(direction * outer,tint,Vector2.zero); vh.AddVert(direction * inner,tint,Vector2.zero);
                if(i==0)continue; int n=start+(i-1)*2;
                vh.AddTriangle(n,n+2,n+1);vh.AddTriangle(n+1,n+2,n+3);
            }
        }
    }
}
