using System;
using System.Collections.Generic;
using UnityEngine;
using Scripts.Core;
using Scripts.Wallets;

namespace KingdomIdle.MageTower
{
    [DefaultExecutionOrder(-950)]
    public sealed class MageTowerManager : MonoBehaviour
    {
        public static MageTowerManager Instance { get; private set; }

        [SerializeField] private MageTowerSkillListSO skillList;

        public const int SlotCount = 5;
        private const string PrefKey = "mt_save";

        private readonly int[] _equipped = new int[SlotCount];
        private readonly Dictionary<int, int> _enhanceLevels = new();
        private readonly Dictionary<int, int> _awakeningLevels = new();
        private readonly Dictionary<int, int> _fragments = new();
        private readonly Dictionary<int, int> _totalAKSpent = new();

        public event Action OnStateChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;

            for (int i = 0; i < SlotCount; i++)
                _equipped[i] = -1;

            Load();
            InitTestData();
        }

        // ===== 테스트 데이터 =====
        private void InitTestData()
        {
            EconomyBridge.TryGetAmount(eCurrency.ArcaneKnowledge, out int ak);
            if (ak < 1000) EconomyBridge.Add(eCurrency.ArcaneKnowledge, 1000 - ak);

            EconomyBridge.TryGetAmount(eCurrency.AncientCoin, out int ac);
            if (ac < 1000) EconomyBridge.Add(eCurrency.AncientCoin, 1000 - ac);

            var skills = GetAllSkills();
            for (int i = 0; i < skills.Count; i++)
            {
                if (skills[i] == null) continue;
                int id = skills[i].id;
                if (!_fragments.ContainsKey(id))
                    _fragments[id] = 15;
                else if (_fragments[id] < 15)
                    _fragments[id] = 15;
            }
        }

        // ===== 데이터 접근 =====
        public IReadOnlyList<MageTowerSkillSO> GetAllSkills()
        {
            if (skillList == null || skillList.skills == null)
                return Array.Empty<MageTowerSkillSO>();
            return skillList.skills;
        }

        public MageTowerSkillSO GetSkillById(int id)
        {
            var skills = GetAllSkills();
            for (int i = 0; i < skills.Count; i++)
            {
                if (skills[i] != null && skills[i].id == id)
                    return skills[i];
            }
            return null;
        }

        public int GetEquippedSkillId(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= SlotCount) return -1;
            return _equipped[slotIndex];
        }

        public int GetEnhanceLevel(int skillId) =>
            _enhanceLevels.TryGetValue(skillId, out int lv) ? lv : 0;

        public int GetAwakeningLevel(int skillId) =>
            _awakeningLevels.TryGetValue(skillId, out int lv) ? lv : 0;

        public int GetFragments(int skillId) =>
            _fragments.TryGetValue(skillId, out int f) ? f : 0;

        public int GetTotalAKSpent(int skillId) =>
            _totalAKSpent.TryGetValue(skillId, out int s) ? s : 0;

        // ===== 스탯 계산 =====
        public float GetEffectiveDamage(int skillId)
        {
            var so = GetSkillById(skillId);
            if (so == null) return 0f;
            int eLv = GetEnhanceLevel(skillId);
            int aLv = GetAwakeningLevel(skillId);
            return so.baseDamage * (1f + 0.05f * eLv) * (1f + 0.05f * aLv);
        }

        public float GetEffectiveCooldown(int skillId)
        {
            var so = GetSkillById(skillId);
            if (so == null) return 0f;
            int aLv = GetAwakeningLevel(skillId);
            return Mathf.Max(1f, so.baseCooldown - 0.2f * aLv);
        }

        // ===== 장착 =====
        public bool Equip(int slotIndex, int skillId)
        {
            if (slotIndex < 0 || slotIndex >= SlotCount) return false;
            if (GetSkillById(skillId) == null) return false;

            for (int i = 0; i < SlotCount; i++)
            {
                if (_equipped[i] == skillId)
                    _equipped[i] = -1;
            }

            _equipped[slotIndex] = skillId;
            Save();
            OnStateChanged?.Invoke();
            return true;
        }

        public void Unequip(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= SlotCount) return;
            if (_equipped[slotIndex] == -1) return;
            _equipped[slotIndex] = -1;
            Save();
            OnStateChanged?.Invoke();
        }

        public bool IsEquipped(int skillId)
        {
            for (int i = 0; i < SlotCount; i++)
            {
                if (_equipped[i] == skillId) return true;
            }
            return false;
        }

        // ===== 강화 =====
        public int GetEnhanceCost(int skillId)
        {
            int lv = GetEnhanceLevel(skillId);
            return 10 * (lv + 1);
        }

        public bool CanEnhance(int skillId)
        {
            var so = GetSkillById(skillId);
            if (so == null) return false;
            if (GetEnhanceLevel(skillId) >= so.maxEnhanceLevel) return false;
            EconomyBridge.TryGetAmount(eCurrency.ArcaneKnowledge, out int ak);
            return ak >= GetEnhanceCost(skillId);
        }

        public bool Enhance(int skillId)
        {
            if (!CanEnhance(skillId)) return false;
            int cost = GetEnhanceCost(skillId);
            EconomyBridge.Add(eCurrency.ArcaneKnowledge, -cost);

            if (!_enhanceLevels.ContainsKey(skillId)) _enhanceLevels[skillId] = 0;
            _enhanceLevels[skillId]++;

            if (!_totalAKSpent.ContainsKey(skillId)) _totalAKSpent[skillId] = 0;
            _totalAKSpent[skillId] += cost;

            Save();
            OnStateChanged?.Invoke();
            return true;
        }

        // ===== 각성 =====
        public int GetAwakeningCost(int skillId)
        {
            int lv = GetAwakeningLevel(skillId);
            return lv + 1;
        }

        public bool CanAwaken(int skillId)
        {
            var so = GetSkillById(skillId);
            if (so == null) return false;
            if (GetAwakeningLevel(skillId) >= so.maxAwakeningLevel) return false;
            return GetFragments(skillId) >= GetAwakeningCost(skillId);
        }

        public bool Awaken(int skillId)
        {
            if (!CanAwaken(skillId)) return false;
            int cost = GetAwakeningCost(skillId);

            _fragments[skillId] -= cost;

            if (!_awakeningLevels.ContainsKey(skillId)) _awakeningLevels[skillId] = 0;
            _awakeningLevels[skillId]++;

            Save();
            OnStateChanged?.Invoke();
            return true;
        }

        // ===== 초기화 =====
        public bool CanReset(int skillId) => GetEnhanceLevel(skillId) > 0;

        public int GetResetRefund(int skillId)
        {
            int spent = GetTotalAKSpent(skillId);
            return Mathf.FloorToInt(spent * 0.8f);
        }

        public bool ResetEnhance(int skillId)
        {
            if (!CanReset(skillId)) return false;
            int refund = GetResetRefund(skillId);
            EconomyBridge.Add(eCurrency.ArcaneKnowledge, refund);
            _enhanceLevels[skillId] = 0;
            _totalAKSpent[skillId] = 0;
            Save();
            OnStateChanged?.Invoke();
            return true;
        }

        // ===== 저장/로드 =====
        [Serializable]
        private class SaveData
        {
            public int[] equipped;
            public List<int> eKeys = new();
            public List<int> eVals = new();
            public List<int> aKeys = new();
            public List<int> aVals = new();
            public List<int> fKeys = new();
            public List<int> fVals = new();
            public List<int> sKeys = new();
            public List<int> sVals = new();
        }

        private void Save()
        {
            var d = new SaveData { equipped = (int[])_equipped.Clone() };
            SerializeDict(_enhanceLevels, d.eKeys, d.eVals);
            SerializeDict(_awakeningLevels, d.aKeys, d.aVals);
            SerializeDict(_fragments, d.fKeys, d.fVals);
            SerializeDict(_totalAKSpent, d.sKeys, d.sVals);
            PlayerPrefs.SetString(PrefKey, JsonUtility.ToJson(d));
            PlayerPrefs.Save();
        }

        private void Load()
        {
            string raw = PlayerPrefs.GetString(PrefKey, "");
            if (string.IsNullOrEmpty(raw)) return;

            var d = JsonUtility.FromJson<SaveData>(raw);
            if (d == null) return;

            if (d.equipped != null)
            {
                int len = Mathf.Min(d.equipped.Length, SlotCount);
                for (int i = 0; i < len; i++) _equipped[i] = d.equipped[i];
            }

            DeserializeDict(d.eKeys, d.eVals, _enhanceLevels);
            DeserializeDict(d.aKeys, d.aVals, _awakeningLevels);
            DeserializeDict(d.fKeys, d.fVals, _fragments);
            DeserializeDict(d.sKeys, d.sVals, _totalAKSpent);
        }

        private static void SerializeDict(Dictionary<int, int> dict, List<int> keys, List<int> vals)
        {
            foreach (var kv in dict)
            {
                keys.Add(kv.Key);
                vals.Add(kv.Value);
            }
        }

        private static void DeserializeDict(List<int> keys, List<int> vals, Dictionary<int, int> dict)
        {
            if (keys == null || vals == null) return;
            int len = Mathf.Min(keys.Count, vals.Count);
            for (int i = 0; i < len; i++)
                dict[keys[i]] = vals[i];
        }
    }
}
