using UnityEngine;
using System;
using System.Collections.Generic;
using Scripts.Users;
using Scripts.Core;

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
        LoadUnlockedJobs();
        _unlockedJobs.Add(0);
        SaveUnlockedJobs();
    }

    public void ChangeJobByCode(ulong jobCode)
    {
        switch ((eJobCode)jobCode)
        {
            case eJobCode.Mage:        ChangeJobByName("Mage");        break;
            case eJobCode.Archer:      ChangeJobByName("Archer");      break;
            case eJobCode.Knight:      ChangeJobByName("Knight");      break;
            case eJobCode.EliteMage:   ChangeJobByName("Elite_Mage");  break;
            case eJobCode.EliteKnight: ChangeJobByName("Elite_Knight");break;
            case eJobCode.EliteArcher: ChangeJobByName("Elite_Archer");break;
            case eJobCode.Spearman:
            default:                   ChangeJobByName("Spearman");    break;
        }
    }

    public void ChangeJobByName(string jobName)
    {
        int idx = jobDatabase.jobs.FindIndex(j => j.jobName == jobName);
        if (idx < 0) return;

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
    /// 직업 변경. 해금되어 있지 않으면 자동으로 해금한다.
    /// (해금 비용 — 전직파편 등 — 은 외부 UI/시스템에서 별도로 처리.)
    /// </summary>
    public bool TryChangeJob(int index)
    {
        JobData data = jobDatabase.GetJob(index);
        if (data == null) return false;

        if (!_unlockedJobs.Contains(index))
        {
            _unlockedJobs.Add(index);
            SaveUnlockedJobs();
            OnJobUnlocked?.Invoke(index);
        }

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
        if (data == null) return;

        _player.playerStatus.ApplyJob(data);
        _player.RefillHP();

        _player.skillSystem?.Setup(data);

        _player.playerOrder?.ApplyRanges(_player.skillSystem);
        _player.playerOrder?.SyncMoveSpeed(_player.playerStatus);

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
