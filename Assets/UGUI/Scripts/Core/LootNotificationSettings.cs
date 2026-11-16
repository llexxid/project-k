using System;
using UnityEngine;

namespace KingdomIdle.UGUI
{
    /// <summary>기기 공통 획득 알림의 세부 선택. 전체 숨김은 기존 GamePresentationSettings가 유지한다.</summary>
    public static class LootNotificationSettings
    {
        public const string CurrencyMaskKey = "settings_lootCurrencyMask";
        public const string EquipmentMaskKey = "settings_lootEquipmentMask";
        public const string MageSkillsKey = "settings_lootMageSkills";
        public const int CurrencyCount = 10, EquipmentRarityCount = 3;
        private const int AllCurrencies = (1 << CurrencyCount) - 1;
        private const int AllEquipment = (1 << EquipmentRarityCount) - 1;
        private const int DefaultCurrencies = AllCurrencies & ~(1 << (int)global::eCurrency.Gold);
        private const int DefaultEquipment = AllEquipment & ~1;
        private static int _currencies = DefaultCurrencies, _equipment = DefaultEquipment;
        private static bool _mageSkills = true, _loaded;
        public static event Action Changed;

        public static bool AllowsCurrency(global::eCurrency currency)
        {
            EnsureLoaded(); int index = (int)currency;
            return (uint)index < CurrencyCount && (_currencies & (1 << index)) != 0;
        }
        public static bool AllowsEquipment(int rarity)
        {
            EnsureLoaded();
            return (uint)rarity < EquipmentRarityCount && (_equipment & (1 << rarity)) != 0;
        }
        public static bool ShowMageSkills { get { EnsureLoaded(); return _mageSkills; } }

        public static void SetCurrency(global::eCurrency currency, bool allowed, bool save = true)
        {
            int index = (int)currency;
            if ((uint)index >= CurrencyCount) throw new ArgumentOutOfRangeException(nameof(currency));
            EnsureLoaded(); int next = allowed ? _currencies | (1 << index) : _currencies & ~(1 << index);
            if (next == _currencies) return;
            _currencies = next; PlayerPrefs.SetInt(CurrencyMaskKey, next); Commit(save);
        }
        public static void SetEquipment(int rarity, bool allowed, bool save = true)
        {
            if ((uint)rarity >= EquipmentRarityCount) throw new ArgumentOutOfRangeException(nameof(rarity));
            EnsureLoaded(); int next = allowed ? _equipment | (1 << rarity) : _equipment & ~(1 << rarity);
            if (next == _equipment) return;
            _equipment = next; PlayerPrefs.SetInt(EquipmentMaskKey, next); Commit(save);
        }
        public static void SetMageSkills(bool allowed, bool save = true)
        {
            EnsureLoaded(); if (_mageSkills == allowed) return;
            _mageSkills = allowed; PlayerPrefs.SetInt(MageSkillsKey, allowed ? 1 : 0); Commit(save);
        }
        public static void ResetDefaults(bool save = true)
        {
            EnsureLoaded();
            if (_currencies == DefaultCurrencies && _equipment == DefaultEquipment && _mageSkills) return;
            _currencies = DefaultCurrencies; _equipment = DefaultEquipment; _mageSkills = true;
            PlayerPrefs.SetInt(CurrencyMaskKey, _currencies); PlayerPrefs.SetInt(EquipmentMaskKey, _equipment);
            PlayerPrefs.SetInt(MageSkillsKey, 1); Commit(save);
        }
        public static void Reload()
        {
            int currencies = ReadMask(CurrencyMaskKey, DefaultCurrencies, AllCurrencies);
            int equipment = ReadMask(EquipmentMaskKey, DefaultEquipment, AllEquipment);
            bool mage = PlayerPrefs.GetInt(MageSkillsKey, 1) == 1;
            bool changed = _loaded && (currencies != _currencies || equipment != _equipment || mage != _mageSkills);
            _currencies = currencies; _equipment = equipment; _mageSkills = mage; _loaded = true;
            if (changed) Changed?.Invoke();
        }
        private static int ReadMask(string key, int fallback, int valid)
        {
            int value = PlayerPrefs.GetInt(key, fallback);
            return value < 0 ? fallback : value & valid;
        }
        private static void EnsureLoaded() { if (!_loaded) Reload(); }
        private static void Commit(bool save) { if (save) PlayerPrefs.Save(); Changed?.Invoke(); }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntime()
        {
            _currencies = DefaultCurrencies; _equipment = DefaultEquipment;
            _mageSkills = true; _loaded = false; Changed = null;
        }
    }
}
