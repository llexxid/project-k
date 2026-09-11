using UnityEngine;
namespace KingdomIdle.UGUI
{
    /// <summary>Reserves the top HUD and bottom navigation on short portrait screens.</summary>
    public sealed class SheetSizeFitter : MonoBehaviour
    {
        public float preferredHeight=1152;
        private RectTransform _rect;
        private void OnEnable(){_rect=(RectTransform)transform;Apply();}
        private void OnRectTransformDimensionsChange(){if(_rect!=null&&isActiveAndEnabled)Apply();}
        private void Apply()
        {
            var parent=_rect.parent as RectTransform;if(parent==null)return;
            // LayerPanels already excludes the bottom navigation height.
            float height=Mathf.Clamp(parent.rect.height-UguiTheme.StageControlsBottom-24,380,preferredHeight);
            if(Mathf.Abs(_rect.sizeDelta.y-height)>.5f)_rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,height);
        }
    }
}
