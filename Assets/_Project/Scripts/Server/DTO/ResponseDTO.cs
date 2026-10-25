using Protocol.Message.Data;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Protocol.Message.Dto
{

// 서버 AuthenticationFunction/Common/Dto/ResponseDTO.cs의 사본(2026-10-25).
// 모든 필드는 Required.Always이고, 서버가 nullable(T?)로 선언한 필드만 Required.AllowNull이다
// (키는 있어야 하고 값은 null일 수 있다. 서버 System.Text.Json은 null도 출력한다).

// 정상 가입·로그인 완료 시 게임 진입에 필요한 데이터. OnGetUserState도 같은 스냅샷을 돌려준다.
public class UserInitResponseDTO : ApiResponseDTO
{
    [JsonProperty(Required = Required.Always)]
    public string SessionID { get; set; } = null!;
    [JsonProperty(Required = Required.Always)]
    public int Level { get; set; }
    [JsonProperty(Required = Required.Always)]
    public long Exp { get; set; }
    [JsonProperty(Required = Required.Always)]
    public long KillScore { get; set; }
    [JsonProperty(Required = Required.Always)]
    public eStage CurrentStage { get; set; }
    [JsonProperty(Required = Required.Always)]
    public long EnchantHPCount { get; set; }
    [JsonProperty(Required = Required.Always)]
    public long EnchantATKCount { get; set; }
    [JsonProperty(Required = Required.Always)]
    public List<CharacterDataQuery> CharacterDatas { get; set; } = null!;
    [JsonProperty(Required = Required.Always)]
    public List<CharacterEquipmentQuery> CharacterEquipmentDatas { get; set; } = null!;
    [JsonProperty(Required = Required.Always)]
    public CurrencyQueryDTO Currency { get; set; } = null!;
    [JsonProperty(Required = Required.Always)]
    public List<SkillCode> SkillTreeDatas { get; set; } = null!;
    [JsonProperty(Required = Required.Always)]
    public List<ItemCode> Inventory { get; set; } = null!;
    [JsonProperty(Required = Required.Always)]
    public List<JobTreeQuery> JobTrees { get; set; } = null!;
    // 상세 상태. 위 레거시 목록(Inventory, SkillTreeDatas, CharacterEquipmentDatas)은 이 상태의 투영이다.
    [JsonProperty(Required = Required.Always)]
    public EquipmentInventoryDTO Equipment { get; set; } = null!;
    [JsonProperty(Required = Required.Always)]
    public MageTowerStateDTO MageTowerState { get; set; } = null!;
    [JsonProperty(Required = Required.Always)]
    public List<CharacterDataQuery> CharacterJobs { get; set; } = null!;
    [JsonProperty(Required = Required.Always)]
    public GachaStateDTO GachaState { get; set; } = null!;
    [JsonProperty(Required = Required.Always)]
    public QuestStateDTO Quests { get; set; } = null!;
    [JsonProperty(Required = Required.Always)]
    public StageProgressDTO StageProgress { get; set; } = null!;
    [JsonProperty(Required = Required.Always)]
    public DungeonStateDTO DungeonState { get; set; } = null!;
    [JsonProperty(Required = Required.Always)]
    public ReincarnationStateDTO Reincarnation { get; set; } = null!;
    [JsonProperty(Required = Required.Always)]
    public AttendanceStateDTO Attendance { get; set; } = null!;
    // 마지막 오프라인 보상 정산 시각(UTC 초). 0이면 아직 기준 시각이 없다.
    [JsonProperty(Required = Required.Always)]
    public long OfflineLastClaimUtc { get; set; }
    [JsonProperty(Required = Required.Always)]
    public long ServerTimeUtc { get; set; }
}
// 전체 상태 재조회. 새 세션을 발급하지 않으며 SessionID는 요청값을 그대로 돌려준다.
public sealed class OnGetUserStateResponseDTO : UserInitResponseDTO { }
public sealed class OnHuntResponseDTO : ApiResponseDTO
{
    // 이번 배치의 장비 드롭과 적용 후 전체 장비·재화 상태.
    [JsonProperty(Required = Required.Always)]
    public List<GachaRewardDTO> Drops { get; set; } = null!;
    [JsonProperty(Required = Required.Always)]
    public EquipmentInventoryDTO Equipment { get; set; } = null!;
    [JsonProperty(Required = Required.Always)]
    public CurrencyQueryDTO Currency { get; set; } = null!;
    // 보상 적용 후의 절대값을 전달한다.
    [JsonProperty(Required = Required.Always)]
    public int Level { get; set; }
    [JsonProperty(Required = Required.Always)]
    public long Exp { get; set; }
    [JsonProperty(Required = Required.Always)]
    public long KillScore { get; set; }
    [JsonProperty(Required = Required.Always)]
    public long Gold { get; set; }
    [JsonProperty(Required = Required.Always)]
    public long AncientCoin { get; set; }
    // 다음 사냥 배치의 순번. 지급된 배치는 같은 순번으로 다시 지급하지 않는다.
    [JsonProperty(Required = Required.Always)]
    public long NextRewardSequence { get; set; }
}
public sealed class OnStageClearResponseDTO : ApiResponseDTO
{
    [JsonProperty(Required = Required.Always)]
    public bool IsFirstClear { get; set; }
    [JsonProperty(Required = Required.Always)]
    public List<QuestRewardDTO> GrantedRewards { get; set; } = null!;
    [JsonProperty(Required = Required.Always)]
    public StageProgressDTO StageProgress { get; set; } = null!;
    [JsonProperty(Required = Required.Always)]
    public EquipmentInventoryDTO Equipment { get; set; } = null!;
    [JsonProperty(Required = Required.Always)]
    public MageTowerStateDTO MageTowerState { get; set; } = null!;
    [JsonProperty(Required = Required.Always)]
    public CurrencyQueryDTO Currency { get; set; } = null!;
    // 다음 스테이지.
    [JsonProperty(Required = Required.Always)]
    public eStage Stage { get; set; }
}
public sealed class OnGachaEquipmentClassFragmentResponseDTO : ApiResponseDTO
{
    [JsonProperty(Required = Required.Always)]
    public List<GachaRewardDTO> Rewards { get; set; } = null!;
    [JsonProperty(Required = Required.Always)]
    public CurrencyQueryDTO Currency { get; set; } = null!;
    [JsonProperty(Required = Required.Always)]
    public GachaStateDTO GachaState { get; set; } = null!;
    [JsonProperty(Required = Required.Always)]
    public EquipmentInventoryDTO Equipment { get; set; } = null!;
    [JsonProperty(Required = Required.Always)]
    public List<ItemCode> GachaList { get; set; } = null!;
    [JsonProperty(Required = Required.Always)]
    public int ClassFragmentCnt { get; set; }
}
public sealed class OnGachaSkillArcaneKnowledgeResponseDTO : ApiResponseDTO
{
    [JsonProperty(Required = Required.Always)]
    public List<GachaRewardDTO> Rewards { get; set; } = null!;
    [JsonProperty(Required = Required.Always)]
    public CurrencyQueryDTO Currency { get; set; } = null!;
    [JsonProperty(Required = Required.Always)]
    public MageTowerStateDTO MageTowerState { get; set; } = null!;
    [JsonProperty(Required = Required.Always)]
    public List<SkillCode> GachaList { get; set; } = null!;
    [JsonProperty(Required = Required.Always)]
    public int ArcaneKnowledgeCnt { get; set; }
}
public sealed class OnEnchantResponseDTO : ApiResponseDTO
{
    [JsonProperty(Required = Required.Always)]
    public int AppliedCount { get; set; }
    [JsonProperty(Required = Required.Always)]
    public long SpentGold { get; set; }
    [JsonProperty(Required = Required.Always)]
    public long CurrentLevel { get; set; }
    [JsonProperty(Required = Required.Always)]
    public long CurrentGold { get; set; }
}

// 장비 변경 응답의 공통 베이스. 변경 후 전체 장비 상태를 담는다.
public abstract class EquipmentActionResponseDTO : ApiResponseDTO
{
    [JsonProperty(Required = Required.Always)]
    public EquipmentInventoryDTO Equipment { get; set; } = null!;
}
public sealed class OnEnchantEquipmentResponseDTO : EquipmentActionResponseDTO
{
    [JsonProperty(Required = Required.Always)]
    public long SpentEquipmentStone { get; set; }
    [JsonProperty(Required = Required.Always)]
    public long CurrentEquipmentStone { get; set; }
    // 강화 후 코드.
    [JsonProperty(Required = Required.Always)]
    public ItemCode ItemCode { get; set; }
}
public sealed class OnGetEquipmentInventoryResponseDTO : EquipmentActionResponseDTO { }
public sealed class OnEquipEquipmentResponseDTO : EquipmentActionResponseDTO { }
public sealed class OnUnequipEquipmentResponseDTO : EquipmentActionResponseDTO { }
public sealed class OnSetEquipmentLockResponseDTO : EquipmentActionResponseDTO { }
public sealed class OnDismantleEquipmentResponseDTO : EquipmentActionResponseDTO
{
    [JsonProperty(Required = Required.Always)]
    public long GrantedEquipmentStone { get; set; }
    [JsonProperty(Required = Required.Always)]
    public long CurrentEquipmentStone { get; set; }
}
public sealed class OnSetAutoDismantleResponseDTO : EquipmentActionResponseDTO { }
public sealed class OnClaimPendingEquipmentResponseDTO : EquipmentActionResponseDTO { }
public sealed class OnClaimReserveEquipmentResponseDTO : EquipmentActionResponseDTO { }

// 마탑 변경 응답의 공통 베이스. 변경 후 전체 마탑 상태를 담는다.
public abstract class MageActionResponseDTO : ApiResponseDTO
{
    [JsonProperty(Required = Required.Always)]
    public MageTowerStateDTO MageTowerState { get; set; } = null!;
}
public sealed class OnEnchantSkillResponseDTO : MageActionResponseDTO
{
    [JsonProperty(Required = Required.Always)]
    public long SpentArcaneKnowledge { get; set; }
    [JsonProperty(Required = Required.Always)]
    public long CurrentArcaneKnowledge { get; set; }
    // 조각이 65,535개를 넘어 압축 코드로 표현할 수 없으면 서버가 null을 보낸다. 상태 기준은 MageTowerState다.
    [JsonProperty(Required = Required.AllowNull)]
    public SkillCode? SkillCode { get; set; }
}
public sealed class OnAwakeningSkillResponseDTO : MageActionResponseDTO
{
    [JsonProperty(Required = Required.Always)]
    public int SpentFragments { get; set; }
    // 조각이 65,535개를 넘어 압축 코드로 표현할 수 없으면 서버가 null을 보낸다. 상태 기준은 MageTowerState다.
    [JsonProperty(Required = Required.AllowNull)]
    public SkillCode? SkillCode { get; set; }
}
public sealed class OnSetSkillLoadoutResponseDTO : MageActionResponseDTO { }
public sealed class OnSetSkillBloomResponseDTO : MageActionResponseDTO { }
public sealed class OnResetSkillEnhancementResponseDTO : MageActionResponseDTO
{
    [JsonProperty(Required = Required.Always)]
    public long RefundedArcaneKnowledge { get; set; }
    [JsonProperty(Required = Required.Always)]
    public long CurrentArcaneKnowledge { get; set; }
}

// 직업 변경 응답의 공통 베이스.
public abstract class JobActionResponseDTO : ApiResponseDTO
{
    [JsonProperty(Required = Required.Always)]
    public int Index { get; set; }
    [JsonProperty(Required = Required.Always)]
    public ulong JobCode { get; set; }
    [JsonProperty(Required = Required.Always)]
    public List<CharacterDataQuery> CharacterJobs { get; set; } = null!;
    [JsonProperty(Required = Required.Always)]
    public List<JobTreeQuery> JobTrees { get; set; } = null!;
    [JsonProperty(Required = Required.Always)]
    public EquipmentInventoryDTO Equipment { get; set; } = null!;
}
public sealed class OnGetJobResponseDTO : JobActionResponseDTO
{
    [JsonProperty(Required = Required.Always)]
    public bool IsNewUnlock { get; set; }
    [JsonProperty(Required = Required.Always)]
    public long SpentClassFragment { get; set; }
    [JsonProperty(Required = Required.Always)]
    public long CurrentClassFragment { get; set; }
}
public sealed class OnChangeJobResponseDTO : JobActionResponseDTO { }
public sealed class OnSetJobTreeResponseDTO : ApiResponseDTO
{
    [JsonProperty(Required = Required.Always)]
    public List<JobTreeQuery> JobTrees { get; set; } = null!;
}
public sealed class OnDeleteAccountResponseDTO : ApiResponseDTO
{
    // PlayFab이 계정 삭제를 접수하면 true. 실패는 ErrorResponseDTO로 전달한다.
    [JsonProperty(Required = Required.Always)]
    public bool IsSuccess { get; set; }
}

// 던전
public abstract class DungeonActionResponseDTO : ApiResponseDTO
{
    [JsonProperty(Required = Required.Always)]
    public DungeonStateDTO DungeonState { get; set; } = null!;
}
// 입장 결과만 받는다. 티켓·클리어 단계는 완료·포기 응답이나 OnGetUserState의 DungeonState로 확인한다.
public sealed class OnEnterDungeonResponseDTO : ApiResponseDTO
{
    [JsonProperty(Required = Required.Always)]
    public long DungeonId { get; set; }
    // 이 전투의 사냥(OnHuntReward.BattleId)·완료·포기 요청에 그대로 넣는다.
    [JsonProperty(Required = Required.Always)]
    public string BattleId { get; set; } = null!;
}
public sealed class OnCompleteDungeonResponseDTO : DungeonActionResponseDTO
{
    [JsonProperty(Required = Required.Always)]
    public bool IsFirstClear { get; set; }
    [JsonProperty(Required = Required.Always)]
    public List<QuestRewardDTO> GrantedRewards { get; set; } = null!;
    [JsonProperty(Required = Required.Always)]
    public List<QuestRewardDTO> TotalRunRewards { get; set; } = null!;
    [JsonProperty(Required = Required.Always)]
    public CurrencyQueryDTO Currency { get; set; } = null!;
    [JsonProperty(Required = Required.Always)]
    public EquipmentInventoryDTO Equipment { get; set; } = null!;
    [JsonProperty(Required = Required.Always)]
    public MageTowerStateDTO MageTowerState { get; set; } = null!;
}
public sealed class OnExitDungeonResponseDTO : DungeonActionResponseDTO { }

// 퀘스트·업적
public abstract class QuestActionResponseDTO : ApiResponseDTO
{
    [JsonProperty(Required = Required.Always)]
    public QuestStateDTO Quests { get; set; } = null!;
}
public sealed class OnGetQuestStateResponseDTO : QuestActionResponseDTO { }
public sealed class OnClearQuestResponseDTO : QuestActionResponseDTO { }
public sealed class OnClearAchievementResponseDTO : QuestActionResponseDTO { }
public sealed class OnClaimQuestRewardResponseDTO : QuestActionResponseDTO
{
    [JsonProperty(Required = Required.Always)]
    public List<QuestRewardDTO> GrantedRewards { get; set; } = null!;
    [JsonProperty(Required = Required.Always)]
    public CurrencyQueryDTO Currency { get; set; } = null!;
    [JsonProperty(Required = Required.Always)]
    public EquipmentInventoryDTO Equipment { get; set; } = null!;
    [JsonProperty(Required = Required.Always)]
    public MageTowerStateDTO MageTowerState { get; set; } = null!;
}

// 환생·오프라인·루비
public sealed class OnReincarnationResponseDTO : ApiResponseDTO
{
    [JsonProperty(Required = Required.Always)]
    public int GainedLevel { get; set; }
    [JsonProperty(Required = Required.Always)]
    public ReincarnationStateDTO Reincarnation { get; set; } = null!;
    [JsonProperty(Required = Required.Always)]
    public eStage CurrentStage { get; set; }
    [JsonProperty(Required = Required.Always)]
    public long EnchantHPCount { get; set; }
    [JsonProperty(Required = Required.Always)]
    public long EnchantATKCount { get; set; }
    [JsonProperty(Required = Required.Always)]
    public CurrencyQueryDTO Currency { get; set; } = null!;
}
public sealed class OnClaimOfflineRewardResponseDTO : ApiResponseDTO
{
    // 기준 시각부터 지난 실제 시간과 보상에 적용한 시간(최대 8시간). 5분 미만이면 0 보상이다.
    [JsonProperty(Required = Required.Always)]
    public long ElapsedSeconds { get; set; }
    [JsonProperty(Required = Required.Always)]
    public long AppliedSeconds { get; set; }
    [JsonProperty(Required = Required.Always)]
    public long KillCount { get; set; }
    [JsonProperty(Required = Required.Always)]
    public long GainedGold { get; set; }
    [JsonProperty(Required = Required.Always)]
    public long GainedExp { get; set; }
    [JsonProperty(Required = Required.Always)]
    public int Level { get; set; }
    [JsonProperty(Required = Required.Always)]
    public long Exp { get; set; }
}
public sealed class OnAddRubyResponseDTO : ApiResponseDTO
{
    [JsonProperty(Required = Required.Always)]
    public long GrantedRuby { get; set; }
    [JsonProperty(Required = Required.Always)]
    public bool IsFirstClear { get; set; }
    [JsonProperty(Required = Required.Always)]
    public DungeonStateDTO DungeonState { get; set; } = null!;
}

// 우편·출석
public sealed class OnGetMailListResponseDTO : ApiResponseDTO
{
    // 아직 우편을 받은 적이 없으면 null이다.
    [JsonProperty(Required = Required.AllowNull)]
    public string MailboxId { get; set; }
    [JsonProperty(Required = Required.Always)]
    public int StartIndex { get; set; }
    // 만료 우편을 제외한 전체 개수. Mails는 StartIndex부터 요청한 개수까지만 담는다(0이 최신).
    [JsonProperty(Required = Required.Always)]
    public int TotalCount { get; set; }
    [JsonProperty(Required = Required.Always)]
    public List<MailDTO> Mails { get; set; } = null!;
    [JsonProperty(Required = Required.Always)]
    public long ServerTimeUtc { get; set; }
}
public sealed class OnClaimMailRewardResponseDTO : ApiResponseDTO
{
    [JsonProperty(Required = Required.Always)]
    public string MailboxId { get; set; } = null!;
    [JsonProperty(Required = Required.Always)]
    public List<long> ClaimedMailIds { get; set; } = null!;
    [JsonProperty(Required = Required.Always)]
    public List<GachaRewardDTO> Rewards { get; set; } = null!;
    [JsonProperty(Required = Required.Always)]
    public int RemainingCount { get; set; }
    [JsonProperty(Required = Required.Always)]
    public CurrencyQueryDTO Currency { get; set; } = null!;
    [JsonProperty(Required = Required.Always)]
    public EquipmentInventoryDTO Equipment { get; set; } = null!;
}
public sealed class OnCheckAttendanceResponseDTO : ApiResponseDTO
{
    [JsonProperty(Required = Required.Always)]
    public AttendanceStateDTO Attendance { get; set; } = null!;
    // 이번 출석으로 지급한 보상의 일차(1~28).
    [JsonProperty(Required = Required.Always)]
    public int CycleDay { get; set; }
    [JsonProperty(Required = Required.Always)]
    public long NextResetUtc { get; set; }
    // 보상표의 해당 일차 보상을 우편 없이 바로 지급한 결과. 장비는 Disposition으로 보관 위치를 알린다.
    [JsonProperty(Required = Required.Always)]
    public List<GachaRewardDTO> Rewards { get; set; } = null!;
}

}
