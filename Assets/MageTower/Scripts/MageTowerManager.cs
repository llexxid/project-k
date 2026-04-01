using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Scripts.Core;
using Scripts.Monster;
using Scripts.Wallets;

namespace KingdomIdle.MageTower
{
    [DefaultExecutionOrder(-950)]
    public sealed class MageTowerManager : MonoBehaviour
    {
        public static MageTowerManager Instance { get; private set; }

        [SerializeField, FormerlySerializedAs("skillList")]
        private MageTowerSkillRegistrySO skillRegistry;

        public const int SlotCount = 5;
        private const string PrefKey = "mt_save";

        private readonly int[] _equipped = new int[SlotCount];
        private readonly Dictionary<int, int> _enhanceLevels = new();
        private readonly Dictionary<int, int> _awakeningLevels = new();
        private readonly Dictionary<int, int> _fragments = new();
        private readonly Dictionary<int, int> _totalAKSpent = new();

        private readonly float[] _cooldowns = new float[SlotCount];
        private readonly float[] _cooldownTimers = new float[SlotCount];
        private bool _autoEnabled;

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

            if (_autoEnabled)
                AutoCastAll();
        }

        private void AutoCastAll()
        {
            for (int i = 0; i < SlotCount; i++)
            {
                if (_equipped[i] < 0) continue;
                if (_cooldownTimers[i] > 0f) continue;
                if (_casting[i]) continue;
                CastSkill(i);
            }
        }

        public bool IsAutoEnabled() => _autoEnabled;
        public void SetAutoEnabled(bool enabled) => _autoEnabled = enabled;

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
            if (skillRegistry == null || skillRegistry.skills == null)
                return Array.Empty<MageTowerSkillSO>();
            return skillRegistry.skills;
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

        public void AddFragments(int skillId, int amount)
        {
            if (!_fragments.ContainsKey(skillId))
                _fragments[skillId] = 0;
            _fragments[skillId] += amount;
            Save();
        }

        public int GetTotalAKSpent(int skillId) =>
            _totalAKSpent.TryGetValue(skillId, out int s) ? s : 0;

        // ===== 스탯 계산 =====
        public float GetEffectiveDamage(int skillId)
        {
            var so = GetSkillById(skillId);
            if (so == null) return 0f;
            int eLv = GetEnhanceLevel(skillId);
            int aLv = GetAwakeningLevel(skillId);
            return so.BaseDamage * (1f + 0.05f * eLv) * (1f + 0.05f * aLv);
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
        /// 화면 내 몬스터를 찾아 스킬을 시전한다.
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

            // 화면에 몬스터가 없으면 시전 불가
            Vector3 targetPos = FindNearestMonsterPosition(out int nearestId);
            if (targetPos == Vector3.zero) return false;

            _casting[slotIndex] = true;
            OnCastingChanged?.Invoke(slotIndex, true);

            if (so.GetEffect<SkillEffect_FireTornado>() != null)
            {
                SpawnPersistent(slotIndex, so);
            }
            else
            {
                var excludedIds = new HashSet<int>();
                if (nearestId != 0) excludedIds.Add(nearestId);
                SpawnChain(slotIndex, so, targetPos, 1, targetPos, excludedIds);
            }

            return true;
        }

        private void SpawnChain(int slotIndex, MageTowerSkillSO so, Vector3 castPos,
                                int castIndex, Vector3 initialTarget,
                                HashSet<int> excludedIds)
        {
            int skillId = so.id;
            ulong dmg = (ulong)Mathf.RoundToInt(GetEffectiveDamage(skillId));

            Vector3 spawnPos = castPos;
            Transform centerChild = so.prefab.transform.Find("Center");
            if (centerChild != null)
                spawnPos = castPos - centerChild.localPosition;

            var go = Instantiate(so.prefab, spawnPos, Quaternion.identity);
            var proj = go.GetComponent<MageTowerSkillProjectile>();
            if (proj == null) proj = go.AddComponent<MageTowerSkillProjectile>();

            // 스킬별 고유 SO에서 totalCasts와 투사체 파라미터 읽기
            int totalCasts = 1;
            float damageRadius = 1.5f;
            bool shakeOnHit = false;
            float shakeDuration = 0.15f;
            float shakeMagnitude = 0.08f;

            var lightning = so.GetEffect<SkillEffect_Lightning>();
            if (lightning != null)
            {
                totalCasts = lightning.totalCasts;
                damageRadius = lightning.damageRadius;
                shakeOnHit = lightning.shakeOnHit;
                shakeDuration = lightning.shakeDuration;
                shakeMagnitude = lightning.shakeMagnitude;
            }
            else
            {
                var iceSpike = so.GetEffect<SkillEffect_IceSpike>();
                if (iceSpike != null)
                {
                    totalCasts = iceSpike.totalCasts;
                    damageRadius = iceSpike.damageRadius;
                }
            }

            bool isLastCast = castIndex >= totalCasts;
            Action onHit = null;

            if (!isLastCast)
            {
                onHit = () =>
                {
                    Vector3 nextPos = GetNextCastPosition(so, initialTarget, excludedIds);
                    if (nextPos != Vector3.zero)
                        SpawnChain(slotIndex, so, nextPos, castIndex + 1,
                                   initialTarget, excludedIds);
                    else
                        FinishCasting(slotIndex, skillId);
                };
            }
            else
            {
                onHit = () => FinishCasting(slotIndex, skillId);
            }

            proj.Initialize(dmg, spawnPos, onHit, damageRadius, shakeOnHit,
                            shakeDuration, shakeMagnitude);
        }

        private void SpawnPersistent(int slotIndex, MageTowerSkillSO so)
        {
            // 화면 내 랜덤 몬스터를 타겟으로 선택
            Transform target = FindRandomMonsterTransform();
            if (target == null)
            {
                FinishCasting(slotIndex, so.id);
                return;
            }

            var go = Instantiate(so.prefab, target.position, Quaternion.identity);
            var persistent = go.GetComponent<MageTowerSkillPersistent>();
            if (persistent == null)
                persistent = go.AddComponent<MageTowerSkillPersistent>();

            ulong dmg = (ulong)Mathf.RoundToInt(GetEffectiveDamage(so.id));
            var fire = so.GetEffect<SkillEffect_FireTornado>();
            float duration = fire != null ? fire.duration : 0f;
            float tickInterval = fire != null ? fire.tickInterval : 0f;
            float moveSpeed = fire != null ? fire.moveSpeed : 8f;
            float arrivalThreshold = fire != null ? fire.arrivalThreshold : 0.05f;
            persistent.Initialize(dmg, duration, tickInterval, moveSpeed,
                                  arrivalThreshold, slotIndex, so.id, target);
        }

        /// <summary>
        /// 외부(MageTowerSkillPersistent 등)에서 시전 종료를 알릴 때 사용.
        /// </summary>
        public void EndCasting(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= SlotCount) return;
            int skillId = _equipped[slotIndex];
            if (skillId < 0) return;
            FinishCasting(slotIndex, skillId);
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
                                            HashSet<int> excludedIds)
        {
            // 라이트닝: 첫 시전 성공 시 몬스터 유무 관계없이 전부 시전
            var lightningEff = so.GetEffect<SkillEffect_Lightning>();
            if (lightningEff != null)
            {
                Vector2 offset = UnityEngine.Random.insideUnitCircle * lightningEff.chainRadius;
                return initialTarget + new Vector3(offset.x, offset.y, 0f);
            }

            // 얼음송곳: 체인할 몬스터가 없으면 중단하고 쿨다운
            var iceSpikeEff = so.GetEffect<SkillEffect_IceSpike>();
            if (iceSpikeEff != null)
            {
                Vector3 pos = FindRandomMonsterPosition(excludedIds, out int newId);
                if (pos != Vector3.zero && newId != 0)
                    excludedIds.Add(newId);
                return pos;
            }

            return FindNearestMonsterPosition();
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
            return FindNearestMonsterPosition(out _);
        }

        private Vector3 FindNearestMonsterPosition(out int instanceId)
        {
            instanceId = 0;
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
                    instanceId = col.gameObject.GetInstanceID();
                    found = true;
                }
            }

            return found ? bestPos : Vector3.zero;
        }

        /// <summary>
        /// excludeIds에 포함된 인스턴스ID의 몬스터를 제외한 랜덤 살아있는 몬스터의 위치를 반환한다.
        /// </summary>
        private Vector3 FindRandomMonsterPosition(HashSet<int> excludeIds, out int selectedId)
        {
            selectedId = 0;
            int count = SearchMonstersOnScreen(out _);
            if (count == 0) return Vector3.zero;

            var candidates = new List<(Vector3 pos, int id)>();
            for (int i = 0; i < count; i++)
            {
                var col = _searchResults[i];
                if (col == null) continue;

                var monster = col.GetComponent<Monster>();
                if (monster != null && monster.MonAction == eMonsterAction.Dead) continue;

                int id = col.gameObject.GetInstanceID();
                if (excludeIds != null && excludeIds.Contains(id)) continue;

                candidates.Add((col.transform.position, id));
            }

            if (candidates.Count == 0) return Vector3.zero;
            var chosen = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            selectedId = chosen.id;
            return chosen.pos;
        }

        /// <summary>
        /// 화면 내 랜덤 살아있는 몬스터의 Transform을 반환한다.
        /// </summary>
        private Transform FindRandomMonsterTransform()
        {
            int count = SearchMonstersOnScreen(out _);
            if (count == 0) return null;

            var candidates = new List<Transform>();
            for (int i = 0; i < count; i++)
            {
                var col = _searchResults[i];
                if (col == null) continue;

                var monster = col.GetComponent<Monster>();
                if (monster != null && monster.MonAction == eMonsterAction.Dead) continue;

                candidates.Add(col.transform);
            }

            if (candidates.Count == 0) return null;
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

        // ===== 서버 동기화 =====

        /// <summary>
        /// 모든 스킬 상태를 64비트 배열로 패킹한다. 서버 전송용.
        /// </summary>
        public long[] PackAllSkills()
        {
            var skills = GetAllSkills();
            var result = new long[skills.Count];
            for (int i = 0; i < skills.Count; i++)
            {
                int id = skills[i].id;
                result[i] = MageTowerSkillCode.Pack(
                    id,
                    GetAwakeningLevel(id),
                    GetEnhanceLevel(id),
                    GetFragments(id));
            }
            return result;
        }

        /// <summary>
        /// 64비트 배열에서 스킬 상태를 복원한다. 서버 로드용.
        /// </summary>
        public void UnpackAllSkills(long[] packed)
        {
            if (packed == null) return;

            _enhanceLevels.Clear();
            _awakeningLevels.Clear();
            _fragments.Clear();

            for (int i = 0; i < packed.Length; i++)
            {
                int id  = MageTowerSkillCode.UnpackSkillId(packed[i]);
                int aLv = MageTowerSkillCode.UnpackAwakeningLevel(packed[i]);
                int eLv = MageTowerSkillCode.UnpackEnhanceLevel(packed[i]);
                int qty = MageTowerSkillCode.UnpackQuantity(packed[i]);

                if (eLv > 0) _enhanceLevels[id] = eLv;
                if (aLv > 0) _awakeningLevels[id] = aLv;
                if (qty > 0) _fragments[id] = qty;
            }

            Save();
            OnStateChanged?.Invoke();
        }

        /// <summary>
        /// 단일 스킬의 64비트 코드를 반환한다.
        /// </summary>
        public long PackSkill(int skillId)
        {
            return MageTowerSkillCode.Pack(
                skillId,
                GetAwakeningLevel(skillId),
                GetEnhanceLevel(skillId),
                GetFragments(skillId));
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
