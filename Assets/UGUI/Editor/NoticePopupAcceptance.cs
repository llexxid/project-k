using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using KingdomIdle.OfflineRewards;
using KingdomIdle.UI;
using Newtonsoft.Json;
using PlayFab.ClientModels;
using Scripts.Core.Manager;
using Scripts.Test;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace KingdomIdle.UGUI.Editor
{
    public static class NoticePopupAcceptance
    {
        private const BindingFlags ControllerMembers = BindingFlags.Static | BindingFlags.NonPublic;
        private static readonly Type Controller = typeof(NoticePopupController);

        public static void RunBatch()
        {
            const string version = "0.23.0";
            var posted = new DateTime(2026, 11, 9, 16, 0, 0, DateTimeKind.Utc);
            string longText = "<b>공지 본문</b>\n" + string.Join("\n", Enumerable.Repeat("스크롤 마지막 줄까지 표시되는 공지입니다.", 50));
            TitleNewsItem Post(string id, string body, int days = 0) => new()
            {
                NewsId = id, Title = id, Body = body, Timestamp = posted.AddDays(days)
            };
            string Patch(string appVersion) => JsonConvert.SerializeObject(new { text = longText, appVersion });
            var feed = new List<TitleNewsItem>
            {
                Post("old-patch", Patch(version), -1),
                Post("new-patch-b", Patch(version)),
                Post("new-patch-a", Patch(version)),
                Post("near-version", Patch("0.23.0.0"), 1),
                Post("notice", JsonConvert.SerializeObject(new { text = longText }), 2),
                Post("invalid-body", "{", 3),
                Post("blank-body", "{\"text\":\"  \"}", 3),
                Post("invalid-version", "{\"text\":\"내용\",\"appVersion\":23}", 3),
                Post("nontext-body", "{\"text\":[\"내용\"]}", 3)
            };

            var articles = TitleNewsFeed.Read(feed);
            Check(articles.Select(article => article.Id).SequenceEqual(new[]
                { "notice", "near-version", "new-patch-a", "new-patch-b", "old-patch" }),
                "Malformed articles are skipped, with newest first and ties by ID");
            var patchArticle = TitleNewsFeed.LatestPatch(articles, version);
            Check(patchArticle?.Id == "new-patch-a" && TitleNewsFeed.LatestPatch(articles, "0.22.2") == null,
                "Automatic patch selection requires the exact version and picks the newest match");
            Check(patchArticle.Text == longText && articles[0].Text == longText,
                "Body preserves the literal markup and line breaks");
            Check(NoticePopupController.PatchKey(version) != NoticePopupController.PatchKey("0.22.2"),
                "Displayed versions use distinct device preference keys");

            var catalog = AssetDatabase.LoadAssetAtPath<UIViewCatalog>("Assets/UGUI/UIViewCatalog.asset");
            Check(catalog != null && catalog.popupPatchNotes != catalog.popupNoticeBoard, "The catalog assigns separate popups");
            CheckPopup(catalog.popupPatchNotes);
            CheckPopup(catalog.popupNoticeBoard);
            var main = catalog.screenMain.GetComponent<MainScreenView>();
            var wave = main.waveHud;
            Check(main.btnMenuNotice != null && main.btnMenuNotice.gameObject.activeSelf &&
                main.btnMenuMail != null && main.btnHamburger != null && main.tabStore != null &&
                wave != null && wave.btnStageAction != null &&
                !main.GetComponentsInChildren<Transform>(true).Any(item => item.name is "BtnMenuShop" or "BtnLoopIcon"),
                "The notice menu keeps mail, the bottom shop and the stage action accessible");

            var canvas = new GameObject("Notice Acceptance Canvas", typeof(RectTransform), typeof(Canvas));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var layer = (RectTransform)new GameObject("LayerPopups", typeof(RectTransform)).transform;
            layer.SetParent(canvas.transform, false);
            layer.gameObject.SetActive(false);
            var host = new GameObject("Notice Acceptance Host");
            host.SetActive(false);
            try
            {
                var manager = host.AddComponent<UIManager>();
                manager.catalog = catalog;
                manager.layerPopups = layer;
                Call("ShowPatch", manager, patchArticle);
                var patch = layer.GetComponentInChildren<NoticePopupView>(true);
                Check(patch.patchBody.gameObject.activeSelf && patch.patchBody.text == longText &&
                    !patch.patchBody.richText && patch.patchToggle != null, "Patch opens with one expanded plain-text article");
                layer.gameObject.SetActive(true);
                foreach (float height in new[] { 1440f, 1920f })
                {
                    layer.sizeDelta = new Vector2(1080f, height);
                    Layout(patch);
                    var size = patch.panel.rect.size;
                    var close = patch.closeButton.transform.position;
                    CheckOverflow(patch);
                    patch.patchToggle.onClick.Invoke();
                    Layout(patch);
                    Check(!patch.patchBody.gameObject.activeSelf && patch.panel.rect.size == size &&
                        patch.closeButton.transform.position == close, "Patch collapse keeps the window and close button fixed");
                    patch.patchToggle.onClick.Invoke();
                    Layout(patch);
                    Check(patch.patchBody.gameObject.activeSelf && patch.panel.rect.size == size &&
                        patch.closeButton.transform.position == close, "Patch expands again inside the same window");
                }
                patch.closeButton.onClick.Invoke();
                Check(!patch.gameObject.activeSelf, "The real patch close button hides the popup");

                var board = (NoticePopupView)Call("Create", manager, catalog.popupNoticeBoard, null);
                Set("_board", board);
                board.gameObject.SetActive(true);
                Set("_requestId", 1);
                Set("_deadline", 1f);
                Call("Finish", 1, null);
                Check(board.status.gameObject.activeSelf && board.retryButton.gameObject.activeSelf, "A failed feed exposes retry");
                Set("_requestId", 2);
                Set("_deadline", 1f);
                Call("Finish", 2, new GetTitleNewsResult { News = feed });
                Check(!board.status.gameObject.activeSelf && !board.retryButton.gameObject.activeSelf, "A successful retry displays articles");
                var rows = board.scroll.content.GetComponentsInChildren<NoticeRowView>();
                Check(rows.Select(row => row.gameObject.name).SequenceEqual(articles.Select(article => article.Id)) &&
                    rows.All(row => !row.body.gameObject.activeSelf && !row.body.richText),
                    "The single scroll list starts collapsed and follows the parsed feed order");

                foreach (float height in new[] { 1440f, 1920f })
                {
                    layer.sizeDelta = new Vector2(1080f, height);
                    Layout(board);
                    var size = board.panel.rect.size;
                    var close = board.closeButton.transform.position;
                    rows[0].toggle.onClick.Invoke();
                    Layout(board);
                    Check(rows[0].body.gameObject.activeSelf && rows[0].body.text == longText &&
                        board.panel.rect.size == size && board.closeButton.transform.position == close,
                        "An article opens as plain text without resizing or moving the window");
                    CheckOverflow(board);
                    rows[1].toggle.onClick.Invoke();
                    Check(!rows[0].body.gameObject.activeSelf && rows[1].body.gameObject.activeSelf,
                        "Opening another row collapses the previous row");
                    rows[1].toggle.onClick.Invoke();
                    Check(rows.All(row => !row.body.gameObject.activeSelf), "Tapping the open row collapses it");
                }
                if (Application.isPlaying)
                {
                    manager.RequestBack();
                    Check(!board.gameObject.activeSelf, "The shared back handler closes the board");
                    board.gameObject.SetActive(true);
                }
                board.backdropButton.onClick.Invoke();
                Check(!board.gameObject.activeSelf, "The real board backdrop closes the popup");
                if (Application.isPlaying) VerifyStartupPopups(manager, patch, board);
            }
            finally
            {
                Object.DestroyImmediate(canvas);
                Object.DestroyImmediate(host);
                NoticePopupController.Reset();
            }
            Debug.Log("NOTICE POPUP ACCEPTANCE PASSED");
        }

        private static void VerifyStartupPopups(UIManager manager, NoticePopupView patch, NoticePopupView board)
        {
            var previousUi = UIManager.Instance;
            var uiInstance = typeof(UIManager).GetProperty(nameof(UIManager.Instance));
            var previousNetwork = NetworkManager.Instance;
            Check(previousNetwork == null || previousNetwork.LoginState == GameLoginState.SignedOut,
                "Startup fixture requires a signed-out Play Mode");
            var network = manager.gameObject.AddComponent<NetworkManager>();
            typeof(NetworkManager).GetProperty(nameof(NetworkManager.LoginState)).SetValue(network, GameLoginState.Ready);
            NetworkManager.Instance = network;
            const BindingFlags instanceFields = BindingFlags.Instance | BindingFlags.NonPublic;
            var activeScreenId = typeof(UIManager).GetField("_activeScreenId", instanceFields);
            activeScreenId.SetValue(manager, UIScreenId.Title);
            typeof(UIManager).GetField("_activeScreenGo", instanceFields).SetValue(manager, manager.gameObject);
            Set("_network", network);
            Set("_generation", network.AccountGeneration);
            Set("_hasFeed", true);

            string version = Application.version;
            string key = NoticePopupController.PatchKey(version);
            bool hadSeen = PlayerPrefs.HasKey(key);
            int priorSeen = PlayerPrefs.GetInt(key);
            try
            {
                uiInstance.SetValue(null, manager);
                GameTest.ResetMainNoticeSeen();
                var title = manager.gameObject.AddComponent<TitleScreenView>();
                var hint = new GameObject("Title Hint", typeof(TMPro.TextMeshProUGUI));
                hint.transform.SetParent(manager.transform, false);
                title.pressHint = hint.GetComponent<TMPro.TextMeshProUGUI>();
                var titleController = new TitleScreenController();
                try
                {
                    titleController.Bind(title, manager);
                    Check(Controller.GetField("_titleContinuation", ControllerMembers).GetValue(null) is Action &&
                        title.pressHint.text == "업데이트 소식을 확인하는 중입니다.",
                        "An already authenticated Title bind resumes entry with a visible update check");
                }
                finally
                {
                    titleController.Dispose();
                    NoticePopupController.CloseForScreenChange();
                }
                int continuations = 0;
                Action continueLogin = () => continuations++;
                Set("_articles", new List<TitleNewsFeed>());
                Set("_titleContinuation", continueLogin);
                NoticePopupController.Tick(manager);
                Check(continuations == 1 && !patch.gameObject.activeSelf && !PlayerPrefs.HasKey(key),
                    "No matching title patch continues login without recording the version");

                var article = new TitleNewsItem
                {
                    NewsId = "title-patch-fixture",
                    Title = "업데이트 노트",
                    Body = JsonConvert.SerializeObject(new { text = "타이틀에서 표시되는 테스트 패치노트", appVersion = version }),
                    Timestamp = DateTime.UtcNow
                };
                var articles = TitleNewsFeed.Read(new List<TitleNewsItem> { article });
                Set("_articles", articles);
                Set("_titleContinuation", continueLogin);
                NoticePopupController.Tick(manager);
                NoticePopupController.Tick(manager);
                Check(continuations == 1 && patch.gameObject.activeInHierarchy && !board.gameObject.activeSelf && PlayerPrefs.HasKey(key),
                    "The matching patch displays on title, records the version, and waits for dismissal");
                patch.closeButton.onClick.Invoke();
                NoticePopupController.Tick(manager);
                Check(continuations == 2 && !patch.gameObject.activeSelf, "Closing the title patch continues login exactly once");

                Set("_titleContinuation", continueLogin);
                NoticePopupController.Tick(manager);
                Check(continuations == 3 && !patch.gameObject.activeSelf, "The same version continues login without repeating the patch");
                GameTest.ResetMainNoticeSeen();
                Check(!PlayerPrefs.HasKey(key), "The editor reset clears the shown version");
                Set("_titleContinuation", continueLogin);
                NoticePopupController.Tick(manager);
                Check(continuations == 3 && patch.gameObject.activeInHierarchy, "The reset allows the title patch to show again");
                patch.backdropButton.onClick.Invoke();
                NoticePopupController.Tick(manager);
                Check(continuations == 4, "Backdrop dismissal also continues login");

                GameTest.ResetMainNoticeSeen();
                Set("_articles", new List<TitleNewsFeed>());
                NoticePopupController.BeginMainEntry();
                NoticePopupController.Tick(manager);
                Check(!board.gameObject.activeSelf, "A queued board waits for the Main screen");
                NoticePopupController.CloseForScreenChange();
                activeScreenId.SetValue(manager, UIScreenId.Main);
                typeof(UIManager).GetField("_mainController", instanceFields).SetValue(manager, new MainScreenController());
                var result = new OfflineRewardClaimResult("fixture", null, 10, 5, 0, 1, 5, 0);
                var reward = RewardPopupView.ShowOfflineRewards(manager.LayerPopups, result, false);
                Check(reward != null && reward.gameObject.activeInHierarchy, "Synthetic offline reward uses the real result prefab");
                NoticePopupController.Tick(manager);
                Check(!board.gameObject.activeSelf && !patch.gameObject.activeSelf, "Visible reward result blocks the in-game board");

                reward.confirmButton.onClick.Invoke();
                NoticePopupController.Tick(manager);
                Check(board.gameObject.activeInHierarchy && board.status.gameObject.activeSelf &&
                    board.status.text == "등록된 공지가 없습니다." && !board.retryButton.gameObject.activeSelf,
                    "Reward confirmation opens the empty in-game board");
                Check((bool)Controller.GetField("_boardShown", ControllerMembers).GetValue(null), "The in-game board is recorded for this login");
                board.closeButton.onClick.Invoke();
                Set("_articles", articles);
                NoticePopupController.Tick(manager);
                Check(!board.gameObject.activeSelf && !patch.gameObject.activeSelf && !PlayerPrefs.HasKey(key),
                    "The board stays closed and Main does not show or record the title patch");
            }
            finally
            {
                uiInstance.SetValue(null, previousUi);
                NetworkManager.Instance = previousNetwork;
                if (hadSeen) PlayerPrefs.SetInt(key, priorSeen);
                else PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
            }
        }

        private static void CheckPopup(GameObject prefab)
        {
            Check(prefab != null && prefab.TryGetComponent<NoticePopupView>(out _), "Catalog popup has a notice view");
            var view = prefab.GetComponent<NoticePopupView>();
            Check(view.panel != null && view.closeButton != null && view.backdropButton != null && view.scroll != null &&
                view.scroll.viewport != null && view.scroll.content != null && view.scroll.vertical && !view.scroll.horizontal &&
                prefab.GetComponentsInChildren<ScrollRect>(true).Length == 1 && !view.closeButton.transform.IsChildOf(view.scroll.transform),
                "Popup references one vertical ScrollRect and keeps close outside it");
        }

        private static void Layout(NoticePopupView view)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)view.scroll.transform.parent);
            LayoutRebuilder.ForceRebuildLayoutImmediate(view.scroll.content);
            Canvas.ForceUpdateCanvases();
        }

        private static void CheckOverflow(NoticePopupView view)
        {
            Check(view.scroll.viewport.rect.height > 0f && view.scroll.content.rect.height > view.scroll.viewport.rect.height,
                "Long content overflows a positive viewport");
            view.scroll.verticalNormalizedPosition = 0f;
            Check(view.scroll.verticalNormalizedPosition < .01f, "The scroll can reach the end of long content");
        }

        private static object Call(string name, params object[] args) => Controller.GetMethod(name, ControllerMembers).Invoke(null, args);
        private static void Set(string name, object value) => Controller.GetField(name, ControllerMembers).SetValue(null, value);

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Notice popup acceptance: " + message);
        }
    }
}
