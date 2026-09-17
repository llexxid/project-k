using System;
using KingdomIdle.Balance;
using UnityEngine;

namespace Scripts.Core
{
    [Serializable]
    public struct StageEnemyData
    {
        [SerializeField] private long _hp, _attack, _gold, _experience;
        [SerializeField] private float _moveSpeed, _attackIntervalSec;
        public BalanceMath.Enemy Numbers => new(_hp, _attack, _gold, _experience);
        public float MoveSpeed => _moveSpeed;
        public float AttackIntervalSec => _attackIntervalSec;
        public bool IsValid => _hp > 0 && _attack > 0 && _gold >= 0 && _experience >= 0 && _moveSpeed > 0 && _attackIntervalSec > 0;
        public StageEnemyData(long hp, long attack, long gold, long experience, float moveSpeed, float interval)
        { _hp = hp; _attack = attack; _gold = gold; _experience = experience; _moveSpeed = moveSpeed; _attackIntervalSec = interval; }
    }

    [Serializable]
    public struct StageFirstClearReward
    {
        [SerializeField] private long _ancientCoins, _classFragments;
        [SerializeField] private int _unlockSkillId, _fragmentSkillId, _skillFragments, _weaponAttack;
        [SerializeField] private string _weaponJob, _unlocks;
        [SerializeField] private bool _defined;
        public bool Defined => _defined;
        public long AncientCoins => _ancientCoins;
        public long ClassFragments => _classFragments;
        public int UnlockSkillId => _defined ? _unlockSkillId : -1;
        public int FragmentSkillId => _defined ? _fragmentSkillId : -1;
        public int SkillFragments => _skillFragments;
        public int WeaponAttack => _weaponAttack;
        public string WeaponJob => _weaponJob;
        public string Unlocks => _unlocks;
        public StageFirstClearReward(long coins, long fragments, int skill, int fragmentSkill, int skillFragments, string job, int attack, string unlocks)
        { _defined = true; _ancientCoins = coins; _classFragments = fragments; _unlockSkillId = skill; _fragmentSkillId = fragmentSkill; _skillFragments = skillFragments; _weaponJob = job; _weaponAttack = attack; _unlocks = unlocks; }
    }

    [Serializable]
    public struct StageEncounterData
    {
        [SerializeField] private string _environmentPoolId;
        [SerializeField] private float _batchDelaySec;
        [SerializeField] private bool _resetTimerPerEnemy;
        [SerializeField] private double _equipmentDropRate;
        [SerializeField] private long _clearRuby, _firstClearRuby;
        [SerializeField] private StageFirstClearReward _firstClear;
        public string EnvironmentPoolId => _environmentPoolId;
        public float BatchDelaySec => _batchDelaySec;
        public bool ResetTimerPerEnemy => _resetTimerPerEnemy;
        public double EquipmentDropRate => _equipmentDropRate;
        public long ClearRuby => _clearRuby;
        public long FirstClearRuby => _firstClearRuby;
        public StageFirstClearReward FirstClear => _firstClear;
        public StageEncounterData(string pool, float delay, bool resetTimer, double dropRate, long ruby, long firstRuby, StageFirstClearReward firstClear)
        { _environmentPoolId = pool; _batchDelaySec = delay; _resetTimerPerEnemy = resetTimer; _equipmentDropRate = dropRate; _clearRuby = ruby; _firstClearRuby = firstRuby; _firstClear = firstClear; }
    }

    [Serializable]
    public struct StageEnvironmentPreset
    {
        [SerializeField] private string _poolId, _presetId;
        [SerializeField] private int _weight;
        public string PoolId => _poolId;
        public string PresetId => _presetId;
        public int Weight => _weight;
        public StageEnvironmentPreset(string pool, string id, int weight) { _poolId = pool; _presetId = id; _weight = weight; }
    }

    [Serializable]
    public struct CatalogMonsterInfo
    {
        [SerializeField] private ulong _id;
        [SerializeField] private string _displayName;
        [SerializeField] private float _moveSpeed, _attackIntervalSec;
        [SerializeField] private float _attackMultiplier;
        public eMonsterType Id => (eMonsterType)_id;
        public string DisplayName => _displayName;
        public float MoveSpeed => _moveSpeed;
        public float AttackIntervalSec => _attackIntervalSec;
        public float AttackMultiplier => _attackMultiplier;
        public CatalogMonsterInfo(eMonsterType id, string name, float speed, float interval, float multiplier = 1)
        { _id = (ulong)id; _displayName = name; _moveSpeed = speed; _attackIntervalSec = interval; _attackMultiplier = multiplier; }
    }
}
