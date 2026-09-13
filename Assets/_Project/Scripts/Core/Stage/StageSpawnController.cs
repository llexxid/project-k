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
    private readonly Queue<(eMonsterType type, bool ranged)> _queue = new();
    private Monster _boss;
    private float _wait;
    private int _spawned;
    public bool Begin(StageSession session, MonsterSpawnLocationSO locations)
    {
        if (session == null || locations == null || locations.GetLocationCount() <= 0) return false;
        _session = session; _locations = locations; _queue.Clear(); _spawned = 0; _wait = 0;
        int entryIndex = 0;
        foreach (var entry in session.Definition.MonsterEntries)
        {
            for (int i = 0; i < entry.Count; i++) _queue.Enqueue((entry.MonsterType, entryIndex > 0));
            entryIndex++;
        }
        Fill(); return true;
    }
    public void Tick(float delta)
    {
        if (_session == null || !_session.IsRunning || _queue.Count == 0) return;
        var kind = _session.Definition.Type;
        if (kind != eStageType.Main)
        {
            if (_session.RemainingMonsterCount > 0) return;
            if (kind == eStageType.RubyDungeon) _session.TimerRunning = false;
            _wait += delta;
            if (_wait < (kind == eStageType.RubyDungeon ? .8f : .3f)) return;
        }
        Fill();
    }
    private void Fill()
    {
        var d = _session.Definition;
        int cap = d.Type == eStageType.Main ? 6 : d.Type == eStageType.GoldDungeon ? 5 : 1;
        while (_queue.Count > 0 && _session.RemainingMonsterCount < cap)
        {
            var entry = _queue.Peek();
            _locations.TryGetPos(UnityEngine.Random.Range(0, _locations.GetLocationCount()), out Vector2 position);
            MonsterSpawner.Instance.SpawnMonster(entry.type, 1, position, Quaternion.identity, out var monster);
            if (monster == null) throw new InvalidOperationException("Stage monster resource unavailable: " + entry.type);
            if (_spawned == 0 && !BattleEconomy.Begin(_session)) { MonsterSpawner.Instance.ReleaseMonster(monster.Type,monster); throw new InvalidOperationException("Battle could not be committed."); }
            bool boss = d.Type == eStageType.RubyDungeon || (d.Type == eStageType.Main && d.WaveNumber == 11);
            var numbers = d.Type == eStageType.Main ? BalanceMath.MainEnemy(d.StageNumber, d.WaveNumber) :
                d.Type == eStageType.GoldDungeon ? BalanceMath.Mimic(d.StageNumber) : BalanceMath.RubyBoss(d.StageNumber, _spawned);
            monster.ApplyBalance(numbers, boss, entry.ranged, d.Type == eStageType.GoldDungeon);
            _session.RegisterMonster(monster); if (boss) _boss = monster;
            _queue.Dequeue(); _spawned++; _wait = 0;
            if (d.Type == eStageType.RubyDungeon) _session.SetTimeLimit(30);
        }
        if (_queue.Count == 0) _session.CompleteSpawning();
    }
    public void Stop() { _queue.Clear(); _session = null; _locations = null; _boss = null; }
    public bool TryGetBossMonster(out Monster monster) { monster = _boss; return monster != null; }
}
