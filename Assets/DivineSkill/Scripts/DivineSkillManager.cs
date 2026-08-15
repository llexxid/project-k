using System;
using System.Collections.Generic;
using UnityEngine;
using Scripts.Core.Manager;
using Scripts.Core;

namespace KingdomIdle.Divine
{
    /// <summary>
    /// 신 스킬(Divine Skill) 시스템의 단일 진입점.
    /// 카드 보유·레벨·장착(파티 공용 1슬롯)·쿨타임·시전·컬렉션 보너스를 모두 관리한다.
    ///
    /// 마탑 스킬과의 차이
    ///  - 마탑: 최대 5슬롯 상시 회전, 캐릭터 스탯과 독립된 자체 수치
    ///  - 신 스킬: 1슬롯 궁극기, 파티 최종 스탯을 기반으로 계산 → 모든 성장 레이어에 자동 동기화
    ///
    /// 데미지 공식 (기획서 3.4.3)
    ///   DivineValue = PartyStat × SkillMult × (1 + 0.1 × (Lv - 1)) × (1 + DivineBuff%)
    /// </summary>
    [DefaultExecutionOrder(-950)]
    public sealed class DivineSkillManager : MonoBehaviour
    {
        public static DivineSkillManager Instance { get; private set; }

        [SerializeField] private DivineSkillRegistrySO registry;

        [Tooltip("전투 중 쿨타임이 돌아오면 자동으로 시전한다.")]
        [SerializeField] private bool autoCastByDefault = true;

        [Header("해금 (기획서 3.4.1)")]
        [Tooltip("이 메인 스테이지를 클리어하면 신 스킬 시스템이 해금된다.")]
        [SerializeField] private int unlockStageNumber = 3;
        [Tooltip("해금 스테이지의 이 웨이브를 클리어하면 해금 (3-10 = 스테이지 3, 웨이브 10).")]
        [SerializeField] private int unlockWaveNumber = 10;
        [Tooltip("해금 시 확정 지급할 카드 id. 0 이하이면 지급하지 않는다.")]
        [SerializeField] private int unlockRewardCardId = 7; // Astra — 현재 연출까지 완성된 카드

        private const string PrefKey = "divine_save";

        /// <summary>파티 공용 장착 슬롯 수. 기획 확정값이며 확장 시 이 상수만 늘린다.</summary>
        public const int SlotCount = 1;

        /// <summary>레벨당 효과 증가율 (가산).</summary>
        private const float LevelBonusPerLevel = 0.1f;

        /// <summary>카드 1종 최초 획득당 파티 공격력·체력 가산 보너스.</summary>
        private const float CollectionBonusPerCard = 0.02f;

        /// <summary>초기 8종 전체 수집 시 추가 보너스.</summary>
        private const float CollectionCompleteBonus = 0.05f;

        // ── 보유 상태 ──
        private readonly Dictionary<int, int> _levels = new();      // cardId → 레벨(1부터)
        private readonly Dictionary<int, int> _duplicates = new();  // cardId → 중복 보유량
        private int _equippedId = -1;
        private bool _systemUnlocked;

        // ── 전투 상태 ──
        private float _cooldownTotal;
        private float _cooldownTimer;
        private bool _casting;
        private bool _autoEnabled;

        // ── 외부 버프(DivineBuff%) ──
        private float _divineBuffPercent;

        /// <summary>자동 시전 대상 검사 주기(초). 물리 질의 비용을 모바일에서 눌러 준다.</summary>
        private const float AutoCheckInterval = 0.25f;
        private float _nextAutoCheckTime;

        // ── 스테이지 전환 판별 ──
        // OnStageEnter 는 "웨이브" 단위로 발화한다. 쿨타임/버프 초기화는 실제 스테이지가
        // 바뀔 때만 수행해야 하므로 (타입, 스테이지 번호) 로 전환을 판별한다.
        private eStageType _lastStageType = (eStageType)(-1);
        private int _lastStageNumber = -1;

        /// <summary>컷인 완료 콜백 세대 토큰 — 스테이지 전환 시 무효화한다.</summary>
        private int _castGen;

        private DivineSkillCaster _caster;

        /// <summary>보유/레벨/장착 상태가 바뀔 때. UI 갱신용.</summary>
        public event Action OnStateChanged;
        /// <summary>쿨타임이 감소하는 프레임마다.</summary>
        public event Action OnCooldownTick;
        /// <summary>시전 시작(true) / 종료(false).</summary>
        public event Action<bool> OnCastStateChanged;
        /// <summary>카드 획득 시 (cardId, 최초 획득 여부).</summary>
        public event Action<int, bool> OnCardAcquired;

        // ────────────────────────────────────────────
        //  라이프사이클
        // ────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            _caster = GetComponent<DivineSkillCaster>();
            if (_caster == null) _caster = gameObject.AddComponent<DivineSkillCaster>();

            _autoEnabled = autoCastByDefault;
            DivineBuffState.ClearAll();
            DivineBuffState.OnChanged += SyncPartyMoveSpeed;

            Load();
        }

        private void Start()
        {
            var stage = StageManager.Instance;
            if (stage != null)
            {
                stage.OnStageEnter += OnStageEnter;
                stage.OnStageCleared += OnStageCleared;
            }
        }

        private void OnDestroy()
        {
            DivineBuffState.OnChanged -= SyncPartyMoveSpeed;

            var stage = StageManager.Instance;
            if (stage != null)
            {
                stage.OnStageEnter -= OnStageEnter;
                stage.OnStageCleared -= OnStageCleared;
            }

            if (Instance == this) Instance = null;
        }

        /// <summary>3-10 최초 클리어 시 시스템 해금 + 카드 1종 확정 지급 (기획서 3.4.1).</summary>
        private void OnStageCleared(StageDefinition definition)
        {
            if (_systemUnlocked || definition == null) return;
            if (definition.Type != eStageType.Main) return;
            if (definition.StageNumber < unlockStageNumber) return;
            if (definition.StageNumber == unlockStageNumber && definition.WaveNumber < unlockWaveNumber) return;

            UnlockSystem();

            if (unlockRewardCardId > 0 && !IsOwned(unlockRewardCardId))
                Acquire(unlockRewardCardId);

            Debug.Log($"[DivineSkill] 신 스킬 시스템 해금 " +
                      $"({definition.StageNumber}-{definition.WaveNumber} 클리어)");
        }

        private void Update()
        {
            DivineBuffState.Tick();

            if (_cooldownTimer > 0f)
            {
                _cooldownTimer -= Time.deltaTime;
                if (_cooldownTimer < 0f) _cooldownTimer = 0f;
                OnCooldownTick?.Invoke();
            }

            TryAutoCast();
        }

        /// <summary>
        /// 자동 시전. 대상 판정에 물리 질의가 들어가므로 매 프레임이 아니라 주기적으로만 검사한다
        /// (쿨타임 중에는 아예 검사하지 않는다).
        /// </summary>
        private void TryAutoCast()
        {
            if (!_autoEnabled) return;
            if (_casting || IsOnCooldown || !_systemUnlocked) return;
            if (DivinePresentation.CutInPlaying) return; // 잔여 컷인 오버레이 아래에서 자동 발동 금지
            if (EquippedCard == null) return;

            if (Time.time < _nextAutoCheckTime) return;
            _nextAutoCheckTime = Time.time + AutoCheckInterval;

            if (CanCast()) Cast();
        }

        /// <summary>스테이지 진입 시 쿨타임 초기화 (기획서 3.4.2) + 잔여 연출/버프 정리.</summary>
        private void OnStageEnter(StageDefinition definition)
        {
            // 매 웨이브: 이전 웨이브의 연출 코루틴/시전 상태만 정리한다
            _castGen++; // 진행 중이던 컷인의 완료 콜백을 무효화
            if (_caster != null) _caster.StopAll();
            if (_casting)
            {
                _casting = false;
                OnCastStateChanged?.Invoke(false);
            }

            // 쿨타임/버프 초기화는 "스테이지"가 실제로 바뀔 때만 수행한다.
            // OnStageEnter 는 웨이브 단위로 발화하므로 (타입, 스테이지 번호)로 전환을 판별한다.
            // (StageNumber 만 비교하면 메인 N ↔ 던전 N 이 충돌한다)
            bool sameStage = definition != null
                          && definition.Type == _lastStageType
                          && definition.StageNumber == _lastStageNumber;
            if (sameStage) return;

            _lastStageType = definition != null ? definition.Type : (eStageType)(-1);
            _lastStageNumber = definition != null ? definition.StageNumber : -1;

            ResetCooldown();
            DivineBuffState.ClearAll();
        }

        // ────────────────────────────────────────────
        //  카탈로그 조회
        // ────────────────────────────────────────────
        public IReadOnlyList<DivineSkillSO> GetAllCards()
        {
            if (registry == null || registry.cards == null)
                return Array.Empty<DivineSkillSO>();
            return registry.cards;
        }

        public DivineSkillSO GetCardById(int cardId)
            => registry != null ? registry.GetById(cardId) : null;

        public int TotalCardCount => GetAllCards().Count;

        // ────────────────────────────────────────────
        //  보유 · 레벨
        // ────────────────────────────────────────────
        public bool IsOwned(int cardId) => _levels.ContainsKey(cardId);

        /// <summary>보유 레벨. 미보유면 0.</summary>
        public int GetLevel(int cardId) => _levels.TryGetValue(cardId, out int lv) ? lv : 0;

        public int GetDuplicates(int cardId) => _duplicates.TryGetValue(cardId, out int d) ? d : 0;

        public int OwnedCount => _levels.Count;

        /// <summary>L → L+1 에 필요한 중복 카드 수 (유물과 동일 곡선: L개).</summary>
        public int GetNextUpgradeReq(int cardId)
        {
            int lv = GetLevel(cardId);
            return lv <= 0 ? 0 : lv;
        }

        public bool CanLevelUp(int cardId)
        {
            if (!IsOwned(cardId)) return false;
            int req = GetNextUpgradeReq(cardId);
            return req > 0 && GetDuplicates(cardId) >= req;
        }

        public bool TryLevelUp(int cardId)
        {
            if (!CanLevelUp(cardId)) return false;

            int req = GetNextUpgradeReq(cardId);
            _duplicates[cardId] -= req;
            _levels[cardId] += 1;

            Save();
            OnStateChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// 카드 획득. 최초면 레벨 1로 보유, 중복이면 레벨업 재료로 적립한다.
        /// 반환값은 "최초 획득" 여부.
        /// </summary>
        public bool Acquire(int cardId)
        {
            if (GetCardById(cardId) == null)
            {
                Debug.LogWarning($"[DivineSkill] 알 수 없는 카드 ID: {cardId}");
                return false;
            }

            bool isNew = !IsOwned(cardId);
            if (isNew)
            {
                _levels[cardId] = 1;

                // 첫 카드는 자동 장착 — 슬롯이 비어 있는 채로 방치되지 않게 한다
                if (_equippedId < 0) _equippedId = cardId;
            }
            else
            {
                _duplicates[cardId] = GetDuplicates(cardId) + 1;
            }

            Save();
            OnCardAcquired?.Invoke(cardId, isNew);
            OnStateChanged?.Invoke();

            // 최초 획득은 컬렉션 보너스(파티 스탯)에 영향을 준다
            if (isNew) RefreshCollectionBonus();
            return isNew;
        }

        // ────────────────────────────────────────────
        //  장착 (파티 공용 1슬롯)
        // ────────────────────────────────────────────
        public int EquippedCardId => _equippedId;
        public DivineSkillSO EquippedCard => _equippedId >= 0 ? GetCardById(_equippedId) : null;
        public bool IsEquipped(int cardId) => _equippedId == cardId;

        public bool Equip(int cardId)
        {
            if (!IsOwned(cardId)) return false;
            if (_equippedId == cardId) return true;

            _equippedId = cardId;
            ResetCooldown();
            Save();
            OnStateChanged?.Invoke();
            return true;
        }

        public void Unequip()
        {
            if (_equippedId < 0) return;
            _equippedId = -1;
            ResetCooldown();
            Save();
            OnStateChanged?.Invoke();
        }

        // ────────────────────────────────────────────
        //  해금
        // ────────────────────────────────────────────
        /// <summary>3-10 최초 클리어 시 해금 (기획서 3.4.1). 해금 전에는 HUD/시전을 막는다.</summary>
        public bool IsSystemUnlocked => _systemUnlocked;

        public void UnlockSystem()
        {
            if (_systemUnlocked) return;
            _systemUnlocked = true;
            Save();
            OnStateChanged?.Invoke();
        }

        // ────────────────────────────────────────────
        //  수치 계산
        // ────────────────────────────────────────────
        /// <summary>외부 버프 합류 항 (박사 과정 +0.3, 여신의 가호 +0.2 등).</summary>
        public float DivineBuffPercent
        {
            get => _divineBuffPercent;
            set => _divineBuffPercent = Mathf.Max(-0.9f, value);
        }

        /// <summary>레벨 배율 = 1 + 0.1 × (Lv - 1).</summary>
        public float GetLevelMultiplier(int cardId)
        {
            int lv = GetLevel(cardId);
            return lv <= 0 ? 1f : 1f + LevelBonusPerLevel * (lv - 1);
        }

        /// <summary>살아있는 파티원의 최종 공격력 합.</summary>
        public double GetPartyAtkSum()
        {
            double sum = 0d;
            var players = DivineSkillCaster.GetAlivePlayers();
            for (int i = 0; i < players.Count; i++)
            {
                var status = players[i].playerStatus;
                if (status != null) sum += status.Atk;
            }
            return sum;
        }

        /// <summary>
        /// 시전 시 적용될 값.
        /// 공격형 = 1히트 데미지, 회복형 = 대상 MAXHP 에 곱할 비율.
        /// </summary>
        public double GetCastValue(DivineSkillSO so)
        {
            if (so == null) return 0d;

            double scaled = so.skillMult
                          * GetLevelMultiplier(so.id)
                          * (1d + _divineBuffPercent);

            return so.IsOffensive ? GetPartyAtkSum() * scaled : scaled;
        }

        /// <summary>UI 표시용 — 장착 카드의 현재 예상 수치.</summary>
        public double GetEquippedCastValue() => GetCastValue(EquippedCard);

        // ── 컬렉션 보너스 ──
        /// <summary>보유 종류 수 × 2% (+ 전종 수집 시 5%). 파티 공격력·체력에 가산된다.</summary>
        public float CollectionBonusRate
        {
            get
            {
                int owned = OwnedCount;
                if (owned <= 0) return 0f;

                float rate = CollectionBonusPerCard * owned;
                if (TotalCardCount > 0 && owned >= TotalCardCount)
                    rate += CollectionCompleteBonus;
                return rate;
            }
        }

        /// <summary>StatEnhanceManager 가 스탯 파이프라인에 합류시킬 때 쓰는 정적 접근자.</summary>
        public static float CollectionRate => Instance != null ? Instance.CollectionBonusRate : 0f;

        private void RefreshCollectionBonus()
        {
            var enhance = StatEnhanceManager.Instance;
            if (enhance != null) enhance.ApplyToAllPlayers();
        }

        // ────────────────────────────────────────────
        //  쿨타임 · 시전
        // ────────────────────────────────────────────
        public bool IsAutoEnabled => _autoEnabled;
        public void SetAutoEnabled(bool enabled) => _autoEnabled = enabled;

        public bool IsCasting => _casting;
        public bool IsOnCooldown => _cooldownTimer > 0f;
        public float CooldownRemaining => _cooldownTimer;

        /// <summary>남은 쿨타임 비율 (1 = 방금 시전, 0 = 사용 가능).</summary>
        public float CooldownRatio =>
            _cooldownTotal <= 0f ? 0f : Mathf.Clamp01(_cooldownTimer / _cooldownTotal);

        public void ResetCooldown()
        {
            _cooldownTimer = 0f;
            _cooldownTotal = 0f;
            OnCooldownTick?.Invoke();
        }

        /// <summary>지금 시전할 수 있는지. 자동 시전 판정과 버튼 활성 판정에 함께 쓴다.</summary>
        public bool CanCast()
        {
            if (!_systemUnlocked) return false;
            if (_casting || IsOnCooldown) return false;

            var so = EquippedCard;
            if (so == null) return false;

            return HasMeaningfulTarget(so);
        }

        /// <summary>대상이 없는 상황에서 궁극기가 헛돌지 않게 하는 게이트.</summary>
        private static bool HasMeaningfulTarget(DivineSkillSO so)
        {
            if (so.IsOffensive || so.effectKind == eDivineEffectKind.PartyHaste)
                return DivineSkillCaster.HasAliveMonster();

            if (so.effectKind == eDivineEffectKind.HealAndGuard)
                return DivineSkillCaster.AnyPlayerBelowHp(0.9f); // 파티가 온전하면 회복을 아낀다

            return true;
        }

        /// <summary>
        /// 수동 시전(HUD 버튼) 진입점. 컷인 연출이 등록돼 있으면 컷인을 먼저 재생하고,
        /// 컷인이 끝나는 시점에 실제로 발동한다. 컷인이 없으면 즉시 발동한다.
        /// </summary>
        public bool CastManual()
        {
            if (!CanCast()) return false;

            var so = EquippedCard;
            if (so != null)
            {
                // 세대 토큰을 캡처해 두고, 스테이지가 바뀌면(_castGen 증가) 완료 콜백을 무시한다
                int gen = _castGen;
                if (DivinePresentation.TryPlayCutIn(so, () => OnCutInComplete(gen)))
                {
                    // 컷인 동안 중복 입력·자동 시전을 막는다 (쿨타임은 실제 발동 시점에 시작)
                    _casting = true;
                    OnCastStateChanged?.Invoke(true);
                    return true;
                }
            }

            return Cast();
        }

        private void OnCutInComplete(int gen)
        {
            // 컷인 도중 스테이지가 전환됐으면 이 콜백은 무효 — OnStageEnter 가 이미 상태를 정리했다
            if (gen != _castGen) return;

            // 컷인 중 상태를 풀고 실제 시전으로 넘긴다
            _casting = false;

            if (!Cast())
            {
                // 컷인이 끝났는데 대상이 사라진 경우 — 쿨타임 없이 원상 복구
                OnCastStateChanged?.Invoke(false);
            }
        }

        /// <summary>수동/자동 공용 시전 진입점. 실제로 발동했으면 true.</summary>
        public bool Cast()
        {
            if (!CanCast()) return false;

            var so = EquippedCard;
            double value = GetCastValue(so);

            _casting = true;
            OnCastStateChanged?.Invoke(true);

            bool fired = _caster.Cast(so, value, OnCastFinished);
            if (!fired)
            {
                // 실행기가 대상을 못 찾은 경우 — 쿨타임을 태우지 않는다
                OnCastFinished();
                return false;
            }

            _cooldownTotal = Mathf.Max(1f, so.cooldown);
            _cooldownTimer = _cooldownTotal;
            OnCooldownTick?.Invoke();
            return true;
        }

        private void OnCastFinished()
        {
            if (!_casting) return;
            _casting = false;
            OnCastStateChanged?.Invoke(false);
        }

        /// <summary>가속 버프가 걸리거나 풀릴 때 파티 이동속도를 다시 밀어 넣는다.</summary>
        private void SyncPartyMoveSpeed()
        {
            var players = DivineSkillCaster.GetAlivePlayers();
            for (int i = 0; i < players.Count; i++)
                players[i].playerOrder?.SyncMoveSpeed(players[i].playerStatus);
        }

        // ────────────────────────────────────────────
        //  서버 동기화 (마탑 스킬과 동일한 64비트 규약)
        // ────────────────────────────────────────────
        public long[] PackAllCards()
        {
            var cards = GetAllCards();
            var result = new long[cards.Count];
            for (int i = 0; i < cards.Count; i++)
            {
                int id = cards[i].id;
                result[i] = DivineSkillCode.Pack(id, GetLevel(id), GetDuplicates(id));
            }
            return result;
        }

        public void UnpackAllCards(long[] packed)
        {
            if (packed == null) return;

            _levels.Clear();
            _duplicates.Clear();

            for (int i = 0; i < packed.Length; i++)
            {
                int id = DivineSkillCode.UnpackCardId(packed[i]);
                int lv = DivineSkillCode.UnpackLevel(packed[i]);
                int dup = DivineSkillCode.UnpackDuplicates(packed[i]);

                if (lv > 0) _levels[id] = lv;
                if (dup > 0) _duplicates[id] = dup;
            }

            if (_equippedId >= 0 && !IsOwned(_equippedId)) _equippedId = -1;

            Save();
            OnStateChanged?.Invoke();
            RefreshCollectionBonus();
        }

        public long PackCard(int cardId)
            => DivineSkillCode.Pack(cardId, GetLevel(cardId), GetDuplicates(cardId));

        // ────────────────────────────────────────────
        //  저장 / 로드 (서버 연동 전까지 로컬)
        // ────────────────────────────────────────────
        [Serializable]
        private class SaveData
        {
            public int equipped = -1;
            public bool unlocked;
            public List<int> lvKeys = new();
            public List<int> lvVals = new();
            public List<int> dupKeys = new();
            public List<int> dupVals = new();
        }

        private void Save()
        {
            var d = new SaveData { equipped = _equippedId, unlocked = _systemUnlocked };
            SerializeDict(_levels, d.lvKeys, d.lvVals);
            SerializeDict(_duplicates, d.dupKeys, d.dupVals);

            PlayerPrefs.SetString(PrefKey, JsonUtility.ToJson(d));
            PlayerPrefs.Save();
        }

        private void Load()
        {
            string raw = PlayerPrefs.GetString(PrefKey, "");
            if (string.IsNullOrEmpty(raw)) return;

            var d = JsonUtility.FromJson<SaveData>(raw);
            if (d == null) return;

            _equippedId = d.equipped;
            _systemUnlocked = d.unlocked;
            DeserializeDict(d.lvKeys, d.lvVals, _levels);
            DeserializeDict(d.dupKeys, d.dupVals, _duplicates);

            if (_equippedId >= 0 && !IsOwned(_equippedId)) _equippedId = -1;
        }

        /// <summary>테스트/환생 등으로 보유 상태를 통째로 비울 때.</summary>
        public void ClearAllProgress()
        {
            _levels.Clear();
            _duplicates.Clear();
            _equippedId = -1;
            _systemUnlocked = false;
            ResetCooldown();
            Save();
            OnStateChanged?.Invoke();
            RefreshCollectionBonus();
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
