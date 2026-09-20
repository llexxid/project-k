using UnityEngine;

namespace KingdomIdle.UGUI
{
    /// <summary>Extends only the HUD background into the display cutout; controls stay in the safe area.</summary>
    public sealed class SafeAreaTopBleed : MonoBehaviour
    {
        [SerializeField] internal RectTransform background;
        Canvas _canvas;
        float _height=-1;
        void OnEnable() { _canvas=GetComponentInParent<Canvas>(); _height=-1; Apply(); }
        void LateUpdate() => Apply();
        void Apply()
        {
            if(background==null || _canvas==null) return;
            float height=Mathf.Max(0,Screen.height-Screen.safeArea.yMax)/Mathf.Max(.01f,_canvas.scaleFactor)+2;
            if(Mathf.Abs(height-_height)<.1f)return;
            _height=height;background.sizeDelta=new Vector2(0,height);
        }
    }
}
