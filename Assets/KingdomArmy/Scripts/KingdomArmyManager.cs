using System;
using System.Collections.Generic;
using UnityEngine;

namespace KingdomIdle.KingdomArmy
{
    /// <summary>
    /// 왕국군 관리 매니저.
    /// - 전직별 고유 파편 관리 (jobName 기반 Dictionary)
    /// - 전직 비용/실행 래핑
    /// - 서버 마이그레이션 시 내부만 교체
    /// </summary>
    public class KingdomArmyManager : MonoBehaviour
    {
        public static KingdomArmyManager Instance { get; private set; }

        [SerializeField] private JobDatabase jobDatabase;
        [SerializeField] private int defaultFragmentCost = 40;

        /// <summary>jobName → 보유 파편 수</summary>
        private readonly Dictionary<string, int> _fragments = new();

        /// <summary>jobName → 전직 비용 (파편 수). SO에서 확장 가능.</summary>
        private readonly Dictionary<string, int> _fragmentCosts = new();

        private const string PrefKey = "KingdomArmy_Save";

        public JobDatabase JobDB => jobDatabase;

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

        public int GetFragments(string jobName) =>
            _fragments.TryGetValue(jobName, out int f) ? f : 0;

        public void AddFragments(string jobName, int amount)
        {
            if (!_fragments.ContainsKey(jobName))
                _fragments[jobName] = 0;
            _fragments[jobName] += amount;
            Save();
            OnStateChanged?.Invoke();
        }

        public int GetFragmentCost(string jobName) =>
            _fragmentCosts.TryGetValue(jobName, out int c) ? c : defaultFragmentCost;

        public bool CanChangeJob(string jobName) =>
            GetFragments(jobName) >= GetFragmentCost(jobName);

        // ── 전직 실행 ──

        /// <summary>
        /// 지정 플레이어를 jobName 직업으로 전직.
        /// 파편을 소모하고 ChangeJob 컴포넌트를 통해 실제 전직 적용.
        /// </summary>
        public bool TryChangeJob(Player player, string jobName)
        {
            if (player == null || string.IsNullOrEmpty(jobName)) return false;
            if (!CanChangeJob(jobName)) return false;

            int cost = GetFragmentCost(jobName);
            _fragments[jobName] -= cost;

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
