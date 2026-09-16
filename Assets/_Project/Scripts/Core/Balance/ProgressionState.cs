using System;
using System.Collections.Generic;

namespace KingdomIdle.Balance
{
    [Serializable]
    public sealed class ProgressionState
    {
        public int Schema = 1;
        public string BalanceVersion = BalanceMath.Version;
        public string Authority = "local-client";
        public long Revision;
        public Dictionary<eCurrency, long> Wallet = new();
        public int AccountLevel = 1;
        public long Experience, Kills;
        public int AttackLevel, HealthLevel, RubyGoldLevel, RubyExpLevel;
        public long RubyGoldSpent, RubyExpSpent, GoldRemainder, ExpRemainder;
        public int BestRubyGoldLevel, BestRubyExpLevel;
        public string RubyResetDay;
        public int PendingRubyReset;
        public int ReincarnationLevel, ReincarnationCount, CycleBossStage, ReincarnationsToday;
        public long LastReincarnationUtc, CycleStartedUtc;
        public string ReincarnationDay;
        public long MainStage = 0x200010001, HighestMainClear;
        public HashSet<long> MainClears = new();
        public HashSet<string> Claims = new();
        public Dictionary<int, string> Jobs = new();
        public Dictionary<int, HashSet<string>> UnlockedJobs = new();
        public List<EquipmentSave> Equipment = new();
        public List<EquipmentSave> PendingEquipment = new();
        public List<LegacyEquipmentStack> LegacyEquipment = new();
        public Dictionary<int, MageSave> MageSkills = new();
        public int[] MageSlots = { -1, -1, -1, -1, -1 };
        public int EquipmentPity;
        public bool PendingReincarnation;
        public string ReincarnatedBattle;
        public Dictionary<string, long> Counters = new();
        public Dictionary<string, QuestPending> PendingQuests = new();
        public int BestStatTotal, BestMageTotal, BestEquipmentTotal;
        public string QuestDay, QuestWeek;
        public int GoldDungeonClear, RubyDungeonClear, GoldTickets = 2, RubyTickets = 2;
        public string TicketDay;
        public string ActiveDungeon;
        public string ActiveBattleId;
        public int LastKillSequence;
        public string LastClearedBattle;
        public long LastDungeonGold, LastDungeonRuby;
        public long LastActiveUtc, OfflineStage;
        public decimal OfflineKpm;
        public int OfflineRubyGold, OfflineRubyExp;
        public Dictionary<string, string> Modules = new();

        // Copy scalars with MemberwiseClone, and every mutable collection/item explicitly.
        // Transactions no longer serialize and parse the complete account just to make a draft.
        public ProgressionState DeepClone()
        {
            var copy = (ProgressionState)MemberwiseClone();
            copy.Wallet = new(Wallet); copy.MainClears = new(MainClears); copy.Claims = new(Claims);
            copy.Jobs = new(Jobs); copy.UnlockedJobs = new();
            foreach (var pair in UnlockedJobs) copy.UnlockedJobs.Add(pair.Key, new(pair.Value));
            copy.Equipment = new(Equipment.Count); copy.PendingEquipment = new(PendingEquipment.Count);
            foreach (var item in Equipment) copy.Equipment.Add(item.Copy());
            foreach (var item in PendingEquipment) copy.PendingEquipment.Add(item.Copy());
            copy.LegacyEquipment = new(LegacyEquipment.Count);
            foreach (var item in LegacyEquipment) copy.LegacyEquipment.Add(item.Copy());
            copy.MageSkills = new();
            foreach (var pair in MageSkills) copy.MageSkills.Add(pair.Key, pair.Value.Copy());
            copy.MageSlots = (int[])MageSlots.Clone(); copy.Counters = new(Counters);
            copy.PendingQuests = new();
            foreach (var pair in PendingQuests) copy.PendingQuests.Add(pair.Key, pair.Value.Copy());
            copy.Modules = new(Modules);
            return copy;
        }
    }
    [Serializable]
    public sealed class QuestPending { public long Id, ExpiresUtc; public decimal Gold; public QuestPending Copy() => (QuestPending)MemberwiseClone(); }
    [Serializable]
    public sealed class EquipmentSave
    {
        public string Id;
        public int Code, Level;
        public int? Player;
        public bool Locked;
        public long ExpiresUtc;
        public EquipmentSave Copy() => (EquipmentSave)MemberwiseClone();
    }
    [Serializable]
    public sealed class LegacyEquipmentStack
    {
        public int Code, Level, Count;
        public LegacyEquipmentStack Copy() => (LegacyEquipmentStack)MemberwiseClone();
    }
    [Serializable]
    public sealed class MageSave
    {
        public int Enhance, Awaken, Fragments;
        public long Spent;
        // Absent in old snapshots: false. Kept outside the legacy packed skill code.
        public bool BloomEnabled;
        public MageSave Copy() => (MageSave)MemberwiseClone();
    }
}
