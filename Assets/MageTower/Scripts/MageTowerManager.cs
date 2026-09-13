using System.Collections;
using Scripts.Core.Manager;
using KingdomIdle.Balance;
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

        private int[] _equipped => LocalProgression.State.MageSlots;
        private MageSave Saved(int id) => LocalProgression.State.MageSkills.TryGetValue(id, out var value) ? value : null;
        public void NotifyCommitted() => OnStateChanged?.Invoke();

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

            _autoEnabled = true;
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
            // 화면 안 몬스터 프리체크는 '시전 가능한 슬롯이 실제로 있을 때' 1회만 —
            // 전 슬롯이 쿨다운 중인 평상시 프레임에는 물리 쿼리를 아예 하지 않고,
            // 몬스터가 전부 화면 밖인 프레임에는 슬롯별(최대 5회) 재탐색 대신 1회로 끝낸다.
            bool prechecked = false;
            for (int i = 0; i < SlotCount; i++)
            {
                if (_equipped[i] < 0) continue;
                if (_cooldownTimers[i] > 0f) continue;
                if (_casting[i]) continue;

                if (!prechecked)
                {
                    if (!AnyMonsterOnScreen()) return;
                    prechecked = true;
                }
                CastSkill(i);
            }
        }

        /// <summary>화면(뷰포트) 안에 살아있는 몬스터가 하나라도 있는지 — AutoCastAll 프레임 프리체크.</summary>
        private bool AnyMonsterOnScreen()
        {
            int count = SearchMonstersOnScreen(out _);
            if (count == 0) return false;

            var cam = MageTowerTargeting.ResolveCamera();
            for (int i = 0; i < count; i++)
            {
                var col = _searchResults[i];
                if (col == null) continue;

                var monster = col.GetComponent<Monster>();
                if (monster != null && monster.MonAction == eMonsterAction.Dead) continue;

                if (!MageTowerTargeting.IsOnScreen(cam, col.transform.position)) continue;
                return true;
            }
            return false;
        }

        public bool IsAutoEnabled() => _autoEnabled;
        public void SetAutoEnabled(bool enabled) => _autoEnabled = enabled;

        public bool IsOwned(int skillId) => Saved(skillId) != null;

        public void Unlock(int skillId)
        {
            if (GetSkillById(skillId) == null) return;
            if (LocalProgression.Execute("mage-grant", state => { Grant(state, skillId); return true; })) NotifyCommitted();
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

        public int GetEnhanceLevel(int id) => Saved(id)?.Enhance ?? 0;
        public int GetAwakeningLevel(int id) => Saved(id)?.Awaken ?? 0;
        public int GetFragments(int id) => Saved(id)?.Fragments ?? 0;
        public long GetTotalAKSpent(int id) => Saved(id)?.Spent ?? 0;
        public static void Grant(ProgressionState state, int skillId)
        {
            if (state.MageSkills.TryGetValue(skillId, out var skill)) skill.Fragments = checked(skill.Fragments + 1);
            else state.MageSkills[skillId] = new MageSave();
        }
        public void AddFragments(int skillId, int amount)
        {
            if (amount <= 0 || GetSkillById(skillId) == null) return;
            if (LocalProgression.Execute("mage-fragments", s => {
                if (!s.MageSkills.TryGetValue(skillId, out var skill)) s.MageSkills[skillId] = skill = new MageSave();
                skill.Fragments = checked(skill.Fragments + amount); return true;
            })) NotifyCommitted();
        }

        // ===== 스탯 계산 =====
        public long GetEffectiveDamage(int skillId)
        {
            var so = GetSkillById(skillId);
            if (so == null) return 0;
            int eLv = GetEnhanceLevel(skillId);
            int aLv = GetAwakeningLevel(skillId);
            return BalanceMath.MageDamage((long)so.BaseDamage, eLv, aLv);
        }

        public float GetEffectiveCooldown(int skillId)
        {
            var so = GetSkillById(skillId);
            if (so == null) return 0;
            int aLv = GetAwakeningLevel(skillId);
            return (float)BalanceMath.MageInterval((decimal)so.baseCooldown, aLv);
        }

        // ===== 장착 =====
        public bool Equip(int slotIndex, int skillId)
        {
            if (slotIndex >= 0 && slotIndex < SlotCount && (_casting[slotIndex] || _cooldownTimers[slotIndex] > 0)) return false;
            if (slotIndex < 0 || slotIndex >= SlotCount || GetSkillById(skillId) == null || !IsOwned(skillId)) return false;
            if (IsEquipped(skillId)) return false;
            bool result = LocalProgression.Execute("mage-equip", state => { state.MageSlots[slotIndex] = skillId; return true; });
            if (result) NotifyCommitted(); return result;
    }

        public void Unequip(int slotIndex)
        {
            if (slotIndex >= 0 && slotIndex < SlotCount && (_casting[slotIndex] || _cooldownTimers[slotIndex] > 0)) return;
            if (slotIndex < 0 || slotIndex >= SlotCount || _casting[slotIndex]) return;
            if (LocalProgression.Execute("mage-unequip", state => { state.MageSlots[slotIndex] = -1; return true; })) NotifyCommitted();
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
            return (int)(BalanceMath.MageCost(GetEnhanceLevel(skillId)) ?? -1);
    }

        public bool CanEnhance(int skillId)
        {
            var so = GetSkillById(skillId);
            if (so == null) return false;
            if (!IsOwned(skillId)) return false;
            if (GetEnhanceLevel(skillId) >= BalanceMath.MageCap) return false;
            EconomyBridge.TryGetAmount(eCurrency.ArcaneKnowledge, out long ak);
            return ak >= GetEnhanceCost(skillId);
        }

        public bool Enhance(int skillId)
        {
            if (!CanEnhance(skillId)) return false;
            bool result = LocalProgression.Execute("mage-enhance", state => {
                if (!state.MageSkills.TryGetValue(skillId, out var skill)) return false;
                long? cost = BalanceMath.MageCost(skill.Enhance);
                if (!cost.HasValue || !LocalProgression.Spend(state, eCurrency.ArcaneKnowledge, cost.Value)) return false;
                skill.Enhance++; skill.Spent = checked(skill.Spent + cost.Value); return true;
            });
            if (result) NotifyCommitted(); return result;
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
            if (!IsOwned(skillId) || GetAwakeningLevel(skillId) >= 10) return false;
            return GetFragments(skillId) >= GetAwakeningCost(skillId);
        }

        public bool Awaken(int skillId)
        {
            if (!CanAwaken(skillId)) return false;
            bool result = LocalProgression.Execute("mage-awaken", state => {
                var skill = state.MageSkills[skillId];
                int cost = skill.Awaken + 1;
                if (skill.Awaken >= 10 || skill.Fragments < cost) return false;
                skill.Fragments -= cost; skill.Awaken++; return true;
            });
            if (result) NotifyCommitted(); return result;
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
            if (StageManager.Instance?.CurrentRunState != eStageRunState.Running) return false;
            if (IsOnCooldown(slotIndex)) return false;
            if (_casting[slotIndex]) return false;

            int skillId = _equipped[slotIndex];
            if (skillId < 0) return false;

            var so = GetSkillById(skillId);
            if (so == null || so.prefab == null) return false;

            // 화면에 몬스터가 없으면 시전 불가
            Vector3 targetPos = FindNearestMonsterPosition(out int nearestId);
            if (targetPos == Vector3.zero) return false;

            if (!LocalProgression.Execute("mage-cast", state => { QuestEconomy.Count(state,eQuestObjectiveType.SkillCast,skillId,1); return true; })) return false;
            _casting[slotIndex] = true;
            _cooldowns[slotIndex] = _cooldownTimers[slotIndex] = GetEffectiveCooldown(skillId);
            OnCastingChanged?.Invoke(slotIndex, true);

            if (so.GetEffect<SkillEffect_FireTornado>() != null)
            {
                SpawnPersistent(slotIndex, so);
            }
            else
            {
                var excludedIds = new HashSet<int>();
                if (nearestId != 0) excludedIds.Add(nearestId);
                StartCoroutine(CastSeries(slotIndex, so, targetPos, excludedIds));
            }

            return true;
        }

        private readonly ulong[] _castDamage = new ulong[SlotCount];
        private IEnumerator CastSeries(int slot, MageTowerSkillSO skill, Vector3 initial, HashSet<int> excluded)
        {
            string battle = LocalProgression.State.ActiveBattleId;
            int hits = BalanceMath.MageHits(skill.id == 0 ? 3 : 4, GetAwakeningLevel(skill.id), false);
            _castDamage[slot] = checked((ulong)GetEffectiveDamage(skill.id));
            double elapsed = 0, duration = skill.id == 0 ? .6 : .9;
            int emitted = 0;
            while (emitted < hits && LocalProgression.State.ActiveBattleId == battle)
            {
                while (emitted < hits && elapsed + .000001 >= duration * emitted / (hits - 1))
                {
                    Vector3 position = emitted == 0 || skill.id == 0 ? initial : GetNextCastPosition(skill,initial,excluded);
                    if (position != Vector3.zero) SpawnChain(slot,skill,position,emitted+1,initial,excluded);
                    emitted++;
                }
                yield return null; elapsed += Time.deltaTime;
            }
            FinishCasting(slot,skill.id);
        }

        private void SpawnChain(int slotIndex, MageTowerSkillSO so, Vector3 castPos,
                                int castIndex, Vector3 initialTarget,
                                HashSet<int> excludedIds)
        {
{
            // Each scheduled strike owns its visual; damage is emitted explicitly once.
            Vector3 spawnPos = castPos;
            Transform center = so.prefab.transform.Find("Center"); if (center != null) spawnPos -= center.localPosition;
            var go = Instantiate(so.prefab, spawnPos, Quaternion.identity);
            var projectile = go.GetComponent<MageTowerSkillProjectile>() ?? go.AddComponent<MageTowerSkillProjectile>();
            projectile.Initialize(_castDamage[slotIndex],spawnPos,null, so.id == 0 ? .5f : .35f,false,.15f,.08f,so.sfxName);
            projectile.OnHit();
        }
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

            if (!string.IsNullOrEmpty(so.sfxName) &&
                System.Enum.TryParse(so.sfxName, out eSFXType fireSfxType))
            {
                SFXManager.Instance.GetSFX(
                    fireSfxType, target.position, Quaternion.identity, sfx => sfx.PlaySFX());
            }

            ulong dmg = checked((ulong)GetEffectiveDamage(so.id));
            var fire = so.GetEffect<SkillEffect_FireTornado>();
            float duration = 5f + GetAwakeningLevel(so.id) / 4;
            float tickInterval = fire != null ? fire.tickInterval : 0f;
            float moveSpeed = fire != null ? fire.moveSpeed : 8f;
            float arrivalThreshold = fire != null ? fire.arrivalThreshold : 0.05f;
            persistent.Initialize(dmg, duration, tickInterval, moveSpeed,
                                  arrivalThreshold, slotIndex, so.id, target, so.sfxLoopName);
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


        }

        private Vector3 GetNextCastPosition(MageTowerSkillSO so, Vector3 initialTarget,
                                            HashSet<int> excludedIds)
        {
            // 라이트닝: 첫 시전 성공 시 몬스터 유무 관계없이 전부 시전
            var lightningEff = so.GetEffect<SkillEffect_Lightning>();
            if (lightningEff != null)
            {
                return initialTarget;
            }

            // 얼음송곳: 체인할 몬스터가 없으면 중단하고 쿨다운
            var iceSpikeEff = so.GetEffect<SkillEffect_IceSpike>();
            if (iceSpikeEff != null)
            {
                Vector3 pos = FindRandomMonsterPosition(excludedIds, out int newId);
                if (newId == 0) pos = FindNearestMonsterPosition(out newId);
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

            var cam = MageTowerTargeting.ResolveCamera();
            float bestDist = float.MaxValue;
            Vector3 bestPos = Vector3.zero;
            bool found = false;

            for (int i = 0; i < count; i++)
            {
                var col = _searchResults[i];
                if (col == null) continue;

                var monster = col.GetComponent<Monster>();
                if (monster != null && monster.MonAction == eMonsterAction.Dead) continue;

                // 뷰포트 밖 몬스터 제외 — 외접원 광역 쿼리가 화면 밖 띠까지 잡는다
                if (!MageTowerTargeting.IsOnScreen(cam, col.transform.position)) continue;

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
        // 시전 중 재사용 스크래치 (AutoCastAll 경로에서 매 시전마다 리스트를 새로 만들지 않게)
        private static readonly List<(Vector3 pos, int id)> _randomPosCandidates = new(32);

        private Vector3 FindRandomMonsterPosition(HashSet<int> excludeIds, out int selectedId)
        {
            selectedId = 0;
            int count = SearchMonstersOnScreen(out _);
            if (count == 0) return Vector3.zero;

            var cam = MageTowerTargeting.ResolveCamera();
            _randomPosCandidates.Clear();
            for (int i = 0; i < count; i++)
            {
                var col = _searchResults[i];
                if (col == null) continue;

                var monster = col.GetComponent<Monster>();
                if (monster != null && monster.MonAction == eMonsterAction.Dead) continue;

                // 뷰포트 밖 몬스터 제외 — 얼음송곳 체인이 화면 밖에 생성되지 않게
                if (!MageTowerTargeting.IsOnScreen(cam, col.transform.position)) continue;

                int id = col.gameObject.GetInstanceID();
                if (excludeIds != null && excludeIds.Contains(id)) continue;

                _randomPosCandidates.Add((col.transform.position, id));
            }

            if (_randomPosCandidates.Count == 0) return Vector3.zero;
            var chosen = _randomPosCandidates[UnityEngine.Random.Range(0, _randomPosCandidates.Count)];
            selectedId = chosen.id;
            return chosen.pos;
        }

        /// <summary>
        /// 화면 내 랜덤 살아있는 몬스터의 Transform을 반환한다.
        /// </summary>
        private static readonly List<Transform> _randomTransformCandidates = new(32);

        private Transform FindRandomMonsterTransform()
        {
            int count = SearchMonstersOnScreen(out _);
            if (count == 0) return null;

            var cam = MageTowerTargeting.ResolveCamera();
            _randomTransformCandidates.Clear();
            for (int i = 0; i < count; i++)
            {
                var col = _searchResults[i];
                if (col == null) continue;

                var monster = col.GetComponent<Monster>();
                if (monster != null && monster.MonAction == eMonsterAction.Dead) continue;

                // 뷰포트 밖 몬스터 제외 — 화염폭풍 최초 대상도 화면 안에서만 고른다
                if (!MageTowerTargeting.IsOnScreen(cam, col.transform.position)) continue;

                _randomTransformCandidates.Add(col.transform);
            }

            if (_randomTransformCandidates.Count == 0) return null;
            return _randomTransformCandidates[UnityEngine.Random.Range(0, _randomTransformCandidates.Count)];
        }

        public bool CanReset(int id) => GetEnhanceLevel(id) > 0;
        public long GetResetRefund(int id) => BalanceMath.Floor(GetTotalAKSpent(id) * .8m);
        public bool ResetEnhance(int id)
        {
            if (!CanReset(id)) return false;
            bool result = LocalProgression.Execute("mage-reset", s => {
                var skill = s.MageSkills[id];
                LocalProgression.Credit(s, eCurrency.ArcaneKnowledge, BalanceMath.Floor(skill.Spent * .8m));
                skill.Enhance = 0; skill.Spent = 0; return true;
            });
            if (result) NotifyCommitted(); return result;
        }
        public long PackSkill(int id) => MageTowerSkillCode.Pack(id, GetAwakeningLevel(id), GetEnhanceLevel(id), GetFragments(id), IsOwned(id) ? 1 : 0);
        public long[] PackAllSkills()
        {
            var result = new List<long>(); foreach (var skill in GetAllSkills()) if (IsOwned(skill.id)) result.Add(PackSkill(skill.id)); return result.ToArray();
        }
        public void UnpackAllSkills(long[] packed)
        {
            if (packed == null || LocalProgression.State.Modules.ContainsKey("mage-imported")) return;
            if (LocalProgression.Execute("mage-import-once", s => {
                foreach (long code in packed)
                {
                    int id = MageTowerSkillCode.UnpackSkillId(code);
                    if (GetSkillById(id) == null) continue;
                    s.MageSkills[id] = new MageSave { Enhance = BalanceMath.Clamp(MageTowerSkillCode.UnpackEnhanceLevel(code), 0, 100),
                        Awaken = BalanceMath.Clamp(MageTowerSkillCode.UnpackAwakeningLevel(code), 0, 10), Fragments = MageTowerSkillCode.UnpackQuantity(code) };
                    // No inferred historical spend: imported levels cannot manufacture reset refunds.
                }
                s.Modules["mage-imported"] = "1"; return true;
            })) NotifyCommitted();
        }
    }
}
