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
        public Dictionary<int, MageSave> MageSkills = new();
        public int[] MageSlots = { -1, -1, -1, -1, -1 };
        public int EquipmentPity;
        public bool PendingReincarnation;
        public string ReincarnatedBattle;
        public Dictionary<string, long> Counters = new();
        public Dictionary<string, QuestPending> PendingQuests = new();
        public int BestStatTotal, BestMageTotal, BestEquipmentTotal;
        public string QuestDay, QuestWeek;
        /// <summary>퀘스트 하위 스키마. 0은 기존 저장이며 로드 중 1로 이관한다.</summary>
        public int QuestSchemaVersion;
        /// <summary>조건이 내려가도 유지하는 활성 가이드 및 전체 업적의 달성 ID다.</summary>
        public HashSet<long> CompletedQuests = new();
        /// <summary>기기 시각 역행으로 기간이 되돌아가지 않게 하는 마지막 승인 시각이다.</summary>
        public long QuestLastObservedUtc;
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
    }
    [Serializable]
    public sealed class QuestPending
    {
        // 기존 세 필드는 이전 버전 JSON과 고정 골드 보상 복원을 위해 유지한다.
        public long Id, ExpiresUtc;
        public decimal Gold;
        /// <summary>이전 저장의 달성 시점 골드를 보존하는지 나타낸다.</summary>
        public bool HasFixedGold;
        /// <summary>달성 당시 카탈로그 버전과 표시 정보. 정의가 비활성화되어도 수령할 수 있다.</summary>
        public string DefinitionVersion, Title, Description;
        public eQuestCategory Category;
        public eQuestObjectiveType ObjectiveType;
        public eQuestPresentationType PresentationType;
        public long TargetId;
        public int RequiredCount;
        /// <summary>고정 재화 또는 수령 시 계산할 골드 규칙의 복사본이다.</summary>
        public List<QuestRewardSave> Rewards = new();
    }
    /// <summary>카탈로그 수정과 독립적으로 보존하는 미수령 지급 명세다.</summary>
    [Serializable]
    public sealed class QuestRewardSave
    {
        public eCurrency Currency;
        public long Amount;
        public bool IsDynamicGold;
    }
    [Serializable]
    public sealed class EquipmentSave
    {
        public string Id;
        public int Code, Level;
        public int? Player;
        public bool Locked;
        public long ExpiresUtc;
    }
    [Serializable]
    public sealed class MageSave
    {
        public int Enhance, Awaken, Fragments;
        public long Spent;
    }
}
