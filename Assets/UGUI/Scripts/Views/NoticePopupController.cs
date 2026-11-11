using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using KingdomIdle.UI;
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
        private const float TitleRequestTimeout = 5f;
        private const float RefreshInterval = 300f;
        private const float MenuRefreshInterval = 30f;
        private static readonly TimeZoneInfo KoreanTime = TimeZoneInfo.CreateCustomTimeZone("KST", TimeSpan.FromHours(9), "KST", "KST");
        private static readonly List<NoticeRowView> Rows = new();
        private static List<TitleNewsFeed> _articles = new();
        private static NetworkManager _network;
        private static long _generation = -1;
        private static int _requestId;
        private static float _deadline;
        private static bool _failed;
        private static bool _hasFeed;
        private static bool _hasUnread;
        private static float _lastRequestAt = float.NegativeInfinity;
        private static float _nextRefreshAt;
        private static Action _titleContinuation;
        private static TitleNewsFeed _titleArticle;
        private static float _titleDeadline;
        private static NoticePopupView _board;
        private static NoticePopupView _patch;
        private static NoticeRowView _expandedRow;

        internal static event Action Changed;
        internal static bool HasUnread => IsCurrentSession && _hasUnread;
        private static bool IsCurrentSession => _network != null && _network == NetworkManager.Instance &&
            _network.IsGameDataReady && _network.AccountGeneration == _generation;

        internal static void BeginMainEntry()
        {
            if (!UseCurrentSession()) return;
            if (!_hasFeed) Refresh();
        }

        internal static void RefreshFromMenu()
        {
            if (UseCurrentSession() && Time.unscaledTime - _lastRequestAt >= MenuRefreshInterval) Refresh();
        }

        internal static void ShowTitlePatchThen(UIManager host, Action continueToMain)
        {
            CloseForScreenChange();
            if (host.ActiveScreenId != UIScreenId.Title || PlayerPrefs.HasKey(PatchKey(Application.version)))
            {
                continueToMain();
                return;
            }

            _titleArticle = BundledPatchNotes.Load(Application.version);
            if (_titleArticle == null)
            {
                continueToMain();
                return;
            }
            _titleContinuation = continueToMain;
            _titleDeadline = Time.unscaledTime + TitleRequestTimeout;
        }

        internal static void ShowBoard()
        {
            var host = UIManager.Instance;
            if (host?.ActiveMain == null || !UseCurrentSession()) return;
            if (OpenBoard(host)) RefreshFromMenu();
        }

        private static bool OpenBoard(UIManager host)
        {
            if (_board == null)
            {
                _board = Create(host, host.Catalog?.popupNoticeBoard);
                if (_board == null) return false;
                _board.retryButton.onClick.AddListener(() => Refresh());
            }

            Open(_board);
            RebuildRows();
            _board.scroll.verticalNormalizedPosition = 1f;
            RefreshStatus();
            MarkFeedRead();
            return true;
        }

        internal static void Tick(UIManager host)
        {
            if (_titleContinuation != null)
            {
                TickTitle(host);
                return;
            }
            if (_generation < 0) return;
            if (!IsCurrentSession)
            {
                Reset();
                return;
            }

            if (_deadline > 0f && Time.unscaledTime >= _deadline) Finish(_requestId, null);
            if (host.ActiveMain != null && Time.unscaledTime >= _nextRefreshAt) Refresh();
        }

        private static void TickTitle(UIManager host)
        {
            if (host.ActiveScreenId != UIScreenId.Title)
            {
                _titleContinuation = null;
                _titleArticle = null;
                return;
            }
            if (_patch != null && _patch.gameObject.activeInHierarchy) return;

            string version = Application.version;
            string key = PatchKey(version);
            var article = _titleArticle;
            if (article == null || PlayerPrefs.HasKey(key))
            {
                FinishTitleEntry();
                return;
            }
            if (!host.CanShowTitlePatch)
            {
                if (Time.unscaledTime >= _titleDeadline) FinishTitleEntry();
                return;
            }
            if (!ShowPatch(host, article))
            {
                FinishTitleEntry();
                return;
            }

            PlayerPrefs.SetInt(key, 1);
            PlayerPrefs.Save();
        }

        private static void FinishTitleEntry()
        {
            var continuation = _titleContinuation;
            _titleContinuation = null;
            _titleArticle = null;
            continuation?.Invoke();
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

        private static void Refresh(float timeout = RequestTimeout)
        {
            if (_deadline > 0f || !IsCurrentSession) return;
            int request = ++_requestId;
            _lastRequestAt = Time.unscaledTime;
            _nextRefreshAt = _lastRequestAt + RefreshInterval;
            _deadline = _lastRequestAt + timeout;
            _failed = false;
            RefreshStatus();
            _network.GetTitleNews(result => Finish(request, result), () => Finish(request, null));
        }

        private static void Finish(int request, GetTitleNewsResult result)
        {
            if (request != _requestId || _deadline <= 0f || !IsCurrentSession) return;
            _deadline = 0f;
            _failed = result == null;
            if (!_failed)
            {
                _hasFeed = true;
                var next = TitleNewsFeed.Read(result.News);
                bool changed = !SameFeed(next);
                _articles = next;
                if (BoardOpen)
                {
                    if (changed) RebuildRows(_expandedRow != null ? _expandedRow.gameObject.name : null);
                    MarkFeedRead();
                }
                UpdateUnread();
            }
            RefreshStatus();
        }

        private static bool SameFeed(List<TitleNewsFeed> next)
        {
            if (_articles.Count != next.Count) return false;
            for (int i = 0; i < next.Count; i++)
            {
                var current = _articles[i];
                var article = next[i];
                if (current.Id != article.Id || current.Title != article.Title || current.Text != article.Text ||
                    current.AppVersion != article.AppVersion || current.Timestamp != article.Timestamp) return false;
            }
            return true;
        }

        private static bool BoardOpen => _board != null && _board.gameObject.activeInHierarchy;

        internal static string ArticleKey(TitleNewsFeed article) => "Notice.ArticleRead." + Hash(article.Id);

        private static string Fingerprint(TitleNewsFeed article) => Hash(
            Newtonsoft.Json.JsonConvert.SerializeObject(new[] { article.Title, article.Text, article.AppVersion }));

        private static string Hash(string value)
        {
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "");
        }

        private static void MarkFeedRead()
        {
            if (!BoardOpen || !_hasFeed || !IsCurrentSession) return;
            bool changed = false;
            foreach (var article in _articles)
            {
                string key = ArticleKey(article);
                string fingerprint = Fingerprint(article);
                if (PlayerPrefs.GetString(key) == fingerprint) continue;
                PlayerPrefs.SetString(key, fingerprint);
                changed = true;
            }
            if (changed) PlayerPrefs.Save();
            UpdateUnread();
        }

        private static void UpdateUnread()
        {
            bool unread = _articles.Exists(article => PlayerPrefs.GetString(ArticleKey(article)) != Fingerprint(article));
            if (_hasUnread == unread) return;
            _hasUnread = unread;
            Changed?.Invoke();
        }

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
                _patch = Create(host, host.Catalog?.popupPatchNotes, FinishTitleEntry);
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

        private static NoticePopupView Create(UIManager host, GameObject prefab, Action onClose = null)
        {
            if (prefab == null || host.LayerPopups == null) return null;
            var go = Object.Instantiate(prefab, host.LayerPopups, false);
            if (!go.TryGetComponent<NoticePopupView>(out var view))
            {
                Object.Destroy(go);
                return null;
            }

            go.SetActive(false);
            void Close()
            {
                go.SetActive(false);
                onClose?.Invoke();
            }
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
            _titleContinuation = null;
            _titleArticle = null;
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
            _hasFeed = false;
            _lastRequestAt = float.NegativeInfinity;
            _nextRefreshAt = 0f;
            if (_hasUnread)
            {
                _hasUnread = false;
                Changed?.Invoke();
            }
        }
    }
}
