using System.Collections;
using System.Collections.Generic;
using Scripts.Core;
using Scripts.Core.Utils;
using Scripts.Monster;
using Scripts.Monster.SO;
using UnityEngine;

public class StageSpawnController
{
    private MonsterSpawnLocationSO _locationSo;
    private StageSession _session;
    private int _locationCount;
    private float _elapsedTime;
    private List<LoopSpawnSchedule> _loopSchedules;
    private Monster _bossMonster;

    public bool Begin(StageSession session, MonsterSpawnLocationSO locationSo)
    {
        if (session == null)
        {
            Debug.LogError("[StageSpawnController] Session is null");
            return false;
        }

        if (locationSo == null)
        {
            Debug.LogError("[StageSpawnController] LocationSO is null");
            return false;
        }
        _session = session;
        _locationSo = locationSo;
        _locationCount = _locationSo.GetLocationCount();
        if (_locationCount <= 0)
        {
            Debug.LogError("[StageSpawnController] Spawn location is empty");
            return false;
        }

        _elapsedTime = 0f;
        _loopSchedules = new List<LoopSpawnSchedule>();
        _bossMonster = null;

        BuildLoopSchedules(session.Definition);
        SpawnMonster();
        _session.CompleteSpawning();
        return true;
    }

    public void Tick(float deltaTime)
    {
        if (_session == null || !_session.IsRunning || _loopSchedules.Count == 0) return;
        
        _elapsedTime += deltaTime;

        bool canSpawn =
            _session.RemainingMonsterCount <
            _session.Definition.LoopSpawnAliveThreshold;

        foreach (LoopSpawnSchedule schedule in _loopSchedules)
        {
            if (_elapsedTime < schedule.NextSpawnTime)
                continue;

            // 제한에 걸려도 해당 스폰 시점은 소비한다.
            schedule.NextSpawnTime =
                _elapsedTime + schedule.IntervalSec;

            if (canSpawn)
                SpawnMonster(schedule.Entries);
        }
    }

    public void Stop()
    {
        _loopSchedules?.Clear();
        _bossMonster = null;
        _session = null;
        _locationSo = null;
    }

    public bool TryGetBossMonster(out Monster monster)
    {
        monster = _bossMonster;
        return monster != null;
    }

    //세션의 모든 몬스터 스폰
    private void SpawnMonster()
    {
        if (_session == null || !_session.IsRunning) return;

        foreach (var monsterEntry in _session.Definition.MonsterEntries)
        {
            for (int i = 0; i < monsterEntry.Count; i++)
            {
                int index = Random.Range(0, _locationCount);
                _locationSo.TryGetPos(index, out Vector2 position);
                MonsterSpawner.Instance.SpawnMonster(
                    monsterEntry.MonsterType,
                    _session.Definition.MonsterStatMultiplier,
                    position,
                    Quaternion.identity,
                    out Monster monster);
                if (monster != null)
                {
                    _session.RegisterMonster(monster);
                    if (_bossMonster == null &&
                        monsterEntry.SpawnPhase == eMonsterSpawnPhase.Boss)
                    {
                        _bossMonster = monster;
                    }
                }
            }
        }
    }
    
    private void SpawnMonster(List<StageMonsterEntry> entries)
    {
        if (_session == null || !_session.IsRunning) return;
        
        foreach (var monsterEntry in entries)
        {
            for (int i = 0; i < monsterEntry.Count; i++)
            {
                int index = Random.Range(0, _locationCount);
                _locationSo.TryGetPos(index, out Vector2 position);
                MonsterSpawner.Instance.SpawnMonster(
                    monsterEntry.MonsterType,
                    _session.Definition.MonsterStatMultiplier,
                    position,
                    Quaternion.identity,
                    out Monster monster);
                if (monster != null)
                {
                    _session.RegisterMonster(monster);
                }
            }
        }
    }
    private void BuildLoopSchedules(StageDefinition definition)
    {
        var loopEntries = new List<StageMonsterEntry>();

        foreach (StageMonsterEntry entry in definition.MonsterEntries)
        {
            if (entry.SpawnPhase == eMonsterSpawnPhase.LoopPool)
                loopEntries.Add(entry);
        }

        if (loopEntries.Count == 0)
            return;

        _loopSchedules.Add(new LoopSpawnSchedule(
            definition.LoopSpawnIntervalSec,
            loopEntries));
    }
    private sealed class LoopSpawnSchedule
    {
        public float IntervalSec { get; }
        public float NextSpawnTime { get; set; }
        public List<StageMonsterEntry> Entries { get; }

        public LoopSpawnSchedule(
            float intervalSec,
            List<StageMonsterEntry> entries)
        {
            IntervalSec = intervalSec;
            NextSpawnTime = intervalSec;
            Entries = entries;
        }
    }
}

