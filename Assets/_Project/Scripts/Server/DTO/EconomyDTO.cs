using System.Collections.Generic;
using Newtonsoft.Json;
using Protocol.Message.Data;

namespace Protocol.Message.Dto
{
    // 서버 Common/Dto/EconomyDTO.cs와 함께 변경한다. Version 1은 계산 규칙의 호환성도 포함한다.
    public enum EconomyCommandKind { Hunt = 0, GoldAttack = 1, GoldHealth = 2, RubyGold = 3, RubyExperience = 4 }
    public enum EconomyCommandStatus { Applied = 0, Rejected = 1, Unprocessed = 2 }
    public enum EconomyBatchStatus { Completed = 0, NotFound = 1, Conflict = 2, Retired = 3 }

    public sealed class OnEconomyBatchRequestDTO : SessionRequestDTO
    {
        public string RequestId { get; set; }
        public long Generation { get; set; }
        public long Sequence { get; set; }
        public long ExpectedEconomyRevision { get; set; }
        public long AppliedRewardEpoch { get; set; }
        public string AcknowledgedRequestId { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)] public string QuestVersion { get; set; }
        public List<EconomyRewardEpochCutoffDTO> RewardEpochCutoffs { get; set; } = new();
        public List<EconomyCommandDTO> Commands { get; set; } = new();
    }

    public sealed class OnGetEconomyBatchResultRequestDTO : SessionRequestDTO
    {
        public string RequestId { get; set; }
        public long Generation { get; set; }
        public long Sequence { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)] public string QuestVersion { get; set; }
    }

    public sealed class EconomyCommandDTO
    {
        public EconomyCommandKind Kind { get; set; }
        public int Mode { get; set; }
        public int ExpectedLevel { get; set; }
        public int CountLimit { get; set; }
        public long SpendLimit { get; set; }
        public eStage Stage { get; set; }
        public string MainRunId { get; set; }
        public string BattleId { get; set; }
        public long RewardEpoch { get; set; }
        public List<HuntResult> TotalKills { get; set; }
    }

    public sealed class EconomyCommandResultDTO
    {
        [JsonProperty(Required = Required.Always)] public int Index { get; set; }
        [JsonProperty(Required = Required.Always)] public EconomyCommandKind Kind { get; set; }
        [JsonProperty(Required = Required.Always)] public EconomyCommandStatus Status { get; set; }
        [JsonProperty(Required = Required.Always)] public int AppliedCount { get; set; }
        [JsonProperty(Required = Required.Always)] public long SpentGold { get; set; }
        [JsonProperty(Required = Required.Always)] public long SpentRuby { get; set; }
        [JsonProperty(Required = Required.Always)] public long GrantedGold { get; set; }
        [JsonProperty(Required = Required.Always)] public long GrantedExp { get; set; }
        [JsonProperty(Required = Required.AllowNull)] public eErrorCode? ErrorCode { get; set; }
    }

    public sealed class EconomyBatchReceiptDTO
    {
        [JsonProperty(Required = Required.Always)] public string RequestId { get; set; }
        [JsonProperty(Required = Required.Always)] public long Generation { get; set; }
        [JsonProperty(Required = Required.Always)] public long Sequence { get; set; }
        [JsonProperty(Required = Required.Always)] public string PayloadHash { get; set; }
        [JsonProperty(Required = Required.Always)] public long CommittedRevision { get; set; }
        [JsonProperty(Required = Required.Always)] public List<EconomyCommandResultDTO> Results { get; set; }
    }

    public sealed class OnEconomyBatchResponseDTO : ApiResponseDTO
    {
        [JsonProperty(Required = Required.Always)] public string RequestId { get; set; }
        [JsonProperty(Required = Required.Always)] public EconomyBatchStatus Status { get; set; }
        [JsonProperty(Required = Required.Always)] public bool Replayed { get; set; }
        [JsonProperty(Required = Required.AllowNull)] public EconomyBatchReceiptDTO Receipt { get; set; }
        [JsonProperty(Required = Required.Always)] public EconomyStateDTO CurrentState { get; set; }
        public QuestStateDTO Quests { get; set; }
        public bool? QuestRefreshRequired { get; set; }
        public QuestChangesDTO QuestChanges { get; set; }
    }

    public sealed class QuestChangesDTO
    {
        public string BaseVersion { get; set; }
        public string Version { get; set; }
        public List<QuestRowDTO> Rows { get; set; }
    }

    public sealed class EconomyStateDTO
    {
        [JsonProperty(Required = Required.Always)] public CurrencyQueryDTO Currency { get; set; }
        [JsonProperty(Required = Required.Always)] public long EnchantATKCount { get; set; }
        [JsonProperty(Required = Required.Always)] public long EnchantHPCount { get; set; }
        [JsonProperty(Required = Required.Always)] public RubyGrowthStateDTO RubyGrowthState { get; set; }
        [JsonProperty(Required = Required.Always)] public int Level { get; set; }
        [JsonProperty(Required = Required.Always)] public long Exp { get; set; }
        [JsonProperty(Required = Required.Always)] public long KillScore { get; set; }
        [JsonProperty(Required = Required.Always)] public UserStageQuery StageState { get; set; }
        [JsonProperty(Required = Required.AllowNull)] public MainBattleStateDTO MainBattle { get; set; }
        [JsonProperty(Required = Required.AllowNull)] public EconomyDungeonBattleDTO DungeonBattle { get; set; }
        [JsonProperty(Required = Required.Always)] public EconomyBatchStateDTO EconomyBatchState { get; set; }
    }

    public sealed class EconomyDungeonBattleDTO
    {
        [JsonProperty(Required = Required.Always)] public string BattleId { get; set; }
        [JsonProperty(Required = Required.Always)] public long DungeonId { get; set; }
        [JsonProperty(Required = Required.Always)] public List<HuntResult> AcceptedKills { get; set; }
        [JsonProperty(Required = Required.Always)] public long RunGold { get; set; }
    }

    public sealed class EconomyBatchStateDTO
    {
        [JsonProperty(Required = Required.Always)] public int Version { get; set; }
        [JsonProperty(Required = Required.Always)] public string OwnerClientId { get; set; }
        [JsonProperty(Required = Required.Always)] public long Generation { get; set; }
        [JsonProperty(Required = Required.Always)] public long NextSequence { get; set; }
        [JsonProperty(Required = Required.Always)] public long RewardEpoch { get; set; }
        [JsonProperty(Required = Required.Always)] public List<EconomyRewardEpochDTO> RewardEpochs { get; set; }
        [JsonProperty(Required = Required.Always)] public long GoldRemainderMicros { get; set; }
        [JsonProperty(Required = Required.Always)] public long ExpRemainderMicros { get; set; }
        [JsonProperty(Required = Required.AllowNull)] public EconomyBatchReceiptDTO PendingReceipt { get; set; }
        [JsonProperty(Required = Required.Always)] public bool OwnershipChanged { get; set; }
    }

    public sealed class EconomyRewardEpochDTO
    {
        [JsonProperty(Required = Required.Always)] public long Id { get; set; }
        [JsonProperty(Required = Required.Always)] public int GoldLevel { get; set; }
        [JsonProperty(Required = Required.Always)] public int ExpLevel { get; set; }
    }

    public sealed class EconomyRewardEpochCutoffDTO
    {
        public long Epoch { get; set; }
        public eStage Stage { get; set; }
        public string MainRunId { get; set; }
        public string BattleId { get; set; }
        public List<HuntResult> TotalKills { get; set; }
    }
}
