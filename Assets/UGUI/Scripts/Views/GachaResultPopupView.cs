using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KingdomIdle.UGUI
{
    /// <summary>뽑기 결과 팝업 셸. GachaResultPopupController가 카드 리스트를 채운다.</summary>
    public sealed class GachaResultPopupView : MonoBehaviour
    {
        [SerializeField] internal RectTransform box;   // 팝업 박스 (PopIn 애니메이션 대상)
        [SerializeField] internal TMP_Text title;
        [SerializeField] internal ScrollRect scroll;
        [SerializeField] internal RectTransform grid;
        [SerializeField] internal RectTransform buttonRow;
        [SerializeField] internal Button btnDone;
        [SerializeField] internal Button btnRePull1;
        [SerializeField] internal Button btnRePullN;
        [SerializeField] internal TMP_Text btnRePull1Label;
        [SerializeField] internal TMP_Text btnRePullNLabel;
        private int _resultCount;
        private Vector2 _lastRootSize = new Vector2(-1, -1), _lastViewportSize = new Vector2(-1, -1);
        private GridLayoutGroup _gridLayout;
        private LayoutElement _gridSize, _contentSize;
        private VerticalLayoutGroup _contentLayout;
        internal int ResultCount => _resultCount;

        internal void SetResultCount(int count)
        {
            _resultCount = Mathf.Max(1, count);
            _lastRootSize = _lastViewportSize = new Vector2(-1, -1);
        }

        internal void RefreshLayoutNow() => RefreshLayout(true);
        private void LateUpdate() => RefreshLayout(false);

        private void RefreshLayout(bool force)
        {
            if (_resultCount == 0 || box == null || scroll == null || scroll.viewport == null || grid == null ||
                !(box.parent is RectTransform root)) return;
            var rootSize = root.rect.size;
            bool rootChanged = _lastRootSize != rootSize;
            if (rootChanged)
            {
                float preferred = _resultCount == 1 ? 1120 : _resultCount == 2 ? 1000 : _resultCount == 3 ? 940 : 1560;
                box.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Min(preferred, Mathf.Max(0, rootSize.y - 96)));
                LayoutRebuilder.ForceRebuildLayoutImmediate(box);
            }
            var viewport = scroll.viewport.rect.size;
            if (!force && !rootChanged && _lastViewportSize == viewport) return;
            if (viewport.x < 1 || viewport.y < 1) return;
            if (_gridLayout == null) _gridLayout = grid.GetComponent<GridLayoutGroup>();
            if (_gridSize == null) _gridSize = grid.GetComponent<LayoutElement>() ?? grid.gameObject.AddComponent<LayoutElement>();
            if (_contentSize == null) _contentSize = scroll.content.GetComponent<LayoutElement>() ?? scroll.content.gameObject.AddComponent<LayoutElement>();
            if (_contentLayout == null) _contentLayout = scroll.content.GetComponent<VerticalLayoutGroup>();
            int columns = _resultCount <= 3 ? _resultCount : Mathf.Clamp(Mathf.FloorToInt((viewport.x + 16) / 292), 1, 3);
            float width = (viewport.x - 16 * (columns - 1)) / columns;
            if (_resultCount == 1) width = Mathf.Min(620, width);
            else if (_resultCount == 2) width = Mathf.Min(430, width);
            float height = _resultCount == 1 ? 700 : _resultCount == 2 ? 540 : _resultCount == 3 ? 460 : 440;
            _gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            _gridLayout.constraintCount = columns;
            _gridLayout.cellSize = new Vector2(width, height);
            _gridLayout.spacing = new Vector2(16, 16);
            _gridLayout.childAlignment = _resultCount <= 3 ? TextAnchor.MiddleCenter : TextAnchor.UpperCenter;
            float rows = Mathf.Ceil((float)_resultCount / columns);
            float contentHeight = rows * height + Mathf.Max(0, rows - 1) * 16;
            _gridSize.preferredHeight = contentHeight;
            _gridSize.flexibleHeight = 0;
            _contentSize.minHeight = _resultCount <= 3 ? viewport.y : 0;
            _contentLayout.childAlignment = _resultCount <= 3 ? TextAnchor.MiddleCenter : TextAnchor.UpperCenter;
            _contentLayout.childForceExpandHeight = false;
            scroll.vertical = contentHeight > viewport.y + 1;
            _lastRootSize = rootSize; _lastViewportSize = viewport;
        }
    }
}
