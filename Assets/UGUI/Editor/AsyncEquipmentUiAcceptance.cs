#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using KingdomIdle.Balance;
using KingdomIdle.Rewards;
using Scripts.Core;
using Scripts.Core.Manager;
using UnityEditor;
using UnityEngine;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>비동기 장비 완료의 상세 선택과 상점을 닫은 뒤 보상 모달 순서를 실제 UI 호출부로 검사한다.</summary>
    public static class AsyncEquipmentUiAcceptance
    {
        const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
        const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        public static Dictionary<string, object> Run()
        {
            var cases = new List<Dictionary<string, object>>();
            var failures = new List<string>();
            void Check(bool valid, string name, object actual = null)
            {
                cases.Add(new Dictionary<string, object> { { "case", name }, { "passed", valid }, { "actual", actual } });
                if (!valid) failures.Add(name);
            }
            Dictionary<string, object> Report() => new()
            {
                { "passed", cases.Count(row => (bool)row["passed"]) }, { "failures", failures }, { "cases", cases },
                { "mode", "empty PlayMode; memory state; inactive network without authentication context; no API calls" }
            };
            if (!Application.isPlaying || LocalProgression.IsReady || UIManager.Instance != null ||
                NetworkManager.Instance != null || UserManager.Instance != null || ModalBackHandler.HasOpenModal)
            { Check(false, "Run in empty PlayMode without a profile, managers, or open modals"); return Report(); }

            // 생명주기 구독을 가진 정적 생성자를 먼저 완료해, 검사 후 정상 구독을 지우지 않는다.
            RuntimeHelpers.RunClassConstructor(typeof(ShopPopupController).TypeHandle);
            var values = new[] { typeof(InventoryPanelController), typeof(ShopPopupController), typeof(LocalProgression),
                typeof(NumberNotation), typeof(GamePresentationSettings) }.SelectMany(type => type.GetFields(Static))
                .Where(field => !field.IsInitOnly && !field.IsLiteral).ToDictionary(field => field, field => field.GetValue(null));
            var registry = (IList)typeof(ModalBackHandler).GetField("Open", Static).GetValue(null);
            var previousModals = registry.Cast<object>().ToArray();
            var preferences = Preferences();
            var previousUi = UIManager.Instance;
            var previousNetwork = NetworkManager.Instance;
            var previousUsers = UserManager.Instance;
            GameObject inventory = null, managers = null, canvas = null;
            try
            {
                try
                {
                    inventory = new GameObject("AsyncEquipmentUiAcceptance_Inventory") { hideFlags = HideFlags.HideAndDontSave };
                    SetStatic(typeof(InventoryPanelController), "_view", inventory.AddComponent<InventoryPanelView>());
                    var pending = typeof(InventoryPanelController).GetField("_pendingEnhanceItemId", Static);
                    void InventoryCase(string name, string current, string returned, bool ok, EquipmentManager.ActionKind kind, string expected)
                    {
                        try
                        {
                            SetStatic(typeof(InventoryPanelController), "_detailItemId", current);
                            SetStatic(typeof(InventoryPanelController), "_equipmentDirty", false);
                            pending?.SetValue(null, "B-before");
                            typeof(InventoryPanelController).GetMethod("OnEquipmentActionCompleted", Static).Invoke(null,
                                new object[] { new EquipmentManager.ActionInfo { Kind = kind, Ok = ok, ItemId = returned } });
                            string actual = (string)GetStatic(typeof(InventoryPanelController), "_detailItemId");
                            Check(actual == expected, name, new Dictionary<string, object>
                            {
                                { "current", current }, { "response", returned }, { "expected", expected }, { "detail", actual },
                                { "pendingOriginSeeded", pending != null }, { "pendingOriginSeedSkipped", pending == null }
                            });
                        }
                        catch (Exception error) { Check(false, name, Message(error)); }
                    }
                    InventoryCase("Inventory: A selection survives B enhancement completion", "A", "B-after", true, EquipmentManager.ActionKind.Enhance, "A");
                    InventoryCase("Inventory: same B detail follows legacy enhanced ID", "B-before", "B-after", true, EquipmentManager.ActionKind.Enhance, "B-after");
                    InventoryCase("Inventory: lock response cannot replace another selected detail", "A", "B-after", true, EquipmentManager.ActionKind.Lock, "A");
                    InventoryCase("Inventory: failed enhancement preserves selection", "A", "B-after", false, EquipmentManager.ActionKind.Enhance, "A");
                    InventoryCase("Inventory: closed detail is not reopened", null, "B-after", true, EquipmentManager.ActionKind.Enhance, null);
                    InventoryCase("Inventory: missing response ID preserves selection", "B-before", null, true, EquipmentManager.ActionKind.Enhance, "B-before");
                    InventoryCase("Inventory: unchanged response ID preserves selection", "B-before", "B-before", true, EquipmentManager.ActionKind.Enhance, "B-before");
                }
                catch (Exception error) { Check(false, "Inventory fixture setup", Message(error)); }
                finally
                {
                    SetStatic(typeof(InventoryPanelController), "_view", null);
                    if (inventory != null) UnityEngine.Object.DestroyImmediate(inventory);
                    inventory = null;
                }

                try
                {
                    var catalog = AssetDatabase.LoadAssetAtPath<UIViewCatalog>("Assets/UGUI/UIViewCatalog.asset");
                    if (catalog == null || catalog.overlaySettings == null || catalog.popupRewardInfo == null || catalog.itemRewardSlot == null)
                        throw new InvalidOperationException("Settings, reward popup, and reward slot prefab references are required.");
                    // Ensure는 파일을 열 수 있으므로 메모리 상태를 읽는 동안 _opening으로 진입을 차단한다.
                    SetStatic(typeof(LocalProgression), "_opening", true);
                    var state = new ProgressionState(); state.Wallet[eCurrency.EquipmentTicket] = 2;
                    SetStatic(typeof(LocalProgression), "_state", state);
                    SetStatic(typeof(LocalProgression), "<AccountKey>k__BackingField", "async-ui-memory-fixture");
                    string before = Newtonsoft.Json.JsonConvert.SerializeObject(state);

                    managers = new GameObject("AsyncEquipmentUiAcceptance_Managers") { hideFlags = HideFlags.HideAndDontSave };
                    managers.SetActive(false);
                    var network = managers.AddComponent<NetworkManager>();
                    NetworkManager.Instance = network;
                    typeof(NetworkManager).GetProperty("LoginState").GetSetMethod(true).Invoke(network, new object[] { GameLoginState.Ready });
                    var host = managers.AddComponent<UIManager>();
                    typeof(UIManager).GetField("catalog", Instance).SetValue(host, catalog); SetUi(host);
                    canvas = new GameObject("AsyncEquipmentUiAcceptance_Canvas", typeof(RectTransform), typeof(Canvas)) { hideFlags = HideFlags.HideAndDontSave };
                    canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                    ((RectTransform)canvas.transform).sizeDelta = new Vector2(1080, 1920);
                    RectTransform Layer(string name)
                    {
                        var layer = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
                        layer.SetParent(canvas.transform, false); layer.anchorMin = Vector2.zero; layer.anchorMax = Vector2.one;
                        layer.offsetMin = layer.offsetMax = Vector2.zero; return layer;
                    }
                    var popups = Layer("LayerPopups"); var overlays = Layer("LayerOverlays");
                    typeof(UIManager).GetField("layerPopups", Instance).SetValue(host, popups);
                    typeof(UIManager).GetField("layerOverlays", Instance).SetValue(host, overlays);
                    var settings = UnityEngine.Object.Instantiate(catalog.overlaySettings, overlays, false);
                    settings.SetActive(true); settings.transform.SetAsLastSibling();
                    int settingsClosed = 0;
                    ModalBackHandler.Bind(settings, () => { settingsClosed++; settings.SetActive(false); });
                    var claim = (ShopAdRewardService.Claim)typeof(ShopAdRewardService.Claim)
                        .GetConstructor(Instance, null, new[] { typeof(ShopOffer), typeof(eCurrency) }, null)
                        .Invoke(new object[] { ShopCatalog.Find("rewarded.daily_equipment"), eCurrency.EquipmentTicket });
                    typeof(ShopAdRewardService.Claim).GetProperty("Earned").GetSetMethod(true).Invoke(claim, new object[] { true });
                    SetStatic(typeof(ShopPopupController), "_view", null);
                    SetStatic(typeof(ShopPopupController), "_host", host);
                    SetStatic(typeof(ShopPopupController), "_claim", claim);
                    SetStatic(typeof(ShopPopupController), "_busy", true);
                    Check(!ShopPopupController.IsOpen && settings.activeInHierarchy && ShopAdRewardService.IsCurrentAccount(claim),
                        "Shop: completion fixture has a closed shop and an active real settings modal");
                    try
                    {
                        typeof(ShopPopupController).GetMethod("HandleReward", Static).Invoke(null,
                            new object[] { claim, new RewardResult(RewardStatus.Success, new[] { new RewardGrant(eCurrency.EquipmentTicket, 2) }), null });
                    }
                    catch (Exception error) { Check(false, "Shop: actual successful completion callback", Message(error)); }
                    var rewards = canvas.GetComponentsInChildren<RewardPopupView>(false);
                    var reward = rewards.LastOrDefault();
                    Check(rewards.Length == 1, "Shop: successful completion is shown once after the shop closed", rewards.Length);
                    Check(reward != null && reward.transform.parent == overlays, "Shop: completion popup uses the overlay layer", reward != null ? reward.transform.parent.name : "missing");
                    Check(reward != null && reward.transform.parent == settings.transform.parent &&
                        reward.transform.GetSiblingIndex() > settings.transform.GetSiblingIndex(), "Shop: completion popup is above the current settings modal");
                    host.RequestBack();
                    Check(reward != null && !reward.gameObject.activeInHierarchy, "Shop: first Back closes the visible completion popup");
                    Check(settings.activeInHierarchy && settingsClosed == 0, "Shop: first Back leaves the underlying settings modal open", settingsClosed);
                    host.RequestBack();
                    Check(!settings.activeInHierarchy && settingsClosed == 1, "Shop: second Back closes settings exactly once", settingsClosed);
                    Check(ReferenceEquals(GetStatic(typeof(LocalProgression), "_state"), state) &&
                        (bool)GetStatic(typeof(LocalProgression), "_opening") && before == Newtonsoft.Json.JsonConvert.SerializeObject(state),
                        "Shop: display completion does not mutate the in-memory progression state");
                    Check(!managers.activeInHierarchy && typeof(NetworkManager).GetField("_context", Instance).GetValue(network) == null &&
                        typeof(NetworkManager).GetField("_functions", Instance).GetValue(network) == null &&
                        ((ICollection)typeof(NetworkManager).GetField("_pendingCalls", Instance).GetValue(network)).Count == 0,
                        "Shop: network stays inactive and uninitialized without authentication or queued API calls");
                }
                catch (Exception error) { Check(false, "Shop fixture setup or input route", Message(error)); }
            }
            finally
            {
                try
                {
                    if (canvas != null) UnityEngine.Object.DestroyImmediate(canvas);
                    if (inventory != null) UnityEngine.Object.DestroyImmediate(inventory);
                    if (managers != null) UnityEngine.Object.DestroyImmediate(managers);
                }
                catch (Exception error) { Check(false, "Fixture object cleanup", Message(error)); }
                finally
                {
                    SetUi(previousUi); NetworkManager.Instance = previousNetwork; UserManager.Instance = previousUsers;
                    foreach (var pair in values) pair.Key.SetValue(null, pair.Value);
                    registry.Clear(); foreach (var modal in previousModals) registry.Add(modal);
                    bool changed = false; foreach (var preference in preferences) changed |= preference.Restore();
                    if (changed) PlayerPrefs.Save();
                }
                Check(UIManager.Instance == previousUi && NetworkManager.Instance == previousNetwork && UserManager.Instance == previousUsers &&
                    values.All(pair => Equals(pair.Key.GetValue(null), pair.Value)) && registry.Cast<object>().SequenceEqual(previousModals) && preferences.All(value => value.Matches()),
                    "Cleanup restores controller, account, manager, modal registry, and preference state");
            }
            return Report();
        }

        static object GetStatic(Type type, string name) => type.GetField(name, Static).GetValue(null);
        static void SetStatic(Type type, string name, object value) => type.GetField(name, Static).SetValue(null, value);
        static void SetUi(UIManager host) => typeof(UIManager).GetProperty("Instance").GetSetMethod(true).Invoke(null, new object[] { host });
        static string Message(Exception error) => (error is TargetInvocationException target && target.InnerException != null ? target.InnerException : error).ToString();
        static Preference[] Preferences()
        {
            var integers = new[] { UIManager.PrefKeyMute, UIManager.PrefKeyPowerSave, UIManager.PrefKeyHideItem, UIManager.PrefKeyDamageText,
                UIManager.PrefKeyScreenShake, GamePresentationSettings.LowSpecKey, GamePresentationSettings.KeepAwakeKey, "title_ambientMotion", NumberNotation.PrefKey,
                LootNotificationSettings.CurrencyMaskKey, LootNotificationSettings.EquipmentMaskKey, LootNotificationSettings.MageSkillsKey };
            var floats = new[] { UIManager.PrefKeyVolume, GameAudioSettings.MusicKey, GameAudioSettings.EffectsKey, GameAudioSettings.RoyalGuardKey,
                GameAudioSettings.MonstersKey, GameAudioSettings.SpellsKey, GameAudioSettings.InterfaceKey };
            return integers.Select(key => new Preference(key, false)).Concat(floats.Select(key => new Preference(key, true))).ToArray();
        }
        sealed class Preference
        {
            readonly string _key; readonly bool _floating, _exists; readonly int _integer; readonly float _number;
            internal Preference(string key, bool floating)
            { _key = key; _floating = floating; _exists = PlayerPrefs.HasKey(key); if (floating) _number = PlayerPrefs.GetFloat(key); else _integer = PlayerPrefs.GetInt(key); }
            internal bool Matches() => PlayerPrefs.HasKey(_key) == _exists && (!_exists || (_floating ? PlayerPrefs.GetFloat(_key) == _number : PlayerPrefs.GetInt(_key) == _integer));
            internal bool Restore()
            { if (Matches()) return false; if (!_exists) PlayerPrefs.DeleteKey(_key); else if (_floating) PlayerPrefs.SetFloat(_key, _number); else PlayerPrefs.SetInt(_key, _integer); return true; }
        }
    }
}
#endif
