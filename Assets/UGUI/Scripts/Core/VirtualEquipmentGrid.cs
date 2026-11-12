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
        RectTransform _snapshotViewport;
        ScrollRect _scroll;
        GridLayoutGroup _grid;
        LayoutElement _layout;
        GameObject _prefab;
        int _columns, _rowCount = -1;
        Vector2 _cell, _spacing;
        int _paddingLeft, _paddingRight, _paddingTop, _paddingBottom;
        Rect _lastRect, _lastViewportRect;
        Matrix4x4 _lastGridMatrix, _lastViewportMatrix;
        int _firstVisible = -1, _visibleCount = -1;
        bool _dirty, _hasGeometry;

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
            _scroll = GetComponentInParent<ScrollRect>();
            _viewport = _scroll != null ? _scroll.viewport : null;
            _rect.pivot = new Vector2(.5f, 1);
        }

        void OnEnable() => _hasGeometry = false;
        void LateUpdate() => Refresh();

        internal void Refresh()
        {
            if (_prefab == null || _rect.rect.width < 1) { _hasGeometry = false; return; }
            if (_scroll != null) _viewport = _scroll.viewport;
            int columns = _grid.constraint == GridLayoutGroup.Constraint.FixedColumnCount ? Mathf.Max(1, _grid.constraintCount)
                : Mathf.Max(1, Mathf.FloorToInt((_rect.rect.width - _grid.padding.horizontal + _grid.spacing.x) / (_grid.cellSize.x + _grid.spacing.x)));
            int rows = Mathf.CeilToInt((float)_rows.Count / columns);
            bool layoutChanged = columns != _columns || rows != _rowCount || _cell != _grid.cellSize || _spacing != _grid.spacing ||
                _paddingLeft != _grid.padding.left || _paddingRight != _grid.padding.right ||
                _paddingTop != _grid.padding.top || _paddingBottom != _grid.padding.bottom;
            bool rebind = layoutChanged || _dirty;
            if (rebind)
            {
                _columns = columns; _rowCount = rows; _cell = _grid.cellSize;
                _spacing = _grid.spacing;
                _paddingLeft = _grid.padding.left; _paddingRight = _grid.padding.right;
                _paddingTop = _grid.padding.top; _paddingBottom = _grid.padding.bottom;
                float height = _grid.padding.vertical + rows * _cell.y + Mathf.Max(0, rows - 1) * _grid.spacing.y;
                _layout.minHeight = _layout.preferredHeight = height;
                _layout.flexibleHeight = 0;
                _rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
                for (int i = 0; i < _indices.Count; i++) _indices[i] = -1;
                _dirty = false;
            }
            var area = _rect.rect;
            var gridMatrix = _rect.localToWorldMatrix;
            var viewportArea = _viewport != null ? _viewport.rect : default;
            var viewportMatrix = _viewport != null ? _viewport.localToWorldMatrix : default;
            // 스크롤·레이아웃·목록이 그대로인 프레임에는 코너 변환과 모든 카드 RectTransform 쓰기를 생략한다.
            if (!rebind && _hasGeometry && _snapshotViewport == _viewport && _lastRect == area && _lastGridMatrix == gridMatrix &&
                _lastViewportRect == viewportArea && _lastViewportMatrix == viewportMatrix) return;
            bool forcePositions = rebind || !_hasGeometry;
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
            _snapshotViewport = _viewport;
            _lastRect = area; _lastGridMatrix = gridMatrix;
            _lastViewportRect = viewportArea; _lastViewportMatrix = viewportMatrix;
            _hasGeometry = true;
            if (!forcePositions && _firstVisible == first && _visibleCount == count) return;
            _firstVisible = first; _visibleCount = count;
            for (int i = 0; i < count; i++)
            {
                if (i == _pool.Count)
                {
                    _pool.Add(Instantiate(_prefab, transform, false).GetComponent<EquipCellView>());
                    _indices.Add(-1);
                }
                var card = _pool[i]; int index = first + i;
                if (!card.gameObject.activeSelf) card.gameObject.SetActive(true);
                if (forcePositions || _indices[i] != index)
                {
                    var rect = (RectTransform)card.transform;
                    rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
                    rect.sizeDelta = _cell;
                    rect.anchoredPosition = new Vector2(_grid.padding.left + (index % columns) * (_cell.x + _grid.spacing.x),
                        -_grid.padding.top - (index / columns) * stride);
                }
                if (_indices[i] != index) { _rows[index](card); _indices[i] = index; }
            }
            for (int i = count; i < _pool.Count; i++) if (_pool[i].gameObject.activeSelf) _pool[i].gameObject.SetActive(false);
        }
    }
}
