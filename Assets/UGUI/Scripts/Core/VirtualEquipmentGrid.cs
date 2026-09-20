using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomIdle.UGUI
{
    /// <summary>Preserves the authored grid and scroll height, instantiating only visible cards.</summary>
    [RequireComponent(typeof(RectTransform), typeof(GridLayoutGroup))]
    public sealed class VirtualEquipmentGrid : MonoBehaviour
    {
        readonly List<Action<EquipCellView>> _rows = new();
        readonly List<EquipCellView> _pool = new();
        readonly List<int> _indices = new();
        readonly Vector3[] _corners = new Vector3[4];
        RectTransform _rect, _viewport;
        GridLayoutGroup _grid;
        LayoutElement _layout;
        GameObject _prefab;
        int _columns, _rowCount = -1;
        Vector2 _cell;
        bool _dirty;

        public static void Add(RectTransform grid, GameObject prefab, Action<EquipCellView> bind)
        {
            var view = grid.GetComponent<VirtualEquipmentGrid>() ?? grid.gameObject.AddComponent<VirtualEquipmentGrid>();
            view._prefab = prefab;
            view._rows.Add(bind);
            view._dirty = true;
        }

        void Awake()
        {
            _rect = (RectTransform)transform;
            _grid = GetComponent<GridLayoutGroup>();
            _grid.enabled = false;
            var fitter = GetComponent<ContentSizeFitter>();
            if (fitter != null) fitter.enabled = false;
            _layout = GetComponent<LayoutElement>() ?? gameObject.AddComponent<LayoutElement>();
            var scroll = GetComponentInParent<ScrollRect>();
            _viewport = scroll != null ? scroll.viewport : null;
            _rect.pivot = new Vector2(.5f, 1);
        }

        void LateUpdate()
        {
            if (_prefab == null || _rect.rect.width < 1) return;
            int columns = _grid.constraint == GridLayoutGroup.Constraint.FixedColumnCount ? Mathf.Max(1, _grid.constraintCount)
                : Mathf.Max(1, Mathf.FloorToInt((_rect.rect.width - _grid.padding.horizontal + _grid.spacing.x) / (_grid.cellSize.x + _grid.spacing.x)));
            int rows = Mathf.CeilToInt((float)_rows.Count / columns);
            if (columns != _columns || rows != _rowCount || _cell != _grid.cellSize || _dirty)
            {
                _columns = columns; _rowCount = rows; _cell = _grid.cellSize;
                float height = _grid.padding.vertical + rows * _cell.y + Mathf.Max(0, rows - 1) * _grid.spacing.y;
                _layout.minHeight = _layout.preferredHeight = height;
                _layout.flexibleHeight = 0;
                _rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
                for (int i = 0; i < _indices.Count; i++) _indices[i] = -1;
                _dirty = false;
            }
            float top = 0, bottom = Mathf.Min(_rect.rect.height, 1200);
            if (_viewport != null)
            {
                _viewport.GetWorldCorners(_corners);
                top = -_rect.InverseTransformPoint(_corners[1]).y;
                bottom = -_rect.InverseTransformPoint(_corners[0]).y;
            }
            float stride = _cell.y + _grid.spacing.y;
            int firstRow = Mathf.Clamp(Mathf.FloorToInt((top - _grid.padding.top) / stride) - 1, 0, Mathf.Max(0, rows - 1));
            int lastRow = Mathf.Clamp(Mathf.CeilToInt((bottom - _grid.padding.top) / stride) + 1, firstRow, rows);
            int first = firstRow * columns, count = Mathf.Min(_rows.Count - first, (lastRow - firstRow) * columns);
            for (int i = 0; i < count; i++)
            {
                if (i == _pool.Count)
                {
                    _pool.Add(Instantiate(_prefab, transform, false).GetComponent<EquipCellView>());
                    _indices.Add(-1);
                }
                var card = _pool[i]; int index = first + i;
                if (!card.gameObject.activeSelf) card.gameObject.SetActive(true);
                var rect = (RectTransform)card.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
                rect.sizeDelta = _cell;
                rect.anchoredPosition = new Vector2(_grid.padding.left + (index % columns) * (_cell.x + _grid.spacing.x),
                    -_grid.padding.top - (index / columns) * stride);
                if (_indices[i] != index) { _rows[index](card); _indices[i] = index; }
            }
            for (int i = count; i < _pool.Count; i++) if (_pool[i].gameObject.activeSelf) _pool[i].gameObject.SetActive(false);
        }
    }
}
