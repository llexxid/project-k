using System;
using System.Collections.Generic;
using KingdomIdle.Balance;
using Scripts.Core;
using Scripts.Core.Utils;
using Scripts.Monster;
using Scripts.Monster.SO;
using UnityEngine;

public class StageSpawnController
{
    private StageSession _session;
    private MonsterSpawnLocationSO _locations;
    private readonly Queue<StageMonsterEntry> _queue = new();
    private Monster _boss;
    private float _wait;
    private int _spawned;
    public bool Begin(StageSession session, MonsterSpawnLocationSO locations)
    {
        if (session == null || locations == null || locations.GetLocationCount() <= 0) return false;
        _session = session; _locations = locations; _queue.Clear(); _spawned = 0; _wait = 0;
        var entries = session.Definition.MonsterEntries;
        int largestCount = 0;
        foreach (var entry in entries)
        {
            if (!entry.Combat.IsValid) throw new InvalidOperationException("Stage combat data is missing. Regenerate Stage_Catalog.");
            largestCount = Mathf.Max(largestCount, entry.Count);
        }
        // Mix the authored roles in each live batch without changing their total counts.
        for (int i = 0; i < largestCount; i++)
            foreach (var entry in entries) if (i < entry.Count) _queue.Enqueue(entry);
        Fill(); return true;
    }
    public void Tick(float delta)
    {
        if (_session == null || !_session.IsRunning || _queue.Count == 0) return;
        var kind = _session.Definition.Type;
        if (kind != eStageType.Main)
        {
            if (_session.RemainingMonsterCount > 0) return;
            if (_session.Definition.Encounter.ResetTimerPerEnemy) _session.TimerRunning = false;
            _wait += delta;
            if (_wait < _session.Definition.Encounter.BatchDelaySec) return;
        }
        Fill();
    }
    private void Fill()
    {
        var d = _session.Definition;
        int cap = d.LoopSpawnAliveThreshold;
        while (_queue.Count > 0 && _session.RemainingMonsterCount < cap)
        {
            var entry = _queue.Peek();
            _locations.TryGetPos(UnityEngine.Random.Range(0, _locations.GetLocationCount()), out Vector2 position);
            position = CombatViewport.Spawn(position, entry.IsBoss);
            MonsterSpawner.Instance.SpawnMonster(entry.MonsterType, 1, position, Quaternion.identity, out var monster);
            if (monster == null) throw new InvalidOperationException("Stage monster resource unavailable: " + entry.MonsterType);
            if (_spawned == 0 && !BattleEconomy.Begin(_session)) { MonsterSpawner.Instance.ReleaseMonster(monster.Type,monster); throw new InvalidOperationException("Battle could not be committed."); }
            bool boss = entry.IsBoss;
            monster.ApplyBalance(entry.Combat.Numbers, boss, entry.IsRanged, d.Type == eStageType.GoldDungeon, entry.Combat.MoveSpeed, entry.Combat.AttackIntervalSec);
            _session.RegisterMonster(monster); if (boss) _boss = monster;
            _queue.Dequeue(); _spawned++; _wait = 0;
            if (d.Encounter.ResetTimerPerEnemy) _session.SetTimeLimit(d.TimeLimitSec);
        }
        if (_queue.Count == 0) _session.CompleteSpawning();
    }
    public void Stop() { _queue.Clear(); _session = null; _locations = null; _boss = null; }
    public bool TryGetBossMonster(out Monster monster) { monster = _boss; return monster != null; }
}
