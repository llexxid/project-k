using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomIdle.UGUI
{
    /// <summary>고정 높이 행을 소수만 생성하고 스크롤 위치에 맞춰 재사용한다.</summary>
    public sealed class VirtualizedRankingList : MonoBehaviour
    {
        internal const float RowHeight = 104f;
        internal const float RowSpacing = 10f;
        private const int BufferRows = 2;

        [SerializeField] internal ScrollRect scrollRect;
        [SerializeField] internal RectTransform viewport;
        [SerializeField] internal RectTransform content;
        [SerializeField] internal RankingRowView rowTemplate;

        private static readonly IReadOnlyList<PowerRankingEntry> EmptyEntries = Array.Empty<PowerRankingEntry>();
        private readonly List<RankingRowView> _rows = new();
        private IReadOnlyList<PowerRankingEntry> _entries = EmptyEntries;
        private int _firstVisibleIndex = -1;

        private float RowStride => RowHeight + RowSpacing;

        private void OnEnable()
        {
            if (scrollRect != null)
                scrollRect.onValueChanged.AddListener(OnScrollChanged);
        }

        private void OnDisable()
        {
            if (scrollRect != null)
                scrollRect.onValueChanged.RemoveListener(OnScrollChanged);
        }

        /// <summary>전체 데이터 높이를 설정하고 필요한 수만큼의 행 풀을 준비한다.</summary>
        public void SetEntries(IReadOnlyList<PowerRankingEntry> entries)
        {
            _entries = entries ?? EmptyEntries;
            if (content == null || viewport == null || rowTemplate == null) return;

            rowTemplate.gameObject.SetActive(false);
            float contentHeight = _entries.Count == 0
                ? 0f
                : _entries.Count * RowHeight + (_entries.Count - 1) * RowSpacing;
            content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, contentHeight);
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, 0f);

            if (scrollRect != null)
            {
                scrollRect.StopMovement();
                scrollRect.verticalNormalizedPosition = 1f;
            }

            Canvas.ForceUpdateCanvases();
            EnsureRowPool();
            _firstVisibleIndex = -1;
            RefreshVisibleRows();
        }

        private void EnsureRowPool()
        {
            int required = Mathf.CeilToInt(Mathf.Max(1f, viewport.rect.height) / RowStride) + BufferRows;
            while (_rows.Count < required)
            {
                var row = Instantiate(rowTemplate, content, false);
                row.name = $"PooledRankingRow_{_rows.Count:00}";
                ConfigureRowRect(row.RectTransform);
                row.gameObject.SetActive(true);
                _rows.Add(row);
            }
        }

        private static void ConfigureRowRect(RectTransform rowRect)
        {
            if (rowRect == null) return;
            rowRect.anchorMin = new Vector2(0f, 1f);
            rowRect.anchorMax = new Vector2(1f, 1f);
            rowRect.pivot = new Vector2(0.5f, 1f);
            rowRect.sizeDelta = new Vector2(0f, RowHeight);
        }

        private void OnScrollChanged(Vector2 _)
        {
            RefreshVisibleRows();
        }

        /// <summary>현재 첫 인덱스가 바뀐 경우에만 풀 행을 다시 바인딩한다.</summary>
        private void RefreshVisibleRows()
        {
            if (content == null || _rows.Count == 0) return;

            int maxFirstIndex = Mathf.Max(0, _entries.Count - _rows.Count);
            int firstIndex = Mathf.Clamp(
                Mathf.FloorToInt(Mathf.Max(0f, content.anchoredPosition.y) / RowStride),
                0,
                maxFirstIndex);

            if (firstIndex == _firstVisibleIndex) return;
            _firstVisibleIndex = firstIndex;

            for (int i = 0; i < _rows.Count; i++)
            {
                int dataIndex = firstIndex + i;
                var row = _rows[i];
                bool hasData = dataIndex < _entries.Count;
                row.gameObject.SetActive(hasData);
                if (!hasData) continue;

                row.RectTransform.anchoredPosition = new Vector2(0f, -dataIndex * RowStride);
                row.Bind(_entries[dataIndex], dataIndex);
            }
        }
    }
}
