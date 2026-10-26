using Protocol.Message.Data;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace Protocol.Message.Dto
{
	// 서버 AuthenticationFunction/Common/Dto/RequestDTO.cs의 사본(2026-10-25). JSON 키는 C# 속성 이름이다.
	// Required.Always는 호출자가 비워 둔 참조형 필드를 요청 사본 직렬화 단계에서 거절해 InvalidRequest로 빨리 실패시킨다.

	public abstract class RequestDTO
	{
		public int ProtocolVersion { get; set; }
	}

	// 가입·로그인을 제외한 API의 요청. Redis의 현재 세션과 비교하여 해당 플레이어의 접속 상태를 확인한다.
	public abstract class SessionRequestDTO : RequestDTO
	{
		public string SessionID { get; set; } = string.Empty;
	}

	// 가입·로그인은 서버에서 세션을 발급받는 API라 SessionID가 없다. 발급한 세션은 응답으로 전달한다.
	public sealed class InitUserRequestDTO : RequestDTO { }
	public sealed class AuthRequestDTO : RequestDTO { }

	public struct HuntResult
	{
		public eMonsterType MonsterType { get; set; }
		public short Count { get; set; }
	}
	public sealed class OnRewardRequestDTO : SessionRequestDTO
	{
		public eStage Stage { get; set; }
		public List<HuntResult> Loots { get; set; } = null!;
		// 던전 사냥은 OnEnterDungeon 응답의 BattleId를 넣는다. 메인 스테이지는 null로 둔다(값이 있으면 1025).
		public string BattleId { get; set; }
	}
	public sealed class OnStageClearRequestDTO : SessionRequestDTO
	{
		public eStage ClearedStage { get; set; }
	}
	public class OnGachaRequestDTO : SessionRequestDTO
	{
		public int Count { get; set; }
	}
	// Client routing types share the server's existing JSON shape; no routing field is sent.
	public sealed class OnGachaEquipmentClassFragmentRequestDTO : OnGachaRequestDTO { }
	public sealed class OnGachaSkillArcaneKnowledgeRequestDTO : OnGachaRequestDTO { }
	public class OnEnchantRequestDTO : SessionRequestDTO
	{
		public int Count { get; set; }
	}
	public sealed class OnEnchantHPRequestDTO : OnEnchantRequestDTO { }
	public sealed class OnEnchantATKRequestDTO : OnEnchantRequestDTO { }
	// 장비 대상 요청의 공통 필드. 종류·강화·만료값으로 대상 묶음을 찾고, 보유 수량/지출은 서버 저장값을 쓴다.
	public class EquipmentItemRequestDTO : SessionRequestDTO
	{
		// 대상 코드를 생략한 요청이 기본값 아이템을 선택하지 않도록 필수로 한다.
		[JsonProperty(Required = Required.Always)]
		public ItemCode ItemCode { get; set; }
	}
	public sealed class OnEnchantEquipmentRequestDTO : EquipmentItemRequestDTO
	{
		// null이면 미장착 수량 1개를 강화한다. 장착 중인 장비는 해당 캐릭터 인덱스(0~2)를 지정한다.
		public int? CharacterIndex { get; set; }
	}
	public class OnEnchantSkillBaseRequest : SessionRequestDTO
	{
		// 스킬 ID 0도 유효하므로 필드 누락과 명시적으로 보낸 코드 0을 구분한다.
		[JsonProperty(Required = Required.Always)]
		public SkillCode SkillCode { get; set; }
	}
	public sealed class OnEnchantSkillRequestDTO : OnEnchantSkillBaseRequest { }
	public sealed class OnAwakeningSkillRequestDTO : OnEnchantSkillBaseRequest { }
	public class OnJobRequestBaseDTO : SessionRequestDTO
	{
		// 생략된 인덱스를 캐릭터 0으로 처리하지 않도록 대상을 명시하게 한다.
		[JsonProperty(Required = Required.Always)]
		public int Index { get; set; }
		public ulong JobCode { get; set; }
	}
	public sealed class OnGetJobRequestDTO : OnJobRequestBaseDTO { }
	public sealed class OnChangeJobRequestDTO : OnJobRequestBaseDTO { }
	public sealed class OnSetJobTreeRequestDTO : SessionRequestDTO { }
	// 탈퇴는 가입·로그인에서 발급받은 SessionID를 전달한다. 그 외 입력은 없다.
	public sealed class OnDeleteAccountRequestDTO : SessionRequestDTO { }

	// 전체 상태 재조회. 새 세션을 발급하지 않는다.
	public sealed class OnGetUserStateRequestDTO : SessionRequestDTO { }

	// 장비
	public sealed class OnGetEquipmentInventoryRequestDTO : SessionRequestDTO { }
	public sealed class OnEquipEquipmentRequestDTO : EquipmentItemRequestDTO
	{
		[JsonProperty(Required = Required.Always)]
		public int CharacterIndex { get; set; }
	}
	public sealed class OnUnequipEquipmentRequestDTO : EquipmentItemRequestDTO
	{
		[JsonProperty(Required = Required.Always)]
		public int CharacterIndex { get; set; }
	}
	public sealed class OnSetEquipmentLockRequestDTO : EquipmentItemRequestDTO
	{
		[JsonProperty(Required = Required.Always)]
		public bool IsLocked { get; set; }
	}
	public enum DismantleMode { Single, Bulk }
	public enum EquipmentLocation { Inventory, Pending }
	public sealed class DismantleItemDTO
	{
		// 코드에 들어 있는 수량만 분해한다. 환급액은 서버의 실제 지출로 계산한다.
		[JsonProperty(Required = Required.Always)]
		public ItemCode ItemCode { get; set; }
		[JsonProperty(Required = Required.Always)]
		public EquipmentLocation Location { get; set; }
	}
	public sealed class DismantleReserveDTO
	{
		[JsonProperty(Required = Required.Always)]
		public ItemCode ItemCode { get; set; }
		// 예비 수량은 압축 코드의 16비트 수량을 넘을 수 있으므로 Count를 사용한다.
		[JsonProperty(Required = Required.Always)]
		public int Count { get; set; }
	}
	public sealed class OnDismantleEquipmentRequestDTO : SessionRequestDTO
	{
		// Single은 장비 1개, Bulk는 명시한 +0 장비 수량들을 일괄 분해한다.
		[JsonProperty(Required = Required.Always)]
		public DismantleMode Mode { get; set; }
		// 주 보관함/대기함의 대상 코드·수량·위치. 서버가 보유/잠금/장착 여부를 검사한다.
		[JsonProperty(Required = Required.Always)]
		public List<DismantleItemDTO> Items { get; set; } = null!;
		// 예비 보관의 대상 코드와 수량. 없으면 빈 목록을 보낸다.
		[JsonProperty(Required = Required.Always)]
		public List<DismantleReserveDTO> ReserveStacks { get; set; } = null!;
	}
	public sealed class OnSetAutoDismantleRequestDTO : SessionRequestDTO
	{
		// bit0 Normal, bit1 Rare, bit2 Epic. 0~7.
		[JsonProperty(Required = Required.Always)]
		public int RarityMask { get; set; }
	}
	public sealed class OnClaimPendingEquipmentRequestDTO : EquipmentItemRequestDTO { }
	public sealed class OnClaimReserveEquipmentRequestDTO : EquipmentItemRequestDTO { }

	// 마탑 스킬
	public sealed class OnSetSkillLoadoutRequestDTO : SessionRequestDTO
	{
		// 슬롯 5칸. 빈 슬롯은 -1이다.
		[JsonProperty(Required = Required.Always)]
		public int[] EquippedSkillIds { get; set; } = null!;
	}
	public class SkillIdRequestDTO : SessionRequestDTO
	{
		[JsonProperty(Required = Required.Always)]
		public int SkillId { get; set; }
	}
	public sealed class OnSetSkillBloomRequestDTO : SkillIdRequestDTO
	{
		[JsonProperty(Required = Required.Always)]
		public bool Enabled { get; set; }
	}
	public sealed class OnResetSkillEnhancementRequestDTO : SkillIdRequestDTO { }

	// 던전
	public sealed class OnEnterDungeonRequestDTO : SessionRequestDTO
	{
		[JsonProperty(Required = Required.Always)]
		public long DungeonId { get; set; }
	}
	public sealed class OnCompleteDungeonRequestDTO : SessionRequestDTO
	{
		[JsonProperty(Required = Required.Always)]
		public long DungeonId { get; set; }
		[JsonProperty(Required = Required.Always)]
		public string BattleId { get; set; } = null!;
	}
	public enum DungeonExitReason { Failed, Abandoned }
	public sealed class OnExitDungeonRequestDTO : SessionRequestDTO
	{
		[JsonProperty(Required = Required.Always)]
		public string BattleId { get; set; } = null!;
		[JsonProperty(Required = Required.Always)]
		public DungeonExitReason Reason { get; set; }
	}

	// 퀘스트·업적
	public sealed class OnGetQuestStateRequestDTO : SessionRequestDTO { }
	public class QuestTargetRequestDTO : SessionRequestDTO
	{
		[JsonProperty(Required = Required.Always)]
		public long QuestId { get; set; }
		// "permanent" 또는 KST 기준 yyyy-MM-dd.
		[JsonProperty(Required = Required.Always)]
		public string PeriodKey { get; set; } = null!;
	}
	public sealed class OnClearQuestRequestDTO : QuestTargetRequestDTO { }
	public sealed class OnClearAchievementRequestDTO : QuestTargetRequestDTO { }
	public sealed class OnClaimQuestRewardRequestDTO : QuestTargetRequestDTO { }

	// 환생·오프라인·루비
	// 환생 조건과 결과는 서버 저장값으로 판단한다. 그 외 입력은 없다.
	public sealed class OnReincarnationRequestDTO : SessionRequestDTO { }
	public sealed class OnClaimOfflineRewardRequestDTO : SessionRequestDTO
	{
		// 오프라인 전 분당 처치 수. 서버가 30 이하로 제한하며 경과 시간과 보상량은 저장값으로 계산한다.
		[JsonProperty(Required = Required.Always)]
		public decimal KillsPerMinute { get; set; }
	}
	public sealed class OnAddRubyRequestDTO : SessionRequestDTO
	{
		// 클리어한 루비 던전 난이도(1~5). 지급량은 서버의 던전 정의로 계산한다.
		[JsonProperty(Required = Required.Always)]
		public int Difficulty { get; set; }
	}

	// 우편·출석
	// 우편 요청의 공통 필드. 호출자가 PlayFabId를 채우지 않아도 NetworkManager가 로그인한 계정의 값으로 덮어쓴다.
	// 서버는 호출자와 다른 PlayFabId를 1042로 거절한다. Required를 두지 않는 이유는 요청 사본을 만드는 직렬화가 먼저 실행되기 때문이다.
	public abstract class MailboxRequestDTO : SessionRequestDTO
	{
		public string PlayFabId { get; set; }
	}
	public sealed class OnGetMailListRequestDTO : MailboxRequestDTO
	{
		// 0이 가장 최근 우편이다.
		[JsonProperty(Required = Required.Always)]
		public int StartIndex { get; set; }
		// 1~50.
		[JsonProperty(Required = Required.Always)]
		public int Count { get; set; }
	}
	public sealed class OnClaimMailRewardRequestDTO : MailboxRequestDTO
	{
		// 한 번에 받을 우편(1~50개, 양수, 중복 없음). 하나라도 받을 수 없으면 서버가 모두 거절한다.
		[JsonProperty(Required = Required.Always)]
		public List<long> MailIds { get; set; } = null!;
	}
	// 출석 일차와 보상은 서버 저장값과 KST 날짜로 정한다. 그 외 입력은 없다.
	public sealed class OnCheckAttendanceRequestDTO : SessionRequestDTO { }
}
