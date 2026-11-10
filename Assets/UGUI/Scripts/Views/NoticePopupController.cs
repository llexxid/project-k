using System;
using System.Collections.Generic;
using System.Globalization;
using PlayFab.ClientModels;
using Scripts.Core.Manager;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace KingdomIdle.UGUI
{
    internal static class NoticePopupController
    {
        private const float RequestTimeout = 15f;
        private static readonly TimeZoneInfo KoreanTime = TimeZoneInfo.CreateCustomTimeZone("KST", TimeSpan.FromHours(9), "KST", "KST");
        private static readonly List<NoticeRowView> Rows = new();
        private static List<TitleNewsFeed> _articles = new();
        private static NetworkManager _network;
        private static long _generation = -1;
        private static int _requestId;
        private static float _deadline;
        private static bool _failed;
        private static bool _startupAttempted;
        private static bool _pendingAuto;
        private static NoticePopupView _board;
        private static NoticePopupView _patch;
        private static NoticeRowView _expandedRow;

        internal static void BeginMainEntry()
        {
            if (UIManager.Instance?.ActiveMain == null || !UseCurrentSession() || _startupAttempted) return;
            _startupAttempted = true;
            _pendingAuto = !PlayerPrefs.HasKey(PatchKey(Application.version));
            if (_articles.Count == 0) Refresh();
        }

        internal static void ShowBoard()
        {
            var host = UIManager.Instance;
            if (host?.ActiveMain == null || !UseCurrentSession()) return;
            if (_board == null)
            {
                _board = Create(host, host.Catalog?.popupNoticeBoard);
                if (_board == null) return;
                _board.retryButton.onClick.AddListener(Refresh);
            }

            Open(_board);
            RebuildRows();
            _board.scroll.verticalNormalizedPosition = 1f;
            Refresh();
            RefreshStatus();
        }

        internal static void Tick(UIManager host)
        {
            if (_generation < 0) return;
            if (_network == null || !_network.IsGameDataReady || _network.AccountGeneration != _generation)
            {
                Reset();
                return;
            }

            if (_deadline > 0f && Time.unscaledTime >= _deadline) Finish(_requestId, null);
            if (!_pendingAuto || _deadline > 0f || !host.CanShowFeatureGuide) return;
            if (Scripts.Core.GameManager.Instance?.OfflineReward?.IsBusy == true) return;
            var guide = Direction.GameDirectManager.Instance;
            if (guide != null && (guide.ActiveSequenceId != null || guide.PendingCount != 0)) return;

            _pendingAuto = false;
            string version = Application.version;
            var article = TitleNewsFeed.LatestPatch(_articles, version);
            if (article != null && !PlayerPrefs.HasKey(PatchKey(version)) && ShowPatch(host, article))
            {
                PlayerPrefs.SetInt(PatchKey(version), 1);
                PlayerPrefs.Save();
            }
        }

        internal static string PatchKey(string version) => "Notice.PatchShown." + version;

        private static bool UseCurrentSession()
        {
            var network = NetworkManager.Instance;
            if (network == null || !network.IsGameDataReady) return false;
            if (_network == network && _generation == network.AccountGeneration) return true;
            Reset();
            _network = network;
            _generation = network.AccountGeneration;
            return true;
        }

        private static void Refresh()
        {
            if (_deadline > 0f) return;
            int request = ++_requestId;
            _deadline = Time.unscaledTime + RequestTimeout;
            _failed = false;
            RefreshStatus();
            _network.GetTitleNews(result => Finish(request, result), () => Finish(request, null));
        }

        private static void Finish(int request, GetTitleNewsResult result)
        {
            if (request != _requestId || _deadline <= 0f) return;
            _deadline = 0f;
            _failed = result == null;
            if (_failed)
            {
                if (_articles.Count == 0) _pendingAuto = false;
            }
            else
            {
                _articles = TitleNewsFeed.Read(result.News);
                if (BoardOpen) RebuildRows(_expandedRow != null ? _expandedRow.gameObject.name : null);
            }
            RefreshStatus();
        }

        private static bool BoardOpen => _board != null && _board.gameObject.activeInHierarchy;

        private static void RefreshStatus()
        {
            if (!BoardOpen) return;
            string status = string.Empty;
            if (_failed) status = "공지를 불러오지 못했습니다.";
            else if (_articles.Count == 0)
                status = _deadline > 0f ? "공지를 불러오는 중입니다." : "등록된 공지가 없습니다.";
            _board.status.text = status;
            _board.status.gameObject.SetActive(status.Length != 0);
            _board.retryButton.gameObject.SetActive(_failed);
        }

        private static void RebuildRows(string expandedId = null)
        {
            foreach (var row in Rows)
            {
                row.gameObject.SetActive(false);
                Object.Destroy(row.gameObject);
            }
            Rows.Clear();
            _expandedRow = null;

            foreach (var article in _articles)
            {
                var row = Object.Instantiate(_board.rowTemplate, _board.scroll.content, false);
                row.gameObject.name = article.Id;
                row.title.text = article.Title;
                var date = TimeZoneInfo.ConvertTimeFromUtc(article.Timestamp.ToUniversalTime(), KoreanTime);
                row.date.text = date.ToString("yyyy.MM.dd", CultureInfo.InvariantCulture);
                row.body.text = article.Text;
                bool expanded = article.Id == expandedId;
                SetRowExpanded(row, expanded);
                if (expanded) _expandedRow = row;
                row.toggle.onClick.AddListener(() => ToggleRow(row));
                row.gameObject.SetActive(true);
                Rows.Add(row);
            }
        }

        private static void ToggleRow(NoticeRowView row)
        {
            var content = _board.scroll.content;
            var heading = row.toggle.transform;
            float headingY = heading.position.y;
            var next = _expandedRow == row ? null : row;
            if (_expandedRow != null) SetRowExpanded(_expandedRow, false);
            _expandedRow = next;
            if (next != null) SetRowExpanded(next, true);

            // Closing an earlier row can move the tapped header out of the viewport.
            _board.scroll.StopMovement();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            float shift = (headingY - heading.position.y) / content.lossyScale.y;
            var position = content.anchoredPosition;
            float scrollable = Mathf.Max(0f, content.rect.height - _board.scroll.viewport.rect.height);
            content.anchoredPosition = new Vector2(position.x, Mathf.Clamp(position.y + shift, 0f, scrollable));
        }

        private static void SetRowExpanded(NoticeRowView row, bool expanded)
        {
            row.body.gameObject.SetActive(expanded);
            row.caret.text = expanded ? "-" : "+";
        }

        private static bool ShowPatch(UIManager host, TitleNewsFeed article)
        {
            if (_patch == null)
            {
                _patch = Create(host, host.Catalog?.popupPatchNotes);
                if (_patch == null) return false;
                _patch.patchToggle.onClick.AddListener(() => SetPatchExpanded(!_patch.patchBody.gameObject.activeSelf));
            }

            _patch.patchTitle.text = article.Title;
            _patch.patchBody.text = article.Text;
            SetPatchExpanded(true);
            Open(_patch);
            _patch.scroll.verticalNormalizedPosition = 1f;
            return _patch.gameObject.activeInHierarchy;
        }

        private static void SetPatchExpanded(bool expanded)
        {
            _patch.patchBody.gameObject.SetActive(expanded);
            _patch.patchCaret.text = expanded ? "-" : "+";
        }

        private static NoticePopupView Create(UIManager host, GameObject prefab)
        {
            if (prefab == null || host.LayerPopups == null) return null;
            var go = Object.Instantiate(prefab, host.LayerPopups, false);
            if (!go.TryGetComponent<NoticePopupView>(out var view))
            {
                Object.Destroy(go);
                return null;
            }

            go.SetActive(false);
            void Close() => go.SetActive(false);
            view.closeButton.onClick.AddListener(Close);
            view.backdropButton.onClick.AddListener(Close);
            ModalBackHandler.Bind(go, Close);
            return view;
        }

        private static void Open(NoticePopupView view)
        {
            view.transform.SetAsLastSibling();
            view.gameObject.SetActive(true);
            UITween.PopIn(view.panel);
        }

        internal static void CloseForScreenChange()
        {
            _pendingAuto = false;
            if (_board != null) _board.gameObject.SetActive(false);
            if (_patch != null) _patch.gameObject.SetActive(false);
        }

        internal static void Reset()
        {
            CloseForScreenChange();
            if (_board != null) Object.Destroy(_board.gameObject);
            if (_patch != null) Object.Destroy(_patch.gameObject);
            _board = null;
            _patch = null;
            _expandedRow = null;
            Rows.Clear();
            _articles.Clear();
            _network = null;
            _generation = -1;
            _deadline = 0f;
            _failed = false;
            _startupAttempted = false;
        }
    }
}
