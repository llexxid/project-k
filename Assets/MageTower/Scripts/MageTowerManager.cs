using System;
using System.Collections.Generic;
using UnityEngine;
using Scripts.Core;
using Scripts.Monster;
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

        private readonly float[] _cooldowns = new float[SlotCount];
        private readonly float[] _cooldownTimers = new float[SlotCount];

        public event Action OnStateChanged;
        public event Action OnCooldownTick;

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
        }

        private void Start()
        {
            InitTestData();
        }

        private void Update()
        {
            bool ticked = false;
            for (int i = 0; i < SlotCount; i++)
            {
                if (_cooldownTimers[i] <= 0f) continue;
                _cooldownTimers[i] -= Time.deltaTime;
                if (_cooldownTimers[i] < 0f) _cooldownTimers[i] = 0f;
                ticked = true;
            }
            if (ticked)
                OnCooldownTick?.Invoke();
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

        // ===== 쿨타임 =====
        public bool IsOnCooldown(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= SlotCount) return false;
            return _cooldownTimers[slotIndex] > 0f;
        }

        public float GetCooldownRatio(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= SlotCount) return 0f;
            if (_cooldowns[slotIndex] <= 0f) return 0f;
            return Mathf.Clamp01(_cooldownTimers[slotIndex] / _cooldowns[slotIndex]);
        }

        // ===== 시전 상태 =====
        private readonly bool[] _casting = new bool[SlotCount];
        public bool IsCasting(int slotIndex) =>
            slotIndex >= 0 && slotIndex < SlotCount && _casting[slotIndex];

        public event Action<int, bool> OnCastingChanged;

        // ===== 스킬 시전 =====
        /// <summary>
        /// 화면 중앙에서 가장 가까운 몬스터를 찾아 스킬 체인을 시작한다.
        /// </summary>
        public bool CastSkill(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= SlotCount) return false;
            if (IsOnCooldown(slotIndex)) return false;
            if (_casting[slotIndex]) return false;

            int skillId = _equipped[slotIndex];
            if (skillId < 0) return false;

            var so = GetSkillById(skillId);
            if (so == null || so.prefab == null) return false;

            Vector3 targetPos = FindNearestMonsterPosition();
            if (targetPos == Vector3.zero) return false;

            _casting[slotIndex] = true;
            OnCastingChanged?.Invoke(slotIndex, true);

            var excludedPositions = new List<Vector3>();
            SpawnChain(slotIndex, so, targetPos, 1, targetPos, excludedPositions);

            return true;
        }

        private void SpawnChain(int slotIndex, MageTowerSkillSO so, Vector3 castPos,
                                int castIndex, Vector3 initialTarget,
                                List<Vector3> excludedPositions)
        {
            excludedPositions.Add(castPos);

            int skillId = so.id;
            ulong dmg = (ulong)Mathf.RoundToInt(GetEffectiveDamage(skillId));
            bool isLightning = so.castPattern == eCastPattern.RandomAroundTarget;

            Vector3 spawnPos = castPos;
            Transform centerChild = so.prefab.transform.Find("Center");
            if (centerChild != null)
                spawnPos = castPos - centerChild.localPosition;

            var go = Instantiate(so.prefab, spawnPos, Quaternion.identity);
            var proj = go.GetComponent<MageTowerSkillProjectile>();
            if (proj == null) proj = go.AddComponent<MageTowerSkillProjectile>();

            bool isLastCast = castIndex >= so.totalCasts;
            Action onHit = null;

            if (!isLastCast)
            {
                onHit = () =>
                {
                    Vector3 nextPos = GetNextCastPosition(so, initialTarget, excludedPositions);
                    if (nextPos != Vector3.zero)
                        SpawnChain(slotIndex, so, nextPos, castIndex + 1,
                                   initialTarget, excludedPositions);
                    else
                        FinishCasting(slotIndex, skillId);
                };
            }
            else
            {
                onHit = () => FinishCasting(slotIndex, skillId);
            }

            proj.Initialize(dmg, spawnPos, onHit, isLightning);
        }

        private void FinishCasting(int slotIndex, int skillId)
        {
            _casting[slotIndex] = false;
            OnCastingChanged?.Invoke(slotIndex, false);

            float cd = GetEffectiveCooldown(skillId);
            _cooldowns[slotIndex] = cd;
            _cooldownTimers[slotIndex] = cd;
        }

        private Vector3 GetNextCastPosition(MageTowerSkillSO so, Vector3 initialTarget,
                                            List<Vector3> excludedPositions)
        {
            switch (so.castPattern)
            {
                case eCastPattern.RandomAroundTarget:
                    // 라이트닝: 첫 시전 성공 시 몬스터 유무 관계없이 전부 시전
                    Vector2 offset = UnityEngine.Random.insideUnitCircle * so.chainRadius;
                    return initialTarget + new Vector3(offset.x, offset.y, 0f);

                case eCastPattern.UniqueRandomMonster:
                    // 얼음송곳: 체인할 몬스터가 없으면 중단하고 쿨다운
                    return FindRandomMonsterPosition(excludedPositions);

                default:
                    return FindNearestMonsterPosition();
            }
        }

        private static readonly List<Collider2D> _searchResults = new(32);

        /// <summary>
        /// 카메라 화면 전체를 커버하는 검색을 수행하고 결과 개수를 반환한다.
        /// worldCenter에 화면 중앙 월드 좌표가 출력된다.
        /// </summary>
        private int SearchMonstersOnScreen(out Vector3 worldCenter)
        {
            worldCenter = Vector3.zero;

            var cam = Camera.main;
            if (cam == null) return 0;

            // Perspective 카메라: z 파라미터는 카메라에서의 거리
            float camDist = Mathf.Abs(cam.transform.position.z);

            Vector3 center = cam.ScreenToWorldPoint(
                new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, camDist));
            center.z = 0f;
            worldCenter = center;

            Vector3 screenEdge = cam.ScreenToWorldPoint(
                new Vector3(Screen.width, Screen.height, camDist));
            float searchRadius = Vector2.Distance(center, (Vector2)screenEdge) + 2f;

            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(GameLayers.EnemyMask);
            filter.useLayerMask = true;
            filter.useTriggers = true;

            _searchResults.Clear();
            return Physics2D.OverlapCircle(worldCenter, searchRadius, filter, _searchResults);
        }

        /// <summary>
        /// 화면 중앙에서 가장 가까운 살아있는 몬스터의 위치를 반환한다.
        /// </summary>
        private Vector3 FindNearestMonsterPosition()
        {
            int count = SearchMonstersOnScreen(out Vector3 worldCenter);
            if (count == 0) return Vector3.zero;

            float bestDist = float.MaxValue;
            Vector3 bestPos = Vector3.zero;
            bool found = false;

            for (int i = 0; i < count; i++)
            {
                var col = _searchResults[i];
                if (col == null) continue;

                var monster = col.GetComponent<Monster>();
                if (monster != null && monster.MonAction == eMonsterAction.Dead) continue;

                float dist = Vector2.Distance(worldCenter, col.transform.position);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestPos = col.transform.position;
                    found = true;
                }
            }

            return found ? bestPos : Vector3.zero;
        }

        /// <summary>
        /// excludePositions에 포함된 위치의 몬스터를 제외한 랜덤 살아있는 몬스터의 위치를 반환한다.
        /// </summary>
        private Vector3 FindRandomMonsterPosition(List<Vector3> excludePositions)
        {
            int count = SearchMonstersOnScreen(out _);
            if (count == 0) return Vector3.zero;

            var candidates = new List<Vector3>();
            for (int i = 0; i < count; i++)
            {
                var col = _searchResults[i];
                if (col == null) continue;

                var monster = col.GetComponent<Monster>();
                if (monster != null && monster.MonAction == eMonsterAction.Dead) continue;

                Vector3 pos = col.transform.position;

                bool excluded = false;
                for (int j = 0; j < excludePositions.Count; j++)
                {
                    if (Vector2.Distance(pos, excludePositions[j]) < 0.1f)
                    {
                        excluded = true;
                        break;
                    }
                }
                if (excluded) continue;

                candidates.Add(pos);
            }

            if (candidates.Count == 0) return Vector3.zero;
            return candidates[UnityEngine.Random.Range(0, candidates.Count)];
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
