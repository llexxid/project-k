using Protocol.Message.Data;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Protocol.Message.Dto
{
	public struct JobStat
	{
		public int HP { get; set; }
		public int ATK { get; set; }
	}

	/* 스킬트리 LayOut 
 * 스킬트리 코드
	[63 - 52] 예약 공간 (12bit)
	[51 - 36] 스킬 코드 (16bit)
	[35 - 28] 강화수치 (8bit)
	[27 - 16] 각성수치 (12bit)
	[15 - 0] 갯수 (16bit)
*/
	public struct SkillCode : IEquatable<SkillCode>
	{
		// 빈 객체를 스킬 ID 0으로 오인하여 강화하지 않도록 코드 값 자체를 필수로 한다.
		[JsonProperty(Required = Required.Always)]
		public ulong Code { get; set; }

		public static SkillCode GenerateSkillCode(eSkillCode skillID)
		{
			SkillCode ret = new SkillCode
			{
				Code = (ulong)skillID << 36
			};
			return ret;
		}

		public override int GetHashCode()
		{
			return GetSkillId().GetHashCode();
		}

		//기존 Equals override
		public override bool Equals(object obj)
		{
			if (obj is SkillCode other)
			{
				return Equals(other);
			}
			return false;
		}
		//IEquatable Equals 구현
		public bool Equals(SkillCode other)
		{
			return GetSkillId() == other.GetSkillId();
		}

		public static bool operator ==(SkillCode lhs, SkillCode rhs)
		{
			return lhs.Equals(rhs);
		}

		public static bool operator !=(SkillCode lhs, SkillCode rhs)
		{
			return !(lhs.Equals(rhs));
		}

		// 필드 범위를 넘거나 gap이 음수면 false를 반환하고 code는 기존 값이다. 예외를 던지지 않는다.
		public bool TryIncreaseCount(int gap, out ulong code)
		{
			code = Code;
			if (gap < 0 || GetSkillAmount() + (ulong)gap >= 0x0000000000010000)
			{
				return false;
			}
			code = Code + (ulong)gap;
			return true;
		}
		public bool TryIncreaseEnchant(int gap, out ulong code)
		{
			code = Code;
			if (gap < 0 || GetEnchantCount() + (ulong)gap >= 0x0000000000000100)
			{
				return false;
			}
			code = Code + ((ulong)gap << 28);
			return true;
		}
		public bool TryIncreaseAwakening(int gap, out ulong code)
		{
			code = Code;
			if (gap < 0 || GetAwakeningCount() + (ulong)gap >= 0x0000000000001000)
			{
				return false;
			}
			code = Code + ((ulong)gap << 16);
			return true;
		}
		public bool TryDecreaseCount(int gap, out ulong code)
		{
			code = Code;
			if (gap < 0 || (ulong)gap > GetSkillAmount())
			{
				return false;
			}
			code = Code - (ulong)gap;
			return true;
		}

		// 실패하면 기존 값을 그대로 반환한다. 성공 여부가 필요하면 Try 메서드를 쓴다.
		public ulong IncreaseCount(int gap = 1) => TryIncreaseCount(gap, out ulong code) ? code : Code;
		public ulong IncreaseEnchant(int gap = 1) => TryIncreaseEnchant(gap, out ulong code) ? code : Code;
		public ulong IncreaseAwakening(int gap = 1) => TryIncreaseAwakening(gap, out ulong code) ? code : Code;
		public ulong DecreaseCount(int gap = 1) => TryDecreaseCount(gap, out ulong code) ? code : Code;

		public ulong GetSkillId()
		{
			ulong skillcodeMask = 0x000FFFF000000000;
			return (Code & skillcodeMask) >> 36;
		}
		public ulong GetEnchantCount()
		{
			ulong EnchantCountMask = 0x0000000FF0000000;
			return (Code & EnchantCountMask) >> 28;
		}
		public ulong GetAwakeningCount()
		{
			ulong AwakeCountMask = 0x000000000FFF0000;
			return (Code & AwakeCountMask) >> 16;
		}
		public ulong GetSkillAmount()
		{
			ulong SkillAmountMask = 0x000000000000FFFF;
			return (Code & SkillAmountMask);
		}
	}
	/*
	 아이템 코드
[63 - 56]  예약공간   (8bit)
[55 - 40]  아이템 ID (16bit)
[39 - 36]  레어도    (4bit) → eEquipmentRarity
[35 - 32]   슬롯      (4bit) → eEquipmentSlot
[31 - 24] 직업 마스크(8bit) → eJobFlag  (0 = 모든 직업 공용) 
[23 - 16]  강화 수치    (8bit)
[15 - 0]  갯수 (16bit)
	 */
	public struct ItemCode : IEquatable<ItemCode>
	{
		// 수량·강화는 서버 상태를 사용하지만 식별에 필요한 코드 값은 생략할 수 없다.
		[JsonProperty(Required = Required.Always)]
		public ulong Code { get; set; }
		public ulong ExpireTimesc { get; set; } // 0인경우 무제한

		public override int GetHashCode()
		{
			// ulong 내부에 구현된 GetHashCode를 그대로 사용합니다.
			return GetItemCode().GetHashCode();
		}

		//기존 Equals override
		public override bool Equals(object obj)
		{
			if (obj is ItemCode other)
			{
				return Equals(other);
			}
			return false;
		}
		//IEquatable Equals 구현
		public bool Equals(ItemCode other)
		{
			ulong itemCode = GetItemCode();
			ulong otherItemCode = other.GetItemCode();
			return itemCode == otherItemCode;
		}

		public static bool operator ==(ItemCode lhs, ItemCode rhs)
		{
			return lhs.Equals(rhs);
		}

		public static bool operator !=(ItemCode lhs, ItemCode rhs)
		{
			return !(lhs.Equals(rhs));
		}

		public static ItemCode GenerateItemCode(int itemCode)
		{
			ItemCode ret = new ItemCode();
			ret.Code = (ulong)itemCode << 24;
			ret.ExpireTimesc = 0;
			return ret;
		}

		// 필드 범위를 넘거나 gap이 음수면 false를 반환하고 code는 기존 값이다. 예외를 던지지 않는다.
		public bool TryIncreaseCount(int gap, out ulong code)
		{
			code = Code;
			if (gap < 0 || GetItemAmount() + (ulong)gap >= 0x0000000000010000)
			{
				return false;
			}
			code = Code + (ulong)gap;
			return true;
		}
		public bool TryIncreaseEnchant(int gap, out ulong code)
		{
			code = Code;
			if (gap < 0 || GetItemEnchantCount() + (ulong)gap >= 0x0000000000000100)
			{
				return false;
			}
			code = Code + ((ulong)gap << 16);
			return true;
		}
		public bool TryDecreaseCount(int gap, out ulong code)
		{
			code = Code;
			if (gap < 0 || (ulong)gap > GetItemAmount())
			{
				return false;
			}
			code = Code - (ulong)gap;
			return true;
		}

		// 실패하면 기존 값을 그대로 반환한다. 성공 여부가 필요하면 Try 메서드를 쓴다.
		public ulong IncreaseCount(int gap = 1) => TryIncreaseCount(gap, out ulong code) ? code : Code;
		public ulong IncreaseEnchant(int gap = 1) => TryIncreaseEnchant(gap, out ulong code) ? code : Code;
		public ulong DecreaseCount(int gap = 1) => TryDecreaseCount(gap, out ulong code) ? code : Code;

		public ulong GetItemCode()
		{
			ulong ItemIDMask = 0x00FFFFFFFF000000;
			return (Code & ItemIDMask) >> 24;
		}

		public ulong GetItemRarity()
		{
			ulong ItemRarityMask = 0x000000F000000000;
			return (Code & ItemRarityMask) >> 36;
		}
		public ulong GetItemEquipSlot()
		{
			ulong ItemEquipSlotMask = 0x0000000F00000000;
			return (Code & ItemEquipSlotMask) >> 32;
		}
		public ulong GetItemJobCode()
		{
			ulong ItemJobCodeMask = 0x00000000FF000000;
			return (Code & ItemJobCodeMask) >> 24;
		}
		public ulong GetItemEnchantCount()
		{
			ulong ItemEnchantCountMask = 0x0000000000FF0000;
			return (Code & ItemEnchantCountMask) >> 16;
		}
		public ulong GetItemAmount()
		{
			ulong ItemEnchantCountMask = 0x000000000000FFFF;
			return (Code & ItemEnchantCountMask);
		}
	}
	// 서버 UserStageQuery 사본(2026-10-31). CurrentStage는 현재 메인 스테이지, HighestStage는 이번 환생 주기 최고 도달(환생 시 1-1),
	// MainClear는 환생에도 유지되는 영구 최고 클리어(0은 없음)다. 클리어 판정은 "스테이지 ID <= MainClear"다.
	public sealed class UserStageQuery
	{
		[JsonProperty(Required = Required.Always)]
		public eStage CurrentStage { get; set; }
		[JsonProperty(Required = Required.Always)]
		public eStage HighestStage { get; set; }
		[JsonProperty(Required = Required.Always)]
		public eStage MainClear { get; set; }
	}

	public class UserDataQuery
	{
		public UserDataQuery(int level, long exp, long monsterkilled)
		{
			Level = level;
			Exp = exp;
			MonsterKilled = monsterkilled;

			// 과거 저장소 시간 필드의 호환성만 유지하며 현재 시각을 읽지 않는다.
			dtLastLoginTime = DateTime.UnixEpoch;
		}

		public int Level { get; set; }
		public long Exp { get; set; }
		public long MonsterKilled { get; set; }
		//Time
		public DateTime dtLastLoginTime { get; set; }
		public long LastLogInTime { get; set; }
		public long LastRewardTime { get; set; }

		public string PasreToJson()
		{
			return JsonConvert.SerializeObject(this);
		}
	}
	public class UserEnhanceMentQuery
	{
		public UserEnhanceMentQuery(ulong enhancementHp, ulong enhancementatk)
		{
			EnhancementHp = enhancementHp;
			EnhancementAtk = enhancementatk;
		}

		public ulong EnhancementHp { get; set; }
		public ulong EnhancementAtk { get; set; }

		public string PasreToJson()
		{
			return JsonConvert.SerializeObject(this);
		}
	}
	public class CharacterDataQuery
	{
		public CharacterDataQuery(string nickname, ulong jobcode, long hp, long atk, int index)
		{
			NickName = nickname;
			JobCode = jobcode;
			Hp = hp;
			Atk = atk;
			Index = index;
		}
		[JsonProperty(Required = Required.Always)]
		public int Index { get; set; }
		[JsonProperty(Required = Required.Always)]
		public string NickName { get; set; }
		[JsonProperty(Required = Required.Always)]
		public ulong JobCode { get; set; }
		[JsonProperty(Required = Required.Always)]
		public long Hp { get; set; }
		[JsonProperty(Required = Required.Always)]
		public long Atk { get; set; }
		public string PasreToJson()
		{
			return JsonConvert.SerializeObject(this);
		}
	}
	public class JobTreeQuery
	{
		//캐릭터 인덱스
		[JsonProperty(Required = Required.Always)]
		public int Index { get; set; }
		[JsonProperty(Required = Required.Always)]
		public List<ulong> JobList { get; set; } = null!;
	}
	//캐릭터가 장착한 장비 정보
	public class CharacterEquipmentQuery
	{
		public CharacterEquipmentQuery(int characternum, ulong itemcode)
		{
			CharacterNum = characternum;
			ItemCode = itemcode;
		}
		[JsonProperty(Required = Required.Always)]
		public int CharacterNum { get; set; }
		[JsonProperty(Required = Required.Always)]
		public ulong ItemCode { get; set; }
		// 0이면 무제한. 서버가 항상 보낸다.
		[JsonProperty(Required = Required.Always)]
		public ulong ExpireTimesc { get; set; }
		public string PasreToJson()
		{
			return JsonConvert.SerializeObject(this);
		}
	}

	public class CurrencyQueryDTO
	{
		public CurrencyQueryDTO (long gold, long ancientCoin, long arcaneKnowledge, long classFragment, long ruby = 0, long kingdomSupply = 0)
		{
			Gold = gold;
			AncientCoin = ancientCoin;
			ArcaneKnowledge = arcaneKnowledge;
			ClassFragment = classFragment;
			Ruby = ruby;
			KingdomSupply = kingdomSupply;
		}
		// S02 additions. The bootstrap remains closed until the complete contract is implemented.
		public long KingdomSupply { get; set; }
		public long TrainingTome { get; set; }
		public long EquipmentStone { get; set; }
		// 장비·마탑 뽑기권(2026-11-01, 상점 일일 광고 보상). 광고 보상 이전 서버에는 없으므로 필수로 두지 않는다.
		public long EquipmentTicket { get; set; }
		public long MageTicket { get; set; }
		[JsonProperty(Required = Required.Always)]
		public long Ruby { get; set; } //특별 무료재화
		[JsonProperty(Required = Required.Always)]
		public long Gold {get;set;} //기본적인 강화
		[JsonProperty(Required = Required.Always)]
		public long AncientCoin { get;set;} //유료재화
		[JsonProperty(Required = Required.Always)]
		public long ArcaneKnowledge { get;set;} //마탑스킬강화
		[JsonProperty(Required = Required.Always)]
		public long ClassFragment { get;set;} // 전직재화

		public string PasreToJson()
		{
			return JsonConvert.SerializeObject(this);
		}
	}

	public class InventoryQueryDTO
	{
		[JsonProperty(Required = Required.Always)]
		public List<ItemCode> Items { get; set; } = null!;
		public string PasreToJson()
		{
			return JsonConvert.SerializeObject(this);
		}
	}

	public class SkillTreeDTO
	{ 
		[JsonProperty(Required = Required.Always)]
		public List<SkillCode> SkillTrees { get; set; } = null!;
		public string PasreToJson()
		{ 
			return JsonConvert.SerializeObject(this);
		}
	}

	// ---- 아래는 서버 QueryDTO.cs 상세 상태 DTO의 사본(2026-10-25). 응답 전용이며 저장 문서(MailBoxDTO 등)는 제외한다. ----
	// 모든 필드는 Required.Always이고, 서버가 nullable(T?)로 선언한 필드만 Required.AllowNull이다.
	// eCurrency는 전역 enum(Wallet)이 먼저 바인딩되므로 서버 사본 Protocol.Message.Data.eCurrency로 완전 수식한다.

	// 장비 묶음. 종류·강화·만료가 같은 장비를 ItemCode의 수량 비트로 묶는다.
	public sealed class EquipmentStackDTO
	{
		[JsonProperty(Required = Required.Always)]
		public ItemCode ItemCode { get; set; }
		[JsonProperty(Required = Required.Always)]
		public bool IsLocked { get; set; }
		// 이 묶음의 장비 1개당 실제 강화석 지출.
		[JsonProperty(Required = Required.Always)]
		public long EnhancementStonesSpent { get; set; }
		[JsonProperty(Required = Required.Always)]
		public List<int> EquippedCharacterIndexes { get; set; } = null!;
		[JsonIgnore]
		public int DefinitionId => (int)ItemCode.GetItemCode();
		[JsonIgnore]
		public int EnhancementLevel => (int)ItemCode.GetItemEnchantCount();
		[JsonIgnore]
		public int Count => (int)ItemCode.GetItemAmount();
	}
	// 예비 보관 묶음. 종류·강화·만료를 식별하는 코드이며 예비 수량의 기준은 Count다.
	public sealed class EquipmentReserveStackDTO
	{
		[JsonProperty(Required = Required.Always)]
		public ItemCode ItemCode { get; set; }
		[JsonProperty(Required = Required.Always)]
		public int Count { get; set; }
		[JsonProperty(Required = Required.Always)]
		public bool IsLocked { get; set; }
	}
	public sealed class EquipmentInventoryDTO
	{
		[JsonProperty(Required = Required.Always)]
		public List<EquipmentStackDTO> Items { get; set; } = null!;
		[JsonProperty(Required = Required.Always)]
		public List<EquipmentStackDTO> PendingItems { get; set; } = null!;
		[JsonProperty(Required = Required.Always)]
		public List<EquipmentReserveStackDTO> ReserveStacks { get; set; } = null!;
		[JsonProperty(Required = Required.Always)]
		public int Capacity { get; set; }
		[JsonProperty(Required = Required.Always)]
		public int PendingCapacity { get; set; }
		// bit0 Normal, bit1 Rare, bit2 Epic.
		[JsonProperty(Required = Required.Always)]
		public int AutoDismantleRarityMask { get; set; }
	}
	public sealed class MageSkillDTO
	{
		[JsonProperty(Required = Required.Always)]
		public int SkillId { get; set; }
		[JsonProperty(Required = Required.Always)]
		public int EnhancementLevel { get; set; }
		[JsonProperty(Required = Required.Always)]
		public int AwakeningLevel { get; set; }
		[JsonProperty(Required = Required.Always)]
		public int Fragments { get; set; }
		[JsonProperty(Required = Required.Always)]
		public long KnowledgeSpent { get; set; }
		[JsonProperty(Required = Required.Always)]
		public bool BloomEnabled { get; set; }
	}
	public sealed class MageTowerStateDTO
	{
		[JsonProperty(Required = Required.Always)]
		public List<MageSkillDTO> Skills { get; set; } = null!;
		// 슬롯 5칸. 빈 슬롯은 -1이다.
		[JsonProperty(Required = Required.Always)]
		public int[] EquippedSkillIds { get; set; } = null!;
	}
	public enum RewardKind { Equipment, Skill, Currency }
	public enum EquipmentDisposition { Inventory, Pending, Reserve, AutoDismantled, None }
	// 가챠·사냥 드롭·우편 수령 보상 1건. Kind에 따라 DefinitionId/ItemCode(장비·스킬) 또는 CurrencyType(재화)가 채워진다.
	public sealed class GachaRewardDTO
	{
		[JsonProperty(Required = Required.Always)]
		public int Order { get; set; }
		[JsonProperty(Required = Required.Always)]
		public RewardKind Kind { get; set; }
		[JsonProperty(Required = Required.AllowNull)]
		public int? DefinitionId { get; set; }
		[JsonProperty(Required = Required.AllowNull)]
		public Protocol.Message.Data.eCurrency? CurrencyType { get; set; }
		[JsonProperty(Required = Required.Always)]
		public long Amount { get; set; }
		[JsonProperty(Required = Required.Always)]
		public bool IsDuplicate { get; set; }
		[JsonProperty(Required = Required.Always)]
		public int GrantedFragments { get; set; }
		[JsonProperty(Required = Required.AllowNull)]
		public ItemCode? ItemCode { get; set; }
		[JsonProperty(Required = Required.Always)]
		public EquipmentDisposition Disposition { get; set; }
		[JsonProperty(Required = Required.Always)]
		public long GrantedEquipmentStone { get; set; }
	}
	public sealed class GachaStateDTO
	{
		// 장비 가챠 천장 카운터(0~39). 40번째는 Epic 확정.
		[JsonProperty(Required = Required.Always)]
		public int EquipmentPity { get; set; }
	}
	public sealed class DungeonStateDTO
	{
		[JsonProperty(Required = Required.Always)]
		public int GoldTickets { get; set; }
		[JsonProperty(Required = Required.Always)]
		public int RubyTickets { get; set; }
		[JsonProperty(Required = Required.Always)]
		public long TicketResetUtc { get; set; }
		[JsonProperty(Required = Required.Always)]
		public int GoldDungeonClear { get; set; }
		[JsonProperty(Required = Required.Always)]
		public int RubyDungeonClear { get; set; }
		// 진행 중인 던전 전투. 없으면 둘 다 null이다.
		[JsonProperty(Required = Required.AllowNull)]
		public string ActiveBattleId { get; set; }
		[JsonProperty(Required = Required.AllowNull)]
		public long? ActiveDungeonId { get; set; }
	}
	public enum QuestCategory { Guide, Daily, Weekly, Achievement }
	public enum QuestRowState { Locked, InProgress, Claimable, Claimed }
	public sealed class QuestRewardDTO
	{
		[JsonProperty(Required = Required.Always)]
		public RewardKind Kind { get; set; }
		[JsonProperty(Required = Required.AllowNull)]
		public Protocol.Message.Data.eCurrency? CurrencyType { get; set; }
		[JsonProperty(Required = Required.AllowNull)]
		public int? DefinitionId { get; set; }
		[JsonProperty(Required = Required.Always)]
		public decimal Amount { get; set; }
		// 사냥 효율에 따라 서버가 지급 시점에 계산하는 골드 보상.
		[JsonProperty(Required = Required.Always)]
		public bool IsDynamicGold { get; set; }
	}
	public sealed class QuestRowDTO
	{
		[JsonProperty(Required = Required.Always)]
		public long QuestId { get; set; }
		// "permanent" 또는 KST 기준 yyyy-MM-dd. 수령 요청에 그대로 넣는다.
		[JsonProperty(Required = Required.Always)]
		public string PeriodKey { get; set; } = null!;
		[JsonProperty(Required = Required.Always)]
		public QuestCategory Category { get; set; }
		[JsonProperty(Required = Required.Always)]
		public string Title { get; set; } = null!;
		[JsonProperty(Required = Required.Always)]
		public string Description { get; set; } = null!;
		[JsonProperty(Required = Required.Always)]
		public string PresentationType { get; set; } = null!;
		[JsonProperty(Required = Required.Always)]
		public string ObjectiveType { get; set; } = null!;
		[JsonProperty(Required = Required.Always)]
		public long TargetId { get; set; }
		[JsonProperty(Required = Required.Always)]
		public long RequiredCount { get; set; }
		[JsonProperty(Required = Required.Always)]
		public long Progress { get; set; }
		[JsonProperty(Required = Required.Always)]
		public QuestRowState State { get; set; }
		[JsonProperty(Required = Required.Always)]
		public bool IsPending { get; set; }
		[JsonProperty(Required = Required.Always)]
		public long ExpiresUtc { get; set; }
		[JsonProperty(Required = Required.Always)]
		public List<QuestRewardDTO> Rewards { get; set; } = null!;
	}
	public sealed class QuestStateDTO
	{
		[JsonProperty(Required = Required.Always)]
		public long ServerTimeUtc { get; set; }
		[JsonProperty(Required = Required.Always)]
		public string DailyPeriodKey { get; set; } = null!;
		[JsonProperty(Required = Required.Always)]
		public long DailyResetUtc { get; set; }
		[JsonProperty(Required = Required.Always)]
		public string WeeklyPeriodKey { get; set; } = null!;
		[JsonProperty(Required = Required.Always)]
		public long WeeklyResetUtc { get; set; }
		[JsonProperty(Required = Required.Always)]
		public List<QuestRowDTO> Rows { get; set; } = null!;
	}
	public sealed class ReincarnationStateDTO
	{
		[JsonProperty(Required = Required.Always)]
		public int Level { get; set; }
		[JsonProperty(Required = Required.Always)]
		public long Count { get; set; }
		// Day(KST yyyy-MM-dd)에 수행한 환생 횟수. 날짜가 바뀌면 0부터 다시 센다.
		[JsonProperty(Required = Required.Always)]
		public int TodayCount { get; set; }
		[JsonProperty(Required = Required.Always)]
		public string Day { get; set; } = null!;
		[JsonProperty(Required = Required.Always)]
		public long LastUtc { get; set; }
	}
	// 월별 출석판(2026-10-31). KST 하루에 한 칸이 열리고 한 달에 최대 28칸이다. 출석은 칸만 열고 보상은 OnClaimAttendanceReward로 받는다.
	// 출석판 필드(Month·OpenedDays·ClaimedDays)는 출석판 이전 서버와도 로그인이 되도록 필수로 두지 않는다.
	public sealed class AttendanceStateDTO
	{
		// 서로 다른 KST 출석일의 누적 수.
		[JsonProperty(Required = Required.Always)]
		public long TotalDays { get; set; }
		[JsonProperty(Required = Required.Always)]
		public string LastDay { get; set; } = null!;
		// 출석판의 KST 달(yyyy-MM). 아직 판을 시작하지 않았으면 빈 문자열이다.
		public string Month { get; set; } = "";
		// 이번 달에 열린 칸 수(0~28). 1일차부터 OpenedDays일차까지 열려 있다.
		public int OpenedDays { get; set; }
		// 보상을 받은 칸 번호(1부터, 오름차순).
		public List<int> ClaimedDays { get; set; } = new List<int>();
	}
	// 상점 일일 광고 보상의 오늘(KST) 수령 기록(2026-11-01). 광고 보상 이전 서버와도 로그인이 되도록 필수로 두지 않는다.
	public sealed class AdRewardStateDTO
	{
		// 수령 기록의 KST 날짜(yyyy-MM-dd). 받은 적이 없으면 빈 문자열이다.
		public string Day { get; set; } = "";
		// Day에 받은 상점 광고 상품 id(rewarded.daily_equipment, rewarded.daily_mage).
		public List<string> ClaimedOfferIds { get; set; } = new List<string>();
	}
	// 재화 종류와 수량(서버 CurrencyAmountDTO 사본). 광고 보상 응답의 GrantedCurrencies에 쓴다.
	public sealed class CurrencyAmountDTO
	{
		// 전역 eCurrency(Wallet)가 먼저 바인딩되므로 서버 사본으로 완전 수식한다.
		[JsonProperty(Required = Required.Always)]
		public Protocol.Message.Data.eCurrency CurrencyType { get; set; }
		[JsonProperty(Required = Required.Always)]
		public long Amount { get; set; }
	}
	// 이번 요청으로 바뀐 장비 묶음의 처리 후 상태(2026-10-31, 출석 보상 수령). 보관 위치별로 같은 묶음(장비 종류·강화·만료)을 덮어쓰거나 추가한다.
	public sealed class EquipmentChangesDTO
	{
		[JsonProperty(Required = Required.Always)]
		public List<EquipmentStackDTO> Items { get; set; } = null!;
		[JsonProperty(Required = Required.Always)]
		public List<EquipmentStackDTO> PendingItems { get; set; } = null!;
		[JsonProperty(Required = Required.Always)]
		public List<EquipmentReserveStackDTO> ReserveStacks { get; set; } = null!;
	}
	public enum MailCategory { System, Attendance }
	// 우편 첨부. 재화는 CurrencyType, 장비는 DefinitionId로 대상을 정한다.
	public sealed class MailRewardDTO
	{
		[JsonProperty(Required = Required.Always)]
		public RewardKind Kind { get; set; }
		[JsonProperty(Required = Required.AllowNull)]
		public Protocol.Message.Data.eCurrency? CurrencyType { get; set; }
		[JsonProperty(Required = Required.AllowNull)]
		public int? DefinitionId { get; set; }
		[JsonProperty(Required = Required.Always)]
		public long Amount { get; set; }
	}
	public sealed class MailDTO
	{
		[JsonProperty(Required = Required.Always)]
		public long MailId { get; set; }
		[JsonProperty(Required = Required.Always)]
		public MailCategory Category { get; set; }
		// 본문 대신 클라이언트 현지화 키만 온다.
		[JsonProperty(Required = Required.Always)]
		public string TitleKey { get; set; } = null!;
		[JsonProperty(Required = Required.Always)]
		public string BodyKey { get; set; } = null!;
		[JsonProperty(Required = Required.Always)]
		public long SentUtc { get; set; }
		[JsonProperty(Required = Required.Always)]
		public long ExpiresUtc { get; set; }
		[JsonProperty(Required = Required.Always)]
		public List<MailRewardDTO> Rewards { get; set; } = null!;
	}
}
