using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using KingdomIdle.Mailbox;
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
            var previousNetwork = NetworkManager.Instance;
            Check(previousNetwork == null || previousNetwork.LoginState == GameLoginState.SignedOut,
                "The notice fixture requires a signed-out session");
            var savedReads = articles.ToDictionary(NoticePopupController.ArticleKey,
                article => PlayerPrefs.HasKey(NoticePopupController.ArticleKey(article))
                    ? PlayerPrefs.GetString(NoticePopupController.ArticleKey(article)) : null);
            try
            {
                var manager = host.AddComponent<UIManager>();
                manager.catalog = catalog;
                manager.layerPopups = layer;
                var network = host.AddComponent<NetworkManager>();
                typeof(NetworkManager).GetProperty(nameof(NetworkManager.LoginState)).SetValue(network, GameLoginState.Ready);
                NetworkManager.Instance = network;
                Set("_network", network);
                Set("_generation", network.AccountGeneration);
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
                if (Application.isPlaying)
                {
                    VerifyStartupPopups(manager, patch, board);
                    VerifyNotifications(manager, network, longText);
                }
            }
            finally
            {
                NoticePopupController.Reset();
                NetworkManager.Instance = previousNetwork;
                Object.DestroyImmediate(canvas);
                Object.DestroyImmediate(host);
                foreach (var saved in savedReads)
                {
                    if (saved.Value == null) PlayerPrefs.DeleteKey(saved.Key);
                    else PlayerPrefs.SetString(saved.Key, saved.Value);
                }
                PlayerPrefs.Save();
            }
            Debug.Log("NOTICE POPUP ACCEPTANCE PASSED");
        }

        private static void VerifyStartupPopups(UIManager manager, NoticePopupView patch, NoticePopupView board)
        {
            var previousUi = UIManager.Instance;
            var uiInstance = typeof(UIManager).GetProperty(nameof(UIManager.Instance));
            var previousNetwork = NetworkManager.Instance;
            const BindingFlags instanceFields = BindingFlags.Instance | BindingFlags.NonPublic;
            var activeScreenId = typeof(UIManager).GetField("_activeScreenId", instanceFields);
            activeScreenId.SetValue(manager, UIScreenId.Title);
            typeof(UIManager).GetField("_activeScreenGo", instanceFields).SetValue(manager, manager.gameObject);
            string version = Application.version;
            string key = NoticePopupController.PatchKey(version);
            bool hadSeen = PlayerPrefs.HasKey(key);
            int priorSeen = PlayerPrefs.GetInt(key);
            TitleScreenController titleController = null;
            try
            {
                uiInstance.SetValue(null, manager);
                NetworkManager.Instance = null;
                PlayerPrefs.DeleteKey(key);
                var bundled = BundledPatchNotes.Load("0.30.0");
                Check(bundled != null && bundled.Title == "0.30.0 패치노트" && bundled.AppVersion == "0.30.0",
                    "The current release carries its versioned patch text before authentication");
                Check(BundledPatchNotes.Parse("{", version) == null &&
                    BundledPatchNotes.Parse("{\"title\":\"T\",\"text\":\"B\",\"appVersion\":\"0.0.1\"}", version) == null &&
                    BundledPatchNotes.Load("0.0.0") == null && BundledPatchNotes.Load("../0.30.0") == null,
                    "Missing, malformed and mismatched bundled notes have no display article");

                var title = manager.gameObject.AddComponent<TitleScreenView>();
                title.popupLogin = new GameObject("Synthetic Login Popup");
                title.popupLogin.transform.SetParent(manager.transform, false);
                var login = new GameObject("Synthetic Login Button", typeof(RectTransform), typeof(Image), typeof(Button));
                login.transform.SetParent(manager.transform, false);
                title.btnLogin = login.GetComponent<Button>();
                titleController = new TitleScreenController();
                int requestBefore = (int)Get("_requestId");
                Set("_failed", true);
                titleController.Bind(title, manager);
                if (BundledPatchNotes.Load(version) == null)
                {
                    Check(Get("_titleContinuation") == null && !PlayerPrefs.HasKey(key),
                        "A build without matching bundled text continues without recording a display");
                    return;
                }
                Check(Get("_titleContinuation") is Action && (int)Get("_requestId") == requestBefore,
                    "Signed-out Title bind schedules bundled notes without a server request");
                title.btnLogin.onClick.Invoke();
                Check(!title.popupLogin.activeSelf, "Login waits while the title note is pending");
                NoticePopupController.Tick(manager);
                Check(patch.gameObject.activeInHierarchy && patch.patchTitle.text == BundledPatchNotes.Load(version).Title &&
                    PlayerPrefs.HasKey(key) && (int)Get("_requestId") == requestBefore,
                    "Signed-out notes render and record the version despite a failed server feed");
                Check(!board.gameObject.activeSelf, "The title note does not open the in-game board");
                patch.closeButton.onClick.Invoke();
                title.btnLogin.onClick.Invoke();
                Check(title.popupLogin.activeSelf, "Closing the note restores the normal login UI");
                titleController.HandleBack();

                int continuations = 0;
                NoticePopupController.ShowTitlePatchThen(manager, () => continuations++);
                Check(continuations == 1 && !patch.gameObject.activeSelf,
                    "The same version continues exactly once without repeating its note");
                var parsedVersion = new Version(version);
                string nextVersion = new Version(parsedVersion.Major, parsedVersion.Minor, Math.Max(0, parsedVersion.Build) + 1).ToString();
                Check(NoticePopupController.PatchKey(version) != NoticePopupController.PatchKey(nextVersion) &&
                    BundledPatchNotes.Parse(JsonConvert.SerializeObject(new { title = "다음 버전", text = "본문", appVersion = nextVersion }), nextVersion) != null,
                    "The next version has independent display state and matching bundled content");

                PlayerPrefs.DeleteKey(key);
                NoticePopupController.ShowTitlePatchThen(manager, () => continuations++);
                NoticePopupController.Tick(manager);
                Check(patch.gameObject.activeInHierarchy && PlayerPrefs.HasKey(key), "An unseen version opens from the bundle again");
                NoticePopupController.CloseForScreenChange();
                patch.closeButton.onClick.Invoke();
                Check(continuations == 1, "Screen closure cancels a pending title continuation");

                PlayerPrefs.DeleteKey(key);
                typeof(UIManager).GetField("_activeScreenGo", instanceFields).SetValue(manager, null);
                NoticePopupController.ShowTitlePatchThen(manager, () => continuations++);
                Set("_titleDeadline", -1f);
                NoticePopupController.Tick(manager);
                Check(continuations == 2 && !PlayerPrefs.HasKey(key),
                    "An unavailable title popup releases login without marking the version");
            }
            finally
            {
                titleController?.Dispose();
                NoticePopupController.CloseForScreenChange();
                typeof(UIManager).GetField("_activeScreenGo", instanceFields).SetValue(manager, manager.gameObject);
                activeScreenId.SetValue(manager, UIScreenId.Main);
                uiInstance.SetValue(null, previousUi);
                NetworkManager.Instance = previousNetwork;
                if (hadSeen) PlayerPrefs.SetInt(key, priorSeen);
                else PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
            }
        }

        private static void VerifyNotifications(UIManager manager, NetworkManager network, string longText)
        {
            const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
            var uiInstance = typeof(UIManager).GetProperty(nameof(UIManager.Instance));
            var previousUi = UIManager.Instance;
            var menuRoot = new GameObject("Notice Notification Fixture", typeof(RectTransform), typeof(MainScreenView));
            menuRoot.SetActive(false);
            var menu = menuRoot.GetComponent<MainScreenView>();
            Button MakeButton(string name)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
                go.transform.SetParent(menuRoot.transform, false);
                return go.GetComponent<Button>();
            }
            menu.btnHamburger = MakeButton("Hamburger");
            menu.btnMenuMail = MakeButton("Mail");
            menu.btnMenuNotice = MakeButton("Notice");
            var main = new MainScreenController();
            var mainType = typeof(MainScreenController);
            mainType.GetField("_view", fields).SetValue(main, menu);
            mainType.GetField("_host", fields).SetValue(main, manager);
            void MenuCall(string method) => mainType.GetMethod(method, fields).Invoke(main, null);
            bool Dot(string field) => ((GameObject)mainType.GetField(field, fields).GetValue(main)).activeSelf;
            string id = "notice-acceptance-" + Guid.NewGuid().ToString("N");
            TitleNewsItem Post(string text) => new()
            {
                NewsId = id, Title = "알림 검증", Body = JsonConvert.SerializeObject(new { text }),
                Timestamp = new DateTime(2026, 11, 11, 0, 0, 0, DateTimeKind.Utc)
            };
            string articleKey = NoticePopupController.ArticleKey(TitleNewsFeed.Read(new List<TitleNewsItem> { Post(longText) })[0]);
            void Receive(string text)
            {
                int request = (int)Get("_requestId") + 1;
                Set("_requestId", request);
                Set("_deadline", Time.unscaledTime + 15f);
                Call("Finish", request, new GetTitleNewsResult { News = new List<TitleNewsItem> { Post(text) } });
            }
            try
            {
                uiInstance.SetValue(null, manager);
                typeof(UIManager).GetField("_mainController", fields).SetValue(manager, main);
                Set("_network", network);
                Set("_generation", network.AccountGeneration);
                Set("_nextRefreshAt", float.PositiveInfinity);
                MenuCall("BindMenuNotifications");
                Check(mainType.GetField("_noticeDot", fields).GetValue(main) != null,
                    "Notice notifications bind even without a mailbox service");
                Receive(longText);
                Check(NoticePopupController.HasUnread && Dot("_noticeDot") && Dot("_hamburgerDot") && !Dot("_mailDot"),
                    "An unseen article lights the notice and hamburger without changing mail");
                NoticePopupController.BeginMainEntry();
                NoticePopupController.Tick(manager);
                Check(!((NoticePopupView)Get("_board")).gameObject.activeSelf,
                    "Main entry never opens the in-game board automatically");

                var mailbox = new MailboxService((_, _, _) => { }, (_, _, _) => { }, _ => true,
                    done => done(true), () => (1L, 1L), () => true, () => 0d);
                typeof(MailboxService).GetField("_unseenMail", fields).SetValue(mailbox, ("notice-fixture", 1L, "", 0L));
                mainType.GetField("_mailbox", fields).SetValue(main, mailbox);
                var refresh = (Action)Delegate.CreateDelegate(typeof(Action), main, mainType.GetMethod("RefreshMenuNotifications", fields));
                mailbox.Changed += refresh;
                MenuCall("RefreshMenuNotifications");
                NoticePopupController.ShowBoard();
                Check(!NoticePopupController.HasUnread && !Dot("_noticeDot") && Dot("_mailDot") && Dot("_hamburgerDot"),
                    "Opening the board reads every fetched article while unread mail keeps the hamburger lit");
                var board = (NoticePopupView)Get("_board");
                Check(board.scroll.content.GetComponentsInChildren<NoticeRowView>().All(row => !row.body.gameObject.activeSelf),
                    "Reading the board requires no individual article expansion");
                mailbox.Open();
                Check(!Dot("_mailDot") && !Dot("_hamburgerDot"), "Opening mail clears only its own notification");

                var row = board.scroll.content.GetComponentsInChildren<NoticeRowView>()[0];
                row.toggle.onClick.Invoke();
                Layout(board);
                board.scroll.verticalNormalizedPosition = .3f;
                var scrollPosition = board.scroll.content.anchoredPosition;
                Receive(longText);
                Check(board.scroll.content.GetComponentsInChildren<NoticeRowView>()[0] == row && row.body.gameObject.activeSelf &&
                    board.scroll.content.anchoredPosition == scrollPosition,
                    "An identical refresh preserves row objects, expansion and scroll position");
                board.closeButton.onClick.Invoke();
                int request = (int)Get("_requestId") + 1;
                Set("_requestId", request);
                Set("_deadline", Time.unscaledTime + 15f);
                Call("Finish", request, null);
                Check(((List<TitleNewsFeed>)Get("_articles"))[0].Text == longText && !NoticePopupController.HasUnread,
                    "A failed refresh preserves the last feed and read receipt");

                Receive(longText + " 수정");
                Check(NoticePopupController.HasUnread && Dot("_noticeDot"), "Editing the same article raises a fresh notification");
                NoticePopupController.ShowBoard();
                Check(!NoticePopupController.HasUnread, "Opening the updated board reads the new fingerprint");
                request = (int)Get("_requestId") + 1;
                Set("_requestId", request);
                Set("_deadline", Time.unscaledTime + 15f);
                board.closeButton.onClick.Invoke();
                Call("Finish", request, new GetTitleNewsResult { News = new List<TitleNewsItem> { Post(longText + " 늦은 수정") } });
                Check(NoticePopupController.HasUnread, "A response arriving after dismissal remains unread");
                NoticePopupController.ShowBoard();
                Check(!NoticePopupController.HasUnread, "Reopening reads the late article");
                board.closeButton.onClick.Invoke();

                Set("_lastRequestAt", Time.unscaledTime);
                Set("_deadline", 0f);
                int before = (int)Get("_requestId");
                NoticePopupController.RefreshFromMenu();
                NoticePopupController.RefreshFromMenu();
                Check((int)Get("_requestId") == before, "Repeated menu opening respects the refresh cooldown");
                Set("_lastRequestAt", Time.unscaledTime - 31f);
                NoticePopupController.RefreshFromMenu();
                Check((int)Get("_requestId") == before + 1 && (float)Get("_nextRefreshAt") >= Time.unscaledTime + 299f,
                    "A failed menu request schedules a five-minute background cooldown");
                NoticePopupController.RefreshFromMenu();
                Check((int)Get("_requestId") == before + 1, "Failure does not create an immediate retry loop");
                Set("_nextRefreshAt", Time.unscaledTime - 1f);
                NoticePopupController.Tick(manager);
                Check((int)Get("_requestId") == before + 2, "A due background refresh runs through the same guarded request");

                int oldRequest = (int)Get("_requestId");
                Set("_deadline", Time.unscaledTime + 15f);
                typeof(NetworkManager).GetProperty(nameof(NetworkManager.AccountGeneration)).SetValue(network, network.AccountGeneration + 1);
                Call("Finish", oldRequest, new GetTitleNewsResult { News = new List<TitleNewsItem> { Post("다른 세대") } });
                Check(!NoticePopupController.HasUnread && ((List<TitleNewsFeed>)Get("_articles"))[0].Text == longText + " 늦은 수정",
                    "A response from a previous account generation cannot update notifications");
                NoticePopupController.Tick(manager);
                Set("_network", network);
                Set("_generation", network.AccountGeneration);
                Receive(longText + " 늦은 수정");
                Check(!NoticePopupController.HasUnread, "Device read receipts survive a session reset");
                main.Dispose();
                var observers = (Action)Controller.GetField("Changed", ControllerMembers).GetValue(null);
                Check(observers == null || observers.GetInvocationList().All(observer => observer.Target != main),
                    "Disposing the menu releases its notice subscription");
                Receive(longText + " 재게시");
                Check(NoticePopupController.HasUnread, "Disposed menu observers do not block a later feed update");
            }
            finally
            {
                main.Dispose();
                typeof(UIManager).GetField("_mainController", fields).SetValue(manager, null);
                uiInstance.SetValue(null, previousUi);
                PlayerPrefs.DeleteKey(articleKey);
                PlayerPrefs.Save();
                Object.DestroyImmediate(menuRoot);
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
            var well = view.panel.Find("BodyWell");
            var border = view.panel.Find("BronzeBorder");
            Check(well != null && well.GetComponent<Image>().color.a == 1f &&
                border != null && border.GetComponent<Image>().sprite != null &&
                view.panel.Find("HeaderDivider") != null && view.closeButton.GetComponentInChildren<TMPro.TMP_Text>().text == "닫기",
                "Both popups provide a visible frame, opaque body and explicit close label");
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
        private static object Get(string name) => Controller.GetField(name, ControllerMembers).GetValue(null);

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Notice popup acceptance: " + message);
        }
    }
}
