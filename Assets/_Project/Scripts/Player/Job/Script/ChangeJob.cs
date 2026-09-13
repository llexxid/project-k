using System;
using System.Collections.Generic;
using KingdomIdle.Balance;
using Scripts.Core;
using Scripts.Core.Manager;
using UnityEngine;

public class ChangeJob : MonoBehaviour
{
    [SerializeField] private JobDatabase jobDatabase;
    private Player _player;
    private SpriteRenderer _spriteRenderer;
    public event Action<string, int, int> OnJobChanged;
    public event Action<int> OnJobUnlocked;
    private void Awake() { _player = GetComponent<Player>(); _spriteRenderer = GetComponent<SpriteRenderer>(); }
    public static bool CanQueueChange => StageManager.Instance == null || StageManager.Instance.CurrentDefinition == null ||
        (StageManager.Instance.CurrentDefinition.Type == eStageType.Main && !StageManager.Instance.IsBossWave);
    public void ChangeJobByCode(ulong code)
    {
        string name = (eJobCode)code switch { eJobCode.Knight => "Knight", eJobCode.Archer => "Archer", eJobCode.Mage => "Mage",
            eJobCode.EliteKnight => "Elite_Knight", eJobCode.EliteArcher => "Elite_Archer", eJobCode.EliteMage => "Elite_Mage", _ => "Spearman" };
        int slot = _player.PlayerIndex;
        if (!LocalProgression.State.Jobs.ContainsKey(slot))
            LocalProgression.Execute("job-import-once", s => {
                s.Jobs[slot] = name; s.UnlockedJobs[slot] = new HashSet<string> { "Spearman", name };
                var tree = UserManager.Instance?.GetJobTreeForCharacter(slot);
                if (tree != null) foreach (var value in tree)
                {
                    string job = ((eJobCode)value) switch { eJobCode.Knight => "Knight", eJobCode.Archer => "Archer", eJobCode.Mage => "Mage", eJobCode.EliteKnight => "Elite_Knight", eJobCode.EliteArcher => "Elite_Archer", eJobCode.EliteMage => "Elite_Mage", _ => "Spearman" };
                    s.UnlockedJobs[slot].Add(job);
                }
                return true;
            });
        ApplySavedJob();
    }
    public void ChangeJobByName(string name)
    {
        int index = jobDatabase.jobs.FindIndex(j => j != null && j.jobName == name);
        if (index >= 0) TryChangeJob(index);
    }
    public bool IsJobUnlocked(int index)
    {
        var data = jobDatabase.GetJob(index);
        return data != null && (data.jobName == "Spearman" || (LocalProgression.State.UnlockedJobs.TryGetValue(_player.PlayerIndex, out var jobs) && jobs.Contains(data.jobName)));
    }
    public bool TryChangeJob(int index)
    {
        var data = jobDatabase.GetJob(index);
        if (data == null || !CanQueueChange) return false;
        int slot = _player.PlayerIndex;
        bool wasUnlocked = IsJobUnlocked(index);
        bool ok = LocalProgression.Execute("job-select", s => {
            if (!s.UnlockedJobs.TryGetValue(slot, out var jobs)) s.UnlockedJobs[slot] = jobs = new HashSet<string> { "Spearman" };
            if (!jobs.Contains(data.jobName))
            {
                bool elite = data.jobName.StartsWith("Elite_", StringComparison.Ordinal);
                if (elite && !jobs.Contains(data.jobName.Substring(6))) return false;
                if (!LocalProgression.Spend(s, eCurrency.ClassFragment, elite ? 120 : 40)) return false;
                jobs.Add(data.jobName);
            }
            s.Jobs[slot] = data.jobName;
            foreach (var item in s.Equipment)
                if (item.Player == slot && EquipmentManager.Instance?.GetData(item.Code) is EquipmentData weapon && !weapon.IsAllowedForJob(data.jobName)) item.Player = null;
            return true;
        });
        if (!ok) return false;
        if (!wasUnlocked) OnJobUnlocked?.Invoke(index);
        if (StageManager.Instance?.CurrentDefinition == null) { ApplySavedJob(); RefreshPartyAura(); }
        return true;
    }
    public void ApplySavedJob()
    {
        if (!LocalProgression.State.Jobs.TryGetValue(_player.PlayerIndex, out var name)) name = "Spearman";
        int index = jobDatabase.jobs.FindIndex(j => j != null && j.jobName == name);
        if (index >= 0 && _player.playerStatus.JobName != name) ApplyJobByIndex(index);
        else if (index >= 0 && _player.skillSystem.SlotCount == 0) ApplyJobByIndex(index);
    }
    public void ApplyJobByIndex(int index)
    {
        JobData data = jobDatabase.GetJob(index); if (data == null) return;
        _player.playerStatus.ApplyJob(data); _player.skillSystem?.Setup(data);
        _player.playerOrder?.ApplyRanges(_player.skillSystem); _player.playerOrder?.SyncMoveSpeed(_player.playerStatus);
        if (_spriteRenderer != null && data.jobSprite != null) _spriteRenderer.sprite = data.jobSprite;
        if (_player._am != null && data.animatorController != null) { _player._am.runtimeAnimatorController = data.animatorController; _player.RebuildAnimatorComponent(); }
        OnJobChanged?.Invoke(data.jobName, index, jobDatabase.Count);
    }
    public static void RefreshPartyAura()
    {
        var players = UserManager.Instance?.GetPlayers(); if (players == null) return;
        decimal attack = 0, health = 0;
        for (int i = 0; i < Math.Min(3, players.Count); i++)
        {
            string job = players[i]?.playerStatus?.JobName;
            if (job == "Knight" || job == "Elite_Knight") health += .1m;
            if (job == "Archer" || job == "Elite_Archer") attack += .1m;
            if (job == "Mage" || job == "Elite_Mage") { attack += .05m; health += .05m; }
        }
        foreach (var player in players) player?.playerStatus?.SetAura(attack, health);
    }
}
