using System;
using System.Collections.Generic;
using System.Linq;
using Protocol.Message.Dto;

namespace KingdomIdle.Balance
{
    [Serializable]
    public sealed class EconomyLocalState
    {
        public EconomyBatchStateDTO Server;
        public EconomyDungeonBattleDTO Dungeon;
        public string LastAppliedRequestId;
        public List<EconomyCommandDTO> Hunts = new();
        public List<EconomyRewardEpochCutoffDTO> Cutoffs = new();

        public EconomyLocalState Copy() => new()
        {
            Server = EconomyCopies.Copy(Server), Dungeon = EconomyCopies.Copy(Dungeon),
            LastAppliedRequestId = LastAppliedRequestId,
            Hunts = Hunts.Select(EconomyCopies.Copy).ToList(), Cutoffs = Cutoffs.Select(EconomyCopies.Copy).ToList()
        };
    }

    internal static class EconomyCopies
    {
        internal static List<HuntResult> Kills(IEnumerable<HuntResult> value) => value?.OrderBy(x => (ulong)x.MonsterType).ToList();
        internal static EconomyCommandDTO Copy(EconomyCommandDTO x) => x == null ? null : new()
        {
            Kind = x.Kind, Mode = x.Mode, ExpectedLevel = x.ExpectedLevel, CountLimit = x.CountLimit, SpendLimit = x.SpendLimit,
            Stage = x.Stage, MainRunId = x.MainRunId, BattleId = x.BattleId, RewardEpoch = x.RewardEpoch, TotalKills = Kills(x.TotalKills), SkillId = x.SkillId, EquipmentInstanceId = x.EquipmentInstanceId
        };
        internal static EconomyRewardEpochCutoffDTO Copy(EconomyRewardEpochCutoffDTO x) => x == null ? null : new()
        { Epoch = x.Epoch, Stage = x.Stage, MainRunId = x.MainRunId, BattleId = x.BattleId, TotalKills = Kills(x.TotalKills) };
        internal static EconomyDungeonBattleDTO Copy(EconomyDungeonBattleDTO x) => x == null ? null : new()
        { BattleId = x.BattleId, DungeonId = x.DungeonId, RunGold = x.RunGold, AcceptedKills = Kills(x.AcceptedKills) };
        internal static EconomyBatchReceiptDTO Copy(EconomyBatchReceiptDTO x) => x == null ? null : new()
        {
            RequestId = x.RequestId, Generation = x.Generation, Sequence = x.Sequence, PayloadHash = x.PayloadHash,
            CommittedRevision = x.CommittedRevision, IndependentGrowthFailures = x.IndependentGrowthFailures,
            Results = x.Results.Select(r => new EconomyCommandResultDTO
            {
                Index = r.Index, Kind = r.Kind, Status = r.Status, AppliedCount = r.AppliedCount,
                SpentGold = r.SpentGold, SpentRuby = r.SpentRuby, SpentArcaneKnowledge = r.SpentArcaneKnowledge, SpentEquipmentStone = r.SpentEquipmentStone,
                GrantedGold = r.GrantedGold, GrantedExp = r.GrantedExp, ErrorCode = r.ErrorCode
            }).ToList()
        };
        internal static EconomyBatchStateDTO Copy(EconomyBatchStateDTO x) => x == null ? null : new()
        {
            Version = x.Version, OwnerClientId = x.OwnerClientId, Generation = x.Generation, NextSequence = x.NextSequence,
            RewardEpoch = x.RewardEpoch, GoldRemainderMicros = x.GoldRemainderMicros, ExpRemainderMicros = x.ExpRemainderMicros,
            PendingReceipt = Copy(x.PendingReceipt), OwnershipChanged = x.OwnershipChanged,
            SupportsMageEnhance = x.SupportsMageEnhance, MageEnhanceMode = x.MageEnhanceMode,
            IndependentGrowthFailures = x.IndependentGrowthFailures, EquipmentEnhanceAvailable = x.EquipmentEnhanceAvailable,
            RewardEpochs = x.RewardEpochs.Select(e => new EconomyRewardEpochDTO { Id = e.Id, GoldLevel = e.GoldLevel, ExpLevel = e.ExpLevel }).ToList()
        };
        internal static bool SameRun(EconomyCommandDTO a, EconomyCommandDTO b) => a.Stage == b.Stage && a.MainRunId == b.MainRunId && a.BattleId == b.BattleId;
        internal static Dictionary<ulong, int> Counts(IEnumerable<HuntResult> kills) => kills?.ToDictionary(x => (ulong)x.MonsterType, x => (int)x.Count) ?? new();
        internal static List<HuntResult> Kills(IReadOnlyDictionary<ulong, int> counts) => counts.OrderBy(x => x.Key)
            .Where(x => x.Value > 0).Select(x => new HuntResult { MonsterType = (Protocol.Message.Data.eMonsterType)x.Key, Count = checked((short)x.Value) }).ToList();
        internal static bool ValidKills(List<HuntResult> kills) => kills != null && kills.Count <= 10 &&
            kills.All(x => (ulong)x.MonsterType != 0 && x.Count > 0) && kills.Select(x => x.MonsterType).Distinct().Count() == kills.Count;
        internal static bool ValidRun(long stage, string main, string dungeon) => stage > 0 &&
            ((MainWaveProgression.ValidId(main) && dungeon == null) || (main == null && MainWaveProgression.ValidId(dungeon)));
    }
}
