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
        private readonly MageTowerSpellCast[] _activeSpells = new MageTowerSpellCast[SlotCount];

        private int[] _equipped => LocalProgression.State.MageSlots;
        private MageSave Saved(int id) => LocalProgression.State.MageSkills.TryGetValue(id, out var value) ? value : null;
        public void NotifyCommitted() => OnStateChanged?.Invoke();

        private readonly float[] _cooldowns = new float[SlotCount];
        private readonly float[] _cooldownTimers = new float[SlotCount];
        private readonly float[] _skillCooldowns = new float[MageSkillRules.SkillCount];
        private readonly float[] _skillCooldownTimers = new float[MageSkillRules.SkillCount];
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
            for (int id = 0; id < _skillCooldownTimers.Length; id++)
                if (_skillCooldownTimers[id] > 0) { _skillCooldownTimers[id] = Mathf.Max(0, _skillCooldownTimers[id] - Time.deltaTime); ticked = true; }
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
                if (IsOnCooldown(i)) continue;
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
        private bool AnyMonsterOnScreen() => MageTowerSpellCast.TryFindTarget(out _);

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
        public static void Grant(ProgressionState state, int skillId, int duplicateFragments = 1)
        {
            if (state.MageSkills.TryGetValue(skillId, out var skill)) skill.Fragments = checked(skill.Fragments + duplicateFragments);
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
            return (float)BalanceMath.MageInterval((decimal)so.baseCooldown, aLv) * (IsBloomEnabled(skillId) ? so.bloomCooldownMultiplier : 1f);
        }

        // ===== 장착 =====
        public bool Equip(int slotIndex, int skillId)
        {
            if (slotIndex < 0 || slotIndex >= SlotCount || GetSkillById(skillId) == null || !IsOwned(skillId)) return false;
            if (_equipped[slotIndex] == skillId) return false;
            bool result = LocalProgression.Execute("mage-equip", state => {
                int previous = state.MageSlots[slotIndex];
                // One atomic swap, never a transient duplicate or an intermediate empty loadout.
                for (int i = 0; i < SlotCount; i++) if (state.MageSlots[i] == skillId) state.MageSlots[i] = previous;
                state.MageSlots[slotIndex] = skillId; return true;
            });
            if (result) NotifyCommitted(); return result;
    }

        public bool Unequip(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= SlotCount || _equipped[slotIndex] < 0) return false;
            bool result = LocalProgression.Execute("mage-unequip", state => { state.MageSlots[slotIndex] = -1; return true; });
            if (result) NotifyCommitted(); return result;
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
            int id = _equipped[slotIndex];
            return _cooldownTimers[slotIndex] > 0f || (id >= 0 && _skillCooldownTimers[id] > 0f);
        }

        public float GetCooldownRatio(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= SlotCount) return 0f;
            int id = _equipped[slotIndex];
            float slot = _cooldowns[slotIndex] > 0 ? _cooldownTimers[slotIndex] / _cooldowns[slotIndex] : 0;
            float skill = id >= 0 && _skillCooldowns[id] > 0 ? _skillCooldownTimers[id] / _skillCooldowns[id] : 0;
            return Mathf.Clamp01(Mathf.Max(slot,skill));
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
            if (slotIndex < 0 || slotIndex >= SlotCount || StageManager.Instance?.CurrentRunState != eStageRunState.Running || IsOnCooldown(slotIndex) || _casting[slotIndex]) return false;
            int skillId = _equipped[slotIndex];
            var skill = GetSkillById(skillId);
            if (skill == null || skill.prefab == null || !IsOwned(skillId) || !MageTowerSpellCast.TryFindTarget(skill, out var target)) return false;
            if (skill.IsHealing && !MageTowerSpellCast.NeedsHealing()) return false;
            LocalProgression.RecordSkillCast(skillId);
            _casting[slotIndex] = true;
            _cooldowns[slotIndex] = _cooldownTimers[slotIndex] = GetEffectiveCooldown(skillId);
            _skillCooldowns[skillId] = _skillCooldownTimers[skillId] = _cooldownTimers[slotIndex];
            var cast = new MageTowerSpellCast(this, skill, GetEffectiveDamage(skillId), GetAwakeningLevel(skillId), IsBloomEnabled(skillId), target);
            _activeSpells[slotIndex] = cast;
            OnCastingChanged?.Invoke(slotIndex, true);
            StartCoroutine(RunCast(slotIndex, cast));
            return true;
        }

        private IEnumerator RunCast(int slot, MageTowerSpellCast cast)
        {
            try { yield return cast.Run(); }
            finally
            {
                cast.Dispose();
                if (_activeSpells[slot] == cast) { _activeSpells[slot] = null; EndCasting(slot); }
            }
        }

        // Retained for old visual components and existing UI bindings.
        public void EndCasting(int slot)
        {
            if (slot < 0 || slot >= SlotCount) return;
            _casting[slot] = false; OnCastingChanged?.Invoke(slot, false);
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            for (int i = 0; i < SlotCount; i++) { _activeSpells[i]?.Dispose(); _activeSpells[i] = null; _casting[i] = false; }
        }
        private void OnDestroy() { OnDisable(); if (Instance == this) Instance = null; }

        public bool IsBloomUnlocked(int id) => IsOwned(id) && GetAwakeningLevel(id) >= MageSkillRules.BloomAwakening;
        public bool IsBloomEnabled(int id) => IsBloomUnlocked(id) && Saved(id).BloomEnabled;
        // A cast snapshots its mode and cooldown. Switching affects the next cast only.
        public bool CanSetBloom(int id) => IsBloomUnlocked(id);
        public bool SetBloomEnabled(int id, bool enabled)
        {
            if (!CanSetBloom(id)) return false;
            bool result = LocalProgression.Execute("mage-bloom", state => {
                if (!state.MageSkills.TryGetValue(id, out var skill) || skill.Awaken < MageSkillRules.BloomAwakening) return false;
                skill.BloomEnabled = enabled; return true;
            });
            if (result) NotifyCommitted(); return result;
        }
        public decimal SingleTargetDps(int id)
        {
            var skill = GetSkillById(id); if (skill == null) return 0;
            return GetEffectiveDamage(id) * MageSkillRules.SingleTargetPowerUnits(skill, GetAwakeningLevel(id), IsBloomEnabled(id)) / (decimal)GetEffectiveCooldown(id);
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
