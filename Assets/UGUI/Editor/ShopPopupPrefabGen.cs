using System;
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
        const string PlayCheck = "ShopPopup.PlayValidation";

        static ShopPopupPrefabGen() => EditorApplication.playModeStateChanged += OnPlayValidationState;

        [MenuItem("KingdomIdle/UGUI/Validate shop in isolated Play Mode")]
        public static void ValidatePlayMode()
        {
            if (EditorApplication.isPlaying || UIManager.Instance != null) throw new InvalidOperationException("Use an empty editor session for shop validation.");
            RequireSavedEditorScenes();
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);
            SessionState.SetBool(PlayCheck, true); SessionState.SetBool(PlayCheck + ".Failed", false);
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
            if (state == PlayModeStateChange.EnteredPlayMode) EditorApplication.delayCall += ExerciseRuntime;
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(PlayCheck, false);
                if (Application.isBatchMode) EditorApplication.Exit(SessionState.GetBool(PlayCheck + ".Failed", false) ? 1 : 0);
            }
        }

        static void ExerciseRuntime()
        {
            var checks = new List<string>(); GameObject ui = null;
            void Check(bool ok, string text) { if (!ok) throw new InvalidOperationException(text); checks.Add(text); }
            try
            {
                if (LocalProgression.IsReady || UIManager.Instance != null) throw new InvalidOperationException("A user account or UI already exists; refusing to replace it.");
                using (LocalProgression.BeginTestSession())
                {
                    LocalProgression.OpenTestAccount("shop-ui-" + Guid.NewGuid().ToString("N"));
                    ui = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UGUI/Prefabs/UGUI_UIRoot.prefab"));
                    var host = UIManager.Instance;
                    ShopPopupController.Show();
                    var view = Object.FindFirstObjectByType<ShopPopupView>();
                    Check(view != null && ShopPopupController.IsOpen, "Actual controller opens the serialized shop prefab in Play Mode");
                    string before = Newtonsoft.Json.JsonConvert.SerializeObject(LocalProgression.State);
                    for (int i = 0; i < 3; i++)
                    {
                        view.tabs[i].onClick.Invoke();
                        Check(view.pages.Select(x => x.activeSelf).SequenceEqual(Enumerable.Range(0, 3).Select(x => x == i)), "Tab " + i + " has exactly one active page");
                        foreach (var row in view.offers.Where(x => x.gameObject.activeInHierarchy)) row.action.onClick.Invoke();
                    }
                    Check(before == Newtonsoft.Json.JsonConvert.SerializeObject(LocalProgression.State), "Real prefab purchase/ad buttons leave the complete account unchanged");
                    view.close.onClick.Invoke(); Check(!ShopPopupController.IsOpen, "Close button dismisses the popup");
                    ShopPopupController.Show(); Check(Object.FindObjectsByType<ShopPopupView>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 1, "Reopening reuses one shop instance");
                    host.RequestBack(); Check(!ShopPopupController.IsOpen, "Common Android-back route closes the shop");
                    ShopPopupController.Show(); view.backdrop.onClick.Invoke(); Check(!ShopPopupController.IsOpen, "Outside touch closes the shop");
                    var plan = KingdomIdle.OfflineRewards.OfflineRewardCalculator.CreatePlan(TimeSpan.FromHours(8), 0x200010001, 30);
                    OfflineRewardPopupController.Show(new KingdomIdle.OfflineRewards.OfflineRewardClaimResult(plan, 64800, 0, 1, 0, 0));
                    var reward = Object.FindFirstObjectByType<OfflineRewardPopupView>();
                    reward.doubleRewardButton.onClick.Invoke();
                    Check(before == Newtonsoft.Json.JsonConvert.SerializeObject(LocalProgression.State) && OfflineRewardPopupController.IsOpen, "Return-ad preview cannot re-credit the already awarded base reward or dismiss it");
                    reward.confirmButton.onClick.Invoke(); Check(!OfflineRewardPopupController.IsOpen, "Base-reward confirmation remains available");
                    ShopPopupController.Hide(); OfflineRewardPopupController.Hide();
                    Object.DestroyImmediate(ui); ui = null;
                }
                Directory.CreateDirectory("Recordings/ShopRevision");
                File.WriteAllText("Recordings/ShopRevision/playmode.txt", "Isolated Editor Play Mode; no production login or account.\n" + string.Join("\n", checks));
                Debug.Log("SHOP PLAY MODE PASSED " + checks.Count);
            }
            catch (Exception exception)
            {
                SessionState.SetBool(PlayCheck + ".Failed", true);
                Directory.CreateDirectory("Recordings/ShopRevision"); File.WriteAllText("Recordings/ShopRevision/playmode-failure.txt", exception.ToString());
                Debug.LogException(exception);
            }
            finally
            {
                ShopPopupController.Hide(); OfflineRewardPopupController.Hide();
                if (ui != null) Object.DestroyImmediate(ui);
                EditorApplication.isPlaying = false;
            }
        }

        [MenuItem("KingdomIdle/UGUI/Rebuild shop and return rewards")]
        public static void Rebuild()
        {
            F.Init(); F.Catalog = PrefabGenUtil.GetOrCreateCatalog();
            F.Catalog.popupShop = Generate();
            F.Catalog.popupOfflineReward = OfflineRewardPopupPrefabGen.Generate();
            var main = PrefabUtility.LoadPrefabContents(MainPath);
            try { WireMain(main.GetComponent<MainScreenView>()); PrefabUtility.SaveAsPrefabAsset(main, MainPath); }
            finally { PrefabUtility.UnloadPrefabContents(main); }
            EditorUtility.SetDirty(F.Catalog); AssetDatabase.SaveAssets();
            MagePolishPreparation.BakeAuthoredGlyphs();
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
            if (catalog.screenMain.GetComponent<MainScreenView>().btnMenuShop == null || catalog.popupOfflineReward.GetComponent<OfflineRewardPopupView>().doubleRewardButton == null)
                throw new InvalidOperationException("Missing store entry or rewarded return action.");
            int references = 0;
            foreach (var asset in new[] { catalog.popupShop, catalog.popupOfflineReward, catalog.screenMain })
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
            Directory.CreateDirectory("Recordings/ShopRevision");
            File.WriteAllText("Recordings/ShopRevision/validation.txt", "Unity=" + Application.unityVersion + " version=" + PlayerSettings.bundleVersion + "\nreferences=" + references + " missing=0\n" + string.Join("\n", report));
            Debug.Log("SHOP VALIDATION PASSED " + report.Count + " checks, " + references + " references");
        }

        public static void BuildDevice() { Rebuild(); TitleLobbyDeviceBuild.BuildForPlayerJourney(); }
        public static void BuildManual() { Validate(); TitleLobbyDeviceBuild.BuildForManualTesting(); }
    }
}
