using System;
using System.Collections.Generic;
using UnityEngine;
using Scripts.Core;

namespace KingdomIdle.KingdomArmy
{
    /// <summary>
    /// 왕국군 관리 매니저.
    /// - 전직 파편은 단일 통합 재화 (eCurrency.ClassFragment). 어떤 직업이든 파편 40개로 전직.
    ///   실제 저장소는 Wallet(EconomyBridge) 이고, 이 매니저는 비용/선행 조건/전직 실행을 래핑한다.
    /// - 2차 전직(Elite)은 1차 전직 완료를 선행 조건으로 한다 (_eliteToBase).
    /// - 서버 마이그레이션 시에도 이 매니저의 API 는 유지된다 (Wallet 내부만 교체).
    /// </summary>
    public class KingdomArmyManager : MonoBehaviour
    {
        public static KingdomArmyManager Instance { get; private set; }

        [SerializeField] private JobDatabase jobDatabase;
        [SerializeField] private EquipmentDatabase equipmentDatabase;
        [SerializeField] private int defaultFragmentCost = 40;

        /// <summary>2차 전직 → 1차 전직 선행 조건 매핑</summary>
        private static readonly Dictionary<string, string> _eliteToBase = new()
        {
            { "Elite_Knight", "Knight" },
            { "Elite_Archer", "Archer" },
            { "Elite_Mage",   "Mage"   },
        };

        public JobDatabase JobDB => jobDatabase;
        public EquipmentDatabase EquipDB => equipmentDatabase;

        /// <summary>파편 수량 / 전직 상태 변경 시 발생. UI 갱신에 사용.</summary>
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
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── 파편 (통합) ──

        /// <summary>
        /// 호환성 유지용. 예전 per-job 구조가 남아있는 호출부를 위해 남겨둔다.
        /// 통합 파편 체제에서는 모든 jobName 에 대해 동일한 전체 파편 수량을 반환.
        /// </summary>
        public static string GetBaseFragmentName(string jobName) => jobName;

        /// <summary>2차 전직의 선행 조건 직업 이름. 선행 조건이 없으면 null.</summary>
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

        /// <summary>현재 보유한 통합 전직 파편 수량.</summary>
        public int GetFragments()
        {
            EconomyBridge.TryGetAmount(eCurrency.ClassFragment, out long amount);
            if (amount < 0) amount = 0;
            if (amount > int.MaxValue) amount = int.MaxValue;
            return (int)amount;
        }

        /// <summary>
        /// [호환성] jobName 매개변수를 무시하고 통합 파편 수량을 반환한다.
        /// 과거 per-job 호출부를 깨지 않기 위해 남겨둔 오버로드.
        /// </summary>
        public int GetFragments(string jobName) => GetFragments();

        /// <summary>통합 전직 파편을 지급한다.</summary>
        public void AddFragments(int amount)
        {
            if (amount == 0) return;
            EconomyBridge.Add(eCurrency.ClassFragment, amount);
            OnStateChanged?.Invoke();
        }

        /// <summary>
        /// [호환성] jobName 을 무시하고 통합 파편을 지급한다.
        /// 기존 per-job 호출부 호환용.
        /// </summary>
        public void AddFragments(string jobName, int amount) => AddFragments(amount);

        /// <summary>전직 1 회 비용(파편 수). 모든 전직에 동일 적용.</summary>
        public int GetFragmentCost() => defaultFragmentCost;

        /// <summary>[호환성] jobName 무시 — 모든 전직 비용은 동일.</summary>
        public int GetFragmentCost(string jobName) => defaultFragmentCost;

        /// <summary>전직 가능 여부. 2차 전직은 1차 전직 완료 + 파편 충분 조건 모두 필요.</summary>
        /// <remarks>해당 플레이어가 이미 그 직업을 해금한 적이 있으면 파편 없이도 자유롭게 재전직 가능.</remarks>
        public bool CanChangeJob(string jobName, Player player = null)
        {
            // 이미 해금된 직업이면 파편/선행 조건 무관 — 무료 재전직
            if (player != null && HasCompletedPromotion(player, jobName))
                return true;

            if (GetFragments() < GetFragmentCost())
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
        /// 처음 전직이면 통합 파편 40개를 소모하고, 이미 해금된 직업이면 무료로 재전직한다.
        /// 실제 전직 적용은 ChangeJob 컴포넌트가 담당.
        /// </summary>
        public bool TryChangeJob(Player player, string jobName)
        {
            if (player == null || string.IsNullOrEmpty(jobName)) return false;
            if (!CanChangeJob(jobName, player)) return false;

            // 이미 해금된 직업은 무료 재전직 — 파편 차감 생략
            bool alreadyUnlocked = HasCompletedPromotion(player, jobName);
            if (!alreadyUnlocked)
            {
                int cost = GetFragmentCost();
                EconomyBridge.Add(eCurrency.ClassFragment, -cost);
            }

            var changeJob = player.GetComponent<ChangeJob>();
            if (changeJob != null)
                changeJob.ChangeJobByName(jobName);

            OnStateChanged?.Invoke();
            return true;
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
    }
}
