using UnityEngine;
using System;
using System.Collections.Generic;
using Scripts.Users;

public class ChangeJob : MonoBehaviour
{
    [SerializeField] private JobDatabase jobDatabase;

    private Player _player;
    private SpriteRenderer _spriteRenderer;

    private int _currentJobIndex = 0;

    private HashSet<int> _unlockedJobs = new HashSet<int>();
    private const string UNLOCK_SAVE_KEY = "UnlockedJobs";

    #region UI Events
    public event Action<string, int, int> OnJobChanged;
    public event Action<int> OnJobUnlocked;
    #endregion

    private void Awake()
    {
        _player = GetComponent<Player>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Start()
    {
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

        LoadUnlockedJobs();
        _unlockedJobs.Add(0);
        SaveUnlockedJobs();

        ApplyJobByIndex(_currentJobIndex);
    }

    private void Update()
    {
        if (_player != null && _player.IsDead) return;

        if (Input.GetKeyDown(KeyCode.J))
            CycleToNextJob();

        if (Input.GetKeyDown(KeyCode.G))
            _player.User?.GainCoin(eCurrency.Gold, 1000);

        if (Input.GetKeyDown(KeyCode.R))
        {
            PlayerPrefs.DeleteKey(UNLOCK_SAVE_KEY);
            _unlockedJobs.Clear();
            _unlockedJobs.Add(0);
            SaveUnlockedJobs();
        }
    }

    private void CycleToNextJob()
    {
        if (jobDatabase == null || jobDatabase.Count == 0) return;
        int next = (_currentJobIndex + 1) % jobDatabase.Count;
        TryChangeJob(next);
    }

    public void ChangeJobByName(string jobName)
    {
        if (jobDatabase == null) return;

        int idx = jobDatabase.jobs.FindIndex(j => j.jobName == jobName);
        if (idx < 0)
        {
            Debug.LogWarning($"[ChangeJob] 직업 '{jobName}'을 JobDatabase에서 찾을 수 없습니다.");
            return;
        }

        if (!_unlockedJobs.Contains(idx))
        {
            _unlockedJobs.Add(idx);
            SaveUnlockedJobs();
            OnJobUnlocked?.Invoke(idx);
        }

        _currentJobIndex = idx;
        ApplyJobByIndex(idx);
    }

    public bool TryChangeJob(int index)
    {
        JobData data = jobDatabase.GetJob(index);
        if (data == null) return false;

        if (_unlockedJobs.Contains(index))
        {
            _currentJobIndex = index;
            ApplyJobByIndex(index);
            return true;
        }

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

    public bool IsJobUnlocked(int index) => _unlockedJobs.Contains(index);

    /// <summary>직업 경로별 패시브 버프를 팀 전체에 곱연산 적용.</summary>
    private void RefreshAllPassiveBonuses()
    {
        Player[] allPlayers = FindObjectsByType<Player>(FindObjectsSortMode.None);

        foreach (var p in allPlayers)
            p.playerStatus.ResetPassiveBonus();

        foreach (var source in allPlayers)
        {
            string jobName = source.playerStatus?.JobName;
            if (string.IsNullOrEmpty(jobName)) continue;

            float atkPct = 0f, hpPct = 0f;
            switch (jobName)
            {
                case "Knight":
                case "Elite_Knight":
                    hpPct = 1f;          // HP +100%
                    break;
                case "Archer":
                case "Elite_Archer":
                    atkPct = 1f;         // ATK +100%
                    break;
                case "Mage":
                case "Elite_Mage":
                    atkPct = 0.5f;       // ATK +50%
                    hpPct = 0.5f;        // HP  +50%
                    break;
                // Spearman: 패시브 없음
            }

            if (atkPct > 0f || hpPct > 0f)
            {
                float atkMult = 1f + atkPct;
                float hpMult  = 1f + hpPct;
                foreach (var target in allPlayers)
                    target.playerStatus.ApplyBuffMultiplier(atkMult, hpMult);
            }
        }
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

    public void ApplyJobByIndex(int index)
    {
        JobData data = jobDatabase.GetJob(index);
        if (data == null)
        {
            Debug.LogWarning($"[ChangeJob] index {index}에 해당하는 JobData가 없습니다.");
            return;
        }

        _player.playerStatus.ApplyJob(data);

        _player.skillSystem?.Setup(data.jobName);

        _player.playerOrder?.ApplyRanges(_player.skillSystem);

        if (_spriteRenderer != null && data.jobSprite != null)
            _spriteRenderer.sprite = data.jobSprite;

        if (_player._am != null && data.animatorController != null)
        {
            _player._am.runtimeAnimatorController = data.animatorController;
            _player.RebuildAnimatorComponent();
        }

        _player.equipmentManager?.SetCurrentJob(data.jobName);

        OnJobChanged?.Invoke(data.jobName, index, jobDatabase.Count);

        RefreshAllPassiveBonuses();
    }
}
