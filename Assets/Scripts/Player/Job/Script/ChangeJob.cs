using UnityEngine;
using System;
using System.Collections.Generic;
using Scripts.Users;

/// <summary>
/// 플레이어 전직 시스템.
/// J키를 누르면 JobDatabase에 등록된 직업을 순서대로 순환한다.
/// UI 연동이 필요할 경우 OnJobChanged 이벤트를 구독하면 된다.
/// </summary>
public class ChangeJob : MonoBehaviour
{
    [Tooltip("게임에 등록된 모든 직업 데이터. 인스펙터에서 JobDatabase.asset을 연결하세요.")]
    [SerializeField] private JobDatabase jobDatabase;

    // Player 컴포넌트를 통해 playerStatus / skillManager / Animator를 참조
    private Player _player;
    private SpriteRenderer _spriteRenderer;

    /// <summary>현재 적용된 직업의 인덱스 (순환에 사용)</summary>
    private int _currentJobIndex = 0;

    /// <summary>한 번이라도 해금한 직업 인덱스 목록 (PlayerPrefs에 저장)</summary>
    private HashSet<int> _unlockedJobs = new HashSet<int>();
    private const string UNLOCK_SAVE_KEY = "UnlockedJobs";

    // ───────────────────────────────────────────
    // UI 연동 이벤트
    // ───────────────────────────────────────────
    #region UI Events

    /// <summary>전직이 완료됐을 때 발행. 인자: (직업 이름, 현재 인덱스, 전체 직업 수)</summary>
    public event Action<string, int, int> OnJobChanged;

    /// <summary>새 직업이 처음 해금됐을 때 발행. 인자: (직업 인덱스)</summary>
    public event Action<int> OnJobUnlocked;

    #endregion
    // ───────────────────────────────────────────


    private void Start()
    {
        _player = GetComponent<Player>();
        _spriteRenderer = GetComponent<SpriteRenderer>();

        if (_player == null)
        {
            Debug.LogError("[ChangeJob] Player 컴포넌트를 찾을 수 없습니다.");
            return;
        }

        if (jobDatabase == null || jobDatabase.Count == 0)
        {
            Debug.LogError("[ChangeJob] JobDatabase가 비어있거나 연결되지 않았습니다.");
            return;
        }

        // 저장된 해금 목록 불러오기
        LoadUnlockedJobs();

        // 시작 직업(index 0)은 항상 해금
        _unlockedJobs.Add(0);
        SaveUnlockedJobs();

        // 시작 시 첫 번째 직업(index 0) 적용
        ApplyJobByIndex(0);
    }

    private void Update()
    {
        // 사망 후에는 전직 입력 차단 (timeScale=0이어도 Update는 계속 호출됨)
        if (_player != null && _player.IsDead) return;

        if (Input.GetKeyDown(KeyCode.J))
        {
            CycleToNextJob();
        }

        // [테스트] G키 → 골드 1000 지급
        if (Input.GetKeyDown(KeyCode.G))
        {
            _player.User?.GainCoin(eCurrency.Gold, 1000);
            Debug.Log("[테스트] 골드 +1000 지급");
        }

        // [테스트] R키 → 해금 목록 초기화 (PlayerPrefs 삭제 후 index 0만 재등록)
        if (Input.GetKeyDown(KeyCode.R))
        {
            PlayerPrefs.DeleteKey(UNLOCK_SAVE_KEY);
            _unlockedJobs.Clear();
            _unlockedJobs.Add(0);
            SaveUnlockedJobs();
            Debug.Log("[테스트] 해금 목록 초기화 완료 (시작 직업만 유지)");
        }
    }

    /// <summary>
    /// 다음 직업으로 순환 전직을 시도한다.
    /// </summary>
    private void CycleToNextJob()
    {
        if (jobDatabase == null || jobDatabase.Count == 0) return;

        int next = (_currentJobIndex + 1) % jobDatabase.Count;
        TryChangeJob(next);
    }

    /// <summary>
    /// 직업 이름으로 전직을 적용한다.
    /// 외부 시스템(KingdomArmyManager 등)에서 비용 처리를 마친 뒤 호출하므로
    /// 골드 비용 검사 없이 직접 해금 및 적용한다.
    /// </summary>
    public void ChangeJobByName(string jobName)
    {
        if (jobDatabase == null) return;

        int idx = jobDatabase.jobs.FindIndex(j => j.jobName == jobName);
        if (idx < 0)
        {
            Debug.LogWarning($"[ChangeJob] 직업 '{jobName}'을 JobDatabase에서 찾을 수 없습니다.");
            return;
        }

        // 해금 처리 (비용은 호출자가 이미 처리)
        if (!_unlockedJobs.Contains(idx))
        {
            _unlockedJobs.Add(idx);
            SaveUnlockedJobs();
            OnJobUnlocked?.Invoke(idx);
        }

        _currentJobIndex = idx;
        ApplyJobByIndex(idx);
    }

    /// <summary>
    /// 이미 해금된 직업이면 무료로 전직.
    /// 처음 전직이면 골드를 소모하고 해금한 뒤 전직.
    /// 골드 부족 시 전직하지 않는다.
    /// </summary>
    public bool TryChangeJob(int index)
    {
        JobData data = jobDatabase.GetJob(index);
        if (data == null) return false;

        // 이미 해금된 직업 → 무료 전직
        if (_unlockedJobs.Contains(index))
        {
            _currentJobIndex = index;
            ApplyJobByIndex(index);
            return true;
        }

        // 첫 전직 → 골드 확인 후 차감
        User user = _player.User;
        if (user == null || !user.CanAfford(eCurrency.Gold, data.unlockCost))
        {
            Debug.LogWarning($"[ChangeJob] 전직 불가 — {data.jobName} 해금 비용: {data.unlockCost}G (골드 부족)");
            return false;
        }

        user.TrySpendCoin(eCurrency.Gold, data.unlockCost);
        _unlockedJobs.Add(index);
        SaveUnlockedJobs();
        OnJobUnlocked?.Invoke(index);

        _currentJobIndex = index;
        ApplyJobByIndex(index);
        return true;
    }

    /// <summary>해당 직업이 이미 해금됐는지 반환 (UI에서 자물쇠 표시 등에 활용)</summary>
    public bool IsJobUnlocked(int index) => _unlockedJobs.Contains(index);

    /// <summary>
    /// 씬의 모든 플레이어 패시브 보너스를 초기화한 뒤 재계산한다.
    /// 어느 플레이어든 전직할 때마다 호출한다.
    /// </summary>
    private void RefreshAllPassiveBonuses()
    {
        Player[] allPlayers = FindObjectsByType<Player>(FindObjectsSortMode.None);

        // 1. 모든 플레이어의 패시브 보너스 초기화
        foreach (var p in allPlayers)
            p.playerStatus.ResetPassiveBonus();

        // 2. 패시브 스킬 보유 플레이어가 범위 내 플레이어에게 보너스 부여
        foreach (var source in allPlayers)
        {
            if (source.skillManager == null) continue;

            foreach (var skill in source.skillManager.GetCurrentSkills())
            {
                if (skill.skillType != SkillType.Passive || skill.passiveAtkBonus <= 0)
                    continue;

                foreach (var target in allPlayers)
                {
                    float dist = Vector2.Distance(
                        source.transform.position, target.transform.position);
                    if (dist > skill.passiveRange) continue;

                    target.playerStatus.AddPassiveBonus(skill.passiveAtkBonus);
                }
            }
        }

        Debug.Log("[ChangeJob] 패시브 스킬 보너스 재계산 완료");
    }

    private void SaveUnlockedJobs()
    {
        PlayerPrefs.SetString(UNLOCK_SAVE_KEY, string.Join(",", _unlockedJobs));
        PlayerPrefs.Save();
    }

    private void LoadUnlockedJobs()
    {
        _unlockedJobs.Clear();
        string saved = PlayerPrefs.GetString(UNLOCK_SAVE_KEY, "");
        if (string.IsNullOrEmpty(saved)) return;

        foreach (var token in saved.Split(','))
        {
            if (int.TryParse(token, out int idx))
                _unlockedJobs.Add(idx);
        }
    }

    /// <summary>
    /// 인덱스로 직업을 적용한다. PlayerStatus 스탯 갱신 + SkillManager 스킬 교체.
    /// </summary>
    private void ApplyJobByIndex(int index)
    {
        JobData data = jobDatabase.GetJob(index);
        if (data == null)
        {
            Debug.LogWarning($"[ChangeJob] index {index}에 해당하는 JobData가 없습니다.");
            return;
        }

        // 1. PlayerStatus 스탯 갱신
        _player.playerStatus.ApplyJob(data);

        // 2. 공격속도 변경 시 attackRate도 동기화
        if (_player.playerOrder?._attack != null)
        {
            _player.playerOrder._attack.attackRate = data.atkSpeed;
        }

        // 3. SkillManager에 직업 스킬 갱신
        _player.skillManager?.RefreshSkills(data.skills);

        // 4. BT 스킬 트리 재조립 (새 직업 스킬 → BT LeafNode로 자동 등록)
        _player.playerOrder?.RebuildSkillTree(data.skills, _player);

        // 5. 스프라이트 교체
        if (_spriteRenderer != null && data.jobSprite != null)
            _spriteRenderer.sprite = data.jobSprite;

        // 6. 애니메이터 컨트롤러 교체
        if (_player._am != null && data.animatorController != null)
        {
            _player._am.runtimeAnimatorController = data.animatorController;
            // 컨트롤러 교체 후 AnimatorComponent의 해시 캐시를 재구성
            _player.RebuildAnimatorComponent();
        }

        // 7. [스킬 레벨] 이 직업의 스킬 레벨을 PlayerSkillRuntime에 로드
        if (SkillEnhanceManager.Instance != null)
        {
            SkillEnhanceManager.Instance.Runtime.Load(data);
        }

        // 8. [장비 시스템] 현재 직업명을 EquipmentManager에 전달
        _player.equipmentManager?.SetCurrentJob(data.jobName);

        // 9. UI 이벤트 발행
        OnJobChanged?.Invoke(data.jobName, index, jobDatabase.Count);

        // 10. 보호막 패시브 발동 기록 초기화 (전직 시 재발동 가능하도록)
        _player.ResetShieldPassive();

        // 11. 패시브 스킬 보너스 전체 재계산
        RefreshAllPassiveBonuses();

        Debug.Log($"[ChangeJob] 전직 완료: {data.jobName} (HP:{data.maxHP} ATK:{data.atk})");
    }
}
