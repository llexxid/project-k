using System;
using KingdomIdle.Balance;
using Scripts.Core.SO;
using UnityEngine;

namespace Scripts.Core
{
    /// <summary>Shared catalog snapshot for battle, offline rewards, and previews, including before scene entry.</summary>
    public static class StageCatalogRules
    {
        private static StageDatabaseSO _database;
        public static StageDatabaseSO Database
        {
            get
            {
                if (_database != null) return _database;
                _database = Resources.Load<StageDatabaseSO>("StageDatabaseSO");
                if (_database == null) throw new InvalidOperationException("Generate Stage_Catalog before building the game.");
                _database.Init();
                return _database;
            }
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => _database = null;
        public static StageDatabaseRecord Get(eStageType type, int stage, int wave = 1)
        {
            long mask = type == eStageType.Main ? 0 : type == eStageType.GoldDungeon ? 0x10000000L : 0x20000000L;
            var id = (eStage)(0x200000000L | mask | ((long)stage << 16) | (uint)wave);
            if (!Database.TryGetStage(id, out var record) || !record.Enabled)
                throw new InvalidOperationException("Stage catalog entry unavailable: " + id);
            return record;
        }
        public static BalanceMath.Enemy MainEnemy(int stage, int wave) => Get(eStageType.Main, stage, wave).MonsterEntries[0].Combat.Numbers;
        public static long DungeonReward(bool gold, int difficulty)
        {
            var record = Get(gold ? eStageType.GoldDungeon : eStageType.RubyDungeon, difficulty);
            if (!gold) return record.Encounter.ClearRuby;
            long total = 0;
            foreach (var entry in record.MonsterEntries) total = checked(total + entry.Count * entry.Combat.Numbers.Gold);
            return total;
        }
    }
}
