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
        static readonly string Output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "QA", "ShopValidation"));
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
            CheckPlay(main != null && main.btnHamburger != null && main.btnMenuNotice != null && main.tabStore != null,
                "Actual main prefab exposes the notice menu entry and bottom shop tab");
            var mail = main.btnMenuMail; var checkIn = main.btnMenuCheckIn;
            CheckPlay(mail != null && checkIn != null && mail != main.btnMenuNotice && checkIn != main.btnMenuNotice &&
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
            CheckPlay(main.popupHamburger.activeSelf && main.btnMenuNotice.gameObject.activeInHierarchy &&
                main.popupHamburger.transform.Find("BtnMenuShop") == null && main.popupHamburger.transform.Find("BtnLoopIcon") == null,
                "Hamburger shows notices and omits shop and repeat-hunt exit");
            main.btnHamburger.onClick.Invoke();
            main.tabStore.Button.onClick.Invoke();
            settle = Time.unscaledTime + .4f;
            while (Time.unscaledTime < settle) yield return null;
            var view = CurrentShop();
            CheckPlay(view != null && ShopPopupController.IsOpen && !main.popupHamburger.activeSelf,
                "Bottom shop tab opens the real shop after closing the dropdown");
            CheckPlay(host.HasActiveTabPanel && host.ActiveTabPanelId == KingdomIdle.UI.UIPanelId.Store &&
                view.transform.parent == host.LayerPanels && view.GetComponent<ModalBackHandler>() == null,
                "Shop participates in the common tab panel stack without a separate modal");
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
                    if (ShopCatalog.Find(row.offerId).Payment == ShopPayment.RewardedAd) continue;
                    row.action.onClick.Invoke(); clicked.Add(row.offerId);
                }
            }
            CheckPlay(clicked.Count == 9 && before == Newtonsoft.Json.JsonConvert.SerializeObject(LocalProgression.State),
                "Purchase and exchange previews leave the account unchanged; live ads are covered by service acceptance");
            var closing = view;
            view.closeButton.onClick.Invoke(); CheckPlay(!ShopPopupController.IsOpen, "Close input immediately detaches the shop");
            main.tabStore.Button.onClick.Invoke();
            view = CurrentShop();
            CheckPlay(view != closing && ShopPopupController.IsOpen, "Bottom shop tab creates a fresh panel while the old one exits");
            settle = Time.unscaledTime + .4f;
            while (Time.unscaledTime < settle) yield return null;
            CheckPlay(closing == null && ShopPopupController.IsOpen && CurrentShop() == view &&
                Object.FindObjectsByType<ShopPopupView>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 1,
                "Delayed destruction of the old shop does not detach the new shop");
            view.tabs[1].onClick.Invoke();
            host.PushPanel(KingdomIdle.UI.UIPanelId.Store);
            CheckPlay(!view.gameObject.activeInHierarchy && CurrentShop() != view, "A stacked shop takes ownership while its predecessor is hidden");
            ShopPopupController.Hide();
            CheckPlay(ShopPopupController.IsOpen && CurrentShop() == view && view.pages[1].activeSelf,
                "Closing a nested shop restores the original bindings and selected category");
            host.RequestBack(); CheckPlay(!ShopPopupController.IsOpen, "Common Android-back route dismisses the shop");
            main.tabStore.Button.onClick.Invoke();
            CurrentShop().backdrop.onClick.Invoke();
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
                    foreach (var button in new[] { main.btnMenuNotice, mail, checkIn })
                        RequireInside((RectTransform)testUi.transform, (RectTransform)button.transform, "Menu entry " + button.name + " at " + size);
                    surface.Save("menu-" + size.x + "x" + size.y);
                    main.btnHamburger.onClick.Invoke();
                    main.tabStore.Button.onClick.Invoke();
                    settle = Time.unscaledTime + .4f;
                    while (Time.unscaledTime < settle) yield return null;
                    view = CurrentShop();
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
                    view.closeButton.onClick.Invoke();
                }
            }
            CheckPlay(main.btnMenuMail == mail && main.btnMenuCheckIn == checkIn && mail.gameObject.activeSelf && checkIn.gameObject.activeSelf,
                "Mail and check-in references and visibility survive all shop interactions");
            CheckPlay(before == Newtonsoft.Json.JsonConvert.SerializeObject(LocalProgression.State),
                "Menu navigation, preview actions and capture leave the complete isolated account unchanged");
            CheckPlay(PlayErrors.Count == 0, "No runtime errors, assertions or exceptions were observed");
        }

        static ShopPopupView CurrentShop() => Object.FindObjectsByType<ShopPopupView>(FindObjectsSortMode.None)
            .Single(view => UIManager.Instance.IsTopPanel(view));

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
            RequireInside((RectTransform)view.transform, view.sheet, "Shop sheet stays within the UI area at " + size);
            var viewport = RectBounds(view.sheet, view.scroll.viewport);
            var tabs = RectBounds(view.sheet, (RectTransform)view.sheet.Find("Tabs"));
            var footer = RectBounds(view.sheet, (RectTransform)view.sheet.Find("PriceNotice"));
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

        [MenuItem("KingdomIdle/UGUI/Apply shop panel layout")]
        public static void Rebuild()
        {
            var catalog = PrefabGenUtil.GetOrCreateCatalog();
            catalog.popupShop = Generate();
            EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets();
            Validate();
        }

        internal static GameObject Generate()
        {
            ApplyPanelLayout();
            return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        }

        public static void ApplyPanelLayout()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var view = root.GetComponent<ShopPopupView>();
                if (view == null) throw new InvalidOperationException("Shop view is missing.");
                view.sheet = view.sheet != null ? view.sheet : root.transform.Find("Panel") as RectTransform;
                if (view.sheet == null) throw new InvalidOperationException("Existing shop content panel is missing.");
                view.title = view.sheet.Find("Header/Title")?.GetComponent<TMP_Text>();
                view.closeButton = view.sheet.Find("Header/BtnClose")?.GetComponent<Button>();
                if (view.title == null || view.closeButton == null)
                    throw new InvalidOperationException("Existing shop header wiring is missing.");
                view.SetTitle("상점");
                PanelGens.ConfigureSheet(view);
                view.sheet.Find("PreviewNotice").GetComponent<TMP_Text>().text = "구매 · 골드 교환 준비 중";
                view.sheet.Find("PriceNotice").GetComponent<TMP_Text>().text =
                    "표시 가격은 개발 예정가입니다.\n판매 시 스토어 가격을 확인해 주세요.";
                view.pages[(int)ShopCategory.Rewarded].transform.Find("DailyHint").GetComponent<TMP_Text>().text =
                    "장비 · 마탑 각각 광고 1회에 뽑기권 2장\n각각 하루 1회 · 한국 시간 자정 초기화";
                root.SetActive(true);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        public static void Validate()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<UIViewCatalog>(PrefabGenUtil.CatalogPath);
            var shop = catalog.popupShop.GetComponent<ShopPopupView>();
            if (!shop.gameObject.activeSelf || shop.sheet == null || shop.closeButton == null || shop.backdrop == null ||
                shop.sheet.parent.GetComponent<RectMask2D>() == null || shop.GetComponent<ModalBackHandler>() != null)
                throw new InvalidOperationException("Shop is not wired to the common panel shell.");
            if (shop.offers.Length != ShopCatalog.Offers.Count || shop.tabs.Length != 3 || shop.pages.Length != 3)
                throw new InvalidOperationException("Shop offer/category wiring mismatch.");
            if (shop.offers.Any(x => ShopCatalog.Find(x.offerId) == null || x.action == null || x.price == null) || shop.offers.Select(x => x.offerId).Distinct().Count() != shop.offers.Length)
                throw new InvalidOperationException("Shop has missing/duplicate IDs or buttons.");
            var main = catalog.screenMain.GetComponent<MainScreenView>();
            if (main.tabStore == null || main.popupHamburger == null || main.btnMenuNotice == null || main.btnMenuMail == null || main.btnMenuCheckIn == null ||
                main.btnMenuNotice == main.btnMenuMail || main.btnMenuNotice == main.btnMenuCheckIn ||
                main.popupHamburger.transform.Find("BtnMenuShop") != null || main.popupHamburger.transform.Find("BtnLoopIcon") != null)
                throw new InvalidOperationException("Missing bottom shop tab or incorrect hamburger menu wiring.");
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
