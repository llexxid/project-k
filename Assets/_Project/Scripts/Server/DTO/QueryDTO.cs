using IdleKingdomFunction.Common.enums;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IdleKingdomFunction.Common.Dto
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
	public class UserCurrentStageQuery
	{
		public eStage CurrentStage { get; set; }
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
}
