using System;
using System.Collections.Generic;
using UnityEngine;

namespace KingdomIdle.KingdomArmy
{
    /// <summary>
    /// 왕국군 관리 매니저.
    /// - 전직별 고유 파편 관리 (jobName 기반 Dictionary)
    /// - 2차 전직(Elite)은 1차 전직 파편을 공유하며, 1차 전직 완료가 선행 조건
    /// - 전직 비용/실행 래핑
    /// - 서버 마이그레이션 시 내부만 교체
    /// </summary>
    public class KingdomArmyManager : MonoBehaviour
    {
        public static KingdomArmyManager Instance { get; private set; }

        [SerializeField] private JobDatabase jobDatabase;
        [SerializeField] private EquipmentDatabase equipmentDatabase;
        [SerializeField] private int defaultFragmentCost = 40;

        /// <summary>jobName → 보유 파편 수</summary>
        private readonly Dictionary<string, int> _fragments = new();

        /// <summary>jobName → 전직 비용 (파편 수). SO에서 확장 가능.</summary>
        private readonly Dictionary<string, int> _fragmentCosts = new();

        /// <summary>2차 전직 → 1차 전직 매핑 (파편 공유 + 선행 조건)</summary>
        private static readonly Dictionary<string, string> _eliteToBase = new()
        {
            { "Elite_Knight", "Knight" },
            { "Elite_Archer", "Archer" },
            { "Elite_Mage",   "Mage"   },
        };

        private const string PrefKey = "KingdomArmy_Save";

        public JobDatabase JobDB => jobDatabase;
        public EquipmentDatabase EquipDB => equipmentDatabase;

        public event Action OnStateChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Load();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── 파편 ──

        /// <summary>2차 전직이면 1차 전직 이름 반환, 아니면 그대로 반환</summary>
        public static string GetBaseFragmentName(string jobName) =>
            _eliteToBase.TryGetValue(jobName, out string baseName) ? baseName : jobName;

        /// <summary>2차 전직의 선행 조건 직업 이름 반환. 선행 조건이 없으면 null.</summary>
        public static string GetPrerequisiteJob(string jobName) =>
            _eliteToBase.TryGetValue(jobName, out string baseName) ? baseName : null;

        /// <summary>해당 플레이어가 특정 전직을 완료했는지 확인</summary>
        public bool HasCompletedPromotion(Player player, string jobName)
        {
            if (player == null) return false;
            var changeJob = player.GetComponent<ChangeJob>();
            if (changeJob == null || jobDatabase == null) return false;

            int idx = jobDatabase.jobs.FindIndex(j => j.jobName == jobName);
            if (idx < 0) return false;
            return changeJob.IsJobUnlocked(idx);
        }

        public int GetFragments(string jobName)
        {
            string key = GetBaseFragmentName(jobName);
            return _fragments.TryGetValue(key, out int f) ? f : 0;
        }

        public void AddFragments(string jobName, int amount)
        {
            string key = GetBaseFragmentName(jobName);
            if (!_fragments.ContainsKey(key))
                _fragments[key] = 0;
            _fragments[key] += amount;
            Save();
            OnStateChanged?.Invoke();
        }

        public int GetFragmentCost(string jobName) =>
            _fragmentCosts.TryGetValue(jobName, out int c) ? c : defaultFragmentCost;

        /// <summary>전직 가능 여부. 2차 전직은 1차 전직 완료 + 파편 충분 조건 모두 필요.</summary>
        /// <remarks>해당 플레이어가 이미 그 직업을 해금한 적이 있으면 파편 없이도 자유롭게 재전직 가능.</remarks>
        public bool CanChangeJob(string jobName, Player player = null)
        {
            // 이미 해금된 직업이면 파편/선행 조건 무관 — 무료 재전직
            if (player != null && HasCompletedPromotion(player, jobName))
                return true;

            if (GetFragments(jobName) < GetFragmentCost(jobName))
                return false;

            string prereq = GetPrerequisiteJob(jobName);
            if (prereq != null && player != null)
                return HasCompletedPromotion(player, prereq);

            return true;
        }

        /// <summary>해당 플레이어가 이 직업을 이미 해금했는지 (UI에서 "재전직 무료" 표시 등에 사용)</summary>
        public bool IsAlreadyUnlocked(Player player, string jobName) =>
            HasCompletedPromotion(player, jobName);

        // ── 전직 실행 ──

        /// <summary>
        /// 지정 플레이어를 jobName 직업으로 전직.
        /// 처음 전직이면 파편을 소모하고, 이미 해금된 직업이면 무료로 재전직한다.
        /// 실제 전직 적용은 ChangeJob 컴포넌트가 담당.
        /// </summary>
        /// <remarks>
        /// 서버 연동 시: 직업별 해금 상태(_unlockedJobs)는 캐릭터별로 서버에 영구 저장되어야 한다.
        /// 현재는 ChangeJob 내부 PlayerPrefs(UnlockedJobs)에 보관 중.
        /// </remarks>
        public bool TryChangeJob(Player player, string jobName)
        {
            if (player == null || string.IsNullOrEmpty(jobName)) return false;
            if (!CanChangeJob(jobName, player)) return false;

            // 이미 해금된 직업은 무료 재전직 — 파편 차감 생략
            bool alreadyUnlocked = HasCompletedPromotion(player, jobName);
            if (!alreadyUnlocked)
            {
                int cost = GetFragmentCost(jobName);
                string fragKey = GetBaseFragmentName(jobName);
                _fragments[fragKey] -= cost;
            }

            var changeJob = player.GetComponent<ChangeJob>();
            if (changeJob != null)
                changeJob.ChangeJobByName(jobName);

            Save();
            OnStateChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// 전직 비용을 동적으로 설정 (SO 확장 시 사용)
        /// </summary>
        public void SetFragmentCost(string jobName, int cost)
        {
            _fragmentCosts[jobName] = cost;
        }

        // ── 플레이어 접근 헬퍼 ──

        public List<Player> GetPlayers()
        {
            var um = Scripts.Core.UserManager.Instance;
            if (um == null) return new List<Player>();

            // UserManager._user._players 접근 (리플렉션 대신 public 경로)
            var flags = System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.Public |
                        System.Reflection.BindingFlags.NonPublic;
            var userField = typeof(Scripts.Core.UserManager).GetField("_user", flags);
            if (userField == null) return new List<Player>();

            var user = userField.GetValue(um) as Scripts.Users.User;
            if (user == null || user._players == null) return new List<Player>();

            return user._players;
        }

        // ── Save / Load ──

        [Serializable]
        private class SaveData
        {
            public List<string> fKeys = new();
            public List<int> fVals = new();
        }

        private void Save()
        {
            var d = new SaveData();
            foreach (var kv in _fragments)
            {
                d.fKeys.Add(kv.Key);
                d.fVals.Add(kv.Value);
            }
            PlayerPrefs.SetString(PrefKey, JsonUtility.ToJson(d));
            PlayerPrefs.Save();
        }

        private void Load()
        {
            string raw = PlayerPrefs.GetString(PrefKey, "");
            if (string.IsNullOrEmpty(raw)) return;

            var d = JsonUtility.FromJson<SaveData>(raw);
            if (d == null) return;

            int len = Mathf.Min(d.fKeys.Count, d.fVals.Count);
            for (int i = 0; i < len; i++)
                _fragments[d.fKeys[i]] = d.fVals[i];
        }
    }
}
