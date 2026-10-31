using System;
using System.Collections.Generic;
using KingdomIdle.Balance;
using KingdomIdle.KingdomArmy;
using Scripts.Core;
using Scripts.Core.Manager;
using UnityEngine;

/// <summary>
/// 캐릭터 한 명의 직업 적용. 전직(해금·선택)은 서버가 확정하고(OnGetJob·OnChangeJob, KingdomArmyManager), 이 컴포넌트는
/// 플레이어 상태(Jobs)에 들어온 서버 직업을 런타임(스탯·스킬·스프라이트)에 반영한다. 로컬 해금·파편 차감은 하지 않는다.
/// </summary>
public class ChangeJob : MonoBehaviour
{
    [SerializeField] private JobDatabase jobDatabase;
    private Player _player;
    private SpriteRenderer _spriteRenderer;
    public event Action<string, int, int> OnJobChanged;
    public event Action<int> OnJobUnlocked;
    private void Awake() { _player = GetComponent<Player>(); _spriteRenderer = GetComponent<SpriteRenderer>(); }
    /// <summary>안내용 가드: 보스 웨이브·던전에서는 전직 버튼을 막는다. 최종 판정은 서버다(던전 진행 중 1025, 메인 사냥 중은 허용).</summary>
    public static bool CanQueueChange => StageManager.Instance == null || StageManager.Instance.CurrentDefinition == null ||
        (StageManager.Instance.CurrentDefinition.Type == eStageType.Main && !StageManager.Instance.IsBossWave);
    /// <summary>캐릭터 생성 직후 호출된다. 서버 직업은 로그인 때 UserManager.ApplyJobs가 플레이어 상태에 썼으므로 저장된 직업을 적용만 한다.</summary>
    public void ChangeJobByCode(ulong code) => ApplySavedJob();
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
    /// <summary>전직 요청을 서버에 보낸다(KingdomArmyManager). true는 "요청 접수"이고 결과는 KingdomArmyManager.OnJobChangeCompleted로 온다.</summary>
    public bool TryChangeJob(int index)
    {
        var data = jobDatabase.GetJob(index);
        if (data == null || !JobData.IsAvailable(data.jobName) || !CanQueueChange) return false;
        var manager = KingdomArmyManager.Instance;
        return manager != null && manager.TryChangeJobEx(_player, data.jobName) == KingdomArmyManager.JobChangeResult.Pending;
    }
    /// <summary>서버 전직 응답이 플레이어 상태에 적용된 뒤 호출된다. 저장된 직업을 런타임에 적용하고, 새로 해금됐으면 OnJobUnlocked를 알린다.</summary>
    internal void NotifyServerJobApplied(bool newlyUnlocked)
    {
        if (newlyUnlocked)
        {
            string name = LocalProgression.State.Jobs.TryGetValue(_player.PlayerIndex, out var saved) ? saved : "Spearman";
            int index = jobDatabase.jobs.FindIndex(j => j != null && j.jobName == name);
            if (index >= 0) OnJobUnlocked?.Invoke(index);
        }
        ApplySavedJob();
    }
    public void ApplySavedJob()
    {
        if (!LocalProgression.State.Jobs.TryGetValue(_player.PlayerIndex, out var name)) name = "Spearman";
        if (!JobData.IsAvailable(name))
        {
            int slot = _player.PlayerIndex;
            string previous = name;
            LocalProgression.Execute("retire-unavailable-job", s => {
                s.Modules["retired-job:" + slot] = previous;
                s.Jobs[slot] = "Spearman";
                return true;
            });
            name = "Spearman";
            _player.PlayerEquipmentManager?.SetCurrentJob("Spearman");
        }
        int index = jobDatabase.jobs.FindIndex(j => j != null && j.jobName == name);
        if (index >= 0 && _player.playerStatus.JobName != name) ApplyJobByIndex(index);
        else if (index >= 0 && _player.skillSystem.SlotCount == 0) ApplyJobByIndex(index);
    }
    public void ApplyJobByIndex(int index)
    {
        JobData data = jobDatabase.GetJob(index); if (data == null) return;
        if (!JobData.IsAvailable(data.jobName)) data = jobDatabase.GetJob("Spearman");
        if (data == null) return;
        _player.playerStatus.ApplyJob(data); _player.skillSystem?.Setup(data);
        _player.SetVfxBodyAnchors(data.vfxFootY, data.vfxHeadY);
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
