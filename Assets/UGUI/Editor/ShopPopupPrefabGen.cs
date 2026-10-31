using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KingdomIdle.Balance;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace KingdomIdle.UGUI.Editor
{
    [InitializeOnLoad]
    public static class ShopPopupPrefabGen
    {
        public const string PrefabPath = "Assets/UGUI/Prefabs/Popups/Popup_Shop.prefab";
        const string MainPath = "Assets/UGUI/Prefabs/Screens/Screen_Main.prefab";
        const string Output = "Recordings/ShopDevelopMerge20261028";
        const string PlayCheck = "ShopPopup.DevelopMergeValidation";
        static readonly List<string> PlayChecks = new(), PlayErrors = new(), PlayWarnings = new();
        static IEnumerator playRoutine;
        static IDisposable testSession;
        static GameObject testUi;
        static bool finishing;

        static ShopPopupPrefabGen()
        {
            EditorApplication.playModeStateChanged += OnPlayValidationState;
            EditorApplication.update += TickPlayValidation;
        }

        [MenuItem("KingdomIdle/UGUI/Validate shop in isolated Play Mode")]
        public static void ValidatePlayMode()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || LocalProgression.IsReady || UIManager.Instance != null)
                throw new InvalidOperationException("Use an empty editor session without an active account for shop validation.");
            RequireSavedEditorScenes();
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);
            Directory.CreateDirectory(Output);
            finishing = false;
            SessionState.SetBool(PlayCheck, true);
            SessionState.SetBool(PlayCheck + ".Failed", false);
            SessionState.SetBool(PlayCheck + ".Completed", false);
            SessionState.SetFloat(PlayCheck + ".Deadline", (float)EditorApplication.timeSinceStartup + 120);
            EditorApplication.isPlaying = true;
        }

        internal static void RequireSavedEditorScenes()
        {
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save the open editor scenes before running isolated validation; unsaved work will not be replaced.");
        }

        static void OnPlayValidationState(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(PlayCheck, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                PlayChecks.Clear(); PlayErrors.Clear(); PlayWarnings.Clear(); finishing = false;
                Application.logMessageReceived += CollectPlayLog;
                playRoutine = ExerciseRuntime();
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                Application.logMessageReceived -= CollectPlayLog;
                if (!SessionState.GetBool(PlayCheck + ".Completed", false))
                {
                    SessionState.SetBool(PlayCheck + ".Failed", true);
                    File.WriteAllText(Output + "/playmode-failure.txt", "Play Mode stopped before shop validation completed.");
                }
                SessionState.SetBool(PlayCheck, false);
                if (Application.isBatchMode) EditorApplication.Exit(SessionState.GetBool(PlayCheck + ".Failed", false) ? 1 : 0);
            }
        }

        static void CollectPlayLog(string message, string trace, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Assert || type == LogType.Error)
                PlayErrors.Add(message + "\n" + trace);
            else if (type == LogType.Warning && !PlayWarnings.Contains(message)) PlayWarnings.Add(message);
        }

        static void TickPlayValidation()
        {
            if (!SessionState.GetBool(PlayCheck, false) || finishing) return;
            try
            {
                if (EditorApplication.timeSinceStartup > SessionState.GetFloat(PlayCheck + ".Deadline", 0))
                    throw new TimeoutException("Shop Play Mode validation exceeded 120 seconds.");
                if (playRoutine == null) return;
                if (playRoutine.MoveNext()) return;
                FinishPlayValidation();
            }
            catch (Exception exception)
            {
                PlayErrors.Add(exception.ToString());
                FinishPlayValidation();
            }
        }

        static void FinishPlayValidation()
        {
            finishing = true;
            try { (playRoutine as IDisposable)?.Dispose(); }
            catch (Exception exception) { PlayErrors.Add(exception.ToString()); }
            playRoutine = null;
            try
            {
                ShopPopupController.Hide();
                UIManager.Instance?.ActiveMain?.Dispose();
                if (testUi != null) Object.DestroyImmediate(testUi);
            }
            catch (Exception exception) { PlayErrors.Add(exception.ToString()); }
            finally { testUi = null; }
            try { testSession?.Dispose(); }
            catch (Exception exception) { PlayErrors.Add(exception.ToString()); }
            finally { testSession = null; }
            Application.logMessageReceived -= CollectPlayLog;
            bool failed = PlayErrors.Count > 0;
            SessionState.SetBool(PlayCheck + ".Failed", failed);
            SessionState.SetBool(PlayCheck + ".Completed", true);
            try
            {
                Directory.CreateDirectory(Output);
                File.WriteAllText(Output + "/playmode.json", Newtonsoft.Json.JsonConvert.SerializeObject(new
                {
                    unity = Application.unityVersion, version = PlayerSettings.bundleVersion,
                    actualPlayMode = true, serializedMainMenuEntry = true, liveBattle = false,
                    scope = "Actual UGUI root/main/shop prefabs, isolated local fixture; no authentication, server transactions or live battle. Ratios are Editor renders with a full safe area.",
                    checks = PlayChecks, errors = PlayErrors, warnings = PlayWarnings
                }, Newtonsoft.Json.Formatting.Indented));
                File.WriteAllText(Output + (failed ? "/playmode-failure.txt" : "/playmode.txt"),
                    string.Join("\n", failed ? PlayErrors : PlayChecks));
            }
            catch (Exception exception)
            {
                failed = true; SessionState.SetBool(PlayCheck + ".Failed", true);
                PlayErrors.Add("Could not write shop validation evidence: " + exception);
            }
            if (failed) Debug.LogError("SHOP PLAY MODE FAILED: " + string.Join("\n", PlayErrors));
            else Debug.Log("SHOP PLAY MODE PASSED " + PlayChecks.Count);
            if (EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.isPlaying = false;
            else
            {
                SessionState.SetBool(PlayCheck, false);
                if (Application.isBatchMode) EditorApplication.Exit(failed ? 1 : 0);
            }
        }

        static void CheckPlay(bool ok, string text)
        {
            if (!ok) throw new InvalidOperationException(text);
            PlayChecks.Add(text);
        }

        static IEnumerator ExerciseRuntime()
        {
            if (LocalProgression.IsReady || UIManager.Instance != null)
                throw new InvalidOperationException("A user account or UI already exists; refusing to replace it.");
            testSession = LocalProgression.BeginTestSession();
            LocalProgression.OpenTestAccount("shop-ui-" + Guid.NewGuid().ToString("N"));
            CheckPlay(LocalProgression.Execute("shop-ui-fixture", state =>
            {
                state.OfflineStage = 0x200010001; state.OfflineKpm = 3.25m; state.RubyGoldLevel = 1;
                state.Wallet[eCurrency.AncientCoin] = 1500; return true;
            }), "Isolated shop fixture is committed without a production login");
            testUi = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UGUI/Prefabs/UGUI_UIRoot.prefab"));
            var host = UIManager.Instance;
            CheckPlay(host != null, "Serialized UGUI root creates its real UI manager");
            // The main shell binds its real menu controller; no bootstrap, server or battle manager is created.
            host.ReplaceScreen(KingdomIdle.UI.UIScreenId.Main);
            var main = testUi.GetComponentInChildren<MainScreenView>(true);
            CheckPlay(main != null && main.btnHamburger != null && main.btnMenuShop != null,
                "Actual main prefab exposes the hamburger and shop entry");
            var mail = main.btnMenuMail; var checkIn = main.btnMenuCheckIn;
            CheckPlay(mail != null && checkIn != null && mail != main.btnMenuShop && checkIn != main.btnMenuShop &&
                mail.transform.IsChildOf(main.popupHamburger.transform) && checkIn.transform.IsChildOf(main.popupHamburger.transform),
                "Current mail and check-in entries remain separate buttons in the main menu");
            foreach (var fitter in testUi.GetComponentsInChildren<SafeAreaFitter>(true))
            {
                fitter.enabled = false;
                var rect = (RectTransform)fitter.transform;
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
            }
            string before = Newtonsoft.Json.JsonConvert.SerializeObject(LocalProgression.State);
            main.btnHamburger.onClick.Invoke();
            float settle = Time.unscaledTime + .3f;
            while (Time.unscaledTime < settle) yield return null;
            CheckPlay(main.popupHamburger.activeSelf && main.btnMenuShop.gameObject.activeInHierarchy,
                "Hamburger input exposes the serialized shop button");
            main.btnMenuShop.onClick.Invoke();
            settle = Time.unscaledTime + .4f;
            while (Time.unscaledTime < settle) yield return null;
            var view = Object.FindFirstObjectByType<ShopPopupView>();
            CheckPlay(view != null && ShopPopupController.IsOpen && !main.popupHamburger.activeSelf,
                "Main-menu shop input opens the real shop and dismisses the dropdown");
            CheckPlay(view.offers.Length == 11 && view.tabs.Length == 3 && view.pages.Length == 3,
                "Runtime shop contains all eleven offers and three tabs");
            var clicked = new HashSet<string>();
            for (int tab = 0; tab < 3; tab++)
            {
                view.tabs[tab].onClick.Invoke();
                CheckPlay(view.pages.Select(x => x.activeSelf).SequenceEqual(Enumerable.Range(0, 3).Select(x => x == tab)),
                    "Tab " + tab + " has exactly one active page");
                foreach (var row in view.offers.Where(x => x.gameObject.activeInHierarchy))
                {
                    row.action.onClick.Invoke(); clicked.Add(row.offerId);
                }
            }
            CheckPlay(clicked.Count == 11 && before == Newtonsoft.Json.JsonConvert.SerializeObject(LocalProgression.State),
                "Every real offer button is exercised without changing any account state");
            view.close.onClick.Invoke(); CheckPlay(!ShopPopupController.IsOpen, "Close input dismisses the shop");
            OpenFromMenu(main);
            CheckPlay(Object.FindObjectsByType<ShopPopupView>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 1,
                "Reopening through the main menu reuses one shop instance");
            host.RequestBack(); CheckPlay(!ShopPopupController.IsOpen, "Common Android-back route dismisses the shop");
            OpenFromMenu(main); view.backdrop.onClick.Invoke();
            CheckPlay(!ShopPopupController.IsOpen, "Outside input dismisses the shop");
            settle = Time.unscaledTime + 4;
            while (Time.unscaledTime < settle) yield return null;
            foreach (var size in new[] { new Vector2Int(1080, 1920), new Vector2Int(720, 1600), new Vector2Int(1200, 1600) })
            {
                using (var surface = new ShopCaptureSurface(testUi, size.x, size.y))
                {
                    main.btnHamburger.onClick.Invoke();
                    settle = Time.unscaledTime + .3f;
                    while (Time.unscaledTime < settle) yield return null;
                    Canvas.ForceUpdateCanvases();
                    foreach (var button in new[] { main.btnMenuShop, mail, checkIn })
                        RequireInside((RectTransform)testUi.transform, (RectTransform)button.transform, "Menu entry " + button.name + " at " + size);
                    surface.Save("menu-" + size.x + "x" + size.y);
                    main.btnMenuShop.onClick.Invoke();
                    settle = Time.unscaledTime + .4f;
                    while (Time.unscaledTime < settle) yield return null;
                    for (int tab = 0; tab < 3; tab++)
                    {
                        view.tabs[tab].onClick.Invoke();
                        yield return null; yield return null;
                        Canvas.ForceUpdateCanvases();
                        ValidateShopLayout(view, size, tab);
                        surface.Save("shop-" + size.x + "x" + size.y + "-tab" + tab);
                        view.scroll.StopMovement(); view.scroll.verticalNormalizedPosition = 0;
                        Canvas.ForceUpdateCanvases();
                        yield return null;
                        Canvas.ForceUpdateCanvases();
                        var last = view.offers.Last(x => x.gameObject.activeInHierarchy);
                        RequireInside(view.scroll.viewport, (RectTransform)last.action.transform,
                            "Final offer action reaches the viewport for tab " + tab + " at " + size);
                        surface.Save("shop-" + size.x + "x" + size.y + "-tab" + tab + "-bottom");
                    }
                    view.close.onClick.Invoke();
                }
            }
            CheckPlay(main.btnMenuMail == mail && main.btnMenuCheckIn == checkIn && mail.gameObject.activeSelf && checkIn.gameObject.activeSelf,
                "Mail and check-in references and visibility survive all shop interactions");
            CheckPlay(before == Newtonsoft.Json.JsonConvert.SerializeObject(LocalProgression.State),
                "Menu navigation, preview actions and capture leave the complete isolated account unchanged");
            CheckPlay(PlayErrors.Count == 0, "No runtime errors, assertions or exceptions were observed");
        }

        static void OpenFromMenu(MainScreenView main)
        {
            main.btnHamburger.onClick.Invoke();
            main.btnMenuShop.onClick.Invoke();
        }

        static Bounds RectBounds(RectTransform relativeTo, RectTransform target)
        {
            var corners = new Vector3[4]; target.GetWorldCorners(corners);
            var bounds = new Bounds(relativeTo.InverseTransformPoint(corners[0]), Vector3.zero);
            for (int i = 1; i < corners.Length; i++) bounds.Encapsulate(relativeTo.InverseTransformPoint(corners[i]));
            return bounds;
        }

        static void RequireInside(RectTransform parent, RectTransform child, string message)
        {
            var bounds = RectBounds(parent, child); var area = parent.rect;
            CheckPlay(bounds.size.x > 0 && bounds.size.y > 0 && bounds.min.x >= area.xMin - 2 && bounds.max.x <= area.xMax + 2 &&
                bounds.min.y >= area.yMin - 2 && bounds.max.y <= area.yMax + 2, message);
        }

        static void ValidateShopLayout(ShopPopupView view, Vector2Int size, int tab)
        {
            RequireInside((RectTransform)view.transform, view.panel, "Popup stays within the UI area at " + size);
            var viewport = RectBounds(view.panel, view.scroll.viewport);
            var tabs = RectBounds(view.panel, (RectTransform)view.panel.Find("Tabs"));
            var footer = RectBounds(view.panel, (RectTransform)view.panel.Find("PriceNotice"));
            CheckPlay(viewport.size.y > 100 && viewport.max.y <= tabs.min.y + 2 && viewport.min.y >= footer.max.y - 2,
                "Scrollable offers avoid fixed tabs and footer for tab " + tab + " at " + size);
            var rows = view.offers.Where(x => x.gameObject.activeInHierarchy).ToArray();
            var page = (RectTransform)view.pages[tab].transform;
            for (int i = 0; i < rows.Length; i++)
            {
                var row = rows[i];
                var action = RectBounds((RectTransform)row.transform, (RectTransform)row.action.transform);
                var amount = RectBounds((RectTransform)row.transform, row.amount.rectTransform);
                CheckPlay(amount.max.x <= action.min.x + 2 && action.size.y >= 80,
                    "Amount and action remain distinct touch regions: " + row.offerId + " at " + size);
                if (i > 0)
                    CheckPlay(RectBounds(page, (RectTransform)rows[i - 1].transform).min.y >= RectBounds(page, (RectTransform)row.transform).max.y - 2,
                        "Adjacent offer cards do not overlap: " + row.offerId + " at " + size);
            }
            RequireInside(view.scroll.viewport, (RectTransform)rows[0].action.transform,
                "First offer action is reachable for tab " + tab + " at " + size);
        }

        sealed class ShopCaptureSurface : IDisposable
        {
            readonly Canvas canvas;
            readonly CanvasScaler scaler;
            readonly bool scalerEnabled;
            readonly RenderMode mode;
            readonly Camera previousCamera;
            readonly float scale, plane;
            readonly Camera camera;
            readonly RenderTexture texture;

            public ShopCaptureSurface(GameObject ui, int width, int height)
            {
                canvas = ui.GetComponent<Canvas>(); scaler = ui.GetComponent<CanvasScaler>();
                mode = canvas.renderMode; previousCamera = canvas.worldCamera; scale = canvas.scaleFactor; plane = canvas.planeDistance;
                scalerEnabled = scaler != null && scaler.enabled;
                if (scaler != null) scaler.enabled = false;
                texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32); texture.Create();
                camera = new GameObject("Shop isolated UI capture camera").AddComponent<Camera>();
                camera.enabled = false; camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = UguiTheme.RusticPanelDeep; camera.cullingMask = 1 << ui.layer;
                camera.nearClipPlane = .1f; camera.farClipPlane = 10; camera.targetTexture = texture;
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
                // Match the real prefab's width-based 1080px scaler, using this render target rather than the host Game view size.
                canvas.scaleFactor = width / 1080f;
                Canvas.ForceUpdateCanvases();
            }

            public void Save(string name)
            {
                Canvas.ForceUpdateCanvases(); camera.Render();
                var previous = RenderTexture.active; Texture2D image = null;
                try
                {
                    RenderTexture.active = texture;
                    image = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
                    image.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0); image.Apply();
                    File.WriteAllBytes(Output + "/" + name + ".png", image.EncodeToPNG());
                }
                finally
                {
                    RenderTexture.active = previous;
                    if (image != null) Object.DestroyImmediate(image);
                }
            }

            public void Dispose()
            {
                canvas.renderMode = mode; canvas.worldCamera = previousCamera; canvas.planeDistance = plane; canvas.scaleFactor = scale;
                if (scaler != null) scaler.enabled = scalerEnabled;
                camera.targetTexture = null; texture.Release();
                Object.DestroyImmediate(texture); Object.DestroyImmediate(camera.gameObject);
                Canvas.ForceUpdateCanvases();
            }
        }

        [MenuItem("KingdomIdle/UGUI/Rebuild shop")]
        public static void Rebuild()
        {
            F.Init(); F.Catalog = PrefabGenUtil.GetOrCreateCatalog();
            F.Catalog.popupShop = Generate();
            var main = PrefabUtility.LoadPrefabContents(MainPath);
            try { WireMain(main.GetComponent<MainScreenView>()); PrefabUtility.SaveAsPrefabAsset(main, MainPath); }
            finally { PrefabUtility.UnloadPrefabContents(main); }
            EditorUtility.SetDirty(F.Catalog); AssetDatabase.SaveAssets();
            Validate();
        }

        internal static void WireMain(MainScreenView main)
        {
            if (main.btnMenuShop == null)
            {
                var copy = Object.Instantiate(main.btnMenuInventory.gameObject, main.popupHamburgerRect, false);
                copy.name = "BtnMenuShop"; main.btnMenuShop = copy.GetComponent<Button>();
            }
            main.btnMenuShop.gameObject.SetActive(true);
            main.btnMenuShop.transform.SetSiblingIndex(main.btnMenuInventory.transform.GetSiblingIndex() + 1);
            var label = main.btnMenuShop.transform.Find("MenuLabel")?.GetComponent<TMP_Text>();
            if (label != null) label.text = "상점";
            var icon = main.btnMenuShop.transform.Find("Icon")?.GetComponent<Image>();
            if (icon != null) icon.sprite = AssetDatabase.LoadAssetAtPath<UIViewCatalog>(PrefabGenUtil.CatalogPath).iconAncientCoin;
        }

        internal static GameObject Generate()
        {
            var root = F.Root("Popup_Shop"); F.Stretch(root);
            var view = root.gameObject.AddComponent<ShopPopupView>();
            var dim = F.Box(root, "Dim", UguiTheme.DimHeavy, false, true); F.Stretch(dim.rectTransform);
            view.backdrop = dim.gameObject.AddComponent<Button>(); view.backdrop.targetGraphic = dim;
            view.backdrop.transition = Selectable.Transition.None;
            var panel = F.PixelPanel(root, "Panel", F.Catalog.kitWindow, F.FrameGold, 20, raycast: true, baseColor: UguiTheme.RusticPanelDeep);
            F.AnchorCenter(panel.rectTransform, 980, 1460); view.panel = panel.rectTransform;
            F.VLayout(panel.gameObject, 14, new RectOffset(30, 30, 24, 26));
            F.CornerBrackets(panel.transform);

            var header = F.Container(panel.transform, "Header"); F.HLayout(header.gameObject, 12, null, TextAnchor.MiddleLeft);
            F.Preferred(header, height: 100);
            var title = F.Text(header, "Title", "왕국 보급 상점", 40, UguiTheme.Parchment, bold: true); F.Flexible(title, flexWidth: 1);
            view.close = F.TextButton(header, "BtnClose", "닫기", 28, UguiTheme.RusticSurface, out _);
            F.Preferred(view.close.transform as RectTransform, width: 140, height: 96);
            Label(panel.transform, "PreviewNotice", "출시 준비 중 · 실제 결제 / 광고 없음", 25, 42, UguiTheme.AccentGold);
            view.wallet = Label(panel.transform, "Wallet", "보유 고대주화  0", 28, 46, UguiTheme.Parchment);

            var tabs = F.Container(panel.transform, "Tabs"); F.HLayout(tabs.gameObject, 10, null, TextAnchor.MiddleCenter, expandWidth: true); F.Preferred(tabs, height: 98);
            view.tabs = new Button[3]; string[] names = { "추천", "주화 / 골드", "매일 광고" };
            for (int i = 0; i < names.Length; i++)
            {
                view.tabs[i] = F.TextButton(tabs, "Tab" + i, names[i], 30, UguiTheme.RusticSurfaceDark, out _);
                F.Flexible(view.tabs[i].transform as RectTransform, flexWidth: 1); F.Preferred(view.tabs[i].transform as RectTransform, height: 98);
            }

            view.scroll = F.VScroll(panel.transform, "Offers", out var content, 18, new RectOffset(0, 8, 0, 12));
            F.Flexible(view.scroll.transform as RectTransform, flexHeight: 1);
            F.Preferred(view.scroll.transform as RectTransform, height: 260);
            view.pages = new GameObject[3]; var cards = new List<ShopOfferView>();
            for (int i = 0; i < 3; i++)
            {
                var page = F.Container(content, "Page" + i); F.VLayout(page.gameObject, 18);
                view.pages[i] = page.gameObject;
                if (i == (int)ShopCategory.Currency)
                {
                    view.income = Label(page, "Income", "골드 기준 수입  0 / 분 · 루비 효과 포함", 24, 64, UguiTheme.TextSecondary);
                    Label(page, "GoldHint", "골드는 안전 클리어 웨이브의 수입에 따라 달라집니다.", 23, 64, UguiTheme.TextSecondary);
                }
                if (i == (int)ShopCategory.Rewarded)
                    Label(page, "DailyHint", "각 항목 하루 1회 · 오전 0시(한국 시간) 초기화\n광고 제거 보유 시 영상 없이 수령 · 횟수는 동일", 24, 94, UguiTheme.TextSecondary);
                foreach (var offer in ShopCatalog.Offers.Where(x => (int)x.Category == i)) cards.Add(Card(page, offer));
                page.gameObject.SetActive(i == 0);
            }
            view.offers = cards.ToArray();
            Label(panel.transform, "PriceNotice", "표시 가격은 개발 예정가입니다. 판매 시 스토어 가격을 확인해 주세요.\n현재 구매 · 교환 · 광고 시청으로 재화가 변하지 않습니다.", 23, 96, UguiTheme.TextSecondary);
            root.gameObject.SetActive(false);
            return PrefabGenUtil.SavePrefab(root.gameObject, PrefabPath);
        }

        static ShopOfferView Card(Transform parent, ShopOffer offer)
        {
            var card = F.Box(parent, offer.Id, UguiTheme.RusticSurfaceDark, true, false);
            var view = card.gameObject.AddComponent<ShopOfferView>(); view.offerId = offer.Id;
            F.VLayout(card.gameObject, 10, new RectOffset(24, 24, 20, 20));
            F.Preferred(card, height: offer.EquipmentTickets > 0 && offer.MageTickets > 0 || offer.PermanentAdRemoval ? 320 : 282);
            var heading = F.Container(card.transform, "Heading"); F.HLayout(heading.gameObject, 16, null, TextAnchor.MiddleLeft); F.Preferred(heading, height: 60);
            Sprite sprite = offer.GoldMinutes > 0 ? F.Catalog.iconCoin : offer.Coins > 0 ? F.Catalog.iconAncientCoin : F.Catalog.iconChest;
            var icon = F.IconImage(heading, "Icon", sprite, 52, 52); F.Preferred(icon, width: 52, height: 52);
            view.title = F.Text(heading, "Title", offer.Title, 32, UguiTheme.Parchment, bold: true); F.Flexible(view.title, flexWidth: 1);
            view.detail = Label(card.transform, "Detail", offer.Detail, 25, offer.PermanentAdRemoval || offer.EquipmentTickets > 0 && offer.MageTickets > 0 ? 94 : 60, UguiTheme.TextSecondary);
            var bottom = F.Container(card.transform, "Bottom"); F.HLayout(bottom.gameObject, 12, null, TextAnchor.MiddleLeft); F.Preferred(bottom, height: 96);
            string amount = offer.GoldMinutes > 0 ? "골드 수입 계산 중" : offer.DailyLimit > 0 ? "매일 1회 · 준비 중" : offer.PermanentAdRemoval ? "영구 이용" : "상품 미리보기";
            view.amount = F.Text(bottom, "Amount", amount, 25, UguiTheme.AccentGold, TextAlignmentOptions.Left, wrap: true); F.Flexible(view.amount, flexWidth: 1);
            string price = offer.Payment == ShopPayment.Store ? $"{offer.PreviewWon:N0}원" : offer.Payment == ShopPayment.AncientCoin ? $"주화 {offer.AncientCoinCost:N0}개" : "광고 보고 받기";
            view.action = F.TextButton(bottom, "BtnAction", price, 28, UguiTheme.RusticSurface, out var priceLabel);
            view.price = priceLabel;
            F.Preferred(view.action.transform as RectTransform, width: 280, height: 96);
            return view;
        }

        static TMP_Text Label(Transform parent, string name, string text, float size, float height, Color color)
        {
            var label = F.Text(parent, name, text, size, color, TextAlignmentOptions.Left, wrap: true);
            F.Preferred(label, height: height); return label;
        }

        public static void Validate()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<UIViewCatalog>(PrefabGenUtil.CatalogPath);
            var shop = catalog.popupShop.GetComponent<ShopPopupView>();
            if (shop.offers.Length != ShopCatalog.Offers.Count || shop.tabs.Length != 3 || shop.pages.Length != 3)
                throw new InvalidOperationException("Shop offer/category wiring mismatch.");
            if (shop.offers.Any(x => ShopCatalog.Find(x.offerId) == null || x.action == null || x.price == null) || shop.offers.Select(x => x.offerId).Distinct().Count() != shop.offers.Length)
                throw new InvalidOperationException("Shop has missing/duplicate IDs or buttons.");
            var main = catalog.screenMain.GetComponent<MainScreenView>();
            if (main.btnMenuShop == null || main.btnMenuMail == null || main.btnMenuCheckIn == null ||
                main.btnMenuShop == main.btnMenuMail || main.btnMenuShop == main.btnMenuCheckIn)
                throw new InvalidOperationException("Missing shop entry or preserved mail/check-in menu reference.");
            int references = 0;
            foreach (var asset in new[] { catalog.popupShop, catalog.screenMain })
                foreach (var component in asset.GetComponentsInChildren<Component>(true))
                {
                    if (component == null) throw new InvalidOperationException("Missing script in " + asset.name);
                    var property = new SerializedObject(component).GetIterator();
                    while (property.NextVisible(true)) if (property.propertyType == SerializedPropertyType.ObjectReference)
                    {
                        references++;
                        if (property.objectReferenceValue == null && property.objectReferenceInstanceIDValue != 0) throw new InvalidOperationException("Missing reference: " + asset.name + "/" + property.propertyPath);
                    }
                }
            var report = ShopAcceptance.Run();
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "/validation.txt", "Unity=" + Application.unityVersion + " version=" + PlayerSettings.bundleVersion + "\nreferences=" + references + " missing=0\n" + string.Join("\n", report));
            Debug.Log("SHOP VALIDATION PASSED " + report.Count + " checks, " + references + " references");
        }

        public static void BuildDevice() { Rebuild(); TitleLobbyDeviceBuild.BuildForPlayerJourney(); }
        public static void BuildManual() { Validate(); TitleLobbyDeviceBuild.BuildForManualTesting(); }
    }
}
