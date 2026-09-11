using UnityEngine;
using UnityEngine.UI;

namespace KingdomIdle.UGUI
{
    /// <summary>Fits complete columns inside the viewport; runs only when width changes.</summary>
    [RequireComponent(typeof(GridLayoutGroup))]
    public sealed class ResponsiveGrid : MonoBehaviour
    {
        [SerializeField] private float minimumCellWidth = 225f;
        [SerializeField] private float cellHeight = 284f;
        [SerializeField] private int maximumColumns = 5;
        private GridLayoutGroup _grid;
        private RectTransform _rect;
        private float _width = -1f;
        private void OnEnable() { _grid = GetComponent<GridLayoutGroup>(); _rect = (RectTransform)transform; _width = -1f; Apply(); }
        private void OnRectTransformDimensionsChange() { if (_grid != null && isActiveAndEnabled) Apply(); }
        private void Apply()
        {
            float width = _rect.rect.width - _grid.padding.horizontal;
            if (width < 1f || Mathf.Abs(width - _width) < 0.5f) return;
            _width = width;
            int columns = Mathf.Clamp(Mathf.FloorToInt((width + _grid.spacing.x) / (minimumCellWidth + _grid.spacing.x)), 1, maximumColumns);
            _grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            _grid.constraintCount = columns;
            _grid.cellSize = new Vector2(Mathf.Max(1f, (width - _grid.spacing.x * (columns - 1)) / columns), cellHeight);
        }
    }
}
