using UnityEngine;
using UnityEngine.UI;

namespace KingdomIdle.UGUI
{
    [RequireComponent(typeof(GridLayoutGroup))]
    public sealed class PromotionTreeGrid : MonoBehaviour
    {
        GridLayoutGroup _grid;
        float _width=-1;
        void OnEnable() { _grid=GetComponent<GridLayoutGroup>(); _width=-1; Apply(); }
        void OnRectTransformDimensionsChange() { if(_grid!=null && isActiveAndEnabled)Apply(); }
        void Apply()
        {
            float width=((RectTransform)transform).rect.width-_grid.padding.horizontal;
            if(width<1 || Mathf.Abs(width-_width)<.5f)return;
            _width=width;_grid.constraint=GridLayoutGroup.Constraint.FixedColumnCount;_grid.constraintCount=3;
            _grid.cellSize=new Vector2((width-_grid.spacing.x*2)/3,340);
        }
    }
}
