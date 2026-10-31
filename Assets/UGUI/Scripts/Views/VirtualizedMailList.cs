using System;
using System.Collections.Generic;
using Protocol.Message.Dto;
using UnityEngine;

namespace KingdomIdle.UGUI
{
    /// <summary>Fixed-height mail rows; existing prefab geometry and nested reward scrolling are retained.</summary>
    public sealed class VirtualizedMailList : MonoBehaviour
    {
        private UnityEngine.UI.ScrollRect _scroll;
        private RectTransform _content, _viewport;
        private GameObject _template;
        private Transform _popupOwner;
        private Action<long> _receive;
        private readonly List<MailRowView> _rows = new();
        private IReadOnlyList<MailDTO> _entries = Array.Empty<MailDTO>();
        private float _height, _spacing, _viewportHeight, _viewportWidth;
        private RectOffset _padding;
        private int _first = -1;
        private bool _canClaim;
        private long _now;
        public int PoolCount => _rows.Count;
        public int VisibleStart => Mathf.Max(0, _first);
        private float Stride => _height + _spacing;

        public void Initialize(RectTransform content, GameObject template, Transform popupOwner, Action<long> receive)
        {
            _content = content; _template = template; _popupOwner = popupOwner; _receive = receive;
            _scroll = content.GetComponentInParent<UnityEngine.UI.ScrollRect>();
            _viewport = _scroll.viewport;
            var layout = content.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
            _spacing = layout != null ? layout.spacing : 0;
            _padding = layout != null ? layout.padding : new RectOffset();
            if (layout != null) layout.enabled = false;
            var fitter = content.GetComponent<UnityEngine.UI.ContentSizeFitter>();
            if (fitter != null) fitter.enabled = false;
            var sample = Instantiate(template, content, false).GetComponent<MailRowView>();
            var rect = (RectTransform)sample.transform;
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
            _height = Mathf.Max(1, UnityEngine.UI.LayoutUtility.GetPreferredHeight(rect));
            var rowFitter = sample.GetComponent<UnityEngine.UI.ContentSizeFitter>();
            if (rowFitter != null) rowFitter.enabled = false;
            Configure(rect);
            sample.gameObject.SetActive(false); _rows.Add(sample);
            _scroll.onValueChanged.AddListener(OnScroll);
        }

        private void Configure(RectTransform rect)
        {
            rect.anchorMin = new Vector2(0, 1); rect.anchorMax = new Vector2(1, 1); rect.pivot = new Vector2(.5f, 1);
            rect.sizeDelta = new Vector2(-_padding.horizontal, _height);
        }

        public void SetEntries(IReadOnlyList<MailDTO> entries, bool canClaim, long now)
        {
            int anchorIndex = Mathf.Clamp(Mathf.FloorToInt(Mathf.Max(0, _content.anchoredPosition.y - _padding.top) / Stride), 0, Mathf.Max(0, _entries.Count - 1));
            long anchor = _entries.Count > 0 ? _entries[anchorIndex].MailId : 0;
            float offset = _content.anchoredPosition.y - anchorIndex * Stride;
            bool changed = !ReferenceEquals(_entries, entries);
            _entries = entries ?? Array.Empty<MailDTO>(); _canClaim = canClaim; _now = now;
            float height = _padding.vertical + _entries.Count * _height + Mathf.Max(0, _entries.Count - 1) * _spacing;
            _content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            if (changed)
            {
                int next = -1;
                for (int i = 0; i < _entries.Count; i++) if (_entries[i].MailId == anchor) { next = i; break; }
                if (next < 0) next = Mathf.Min(anchorIndex, Mathf.Max(0, _entries.Count - 1));
                _scroll.StopMovement();
                float position = anchor == 0 ? 0 : next * Stride + offset;
                _content.anchoredPosition = new Vector2(_content.anchoredPosition.x, Mathf.Clamp(position, 0, Mathf.Max(0, height - _viewport.rect.height)));
                _first = -1;
            }
            EnsurePool(); Render(); UpdateTime(now, canClaim);
        }

        private void EnsurePool()
        {
            _viewportHeight = _viewport.rect.height; _viewportWidth = _viewport.rect.width;
            int needed = Math.Min(_entries.Count, Mathf.CeilToInt(Mathf.Max(1, _viewportHeight) / Stride) + 2);
            while (_rows.Count < needed)
            {
                var row = Instantiate(_template, _content, false).GetComponent<MailRowView>();
                var fitter = row.GetComponent<UnityEngine.UI.ContentSizeFitter>();
                if (fitter != null) fitter.enabled = false;
                Configure((RectTransform)row.transform); row.gameObject.SetActive(false); _rows.Add(row);
            }
            while (_rows.Count > Mathf.Max(1, needed))
            {
                var row = _rows[_rows.Count - 1]; row.Unbind(); Destroy(row.gameObject); _rows.RemoveAt(_rows.Count - 1);
            }
        }

        private void LateUpdate()
        {
            if (_viewport == null || (Mathf.Approximately(_viewportHeight, _viewport.rect.height) && Mathf.Approximately(_viewportWidth, _viewport.rect.width))) return;
            EnsurePool(); _first = -1; Render();
        }
        private void OnScroll(Vector2 _) => Render();
        private void Render()
        {
            int first = Mathf.Clamp(Mathf.FloorToInt(Mathf.Max(0, _content.anchoredPosition.y - _padding.top) / Stride), 0, Mathf.Max(0, _entries.Count - _rows.Count));
            if (first == _first) return;
            _first = first;
            for (int i = 0; i < _rows.Count; i++)
            {
                int index = first + i;
                var row = _rows[i]; bool active = index < _entries.Count;
                row.gameObject.SetActive(active);
                if (!active) { row.Unbind(); continue; }
                ((RectTransform)row.transform).anchoredPosition = new Vector2((_padding.left - _padding.right) * .5f, -_padding.top - index * Stride);
                var mail = _entries[index];
                row.Bind(mail, _popupOwner, () => _receive(mail.MailId), _now, _canClaim);
            }
        }
        public void UpdateTime(long now, bool canClaim)
        {
            _now = now; _canClaim = canClaim;
            foreach (var row in _rows)
                if (row.gameObject.activeSelf) { row.RefreshExpiry(now); row.SetCanClaim(canClaim, now); }
        }
        private void OnDestroy()
        {
            if (_scroll != null) _scroll.onValueChanged.RemoveListener(OnScroll);
        }
    }
}
