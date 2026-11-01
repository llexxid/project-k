#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using KingdomIdle.Balance;
using Scripts.Core;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>실제 프리팹과 격리 계정으로 인벤토리 재화 탭의 표시·갱신·정리를 검사한다.</summary>
    public static class InventoryCurrencyAcceptance
    {
        public static Dictionary<string, object> Run()
        {
            if (!Application.isPlaying || UIManager.Instance != null || UserManager.Instance != null ||
                (LocalProgression.IsReady && !(LocalProgression.AccountKey?.StartsWith("balance-qa-", StringComparison.Ordinal) ?? false)))
                throw new InvalidOperationException("InventoryCurrencyAcceptance requires PlayMode in an empty test scene without a production account.");

            var checks = new List<string>();
            void Check(bool condition, string label)
            { if (!condition) throw new InvalidOperationException("Inventory currency acceptance: " + label); checks.Add(label); }
            GameObject managers = null, canvas = null;
            InventoryPanelView view = null;
            int originalSubscribers = CurrencySubscribers();
            bool originalReady = LocalProgression.IsReady;
            string originalAccount = LocalProgression.AccountKey;
            var wallet = typeof(EconomyBridge).Assembly.GetType("Scripts.Wallets.Wallet");
            var subscribed = wallet.GetField("_subscribed", BindingFlags.Static | BindingFlags.NonPublic);
            bool originalWalletSubscription = (bool)subscribed.GetValue(null);
            using (LocalProgression.BeginTestSession())
            {
                try
                {
                    var catalog = AssetDatabase.LoadAssetAtPath<UIViewCatalog>("Assets/UGUI/UIViewCatalog.asset");
                    Check(catalog != null && catalog.panelInventory != null && catalog.itemInventoryListPage != null &&
                        catalog.itemEquipCell != null && catalog.popupRewardInfo != null, "Catalog references the real inventory, card, and information prefabs");
                    LocalProgression.OpenTestAccount("inventory-currency-" + Guid.NewGuid().ToString("N"));
                    managers = new GameObject("InventoryCurrencyAcceptance_Managers");
                    managers.SetActive(false);
                    var users = managers.AddComponent<UserManager>();
                    SetField(users, "_user", new Scripts.Users.User());
                    UserManager.Instance = users;
                    var ui = managers.AddComponent<UIManager>();
                    SetField(ui, "catalog", catalog);
                    SetUi(ui);
                    canvas = new GameObject("InventoryCurrencyAcceptance_Canvas", typeof(RectTransform), typeof(Canvas));
                    canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                    view = UnityEngine.Object.Instantiate(catalog.panelInventory, canvas.transform, false).GetComponent<InventoryPanelView>();
                    view.gameObject.SetActive(true);
                    InventoryPanelController.Populate(view);
                    var content = Field<RectTransform>(view, "content");
                    var tabs = Field<RectTransform>(view, "navBar").GetComponentsInChildren<UnityEngine.UI.Button>(false);
                    Check(tabs.Length == 4, "The existing four inventory tabs remain");
                    Check(CurrencySubscribers() == originalSubscribers + 1, "Opening subscribes once to wallet amount changes");
                    var allText = Text(CurrentPage(content));
                    tabs[2].onClick.Invoke();
                    Check(Cards(CurrentPage(content)).Length == 0, "Zero enhancement stones produce no material slot");
                    tabs[3].onClick.Invoke();
                    Check(Cards(CurrentPage(content)).Length == 0, "Zero tickets produce no miscellaneous slots");

                    Check(SetBalances(7, 2, 3), "Seed three balances only in the isolated account");
                    var page = CurrentPage(content);
                    var cards = Cards(page);
                    Check(cards.Select(x => Field<TMP_Text>(x, "nameLabel").text).SequenceEqual(new[] { "장비 뽑기권", "마탑 뽑기권" }),
                        "Miscellaneous slots contain both tickets in the agreed order");
                    Check(Field<UnityEngine.UI.Image>(cards[0], "icon").sprite == catalog.iconChest &&
                        Field<UnityEngine.UI.Image>(cards[1], "icon").sprite == catalog.iconSkillWand &&
                        cards.All(x => !Field<UnityEngine.UI.Image>(x, "rarityBar").gameObject.activeSelf),
                        "Tickets use the shared icons and hide equipment rarity decoration");
                    Check(Field<TMP_Text>(cards[0], "subLabel").text.Contains("2") && Field<TMP_Text>(cards[1], "subLabel").text.Contains("3"),
                        "Each ticket slot displays its own balance");
                    Check(SetBalances(7, 5, 3), "Increase the visible equipment ticket balance");
                    Check(CurrentPage(content) == page && Cards(page)[0] == cards[0] && Field<TMP_Text>(cards[0], "subLabel").text.Contains("5"),
                        "A positive balance update changes the existing slot without rebuilding the page");
                    Check(LocalProgression.Execute("qa-inventory-unrelated", s => { s.Wallet[eCurrency.Gold] = 50; return true; }) && CurrentPage(content) == page,
                        "An unrelated wallet change leaves the current page intact");

                    cards[0].Button.onClick.Invoke();
                    var popup = view.GetComponentsInChildren<RewardPopupView>(false).Single();
                    Check(popup.transform.IsChildOf(view.transform) && popup.transform.parent == view.transform &&
                        popup.transform.GetSiblingIndex() == view.transform.childCount - 1,
                        "Item information is the inventory's top sibling so an external lower popup layer cannot hide it");
                    Check(popup.title.text == "장비 뽑기권" && popup.description.text.Contains("사용 기능은 준비 중") &&
                        popup.GetComponentInChildren<RewardSlotView>().quantity.text == NumberNotation.Format(5),
                        "Clicking shows the shared item explanation and the current balance");
                    popup.Close();
                    Check(SetBalances(7, 0, 3) && Cards(CurrentPage(content)).Length == 1 &&
                        Field<TMP_Text>(Cards(CurrentPage(content))[0], "nameLabel").text == "마탑 뽑기권",
                        "A ticket reaching zero removes only its slot");
                    Check(SetBalances(7, 0, 0) && Cards(CurrentPage(content)).Length == 0,
                        "Removing the final ticket restores the empty miscellaneous page");

                    tabs[2].onClick.Invoke();
                    page = CurrentPage(content);
                    cards = Cards(page);
                    Check(cards.Length == 1 && Field<TMP_Text>(cards[0], "nameLabel").text == "강화석" &&
                        Field<UnityEngine.UI.Image>(cards[0], "icon").sprite == catalog.iconEquipmentStone &&
                        Field<TMP_Text>(cards[0], "subLabel").text.Contains("7"), "Materials display enhancement stones as one icon card");
                    Check(SetBalances(9, 2, 3) && CurrentPage(content) == page && Field<TMP_Text>(cards[0], "subLabel").text.Contains("9"),
                        "The material slot updates in place while ticket balances change independently");
                    tabs[0].onClick.Invoke();
                    Check(Cards(CurrentPage(content)).Length == 0 && Text(CurrentPage(content)) == allText,
                        "Currency ownership does not add slots or text to the existing All tab");
                    tabs[2].onClick.Invoke();
                    Check(SetBalances(0, 2, 3) && Cards(CurrentPage(content)).Length == 0, "Zero stones hide the material slot after an update");
                    Close(view);
                    Check(CurrencySubscribers() == originalSubscribers, "Closing removes the wallet change subscription");
                    InventoryPanelController.Populate(view);
                    Check(CurrencySubscribers() == originalSubscribers + 1, "Reopening does not accumulate wallet listeners");
                    Close(view);
                }
                finally
                {
                    if (view != null) Close(view);
                    if (canvas != null) UnityEngine.Object.DestroyImmediate(canvas);
                    UserManager.Instance = null;
                    SetUi(null);
                    if (managers != null) UnityEngine.Object.DestroyImmediate(managers);
                    if (!originalWalletSubscription)
                    {
                        // Dispose의 Changed가 fixture 지갑을 통해 local-guest를 다시 열지 않도록 먼저 해제한다.
                        var publish = (Action)Delegate.CreateDelegate(typeof(Action), wallet.GetMethod("Publish", BindingFlags.Static | BindingFlags.NonPublic));
                        LocalProgression.Changed -= publish;
                        subscribed.SetValue(null, false);
                        ((Dictionary<eCurrency, long>)wallet.GetField("Published", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null)).Clear();
                    }
                }
            }
            Check(CurrencySubscribers() == originalSubscribers && UIManager.Instance == null && UserManager.Instance == null &&
                (bool)subscribed.GetValue(null) == originalWalletSubscription && LocalProgression.IsReady == originalReady &&
                LocalProgression.AccountKey == originalAccount, "Cleanup restores wallet subscriptions, account state, and manager references");
            return new Dictionary<string, object> { { "passed", checks.Count }, { "checks", checks } };
        }

        private static bool SetBalances(long stones, long equipment, long mage) => LocalProgression.Execute("qa-inventory-currencies", s =>
        {
            s.Wallet[eCurrency.EquipmentStone] = stones;
            s.Wallet[eCurrency.EquipmentTicket] = equipment;
            s.Wallet[eCurrency.MageTicket] = mage;
            return true;
        });
        private static InventoryListPageView CurrentPage(Transform content) => content.GetComponentsInChildren<InventoryListPageView>(false).Last();
        private static EquipCellView[] Cards(InventoryListPageView page) => page.GetComponentsInChildren<EquipCellView>(false);
        private static string Text(InventoryListPageView page) => string.Join("|", new[] { "sectionTitle", "subsectionTitle", "placeholder" }
            .Select(name => Field<TMP_Text>(page, name)).Select(label => label.gameObject.activeSelf ? label.text : ""));
        private static void Close(InventoryPanelView view) => ((Action)typeof(BottomSheetView).GetField("OnClosed", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view))?.Invoke();
        private static int CurrencySubscribers()
        {
            var wallet = typeof(EconomyBridge).Assembly.GetType("Scripts.Wallets.Wallet");
            var handlers = (Delegate)wallet.GetField("OnAnyChanged", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            return handlers?.GetInvocationList().Count(x => x.Method.DeclaringType == typeof(InventoryPanelController)) ?? 0;
        }
        private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).GetValue(target);
        private static void SetField(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).SetValue(target, value);
        private static void SetUi(UIManager value) => typeof(UIManager).GetProperty("Instance").GetSetMethod(true).Invoke(null, new object[] { value });
    }
}
#endif
