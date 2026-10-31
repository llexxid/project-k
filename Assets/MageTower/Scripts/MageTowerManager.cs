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
using PlayFab;
using Protocol.Message.Dto;

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
        private readonly float[] _skillCooldowns = new float[MageSkillRules.IdCapacity];
        private readonly float[] _skillCooldownTimers = new float[MageSkillRules.IdCapacity];
        private bool _autoEnabled;

        public event Action OnStateChanged;
        public event Action OnCooldownTick;

        /// <summary>서버 마탑 조작의 결과. Pending을 돌려준 뒤 OnActionCompleted로 결과를 알린다. 나머지는 안내용 가드다(최종 판정은 서버).</summary>
        public enum MageActionResult { Pending, Busy, NetworkNotReady, InvalidRequest, NotOwned, MaxLevel, NotEnough, NotUnlocked, NoChange }
        public enum MageActionKind { Enhance, Awaken, Loadout, Bloom, Reset }
        /// <summary>완료 이벤트 인자. 지출·환급은 서버 응답 값이고 표시 전용이다.</summary>
        public sealed class MageActionInfo
        {
            public MageActionKind Kind;
            public int SkillId = -1;
            public bool Ok;
            public long SpentArcaneKnowledge, RefundedArcaneKnowledge;
            public int SpentFragments;
            public int ErrorCode;
            public string Failure;
            public string LogLine;
        }
        /// <summary>서버 응답을 기다리는 중. 같은 버튼 연타는 매니저가 막는다(NetworkManager 큐는 1017만 막는다).</summary>
        public bool IsBusy { get; private set; }
        /// <summary>마지막 서버 거절 또는 적용 실패. Business 오류면 CustomData에 서버 ErrorResponseDTO가 들어 있다.</summary>
        public PlayFabError LastFailure { get; private set; }
        public event Action<bool> OnBusyChanged;
        public event Action<MageActionInfo> OnActionCompleted;

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
            // TryCast already rejects these states; avoid scanning targets every idle frame.
            if (StageManager.Instance?.CurrentRunState != eStageRunState.Running) return;
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
        public void SetAutoEnabled(bool enabled) { if (_autoEnabled == enabled) return; _autoEnabled = enabled; OnStateChanged?.Invoke(); }

        public bool IsOwned(int skillId) => MageSkillRules.IsAvailable(skillId) && Saved(skillId) != null;

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
            if (!MageSkillRules.IsAvailable(id)) return null;
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
            return MageSkillRules.IsAvailable(_equipped[slotIndex]) ? _equipped[slotIndex] : -1;
        }

        public int GetEnhanceLevel(int id) => Saved(id)?.Enhance ?? 0;
        public int GetAwakeningLevel(int id) => Saved(id)?.Awaken ?? 0;
        public int GetFragments(int id) => Saved(id)?.Fragments ?? 0;
        public long GetTotalAKSpent(int id) => Saved(id)?.Spent ?? 0;
        public static void Grant(ProgressionState state, int skillId, int duplicateFragments = 1)
        {
            if (!MageSkillRules.IsAvailable(skillId)) return;
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
            return BalanceMath.MageDamage((long)so.BaseDamage, eLv, aLv, PartyAttack);
        }

        // Include every equipped party member, even while down: a death must not shrink
        // the tower's progression bonus. Casts snapshot the value once at their start.
        public long PartyAttack
        {
            get
            {
                long total = 0;
                var players = Scripts.Core.UserManager.Instance?.GetPlayers();
                if (players != null) foreach (var player in players)
                    if (player != null && player.playerStatus != null) total = checked(total + player.playerStatus.Atk);
                return total;
            }
        }

        public float GetEffectiveCooldown(int skillId)
        {
            var so = GetSkillById(skillId);
            if (so == null) return 0;
            int aLv = GetAwakeningLevel(skillId);
            return (float)BalanceMath.MageInterval((decimal)so.baseCooldown, aLv) * (IsBloomEnabled(skillId) ? so.bloomCooldownMultiplier : 1f);
        }

        // ===== 장착 =====
        /// <summary>슬롯 편성은 서버(OnSetSkillLoadout)가 확정한다. 5칸 배열을 통째로 보내고 응답의 MageTowerState를 저장한다. bool 반환은 QA 호환(Pending이면 true)이다.</summary>
        public bool Equip(int slotIndex, int skillId) => TryEquipEx(slotIndex, skillId) == MageActionResult.Pending;
        public MageActionResult TryEquipEx(int slotIndex, int skillId)
        {
            if (slotIndex < 0 || slotIndex >= SlotCount || GetSkillById(skillId) == null) return MageActionResult.InvalidRequest;
            if (!IsOwned(skillId)) return MageActionResult.NotOwned;
            if (_equipped[slotIndex] == skillId) return MageActionResult.NoChange;
            if (IsBusy) return MageActionResult.Busy;
            int[] loadout = (int[])_equipped.Clone();
            int previous = loadout[slotIndex];
            // One atomic swap, never a transient duplicate or an intermediate empty loadout.
            for (int i = 0; i < SlotCount; i++) if (loadout[i] == skillId) loadout[i] = previous;
            loadout[slotIndex] = skillId;
            return RequestLoadout(loadout, skillId);
        }

        public bool Unequip(int slotIndex) => TryUnequipEx(slotIndex) == MageActionResult.Pending;
        public MageActionResult TryUnequipEx(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= SlotCount) return MageActionResult.InvalidRequest;
            if (_equipped[slotIndex] < 0) return MageActionResult.NoChange;
            if (IsBusy) return MageActionResult.Busy;
            int[] loadout = (int[])_equipped.Clone();
            int skillId = loadout[slotIndex];
            loadout[slotIndex] = -1;
            return RequestLoadout(loadout, skillId);
        }

        private MageActionResult RequestLoadout(int[] loadout, int skillId)
        {
            var network = Begin(out MageActionResult gate);
            if (network == null) return gate;
            var info = new MageActionInfo { Kind = MageActionKind.Loadout, SkillId = skillId };
            string detail = "slots=" + string.Join(".", loadout);
            network.Call<OnSetSkillLoadoutRequestDTO, OnSetSkillLoadoutResponseDTO>(new OnSetSkillLoadoutRequestDTO { EquippedSkillIds = loadout },
                r => ApplyServerResult(info, "loadout", detail, r.MageTowerState, null),
                e => RejectServerResult(info, "loadout", e));
            return MageActionResult.Pending;
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

        /// <summary>강화 비용 차감·단계 상승은 서버(OnEnChantSkill)가 확정하고 응답의 MageTowerState·CurrentArcaneKnowledge를 저장한다. CanEnhance는 안내용이다.</summary>
        public bool Enhance(int skillId) => TryEnhanceEx(skillId) == MageActionResult.Pending;
        public MageActionResult TryEnhanceEx(int skillId)
        {
            if (GetSkillById(skillId) == null) return MageActionResult.InvalidRequest;
            if (!IsOwned(skillId)) return MageActionResult.NotOwned;
            if (GetEnhanceLevel(skillId) >= BalanceMath.MageCap) return MageActionResult.MaxLevel;
            if (IsBusy) return MageActionResult.Busy;
            if (!CanEnhance(skillId)) return MageActionResult.NotEnough;
            var network = Begin(out MageActionResult gate);
            if (network == null) return gate;
            var info = new MageActionInfo { Kind = MageActionKind.Enhance, SkillId = skillId };
            network.Call<OnEnchantSkillRequestDTO, OnEnchantSkillResponseDTO>(new OnEnchantSkillRequestDTO { SkillCode = Code(skillId) },
                r => { info.SpentArcaneKnowledge = r.SpentArcaneKnowledge; ApplyServerResult(info, "enhance", "spent=" + r.SpentArcaneKnowledge, r.MageTowerState, r.CurrentArcaneKnowledge); },
                e => RejectServerResult(info, "enhance", e));
            return MageActionResult.Pending;
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

        /// <summary>각성 비용(파편 Awaken+1)과 단계는 서버(OnAwakeningSkill)가 확정한다. 응답에 재화가 없으므로 MageTowerState만 저장한다.</summary>
        public bool Awaken(int skillId) => TryAwakenEx(skillId) == MageActionResult.Pending;
        public MageActionResult TryAwakenEx(int skillId)
        {
            if (GetSkillById(skillId) == null) return MageActionResult.InvalidRequest;
            if (!IsOwned(skillId)) return MageActionResult.NotOwned;
            if (GetAwakeningLevel(skillId) >= 10) return MageActionResult.MaxLevel;
            if (IsBusy) return MageActionResult.Busy;
            if (GetFragments(skillId) < GetAwakeningCost(skillId)) return MageActionResult.NotEnough;
            var network = Begin(out MageActionResult gate);
            if (network == null) return gate;
            var info = new MageActionInfo { Kind = MageActionKind.Awaken, SkillId = skillId };
            network.Call<OnAwakeningSkillRequestDTO, OnAwakeningSkillResponseDTO>(new OnAwakeningSkillRequestDTO { SkillCode = Code(skillId) },
                r => { info.SpentFragments = r.SpentFragments; ApplyServerResult(info, "awaken", "frag=" + r.SpentFragments, r.MageTowerState, null); },
                e => RejectServerResult(info, "awaken", e));
            return MageActionResult.Pending;
        }

        // ===== 쿨타임 =====
        public bool IsOnCooldown(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= SlotCount) return false;
            int id = _equipped[slotIndex];
            return _cooldownTimers[slotIndex] > 0f || (id >= 0 && _skillCooldownTimers[id] > 0f);
        }

        public float GetCooldownRemaining(int slot)
        {
            if (slot < 0 || slot >= SlotCount) return 0;
            int id = GetEquippedSkillId(slot);
            return Mathf.Max(_cooldownTimers[slot], id >= 0 ? _skillCooldownTimers[id] : 0);
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
            => TryCast(slotIndex, null);

        public bool CastSkillAt(int slotIndex, Vector3 position)
        {
            if (slotIndex < 0 || slotIndex >= SlotCount || !IsValidAimPoint(position)) return false;
            var skill = GetSkillById(_equipped[slotIndex]);
            return skill != null && skill.CanAimWithBloom(IsBloomEnabled(skill.id)) && TryCast(slotIndex, position);
        }

        public static bool IsValidAimPoint(Vector3 point)
        {
            var camera = MageTowerTargeting.ResolveCamera();
            if (camera == null || float.IsNaN(point.x) || float.IsNaN(point.y) || float.IsInfinity(point.x) || float.IsInfinity(point.y)) return false;
            var viewport = camera.WorldToViewportPoint(point);
            return Mathf.Abs(point.z) < .01f && viewport.z > 0 && viewport.x >= .03f && viewport.x <= .97f && viewport.y >= .25f && viewport.y <= .84f;
        }

        private bool TryCast(int slotIndex, Vector3? position)
        {
            if (slotIndex < 0 || slotIndex >= SlotCount || StageManager.Instance?.CurrentRunState != eStageRunState.Running || IsOnCooldown(slotIndex) || _casting[slotIndex]) return false;
            int skillId = _equipped[slotIndex];
            var skill = GetSkillById(skillId);
            if (skill == null || skill.prefab == null || !IsOwned(skillId)) return false;
            Scripts.Monster.Monster target = null;
            if (skill.IsHealing)
            {
                if (!position.HasValue)
                {
                    if (!MageTowerSpellCast.TryFindHealingPoint(out var healPoint)) return false;
                    position = healPoint;
                }
            }
            else if (skill.spellKind == MageSpellKind.ArcaneVolley && !IsBloomEnabled(skillId))
            {
                if (!MageTowerSpellCast.TryFindTarget(out _)) return false;
                position = MageTowerTargeting.BattleCenter();
            }
            else if (!position.HasValue && !MageTowerSpellCast.TryFindTarget(skill, out target, IsBloomEnabled(skillId))) return false;
            // 기간 경계 저장이 거절되면 미집계 시전이나 쿨다운 소비 없이 다시 시도할 수 있다.
            if (!LocalProgression.RecordSkillCast(skillId)) return false;
            _casting[slotIndex] = true;
            _cooldowns[slotIndex] = _cooldownTimers[slotIndex] = GetEffectiveCooldown(skillId);
            _skillCooldowns[skillId] = _skillCooldownTimers[skillId] = _cooldownTimers[slotIndex];
            var cast = new MageTowerSpellCast(this, skill, GetEffectiveDamage(skillId), GetAwakeningLevel(skillId), IsBloomEnabled(skillId), target, position);
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
        /// <summary>개화 켜기/끄기는 서버(OnSetSkillBloom)가 확정한다(켤 때 각성 10 필요). 응답의 MageTowerState를 저장한다.</summary>
        public bool SetBloomEnabled(int id, bool enabled) => TrySetBloomEx(id, enabled) == MageActionResult.Pending;
        public MageActionResult TrySetBloomEx(int id, bool enabled)
        {
            if (GetSkillById(id) == null) return MageActionResult.InvalidRequest;
            if (!IsOwned(id)) return MageActionResult.NotOwned;
            if (!CanSetBloom(id)) return MageActionResult.NotUnlocked;
            if (IsBloomEnabled(id) == enabled) return MageActionResult.NoChange;
            if (IsBusy) return MageActionResult.Busy;
            var network = Begin(out MageActionResult gate);
            if (network == null) return gate;
            var info = new MageActionInfo { Kind = MageActionKind.Bloom, SkillId = id };
            network.Call<OnSetSkillBloomRequestDTO, OnSetSkillBloomResponseDTO>(new OnSetSkillBloomRequestDTO { SkillId = id, Enabled = enabled },
                r => ApplyServerResult(info, "bloom", "enabled=" + enabled, r.MageTowerState, null),
                e => RejectServerResult(info, "bloom", e));
            return MageActionResult.Pending;
        }
        public decimal SingleTargetDps(int id)
        {
            var skill = GetSkillById(id); if (skill == null) return 0;
            return GetEffectiveDamage(id) * MageSkillRules.SingleTargetPowerUnits(skill, GetAwakeningLevel(id), IsBloomEnabled(id)) / (decimal)GetEffectiveCooldown(id);
        }

        public bool CanReset(int id) => IsOwned(id) && GetEnhanceLevel(id) > 0;
        public long GetResetRefund(int id) => BalanceMath.Floor(GetTotalAKSpent(id) * .8m);
        /// <summary>강화 초기화·환급(실제 지출의 80%)은 서버(OnResetSkillEnhancement)가 확정하고 응답의 MageTowerState·CurrentArcaneKnowledge를 저장한다. GetResetRefund는 표시용이다.</summary>
        public bool ResetEnhance(int id) => TryResetEx(id) == MageActionResult.Pending;
        public MageActionResult TryResetEx(int id)
        {
            if (GetSkillById(id) == null) return MageActionResult.InvalidRequest;
            if (!IsOwned(id)) return MageActionResult.NotOwned;
            if (!CanReset(id)) return MageActionResult.NoChange;
            if (IsBusy) return MageActionResult.Busy;
            var network = Begin(out MageActionResult gate);
            if (network == null) return gate;
            var info = new MageActionInfo { Kind = MageActionKind.Reset, SkillId = id };
            network.Call<OnResetSkillEnhancementRequestDTO, OnResetSkillEnhancementResponseDTO>(new OnResetSkillEnhancementRequestDTO { SkillId = id },
                r => { info.RefundedArcaneKnowledge = r.RefundedArcaneKnowledge; ApplyServerResult(info, "reset", "refund=" + r.RefundedArcaneKnowledge, r.MageTowerState, r.CurrentArcaneKnowledge); },
                e => RejectServerResult(info, "reset", e));
            return MageActionResult.Pending;
        }

        // ===== 서버 연동 =====
        // 서버 SkillCode는 스킬 ID를 36비트 왼쪽으로 민 값이다(SkillCode.GenerateSkillCode와 같은 규칙, eSkillCode 밖의 ID도 보낸다).
        private static SkillCode Code(int skillId) => new SkillCode { Code = (ulong)skillId << 36 };

        /// <summary>IsBusy·로그인 준비를 검사하고 통과하면 요청 중 상태로 바꾼다. 실패 사유는 gate로 돌려준다.</summary>
        private NetworkManager Begin(out MageActionResult gate)
        {
            gate = MageActionResult.Pending;
            if (IsBusy) { gate = MageActionResult.Busy; return null; }
            var network = NetworkManager.Instance;
            if (network == null || !network.IsGameDataReady) { gate = MageActionResult.NetworkNotReady; return null; }
            IsBusy = true; LastFailure = null; OnBusyChanged?.Invoke(true);
            return network;
        }

        /// <summary>응답의 MageTowerState(와 AK 잔액)를 플레이어 상태에 쓴다. 응답에 없는 구역은 건드리지 않는다. 저장 실패는 ApplyFailed로 알린다.</summary>
        private void ApplyServerResult(MageActionInfo info, string op, string detail, MageTowerStateDTO state, long? arcaneKnowledge)
        {
            bool saved;
            try
            {
                saved = LocalProgression.Execute("server-mage-" + op, s =>
                {
                    Scripts.Core.UserManager.ApplyMageTower(s, state);
                    if (arcaneKnowledge.HasValue) s.Wallet[eCurrency.ArcaneKnowledge] = Math.Max(0, arcaneKnowledge.Value);
                    return true;
                });
            }
            catch (Exception exception) { Debug.LogException(exception); saved = false; }
            var current = LocalProgression.State;
            info.LogLine = $"[Mage] {op} skill={info.SkillId} {detail} | server Mage={Scripts.Core.UserManager.DescribeMage(state)} AK={(arcaneKnowledge.HasValue ? arcaneKnowledge.Value.ToString() : "-")}" +
                $" | state Mage={Scripts.Core.UserManager.DescribeMage(current)} AK={LocalProgression.Balance(eCurrency.ArcaneKnowledge)} | saved={saved}";
            Debug.Log(info.LogLine);
            if (!saved)
            {
                Direction.GameDirectInteraction.ReportSaveFailure();
                LastFailure = NetworkManager.CreateError("ApplyFailed", "서버 마탑 결과를 저장하지 못했습니다: " + LocalProgression.LastError);
                info.Ok = false;
                info.Failure = "서버 결과를 저장하지 못했습니다. 다시 로그인하면 서버 값으로 맞춰집니다.";
                Complete(info);
                return;
            }
            info.Ok = true;
            Complete(info);
        }

        /// <summary>서버 거절·전송 실패. 거절(Business)은 상태를 건드리지 않고 문구만 돌려준다. 응답 유실(Timeout/Transport)은 재전송하지 않고 서버 상태를 다시 받는다.</summary>
        private void RejectServerResult(MageActionInfo info, string op, PlayFabError error)
        {
            LastFailure = error;
            int code = NetworkManager.TryGetBusinessError(error, out var business) ? (int)business.ErrorCode : 0;
            string kind = NetworkManager.GetFailureKind(error);
            info.LogLine = $"[Mage] {op} skill={info.SkillId} rejected code={code} kind={kind} message={error?.ErrorMessage}";
            Debug.LogWarning(info.LogLine);
            info.Ok = false; info.ErrorCode = code;
            if (code == 0 && (kind == "Timeout" || kind == "Transport") && Scripts.Core.UserManager.Instance != null)
            {
                Scripts.Core.UserManager.Instance.RefreshFromServer(ok =>
                {
                    info.Failure = ok ? "서버 응답이 늦어 마탑 상태를 서버에서 다시 불러왔습니다." : NetworkManager.DescribeFailure(error, ActionName(info.Kind));
                    Complete(info);
                });
                return;
            }
            info.Failure = NetworkManager.DescribeFailure(error, ActionName(info.Kind));
            Complete(info);
        }

        private void Complete(MageActionInfo info)
        {
            IsBusy = false; OnBusyChanged?.Invoke(false);
            if (info.Ok) NotifyCommitted();
            OnActionCompleted?.Invoke(info);
        }

        public static string ActionName(MageActionKind kind)
        {
            switch (kind)
            {
                case MageActionKind.Enhance: return "스킬 강화";
                case MageActionKind.Awaken: return "스킬 각성";
                case MageActionKind.Loadout: return "스킬 편성";
                case MageActionKind.Bloom: return "개화 설정";
                default: return "강화 초기화";
            }
        }

        /// <summary>완료 이벤트의 토스트 문구. 수치는 서버 응답 값이다.</summary>
        public static string Describe(MageActionInfo info)
        {
            if (info == null) return "";
            if (!info.Ok) return info.Failure ?? (ActionName(info.Kind) + "에 실패했습니다.");
            switch (info.Kind)
            {
                case MageActionKind.Enhance: return "강화 완료 · 지식 " + info.SpentArcaneKnowledge + " 사용";
                case MageActionKind.Awaken: return "각성 완료 · 파편 " + info.SpentFragments + "개 사용";
                case MageActionKind.Loadout: return "스킬 편성을 저장했습니다.";
                case MageActionKind.Bloom: return "개화 설정을 저장했습니다.";
                default: return "강화를 초기화하고 지식 " + info.RefundedArcaneKnowledge + "을(를) 돌려받았습니다.";
            }
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
                    if (id == 8 ? s.Modules.ContainsKey(MageCatalogMigration.MeteorArchive) : GetSkillById(id) == null) continue;
                    s.MageSkills[id] = new MageSave { Enhance = BalanceMath.Clamp(MageTowerSkillCode.UnpackEnhanceLevel(code), 0, 100),
                        Awaken = BalanceMath.Clamp(MageTowerSkillCode.UnpackAwakeningLevel(code), 0, 10), Fragments = MageTowerSkillCode.UnpackQuantity(code) };
                    // No inferred historical spend: imported levels cannot manufacture reset refunds.
                }
                MageCatalogMigration.Apply(s);
                s.Modules["mage-imported"] = "1"; return true;
            })) NotifyCommitted();
        }
    }
}
