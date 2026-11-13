using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using KingdomIdle.Balance;
using Scripts.Core;
using Scripts.Core.Manager;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>실제 설정 프리팹과 기기 선택을 검사하고 모든 사용한 PlayerPrefs/캐시를 원래대로 되돌린다.</summary>
    public static class LootNotificationSettingsAcceptance
    {
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        public static Dictionary<string, object> Run()
        {
            if (!Application.isPlaying || LocalProgression.IsReady || NetworkManager.Instance != null || UserManager.Instance != null || UIManager.Instance != null)
                throw new InvalidOperationException("Run loot settings acceptance in empty-scene PlayMode without a profile or game managers.");
            var integers = new[] { LootNotificationSettings.CurrencyMaskKey, LootNotificationSettings.EquipmentMaskKey, LootNotificationSettings.MageSkillsKey,
                UIManager.PrefKeyHideItem, UIManager.PrefKeyPowerSave, UIManager.PrefKeyDamageText, UIManager.PrefKeyScreenShake,
                GamePresentationSettings.LowSpecKey, GamePresentationSettings.KeepAwakeKey, "title_ambientMotion", UIManager.PrefKeyMute, NumberNotation.PrefKey };
            var floats = new[] { UIManager.PrefKeyVolume, GameAudioSettings.MusicKey, GameAudioSettings.EffectsKey, GameAudioSettings.RoyalGuardKey,
                GameAudioSettings.MonstersKey, GameAudioSettings.SpellsKey, GameAudioSettings.InterfaceKey };
            var preferences = integers.Select(key => new Preference(key, false)).Concat(floats.Select(key => new Preference(key, true))).ToArray();
            var staticValues = new[] { typeof(LootNotificationSettings), typeof(GamePresentationSettings), typeof(GameAudioSettings), typeof(NumberNotation) }
                .SelectMany(type => type.GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public))
                .Where(field => !field.IsInitOnly && !field.IsLiteral).ToDictionary(field => field, field => field.GetValue(null));
            int sleep = Screen.sleepTimeout, frameRate = Application.targetFrameRate; float volume = AudioListener.volume;
            long generation = LocalProgression.AccountGeneration; string account = LocalProgression.AccountKey;
            var previousHost = UIManager.Instance;
            var checks = new List<string>();
            void Check(bool value, string label) { if (!value) throw new InvalidOperationException("Loot settings acceptance: " + label); checks.Add(label); }
            var owner = new GameObject("LootSettingsAcceptance_Manager"); owner.SetActive(false);
            GameObject canvas = null;
            var controller = new SettingsModalController();
            int notifications = 0; Action changed = () => notifications++;
            long allocations = 0;
            try
            {
                foreach (string key in integers.Take(3)) PlayerPrefs.DeleteKey(key);
                PlayerPrefs.SetInt(GamePresentationSettings.LowSpecKey, 0); // Apply의 옛 기기설정 이관을 fixture 밖으로 남기지 않는다.
                PlayerPrefs.SetInt(UIManager.PrefKeyHideItem, 1);
                var legacy = integers.Skip(3).Where(key => key != UIManager.PrefKeyHideItem).Select(key => new Preference(key, false))
                    .Concat(floats.Select(key => new Preference(key, true))).ToArray();
                LootNotificationSettings.Reload();
                Check(Enum.GetValues(typeof(eCurrency)).Cast<eCurrency>().Select(value => (int)value).SequenceEqual(Enumerable.Range(0, 10)) &&
                    (int)eEquipmentRarity.Normal == 0 && (int)eEquipmentRarity.Rare == 1 && (int)eEquipmentRarity.Epic == 2,
                    "Ten actual currencies and the three actual equipment rarities keep their existing IDs");
                for (int i = 0; i < 10; i++) Check(LootNotificationSettings.AllowsCurrency((eCurrency)i) == (i != 0), "Missing currency preference defaults " + (eCurrency)i);
                for (int i = 0; i < 3; i++) Check(LootNotificationSettings.AllowsEquipment(i) == (i != 0), "Missing equipment preference defaults rarity " + i);
                Check(LootNotificationSettings.ShowMageSkills && !PlayerPrefs.HasKey(LootNotificationSettings.CurrencyMaskKey) &&
                    !PlayerPrefs.HasKey(LootNotificationSettings.EquipmentMaskKey) && !PlayerPrefs.HasKey(LootNotificationSettings.MageSkillsKey),
                    "Rarity-free mage skills default on; reading defaults does not create saved preferences");
                Check(!LootNotificationSettings.AllowsCurrency((eCurrency)(-1)) && !LootNotificationSettings.AllowsCurrency((eCurrency)10) &&
                    !LootNotificationSettings.AllowsEquipment(-1) && !LootNotificationSettings.AllowsEquipment(3), "Unknown currency or equipment rarity is not displayed");
                LootNotificationSettings.Changed += changed;
                var catalog = AssetDatabase.LoadAssetAtPath<UIViewCatalog>("Assets/UGUI/UIViewCatalog.asset");
                Check(catalog != null && catalog.overlaySettings != null, "Real settings catalog prefab exists");
                var host = owner.AddComponent<UIManager>(); Set(host, "catalog", catalog); SetHost(host);
                canvas = new GameObject("LootSettingsAcceptance_Canvas", typeof(RectTransform), typeof(Canvas));
                canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace; ((RectTransform)canvas.transform).sizeDelta = new Vector2(1080, 1920);
                Set(host, "layerOverlays", (RectTransform)canvas.transform);
                controller.Open(host);
                var view = View(controller);
                Check(view.tabs?.Length == 4 && view.pages?.Length == 4 && view.tabs[3].GetComponentInChildren<TMPro.TMP_Text>(true).text == "획득 알림",
                    "Actual settings prefab contains the fourth acquisition notifications tab");
                Check(view.lootScroll != null && view.lootScroll != view.scroll && view.pages[3] == view.lootScroll.gameObject &&
                    view.lootCurrencyToggles?.Length == 10 && view.lootEquipmentToggles?.Length == 3 && view.tglLootMageSkills != null && view.btnLootDefaults != null,
                    "Independent scroll and all ten currency / three equipment / mage / reset references are serialized");
                Check(view.tglHideItem.isOn && GamePresentationSettings.HideItemNotifications && PlayerPrefs.GetInt(UIManager.PrefKeyHideItem) == 1 &&
                    !view.lootCurrencyToggles[0].isOn && !view.lootEquipmentToggles[0].isOn && view.tglLootMageSkills.isOn,
                    "Existing hide-all master is retained while detailed defaults stay independent");
                view.tabs[3].onClick.Invoke();
                Check(view.pages.Select(page => page.activeSelf).SequenceEqual(new[] { false, false, false, true }) &&
                    !view.scroll.gameObject.activeSelf && view.lootScroll.gameObject.activeInHierarchy &&
                    view.GetComponentsInChildren<ScrollRect>(false).Length == 1, "Fourth tab fills the body with one active scroll page");
                view.lootCurrencyToggles[0].isOn = true; view.lootEquipmentToggles[0].isOn = true; view.tglLootMageSkills.isOn = false;
                Check(notifications == 3 && LootNotificationSettings.AllowsCurrency(eCurrency.Gold) && LootNotificationSettings.AllowsEquipment(0) &&
                    !LootNotificationSettings.ShowMageSkills, "Actual UI switches update each category once");
                LootNotificationSettings.SetCurrency(eCurrency.Gold, true, false); LootNotificationSettings.SetEquipment(0, true, false);
                LootNotificationSettings.SetMageSkills(false, false);
                Check(notifications == 3, "Repeated identical selections do not publish another refresh");
                view.tglHideItem.isOn = false; view.tglHideItem.isOn = true;
                Check(notifications == 3 && GamePresentationSettings.HideItemNotifications && LootNotificationSettings.AllowsCurrency(eCurrency.Gold) &&
                    LootNotificationSettings.AllowsEquipment(0) && !LootNotificationSettings.ShowMageSkills,
                    "Toggling the legacy master preserves detailed choices");
                LootNotificationSettings.Reload(); int previousViewId = view.GetInstanceID();
                controller.Close(); UnityEngine.Object.DestroyImmediate(view.gameObject); controller.Open(host); view = View(controller);
                Check(view.GetInstanceID() != previousViewId && view.lootCurrencyToggles[0].isOn && view.lootEquipmentToggles[0].isOn &&
                    !view.tglLootMageSkills.isOn && view.tglHideItem.isOn && view.lootScroll.gameObject.activeInHierarchy,
                    "Saved choices and selected tab survive actual settings recreation without a login");
                for (int i = 0; i < 10; i++)
                {
                    var before = Enumerable.Range(0, 10).Select(index => LootNotificationSettings.AllowsCurrency((eCurrency)index)).ToArray();
                    LootNotificationSettings.SetCurrency((eCurrency)i, !before[i], false);
                    Check(Enumerable.Range(0, 10).All(index => LootNotificationSettings.AllowsCurrency((eCurrency)index) == (index == i ? !before[index] : before[index])),
                        "Currency selection changes only " + (eCurrency)i);
                    LootNotificationSettings.SetCurrency((eCurrency)i, before[i], false);
                }
                int beforeReset = notifications; view.btnLootDefaults.onClick.Invoke();
                Check(notifications == beforeReset + 1 && !view.lootCurrencyToggles[0].isOn && !view.lootEquipmentToggles[0].isOn &&
                    view.tglLootMageSkills.isOn && GamePresentationSettings.HideItemNotifications && view.tglHideItem.isOn,
                    "One reset restores detail defaults and keeps the existing hide-all master");
                Canvas.ForceUpdateCanvases(); LayoutRebuilder.ForceRebuildLayoutImmediate(view.lootScroll.content);
                view.lootScroll.verticalNormalizedPosition = 0; Canvas.ForceUpdateCanvases();
                var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(view.lootScroll.viewport, view.btnLootDefaults.transform);
                Check(view.lootScroll.content.rect.height > view.lootScroll.viewport.rect.height &&
                    bounds.min.y >= view.lootScroll.viewport.rect.yMin - 2 && bounds.max.y <= view.lootScroll.viewport.rect.yMax + 2,
                    "Actual long settings page scrolls all the way to its final reset action");
                view.tabs[0].onClick.Invoke();
                Check(view.scroll.gameObject.activeInHierarchy && !view.lootScroll.gameObject.activeSelf && view.pages[0].activeSelf,
                    "Returning to Display restores the existing settings scroll");
                Check(legacy.All(pref => pref.Matches()), "Unrelated existing display, audio and device preferences remain unchanged");
                long beforeAllocation = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 10000; i++) { LootNotificationSettings.AllowsCurrency(eCurrency.Gold); LootNotificationSettings.AllowsEquipment(0); }
                allocations = GC.GetAllocatedBytesForCurrentThread() - beforeAllocation;
                Check(allocations == 0, "Twenty thousand cached filter reads allocate no managed bytes");
                Check(!LocalProgression.IsReady && LocalProgression.AccountGeneration == generation && LocalProgression.AccountKey == account &&
                    NetworkManager.Instance == null && UserManager.Instance == null, "Settings work without opening an account or creating a network session");
            }
            finally
            {
                LootNotificationSettings.Changed -= changed;
                controller.Close();
                if (canvas != null) UnityEngine.Object.DestroyImmediate(canvas);
                UnityEngine.Object.DestroyImmediate(owner); SetHost(previousHost);
                foreach (var pref in preferences) pref.Restore();
                PlayerPrefs.Save();
                foreach (var value in staticValues) value.Key.SetValue(null, value.Value);
                Screen.sleepTimeout = sleep; Application.targetFrameRate = frameRate; AudioListener.volume = volume;
            }
            Check(preferences.All(pref => pref.Matches()), "Finally restores the original existence and value of every touched PlayerPrefs key");
            return new Dictionary<string, object> { ["passed"] = checks.Count, ["checks"] = checks, ["cachedReadsAllocatedBytes"] = allocations };
        }
        private static SettingsModalView View(SettingsModalController controller) => (SettingsModalView)typeof(SettingsModalController).GetField("_view", Fields).GetValue(controller);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, Fields).SetValue(target, value);
        private static void SetHost(UIManager host) => typeof(UIManager).GetProperty("Instance").SetValue(null, host);
        private sealed class Preference
        {
            private readonly string key; private readonly bool exists, floating; private readonly int integer; private readonly float number;
            internal Preference(string name, bool isFloat)
            { key = name; floating = isFloat; exists = PlayerPrefs.HasKey(key); integer = floating ? 0 : PlayerPrefs.GetInt(key); number = floating ? PlayerPrefs.GetFloat(key) : 0; }
            internal void Restore()
            { if (!exists) PlayerPrefs.DeleteKey(key); else if (floating) PlayerPrefs.SetFloat(key, number); else PlayerPrefs.SetInt(key, integer); }
            internal bool Matches() => PlayerPrefs.HasKey(key) == exists && (!exists || (floating ? PlayerPrefs.GetFloat(key).Equals(number) : PlayerPrefs.GetInt(key) == integer));
        }
    }
}
